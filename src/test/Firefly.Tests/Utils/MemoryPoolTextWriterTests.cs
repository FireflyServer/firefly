using Firefly.Tests.Fakes;
using Firefly.Utils;
using Xunit;

namespace Firefly.Tests.Utils
{
    public class MemoryPoolTextWriterTests
    {
        public MemoryPoolTextWriter Writer { get; set; }
        public FakeMemoryPool Pool { get; set; }

        public MemoryPoolTextWriterTests()
        {
            Pool = new FakeMemoryPool();
            Writer = new MemoryPoolTextWriter(Pool);
        }

        [Fact]
        public void StartsWithEmptySegment()
        {
            Assert.Equal(0, Writer.Buffer.Count);
        }

        [Fact]
        public void FlushAloneDoesntCauseWrite()
        {
            Writer.Flush();
            Assert.Equal(0, Writer.Buffer.Count);
        }

        [Fact]
        public void AddingTextDoesntAutomaticallyCreateByteData()
        {
            Writer.Write('A');
            Writer.Write('B');
            Writer.Write('C');
            Writer.Write("hello");
            Assert.Equal(0, Writer.Buffer.Count);
        }

        [Fact]
        public void CallingFlushForcesBufferToStartPopulating()
        {
            Writer.Write('A');
            Writer.Write('B');
            Writer.Write('C');
            Writer.Write("hello");
            Writer.Flush();
            Assert.Equal(8, Writer.Buffer.Count);
            Assert.Equal(65, Writer.Buffer.Array[0]);
            Assert.Equal(66, Writer.Buffer.Array[1]);
            Assert.Equal(67, Writer.Buffer.Array[2]);
        }
    }
}
