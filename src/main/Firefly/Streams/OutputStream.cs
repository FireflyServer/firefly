using System;
using System.IO;

namespace Firefly.Streams
{
    class OutputStream : Stream
    {
        readonly Func<ArraySegment<byte>, bool> _write;
        readonly Func<Action, bool> _flush;

        public OutputStream(Func<ArraySegment<byte>, bool> write, Func<Action, bool> flush)
        {
            _write = write;
            _flush = flush;
        }

        public override void Flush()
        {
            _flush(null);
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
            _write(new ArraySegment<byte>(buffer, offset, count));
        }

        public override bool CanRead
        {
            get
            {
                return false;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return true;
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