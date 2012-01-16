using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using Dragonfly.Utils;
using Gate.Owin;

namespace Dragonfly.Http
{
    public class Connection : IAsyncResult
    {
        private readonly IServerTrace _trace;
        private readonly AppDelegate _app;
        private readonly Socket _socket;
        private unsafe NativeOverlapped* _recvOverlapped;

        private Baton _baton;
        private Frame _frame;
        private int _receiveCount;
        private SocketError _socketError;

        private Action<Exception> _fault;
        private Action _frameConsumeCallback;

        public Connection(IServerTrace trace, AppDelegate app, Socket socket)
        {
            _trace = trace;
            _app = app;
            _socket = socket;
            Init();
        }

        private unsafe void Init()
        {
            _recvOverlapped = new Overlapped { AsyncResult = this }.Pack(CompletionCallback, null);
        }
        private unsafe void Term()
        {
            Overlapped.Free(_recvOverlapped);
        }

        public enum Next
        {
            ReadMore,
            NewFrame,
            CloseConnection,
        };

        public void Execute()
        {
            _trace.Event(TraceEventType.Start, TraceMessage.Connection);

            _baton = new Baton
                         {
                             Buffer = new ArraySegment<byte>(new byte[1024], 0, 0),
                             Next = Next.NewFrame,
                         };
            _fault = ex =>
                         {
                             Debug.WriteLine(ex.Message);
                         };

            _frameConsumeCallback =
                () =>
                {
                    try
                    {
                        Go(2);
                    }
                    catch (Exception ex)
                    {
                        _fault(ex);
                    }
                };



            try
            {
                _socket.Blocking = false;
                //ThreadPool.BindHandle(_socket.Handle);
                //Go(0);
                _socket.BeginSend(
                    new byte[0],
                    0,
                    0,
                    SocketFlags.None,
                    ar =>
                    {
                        try
                        {
                            _socket.EndSend(ar, out _socketError);
                            Go(0);
                        }
                        catch (Exception ex)
                        {
                            _fault(ex);
                        }
                    }, null);
            }
            catch (Exception ex)
            {
                _fault(ex);
            }
        }


        public unsafe void Go(int marker)
        {
            switch (marker)
            {
                case 0:
                    goto marker0;
                case 1:
                    goto marker1;
                case 2:
                    goto marker2;
                case 3:
                    goto marker3;
            }
        marker0:

            if (_baton.Next == Next.NewFrame)
            {
                _baton.Buffer = new ArraySegment<byte>(new byte[1024], 0, 0);
                _frame = new Frame(_app, ProduceData, ProduceEnd);
                _baton.Next = Next.ReadMore;
            }

            if (_baton.Next == Next.CloseConnection)
            {
                //Term();
                _socket.Shutdown(SocketShutdown.Receive);
                // todo: method to decrement vitality, pairs with shutdown-to-send

                _trace.Event(TraceEventType.Stop, TraceMessage.Connection);
                return;
            }

        marker4:
            uint numberOfBytesRecvd;
            SocketFlags flags;
            int recvResult;
            SocketError recvError;
            WSABUF wsabuf;
            var buffer = _baton.Available(128);
            fixed (byte* p = &buffer.Array[buffer.Offset])
            {
                wsabuf = new WSABUF
                             {
                                 buf = new IntPtr(p),
                                 len = (uint)buffer.Count
                             };
                flags = SocketFlags.None;
                numberOfBytesRecvd = 0;
                recvResult = WSARecv(
                    _socket.Handle,
                    ref wsabuf,
                    1,
                    ref numberOfBytesRecvd,
                    ref flags,
                    null,
                    IntPtr.Zero);

                recvError = recvResult == -1 ? (SocketError)Marshal.GetLastWin32Error() : SocketError.Success;
            }

            if (recvError == SocketError.Success)
            {
                _receiveCount = (int)numberOfBytesRecvd;
                goto marker1;
            }
            if (recvError != SocketError.WouldBlock)
            {
                _receiveCount = 0;
                goto marker1;
            }

            wsabuf = new WSABUF { buf = IntPtr.Zero, len = 0 };
            flags = SocketFlags.None;
            numberOfBytesRecvd = 0;
            recvResult = WSARecv(
                _socket.Handle,
                ref wsabuf,
                1,
                ref numberOfBytesRecvd,
                ref flags,
                _recvOverlapped,
                IntPtr.Zero);
            recvError = recvResult == -1 ? (SocketError)Marshal.GetLastWin32Error() : SocketError.Success;
            if (recvError == SocketError.IOPending)
                return;

            _receiveCount = recvError == SocketError.Success ? (int)numberOfBytesRecvd : 0;
        marker3:
            if (_receiveCount == 0)
                goto marker4;

        marker1:
            if (_receiveCount == 0)
            {
                _baton.Complete = true;
            }
            else
            {
                _baton.Extend(_receiveCount);
            }

            if (_frame.Consume(
                _baton,
                _frameConsumeCallback,
                _fault))
            {
                return;
            }

        marker2:
            goto marker0;
        }


