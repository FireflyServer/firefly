using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dragonfly.Utils;

namespace Dragonfly.Tests.Fakes
{
    public class FakeServices : IDragonflyServices
    {
        public FakeServices()
        {
            Trace = new FakeTrace();
            Memory = new FakeMemoryPool();
        }
        public IServerTrace Trace { get; private set; }
        public IMemoryPool Memory { get; private set; }
    }
}
