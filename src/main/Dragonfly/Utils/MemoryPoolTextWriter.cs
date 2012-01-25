using System;
using System.IO;
using System.Text;

namespace Dragonfly.Utils
{
    public class MemoryPoolTextWriter : TextWriter
    {
        private readonly IMemoryPool _memory;

        private char[] _textArray;
        private int _textBegin;
        private int _textEnd;
        // ReSharper disable InconsistentNaming
        private const int _textLength = 128;
        // ReSharper restore InconsistentNaming

        private byte[] _dataArray;
        private int _dataEnd;

        private readonly Encoder _encoding;

        public ArraySegment<byte> Buffer
        {
            get { return new ArraySegment<byte>(_dataArray, 0, _dataEnd); }
        }

        public MemoryPoolTextWriter(IMemoryPool memory)
        {
            _memory = memory;
            _textArray = _memory.AllocChar(_textLength);
            _dataArray = _memory.Empty;
            _encoding = Encoding.Default.GetEncoder();
        }

        public override Encoding Encoding
        {
            get { return Encoding.Default; }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    if (_textArray != null)
                    {
                        _memory.FreeChar(_textArray);
                        _textArray = null;
                    }
                    if (_dataArray != null)
                    {
                        _memory.FreeByte(_dataArray);
                        _dataArray = null;
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private void Encode(bool flush)
        {
            int charsUsed;
            int bytesUsed;
            bool completed;

            if (flush)
            {
                var needed = _encoding.GetByteCount(
                    _textArray, _textBegin, _textEnd - _textBegin,
                    true);
                var available = _dataEnd - _dataArray.Length;
                if (needed > available)
                {
                    Grow(needed - available);
                }
            }

            _encoding.Convert(
                _textArray, _textBegin, _textEnd - _textBegin,
                _dataArray, _dataEnd, _dataArray.Length - _dataEnd,
                flush, out charsUsed, out bytesUsed, out completed);

            if (charsUsed == 0 && bytesUsed == 0 && _textEnd != _textBegin)
            {
                Grow(_dataArray.Length + Math.Max(_dataArray.Length, 128));

                _encoding.Convert(
                    _textArray, _textBegin, _textEnd - _textBegin,
                    _dataArray, _dataEnd, _dataArray.Length - _dataEnd,
                    flush, out charsUsed, out bytesUsed, out completed);
            }

            if (_textBegin + charsUsed == _textEnd)
            {
                _textBegin = _textEnd = 0;
            }
            else
            {
                _textBegin += charsUsed;
            }

            _dataEnd += bytesUsed;
        }

        private void Grow(int minimumNeeded)
        {
            var newLength = minimumNeeded;
            var newArray = _memory.AllocByte(newLength);
            Array.Copy(_dataArray, 0, newArray, 0, _dataEnd);
            _memory.FreeByte(_dataArray);
            _dataArray = newArray;
        }

        public override void Write(char value)
        {
            if (_textLength == _textEnd)
            {
                Encode(false);
                if (_textLength == _textEnd)
                {
                    throw new InvalidOperationException("Unexplainable failure to encode text");
                }
            }

            _textArray[_textEnd++] = value;
        }

        public override void Write(string value)
        {
            var sourceIndex = 0;
            var sourceLength = value.Length;
            while (sourceIndex < sourceLength)
            {
                if (_textLength == _textEnd)
                {
                    Encode(false);
                }

                var count = sourceLength - sourceIndex;
                if (count > _textLength - _textEnd)
                    count = _textLength - _textEnd;

                value.CopyTo(sourceIndex, _textArray, _textEnd, count);
                sourceIndex += count;
                _textEnd += count;
            }
        }

        public override void Flush()
        {
            while (_textBegin != _textEnd)
            {
                Encode(true);
            }
        }
    }
}
