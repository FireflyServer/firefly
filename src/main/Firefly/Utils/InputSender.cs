using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Firefly.Utils
{


    public class InputSender
    {
        public struct Result
        {
            public bool Pending;
            public int BytesTransfered;
            public Exception Exception;
        }

        readonly object _lock = new object();

        ArraySegment<byte> _pushBuffer;
        Action<Result> _pushCallback;
        int _pushActive;
        Result _pushResult;

        ArraySegment<byte> _pullBuffer;
        Action<Result> _pullCallback;
        int _pullActive;
        Result _pullResult;


        public Result Push(ArraySegment<byte> buffer, Action<Result> callback)
        {
            var wasActive = Interlocked.CompareExchange(ref _pushActive, 1, 0);
            if (wasActive != 0)
            {
                return new Result { Exception = new InvalidOperationException("Concurrent operations not allowed.") };
            }
            lock (_lock)
            {
                try
                {
                    _pushBuffer = buffer;
                    _pushCallback = callback;
                    Tranceive();
                    if (!_pushResult.Pending)
                    {
                        return _pushResult;
                    }
                    return new Result { Pending = true };
                }
                finally
                {
                    Interlocked.Exchange(ref _pushActive, 0);
                }
            }

        }

        public Result Pull(ArraySegment<byte> buffer, Action<Result> callback)
        {
            var wasActive = Interlocked.CompareExchange(ref _pullActive, 1, 0);
            if (wasActive != 0)
            {
                return new Result { Exception = new InvalidOperationException("Concurrent operations not allowed.") };
            }
            try
            {
                lock (_lock)
                {
                    _pullBuffer = buffer;
                    _pullCallback = callback;
                    //}
                    Tranceive();
                    //lock (_lock)
                    //{
                    //TODO: inline completion
                }
                return new Result { Pending = true };
            }
            finally
            {
                Interlocked.Exchange(ref _pullActive, 0);
            }
        }

        void Tranceive()
        {
            if (_pushBuffer.Array == null || _pullBuffer.Array == null)
            {

            }
        }
    }
}
