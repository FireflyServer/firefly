using System;
using System.IO;
using System.Text;
using System.Threading;
using Owin;

namespace Firefly.Tests.Fakes
{
    public class FakeRequestBody
    {
        private int _subscribeCount;

        public FakeRequestBody(BodyDelegate body)
        {
            Body = body;
            MemoryStream = new MemoryStream();
            Encoding = Encoding.UTF8;
        }

        public BodyDelegate Body { get; set; }

        public int SubscribeCount
        {
            get
            {
                return _subscribeCount;
            }
            set
            {
                _subscribeCount = value;
            }
        }

        public MemoryStream MemoryStream { get; set; }

        public Encoding Encoding { get; set; }
        public bool Ended { get; set; }
        public bool Canceled { get; set; }
        public Exception LastException { get; set; }

        public string Text
        {
            get
            {
                return Encoding.GetString(MemoryStream.ToArray());
            }
        }

        public void Subscribe(
            Func<ArraySegment<byte>, bool> write,
            Func<Action, bool> flush,
            Action<Exception> end,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _subscribeCount);
            cancellationToken.Register(() => Canceled = true);
            Body(
                data =>
                {
                    MemoryStream.Write(data.Array, data.Offset, data.Count);
                    return write(data);
                },
                flush,
                ex =>
                {
                    LastException = ex;
                    Ended = true;
                    end(ex);
                },
                cancellationToken);
        }
    }
}
