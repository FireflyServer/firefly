using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Gate;
using Owin;

namespace Sandbox
{
    public static class WebSockets
    {
        public static IAppBuilder UseWebSockets(this IAppBuilder builder, string path, Func<Action<int, ArraySegment<byte>>, Action<int, ArraySegment<byte>>> service)
        {
            return builder.Use<AppDelegate>(app => Middleware(app, path, service));
        }

        public static AppDelegate Middleware(AppDelegate app, string path, Func<Action<int, ArraySegment<byte>>, Action<int, ArraySegment<byte>>> service)
        {
            return
                (env, result, fault) =>
                {
                    if ((string)env["owin.RequestPath"] != path)
                    {
                        app(env, result, fault);
                        return;
                    }

                    var requestBody = (BodyDelegate)env["owin.RequestBody"];
                    var requestHeaders = (IDictionary<string, IEnumerable<string>>)env["owin.RequestHeaders"];

                    var webSocketVersion = requestHeaders.GetHeader("Sec-WebSocket-Version");
                    var webSocketKey = requestHeaders.GetHeader("Sec-WebSocket-Key");
                    if (string.IsNullOrWhiteSpace(webSocketVersion) || string.IsNullOrWhiteSpace(webSocketKey))
                    {
                        app(env, result, fault);
                        return;
                    }

                    var webSocketAccept = Convert.ToBase64String(SHA1.Create().ComputeHash(Encoding.Default.GetBytes(webSocketKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));

                    var responseBody = BaseFramingProtocol(
                        requestBody,
                        service);

                    result(
                        "101 Switching Protocols",
                        new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase)
                            {
                                {"Connection", new[] {"Upgrade"}},
                                {"Upgrade", new[] {"websocket"}},
                                {"Sec-WebSocket-Accept", new[] {webSocketAccept}}
                            },
                        responseBody
                        );
                };
        }



        private static BodyDelegate BaseFramingProtocol(
            BodyDelegate requestBody,
            Func<Action<int, ArraySegment<byte>>, Action<int, ArraySegment<byte>>> service)
        {
            return
                (write, flush, end, cancel) =>
                {
                    Action<int, ArraySegment<byte>> outgoing =
                        (opcode, data) =>
                        {
                            Console.WriteLine("Outgoing opcode:{0}", opcode);
                            var bytes = new byte[data.Count + 2];
                            bytes[0] = (byte)(0x80 | opcode);
                            bytes[1] = (byte)data.Count;
                            Array.Copy(data.Array, data.Offset, bytes, 2, data.Count);
                            write(new ArraySegment<byte>(bytes, 0, bytes.Length));
                        };
                    var incoming = service(outgoing);

                    var buffer = new ArraySegment<byte>(new byte[128], 0, 0);

                    requestBody.Invoke(
                        data =>
                        {
                            buffer = Concat(buffer, data);
                            var header = 2;
                            if (buffer.Count < header)
                            {
                                return false;
                            }

                            var ch0 = buffer.Array[buffer.Offset];
                            var ch1 = buffer.Array[buffer.Offset + 1];
                            var fin = (ch0 >> 7) & 0x01;
                            var opcode = (ch0 >> 0) & 0x0f;
                            var mask = (ch1 >> 7) & 0x01;
                            var maskKey = new byte[] { 0, 0, 0, 0 };
                            var len = (ch1 >> 0) & 0x7f;
                            if (len == 126)
                            {
                                header = 4;
                                if (buffer.Count < header)
                                {
                                    return false;
                                }
                                len = (buffer.Array[buffer.Offset + 2] * 0x100) + buffer.Array[buffer.Offset + 3];
                            }
                            else if (len == 127)
                            {
                                header = 10;
                                if (buffer.Count < header)
                                {
                                    return false;
                                }
                                len =
                                    (buffer.Array[buffer.Offset + 6] * 0x1000000) + (buffer.Array[buffer.Offset + 7] * 0x10000) +
                                    (buffer.Array[buffer.Offset + 8] * 0x100) + buffer.Array[buffer.Offset + 9];
                            }
                            if (mask == 1)
                            {
                                header += 4;
                                if (buffer.Count < header)
                                {
                                    return false;
                                }
                                maskKey[0] = buffer.Array[buffer.Offset + header - 4];
                                maskKey[1] = buffer.Array[buffer.Offset + header - 3];
                                maskKey[2] = buffer.Array[buffer.Offset + header - 2];
                                maskKey[3] = buffer.Array[buffer.Offset + header - 1];
                            }
                            if (buffer.Count < header + len)
                            {
                                return false;
                            }
                            Console.WriteLine("fin:{0} opcode:{1} mask:{2} len:{3}", fin, opcode, mask, len);
                            if (mask == 1)
                            {
                                for (var index = 0; index != len; ++index)
                                {
                                    buffer.Array[buffer.Offset + header + index] = (byte)(buffer.Array[buffer.Offset + header + index] ^
                                                                                  maskKey[index & 0x03]);
                                }
                            }
                            var messageBody = new ArraySegment<byte>(buffer.Array, buffer.Offset + header, len);
                            buffer = new ArraySegment<byte>(buffer.Array, buffer.Offset + header + len, buffer.Count - header - len);
                            incoming(opcode, messageBody);
                            return false;
                        },
                        _ => false,
                        ex =>
                        {
                            Console.WriteLine("complete");
                            end(ex);
                        },
                        cancel);
                };
        }

        private static ArraySegment<byte> Concat(ArraySegment<byte> target, ArraySegment<byte> source)
        {
            if (target.Array.Length < target.Count + source.Count)
            {
                var grown = new byte[target.Count + source.Count];
                Array.Copy(target.Array, target.Offset, grown, 0, target.Count);
                Array.Copy(source.Array, source.Offset, grown, target.Count, source.Count);
                return new ArraySegment<byte>(grown, 0, target.Count + source.Count);
            }

            if (target.Array.Length - target.Offset < target.Count + source.Count)
            {
                Array.Copy(target.Array, target.Offset, target.Array, 0, target.Count);
                Array.Copy(source.Array, source.Offset, target.Array, target.Count, source.Count);
                return new ArraySegment<byte>(target.Array, 0, target.Count + source.Count);
            }

            Array.Copy(source.Array, source.Offset, target.Array, target.Offset + target.Count, source.Count);
            return new ArraySegment<byte>(target.Array, target.Offset, target.Count + source.Count);
        }
    }
}