using System;
using System.Collections.Generic;

namespace Dragonfly.Utils
{
    public class MemoryPool : IMemoryPool
    {
        static readonly byte[] EmptyArray = new byte[0];

        readonly Stack<byte[]> _pool = new Stack<byte[]>();
        readonly object _poolSync = new object();
        private const int PoolLimit = 256;

        readonly Stack<byte[]> _pool2 = new Stack<byte[]>();
        readonly object _pool2Sync = new object();
        private const int Pool2Limit = 64;

        public byte[] Empty
        {
            get { return EmptyArray; }
        }

        public byte[] Alloc(int minimumSize)
        {
            if (minimumSize == 0)
            {
                return EmptyArray;
            }
            if (minimumSize <= 1024)
            {
                lock (_poolSync)
                {
                    if (_pool.Count != 0)
                        return _pool.Pop();
                }
                return new byte[1024];
            }
            if (minimumSize <= 2048)
            {
                lock (_pool2Sync)
                {
                    if (_pool2.Count != 0)
                        return _pool2.Pop();
                }
                return new byte[2048];
            }
            return new byte[minimumSize];
        }

        public void Free(byte[] memory)
        {
            if (memory == null) return;
            switch (memory.Length)
            {
                case 1024:
                    lock(_poolSync)
                    {
                        if (_pool.Count < PoolLimit)
                        {
                            _pool.Push(memory);
                        }
                    }
                    break;
                case 2048:
                    lock (_pool2Sync)
                    {
                        if (_pool2.Count < Pool2Limit)
                        {
                            _pool2.Push(memory);
                        }
                    }
                    break;
            }
        }
    }
}
