using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gate.Owin;

// ReSharper disable AccessToModifiedClosure

namespace Dragonfly.Http
{
    public class Frame
    {
        private readonly AppDelegate _app;
        private readonly Func<ArraySegment<byte>, Action, bool> _produceData;
        private readonly Action _produceEnd;

        Mode _mode;
        enum Mode
        {
            StartLine,
            MessageHeader,
            MessageBody,
            Terminated,
        }

        private string _method;
        private string _requestUri;
        private string _path;
        private string _queryString;
        private string _httpVersion;
        private readonly IDictionary<string, IEnumerable<string>> _headers = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);
        private MessageBody _messageBody;
        private bool _resultStarted;
        private bool _keepAlive;

        public Frame(AppDelegate app, Func<ArraySegment<byte>, Action, bool> produceData, Action<bool> produceEnd)
        {
            _app = app;
            _produceData = produceData;
            _produceEnd = () =>
            {
                if (!_messageBody.Drain(() => produceEnd(_keepAlive)))
                    produceEnd(_keepAlive);
            };
        }

        public bool LocalIntakeFin
        {
            get
            {
                return _mode == Mode.MessageBody
                    ? _messageBody.LocalIntakeFin
                    : _mode == Mode.Terminated;
            }
        }

        public bool Consume(Baton baton, Action callback, Action<Exception> fault)
        {
            for (; ; )
            {
                switch (_mode)
                {
                    case Mode.StartLine:
                        if (baton.RemoteIntakeFin)
                        {
                            _mode = Mode.Terminated;
                            return false;
                        }

                        if (!TakeStartLine(baton))
                        {
                            return false;
                        }

                        _mode = Mode.MessageHeader;
                        break;

                    case Mode.MessageHeader:
                        if (baton.RemoteIntakeFin)
                        {
                            _mode = Mode.Terminated;
                            return false;
                        }

                        var endOfHeaders = false;
                        while (!endOfHeaders)
                        {
                            if (!TakeMessageHeader(baton, out endOfHeaders))
                                return false;
                        }

                        var resumeBody = HandleExpectContinue(callback);
                        _messageBody = MessageBody.For(
                            _httpVersion,
                            _headers,
                            () =>
                            {
                                if (!Consume(baton, resumeBody, fault))
                                    resumeBody.Invoke();
                            });
                        _keepAlive = _messageBody.RequestKeepAlive;
                        _mode = Mode.MessageBody;
                        Execute();
                        return true;

                    case Mode.MessageBody:
                        return _messageBody.Consume(baton, callback, fault);

                    case Mode.Terminated:
                        return false;
                }
            }
        }

        Action HandleExpectContinue(Action continuation)
        {
            IEnumerable<string> expect;
            if (_httpVersion.Equals("HTTP/1.1") &&
                _headers.TryGetValue("Expect", out expect) &&
                (expect.FirstOrDefault() ?? "").Equals("100-continue", StringComparison.OrdinalIgnoreCase))
            {
                return
                    () =>
                    {
                        if (!_resultStarted)
                        {
                            continuation.Invoke();
                        }
                        else
                        {
                            var bytes = Encoding.Default.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
                            if (!_produceData(new ArraySegment<byte>(bytes), continuation))
                                continuation.Invoke();
                        }
                    };
            }
            return continuation;
        }

        private void Execute()
        {
            var env = CreateOwinEnvironment();
            _app(
                env,
                (status, headers, body) =>
                {
                    _resultStarted = true;

                    Action sendResponseBody =
                        () => body(
                            _produceData,
                            ex => _produceEnd(),
                            () => _produceEnd());

                    if (!_produceData(CreateResponseHeader(status, headers), sendResponseBody))
                        sendResponseBody.Invoke();
                },
                ex => _produceEnd());

            return;
        }

