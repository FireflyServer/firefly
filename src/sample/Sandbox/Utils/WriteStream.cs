using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sandbox.Utils
{
    class WriteStream : NotImplementedStream
    {
        private readonly Func<ArraySegment<byte>, Action, bool> _write;
        private Action _close = () => { };

        public WriteStream(Func<ArraySegment<byte>, Action, bool> write, Action close)
        {
            _write = write;
            _close = close;
        }

        public override bool CanWrite
        {
            get
            {
                return true;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _write(new ArraySegment<byte>(buffer, offset, count), null);
        }

        public override IAsyncResult BeginWrite(
            byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            var tcs = new TaskCompletionSource<object>();

            if (!_write(new ArraySegment<byte>(buffer, offset, count), () => tcs.SetResult(null)))
            {
                tcs.SetResult(null);
            }

            return tcs.Task;
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            var t = (Task)asyncResult;
            t.Wait();
        }

        public override void Close()
        {
            Interlocked.Exchange(ref _close, () => { }).Invoke();
        }
    }
}
