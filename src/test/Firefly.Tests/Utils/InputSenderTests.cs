using System;
using System.Linq;
using System.Text;
using Firefly.Tests.Fakes;
using Shouldly;
using Xunit;
using Firefly.Utils;

namespace Firefly.Tests.Utils
{
    public class InputSenderTests
    {
        InputSender _sender;
        ArraySegment<byte> _pushData;
        ArraySegment<byte> _pullBuffer;
        InputSender.Result? _pushCallbackResult;
        InputSender.Result? _pullCallbackResult;

        public InputSenderTests()
        {
            _sender = new InputSender(new FakeServices());
            _pushData = ToArraySegment("Alpha");
            _pullBuffer = ToArraySegment(1024);
        }

        void PushCallback(InputSender.Result result)
        {
            _pushCallbackResult = result;
        }

        void PullCallback(InputSender.Result result)
        {
            _pullCallbackResult = result;
        }

        static ArraySegment<byte> ToArraySegment(string text)
        {
            return new ArraySegment<byte>(Encoding.UTF8.GetBytes(text));
        }
        
        static ArraySegment<byte> ToArraySegment(int length)
        {
            return new ArraySegment<byte>(new byte[length]);
        }

        [Fact]
        public void PushWithoutPullShouldReturnPending()
        {
            var result = _sender.Push(new InputSender.Message { Buffer = _pushData }, PushCallback);

            result.Pending.ShouldBe(true);
            _pushCallbackResult.HasValue.ShouldBe(false);
        }

        [Fact]
        public void PullWithoutPushShouldReturnPending()
        {
            var result = _sender.Pull(new InputSender.Message { Buffer = _pullBuffer }, PullCallback);

            result.Pending.ShouldBe(true);
            _pullCallbackResult.HasValue.ShouldBe(false);
        }

        [Fact]
        public void PullAfterPushShouldCompleteSynchronously()
        {
            var pushResult = _sender.Push(new InputSender.Message { Buffer = _pushData }, PushCallback);

            var pullResult = _sender.Pull(new InputSender.Message { Buffer = _pullBuffer }, PullCallback);

            pushResult.Pending.ShouldBe(true);
            pullResult.Pending.ShouldBe(false);
        }

        [Fact]
        public void PullAfterPushShouldHaveData()
        {
            var pushResult = _sender.Push(new InputSender.Message { Buffer = _pushData }, PushCallback);

            var pullResult = _sender.Pull(new InputSender.Message { Buffer = _pullBuffer }, PullCallback);

            pullResult.Message.Buffer.Count.ShouldBe(_pushData.Count);
            ShouldHaveSameData(pullResult.Message.Buffer, _pushData);
        }

        void ShouldHaveSameData(ArraySegment<byte> arraySegment1, ArraySegment<byte> arraySegment2)
        {
            var enum1 = arraySegment1.Array.Skip(arraySegment1.Offset).Take(arraySegment1.Count);
            var enum2 = arraySegment2.Array.Skip(arraySegment2.Offset).Take(arraySegment2.Count);
            enum1.ShouldBe(enum2);
        }

        [Fact]
        public void PushAfterCompletePullShouldHaveZeroLengthBuffer()
        {
            var pullResult = _sender.Pull(new InputSender.Message { Buffer = _pullBuffer }, PullCallback);

            var pushResult = _sender.Push(new InputSender.Message { Buffer = _pushData }, PushCallback);

            pushResult.Message.Buffer.Count.ShouldBe(0);
            
        }

        [Fact]
        public void PushAfterSmallerPullShouldHaveSomeRemainingData()
        {
            var pullBuffer = ToArraySegment(5);
            var pullResult = _sender.Pull(new InputSender.Message { Buffer = pullBuffer }, PullCallback);

            var pushBuffer = ToArraySegment("Hello World");
            var pushResult = _sender.Push(new InputSender.Message { Buffer = pushBuffer }, PushCallback);

            pushResult.Message.Buffer.Count.ShouldBe(6);
            ShouldHaveSameData(pushResult.Message.Buffer, ToArraySegment(" World"));
        }

        [Fact]
        public void PullShouldTriggerPushCallbackOnly()
        {
            var pushResult = _sender.Push(new InputSender.Message {Buffer = _pushData}, PushCallback);
            pushResult.Pending.ShouldBe(true);

            _pushCallbackResult.HasValue.ShouldBe(false);

            var pullResult = _sender.Pull(new InputSender.Message {Buffer = _pullBuffer}, PullCallback);
            pullResult.Pending.ShouldBe(false);

            _pushCallbackResult.HasValue.ShouldBe(true);
            _pullCallbackResult.HasValue.ShouldBe(false);

            _pushCallbackResult.GetValueOrDefault().Pending.ShouldBe(false);
            _pushCallbackResult.GetValueOrDefault().Message.Buffer.Count.ShouldBe(0);
        }
    }    
}

