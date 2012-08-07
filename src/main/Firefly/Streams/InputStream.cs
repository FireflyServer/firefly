using System;
using System.IO;
using System.Threading;

namespace Firefly.Streams
{
    class InputStream : Stream
    {
        readonly Action<Func<ArraySegment<byte>, bool>, Func<Action, bool>, Action<Exception>, CancellationToken> _subscribe;

        public InputStream(Action<Func<ArraySegment<byte>, bool>, Func<Action, bool>, Action<Exception>, CancellationToken> subscribe)
        {
            _subscribe = subscribe;
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override bool CanSeek
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override bool CanWrite
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override long Length
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override long Position { get; set; }
    }
}