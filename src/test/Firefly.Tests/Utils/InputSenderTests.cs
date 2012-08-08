using System;
using System.Text;
using Shouldly;
using Xunit;
using Firefly.Utils;

namespace Firefly.Tests.Utils
{
    public class InputSenderTests
    {
        InputSender _sender;
        ArraySegment<byte> _data;
        InputSender.Result? _callbackResult;

        public InputSenderTests()
        {
            _sender = new InputSender();
            _data = new ArraySegment<byte>(Encoding.UTF8.GetBytes("Alpha"));
        }

        [Fact]
        public void PushWithoutPullShouldReturnPending()
        {
            var result = _sender.Push(_data, Callback);

            result.Pending.ShouldBe(true);
            _callbackResult.HasValue.ShouldBe(false);
        }

        [Fact]
        public void PullWithoutPushShouldReturnPending()
        {
            var result = _sender.Pull(_data, Callback);

            result.Pending.ShouldBe(true);
            _callbackResult.HasValue.ShouldBe(false);
        }

        void Callback(InputSender.Result result)
        {
            _callbackResult = result;
        }
    }
}
