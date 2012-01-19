using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Dragonfly.Http;
using Dragonfly.Utils;
using Xunit;

namespace Dragonfly.Tests.Http
{
    public class FrameTests : FrameTestsBase
    {
        
        [Fact]
        public void FakeAppCanReturnResults()
        {
            App.ResponseStatus = "200 Super";
            App.ResponseHeaders["x-header"] = new[] { "value" };
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
            AssertInputState(false, Connection.Next.NewFrame, "");
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
            App.ResponseHeaders["Connection"] = new[]{"close"};
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

            AssertInputState(false, Connection.Next.NewFrame, "");
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

            AssertInputState(false, Connection.Next.NewFrame, "67890");
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

            AssertInputState(false, Connection.Next.ReadMore, "");
            AssertOutputState(false, 0);

            Assert.Equal(1, App.RequestBody.SubscribeCount);
            Assert.False(App.RequestBody.Ended);
            Assert.Equal("12345", App.RequestBody.Text);
        }

    }
}
