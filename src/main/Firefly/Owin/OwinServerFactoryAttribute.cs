using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Firefly.Owin;
using Firefly.Utils;

[assembly: OwinServerFactory]
namespace Firefly.Owin
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    public class OwinServerFactoryAttribute : Attribute
    {
        public static void Initialize(IDictionary<string, object> properties)
        {
            if (Addresses(properties) == null)
            {
                properties["host.Addresses"] = new List<IDictionary<string, object>>();
            }
        }

        public static IDisposable Create(AppFunc app, IDictionary<string, object> properties)
        {
            var factory = new Http.ServerFactory();

            var created = new List<IDisposable>();

            var addresses = Addresses(properties);
            if (addresses != null)
            {
                foreach (var address in addresses)
                {
                    var port = Port(address);
                    var hostname = Host(address);
                    if (hostname == null || hostname == "*" || hostname == "+")
                    {
                        created.Add(factory.Create(app, port));

                        Kickstart(new IPEndPoint(IPAddress.Loopback,port));
                    }
                    else
                    {
                        created.Add(factory.Create(app, port, hostname));

                        Kickstart(new DnsEndPoint(hostname, port));
                    }
                }
            }
            return new Disposable(
                () =>
                {
                    foreach (var disposable in created)
                    {
                        disposable.Dispose();
                    }
                });
        }

        private static void Kickstart(EndPoint endPoint)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            socket.Connect(endPoint);
            socket.Close();
        }

        static IList<IDictionary<string, object>> Addresses(IDictionary<string, object> properties)
        {
            object value;
            if (properties.TryGetValue("host.Addresses", out value) && value is IList<IDictionary<string, object>>)
            {
                return (IList<IDictionary<string, object>>)value;
            }
            return null;
        }

        static string Host(IDictionary<string, object> address)
        {
            object value;
            return address.TryGetValue("host", out value) ? Convert.ToString(value) : null;
        }

        static int Port(IDictionary<string, object> address)
        {
            object value;
            return address.TryGetValue("port", out value) ? Convert.ToInt32(value) : 0;
        }
    }
}
