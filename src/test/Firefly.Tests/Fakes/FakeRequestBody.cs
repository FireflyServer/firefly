using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Firefly.Tests.Extensions;

namespace Firefly.Tests.Fakes
{
    public class FakeRequestBody
    {
        private int _subscribeCount;

        public FakeRequestBody(Stream body)
        {
            Body = body;
            MemoryStream = new MemoryStream();
            Encoding = Encoding.UTF8;
        }

        public Stream Body { get; set; }

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

        public Task Subscribe(
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _subscribeCount);
            cancellationToken.Register(() => Canceled = true);

            return Body.CopyToAsync(MemoryStream, 4096, cancellationToken)
                .Then(
                    () =>
                    {
                        Ended = true;
                    })
                    .Catch(info=>
                    {
                        LastException = info.Exception;
                        return info.Throw();
                    });
        }
    }
}
