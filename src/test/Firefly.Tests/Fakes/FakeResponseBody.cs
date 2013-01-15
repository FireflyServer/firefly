using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Firefly.Tests.Extensions;

namespace Firefly.Tests.Fakes
{
    public class FakeResponseBody
    {
        public FakeResponseBody()
        {
            Encoding = Encoding.UTF8;
            Text = "";
        }

        public Task Subscribe(Stream output)
        {
            return output.WriteAsync(Bytes.Array, Bytes.Offset, Bytes.Count, CancellationToken.None);
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
