using System;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using Dragonfly.Utils;

namespace Dragonfly.Tests.Fakes
{
    public class FakeSocket : ISocket
    {
        private ArraySegment<byte> _input = new ArraySegment<byte>(new byte[0]);
        private ArraySegment<byte> _output = new ArraySegment<byte>(new byte[0]);
        private readonly object _receiveLock = new object();
        private readonly object _sendLock = new object();

        public FakeSocket()
        {
            Encoding = Encoding.Default;
        }

        public Encoding Encoding { get; set; }

        public bool ReceiveAsyncPaused { get; set; }
        public SocketAsyncEventArgs ReceiveAsyncArgs { get; set; }

        public bool DisconnectCalled { get; set; }
        public bool ShutdownSendCalled { get; set; }
        public bool ShutdownReceiveCalled { get; set; }


        public string Input
        {
            get { lock (_receiveLock) { return Encoding.GetString(_input.Array, _input.Offset, _input.Count); } }
        }
        public string Output
        {
            get { return Encoding.GetString(_output.Array, _output.Offset, _output.Count); }
        }

        public void Add(string text)
        {
            ContextCallback callback;
            lock (_receiveLock)
            {
                var buffer = new ArraySegment<byte>(Encoding.GetBytes(text));
                var combined = new ArraySegment<byte>(new byte[_input.Count + buffer.Count]);
                Array.Copy(_input.Array, _input.Offset, combined.Array, 0, _input.Count);
                Array.Copy(buffer.Array, buffer.Offset, combined.Array, _input.Count, buffer.Count);
                _input = combined;
                callback = TryReceiveAsync();
            }
            if (callback != null)
            {
                callback(null);
            }
        }

        int TakeInput(ArraySegment<byte> buffer)
        {
            lock (_receiveLock)
            {
                var bytesTransferred = Math.Min(buffer.Count, _input.Count);
                Array.Copy(_input.Array, _input.Offset, buffer.Array, buffer.Offset, bytesTransferred);
                _input = new ArraySegment<byte>(
                    _input.Array,
                    _input.Offset + bytesTransferred,
                    _input.Count - bytesTransferred);
                return bytesTransferred;
            }
        }
        private int GiveOutput(ArraySegment<byte> buffer)
        {
            var bytesTransfered = buffer.Count;
            var combined = new ArraySegment<byte>(new byte[_output.Count + buffer.Count]);
            Array.Copy(_output.Array, _output.Offset, combined.Array, 0, _output.Count);
            Array.Copy(buffer.Array, buffer.Offset, combined.Array, _output.Count, buffer.Count);
            _output = combined;
            return bytesTransfered;
        }

        ContextCallback TryReceiveAsync()
        {
            lock (_receiveLock)
            {
                if (ReceiveAsyncArgs == null || _input.Count == 0) return null;

                var args = ReceiveAsyncArgs;
                ReceiveAsyncPaused = false;
                ReceiveAsyncArgs = null;

                var bytesTransferred = TakeInput(new ArraySegment<byte>(args.Buffer, args.Offset, args.Count));
                SetField(args, "m_BytesTransferred", bytesTransferred);
                SetField(args, "m_SocketError", SocketError.Success);
                return (ContextCallback)GetField(args, "m_ExecutionCallback");
            }
        }


        // ISocket follows

        public bool Blocking { get; set; }

        public bool Connected { get; private set; }


        public int Receive(byte[] buffer, int offset, int size, SocketFlags socketFlags, out SocketError errorCode)
        {
            lock (_receiveLock)
            {
                if (ReceiveAsyncPaused)
                    throw new InvalidOperationException("FakeSocket.Receive cannot be called when ReceiveCalled is true");

                var bytesTransferred = TakeInput(new ArraySegment<byte>(buffer, offset, size));
                errorCode = bytesTransferred == 0 ? SocketError.WouldBlock : SocketError.Success;
                return bytesTransferred;
            }
        }

        public bool ReceiveAsync(SocketAsyncEventArgs e)
        {
            lock (_receiveLock)
            {
                if (ReceiveAsyncPaused)
                    throw new InvalidOperationException("FakeSocket.Receive cannot be called when ReceiveCalled is true");

                ReceiveAsyncPaused = true;
                ReceiveAsyncArgs = e;
                return TryReceiveAsync() == null;
            }
        }

        private static void SetField(object obj, string fieldName, object fieldValue)
        {
            var fieldInfo = obj.GetType().GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.SetField | BindingFlags.Instance);
            fieldInfo.SetValue(obj, fieldValue);
        }
        private static object GetField(object obj, string fieldName)
        {
            var fieldInfo = obj.GetType().GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
            return fieldInfo.GetValue(obj);
        }

        public int Send(byte[] buffer, int offset, int size, SocketFlags socketFlags, out SocketError errorCode)
        {
            lock (_sendLock)
            {
                var byteTransfered = GiveOutput(new ArraySegment<byte>(buffer, offset, size));
                errorCode = byteTransfered == 0 ? SocketError.Success : SocketError.WouldBlock;
                return byteTransfered;
            }
        }


        public bool SendAsync(SocketAsyncEventArgs e)
        {
            lock (_sendLock)
            {
                var byteTransfered = GiveOutput(new ArraySegment<byte>(e.Buffer, e.Offset, e.Count));
                var errorCode = byteTransfered == 0 ? SocketError.WouldBlock : SocketError.Success;

                SetField(e, "m_SocketError", errorCode);
                SetField(e, "m_BytesTransferred", byteTransfered);
                return false;
            }
        }

        public void WaitToSend()
        {
            throw new NotImplementedException();
        }

        public void Shutdown(SocketShutdown how)
        {
            switch(how)
            {
                case SocketShutdown.Send:
                    ShutdownSendCalled = true;
                    break;
                case SocketShutdown.Receive:
                    ShutdownReceiveCalled = true;
                    break;
                case SocketShutdown.Both:
                    ShutdownSendCalled = true;
                    ShutdownReceiveCalled = true;
                    break;
            }
        }

        public bool DisconnectAsync(SocketAsyncEventArgs e)
        {
            DisconnectCalled = true;
            return false;
        }

        public void Close()
        {
            throw new NotImplementedException();
        }
    }
}
