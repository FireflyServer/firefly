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

        public FakeSocket()
        {
            Encoding = Encoding.Default;
        }

        public Encoding Encoding { get; set; }

        public bool Paused { get; set; }
        public SocketAsyncEventArgs ReceivePausedArgs { get; set; }

        public void Add(string text)
        {
            _input = Combine(_input, new ArraySegment<byte>(Encoding.GetBytes(text)));
            if (Paused && _input.Count != 0)
            {
                var args = ReceivePausedArgs;
                Paused = false;
                ReceivePausedArgs = null;

                SocketError errorCode;
                var bytesTransferred = Receive(
                    args.Buffer,
                    args.Offset,
                    args.Count,
                    args.SocketFlags,
                    out errorCode);

                SetField(args, "m_BytesTransferred", bytesTransferred);
                SetField(args, "m_SocketError", errorCode);
                var callback = (ContextCallback)GetField(args, "m_ExecutionCallback");
                callback(args);
            }
        }

        ArraySegment<byte> Combine(ArraySegment<byte> arr0, ArraySegment<byte> arr1)
        {
            var combined = new ArraySegment<byte>(new byte[arr0.Count + arr1.Count]);
            Array.Copy(arr0.Array, arr0.Offset, combined.Array, 0, arr0.Count);
            Array.Copy(arr1.Array, arr1.Offset, combined.Array, arr0.Count, arr1.Count);
            return combined;
        }

        // ISocket follows

        public bool Blocking { get; set; }
        
        public bool Connected { get; private set; }

        public string Output
        {
            get { return Encoding.GetString(_output.Array, _output.Offset, _output.Count); }
        }

        public int Receive(byte[] buffer, int offset, int size, SocketFlags socketFlags, out SocketError errorCode)
        {
            if (Paused)
                throw new InvalidOperationException("FakeSocket.Receive cannot be called when ReceivePaused is true");

            if (_input.Count == 0)
            {
                errorCode = SocketError.WouldBlock;
                return 0;
            }

            var bytesTransferred = Math.Min(size, _input.Count);
            Array.Copy(_input.Array, _input.Offset, buffer, offset, bytesTransferred);
            _input = new ArraySegment<byte>(
                _input.Array, 
                _input.Offset + bytesTransferred, 
                _input.Count - bytesTransferred);
            errorCode = SocketError.Success;
            return bytesTransferred;
        }

        public bool ReceiveAsync(SocketAsyncEventArgs e)
        {
            if (Paused)
                throw new InvalidOperationException("FakeSocket.Receive cannot be called when ReceivePaused is true");

            Paused = true;
            ReceivePausedArgs = e;
            ThreadPool.QueueUserWorkItem(_ => Add(""));
            return true;
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
            _output = Combine(_output, new ArraySegment<byte>(buffer, offset, size));
            errorCode = SocketError.Success;
            return size;
        }

        public bool SendAsync(SocketAsyncEventArgs e)
        {
            _output = Combine(_output, new ArraySegment<byte>(e.Buffer, e.Offset, e.Count));
            SetField(e, "m_SocketError", SocketError.Success);
            SetField(e, "m_BytesTransferred", e.Count);
            return false;
        }

        public void WaitToSend()
        {
            throw new NotImplementedException();
        }

        public void Shutdown(SocketShutdown how)
        {
            throw new NotImplementedException();
        }

        public bool DisconnectAsync(SocketAsyncEventArgs e)
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            throw new NotImplementedException();
        }
    }
}
