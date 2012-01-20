using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Dragonfly.Http;
using Dragonfly.Tests.Http;
using Xunit;

namespace Dragonfly.Tests.Fakes
{
    public class FakeInput
    {
        public FakeInput()
        {
            Baton = new Baton
                        {
                            Buffer = new ArraySegment<byte>(new byte[1024], 0, 0),
                            Next = Connection.Next.ReadMore
                        };
            WaitHandle = new ManualResetEvent(false);
            Encoding = Encoding.UTF8;
        }
        public Baton Baton { get; set; }
        public Func<Baton, Action, Action<Exception>, bool> Consume { get; set; }

        public bool Paused { get; set; }
        public ManualResetEvent WaitHandle { get; set; }
        public Exception LastException { get; set; }
        public Encoding Encoding { get; set; }

        public String Text
        {
            get
            {
                return Encoding.GetString(Baton.Buffer.Array, Baton.Buffer.Offset, Baton.Buffer.Count);
            }
        }

        public void Add(string text)
        {
            if (Paused)
                throw new InvalidOperationException("FakeInput.Add cannot be called when Paused is true");

            var count = Encoding.GetBytes(text, 0, text.Length, Baton.Buffer.Array, Baton.Buffer.Offset + Baton.Buffer.Count);
            Assert.Equal(text.Length, count);
            Baton.Buffer = new ArraySegment<byte>(
                Baton.Buffer.Array,
                Baton.Buffer.Offset,
                Baton.Buffer.Count + count);

            CallConsume();
        }

        public void End()
        {
            if (Paused)
                throw new InvalidOperationException("FakeInput.End cannot be called when End is true");

            Baton.Complete = true;
            CallConsume();
        }

        private void CallConsume()
        {
            WaitHandle.Reset();
            
            Paused = true;
            if (!Consume(Baton, Resume, Error))
                Paused = false;
        }

        public void Resume()
        {
            if (!Paused)
                throw new InvalidOperationException("FakeInput.Resume cannot be called when Paused is false");
            Paused = false;
            WaitHandle.Set();
        }

        public void Error(Exception ex)
        {
            if (!Paused)
                throw new InvalidOperationException("FakeInput.Error cannot be called when Paused is false");
            LastException = ex;
            Paused = false;
            WaitHandle.Set();
        }

        public void AddIndividualBytes(string text)
        {
            var data = text.ToArraySegment();
            foreach (var value in data.Array.Skip(data.Offset).Take(data.Count))
            {
                var available = Baton.Available(1);
                available.Array[available.Offset] = value;
                Baton.Extend(1);
                CallConsume();
                if (Paused)
                    throw new InvalidOperationException("Pause not implemented on this one yet");
            }
        }
    }
}
