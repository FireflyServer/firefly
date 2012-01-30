using System;
using System.Net.Sockets;

namespace Firefly.Utils
{
    public interface ISocket
    {
        bool Blocking { get; set; }
        bool Connected { get; }

        int Receive(byte[] buffer, int offset, int size, SocketFlags socketFlags, out SocketError errorCode);
        bool ReceiveAsync(SocketAsyncEventArgs e);

        int Send(byte[] buffer, int offset, int size, SocketFlags socketFlags, out SocketError errorCode);
        bool SendAsync(SocketAsyncEventArgs e);
        
        void WaitToSend();

        void Shutdown(SocketShutdown how);
        bool DisconnectAsync(SocketAsyncEventArgs e);
        void Close();
    }
}
