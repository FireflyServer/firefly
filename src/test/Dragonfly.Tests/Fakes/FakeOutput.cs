using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dragonfly.Tests.Fakes
{
    public class FakeOutput
    {
        public FakeOutput()
        {
            MemoryStream = new MemoryStream();
            Encoding = Encoding.UTF8;
        }

        public bool ProduceData(ArraySegment<byte> data, Action resume)
        {
            MemoryStream.Write(data.Array, data.Offset, data.Count);
            return false;
        }

        public void ProduceEnd(bool keepAlive)
        {
            Ended = true;
            KeepAlive = keepAlive;
        }


        public bool Ended { get; set; }
        public bool KeepAlive { get; set; }
        public MemoryStream MemoryStream { get; set; }
        public Encoding Encoding { get; set; }

        public string Text
        {
            get { return Encoding.GetString(MemoryStream.ToArray()); }
        }
    }
}
