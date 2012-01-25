using Dragonfly.Utils;

namespace Dragonfly
{
    public class DragonflyServices : IDragonflyServices
    {
        public DragonflyServices()
        {
            Trace = NullServerTrace.Instance;
            Memory = new MemoryPool();
        }

        public IServerTrace Trace { get; set; }
        public IMemoryPool Memory { get; set; }
    }
}