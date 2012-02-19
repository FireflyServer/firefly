using System;
using System.Diagnostics;
using Firefly.Utils;

namespace Firefly.Http
{
    public class Baton
    {
        private readonly IMemoryPool _memory;

        public Baton(IMemoryPool memory)
        {
            _memory = memory;
            Buffer = new ArraySegment<byte>(_memory.Empty, 0, 0);
        }

        public ArraySegment<byte> Buffer { get; set; }
        public bool RemoteIntakeFin { get; set; }


        public void Skip(int count)
        {
            Buffer = new ArraySegment<byte>(Buffer.Array, Buffer.Offset + count, Buffer.Count - count);
        }

        public ArraySegment<byte> Take(int count)
        {
            var taken = new ArraySegment<byte>(Buffer.Array, Buffer.Offset, count);
            Skip(count);
            return taken;
        }

        public void Free()
        {
            if (Buffer.Count == 0 && Buffer.Array.Length != 0)
            {
                _memory.FreeByte(Buffer.Array);
                Buffer = new ArraySegment<byte>(_memory.Empty, 0, 0);
            }
        }

        public ArraySegment<byte> Available(int minimumSize)
        {
            if (Buffer.Count == 0 && Buffer.Offset != 0)
            {
                Buffer = new ArraySegment<byte>(Buffer.Array, 0, 0);
            }

            var availableSize = Buffer.Array.Length - Buffer.Offset - Buffer.Count;

            if (availableSize < minimumSize)
            {
                if (availableSize + Buffer.Offset >= minimumSize)
                {
                    Array.Copy(Buffer.Array, Buffer.Offset, Buffer.Array, 0, Buffer.Count);
                    if (Buffer.Count != 0)
                    {
                        Buffer = new ArraySegment<byte>(Buffer.Array, 0, Buffer.Count);
                    }
                    availableSize = Buffer.Array.Length - Buffer.Offset - Buffer.Count;
                }
                else
                {
                    var largerSize = Buffer.Array.Length + Math.Max(Buffer.Array.Length, minimumSize);
                    var larger = new ArraySegment<byte>(_memory.AllocByte(largerSize), 0, Buffer.Count);
                    if (Buffer.Count != 0)
                    {
                        Array.Copy(Buffer.Array, Buffer.Offset, larger.Array, 0, Buffer.Count);
                    }
                    _memory.FreeByte(Buffer.Array);
                    Buffer = larger;
                    availableSize = Buffer.Array.Length - Buffer.Offset - Buffer.Count;
                }
            }
            return new ArraySegment<byte>(Buffer.Array, Buffer.Offset + Buffer.Count, availableSize);
        }

        public void Extend(int count)
        {
            Debug.Assert(count >= 0);
            Debug.Assert(Buffer.Offset >= 0);
            Debug.Assert(Buffer.Offset <= Buffer.Array.Length);
            Debug.Assert(Buffer.Offset + Buffer.Count <= Buffer.Array.Length);
            Debug.Assert(Buffer.Offset + Buffer.Count + count <= Buffer.Array.Length);

            Buffer = new ArraySegment<byte>(Buffer.Array, Buffer.Offset, Buffer.Count + count);
        }
    }
}
