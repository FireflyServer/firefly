using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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

            var beginAccept = Noop;
            beginAccept =
                () =>
                {
                    _trace.Event(TraceEventType.Verbose, TraceMessage.ServerFactoryBeginAccept);
                    listenSocket.BeginAccept(
                        iar =>
                        {
                            _trace.Event(TraceEventType.Verbose, TraceMessage.ServerFactoryEndAccept);
                            var connectionSocket = listenSocket.EndAccept(iar);
                            beginAccept.Invoke();

                            var connection = new Connection(_trace, app, connectionSocket);
                            _trace.Event(TraceEventType.Information, TraceMessage.ServerFactoryConnectionExecute);
                            connection.Execute();
                        }, null);
                };

            beginAccept.Invoke();

            return new Disposable(
                () =>
                {
                    _trace.Event(TraceEventType.Stop, TraceMessage.ServerFactory);

                    beginAccept = Noop;
                    listenSocket.Close();
                });
        }
    }
}
