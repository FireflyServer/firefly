using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Firefly.Utils;

namespace Firefly.Tests.Fakes
{
    public class FakeSocketEvent : ISocketEvent
    {
        public void Dispose()
        {
        }

        public byte[] Buffer { get; set; }
        public int Offset { get; set; }
        public int Count { get; set; }
        public IList<ArraySegment<byte>> BufferList { get; set; }
        public void SetBuffer(byte[] buffer, int offset, int count)
        {
            Buffer = buffer;
            Offset = offset;
            Count = count;
        }

        public Action Completed { get; set; }
        public SocketAsyncOperation LastOperation { get; set; }
        public SocketError SocketError { get; set; }
        public int BytesTransferred { get; set; }
    }
}