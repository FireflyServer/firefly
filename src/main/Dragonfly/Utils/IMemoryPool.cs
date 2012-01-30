namespace Firefly.Utils
{
    public interface IMemoryPool
    {
        byte[] Empty { get; }

        byte[] AllocByte(int minimumSize);
        void FreeByte(byte[] memory);

        char[] AllocChar(int minimumSize);
        void FreeChar(char[] memory);
    }
}