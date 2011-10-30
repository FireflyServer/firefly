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
        ServerFactoryEndAccept,
        ServerFactoryBeginAccept,

        // TraceEventType.Information
        ServerFactoryConnectionExecute,
    };

    public interface IServerTrace
    {
        void Event(TraceEventType type, TraceMessage message);
    }
}
