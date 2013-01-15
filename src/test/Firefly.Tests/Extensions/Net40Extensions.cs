using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Firefly.Tests.Extensions
{
    static class Net40Extensions
    {
        public static Task CopyToAsync(this Stream stream, Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            var copyToAsyncMethod = stream.GetType().GetMethod("CopyToAsync", BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Stream), typeof(int), typeof(CancellationToken) }, null);
            if (copyToAsyncMethod != null)
            {
                return (Task)copyToAsyncMethod.Invoke(stream, new object[] { destination, bufferSize, cancellationToken });
            }
            throw new NotImplementedException("Missing CopyToAsync method");
        }
        public static Task WriteAsync(this Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var copyToAsyncMethod = stream.GetType().GetMethod("WriteAsync",
                BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.Public,
                null,
                new Type[] { typeof(byte[]), typeof(int), typeof(int), typeof(CancellationToken) },
                null);
            if (copyToAsyncMethod != null)
            {
                return (Task)copyToAsyncMethod.Invoke(stream, new object[] { buffer, offset, count, cancellationToken });
            }
            throw new NotImplementedException("Missing WriteAsync method");
        }
    }
}
