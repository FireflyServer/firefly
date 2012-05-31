using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Firefly.Utils
{
    public interface ISocket
    {
        bool Blocking { get; set; }
        bool NoDelay { get; set; }
        bool Connected { get; }
        EndPoint LocalEndPoint { get; }
        EndPoint RemoteEndPoint { get; }

        int Receive(byte[] buffer, int offset, int size, SocketFlags socketFlags, out SocketError errorCode);
        bool ReceiveAsync(ISocketEvent socketEvent);

        int Send(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags, out SocketError errorCode);
        bool SendAsync(ISocketEvent socketEvent);

        void Shutdown(SocketShutdown how);
        bool DisconnectAsync(SocketAsyncEventArgs e);
        void Close();
    }

    public interface ISocketEvent : IDisposable
    {
        byte[] Buffer { get; }
        int Offset { get; }
        int Count { get; }
        IList<ArraySegment<byte>> BufferList { get; set; }
        void SetBuffer(byte[] buffer, int offset, int count);

        Action Completed { get; set; }

        SocketAsyncOperation LastOperation { get; }
        SocketError SocketError { get; }
        int BytesTransferred { get; }
    }
}
