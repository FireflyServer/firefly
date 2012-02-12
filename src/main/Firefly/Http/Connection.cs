using System;
using System.Collections.Generic;
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
        private ISocketSender _socketSender;
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
            _socketSender = new SocketSender(_services, _socket);
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
                _socket.NoDelay = true;
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
                    Write = _socketSender.Write,
                    Flush = _socketSender.Flush,
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


        private void ProduceEnd(ProduceEndType endType)
        {
            Action drained =
                () =>
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
                    };

            if (!_socketSender.Flush(drained))
                drained.Invoke();
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
