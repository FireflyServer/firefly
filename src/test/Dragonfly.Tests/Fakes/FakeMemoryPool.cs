using System;
using Dragonfly.Utils;

namespace Dragonfly.Tests.Fakes
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
    }
}