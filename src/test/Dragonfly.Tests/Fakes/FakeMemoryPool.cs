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

        public byte[] Alloc(int minimumSize)
        {
            return new byte[minimumSize];
        }

        public void Free(byte[] memory)
        {            
        }
    }
}