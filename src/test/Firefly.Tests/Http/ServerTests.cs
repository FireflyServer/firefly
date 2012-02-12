using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Firefly.Http;
using Firefly.Tests.Extensions;
using Owin;
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
                    (write, flush, end, cancel) => end(null));
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

        [Fact]
        public void SyncWritesBufferAndCanBeReadBackSlowly()
        {
            var responseEnded = new TaskCompletionSource<bool>();

            AppDelegate app = (env, result, fault) => result(
                "200 OK",
                new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase),
                (write, flush, end, cancel) =>
                {
                    var data = "Hello world!\r\n".ToArraySegment();
                    foreach (var loop in Enumerable.Range(0, 10000))
                    {
                        write(data);
                    }
                    end(null);
                    responseEnded.TrySetResult(true);
                });

            using (new ServerFactory().Create(app, 56567))
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                socket.Connect("localhost", 56567);
                socket.Send(
@"GET / HTTP/1.1
Connection: close
Host: localhost

");
                if (Debugger.IsAttached) responseEnded.Task.Wait();

                Assert.True(responseEnded.Task.Wait(TimeSpan.FromSeconds(5)));

                var totalBytes = 0;
                var buffer = new byte[1024];
                var totalText = "";
                for (; ; )
                {
                    var bytes = socket.Receive(buffer);
                    if (bytes == 0)
                    {
                        break;
                    }
                    totalBytes += bytes;
                    totalText += new ArraySegment<byte>(buffer, 0, bytes).ToString(Encoding.Default);
                }
                socket.Disconnect(false);
                var x = 5;
                Thread.Sleep(900);
            }
        }


        [Fact]
        public void AsyncFlushAndCanBeReadBackSlowlyWithoutBuffering()
        {
            var responseStarted = new TaskCompletionSource<bool>();
            var responseEnded = new TaskCompletionSource<bool>();

            AppDelegate app = (env, result, fault) => result(
                "200 OK",
                new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase),
                (write, flush, end, cancel) =>
                {
                    var data = "Hello world!\r\n".ToArraySegment();

                    var loop = 0;
                    Action go = () => { };
                    go = () =>
                               {
                                   while (loop != 10000)
                                   {
                                       ++loop;
                                       // ReSharper disable AccessToModifiedClosure
                                       if (write(data) && flush(go))
                                           return;
                                       // ReSharper restore AccessToModifiedClosure
                                   }
                                   end(null);
                                   responseEnded.TrySetResult(true);
                               };
                    go.Invoke();
                    responseStarted.TrySetResult(true);
                });

            using (new ServerFactory().Create(app, 56567))
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                socket.Connect("localhost", 56567);
                socket.Send(
@"GET / HTTP/1.1
Connection: close
Host: localhost

");
                //if (Debugger.IsAttached) responseStarted.Task.Wait();

                //Assert.True(responseStarted.Task.Wait(TimeSpan.FromSeconds(5)));

                var totalBytes = 0;
                var buffer = new byte[1024];
                var totalText = "";
                var chunks = "";
                for (; ; )
                {
                    var bytes = socket.Receive(buffer);
                    if (bytes == 0)
                    {
                        break;
                    }
                    totalBytes += bytes;
                    chunks = chunks + bytes + "\r\n";
                    totalText += new ArraySegment<byte>(buffer, 0, bytes).ToString(Encoding.Default) + "*";
                }
                socket.Disconnect(false);
                var x = 5;
                Thread.Sleep(900);
            }
        }
    }
}
