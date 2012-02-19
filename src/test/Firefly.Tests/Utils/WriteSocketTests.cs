using System.Linq;
using Firefly.Tests.Extensions;
using Firefly.Tests.Fakes;
using Firefly.Utils;
using Xunit;

namespace Firefly.Tests.Utils
{
    public class WriteSocketTests
    {
        public FakeServices Services { get; set; }
        public FakeSocket Socket { get; set; }
        public SocketSender Sender { get; set; }

        public WriteSocketTests()
        {
            Services = new FakeServices();
            Socket = new FakeSocket();
            Sender = new SocketSender(Services, Socket);
        }

        [Fact]
        public void WriteWillCallSocketImmediatelyAndReturnFalseIfNotBuffering()
        {
            var buffering = Sender.Write("Hello".ToArraySegment());
            Assert.False(buffering);
            Assert.Equal("Hello", Socket.Output);
        }

        [Fact]
        public void WriteCanBeCalledManyTimesImmediately()
        {
            foreach (var loop in Enumerable.Range(0, 3))
            {
                var buffering = Sender.Write("Hello".ToArraySegment());
                Assert.False(buffering);
            }
            Assert.Equal("HelloHelloHello", Socket.Output);
        }

        [Fact]
        public void WriteWillReturnTrueWhenLargerThanOutputWindow()
        {
            Socket.OutputWindow = 10;
            var buffering = Sender.Write("Hello world!".ToArraySegment());
            Assert.True(buffering);
            Assert.Equal("Hello worl", Socket.Output);
        }

        [Fact]
        public void WriteWillContinueToReturnFalse()
        {
            Socket.OutputWindow = 10;
            var buffering = Sender.Write("Hello world!".ToArraySegment());
            var buffering2 = Sender.Write("Hello world!".ToArraySegment());
            Assert.True(buffering);
            Assert.True(buffering2);
            Assert.Equal("Hello worl", Socket.Output);
        }
    }
}
