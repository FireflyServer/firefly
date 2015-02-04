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
using System.Collections.Generic;
using System.Globalization;

namespace Firefly
{
    /// <summary>
    /// Summary description for ServerFactory
    /// </summary>
    public class ServerFactory : IServerFactory
    {

        public IServerInformation Initialize(IConfiguration configuration)
        {
            var information = new ServerInformation();
            information.Initialize(configuration);
            return information;
        }

        public IDisposable Start(IServerInformation serverInformation, Func<object, Task> application)
        {
            var services = (ServerInformation)serverInformation;
            services.Trace.Event(TraceEventType.Information, TraceMessage.ServerFactory);

            var disposables = new List<IDisposable>(services.Addresses.Count);

            foreach (var address in services.Addresses)
            {
                var endpoint = new IPEndPoint(IPAddress.Any, address.Port);
                disposables.Add(StartListener(endpoint, services, application));
            }

            return new Disposable(
                () =>
                {
                    foreach (var server in disposables)
                        server.Dispose();
                });
        }

        private IDisposable StartListener(IPEndPoint endPoint, IFireflyService service, Func<object, Task> application)
        {
            var listenSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.IP);
            listenSocket.Bind(endPoint);
            listenSocket.Listen(-1);

            WaitCallback connectionExecute = connection =>
            {
                service.Trace.Event(TraceEventType.Verbose, TraceMessage.ServerFactoryConnectionExecute);
                ((Connection)connection).Execute();
            };

            var stop = false;
            var acceptEvent = new SocketAsyncEventArgs();
            Action accept = () =>
            {
                while (!stop)
                {
                    service.Trace.Event(TraceEventType.Verbose, TraceMessage.ServerFactoryAcceptAsync);

                    if (listenSocket.AcceptAsync(acceptEvent))
                    {
                        return;
                    }

                    service.Trace.Event(TraceEventType.Verbose, TraceMessage.ServerFactoryAcceptCompletedSync);

                    if (acceptEvent.SocketError != SocketError.Success)
                    {
                        service.Trace.Event(TraceEventType.Error, TraceMessage.ServerFactoryAcceptSocketError);
                    }

                    if (acceptEvent.SocketError == SocketError.Success &&
                        acceptEvent.AcceptSocket != null)
                    {
                        ThreadPool.QueueUserWorkItem(
                            connectionExecute,
                            new Connection(
                                service,
                                application,
                                new SocketWrapper(acceptEvent.AcceptSocket),
                                OnDisconnect));
                    }
                    acceptEvent.AcceptSocket = null;
                }
            };
            acceptEvent.Completed += (_, __) =>
            {
                service.Trace.Event(TraceEventType.Verbose, TraceMessage.ServerFactoryAcceptCompletedAsync);

                if (acceptEvent.SocketError == SocketError.Success &&
                    acceptEvent.AcceptSocket != null)
                {
                    ThreadPool.QueueUserWorkItem(
                        connectionExecute,
                        new Connection(
                            service,
                            application,
                            new SocketWrapper(acceptEvent.AcceptSocket),
                            OnDisconnect));
                }
                acceptEvent.AcceptSocket = null;
                accept();
            };
            accept();

            return new Disposable(() =>
            {
                stop = true;
                listenSocket.Dispose();
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

        public ServerInformation()
        {
            Addresses = new List<ServerAddress>();
        }

        public void Initialize(IConfiguration configuration)
        {
            string urls;
            if (!configuration.TryGet("server.urls", out urls))
            {
                urls = "http://+:3004/";
            }
            foreach (var url in urls.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string scheme;
                string host;
                int port;
                string path;
                if (DeconstructUrl(url, out scheme, out host, out port, out path))
                {
                    Addresses.Add(
                        new ServerAddress
                        {
                            Scheme = scheme,
                            Host = host,
                            Port = port,
                            Path = path
                        });
                }
            }
        }

        public IList<ServerAddress> Addresses { get; private set; }

        internal static bool DeconstructUrl(
           string url,
           out string scheme,
           out string host,
           out int port,
           out string path)
        {
            url = url ?? string.Empty;

            int delimiterStart1 = url.IndexOf("://", StringComparison.Ordinal);
            if (delimiterStart1 < 0)
            {
                scheme = null;
                host = null;
                port = 0;
                path = null;
                return false;
            }
            int delimiterEnd1 = delimiterStart1 + "://".Length;

            int delimiterStart3 = url.IndexOf("/", delimiterEnd1, StringComparison.Ordinal);
            if (delimiterStart3 < 0)
            {
                delimiterStart3 = url.Length;
            }
            int delimiterStart2 = url.LastIndexOf(":", delimiterStart3 - 1, delimiterStart3 - delimiterEnd1, StringComparison.Ordinal);
            int delimiterEnd2 = delimiterStart2 + ":".Length;
            if (delimiterStart2 < 0)
            {
                delimiterStart2 = delimiterStart3;
                delimiterEnd2 = delimiterStart3;
            }

            scheme = url.Substring(0, delimiterStart1);
            string portString = url.Substring(delimiterEnd2, delimiterStart3 - delimiterEnd2);
            int portNumber;
            if (int.TryParse(portString, NumberStyles.Integer, CultureInfo.InvariantCulture, out portNumber))
            {
                host = url.Substring(delimiterEnd1, delimiterStart2 - delimiterEnd1);
                port = portNumber;
            }
            else
            {
                if (string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase))
                {
                    port = 80;
                }
                else if (string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase))
                {
                    port = 443;
                }
                else
                {
                    port = 0;
                }
                host = url.Substring(delimiterEnd1, delimiterStart3 - delimiterEnd1);
            }
            path = url.Substring(delimiterStart3);
            return true;
        }
    }

    public class ServerAddress
    {
        public string Host { get; internal set; }
        public string Path { get; internal set; }
        public int Port { get; internal set; }
        public string Scheme { get; internal set; }
    }
}