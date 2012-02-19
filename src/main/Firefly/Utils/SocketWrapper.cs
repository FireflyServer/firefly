using System;
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
            get
            {
                return _socket.Blocking;
            }
            set
            {
                _socket.Blocking = value;
            }
        }

        public bool NoDelay
        {
            get
            {
                return _socket.NoDelay;
            }
            set
            {
                _socket.NoDelay = value;
            }
        }

        public bool Connected
        {
            get
            {
                return _socket.Connected;
            }
        }

        public int Receive(byte[] buffer, int offset, int size, SocketFlags socketFlags, out SocketError errorCode)
        {
            return _socket.Receive(buffer, offset, size, socketFlags, out errorCode);
        }

        public bool ReceiveAsync(ISocketEvent socketEvent)
        {
            return _socket.ReceiveAsync(((SocketEventWrapper)socketEvent).SocketAsyncEventArgs);
        }

        public int Send(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags, out SocketError errorCode)
        {
            return _socket.Send(buffers, socketFlags, out errorCode);
        }

        public bool SendAsync(ISocketEvent socketEvent)
        {
            return _socket.SendAsync(((SocketEventWrapper)socketEvent).SocketAsyncEventArgs);
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
