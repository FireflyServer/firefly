using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Dragonfly.Utils;
using Gate.Owin;

// ReSharper disable AccessToModifiedClosure

namespace Dragonfly.Http
{
    public class ServerFactory
    {
        private readonly IServerTrace _trace = NullServerTrace.Instance;

        public ServerFactory()
        {
        }

        public ServerFactory(IServerTrace trace)
        {
            _trace = trace;
        }

        private static readonly Action Noop = () => { };

        public IDisposable Create(AppDelegate app, int port)
        {
            return Create(app, new IPEndPoint(0, port));
        }

        public IDisposable Create(AppDelegate app, int port, string hostname)
        {
            return Create(app, new DnsEndPoint(hostname, port, AddressFamily.InterNetwork));
        }

        public IDisposable Create(AppDelegate app, EndPoint endpoint)
        {
            _trace.Event(TraceEventType.Start, TraceMessage.ServerFactory);

            var listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            listenSocket.Bind(endpoint);
            listenSocket.Listen(-1);

            WaitCallback connectionExecute = connection =>
            {
                _trace.Event(TraceEventType.Verbose, TraceMessage.ServerFactoryConnectionExecute);
                ((Connection)connection).Execute();
            };

            var stop = false;
            var acceptEvent = new SocketAsyncEventArgs();
            Action accept =
                () =>
                {
                    while (!stop)
                    {
                        _trace.Event(TraceEventType.Verbose, TraceMessage.ServerFactoryAcceptAsync);

                        if (listenSocket.AcceptAsync(acceptEvent))
                            return;

                        _trace.Event(TraceEventType.Verbose, TraceMessage.ServerFactoryAcceptCompletedSync);

                        if (acceptEvent.SocketError != SocketError.Success)
                        {
                            _trace.Event(TraceEventType.Error, TraceMessage.ServerFactoryAcceptSocketError);
                        }

                        if (acceptEvent.SocketError == SocketError.Success &&
                            acceptEvent.AcceptSocket != null)
                        {
                            ThreadPool.QueueUserWorkItem(
                                connectionExecute,
                                new Connection(
                                    _trace, 
                                    app, 
                                    new SocketWrapper(acceptEvent.AcceptSocket), 
                                    OnDisconnect));
                        }
                        acceptEvent.AcceptSocket = null;
                    }
                };
            acceptEvent.Completed +=
                (_, __) =>
                {
                    _trace.Event(TraceEventType.Verbose, TraceMessage.ServerFactoryAcceptCompletedAsync);

                    if (acceptEvent.SocketError == SocketError.Success &&
                        acceptEvent.AcceptSocket != null)
                    {
                        ThreadPool.QueueUserWorkItem(
                            connectionExecute, 
                            new Connection(
                                _trace, 
                                app, 
                                new SocketWrapper(acceptEvent.AcceptSocket), 
                                OnDisconnect));
                    }
                    acceptEvent.AcceptSocket = null;
                    accept();
                };
            accept();

            return new Disposable(
                () =>
                {
                    _trace.Event(TraceEventType.Stop, TraceMessage.ServerFactory);

                    stop = true;
                    listenSocket.Close();
                    acceptEvent.Dispose();
                });
        }

        private static void OnDisconnect(ISocket obj)
        {
            obj.Close();
        }
    }
}
