using System;

namespace Dragonfly.Http
{
    public class Baton
    {
        public ArraySegment<byte> Buffer { get; set; }
        public bool Complete { get; set; }
        public Connection.Next Next { get; set; }


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

        public ArraySegment<byte> Available(int minimumSize)
        {
            if (Buffer.Count == 0 && Buffer.Offset != 0)
                Buffer = new ArraySegment<byte>(Buffer.Array, 0, 0);

            var availableSize = Buffer.Array.Length - Buffer.Offset - Buffer.Count;

            if (availableSize < minimumSize)
            {
                if (availableSize + Buffer.Offset >= minimumSize && false)
                {
                    Array.Copy(Buffer.Array, Buffer.Offset, Buffer.Array, 0, Buffer.Count);
                    Buffer = new ArraySegment<byte>(Buffer.Array, 0, Buffer.Count);
                    availableSize = Buffer.Array.Length - Buffer.Offset - Buffer.Count;
                }
                else
                {
                    var larger = new ArraySegment<byte>(new byte[Buffer.Array.Length * 2 + minimumSize], 0, Buffer.Count);
                    Array.Copy(Buffer.Array, Buffer.Offset, larger.Array, 0, Buffer.Count);
                    Buffer = larger;
                    availableSize = Buffer.Array.Length - Buffer.Offset - Buffer.Count;
                }
            }
            return new ArraySegment<byte>(Buffer.Array, Buffer.Offset + Buffer.Count, availableSize);
        }

        public void Extend(int count)
        {
            Buffer = new ArraySegment<byte>(Buffer.Array, Buffer.Offset, Buffer.Count + count);
        }

    }
}
