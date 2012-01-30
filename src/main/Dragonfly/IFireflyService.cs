using Firefly.Utils;

namespace Firefly
{
    public interface IFireflyService
    {
        IServerTrace Trace { get; }
        IMemoryPool Memory { get; }
    }
}
