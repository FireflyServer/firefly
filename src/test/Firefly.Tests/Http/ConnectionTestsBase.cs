using System.Threading;
using Firefly.Http;
using Firefly.Tests.Fakes;
using Firefly.Utils;

namespace Firefly.Tests.Http
{
    public abstract class ConnectionTestsBase
    {
        protected readonly FakeApp App;
        protected readonly Connection Connection;
        protected readonly FakeSocket Socket;

        protected bool Disconnected { get; set; }
        protected EventWaitHandle DisconnectedEvent { get; set; }


        public ConnectionTestsBase()
        {
            App = new FakeApp();
            Socket = new FakeSocket();
            Connection = new Connection(new FakeServices(), App.Call, Socket, OnDisconnected);
            DisconnectedEvent = new ManualResetEvent(false);
        }

        private void OnDisconnected(ISocket obj)
        {
            Disconnected = true;
            DisconnectedEvent.Set();
        }
    }
}
