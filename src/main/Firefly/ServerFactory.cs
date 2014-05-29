using Microsoft.AspNet.Hosting.Server;
using System;
using Microsoft.AspNet.Builder;
using Microsoft.Framework.ConfigurationModel;
using System.Threading.Tasks;
using System.Diagnostics;
using Firefly.Utils;
using System.Net.Sockets;
using System.Threading;
using Firefly.Http;
using System.Net;

namespace Firefly
{
    /// <summary>
    /// Summary description for ServerFactory
    /// </summary>
    public class ServerFactory : IServerFactory
    {
        public IServerInformation Initialize(IConfiguration configuration)
        {
            return new ServerInformation();
        }

        public IDisposable Start(IServerInformation serverInformation, Func<object, Task> application)
        {
            var _services = (IFireflyService)serverInformation;
            _services.Trace.Event(TraceEventType.Start, TraceMessage.ServerFactory);

            var endpoint = new IPEndPoint(IPAddress.Loopback, 3001);

            var listenSocket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.IP);
            listenSocket.Bind(endpoint);
            listenSocket.Listen(-1);

            WaitCallback connectionExecute = connection =>
            {
                _services.Trace.Event(TraceEventType.Verbose, TraceMessage.ServerFactoryConnectionExecute);
                ((Connection)connection).Execute();
            };

            var stop = false;
            var acceptEvent = new SocketAsyncEventArgs();
            Action accept = () =>
            {
                while (!stop)
                {
                    _services.Trace.Event(TraceEventType.Verbose, TraceMessage.ServerFactoryAcceptAsync);

                    if (listenSocket.AcceptAsync(acceptEvent))
                    {
                        return;
                    }

                    _services.Trace.Event(TraceEventType.Verbose, TraceMessage.ServerFactoryAcceptCompletedSync);

                    if (acceptEvent.SocketError != SocketError.Success)
                    {
                        _services.Trace.Event(TraceEventType.Error, TraceMessage.ServerFactoryAcceptSocketError);
                    }

                    if (acceptEvent.SocketError == SocketError.Success &&
                        acceptEvent.AcceptSocket != null)
                    {
                        ThreadPool.QueueUserWorkItem(
                            connectionExecute,
                            new Connection(
                                _services,
                                application,
                                new SocketWrapper(acceptEvent.AcceptSocket),
                                OnDisconnect));
                    }
                    acceptEvent.AcceptSocket = null;
                }
            };
            acceptEvent.Completed += (_, __) =>
            {
                _services.Trace.Event(TraceEventType.Verbose, TraceMessage.ServerFactoryAcceptCompletedAsync);

                if (acceptEvent.SocketError == SocketError.Success &&
                    acceptEvent.AcceptSocket != null)
                {
                    ThreadPool.QueueUserWorkItem(
                        connectionExecute,
                        new Connection(
                            _services,
                            application,
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
                    _services.Trace.Event(TraceEventType.Stop, TraceMessage.ServerFactory);

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

    public class ServerInformation : FireflyService, IServerInformation
    {
        public string Name
        {
            get
            {
                return "Firefly";
            }
        }
    }
}