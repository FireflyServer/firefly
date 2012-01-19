using System;
using System.Diagnostics;
using Dragonfly.Utils;

namespace Dragonfly.Tests.Fakes
{
    public class FakeTrace : IServerTrace
    {
        public void Event(TraceEventType type, TraceMessage message)
        {
            Console.WriteLine("[{0} {1}]", type, message);
        }
    }
}
