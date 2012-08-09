using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using Firefly.Utils;
using Owin;

namespace Firefly.Http
{
    public class Connection
    {
        private readonly IFireflyService _services;
        private readonly AppDelegate _app;
        private readonly ISocket _socket;
        private readonly ISocketSender _socketSender;
        private readonly Action<ISocket> _disconnected;

        private Baton _baton;
        private Frame _frame;

        private Action<Exception> _fault;
        private Action<Frame, Exception> _frameConsumeCallback;
        private ISocketEvent _receiveSocketEvent;
        private Action _receiveAsyncCompleted;
        private Frame _receiveAsyncCompletedFrame;

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

            _fault = ex => { Debug.WriteLine(ex.Message); };

            _receiveSocketEvent = _services.Memory.AllocSocketEvent();
            _receiveSocketEvent.SetBuffer(_services.Memory.Empty, 0, 0);


            _frameConsumeCallback = (frame,error) =>
            {
                if (error!=null)
                {
                    _fault(error);
                }
                try
                {
                    Go(false, frame);
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
                Go(true, null);
            }
            catch (Exception ex)
            {
                _fault(ex);
            }
        }


        private void Go(bool newFrame, Frame frame)
        {
            if (newFrame)
            {
                frame = _frame = new Frame(
                    new FrameContext
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
                        _frameConsumeCallback))
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
                    if (ReceiveAsync(frame))
                    {
                        return;
                    }

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
                    _frameConsumeCallback))
                {
                    return;
                }
            }
        }

        private bool ReceiveAsync(Frame frame)
        {
            // Lazy initialization of callback Action
            if (_receiveAsyncCompleted == null)
            {
                _receiveAsyncCompleted = ReceiveAsyncCompleted;
            }

            // Point callback at "this" only while an operation is occurring 
            // to avoid a cyclic reference that can cause memory leaks if 
            // the connection machinary doesn't wind down properly
            _receiveSocketEvent.Completed = _receiveAsyncCompleted;
            _receiveAsyncCompletedFrame = frame;
            if (!_socket.ReceiveAsync(_receiveSocketEvent))
            {
                _receiveSocketEvent.Completed = null;
                return false;
            }
            return true;
        }

        private void ReceiveAsyncCompleted()
        {
            var frame = _receiveAsyncCompletedFrame;
            _receiveSocketEvent.Completed = null;
            _receiveAsyncCompletedFrame = null;
            try
            {
                Go(false, frame);
            }
            catch (Exception ex)
            {
                _fault(ex);
            }
        }

        private void ProduceEnd(ProduceEndType endType)
        {
            Action drained = () =>
            {
                switch (endType)
                {
                    case ProduceEndType.SocketShutdownSend:
                        _socket.Shutdown(SocketShutdown.Send);
                        break;
                    case ProduceEndType.ConnectionKeepAlive:
                        ThreadPool.QueueUserWorkItem(_ => Go(true, null));
                        break;
                    case ProduceEndType.SocketDisconnect:
                        _services.Trace.Event(TraceEventType.Stop, TraceMessage.Connection);

                        _baton.Free();

                        var receiveSocketEvent = Interlocked.Exchange(ref _receiveSocketEvent, null);

                        // this has a race condition
                        if (receiveSocketEvent.Completed == null)
                        {
                            _services.Memory.FreeSocketEvent(receiveSocketEvent);
                        }
                        else
                        {
                            receiveSocketEvent.Completed = () => _services.Memory.FreeSocketEvent(receiveSocketEvent);
                        }

                        _socket.Shutdown(SocketShutdown.Receive);

                        var e = new SocketAsyncEventArgs();
                        Action cleanup = () =>
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
            {
                drained.Invoke();
            }
        }
    }
}
