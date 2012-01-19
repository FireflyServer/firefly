using System.Threading;
using Dragonfly.Http;
using Dragonfly.Tests.Fakes;
using Dragonfly.Utils;

namespace Dragonfly.Tests.Http
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
            Connection = new Connection(new FakeTrace(), App.Call, Socket, OnDisconnected);
            DisconnectedEvent = new ManualResetEvent(false);
        }

        private void OnDisconnected(ISocket obj)
        {
            Disconnected = true;
            DisconnectedEvent.Set();
        }

    }
}