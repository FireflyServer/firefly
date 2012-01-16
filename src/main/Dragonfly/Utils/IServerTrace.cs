using System;
using System.Diagnostics;

namespace Dragonfly.Utils
{
    public enum TraceMessage
    {
        // TraceEventType.Start || TraceEventType.Stop
        ServerFactory,
        Connection,

        // TraceEventType.Verbose
        ServerFactoryAcceptAsync,
        ServerFactoryAcceptCompletedAsync,
        ServerFactoryAcceptCompletedSync,

        // TraceEventType.Information
        ServerFactoryConnectionExecute,

        // TraceEventType.Warning
        ConnectionSendSocketError,

        // TraceEventType.Error
        ServerFactoryAcceptSocketError
    };

    public interface IServerTrace
    {
        void Event(TraceEventType type, TraceMessage message);
    }
}
