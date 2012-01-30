using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Firefly.Tests.Fakes;
using Firefly.Http;
using Gate.Owin;
using Xunit;

namespace Firefly.Tests.Http
{
    public class ServerTests
    {
        [Fact]
        public void ServerWillOpenSocketWhenToldToListen()
        {
            new ServerFactory().Create((env, result, fault) => { }, 56565);

            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            socket.Connect("localhost", 56565);
            socket.Close();
        }

        [Fact]
        public void ServerWillCloseSocket()
        {
            var server = new ServerFactory().Create((env, result, fault) => { }, 56566);
            server.Dispose();

            Assert.Throws<SocketException>(() =>
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                socket.Connect("localhost", 56566);
                socket.Close();
            });
        }

        [Fact]
        public void ServerWillCallAppWhenRequestHeadersAreComplete()
        {
            var called = new TaskCompletionSource<bool>();

            AppDelegate app = (env, result, fault) =>
            {
                called.TrySetResult(true);
                result(
                    "200 OK",
                    new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase),
                    (a, b, c) =>
                    {
                        c();
                        return () => { };
                    });
            };

            using (new ServerFactory().Create(app, 56567))
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                socket.Connect("localhost", 56567);
                socket.Send(
@"GET / HTTP/1.0
Connection: close
Host: localhost

");
                Assert.True(called.Task.Wait(TimeSpan.FromSeconds(5)));
            }
        }

    }
}
