using System;
using System.Diagnostics;
using Firefly.Utils;

namespace Firefly.Tests.Fakes
{
    public class FakeTrace : IServerTrace
    {
        public void Event(TraceEventType type, TraceMessage message)
        {
            Console.WriteLine("[{0} {1}]", type, message);
        }
    }
}
