using System.Net;
using Xunit;

namespace Firefly.Tests.Http
{
    public class FrameTests : FrameTestsBase
    {
        [Fact]
        public void FakeAppCanReturnResults()
        {
            App.ResponseStatus = "200 Super";
            App.ResponseHeaders["x-header"] = new[] {"value"};
            App.ResponseBody.Text = "This is the response";

            Input.Add(
                @"POST /hello/world?x=y HTTP/1.1
Host:localhost

");
            Input.End();


            AssertOutputState(true, "200 Super", "x-header: value", "This is the response");
            Assert.Equal("localhost", App.RequestHeader("Host"));
        }


        [Fact]
        public void ValuesFromTheOwinSpecificationArePresent()
        {
            Input.Add("GET /hello/world?x=y HTTP/1.1\r\nHost: london\r\nfoo: bar\r\nfrap: quad\r\n\r\n");
            Input.End();
            AssertInputState(false, true, "");
            AssertOutputState(true);
            Assert.Equal(1, App.CallCount);

            Assert.Equal("1.0", App.Env["owin.Version"]);
            Assert.Equal("http", App.Env["owin.RequestScheme"]);
            Assert.Equal("GET", App.Env["owin.RequestMethod"]);
            Assert.Equal("", App.Env["owin.RequestPathBase"]);
            Assert.Equal("/hello/world", App.Env["owin.RequestPath"]);
            Assert.Equal("x=y", App.Env["owin.RequestQueryString"]);
            Assert.Equal("london", App.RequestHeader("Host"));
            Assert.Equal("bar", App.RequestHeader("foo"));
            Assert.Equal("quad", App.RequestHeader("frap"));
        }

        [Fact]
        public void CompletedRequestGeneratesResponse()
        {
            App.ResponseStatus = "418 I'm a teapot";
            App.ResponseHeaders["Connection"] = new[] {"close"};
            Input.Add(
                @"GET /hello/world?x=y HTTP/1.1
Host: localhost

");
            AssertOutputState(true, "418 I'm a teapot", "Connection: close");
        }

        [Fact]
        public void RequestBodyMayBeConsumedFromApplication()
        {
            App.OptionReadRequestBody = true;

            Input.Add(
                @"POST /hello/world?x=y HTTP/1.1
Host: localhost
Content-Length: 5

12345");

            AssertInputState(false, true, "");
            AssertOutputState(true);

            Assert.Equal(1, App.RequestBody.SubscribeCount);
            Assert.True(App.RequestBody.Ended);
            Assert.Equal("12345", App.RequestBody.Text);
        }

        [Fact]
        public void ExcessRequestBodyPassesToNextFrame()
        {
            App.OptionReadRequestBody = true;

            Input.Add(
                @"POST /hello/world?x=y HTTP/1.1
Host: localhost
Content-Length: 5

1234567890");

            AssertInputState(false, true, "67890");
            AssertOutputState(true);

            Assert.Equal(1, App.RequestBody.SubscribeCount);
            Assert.True(App.RequestBody.Ended);
            Assert.Equal("12345", App.RequestBody.Text);
        }

        [Fact]
        public void InsufficientRequestBodyWillWaitForMoreData()
        {
            App.OptionReadRequestBody = true;

            Input.Add(
                @"POST /hello/world?x=y HTTP/1.1
Host: localhost
Content-Length: 10

12345");

            AssertInputState(false, false, "");
            AssertOutputState(false, 0);

            Assert.Equal(1, App.RequestBody.SubscribeCount);
            Assert.False(App.RequestBody.Ended);
            Assert.Equal("12345", App.RequestBody.Text);
        }

        [Fact]
        public void ValuesForCommonServerVariablesArePresent()
        {
            Socket.LocalEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 80);
            Socket.RemoteEndPoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 12345);
            Input.Add("GET /hello/world?x=y HTTP/1.1\r\nHost: london\r\nfoo: bar\r\nfrap: quad\r\n\r\n");
            Input.End();
            AssertInputState(false, true, "");
            AssertOutputState(true);
            Assert.Equal(1, App.CallCount);

            Assert.Equal("127.0.0.1", App.Env["server.LOCAL_ADDR"]);
            Assert.Equal("80", App.Env["server.SERVER_PORT"]);
            Assert.Equal("192.168.1.1", App.Env["server.REMOTE_ADDR"]);
            Assert.Equal("12345", App.Env["server.REMOTE_PORT"]);
        }

    }
}
