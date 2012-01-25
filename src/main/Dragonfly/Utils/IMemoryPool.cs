namespace Dragonfly.Utils
{
    public interface IMemoryPool
    {
        byte[] Empty { get; }
        byte[] Alloc(int minimumSize);
        void Free(byte[] memory);
    }
}