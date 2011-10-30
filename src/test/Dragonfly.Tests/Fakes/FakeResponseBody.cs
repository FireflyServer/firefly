using System;
using System.Text;

namespace Dragonfly.Tests.Fakes
{
    public class FakeResponseBody 
    {
        public FakeResponseBody()
        {
            Encoding = Encoding.UTF8;
            Text = "";
        }

        public Action Subscribe(Func<ArraySegment<byte>, Action, bool> next, Action<Exception> error, Action complete)
        {
            next(Bytes, null);
            complete();
            return () => { };
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