using Dragonfly.Http;
using Xunit;

namespace Dragonfly.Tests.Http
{
    public class FrameRequestHeaderTests : FrameTestsBase
    {
        [Fact]
        public void DemandsMoreIfFirstLineIncomplete()
        {
            Input.Add("GET / HTTP/1.1\r");
            AssertInputState(false, Connection.Next.ReadMore, "GET / HTTP/1.1\r");
            AssertOutputState(false, 0);
        }


        [Fact]
        public void ConsumesFirstLineIfComplete()
        {
            Input.Add("GET / HTTP/1.1\r\n");
            AssertInputState(false, Connection.Next.ReadMore, "");
            AssertOutputState(false, 0);
        }

        [Fact]
        public void IncompleteHeadersAreLeftIntact()
        {
            Input.Add("GET / HTTP/1.1\r\nfoo: bar\r");
            AssertInputState(false, Connection.Next.ReadMore, "foo: bar\r");
            AssertOutputState(false, 0);
        }

        [Fact]
        public void CharacterFollowingHeaderMustBeKnownBecauseOfHeaderWrapping()
        {
            Input.Add("GET / HTTP/1.1\r\nfoo: bar\r\nfrap: quad\r\n");
            AssertInputState(false, Connection.Next.ReadMore, "frap: quad\r\n");
            AssertOutputState(false, 0);
        }

        [Fact]
        public void AllCompleteHeadersAreConsumed()
        {
            Input.Add("GET / HTTP/1.1\r\nfoo: bar\r\nfrap: quad\r\n\r");
            AssertInputState(false, Connection.Next.ReadMore, "\r");
            AssertOutputState(false, 0);
        }

        [Fact]
        public void BlankLineEndsHeadersAndCallsTheAppDelegate()
        {
            Input.Add("GET / HTTP/1.1\r\nfoo: bar\r\nfrap: quad\r\n\r\n");
            AssertInputState(false, Connection.Next.NewFrame, "");
            Assert.Equal(1, App.CallCount);
            AssertOutputState(true);
        }


        [Fact]
        public void LeadingAndTrailingLinearWhiteSpaceIsCropped()
        {
            App.OptionReadRequestBody = true;

            Input.Add(
"Get / HTTP/1.1\r\nContent-Length: 0\r\n" +
"x-1:alpha beta\r\n" +
"x-2: alpha beta\r\n" +
"x-3:alpha beta \r\n" +
"x-4:\talpha beta\t\r\n" +
"x-5:\t  \t \talpha\tbeta\t  \t \t\r\n" +
"\r\n");

            AssertInputState(false, Connection.Next.NewFrame, "");
            AssertOutputState(true);

            Assert.Equal("alpha beta", App.RequestHeaders["x-1"]);
            Assert.Equal("alpha beta", App.RequestHeaders["x-2"]);
            Assert.Equal("alpha beta", App.RequestHeaders["x-3"]);
            Assert.Equal("alpha beta", App.RequestHeaders["x-4"]);
            Assert.Equal("alpha\tbeta", App.RequestHeaders["x-5"]);
        }

        [Fact]
        public void MultipleHeadersAreCrlfDelimitedWithOrderPreserved()
        {
            App.OptionReadRequestBody = true;

            Input.Add(
"Get / HTTP/1.1\r\nContent-Length: 0\r\n" +
"x-1:alpha beta1\r\n" +
"x-1: alpha beta2\r\n" +
"x-2: foo1 \r\n" +
"x-1:alpha beta3 \r\n" +
"x-2: foo2 \r\n" +
"x-1:\talpha beta4\t\r\n" +
"x-1:\t  \t \talpha\tbeta5\t  \t \t\r\n" +
"\r\n");

            AssertInputState(false, Connection.Next.NewFrame, "");
            AssertOutputState(true);

            Assert.Equal("alpha beta1\r\nalpha beta2\r\nalpha beta3\r\nalpha beta4\r\nalpha\tbeta5", App.RequestHeaders["x-1"]);
            Assert.Equal("foo1\r\nfoo2", App.RequestHeaders["x-2"]);
        }

        [Fact]
        public void WrappedHeadersHaveCrlfReplacedWithSpace()
        {
            App.OptionReadRequestBody = true;

            Input.Add(
"Get / HTTP/1.1\r\nContent-Length: 0\r\n" +
"x-1:alpha beta1\r\n" +
"x-1:alpha\r\n\tbeta2\r\n" +
"x-1:alpha\r\n beta3 \r\n" +
"x-1:\talpha beta4\t\r\n" +
"\r\n");

            AssertInputState(false, Connection.Next.NewFrame, "");
            AssertOutputState(true);

            Assert.Equal("alpha beta1\r\nalpha \tbeta2\r\nalpha  beta3\r\nalpha beta4", App.RequestHeaders["x-1"]);
        }

    }
}