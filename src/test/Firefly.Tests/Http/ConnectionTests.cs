using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace Firefly.Tests.Http
{
    public class ConnectionTests : ConnectionTestsBase
    {
        [Fact]
        public Task ConnectionClassCanBeTested()
        {
            App.OptionCallResultImmediately = false;

            Assert.Equal(false, Socket.ReceiveAsyncPaused);

            Connection.Execute();

            Assert.Equal(true, Socket.ReceiveAsyncPaused);

            return Socket.AddAsync(
                @"GET / HTTP/1.1
Host: localhost
Connection: close

").Then(() => Socket.ReceiveAsyncPaused.ShouldBe(false));
        }


        [Fact]
        public Task WithoutResponseBodyInformationServerWillSendConnectionCloseAndDisconnect()
        {
            App.OptionCallResultImmediately = true;

            Connection.Execute();

            return Socket.AddAsync(
                @"GET / HTTP/1.1
Host: localhost

GET /ignored HTTP/1.1
Host: localhost

"
                ).Then(() =>
                {
                    Assert.True(Socket.DisconnectCalled);
                    Assert.Equal(1, App.CallCount);
                    Assert.Equal(
                        @"HTTP/1.1 200 OK
Connection: close

", Socket.Output);
                });
        }

        [Fact]
        public void ConnectionCloseWillCauseDisconnectEvenWithResponseBodyInformation()
        {
            App.OptionCallResultImmediately = true;
            App.ResponseHeaders["Connection"] = new[] { "close" };
            App.ResponseHeaders["Content-Length"] = new[] { "0" };

            Connection.Execute();

            Socket.Add(
                @"GET / HTTP/1.1
Host: localhost

GET /ignored HTTP/1.1
Host: localhost

");

            Assert.True(Socket.DisconnectCalled);
            Assert.Equal(1, App.CallCount);
            Assert.Equal(
                @"HTTP/1.1 200 OK
Connection: close
Content-Length: 0

", Socket.Output);
        }

        [Fact]
        public void ContentLengthAloneWillAllowKeepAliveToOccur()
        {
            App.OptionCallResultImmediately = true;
            App.ResponseHeaders["Content-Length"] = new[] { "0" };

            Connection.Execute();

            Socket.Add(
                @"GET / HTTP/1.1
Host: localhost

GET /ignored HTTP/1.1
Host: localhost

");
            Thread.Sleep(500);

            Assert.False(Socket.DisconnectCalled);
            Assert.True(Socket.ReceiveAsyncPaused);
            Assert.Equal(2, App.CallCount);
            Assert.Equal(
                @"HTTP/1.1 200 OK
Content-Length: 0

HTTP/1.1 200 OK
Content-Length: 0

", Socket.Output);
        }

        [Fact]
        public void RequestsMayArriveIndividually()
        {
            App.OptionCallResultImmediately = true;
            App.ResponseHeaders["Content-Length"] = new[] { "0" };

            Connection.Execute();

            Socket.Add(
                @"GET / HTTP/1.1
Host: localhost

");
            Thread.Sleep(500);

            Assert.False(Socket.DisconnectCalled);
            Assert.True(Socket.ReceiveAsyncPaused);
            Assert.Equal(1, App.CallCount);
            Assert.Equal(
                @"HTTP/1.1 200 OK
Content-Length: 0

", Socket.Output);

            Socket.Add(
                @"GET / HTTP/1.1
Host: localhost

");

            Thread.Sleep(500);

            Assert.False(Socket.DisconnectCalled);
            Assert.True(Socket.ReceiveAsyncPaused);
            Assert.Equal(2, App.CallCount);
            Assert.Equal(
                @"HTTP/1.1 200 OK
Content-Length: 0

HTTP/1.1 200 OK
Content-Length: 0

", Socket.Output);
        }

        [Fact]
        public void RequestBodyContentLengthAllowsBackToBackPosts()
        {
            App.OptionCallResultImmediately = true;
            App.ResponseHeaders["Content-Length"] = new[] { "0" };

            Connection.Execute();

            Socket.Add(
                @"POST /one HTTP/1.1
Content-Length: 7
Host: localhost

Hello
POST /two HTTP/1.1
Content-Length: 13
Host: localhost

Hello World
GET /three HTTP/1.1
Host: localhost

");
            Thread.Sleep(500);

            Assert.False(Socket.DisconnectCalled);
            Assert.True(Socket.ReceiveAsyncPaused);
            Assert.Equal(3, App.CallCount);
            Assert.Equal(
                @"HTTP/1.1 200 OK
Content-Length: 0

HTTP/1.1 200 OK
Content-Length: 0

HTTP/1.1 200 OK
Content-Length: 0

",
                Socket.Output);
        }

        [Fact]
        public void RequestBodyChunkedAlsoAllowsBackToBackPosts()
        {
            App.OptionCallResultImmediately = true;
            App.ResponseHeaders["Content-Length"] = new[] { "0" };

            Connection.Execute();

            Socket.Add(
                @"POST /one HTTP/1.1
Transfer-Encoding: chunked
Host: localhost

5
Hello
0
POST /two HTTP/1.1
Transfer-Encoding: chunked
Host: localhost

5
Hello
6
 World
0
GET /three HTTP/1.1
Host: localhost

");
            Thread.Sleep(500);

            Assert.False(Socket.DisconnectCalled);
            Assert.True(Socket.ReceiveAsyncPaused);
            Assert.Equal(3, App.CallCount);
            Assert.Equal(
                @"HTTP/1.1 200 OK
Content-Length: 0

HTTP/1.1 200 OK
Content-Length: 0

HTTP/1.1 200 OK
Content-Length: 0

",
                Socket.Output);
        }
    }
}
