using System;
using System.Collections.Generic;
using System.Threading;
using Firefly.Utils;
using Owin;

namespace Firefly.Http
{
    public abstract class MessageBody
    {
        Subscriber _subscriber;
        private Action _continuation;
        private bool _cancel;

        public bool LocalIntakeFin { get; set; }
        public bool RequestKeepAlive { get; protected set; }

        class Subscriber
        {
            public Subscriber(
                Func<ArraySegment<byte>, bool> write,
                Func<Action, bool> flush,
                Action<Exception> end,
                CancellationToken cancellationToken)
            {
                Write = write;
                Flush = flush;
                End = end;
                CancellationToken = cancellationToken;
            }

            public Func<ArraySegment<byte>, bool> Write { get; set; }
            public Func<Action, bool> Flush { get; set; }
            public Action<Exception> End { get; set; }
            public CancellationToken CancellationToken { get; set; }

        }

        protected MessageBody(Action continuation)
        {
            _continuation = continuation;
        }

        public static MessageBody For(string httpVersion, IDictionary<string, IEnumerable<string>> headers, Action continuation)
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

        public bool Drain(Action continuation)
        {
            if (_subscriber != null) return false;

            Subscribe(
                _ => false,
                _ => false,
                _ => continuation(),
                CancellationToken.None);

            return true;
        }


        public void Subscribe(
            Func<ArraySegment<byte>, bool> write,
            Func<Action, bool> flush,
            Action<Exception> end,
            CancellationToken cancellationToken)
        {
            var subscriber = new Subscriber(write, flush, end, cancellationToken);
            if (Interlocked.CompareExchange(ref _subscriber, subscriber, null) != null)
            {
                try
                {
                    end(new InvalidOperationException("MessageBody.Subscribe may only be called once"));
                }
                catch
                {
                }
                return;
            }

            if (_subscriber.CancellationToken.IsCancellationRequested)
            {
                _cancel = true;
            }
            else
            {
                _subscriber.CancellationToken.Register(() => _cancel = true);
            }

            var continuation = Interlocked.Exchange(ref _continuation, null);
            if (continuation != null)
                continuation.Invoke();
        }

        public abstract bool Consume(Baton baton, Action callback, Action<Exception> fault);


        class ForRemainingData : MessageBody
        {
            public ForRemainingData(Action continuation)
                : base(continuation)
            {
            }

            public override bool Consume(Baton baton, Action callback, Action<Exception> fault)
            {
                if (baton.RemoteIntakeFin)
                {
                    LocalIntakeFin = true;
                    _subscriber.End(null);
                    return false;
                }

                var consumed = baton.Take(baton.Buffer.Count);
                return _subscriber.Write(consumed) && _subscriber.Flush(callback);
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

            public override bool Consume(Baton baton, Action callback, Action<Exception> fault)
            {
                var consumeLength = Math.Min(_neededLength, baton.Buffer.Count);
                _neededLength -= consumeLength;

                var consumed = baton.Take(consumeLength);

                if (_neededLength != 0)
                {
                    // TODO: if check baton.Complete==true && neededlength != 0 then remote socket closed early
                    return _subscriber.Write(consumed) && _subscriber.Flush(callback);
                }

                LocalIntakeFin = true;

                if (consumed.Count != 0)
                {
                    if (_subscriber.Write(consumed) &&
                        _subscriber.Flush(() => { _subscriber.End(null); callback(); }))
                    {
                        return true;
                    }
                }

                _subscriber.End(null);
                return false;
            }
        }


        /// <summary>
        /// http://tools.ietf.org/html/rfc2616#section-3.6.1
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

            public override bool Consume(Baton baton, Action callback, Action<Exception> fault)
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

                            if (_subscriber.Write(consumed) &&
                                _subscriber.Flush(callback))
                            {
                                return true;
                            }                           
                            break;

                        case Mode.ChunkDataCRLF:
                            if (baton.Buffer.Count < 2)
                                return false;
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
                if (remaining.Count < 2) return false;
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