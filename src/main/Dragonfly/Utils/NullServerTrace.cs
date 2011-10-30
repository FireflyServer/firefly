using System.Diagnostics;

namespace Dragonfly.Utils
{
    public class NullServerTrace : IServerTrace
    {
        public static readonly IServerTrace Instance = new NullServerTrace();

        public void Event(TraceEventType type, TraceMessage message)
        {            
        }
    }
}