        private ArraySegment<byte> CreateResponseHeader(string status, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
        {
            var sb = new StringBuilder(128);
            sb.Append("HTTP/1.1 ").AppendLine(status);

            var hasConnection = false;
            var hasTransferEncoding = false;
            var hasContentLength = false;
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    var isConnection = false;
                    if (!hasConnection &&
                        string.Equals(header.Key, "Connection", StringComparison.OrdinalIgnoreCase))
                    {
                        hasConnection = isConnection = true;
                    }
                    else if (!hasTransferEncoding &&
                        string.Equals(header.Key, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                    {
                        hasTransferEncoding = true;
                    }
                    else if (!hasContentLength &&
                        string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
                    {
                        hasContentLength = true;
                    }

                    foreach (var value in header.Value)
                    {
                        sb.Append(header.Key).Append(": ").Append(value).Append("\r\n");
                        if (isConnection && value.IndexOf("close", StringComparison.OrdinalIgnoreCase) != -1)
                        {
                            _keepAlive = false;
                        }
                    }
                }
            }

            if (hasTransferEncoding == false && hasContentLength == false)
            {
                _keepAlive = false;
            }
            if (_keepAlive == false && hasConnection == false)
            {
                sb.Append("Connection: close\r\n");
            }

            sb.Append("\r\n");
            return new ArraySegment<byte>(Encoding.Default.GetBytes(sb.ToString()));
        }

        private bool TakeStartLine(Baton baton)
        {
            var remaining = baton.Buffer;
            if (remaining.Count < 2) return false;
            var firstSpace = -1;
            var secondSpace = -1;
            var questionMark = -1;
            var ch0 = remaining.Array[remaining.Offset];
            for (var index = 0; index != remaining.Count - 1; ++index)
            {
                var ch1 = remaining.Array[remaining.Offset + index + 1];
                if (ch0 == '\r' && ch1 == '\n')
                {
                    if (secondSpace == -1)
                    {
                        throw new InvalidOperationException("INVALID REQUEST FORMAT");
                    }
                    _method = GetString(remaining, 0, firstSpace);
                    _requestUri = GetString(remaining, firstSpace + 1, secondSpace);
                    if (questionMark == -1)
                    {
                        _path = _requestUri;
                        _queryString = string.Empty;
                    }
                    else
                    {
                        _path = GetString(remaining, firstSpace + 1, questionMark);
                        _queryString = GetString(remaining, questionMark + 1, secondSpace);
                    }
                    _httpVersion = GetString(remaining, secondSpace + 1, index);
                    baton.Skip(index + 2);
                    return true;
                }

                if (ch0 == ' ' && firstSpace == -1)
                {
                    firstSpace = index;
                }
                else if (ch0 == ' ' && firstSpace != -1 && secondSpace == -1)
                {
                    secondSpace = index;
                }
                else if (ch0 == '?' && firstSpace != -1 && questionMark == -1 && secondSpace == -1)
                {
                    questionMark = index;
                }
                ch0 = ch1;
            }
            return false;
        }

        static string GetString(ArraySegment<byte> range, int startIndex, int endIndex)
        {
            return Encoding.Default.GetString(range.Array, range.Offset + startIndex, endIndex - startIndex);
        }


        private bool TakeMessageHeader(Baton baton, out bool endOfHeaders)
        {
            var remaining = baton.Buffer;
            endOfHeaders = false;
            if (remaining.Count < 2) return false;
            var ch0 = remaining.Array[remaining.Offset];
            var ch1 = remaining.Array[remaining.Offset + 1];
            if (ch0 == '\r' && ch1 == '\n')
            {
                endOfHeaders = true;
                baton.Skip(2);
                return true;
            }

            if (remaining.Count < 3) return false;
            var wrappedHeaders = false;
            var colonIndex = -1;
            var valueStartIndex = -1;
            var valueEndIndex = -1;
            for (var index = 0; index != remaining.Count - 2; ++index)
            {
                var ch2 = remaining.Array[remaining.Offset + index + 2];
                if (ch0 == '\r' &&
                    ch1 == '\n' &&
                    ch2 != ' ' &&
                    ch2 != '\t')
                {
                    var name = Encoding.ASCII.GetString(remaining.Array, remaining.Offset, colonIndex);
                    var value = "";
                    if (valueEndIndex != -1)
                        value = Encoding.ASCII.GetString(remaining.Array, remaining.Offset + valueStartIndex, valueEndIndex - valueStartIndex);
                    if (wrappedHeaders)
                        value = value.Replace("\r\n", " ");
                    AddRequestHeader(name, value);
                    baton.Skip(index + 2);
                    return true;
                }
                if (colonIndex == -1 && ch0 == ':')
                {
                    colonIndex = index;
                }
                else if (colonIndex != -1 &&
                    ch0 != ' ' &&
                    ch0 != '\t' &&
                    ch0 != '\r' &&
                    ch0 != '\n')
                {
                    if (valueStartIndex == -1)
                        valueStartIndex = index;
                    valueEndIndex = index + 1;
                }
                else if (!wrappedHeaders &&
                    ch0 == '\r' &&
                    ch1 == '\n' &&
                    (ch2 == ' ' ||
                    ch2 == '\t'))
                {
                    wrappedHeaders = true;
                }

                ch0 = ch1;
                ch1 = ch2;
            }
            return false;
        }


        private void AddRequestHeader(string name, string value)
        {
            IEnumerable<string> existing;
            if (_headers.TryGetValue(name, out existing))
            {
                _headers[name] = existing.Concat(new[] { value });
            }
            else
            {
                _headers[name] = new[] { value };
            }
        }

        private IDictionary<string, object> CreateOwinEnvironment()
        {
            IDictionary<string, object> env = new Dictionary<string, object>();
            env["owin.RequestMethod"] = _method;
            env["owin.RequestPath"] = _path;
            env["owin.RequestPathBase"] = "";
            env["owin.RequestQueryString"] = _queryString;
            env["owin.RequestHeaders"] = _headers;
            env["owin.RequestBody"] = (BodyDelegate)_messageBody.Subscribe;
            env["owin.RequestScheme"] = "http"; // TODO: pass along information about scheme, cgi headers, etc
            env["owin.Version"] = "1.0";
            return env;
        }
    }
}
