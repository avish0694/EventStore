using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.TestClient.Commands
{
    internal class SubscribeToStreamProcessor : ICmdProcessor
    {
        public string Usage { get { return "SUBSCR [<stream_1> <stream_2> ... <stream_n>]"; } }
        public string Keyword { get { return "SUBSCR"; } }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            context.IsAsync();

            var streamByCorrId = new Dictionary<Guid, string>();

            var connection = context.Client.CreateTcpConnection(
                    context,
                    connectionEstablished: conn =>
                    {
                    },
                    handlePackage: (conn, pkg) =>
                    {
                        switch (pkg.Command)
                        {
                            case TcpCommand.SubscriptionConfirmation:
                            {
                                var dto = pkg.Data.Deserialize<TcpClientMessageDto.SubscriptionConfirmation>();
                                context.Log.Info("Subscription to <{@streambyCorrId}> WAS CONFIRMED! Subscribed at {@lastCommitPosition} ({@lastEventNumber})", 
                                                 streamByCorrId[pkg.CorrelationId], dto.LastCommitPosition, dto.LastEventNumber);
                                break;
                            }
                            case TcpCommand.StreamEventAppeared:
                            {
                                var dto = pkg.Data.Deserialize<TcpClientMessageDto.StreamEventAppeared>();
                                context.Log.Info("NEW EVENT:\n\n"
                                                 + "\tEventStreamId: {0}\n"
                                                 + "\tEventNumber:   {1}\n"
                                                 + "\tEventType:     {2}\n"
                                                 + "\tData:          {3}\n"
                                                 + "\tMetadata:      {4}\n",
                                                 dto.Event.Event.EventStreamId,
                                                 dto.Event.Event.EventNumber,
                                                 dto.Event.Event.EventType,
                                                 Common.Utils.Helper.UTF8NoBom.GetString(dto.Event.Event.Data ?? new byte[0]),
                                                 Common.Utils.Helper.UTF8NoBom.GetString(dto.Event.Event.Metadata ?? new byte[0])); /*TODO: structured-log @shaan1337: seems like no changes are required here, just review.*/
                                break;
                            }
                            case TcpCommand.SubscriptionDropped:
                            {
                                pkg.Data.Deserialize<TcpClientMessageDto.SubscriptionDropped>();
                                context.Log.Error("Subscription to <{@fixthisvar}> WAS DROPPED!", streamByCorrId[pkg.CorrelationId]); /*TODO: structured-log @avish0694: the following parameters need attention: {0}*/
                                break;
                            }
                            default:
                                context.Fail(reason: string.Format("Unexpected TCP package: {0}.", pkg.Command));
                                break;
                        }
                    },
                    connectionClosed: (c, error) =>
                    {
                        if (error == SocketError.Success)
                            context.Success();
                        else
                            context.Fail();
                    });

            if (args.Length == 0)
            {
                context.Log.Info("SUBSCRIBING TO ALL STREAMS...");
                var cmd = new TcpClientMessageDto.SubscribeToStream(string.Empty, resolveLinkTos: false);
                Guid correlationId = Guid.NewGuid();
                streamByCorrId[correlationId] = "$all";
                connection.EnqueueSend(new TcpPackage(TcpCommand.SubscribeToStream, correlationId, cmd.Serialize()).AsByteArray());
            }
            else
            {
                foreach (var stream in args)
                {
                    context.Log.Info("SUBSCRIBING TO STREAM <{@stream}>...", stream);
                    var cmd = new TcpClientMessageDto.SubscribeToStream(stream, resolveLinkTos: false);
                    var correlationId = Guid.NewGuid();
                    streamByCorrId[correlationId] = stream;
                    connection.EnqueueSend(new TcpPackage(TcpCommand.SubscribeToStream, correlationId, cmd.Serialize()).AsByteArray());
                }
            }

            context.WaitForCompletion();
            return true;
        }
    }
}