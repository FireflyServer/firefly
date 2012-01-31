using Firefly.Http;
using Firefly.Tests.Fakes;
using Xunit;

namespace Firefly.Tests.Http
{
    public class FrameTestsBase
    {
        protected readonly FakeServices Services;
        protected readonly FakeApp App;
        protected readonly FakeOutput Output;
        protected readonly FakeInput Input;
        protected readonly Frame Frame;

        public FrameTestsBase()
        {
            Services = new FakeServices();
            App = new FakeApp();
            Output = new FakeOutput();
            Input = new FakeInput();
            Frame = new Frame(
                new FrameContext
                    {
                        Services = Services,
                        App = App.Call,
                        Write = Output.Write,
                        Flush = Output.Flush,
                        End = Output.End,
                    });
            Input.Consume = Frame.Consume;
        }

        protected void AssertInputState(bool paused, bool localIntakeFin, string text)
        {
            Assert.Equal(paused, Input.Paused);
            Assert.Equal(localIntakeFin, Frame.LocalIntakeFin);
            Assert.Equal(text, Input.Text);
        }

        protected void AssertOutputState(bool ended, int length, params string[] text)
        {
            Assert.Equal(ended, Output.Ended);
            Assert.Equal(length, Output.Text.Length);

            var searchIndex = 0;
            foreach (var segment in text)
            {
                var matchIndex = Output.Text.IndexOf(segment, searchIndex);
                Assert.NotEqual(-1, matchIndex);
                searchIndex = matchIndex + segment.Length;
            }
        }

        protected void AssertOutputState(bool ended, params string[] text)
        {
            Assert.Equal(ended, Output.Ended);

            var searchIndex = 0;
            foreach (var segment in text)
            {
                var matchIndex = Output.Text.IndexOf(segment, searchIndex);
                Assert.NotEqual(-1, matchIndex);
                searchIndex = matchIndex + segment.Length;
            }
        }
    }
}