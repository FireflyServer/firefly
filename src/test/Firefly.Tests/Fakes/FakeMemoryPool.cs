using System;
using Firefly.Utils;

namespace Firefly.Tests.Fakes
{
    public class FakeMemoryPool : IMemoryPool
    {
        public byte[] Empty
        {
            get { return new byte[0]; }
        }

        public int AllocByteCount { get; set; }
        public int FreeByteCount { get; set; }
        public int AllocCharCount { get; set; }
        public int FreeCharCount { get; set; }


        public byte[] AllocByte(int minimumSize)
        {
            ++AllocByteCount;
            return new byte[minimumSize];
        }

        public void FreeByte(byte[] memory)
        {
            if (memory != null && memory.Length != 0)
            {
                ++FreeByteCount;
            }
        }

        public char[] AllocChar(int minimumSize)
        {
            ++AllocCharCount;
            return new char[minimumSize];
        }

        public void FreeChar(char[] memory)
        {
            if (memory != null && memory.Length != 0)
            {
                ++FreeCharCount;
            }
        }

        public ArraySegment<byte> AllocSegment(int minimumSize)
        {
            return new ArraySegment<byte>(AllocByte(minimumSize));
        }

        public void FreeSegment(ArraySegment<byte> segment)
        {
            FreeByte(segment.Array);
        }

        public ISocketEvent AllocSocketEvent()
        {
            return new FakeSocketEvent();
        }

        public void FreeSocketEvent(ISocketEvent socketEvent)
        {            
        }
    }
}