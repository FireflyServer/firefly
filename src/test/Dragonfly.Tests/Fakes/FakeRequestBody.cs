using System;
using System.IO;
using System.Text;
using System.Threading;
using Dragonfly.Utils;
using Gate.Owin;

namespace Dragonfly.Tests.Fakes
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
            get { return _subscribeCount; }
            set { _subscribeCount = value; }
        }

        public MemoryStream MemoryStream { get; set; }

        public Encoding Encoding { get; set; }
        public bool Ended { get; set; }
        public bool Canceled { get; set; }
        public Exception LastException { get; set; }

        public string Text
        {
            get { return Encoding.GetString(MemoryStream.ToArray()); }
        }

        public Action Subscribe(
            Func<ArraySegment<byte>, Action, bool> next,
            Action<Exception> error,
            Action complete)
        {
            Interlocked.Increment(ref _subscribeCount);
            var cancel = Body(
                (data, continuation) =>
                {
                    MemoryStream.Write(data.Array, data.Offset, data.Count);
                    return next(data, continuation);
                },
                    ex =>
                    {
                        LastException = ex;
                        error(ex);
                    },
                    () =>
                    {
                        Ended = true;
                        complete();
                    });
            return () =>
                       {
                           Canceled = true;  cancel(); };
        }

    }
}