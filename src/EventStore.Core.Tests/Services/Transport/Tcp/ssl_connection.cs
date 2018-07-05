using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Core.Tests.Helpers;
using EventStore.Transport.Tcp;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Tcp
{
    [TestFixture, Category("LongRunning")]
    public class ssl_connections
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<ssl_connections>();

        [Test]
        public void should_connect_to_each_other_and_send_data()
        {
            var ip = IPAddress.Loopback;
            var port = PortsHelper.GetAvailablePort(ip);
            var serverEndPoint = new IPEndPoint(ip, port);
            X509Certificate cert = GetCertificate();

            var sent = new byte[1000];
            new Random().NextBytes(sent);

            var received = new MemoryStream();

            var done = new ManualResetEventSlim();

            var listener = new TcpServerListener(serverEndPoint);
            listener.StartListening((endPoint, socket) =>
            {
                var ssl = TcpConnectionSsl.CreateServerFromSocket(Guid.NewGuid(), endPoint, socket, cert, verbose: true);
                ssl.ConnectionClosed += (x, y) => done.Set();
                if (ssl.IsClosed) done.Set();
                
                Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback = null;
                callback = (x, y) =>
                {
                    foreach (var arraySegment in y)
                    {
                        received.Write(arraySegment.Array, arraySegment.Offset, arraySegment.Count);
                        Log.Info("Received: {@arraySegment} bytes, total: {@received}.", arraySegment.Count, received.Length);
                    }

                    if (received.Length >= sent.Length)
                    {
                        Log.Info("Done receiving..."); /*TODO: structured-log @Lougarou: seems like no changes are required here, just review.*/
                        done.Set();
                    }
                    else
                    {
                        Log.Info("Receiving..."); /*TODO: structured-log @shaan1337: seems like no changes are required here, just review.*/
                        ssl.ReceiveAsync(callback);
                    }
                };
                Log.Info("Receiving..."); /*TODO: structured-log @avish0694: seems like no changes are required here, just review.*/
                ssl.ReceiveAsync(callback);
            }, "Secure");

            var clientSsl = TcpConnectionSsl.CreateConnectingConnection(
                Guid.NewGuid(), 
                serverEndPoint, 
                "ES",
                false,
                new TcpClientConnector(),
                TcpConnectionManager.ConnectionTimeout,
                conn =>
                {
                    Log.Info("Sending bytes..."); /*TODO: structured-log @Lougarou: seems like no changes are required here, just review.*/
                    conn.EnqueueSend(new[] {new ArraySegment<byte>(sent)});
                },
                (conn, err) =>
                {
                    Log.Error("Connecting failed: {@err}.", err);
                    done.Set();
                },
                verbose: true);

            Assert.IsTrue(done.Wait(20000), "Took too long to receive completion.");

            Log.Info("Stopping listener..."); /*TODO: structured-log @shaan1337: seems like no changes are required here, just review.*/
            listener.Stop();
            Log.Info("Closing client ssl connection..."); /*TODO: structured-log @avish0694: seems like no changes are required here, just review.*/
            clientSsl.Close("Normal close.");
            Log.Info("Checking received data..."); /*TODO: structured-log @Lougarou: seems like no changes are required here, just review.*/
            Assert.AreEqual(sent, received.ToArray());
        }

        public static X509Certificate2 GetCertificate()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EventStore.Core.Tests.server.p12"))
            using (var mem = new MemoryStream())
            {
                stream.CopyTo(mem);
                return new X509Certificate2(mem.ToArray(), "1111");
            }
        }
    }
}
