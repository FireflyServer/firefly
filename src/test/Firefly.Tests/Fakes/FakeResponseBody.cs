using System;
using System.Text;
using System.Threading;

namespace Firefly.Tests.Fakes
{
    public class FakeResponseBody
    {
        public FakeResponseBody()
        {
            Encoding = Encoding.UTF8;
            Text = "";
        }

        public void Subscribe(
            Func<ArraySegment<byte>, bool> write,
            Func<Action, bool> flush,
            Action<Exception> end,
            CancellationToken cancellationtoken)
        {
            write(Bytes);
            end(null);
        }

        public ArraySegment<byte> Bytes { get; set; }
        public Encoding Encoding { get; set; }


        public string Text
        {
            get
            {
                return Encoding.GetString(Bytes.Array, Bytes.Offset, Bytes.Count);
            }
            set
            {
                var data = Encoding.GetBytes(value);
                Bytes = new ArraySegment<byte>(data, 0, data.Length);
            }
        }
    }
}
