using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Profile.Server;

namespace Profile.SystemBehaviors
{
    class Program
    {
        static void Main(string[] args)
        {
        }

        private static void Test()
        {
            var socket1 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            socket1.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            socket1.Listen(-1);

            var socket2 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            socket2.Connect(socket1.LocalEndPoint);
            socket2.Blocking = false;

            var socket3 = socket1.Accept();
            socket3.Blocking = false;

            var data = Enumerable.Range(0, 30000).Aggregate("", (a, b) => a + "Hello World!").ToArraySegment();
            int totalSent = 0;
            for (;;)
            {
                SocketError errorCode;
                var sent = socket3.Send(new[] {data}, SocketFlags.Partial, out errorCode);
                Console.WriteLine("{0} {1} {2}", totalSent, sent, errorCode);
                totalSent += sent;
                if (errorCode != SocketError.Success)
                {
                    int x = 5;
                    break;
                }
                if (sent != data.Count)
                {
                    int x = 5;
                    break;
                }
            }
            int receiveTotal = 0;
            var y = "";

            var sendArgs = new SocketAsyncEventArgs();
            sendArgs.SetBuffer(data.Array, data.Offset, 1);
            sendArgs.Completed +=
                (a, b) =>
                {
                    totalSent += b.BytesTransferred;

                    SocketError errorCode;
                    var sent = socket3.Send(new[] {data}, SocketFlags.Partial, out errorCode);
                    totalSent += sent;
                    Console.WriteLine(totalSent + " " + sent + " " + DateTime.UtcNow + y);

                    int x = 5;
                    socket3.SendAsync(sendArgs);
                };
            var sendAsync = socket3.SendAsync(sendArgs);

            for (var x2 = 0; x2 != 100; ++x2)
            {
                Thread.Sleep(10);
            }
            y = "receiving";
            int xx = 4;

            while (receiveTotal < 12000)
            {
                for (var x2 = 0; x2 != 1; ++x2)
                {
                    Thread.Sleep(10);
                }
                var receiveCount = socket2.Receive(new byte[100]);
                receiveTotal += receiveCount;
            }
        }
    }
}
