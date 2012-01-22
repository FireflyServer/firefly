using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Dragonfly.Http;
using Dragonfly.Tests.Http;
using Dragonfly.Utils;
using Gate;
using Gate.Adapters.Nancy;
using Gate.Builder;
using Gate.Middleware;
using Gate.Owin;

namespace Sandbox
{
    class Program
    {
        static void Main(string[] args)
        {
            var nancyApp = StartupNancyApp();

            //var workingTitleApp = StartupWorkingTitleApp();

            for (; ; )
            {
                var input = Console.ReadLine();
                if (input == "exit")
                {
                    nancyApp.Dispose();
                    //workingTitleApp.Dispose();
                    return;
                }

                if (input == "1")
                {
                    var request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:8080");
                    try { request.GetResponse(); }
                    catch (WebException ex) { Console.WriteLine(ex.Message); }
                }
                else if (input == "2")
                {

                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                    socket.Connect("localhost", 8080);
                    var blocking = socket.Blocking;
                    socket.Blocking = false;
                    //var sr = socket.BeginReceive(new byte[0], 0, 0, SocketFlags.None, ar =>
                    //                                                                      {
                    //                                                                          socket.EndReceive(ar);
                    //                                                                      }, null);

                    //var optionOutValue = new byte[4];
                    //var ioControl = socket.IOControl(IOControlCode.NonBlockingIO, new byte[] {1, 0, 0, 0}, optionOutValue);
                    unsafe
                    {
                        var wsabuf = new WSABUF();
                        uint numberOfBytesRecvd;
                        var flags = SocketFlags.None;
                        var result = WSARecv(socket.Handle, ref wsabuf, 1, out numberOfBytesRecvd, ref flags, null, CallbackThunk1);

                        var lastError = result == -1 ? Marshal.GetLastWin32Error() : 0;

                        var overlapped = new Overlapped();
                        overlapped.AsyncResult = new ARes();
                        var nativeOverlapped = overlapped.Pack(Iocb, null);
                        Trace.WriteLine(string.Format("{0}", new IntPtr(nativeOverlapped)));

                        wsabuf = new WSABUF { buf = Marshal.AllocCoTaskMem(512), len = 512 };
                        var result2 = WSARecv2(socket.Handle, ref wsabuf, 1, out numberOfBytesRecvd, ref flags, nativeOverlapped, IntPtr.Zero);
                        var lastError2 = result2 == -1 ? Marshal.GetLastWin32Error() : 0;

                        var data = @"GET / HTTP/1.1
Host: localhost
Connection: close

".ToArraySegment();
                        SocketError err;
                        socket.BeginSend(data.Array, data.Offset, data.Count, SocketFlags.None, out err,
                            ar =>
                            {
                                socket.EndSend(ar);
                                socket.Shutdown(SocketShutdown.Send);
                            }, null);


                    }
                }
                else
                {
                    Console.WriteLine("Known input. Enter exit to exit.");
                }
            }
        }

        private static readonly unsafe IOCompletionCallback Iocb = MyIocb;

        private static unsafe void MyIocb(uint errorcode, uint numbytes, NativeOverlapped* poverlap)
        {
            var overlapped = Overlapped.Unpack(poverlap);
            var x = overlapped.AsyncResult;
        }

        public unsafe delegate void CompletionROUTINE(
            UInt32 dwError,
            UInt32 cbTransferred,
            NativeOverlapped* lpOverlapped,
            SocketFlags dwFlags);

        private static readonly unsafe CompletionROUTINE CallbackThunk = Callback;

        private static unsafe void Callback(uint dwerror, uint cbtransferred, NativeOverlapped* lpoverlapped, SocketFlags dwflags)
        {
            Trace.WriteLine(string.Format("{0}", new IntPtr(lpoverlapped)));
            var overlapped = Overlapped.Unpack(lpoverlapped);
            var x = overlapped.AsyncResult;
        }


        private static readonly unsafe CompletionROUTINE CallbackThunk1 = Callback1;

        private static unsafe void Callback1(uint dwerror, uint cbtransferred, NativeOverlapped* lpoverlapped, SocketFlags dwflags)
        {
            Trace.WriteLine(string.Format("1:{0}", new IntPtr(lpoverlapped)));
            var overlapped = Overlapped.Unpack(lpoverlapped);
        }


