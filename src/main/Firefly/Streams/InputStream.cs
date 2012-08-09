using System;
using System.IO;
using System.Threading;
using Firefly.Utils;

namespace Firefly.Streams
{
    class InputStream : Stream
    {
        readonly Lazy<InputSender> _sender;

        int _readLength;
        bool _readFin;
        Exception _readError;

        public InputStream(Func<InputSender> subscribe)
        {
            _sender = new Lazy<InputSender>(subscribe);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public static readonly Action<InputSender.Result> ReadCallback = result =>
        {
            var readContext = (ReadContext)result.Message.State;
            readContext.Result = result;
            readContext.IsCompleted = true;
            readContext.SetWaitHandle();
            if (readContext.Callback != null)
            {
                readContext.Callback.Invoke(readContext);
            }
        };

        public override int Read(byte[] buffer, int offset, int count)
        {
            var readContext = new ReadContext();
            var result = _sender.Value.Pull(
                new InputSender.Message
                {
                    Buffer = new ArraySegment<byte>(buffer, offset, count),
                    State = readContext
                },
                ReadCallback);

            if (result.Pending)
            {
                readContext.AsyncWaitHandle.WaitOne();
                result = readContext.Result;
            }
            if (result.Message.Error != null)
            {
                throw new AggregateException(result.Message.Error);
            }
            return result.Message.Buffer.Count;
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            var readContext = new ReadContext { Self = this, Callback = callback, AsyncState = state };

            var result = _sender.Value.Pull(new InputSender.Message
            {
                Buffer = new ArraySegment<byte>(buffer, offset, count),
                State = readContext
            }, ReadCallback);

            if (result.Pending)
            {
                return readContext;
            }
            readContext.Result = result;
            readContext.IsCompleted = true;
            readContext.CompletedSynchronously = true;
            if (callback != null)
            {
                callback(readContext);
            }

            return readContext;
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            var readContext = (ReadContext)asyncResult;
            if (!readContext.IsCompleted)
            {
                readContext.AsyncWaitHandle.WaitOne();
            }
            if (readContext.Result.Message.Error != null)
            {
                throw new AggregateException(readContext.Result.Message.Error);
            }
            return readContext.Result.Message.Buffer.Count;
        }

        class ReadContext : IAsyncResult
        {
            public InputStream Self { get; set; }

            public AsyncCallback Callback { get; set; }

            public bool IsCompleted { get; set; }
            public object AsyncState { get; set; }
            public bool CompletedSynchronously { get; set; }

            public InputSender.Result Result { get; set; }

            WaitHandle _waitHandle;
            static readonly WaitHandle CompletedWaitHandle = new ManualResetEvent(true);

            public WaitHandle AsyncWaitHandle
            {
                get
                {
                    return _waitHandle ?? Interlocked.CompareExchange(ref _waitHandle, IsCompleted ? CompletedWaitHandle : new ManualResetEvent(false), null);
                }
            }

            public void SetWaitHandle()
            {
                var wh = AsyncWaitHandle;
                if (wh != CompletedWaitHandle)
                {
                    ((EventWaitHandle)wh).Set();
                }
            }
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
