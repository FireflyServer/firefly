using System;
using System.Collections.Generic;
using System.Threading;
using Firefly.Utils;

namespace Firefly.Http
{
    public static class DelegateExtensions
    {
        public static void InvokeNoThrow(this Action d)
        {
            try { d.Invoke(); }
            catch { }
        }
        public static void InvokeNoThrow<T>(this Action<T> d, T arg1)
        {
            try { d.Invoke(arg1); }
            catch { }
        }
    }

    public abstract class MessageBody
    {
        Subscriber _subscriber;
        private Action _continuation;

        public bool LocalIntakeFin { get; set; }
        public bool RequestKeepAlive { get; protected set; }

        protected MessageBody(Action continuation)
        {
            _continuation = continuation;
        }


        public static MessageBody For(
            string httpVersion, IDictionary<string, string[]> headers, Action continuation)
        {
            // see also http://tools.ietf.org/html/rfc2616#section-4.4

            var keepAlive = httpVersion != "HTTP/1.0";

            string connection;
            if (headers.TryGet("Connection", out connection))
            {
                keepAlive = connection.Equals("keep-alive", StringComparison.OrdinalIgnoreCase);
            }

            string transferEncoding;
            if (headers.TryGet("Transfer-Encoding", out transferEncoding))
            {
                return new ForChunkedEncoding(keepAlive, continuation);
            }

            string contentLength;
            if (headers.TryGet("Content-Length", out contentLength))
            {
                return new ForContentLength(keepAlive, int.Parse(contentLength), continuation);
            }

            if (keepAlive)
            {
                return new ForContentLength(true, 0, continuation);
            }

            return new ForRemainingData(continuation);
        }

        class Subscriber
        {
            readonly InputSender.TransferDelegate _transfer;
            Action<Exception> _callback;

            public Subscriber(InputSender.TransferDelegate transfer)
            {
                _transfer = transfer;
            }

            public bool End(Action<Exception> callback)
            {
                var result1 = _transfer.Invoke(
                    new InputSender.Message { Fin = true },
                    result2 => callback(result2.Message.Error));
                return result1.Pending;
            }

            public bool Write(ArraySegment<byte> data, Action<Exception> callback)
            {
                _callback = callback;
                return Transfer(data);
            }

            bool Transfer(ArraySegment<byte> data)
            {
                while (data.Count != 0)
                {
                    var result = _transfer(new InputSender.Message { Buffer = data }, TransferCallback);
                    if (result.Pending)
                        return true;

                    if (result.Message.Error != null)
                    {
                        _callback.InvokeNoThrow(result.Message.Error);
                    }
                    data = result.Message.Buffer;
                }
                return false;
            }

            void TransferCallback(InputSender.Result result)
            {
                if (result.Message.Error != null)
                {
                    _callback.InvokeNoThrow(result.Message.Error);
                    return;
                }
                try
                {
                    if (!Transfer(result.Message.Buffer))
                    {
                        _callback.InvokeNoThrow(null);
                    }
                }
                catch (Exception ex)
                {
                    _callback.InvokeNoThrow(ex);
                }
            }
        }

        public bool Drain(Action continuation)
        {
            if (_subscriber != null)
            {
                return false;
            }

            Subscribe((message, callback) =>
            {
                if (message.Fin || message.Error != null)
                {
                    continuation.Invoke();
                    return new InputSender.Result { Message = new InputSender.Message { State = message.State } };
                }

                return new InputSender.Result
                {
                    Message = new InputSender.Message
                    {
                        State = message.State,
                        Buffer = new ArraySegment<byte>(message.Buffer.Array, message.Buffer.Offset + message.Buffer.Count, 0)
                    }
                };
            });
            return true;
        }


        public void Subscribe(InputSender.TransferDelegate transfer)
        {
            var subscriber = new Subscriber(transfer);
            if (Interlocked.CompareExchange(ref _subscriber, subscriber, null) != null)
            {
                throw new InvalidOperationException("MessageBody.Subscribe may only be called once");
            }

            var continuation = Interlocked.Exchange(ref _continuation, null);
            if (continuation != null)
            {
                continuation.Invoke();
            }
        }

        public abstract bool Consume(Baton baton, Action<Exception> callback);


        class ForRemainingData : MessageBody
        {
            public ForRemainingData(Action continuation)
                : base(continuation)
            {
            }

            public override bool Consume(Baton baton, Action<Exception> callback)
            {
                if (baton.RemoteIntakeFin)
                {
                    LocalIntakeFin = true;
                    return _subscriber.End(callback);
                }

                var consumed = baton.Take(baton.Buffer.Count);
                return _subscriber.Write(consumed, callback);
            }
        }

        class ForContentLength : MessageBody
        {
            private readonly int _contentLength;
            private int _neededLength;

            public ForContentLength(bool keepAlive, int contentLength, Action continuation)
                : base(continuation)
            {
                RequestKeepAlive = keepAlive;
                _contentLength = contentLength;
                _neededLength = _contentLength;
            }

