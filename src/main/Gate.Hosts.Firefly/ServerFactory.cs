using System;
using Gate.Hosts.Firefly;
using Owin;

[assembly: ServerFactory]
namespace Gate.Hosts.Firefly
{
    public class ServerFactory : Attribute
    {
        public IDisposable Create(AppDelegate app, int port, string hostname)
        {
            var serverFactory = new global::Firefly.Http.ServerFactory();
            return serverFactory.Create(app, port, hostname);
        }
    }
}
