using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Dragonfly.Utils;

namespace Dragonfly.Http
{
    public abstract class MessageBody
    {
        private bool _cancel;

        Subscriber _activeSubscriber;
        readonly Subscriber _earlySubscriber;

        private Tuple<ArraySegment<byte>, Action> _earlyData;
        private Exception _earlyError;
        private bool _earlyComplete;
        public Action<bool> SubscribeCalled = hasEarlyData => { };

        class Subscriber
        {
            public Subscriber(Func<ArraySegment<byte>, Action, bool> next, Action<Exception> error, Action complete)
            {
                Next = next;
                Error = error;
                Complete = complete;
            }

            public Func<ArraySegment<byte>, Action, bool> Next { get; private set; }
            public Action<Exception> Error { get; private set; }
            public Action Complete { get; private set; }
        }

        public MessageBody()
        {
            _activeSubscriber = _earlySubscriber = new Subscriber(
                (data, continuation) =>
                {
                    var prior = Interlocked.CompareExchange(ref _earlyData, Tuple.Create(data, continuation), null);
                    // assert prior is null
                    return true;
                },
                ex => _earlyError = ex,
                () => _earlyComplete = true);
        }

        static bool TryGet(IDictionary<string, IEnumerable<string>> headers, string name, out string value)
        {
            IEnumerable<string> values;
            if (!headers.TryGetValue(name, out values) || values == null)
            {
                value = null;
                return false;
            }
            var count = values.Count();
            if (count == 0)
            {
                value = null;
                return false;
            }
            if (count == 1)
            {
                value = values.Single();
                return true;
            }
            value = string.Join(",", values.ToString());
            return true;
        }

        public static MessageBody For(string httpVersion, IDictionary<string, IEnumerable<string>> headers)
        {
            // see also http://tools.ietf.org/html/rfc2616#section-4.4

            var keepAlive = httpVersion != "HTTP/1.0";

            string connection;
            if (TryGet(headers, "Connection", out connection))
            {
                keepAlive = connection.Equals("keep-alive", StringComparison.OrdinalIgnoreCase);
            }

            string transferEncoding;
            if (TryGet(headers, "Transfer-Encoding", out transferEncoding))
            {
                return new ForChunkedEncoding(keepAlive);
            }

            string contentLength;
            if (TryGet(headers, "Content-Length", out contentLength))
            {
                return new ForContentLength(keepAlive, int.Parse(contentLength));
            }

            if (keepAlive)
            {
                return new ForContentLength(true, 0);
            }

            return new ForRemainingData();
        }

        public Action Subscribe(Func<ArraySegment<byte>, Action, bool> next, Action<Exception> error, Action complete)
        {
            var subscriber = new Subscriber(next, error, complete);
            var priorSubscriber = Interlocked.CompareExchange(ref _activeSubscriber, subscriber, _earlySubscriber);
            if (priorSubscriber != _earlySubscriber)
            {
                try
                {
                    error(new InvalidOperationException("MessageBody.Subscribe may only be called once"));
                }
                catch
                {
                }
                return () => { };
            }

            // repeat the early data to subscriber
            var earlyData = Interlocked.Exchange(ref _earlyData, null);

            SubscribeCalled(earlyData != null);

            if (earlyData != null)
            {
                var paused = next(earlyData.Item1, earlyData.Item2);
                if (!paused)
                    earlyData.Item2.Invoke();
            }
            else if (_earlyError != null)
            {
                _activeSubscriber.Error(_earlyError);
            }
            else if (_earlyComplete)
            {
                _activeSubscriber.Complete();
            }

            return () => _cancel = true;
        }

        public abstract bool Consume(Baton baton, Action callback, Action<Exception> fault);


        public class ForRemainingData : MessageBody
        {
            public override bool Consume(Baton baton, Action callback, Action<Exception> fault)
            {
                if (baton.Complete)
                {
                    _activeSubscriber.Complete();
                    baton.Next = Connection.Next.CloseConnection;
                    return false;
                }

                var consumed = baton.Take(baton.Buffer.Count);

                return _activeSubscriber.Next(consumed, callback);
            }
        }

        public class ForContentLength : MessageBody
        {
            private readonly bool _keepAlive;
            private readonly int _contentLength;
            private int _neededLength;

            public ForContentLength(bool keepAlive, int contentLength)
            {
                _keepAlive = keepAlive;
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
                    return _activeSubscriber.Next(consumed, callback);
                }

                baton.Next = _keepAlive ? Connection.Next.NewFrame : Connection.Next.CloseConnection;

                if (consumed.Count != 0)
                {
                    var delayed = _activeSubscriber.Next(
                        consumed,
                        () =>
                        {
                            _activeSubscriber.Complete();
                            callback();
                        });
                    if (delayed)
                    {
                        return true;
                    }
                    _activeSubscriber.Complete();
                    return false;
                }

                _activeSubscriber.Complete();
                return false;
            }
        }


        /// <summary>
        /// http://tools.ietf.org/html/rfc2616#section-3.6.1
        /// </summary>
        public class ForChunkedEncoding : MessageBody
        {
            private readonly bool _keepAlive;
            private int _neededLength;

            private Mode _mode = Mode.ChunkSizeLine;
            private enum Mode
            {
                ChunkSizeLine,
                ChunkData,
                ChunkDataCRLF,
                Complete,
            } ;


            public ForChunkedEncoding(bool keepAlive)
            {
                _keepAlive = keepAlive;
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
                                baton.Next = _keepAlive ? Connection.Next.NewFrame : Connection.Next.CloseConnection;
                                _activeSubscriber.Complete();
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

                            var paused = _activeSubscriber.Next(
                                consumed,
                                () =>
                                {
                                    try
                                    {
                                        if (!Consume(baton, callback, fault))
                                            callback();
                                    }
                                    catch (Exception ex)
                                    {
                                        fault(ex);
                                    }
                                });
                            if (paused)
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