using System;
using System.Diagnostics;
using System.Threading;

namespace Firefly.Utils
{
    public class InputSender
    {
        readonly IFireflyService _services;
        readonly object _lock = new object();

        Result _pushResult;
        Action<Result> _pushCallback;
        bool _pushNoMore;

        Result _pullResult;
        Action<Result> _pullCallback;
        bool _pullNoMore;

        public InputSender(IFireflyService services)
        {
            _services = services;
        }

        public bool Decoupled { get; set; }

        public struct Message
        {
            public object State;
            public ArraySegment<byte> Buffer;
            public bool Fin;
            public Exception Error;
        }

        public struct Result
        {
            public bool Pending;
            public Message Message;
        }

        public delegate Result TransferDelegate(Message message, Action<Result> callback);

        public Result Push(Message message, Action<Result> callback)
        {
            var result = new Result { Message = message, Pending = true };

            Action continuation;
            lock (_lock)
            {
                continuation = Tranceive(ref result, ref _pullResult);
                if (result.Pending)
                {
                    _pushResult = result;
                    _pushCallback = callback;
                }
            }
            if (continuation != null)
            {
                try { continuation.Invoke(); }
                catch
                {
                    _services.Trace.Event(TraceEventType.Warning, TraceMessage.InputSenderCallbackError);
                }
            }
            return result;
        }

        public Result Pull(Message message, Action<Result> callback)
        {
            var result = new Result { Message = message, Pending = true };

            Action continuation;
            lock (_lock)
            {
                continuation = Tranceive(ref _pushResult, ref result);
                if (result.Pending)
                {
                    _pullResult = result;
                    _pullCallback = callback;
                }
            }
            if (continuation != null)
            {
                continuation.Invoke();
            }
            return result;
        }

        Action Tranceive(ref Result pushResult, ref Result pullResult)
        {
            if (Decoupled || _pullNoMore || _pushNoMore)
            {
                if (pushResult.Pending)
                {
                    pushResult.Pending = false;
                    var pushArray = pushResult.Message.Buffer;
                    pushResult.Message.Buffer = pushArray.Array == null ? default(ArraySegment<byte>) : new ArraySegment<byte>(pushArray.Array, pushArray.Offset, pushArray.Count);
                    pushResult.Message.Fin = _pullNoMore;
                    pushResult.Message.Error = null;
                }
                if (pullResult.Pending)
                {
                    pullResult.Pending = false;
                    var pullArray = pullResult.Message.Buffer;
                    pullResult.Message.Buffer = pullArray.Array == null ? default(ArraySegment<byte>) : new ArraySegment<byte>(pullArray.Array, pullArray.Offset, 0);
                    pullResult.Message.Fin = _pushNoMore;
                    pullResult.Message.Error = null;
                }
            }
            else
            {
                if (!pushResult.Pending || !pullResult.Pending)
                {
                    return null;
                }

                pushResult.Pending = false;
                pullResult.Pending = false;

                var pushArray = pushResult.Message.Buffer;
                var pullArray = pullResult.Message.Buffer;

                var bytesTransfered = Math.Min(
                    pushArray.Count,
                    pullArray.Count);

                if (bytesTransfered != 0)
                {
                    Array.Copy(
                        pushArray.Array,
                        pushArray.Offset,
                        pullArray.Array,
                        pullArray.Offset,
                        bytesTransfered);
                }

                _pushNoMore |= pushResult.Message.Fin || (pushResult.Message.Error != null);
                _pullNoMore |= pullResult.Message.Fin || (pullResult.Message.Error != null);

                var fin = pushResult.Message.Fin;
                pushResult.Message.Fin = pullResult.Message.Fin;
                pullResult.Message.Fin = fin;

                var error = pushResult.Message.Error;
                pushResult.Message.Error = pullResult.Message.Error;
                pullResult.Message.Error = error;

                if (pushArray.Array != null)
                {
                    pushResult.Message.Buffer = new ArraySegment<byte>(pushArray.Array, pushArray.Offset + bytesTransfered, pushArray.Count - bytesTransfered);
                }
                if (pullArray.Array != null)
                {
                    pullResult.Message.Buffer = new ArraySegment<byte>(pullArray.Array, pullArray.Offset, bytesTransfered);
                }
            }

            Action<Result> callback;
            Result result;
            if (_pushCallback != null)
            {
                callback = _pushCallback;
                result = pushResult;
                _pushCallback = null;
                pushResult = default(Result);
            }
            else if (_pullCallback != null)
            {
                callback = _pullCallback;
                result = pullResult;
                _pullCallback = null;
                pullResult = default(Result);
            }
            else
            {
                return null;
            }

            return () =>
            {
                try
                {
                    callback.Invoke(result);
                }
                catch
                {
                    _services.Trace.Event(TraceEventType.Warning, TraceMessage.InputSenderCallbackError);
                }
            };
        }
    }
}

