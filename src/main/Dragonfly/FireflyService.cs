using Firefly.Utils;

namespace Firefly
{
    public class FireflyService : IFireflyService
    {
        public FireflyService()
        {
            Trace = NullServerTrace.Instance;
            Memory = new MemoryPool();
        }

        public IServerTrace Trace { get; set; }
        public IMemoryPool Memory { get; set; }
    }
}