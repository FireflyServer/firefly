using System;
using System.Text;
using Firefly.Http;
using Firefly.Tests.Extensions;
using Firefly.Tests.Fakes;
using Xunit;

namespace Firefly.Tests.Http
{
    public class BatonTests
    {
        [Fact]
        public void SkipAdvancesBuffer()
        {
            // Arrange
            var baton = new Baton(new FakeMemoryPool()) {Buffer = "xxhello world".ToArraySegment()};
            baton.Skip(2);

            // Act
            baton.Skip(5);

            // Assert
            Assert.Equal(6, baton.Buffer.Count);
            Assert.Equal(" world", baton.Buffer.ToString(Encoding.Default));
        }

        [Fact]
        public void TakeAdvancesBufferAndReturnsTakenSegment()
        {
            // Arrange
            var baton = new Baton(new FakeMemoryPool()) {Buffer = "xxhello world".ToArraySegment()};
            baton.Skip(2);

            // Act
            var taken = baton.Take(5);

            // Assert
            Assert.Equal(6, baton.Buffer.Count);
            Assert.Equal(" world", baton.Buffer.ToString(Encoding.Default));
            Assert.Equal(5, taken.Count);
            Assert.Equal("hello", taken.ToString(Encoding.Default));
        }


        [Fact]
        public void ExtendCausesArraySegmentToIncludeMoreBytesAtTheEnd()
        {
            // Arrange
            var baton = new Baton(new FakeMemoryPool()) {Buffer = "xxhello worldxx".ToArraySegment()};
            baton.Buffer = new ArraySegment<byte>(baton.Buffer.Array, 2, 5);

            // Act
            var before = baton.Buffer.ToString(Encoding.Default);
            baton.Extend(5);
            var after = baton.Buffer.ToString(Encoding.Default);

            // Assert
            Assert.Equal("hello", before);
            Assert.Equal("hello worl", after);
            Assert.Equal(2, baton.Buffer.Offset);
            Assert.Equal(10, baton.Buffer.Count);
        }

        [Fact]
        public void AvailableBufferReturnsAreaThatIsUnused()
        {
            // Arrange
            var baton = new Baton(new FakeMemoryPool()) {Buffer = "xxhello worldxx".ToArraySegment()};
            baton.Buffer = new ArraySegment<byte>(baton.Buffer.Array, 2, 5);

            // Act
            var buffer = baton.Available(0);

            // Assert
            Assert.Equal(8, buffer.Count);
            Assert.Equal(" worldxx", buffer.ToString(Encoding.Default));
        }

        [Fact]
        public void AvailableBufferBringsOffsetBackToZeroIfOccupiedSegmentIsZeroLength()
        {
            // Arrange
            var baton = new Baton(new FakeMemoryPool()) {Buffer = "xxhello worldxx".ToArraySegment()};
            baton.Buffer = new ArraySegment<byte>(baton.Buffer.Array, 2, 0);

            // Act
            var buffer = baton.Available(0);

            // Assert
            Assert.Equal(15, buffer.Count);
            Assert.Equal("xxhello worldxx", buffer.ToString(Encoding.Default));
        }
    }
}