        public struct WSABUF
        {
            public UInt32 len;
            public IntPtr buf;
        }

        [DllImport("ws2_32.dll", SetLastError = true)]
        public extern static unsafe int WSARecv(
            IntPtr s,
            ref WSABUF lpBuffers,
            UInt32 dwBufferCount,
            out UInt32 lpNumberOfBytesRecvd,
            ref SocketFlags lpFlags,
            NativeOverlapped* lpOverlapped,
            CompletionROUTINE lpCompletionRoutine);

        [DllImport("ws2_32.dll", SetLastError = true, EntryPoint = "WSARecv")]
        public extern static unsafe int WSARecv2(
            IntPtr s,
            ref WSABUF lpBuffers,
            UInt32 dwBufferCount,
            out UInt32 lpNumberOfBytesRecvd,
            ref SocketFlags lpFlags,
            NativeOverlapped* lpOverlapped,
            IntPtr lpCompletionRoutine);

        //private static IDisposable StartupWorkingTitleApp()
        //{
        //    var server = new Server((req, resp) =>
        //                                {
        //                                    resp.Write("Hello, again, world");
        //                                    resp.End();
        //                                });
        //    server.Listen(8081, null);
        //    return server;
        //}

        private static IDisposable StartupNancyApp()
        {
            var builder = new AppBuilder();

            builder
                .Use(SetResponseHeader, "X-Server", "Dragonfly")
                .Use(ShowCalls)
                .UseWebSockets("/socketserver", OnConnection)
                .UseChunked()
                .RunNancy();

            var app = builder.Materialize<AppDelegate>();
            var server = new ServerFactory(new StdoutTrace()).Create(app, 8080);

            Console.WriteLine("Running on localhost:8080");
            return server;
        }

        static Action<int, ArraySegment<byte>> OnConnection(Action<int, ArraySegment<byte>> outgoing)
        {
            Console.WriteLine("Connected");
            outgoing(1, new ArraySegment<byte>(Encoding.Default.GetBytes("Good morning!")));
            return
                (opcode, data) =>
                {
                    Console.WriteLine("Incoming opcode:{0}", opcode);
                    switch (opcode)
                    {
                        case 1:
                            Console.WriteLine(Encoding.Default.GetString(data.Array, data.Offset, data.Count));
                            break;
                    }
                    outgoing(opcode, data);
                };

        }

        private static AppDelegate SetResponseHeader(AppDelegate app, string name, string value)
        {
            return
                (env, result, fault) =>
                app(
                    env,
                    (status, headers, body) =>
                    {
                        headers[name] = new[] { value };
                        result(status, headers, body);
                    },
                    fault);
        }

        private static AppDelegate ShowCalls(AppDelegate app)
        {
            return
                (env, result, fault) =>
                {
                    Console.WriteLine("==========");
                    foreach (var kv in env)
                    {
                        if (kv.Value is IDictionary<string, IEnumerable<string>>)
                        {
                            foreach (var kv2 in kv.Value as IDictionary<string, IEnumerable<string>>)
                            {
                                foreach (var value in kv2.Value)
                                {
                                    Console.WriteLine(kv.Key + "[" + kv2.Key + "] " + value);
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine(kv.Key + " " + kv.Value);
                        }
                    }
                    app(env,
                        (status, headers, body) =>
                        {
                            Console.WriteLine("----------");
                            Console.WriteLine(status);
                            foreach (var kv in headers)
                            {
                                foreach (var value in kv.Value)
                                {
                                    Console.WriteLine(kv.Key + " " + value);
                                }
                            }
                            result(status, headers, body);
                        },
                        fault);
                };
        }
    }

    internal class StdoutTrace : IServerTrace
    {
        public void Event(TraceEventType type, TraceMessage message)
        {
            Console.WriteLine("[{0} {1}]", type, message);
        }
    }

    internal class ARes : IAsyncResult
    {
        public bool IsCompleted
        {
            get { throw new NotImplementedException(); }
        }

        public WaitHandle AsyncWaitHandle
        {
            get { throw new NotImplementedException(); }
        }

        public object AsyncState
        {
            get { throw new NotImplementedException(); }
        }

        public bool CompletedSynchronously
        {
            get { throw new NotImplementedException(); }
        }
    }
}
