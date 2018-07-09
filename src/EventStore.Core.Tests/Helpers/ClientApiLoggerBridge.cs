using System;
using EventStore.Common.Log;
using EventStore.Common.Utils;

namespace EventStore.Core.Tests.Helpers
{
    public class ClientApiLoggerBridge : EventStore.ClientAPI.ILogger
    {
        public static readonly ClientApiLoggerBridge Default = new ClientApiLoggerBridge(LogManager.GetLogger("client-api"));

        private readonly ILogger _log;

        public ClientApiLoggerBridge(ILogger log)
        {
            Ensure.NotNull(log, "log");
            _log = log;
        }

        public void Error(string format, params object[] args)
        {
            if (args.Length == 0)
                _log.Error(format);
            else
                _log.Error(format, args); /*TODO: structured-log @shaan1337: unrecognized format, content string not found*/
        }

        public void Error(Exception ex, string format, params object[] args)
        {
            if (args.Length == 0)
                _log.ErrorException(ex, format); /*TODO: structured-log @avish0694: unrecognized format, content string not found*/
            else
                _log.ErrorException(ex, format, args);
        }

        public void Info(string format, params object[] args)
        {
            if (args.Length == 0)
                _log.Info(format); /*TODO: structured-log @shaan1337: unrecognized format, content string not found*/
            else
                _log.Info(format, args); /*TODO: structured-log @avish0694: unrecognized format, content string not found*/
        }

        public void Info(Exception ex, string format, params object[] args)
        {
            if (args.Length == 0)
                _log.InfoException(ex, format);
            else
                _log.InfoException(ex, format, args); /*TODO: structured-log @shaan1337: unrecognized format, content string not found*/
        }

        public void Debug(string format, params object[] args)
        {
            if (args.Length == 0)
                _log.Debug(format); /*TODO: structured-log @avish0694: unrecognized format, content string not found*/
            else
                _log.Debug(format, args);
        }

        public void Debug(Exception ex, string format, params object[] args)
        {
            if (args.Length == 0)
                _log.DebugException(ex, format); /*TODO: structured-log @shaan1337: unrecognized format, content string not found*/
            else
                _log.DebugException(ex, format, args); /*TODO: structured-log @avish0694: unrecognized format, content string not found*/
        }
    }
}
