using System.Collections.Generic;
using System.Net.Sockets;

namespace Firefly.Utils
{
    class SocketWrapper : ISocket
    {
        private readonly Socket _socket;

        public SocketWrapper(Socket socket)
        {
            _socket = socket;
        }

        public bool Blocking
        {
            get { return _socket.Blocking; }
            set { _socket.Blocking = value; }
        }

        public bool Connected
        {
            get { return _socket.Connected; }
        }

        public int Receive(byte[] buffer, int offset, int size, SocketFlags socketFlags, out SocketError errorCode)
        {
            return _socket.Receive(buffer, offset, size, socketFlags, out errorCode);
        }

        public bool ReceiveAsync(SocketAsyncEventArgs e)
        {
            return _socket.ReceiveAsync(e);
        }

        public int Send(byte[] buffer, int offset, int size, SocketFlags socketFlags, out SocketError errorCode)
        {
            return _socket.Send(buffer, offset, size, socketFlags, out errorCode);
        }

        public bool SendAsync(SocketAsyncEventArgs e)
        {
            return _socket.SendAsync(e);
        }

        public void WaitToSend()
        {
            Socket.Select(null, new List<Socket> { _socket }, null, -1);
        }

        public void Shutdown(SocketShutdown how)
        {
            _socket.Shutdown(how);
        }

        public bool DisconnectAsync(SocketAsyncEventArgs e)
        {
            return _socket.DisconnectAsync(e);
        }

        public void Close()
        {
            _socket.Close();
        }
    }
}