            public override bool Consume(Baton baton, Action<Exception> callback)
            {
                var consumeLength = Math.Min(_neededLength, baton.Buffer.Count);
                _neededLength -= consumeLength;

                var consumed = baton.Take(consumeLength);

                if (_neededLength != 0)
                {
                    // TODO: if check baton.Complete==true && neededlength != 0 then remote socket closed early
                    return _subscriber.Write(consumed, callback);
                }

                LocalIntakeFin = true;

                if (consumed.Count == 0)
                {
                    return _subscriber.End(callback);
                }

                if (_subscriber.Write(consumed, ex =>
                        {
                            if (ex != null)
                            {
                                callback(ex);
                            }
                            else
                            {
                                if (!_subscriber.End(callback))
                                    callback(null);
                            }
                        }))
                {
                    return true;
                }

                return _subscriber.End(callback);
            }
        }


        /// <summary>
        ///   http://tools.ietf.org/html/rfc2616#section-3.6.1
        /// </summary>
        class ForChunkedEncoding : MessageBody
        {
            private int _neededLength;

            private Mode _mode = Mode.ChunkSizeLine;

            private enum Mode
            {
                ChunkSizeLine,
                ChunkData,
                ChunkDataCRLF,
                Complete,
            } ;


            public ForChunkedEncoding(bool keepAlive, Action continuation)
                : base(continuation)
            {
                RequestKeepAlive = keepAlive;
            }

            public override bool Consume(Baton baton, Action<Exception> callback)
            {
                for (; ; )
                {
                    switch (_mode)
                    {
                        case Mode.ChunkSizeLine:
                            var chunkSize = 0;
                            if (!TakeChunkedLine(baton, ref chunkSize))
                            {
                                return false;
                            }

                            _neededLength = chunkSize;
                            if (chunkSize == 0)
                            {
                                _mode = Mode.Complete;
                                LocalIntakeFin = true;
                                _subscriber.End(null);
                                return false;
                            }
                            _mode = Mode.ChunkData;
                            break;

                        case Mode.ChunkData:
                            if (_neededLength == 0)
                            {
                                _mode = Mode.ChunkDataCRLF;
                                break;
                            }
                            if (baton.Buffer.Count == 0)
                            {
                                return false;
                            }

                            var consumeLength = Math.Min(_neededLength, baton.Buffer.Count);
                            _neededLength -= consumeLength;
                            var consumed = baton.Take(consumeLength);

                            if (_subscriber.Write(consumed, callback))
                            {
                                return true;
                            }
                            break;

                        case Mode.ChunkDataCRLF:
                            if (baton.Buffer.Count < 2)
                            {
                                return false;
                            }
                            var crlf = baton.Take(2);
                            if (crlf.Array[crlf.Offset] != '\r' ||
                                crlf.Array[crlf.Offset + 1] != '\n')
                            {
                                throw new NotImplementedException("INVALID REQUEST FORMAT");
                            }
                            _mode = Mode.ChunkSizeLine;
                            break;

                        default:
                            throw new NotImplementedException("INVALID REQUEST FORMAT");
                    }
                }
            }

            private static bool TakeChunkedLine(Baton baton, ref int chunkSizeOut)
            {
                var remaining = baton.Buffer;
                if (remaining.Count < 2)
                {
                    return false;
                }
                var ch0 = remaining.Array[remaining.Offset];
                var chunkSize = 0;
                var mode = 0;
                for (var index = 0; index != remaining.Count - 1; ++index)
                {
                    var ch1 = remaining.Array[remaining.Offset + index + 1];

                    if (mode == 0)
                    {
                        if (ch0 >= '0' && ch0 <= '9')
                        {
                            chunkSize = chunkSize * 0x10 + (ch0 - '0');
                        }
                        else if (ch0 >= 'A' && ch0 <= 'F')
                        {
                            chunkSize = chunkSize * 0x10 + (ch0 - ('A' - 10));
                        }
                        else if (ch0 >= 'a' && ch0 <= 'f')
                        {
                            chunkSize = chunkSize * 0x10 + (ch0 - ('a' - 10));
                        }
                        else
                        {
                            throw new NotImplementedException("INVALID REQUEST FORMAT");
                        }
                        mode = 1;
                    }
                    else if (mode == 1)
                    {
                        if (ch0 >= '0' && ch0 <= '9')
                        {
                            chunkSize = chunkSize * 0x10 + (ch0 - '0');
                        }
                        else if (ch0 >= 'A' && ch0 <= 'F')
                        {
                            chunkSize = chunkSize * 0x10 + (ch0 - ('A' - 10));
                        }
                        else if (ch0 >= 'a' && ch0 <= 'f')
                        {
                            chunkSize = chunkSize * 0x10 + (ch0 - ('a' - 10));
                        }
                        else if (ch0 == ';')
                        {
                            mode = 2;
                        }
                        else if (ch0 == '\r' && ch1 == '\n')
                        {
                            baton.Skip(index + 2);
                            chunkSizeOut = chunkSize;
                            return true;
                        }
                        else
                        {
                            throw new NotImplementedException("INVALID REQUEST FORMAT");
                        }
                    }
                    else if (mode == 2)
                    {
                        if (ch0 == '\r' && ch1 == '\n')
                        {
                            baton.Skip(index + 2);
                            chunkSizeOut = chunkSize;
                            return true;
                        }
                        else
                        {
                            // chunk-extensions not currently parsed
                        }
                    }

                    ch0 = ch1;
                }
                return false;
            }
        }
    }
}
