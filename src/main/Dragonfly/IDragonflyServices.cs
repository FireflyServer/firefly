using Dragonfly.Utils;

namespace Dragonfly
{
    public interface IDragonflyServices
    {
        IServerTrace Trace { get; }
        IMemoryPool Memory { get; }
    }
}
