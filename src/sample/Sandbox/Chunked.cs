using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gate.Owin;

namespace Sandbox
{
    public static class Chunked
    {
        public static IAppBuilder UseChunked(this IAppBuilder builder)
        {
            return builder.Use<AppDelegate>(Middleware);
        }

        static readonly ArraySegment<byte> EndOfChunk = new ArraySegment<byte>(Encoding.ASCII.GetBytes("\r\n"));
        static readonly ArraySegment<byte> FinalChunk = new ArraySegment<byte>(Encoding.ASCII.GetBytes("0\r\n\r\n"));

        public static AppDelegate Middleware(AppDelegate app)
        {
            return
                (env, result, fault) =>
                {
                    app(
                        env,
                        (status, headers, body) =>
                        {
                            if (IsStatusWithNoNoEntityBody(status) ||
                                headers.ContainsKey("Content-Length") ||
                                headers.ContainsKey("Transfer-Encoding"))
                            {
                                result(status, headers, body);
                            }
                            else
                            {
                                headers["Transfer-Encoding"] = new[] { "chunked" };

                                result(status, headers,
                                       (write, error, end) => body(
                                           (data, continuation) =>
                                           {
                                               if (data.Count == 0)
                                               {
                                                   return write(data, continuation);
                                               }

                                               var chunkPrefix = new ArraySegment<byte>(Encoding.ASCII.GetBytes(data.Count.ToString("x") + "\r\n"));

                                               if (continuation == null)
                                               {
                                                   write(chunkPrefix, null);
                                                   write(data, null);
                                                   write(EndOfChunk, null);
                                                   return false;
                                               }

                                               if (write(
                                                   chunkPrefix,
                                                   () =>
                                                   {
                                                       if (write(
                                                           data,
                                                           () =>
                                                           {
                                                               if (write(EndOfChunk, continuation))
                                                                   return;
                                                               continuation();
                                                           }))
                                                       {
                                                           return;
                                                       }
                                                       if (write(EndOfChunk, continuation))
                                                       {
                                                           return;
                                                       }
                                                       continuation();
                                                   }))
                                               {
                                                   return true;
                                               }
                                               if (write(
                                                   data,
                                                   () =>
                                                   {
                                                       if (write(EndOfChunk, continuation))
                                                           return;
                                                       continuation();
                                                   }))
                                               {
                                                   return true;
                                               }
                                               return write(EndOfChunk, continuation);
                                           },
                                           error,
                                           () =>
                                           {
                                               if (write(FinalChunk, end))
                                               {
                                                   return;
                                               }
                                               end();
                                           }));
                            }
                        },
                        fault);
                };
        }

        private static bool IsStatusWithNoNoEntityBody(string status)
        {
            return status.StartsWith("1")
                   || status.StartsWith("204")
                   || status.StartsWith("205")
                   || status.StartsWith("304");
        }
    }
}
