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

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return base.BeginRead(buffer, offset, count, callback, state);
        }
        
        public override int EndRead(IAsyncResult asyncResult)
        {
            return base.EndRead(asyncResult);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead { get { return true; } }

        public override bool CanSeek { get { return false; } }

        public override bool CanWrite { get { return false; } }

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