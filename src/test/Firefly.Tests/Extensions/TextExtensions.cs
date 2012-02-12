using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace Firefly.Tests.Extensions
{
    public static class TextExtensions
    {
        public static void Send(this Socket socket, string text, Encoding encoding = null)
        {
            var bytes = (encoding ?? Encoding.Default).GetBytes(text);
            socket.Send(bytes);
        }

        public static ArraySegment<byte> ToArraySegment(this string text, Encoding encoding = null)
        {
            return new ArraySegment<byte>((encoding ?? Encoding.Default).GetBytes(text));
        }

        public static String ToString(this ArraySegment<byte> data, Encoding encoding = null)
        {
            return (encoding ?? Encoding.Default).GetString(data.Array, data.Offset, data.Count);
        }

        public static void Write(this Stream stream, string text, Encoding encoding = null)
        {
            var bytes = (encoding ?? Encoding.Default).GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }
    }


}