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

        private Baton _baton;
        private Frame _frame;

        private Action<Exception> _fault;
        private Action _frameConsumeCallback;
        private SocketAsyncEventArgs _socketReceiveAsyncEventArgs;

        public Connection(IServerTrace trace, AppDelegate app, Socket socket)
        {
            _trace = trace;
            _app = app;
            _socket = socket;
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

            _socketReceiveAsyncEventArgs = new SocketAsyncEventArgs();
            _socketReceiveAsyncEventArgs.SetBuffer(new byte[0], 0, 0);
            _socketReceiveAsyncEventArgs.Completed +=
                (_, __) =>
                {
                    try
                    {
                        Go();
                    }
                    catch (Exception ex)
                    {
                        _fault(ex);
                    }
                };

            _frameConsumeCallback =
                () =>
                {
                    try
                    {
                        Go();
                    }
                    catch (Exception ex)
                    {
                        _fault(ex);
                    }
                };
            try
            {
                _socket.Blocking = false;
                Go();
            }
            catch (Exception ex)
            {
                _fault(ex);
            }
        }


        public void Go()
        {
            for (; ; )
            {
                if (_baton.Next == Next.NewFrame)
                {
                    _baton.Buffer = new ArraySegment<byte>(new byte[1024], 0, 0);
                    _frame = new Frame(_app, ProduceData, ProduceEnd);
                    _baton.Next = Next.ReadMore;
                }

                if (_baton.Next == Next.ReadMore)
                {
                    SocketError recvError;
                    var buffer = _baton.Available(128);
                    var receiveCount = _socket.Receive(
                        buffer.Array,
                        buffer.Offset,
                        buffer.Count,
                        SocketFlags.None,
                        out recvError);

                    if (recvError == SocketError.WouldBlock)
                    {
                        if (_socket.ReceiveAsync(_socketReceiveAsyncEventArgs))
                            return;

                        continue;
                    }

                    if (recvError != SocketError.Success || receiveCount == 0)
                    {
                        _baton.Complete = true;
                    }
                    else
                    {
                        _baton.Extend(receiveCount);
                    }

                    if (_frame.Consume(
                        _baton,
                        _frameConsumeCallback,
                        _fault))
                    {
                        return;
                    }
                }

                if (_baton.Next == Next.CloseConnection)
                {
                    _socketReceiveAsyncEventArgs.Dispose();
                    _socketReceiveAsyncEventArgs = null;

                    _socket.Shutdown(SocketShutdown.Receive);
                    _socket.Close();
                    // todo: method to decrement vitality, pairs with shutdown-to-send

                    _trace.Event(TraceEventType.Stop, TraceMessage.Connection);
                    return;
                }
            }
        }

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
            e.Completed +=
                (_, __) =>
                {
                    e.Dispose();
                    callback(new SendInfo { BytesSent = e.BytesTransferred, SocketError = e.SocketError });
                };
            e.SetBuffer(data.Array, data.Offset, data.Count);
            var delayed = _socket.SendAsync(e);

            if (delayed)
            {
                return new SendInfo { SocketError = SocketError.IOPending };
            }

            e.Dispose();
            return new SendInfo { BytesSent = e.BytesTransferred, SocketError = e.SocketError };
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
