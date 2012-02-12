using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace Firefly.Utils
{
    public interface ISocket
    {
        bool Blocking { get; set; }
        bool NoDelay { get; set; }
        bool Connected { get; }

        int Receive(byte[] buffer, int offset, int size, SocketFlags socketFlags, out SocketError errorCode);
        bool ReceiveAsync(SocketAsyncEventArgs e);

        int Send(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags, out SocketError errorCode);
        bool SendAsync(SocketAsyncEventArgs e);
        
        void Shutdown(SocketShutdown how);
        bool DisconnectAsync(SocketAsyncEventArgs e);
        void Close();
    }

}
