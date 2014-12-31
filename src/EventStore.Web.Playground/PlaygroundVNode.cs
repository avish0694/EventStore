using System;
using System.Linq;
using System.Net;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Core.Settings;

namespace EventStore.Web.Playground
{
    public class PlaygroundVNode
    {
        private QueuedHandler MainQueue
        {
            get { return _mainQueue; }
        }

        private InMemoryBus Bus
        {
            get { return _mainBus; }
        }

        private HttpService HttpService
        {
            get { return _httpService; }
        }

        private TimerService TimerService
        {
            get { return _timerService; }
        }

        private readonly IPEndPoint _tcpEndPoint;
        private readonly IPEndPoint _httpEndPoint;

        private readonly QueuedHandler _mainQueue;
        private readonly InMemoryBus _mainBus;

        private readonly HttpService _httpService;
        private readonly TimerService _timerService;

        private readonly InMemoryBus[] _workerBuses;

        public PlaygroundVNode(PlaygroundVNodeSettings vNodeSettings)
        {
            _tcpEndPoint = vNodeSettings.ExternalTcpEndPoint;
            _httpEndPoint = vNodeSettings.ExternalHttpEndPoint;

            _mainBus = new InMemoryBus("MainBus");
            var controller = new PlaygroundVNodeController(Bus, _httpEndPoint);
            _mainQueue = new QueuedHandler(controller, "MainQueue");
            controller.SetMainQueue(MainQueue);

            // MONITORING
            var monitoringInnerBus = new InMemoryBus("MonitoringInnerBus", watchSlowMsg: false);
            var monitoringQueue = new QueuedHandler(
                monitoringInnerBus, "MonitoringQueue", true, TimeSpan.FromMilliseconds(100));
            Bus.Subscribe(monitoringQueue.WidenFrom<SystemMessage.SystemInit, Message>());
            Bus.Subscribe(monitoringQueue.WidenFrom<SystemMessage.StateChangeMessage, Message>());
            Bus.Subscribe(monitoringQueue.WidenFrom<SystemMessage.BecomeShuttingDown, Message>());


            // MISC WORKERS
            _workerBuses = Enumerable.Range(0, vNodeSettings.WorkerThreads).Select(queueNum =>
                new InMemoryBus(string.Format("Worker #{0} Bus", queueNum + 1),
                                watchSlowMsg: true,
                                slowMsgThreshold: TimeSpan.FromMilliseconds(50))).ToArray();
            var workersHandler = new MultiQueuedHandler(
                vNodeSettings.WorkerThreads,
                queueNum => new QueuedHandlerThreadPool(_workerBuses[queueNum],
                    string.Format("Worker #{0}", queueNum + 1),
                    groupName: "Workers",
                    watchSlowMsg: true,
                    slowMsgThreshold: TimeSpan.FromMilliseconds(50)));

            // AUTHENTICATION INFRASTRUCTURE
            var dispatcher = new IODispatcher(_mainBus, new PublishEnvelope(workersHandler, crossThread: true));
            var passwordHashAlgorithm = new Rfc2898PasswordHashAlgorithm();
            var internalAuthenticationProvider = new InternalAuthenticationProvider(dispatcher, passwordHashAlgorithm, 1000);
            var passwordChangeNotificationReader = new PasswordChangeNotificationReader(_mainQueue, dispatcher);
            _mainBus.Subscribe<SystemMessage.SystemStart>(passwordChangeNotificationReader);
            _mainBus.Subscribe<SystemMessage.BecomeShutdown>(passwordChangeNotificationReader);
            _mainBus.Subscribe(internalAuthenticationProvider);
            _mainBus.Subscribe(dispatcher);

            SubscribeWorkers(bus =>
            {
                bus.Subscribe(dispatcher.ForwardReader);
                bus.Subscribe(dispatcher.BackwardReader);
                bus.Subscribe(dispatcher.Writer);
                bus.Subscribe(dispatcher.StreamDeleter);
                bus.Subscribe(dispatcher.Awaker);
            });

            // TCP
            var tcpService = new TcpService(
                MainQueue, _tcpEndPoint, workersHandler, TcpServiceType.External, TcpSecurityType.Normal, new ClientTcpDispatcher(), 
                TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(1000), internalAuthenticationProvider, null);
            Bus.Subscribe<SystemMessage.SystemInit>(tcpService);
            Bus.Subscribe<SystemMessage.SystemStart>(tcpService);
            Bus.Subscribe<SystemMessage.BecomeShuttingDown>(tcpService);

            // HTTP
            {
                var httpAuthenticationProviders = new HttpAuthenticationProvider[]
                {
                    new BasicHttpAuthenticationProvider(internalAuthenticationProvider),
                    new TrustedHttpAuthenticationProvider(),
                    new AnonymousHttpAuthenticationProvider()
                };

                var httpPipe = new HttpMessagePipe();
                var httpSendService = new HttpSendService(httpPipe, forwardRequests: false);
                _mainBus.Subscribe<SystemMessage.StateChangeMessage>(httpSendService);
                _mainBus.Subscribe(new WideningHandler<HttpMessage.SendOverHttp, Message>(workersHandler));
                SubscribeWorkers(bus =>
                {
                    bus.Subscribe<HttpMessage.HttpSend>(httpSendService);
                    bus.Subscribe<HttpMessage.HttpSendPart>(httpSendService);
                    bus.Subscribe<HttpMessage.HttpBeginSend>(httpSendService);
                    bus.Subscribe<HttpMessage.HttpEndSend>(httpSendService);
                    bus.Subscribe<HttpMessage.SendOverHttp>(httpSendService);
                });

                _httpService = new HttpService(ServiceAccessibility.Private, _mainQueue, new TrieUriRouter(),
                                               workersHandler, vNodeSettings.HttpPrefixes);

                _mainBus.Subscribe<SystemMessage.SystemInit>(_httpService);
                _mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(_httpService);
                _mainBus.Subscribe<HttpMessage.PurgeTimedOutRequests>(_httpService);
                HttpService.SetupController(new AdminController(_mainQueue));
                HttpService.SetupController(new PingController());
                HttpService.SetupController(new StatController(monitoringQueue, workersHandler));
                HttpService.SetupController(new AtomController(httpSendService, _mainQueue, workersHandler));
                HttpService.SetupController(new UsersController(httpSendService, _mainQueue, workersHandler));

                SubscribeWorkers(bus => HttpService.CreateAndSubscribePipeline(bus, httpAuthenticationProviders));
            }


            // TIMER
            _timerService = new TimerService(new ThreadBasedScheduler(new RealTimeProvider()));
            Bus.Subscribe<TimerMessage.Schedule>(TimerService);

            monitoringQueue.Start();
            MainQueue.Start();
        }

        private void SubscribeWorkers(Action<InMemoryBus> setup)
        {
            foreach (var workerBus in _workerBuses)
            {
                setup(workerBus);
            }
        }

        public void Start()
        {
            MainQueue.Publish(new SystemMessage.SystemInit());
        }

        public void Stop(bool exitProcess, bool shutdownHttp)
        {
            MainQueue.Publish(new ClientMessage.RequestShutdown(exitProcess, shutdownHttp));
        }

        public override string ToString()
        {
            return string.Format("[{0}, {1}]", _tcpEndPoint, _httpEndPoint);
        }
    }
}
