using System;
using System.IO;

namespace Firefly.Streams
{
    class DuplexStream : Stream
    {
        readonly InputStream _inputStream;
        readonly OutputStream _outputStream;

        public DuplexStream(InputStream inputStream, OutputStream outputStream)
        {
            _inputStream = inputStream;
            _outputStream = outputStream;
        }

        public override void Close()
        {
            _inputStream.Close();
            _outputStream.Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inputStream.Dispose();
                _outputStream.Dispose();
            }
        }

        public override void Flush()
        {
            _outputStream.Flush();
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return _inputStream.BeginRead(buffer, offset, count, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return _inputStream.EndRead(asyncResult);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return _outputStream.BeginWrite(buffer, offset, count, callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            _outputStream.EndWrite(asyncResult);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _inputStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _inputStream.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _inputStream.Read(buffer, offset, count);
        }

        public override int ReadByte()
        {
            return _inputStream.ReadByte();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _outputStream.Write(buffer, offset, count);
        }

        public override void WriteByte(byte value)
        {
            _outputStream.WriteByte(value);
        }

        public override bool CanRead
        {
            get
            {
                return _inputStream.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return _inputStream.CanSeek;
            }
        }

        public override bool CanTimeout
        {
            get
            {
                return _outputStream.CanTimeout || _inputStream.CanTimeout;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return _outputStream.CanWrite;
            }
        }

        public override long Length
        {
            get
            {
                return _inputStream.Length;
            }
        }

        public override long Position
        {
            get
            {
                return _inputStream.Position;
            }
            set
            {
                _inputStream.Position = value;
            }
        }

        public override int ReadTimeout
        {
            get
            {
                return _inputStream.ReadTimeout;
            }
            set
            {
                _inputStream.ReadTimeout = value;
            }
        }

        public override int WriteTimeout
        {
            get
            {
                return _outputStream.WriteTimeout;
            }
            set
            {
                _outputStream.WriteTimeout = value;
            }
        }
    }
}