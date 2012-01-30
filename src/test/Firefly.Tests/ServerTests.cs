using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using Dragonfly.Http;
using Xunit;

namespace Dragonfly.Tests
{
    public class ServerTests
    {
        [Fact]
        public void ServerWillOpenSocketWhenToldToListen()
        {
            var server = new Server((req, resp) => { });
            server.Listen(56565, null, null);

            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            socket.Connect("localhost", 56565);
            socket.Close();
        }

        [Fact]
        public void ServerWillCloseSocket()
        {
            var server = new Server();
            server.Listen(56566, null, null);
            server.Close();

            Assert.Throws<SocketException>(() =>
                              {
                                  var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                                  socket.Connect("localhost", 56566);
                                  socket.Close();
                              });
        }

        [Fact]
        public void ServerWillEmitCloseRequest()
        {
            var server = new Server();
            var closed = false;
            server.OnClose(errno => closed = true);
            server.Listen(56566, null, null);

            var closedBefore = closed;
            server.Close();
            var closedAfter = closed;

            Assert.Equal(false, closedBefore);
            Assert.Equal(true, closedAfter);
        }


        [Fact]
        public void ServerWillEmitRequestEvent()
        {
            var requested = false;
            using (var server = new Server((req, resp) =>
            {
                requested = true;
                resp.Headers["Content-Type"] = "text/plain";
                resp.Headers["Connection"] = "close";
                resp.WriteHead(200, "OK");
                resp.End();
            }))
            {
                server.Listen(56567, null, null);

                var webRequest = System.Net.WebRequest.Create("http://localhost:56567");
                var webResponse = webRequest.GetResponse();

                Assert.Equal(true, requested);

            }
        }


        [Fact]
        public void TextCanBeSentThroughResponse()
        {
            var requested = false;
            using (var server = new Server((req, resp) =>
            {
                requested = true;
                resp.Headers["Content-Type"] = "text/plain";
                resp.Headers["Connection"] = "close";
                resp.WriteHead(200, "OK");
                resp.Write("This is a triumph");
                resp.End();
            }))
            {
                server.Listen(56567, null, null);

                var webRequest = System.Net.WebRequest.Create("http://localhost:56567");
                var webResponse = webRequest.GetResponse();
                using (var stream = webResponse.GetResponseStream())
                {
                    using (var reader = new StreamReader(stream))
                    {
                        var text = reader.ReadToEnd();
                        Assert.Equal("This is a triumph", text);
                    }
                }

                Assert.Equal(true, requested);
            }
        }
    }
}
