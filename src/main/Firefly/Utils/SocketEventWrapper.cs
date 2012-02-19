using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace Firefly.Utils
{
    public class SocketEventWrapper : ISocketEvent
    {
        private readonly SocketAsyncEventArgs _socketEvent;
        private Action _completed;
        private static readonly Action Noop = () => { };

        public SocketEventWrapper(SocketAsyncEventArgs socketEvent)
        {
            _socketEvent = socketEvent;
            SocketAsyncEventArgs.Completed += SocketEventCompleted;
            Completed = Noop;
        }

        void SocketEventCompleted(object sender, SocketAsyncEventArgs e)
        {
            Completed.Invoke();
        }

        public SocketAsyncEventArgs SocketAsyncEventArgs
        {
            get
            {
                return _socketEvent;
            }
        }

        public void Dispose()
        {
            SocketAsyncEventArgs.Dispose();
        }

        public byte[] Buffer
        {
            get
            {
                return SocketAsyncEventArgs.Buffer;
            }
        }

        public int Offset
        {
            get
            {
                return SocketAsyncEventArgs.Offset;
            }
        }

        public int Count
        {
            get
            {
                return SocketAsyncEventArgs.Count;
            }
        }

        public IList<ArraySegment<byte>> BufferList
        {
            get
            {
                return SocketAsyncEventArgs.BufferList;
            }
            set
            {
                SocketAsyncEventArgs.BufferList = value;
            }
        }

        public void SetBuffer(byte[] buffer, int offset, int count)
        {
            SocketAsyncEventArgs.SetBuffer(buffer, offset, count);
        }


        public Action Completed
        {
            get
            {
                return _completed;
            }
            set
            {
                _completed = value ?? Noop;
            }
        }

        public SocketAsyncOperation LastOperation
        {
            get
            {
                return SocketAsyncEventArgs.LastOperation;
            }
        }

        public SocketError SocketError
        {
            get
            {
                return SocketAsyncEventArgs.SocketError;
            }
        }

        public int BytesTransferred
        {
            get
            {
                return SocketAsyncEventArgs.BytesTransferred;
            }
        }
    }
}
