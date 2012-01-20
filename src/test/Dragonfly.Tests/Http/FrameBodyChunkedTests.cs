using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dragonfly.Http;
using Xunit;

namespace Dragonfly.Tests.Http
{
    public class FrameBodyChunkedTests : FrameTestsBase
    {
        [Fact]
        public void AfterHeadersMoreReadingIsNeeded()
        {
            Input.Add(
@"POST / HTTP/1.1
Transfer-Encoding: chunked

");
            AssertInputState(false, Connection.Next.ReadMore, "");
        }

        [Fact]
        public void IncompleteChunkedSizeLineIsLeftIntact()
        {
            App.OptionReadRequestBody = true;
            Input.Add(
@"POST / HTTP/1.1
Transfer-Encoding: chunked

a");
            AssertInputState(false, Connection.Next.ReadMore, "a");
            Assert.Equal("", App.RequestBody.Text);
            Assert.False(App.RequestBody.Ended);
        }

        [Fact]
        public void ChunkedSizeLineIsTakenAfterCrlf()
        {
            App.OptionReadRequestBody = true;
            Input.Add(
@"POST / HTTP/1.1
Transfer-Encoding: chunked

a
");
            AssertInputState(false, Connection.Next.ReadMore, "");
        }

        [Fact]
        public void ChunkedExtensionsAreAcceptedAndIgnored()
        {
            App.OptionReadRequestBody = true;
            Input.Add(
@"POST / HTTP/1.1
Transfer-Encoding: chunked

a;foo=bar
");
            AssertInputState(false, Connection.Next.ReadMore, "");
        }

        [Fact]
        public void IncrementalChunkDataPassedToApplication()
        {
            App.OptionReadRequestBody = true;
            Input.Add(
@"POST / HTTP/1.1
Transfer-Encoding: chunked

a;foo=bar
12345");
            AssertInputState(false, Connection.Next.ReadMore, "");
            Assert.False(App.RequestBody.Ended);
            Assert.Equal("12345", App.RequestBody.Text);
        }

        [Fact]
        public void CompleteChunkPassedToApplicationWithoutCRLF()
        {
            App.OptionReadRequestBody = true;
            Input.Add(
@"POST / HTTP/1.1
Transfer-Encoding: chunked

a;foo=bar
1234567890
");
            AssertInputState(false, Connection.Next.ReadMore, "");
            Assert.False(App.RequestBody.Ended);
            Assert.Equal("1234567890", App.RequestBody.Text);
        }

        [Fact]
        public void SecondChunkStartsOnNextLine()
        {
            App.OptionReadRequestBody = true;
            Input.Add(
@"POST / HTTP/1.1
Transfer-Encoding: chunked

a;foo=bar
1234567890
10
1234567890");
            AssertInputState(false, Connection.Next.ReadMore, "");
            Assert.False(App.RequestBody.Ended);
            Assert.Equal("12345678901234567890", App.RequestBody.Text);
        }

        [Fact]
        public void SecondChunkStartsOnNextLineAndWillBeCompleted()
        {
            App.OptionReadRequestBody = true;
            Input.Add(
@"POST / HTTP/1.1
Transfer-Encoding: chunked

a;foo=bar
1234567890
10
1234567890qwerty
");
            AssertInputState(false, Connection.Next.ReadMore, "");
            Assert.False(App.RequestBody.Ended);
            Assert.Equal("12345678901234567890qwerty", App.RequestBody.Text);
        }

        [Fact]
        public void ZeroLengthChunkEndsTransfer()
        {
            App.OptionReadRequestBody = true;
            Input.Add(
@"POST / HTTP/1.1
Transfer-Encoding: chunked

a;foo=bar
1234567890
10
1234567890qwerty
0
");
            AssertInputState(false, Connection.Next.NewFrame, "");
            Assert.True(App.RequestBody.Ended);
            Assert.Equal("12345678901234567890qwerty", App.RequestBody.Text);
        }

        [Fact]
        public void ChunkTransferNotAffectedByReceiveBoundaries()
        {
            App.OptionReadRequestBody = true;
            Input.AddIndividualBytes(
@"POST / HTTP/1.1
Transfer-Encoding: chunked

a;foo=bar
1234567890
10
1234567890qwerty
0
");
            AssertInputState(false, Connection.Next.NewFrame, "");
            Assert.True(App.RequestBody.Ended);
            Assert.Equal("12345678901234567890qwerty", App.RequestBody.Text);
        }


        [Fact]
        public void ExtraDataLeftForNextReqeust()
        {
            App.OptionReadRequestBody = true;
            Input.Add(
@"POST / HTTP/1.1
Transfer-Encoding: chunked

a;foo=bar
1234567890
10
1234567890qwerty
0
GET / ");
            AssertInputState(false, Connection.Next.NewFrame, "GET / ");
            Assert.True(App.RequestBody.Ended);
            Assert.Equal("12345678901234567890qwerty", App.RequestBody.Text);
        }
    }
}