        private static readonly unsafe IOCompletionCallback CompletionCallback = CompletionCallbackMethod;


        private static unsafe void CompletionCallbackMethod(uint errorcode, uint numbytes, NativeOverlapped* poverlap)
        {
            var overlapped = Overlapped.Unpack(poverlap);
            var self = (Connection)overlapped.AsyncResult;
            self._receiveCount = (int)numbytes;
            self.Go(3);
        }

        public struct WSABUF
        {
            public UInt32 len;
            public IntPtr buf;
        }

        [DllImport("ws2_32.dll", SetLastError = true)]
        public extern static unsafe int WSARecv(
            IntPtr s,
            ref WSABUF lpBuffers,
            UInt32 dwBufferCount,
            ref UInt32 lpNumberOfBytesRecvd,
            ref SocketFlags lpFlags,
            NativeOverlapped* lpOverlapped,
            IntPtr lpCompletionRoutine);


        struct SendInfo
        {
            public int BytesSent { get; set; }
            public SocketError SocketError { get; set; }
        }

        private bool ProduceData(ArraySegment<byte> data, Action callback)
        {
            if (callback == null)
            {
                DoProduce(data);
                return false;
            }

            return DoProduce(data, callback);
        }

        private void DoProduce(ArraySegment<byte> data)
        {
            var remaining = data;

            while (remaining.Count != 0)
            {
                SocketError errorCode;
                var sent = _socket.Send(remaining.Array, remaining.Offset, remaining.Count, SocketFlags.None, out errorCode);
                if (errorCode != SocketError.Success)
                {
                    _trace.Event(TraceEventType.Warning, TraceMessage.ConnectionSendSocketError);
                    break;
                }
                if (sent == remaining.Count)
                {
                    break;
                }

                remaining = new ArraySegment<byte>(
                    remaining.Array,
                    remaining.Offset + sent,
                    remaining.Count - sent);

                // BLOCK - enters a wait state for sync output waiting for kernel buffer to be writable
                Socket.Select(null, new List<Socket> { _socket }, null, -1);
            }
        }

        // ReSharper disable AccessToModifiedClosure
        private bool DoProduce(ArraySegment<byte> data, Action callback)
        {
            var remaining = data;

            while (remaining.Count != 0)
            {
                var info = DoSend(
                    remaining,
                    asyncInfo =>
                    {
                        if (asyncInfo.SocketError != SocketError.Success)
                        {
                            _trace.Event(TraceEventType.Warning, TraceMessage.ConnectionSendSocketError);
                            callback();
                            return;
                        }
                        if (asyncInfo.BytesSent == remaining.Count)
                        {
                            callback();
                            return;
                        }

                        if (!DoProduce(
                            new ArraySegment<byte>(
                                remaining.Array,
                                remaining.Offset + asyncInfo.BytesSent,
                                remaining.Count - asyncInfo.BytesSent),
                            callback))
                        {
                            callback();
                        }
                    });
                if (info.SocketError == SocketError.IOPending)
                {
                    return true;
                }
                if (info.SocketError != SocketError.Success)
                {
                    _trace.Event(TraceEventType.Warning, TraceMessage.ConnectionSendSocketError);
                    break;
                }
                if (info.BytesSent == remaining.Count)
                {
                    break;
                }

                remaining = new ArraySegment<byte>(
                        remaining.Array,
                        remaining.Offset + info.BytesSent,
                        remaining.Count - info.BytesSent);
            }
            return false;
        }
        // ReSharper restore AccessToModifiedClosure

        private SendInfo DoSend(ArraySegment<byte> data, Action<SendInfo> callback)
        {
            var e = new SocketAsyncEventArgs();
            e.Completed += (_, __) => callback(new SendInfo { BytesSent = e.BytesTransferred, SocketError = e.SocketError });
            e.SetBuffer(data.Array, data.Offset, data.Count);
            var delayed = _socket.SendAsync(e);

            return delayed
                ? new SendInfo { SocketError = SocketError.IOPending }
                : new SendInfo { BytesSent = e.BytesTransferred, SocketError = e.SocketError };
        }

        private void ProduceEnd()
        {
            //TODO keep-alive
            if (_socket.Connected)
            {
                _socket.Shutdown(SocketShutdown.Send);
            }
        }


        bool IAsyncResult.IsCompleted
        {
            get { throw new NotImplementedException(); }
        }

        WaitHandle IAsyncResult.AsyncWaitHandle
        {
            get { throw new NotImplementedException(); }
        }

        object IAsyncResult.AsyncState
        {
            get { throw new NotImplementedException(); }
        }

        bool IAsyncResult.CompletedSynchronously
        {
            get { throw new NotImplementedException(); }
        }
    }
}
