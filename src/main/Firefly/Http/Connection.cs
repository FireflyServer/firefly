using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using Firefly.Utils;
using Owin;

namespace Firefly.Http
{
    public class Connection : IAsyncResult
    {
        private readonly IFireflyService _services;
        private readonly AppDelegate _app;
        private readonly ISocket _socket;
        private readonly Action<ISocket> _disconnected;

        private Baton _baton;
        private Frame _frame;

        private Action<Exception> _fault;
        private Action _frameConsumeCallback;
        private SocketAsyncEventArgs _socketReceiveAsyncEventArgs;

        public Connection(IFireflyService services, AppDelegate app, ISocket socket, Action<ISocket> disconnected)
        {
            _services = services;
            _app = app;
            _socket = socket;
            _disconnected = disconnected;
        }

        public void Execute()
        {
            _services.Trace.Event(TraceEventType.Start, TraceMessage.Connection);

            _baton = new Baton(_services.Memory);

            _fault = ex =>
                         {
                             Debug.WriteLine(ex.Message);
                         };

            _socketReceiveAsyncEventArgs = new SocketAsyncEventArgs();
            _socketReceiveAsyncEventArgs.SetBuffer(_services.Memory.Empty, 0, 0);
            _socketReceiveAsyncEventArgs.Completed +=
                (_, __) =>
                {
                    try
                    {
                        Go(false);
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
                        Go(false);
                    }
                    catch (Exception ex)
                    {
                        _fault(ex);
                    }
                };
            try
            {
                _socket.Blocking = false;
                Go(true);
            }
            catch (Exception ex)
            {
                _fault(ex);
            }
        }


        private void Go(bool newFrame)
        {
            var frame = _frame;
            if (newFrame)
            {
                frame = _frame = new Frame(new FrameContext
                {
                    Services = _services,
                    App = _app,
                    Write = data => ProduceData(data, null),
                    Flush = _ => false,
                    End = ProduceEnd
                });

                if (_baton.Buffer.Count != 0)
                {
                    if (frame.Consume(
                        _baton,
                        _frameConsumeCallback,
                        _fault))
                    {
                        return;
                    }
                }
            }

            while (frame.LocalIntakeFin == false)
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
                    _baton.Free();
                    if (_socket.ReceiveAsync(_socketReceiveAsyncEventArgs))
                        return;

                    continue;
                }

                if (recvError != SocketError.Success || receiveCount == 0)
                {
                    _baton.RemoteIntakeFin = true;
                }
                else
                {
                    _baton.Extend(receiveCount);
                }

                if (frame.Consume(
                    _baton,
                    _frameConsumeCallback,
                    _fault))
                {
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
            // Rogue value implies shutdown send (used for 1.0 clients)
            if (data.Array == null)
            {
                _socket.Shutdown(SocketShutdown.Send);
                return false;
            }

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
                    _services.Trace.Event(TraceEventType.Warning, TraceMessage.ConnectionSendSocketError);
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
                //Socket.Select(null, new List<Socket> { _socket }, null, -1);
                _socket.WaitToSend();
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
                            _services.Trace.Event(TraceEventType.Warning, TraceMessage.ConnectionSendSocketError);
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
                    _services.Trace.Event(TraceEventType.Warning, TraceMessage.ConnectionSendSocketError);
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

        private void ProduceEnd(ProduceEndType endType)
        {
            switch (endType)
            {
                case ProduceEndType.SocketShutdownSend:
                    _socket.Shutdown(SocketShutdown.Send);
                    break;
                case ProduceEndType.ConnectionKeepAlive:
                    ThreadPool.QueueUserWorkItem(_ => Go(true));
                    break;
                case ProduceEndType.SocketDisconnect:
                    _services.Trace.Event(TraceEventType.Stop, TraceMessage.Connection);

                    _baton.Free();

                    _socketReceiveAsyncEventArgs.Dispose();
                    _socketReceiveAsyncEventArgs = null;
                    _socket.Shutdown(SocketShutdown.Receive);

                    var e = new SocketAsyncEventArgs();
                    Action cleanup =
                        () =>
                        {
                            e.Dispose();
                            _disconnected(_socket);
                        };

                    e.Completed += (_, __) => cleanup();
                    if (!_socket.DisconnectAsync(e))
                    {
                        cleanup();
                    }
                    break;
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
