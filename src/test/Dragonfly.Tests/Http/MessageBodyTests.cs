using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dragonfly.Http;
using Dragonfly.Utils;
using Xunit;

namespace Dragonfly.Tests.Http
{
    public class MessageBodyTests
    {
        [Fact]
        public void SubscribedBodyPassesInformationDirectly()
        {
            // Arrange
            var resultData = new ArraySegment<byte>();

            var body = new MessageBody.ForRemainingData(()=> { });
            var cancel = body.Subscribe(
                (data, continuation) =>
                    {
                        resultData = data;
                        return false;
                    },
                ex => { },
                () => { });

            // Act
            var baton = new Baton {Buffer = "Hello World".ToArraySegment()};
            var sr = body.Consume(
                baton,
                () => { },
                ex => { });

            // Assert
            Assert.False(sr);
            Assert.NotNull(baton.Buffer);
            Assert.Equal(0, baton.Buffer.Count);
            Assert.NotNull(resultData.Array);
            Assert.Equal(11, resultData.Count);
        }

        [Fact]
        public void SubscriberCanPauseConsumption()
        {
            // Arrange
            var nextData = new ArraySegment<byte>();
            Action nextContinuation = null;
            var body = new MessageBody.ForRemainingData(()=> { });
            var cancel = body.Subscribe(
                (data, continuation) =>
                {
                    nextData = data;
                    nextContinuation = continuation;
                    return continuation != null;
                },
                ex => { },
                () => { });

            // Act
            var baton = new Baton { Buffer = "Hello World".ToArraySegment() };

            var resumed = false;
            var paused = body.Consume(
                baton,
                () => resumed = true,
                ex => { });

            var dataBytesBeforeResume = nextData;
            var resumedBeforeResume = resumed;

            nextContinuation();

            // Assert
            Assert.True(paused);
            Assert.False(resumedBeforeResume);
            Assert.NotNull(dataBytesBeforeResume);
            Assert.NotNull(dataBytesBeforeResume.Array);
            Assert.Equal(0, baton.Buffer.Count);
        }


        //[Fact]
        //public void ZeroByteSegmentCompletesObserverAndIndicatesConnectionClose()
        //{
        //    // Arrange
        //    ArraySegment<byte>? dataBytes = null;
        //    var body = new MessageBody.ForRemainingData();
        //    var dataComplete = false;
        //    var cancel = body.Subscribe(
        //        data => dataBytes = data.Bytes,
        //        ex => { },
        //        () => dataComplete = true);

        //    // Act
        //    var sr = body.Consume(
        //        "Hello World".ToArraySegment(),
        //        ar => { },
        //        ex => { });

        //    var dataBytesBetweenCalls = dataBytes;
        //    var dataCompleteBetweenCalls = dataComplete;
        //    dataBytes = null;

        //    var sr2 = body.Consume(
        //        "".ToArraySegment(),
        //        ar => { },
        //        ex => { });


        //    // Assert
        //    Assert.NotNull(sr);
        //    Assert.Equal(Connection.Next.ReadMore, sr.Item2);
        //    Assert.NotNull(dataBytesBetweenCalls);
        //    Assert.Equal(11, dataBytesBetweenCalls.Value.Count);
        //    Assert.False(dataCompleteBetweenCalls);
        //    Assert.NotNull(sr2);
        //    Assert.Equal(Connection.Next.CloseConnection, sr2.Item2);
        //    Assert.Null(dataBytes);
        //    Assert.True(dataComplete);
        //}

        //[Fact]
        //public void ContentLengthStopsConsumingBytesAtContentLength()
        //{
        //    // Arrange
        //    var body = new MessageBody.ForContentLength(false, 5);
        //    var dataBytes = new ArraySegment<byte>();
        //    bool dataComplete = false;
        //    var cancel = body.Subscribe(
        //        data => dataBytes = data.Bytes,
        //        ex => { },
        //        () => dataComplete = true);

        //    // Act
        //    var sr = body.Consume(
        //        "Hello World".ToArraySegment(),
        //        ar => { },
        //        ex => { });

        //    // Assert
        //    Assert.NotNull(dataBytes.Array);
        //    Assert.Equal(5, dataBytes.Count);
        //    Assert.True(dataComplete);

        //    Assert.NotNull(sr);
        //    Assert.Equal(6, sr.Item1.Count);
        //    Assert.Equal(Connection.Next.CloseConnection, sr.Item2);
        //}
    }
}
