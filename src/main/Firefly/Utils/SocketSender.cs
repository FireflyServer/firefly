using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;

namespace Firefly.Utils
{
    /// <summary>
    /// Operations performed for buffered socket output
    /// </summary>
    public interface ISocketSender
    {
        bool Write(ArraySegment<byte> buffer);
        bool Flush(Action drained);
    }

    public class SocketSender : ISocketSender, IDisposable
    {
        private readonly object _sync = new object();
        private readonly IFireflyService _service;
        private readonly ISocket _socket;

        private State _state = State.Immediate;
        private SocketError _socketError = SocketError.Success;

        private Action _drained = null;

        struct SegmentData
        {
            public ArraySegment<byte> Segment;
            public ArraySegment<byte> Data;
        }

        /// <summary>
        /// _tail.Segment is the most recent allocation used to hold buffered data. 
        /// _tail.Data spand the memory inside the _tail.Segment that has been buffered but has not started sending.
        /// </summary>
        private SegmentData _tail;

        /// <summary>
        /// _pushed contains the _tail that are displaced when _tail.Data has reached the end of _tail.Segment. 
        /// The same Segment will never be in both _pushed and _tail at the same time.
        /// </summary>
        private IList<SegmentData> _pushed = new List<SegmentData>();

        /// <summary>
        /// _sending contains the _pushed that were moved once an async send has been initiated.
        /// </summary>
        private IList<SegmentData> _sending = new List<SegmentData>();


        private ISocketEvent _socketEvent;
        private bool _disposed;

        enum State
        {
            Immediate,
            Buffering,
        }

        public SocketSender(IFireflyService service, ISocket socket)
        {
            _service = service;
            _socket = socket;
            _state = State.Immediate;
            _socketEvent = _service.Memory.AllocSocketEvent();
            //TODO: alloc an action field lazily. assign that field to this property only while an async operation is outstanding
            _socketEvent.Completed = SocketEventCompleted;
        }

        ~SocketSender()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_socketEvent != null)
                {
                    _socketEvent.Dispose();
                    _socketEvent = null;
                }
                _disposed = true;
            }
        }

        public bool Write(ArraySegment<byte> buffer)
        {
            lock (_sync)
            {
                return _state == State.Immediate ? DoWriteImmediate(buffer) : DoWriteBuffering(buffer);
            }
        }

        public bool Flush(Action drained)
        {
            lock (_sync)
            {
                return DoFlush(drained);
            }
        }

        void SocketEventCompleted()
        {
            lock (_sync)
            {
                Debug.Assert(_socketEvent.LastOperation == SocketAsyncOperation.Send);
                DoSendCompleted();
            }
        }


        bool DoWriteImmediate(ArraySegment<byte> buffer)
        {
            Debug.Assert(_state == State.Immediate);

            var remaining = buffer;
            while (_socketError == SocketError.Success && remaining.Count != 0)
            {
                // send a synchronous chunk
                SocketError errorCode;
                var bytesTransfered = _socket.Send(new[] { remaining }, SocketFlags.None, out errorCode);

                remaining = new ArraySegment<byte>(
                    remaining.Array,
                    remaining.Offset + bytesTransfered,
                    remaining.Count - bytesTransfered);

                if (errorCode == SocketError.WouldBlock)
                {
                    SetStateBuffering();
                    DoWriteBuffering(remaining);
                    var isAsync = SendStart();
                    if (!isAsync)
                    {
                        SendEnd();
                        SetStateImmediate();
                    }
                    return _state == State.Buffering;
                }
                if (errorCode != SocketError.Success)
                {
                    _socketError = errorCode;
                    return false;
                }
            }
            return _state == State.Buffering;
        }

        bool DoWriteBuffering(ArraySegment<byte> buffer)
        {
            Debug.Assert(_state == State.Buffering);

            // remaining local variable will shrink as data is copied
            var remaining = buffer;
            while (remaining.Count != 0)
            {
                // space currently available is the end of the tail segment minus the end of the tail data
                var tailAvailable = _tail.Segment.Offset + _tail.Segment.Count - _tail.Data.Offset - _tail.Data.Count;

                // when no tail is available a new buffer must be acquired
                if (tailAvailable == 0)
                {
                    // the the segment contains memory, push it to the backlog
                    if (_tail.Segment.Count != 0)
                    {
                        _pushed.Add(_tail);
                    }

                    // acquire a new segment and point the data cursor at it's origin
                    _tail.Segment = _service.Memory.AllocSegment(1024);
                    _tail.Data = new ArraySegment<byte>(_tail.Segment.Array, _tail.Segment.Offset, 0);

                    // the size available at this moment is always the new segment's length
                    tailAvailable = _tail.Segment.Count;
                }

                // copy the most number of bytes you can from the sender's remaining bytes to after the tail's data
                var length = Math.Min(remaining.Count, tailAvailable);
                Array.Copy(remaining.Array, remaining.Offset, _tail.Data.Array, _tail.Data.Offset + _tail.Data.Count, length);

                // sender's remaining bytes shrink
                remaining = new ArraySegment<byte>(remaining.Array, remaining.Offset + length, remaining.Count - length);

                // tail's data bytes grow
                _tail.Data = new ArraySegment<byte>(_tail.Data.Array, _tail.Data.Offset, _tail.Data.Count + length);
            }
            return true;
        }

        private void DoSendCompleted()
        {
            // take care of buffers from previous start
            SendEnd();

            // TODO: attempt non-blocking synchronous send? But don't attempt more than socket.outputbuffer at a time?

            // if there is no pending data to send then
            // switch back to immediate mode
            if (_pushed.Count == 0 && _tail.Data.Count == 0)
            {
                SetStateImmediate();
                return;
            }

            // initiate the next start
            if (!SendStart())
            {
                // next start was not async after all, so take care
                // of buffers again and switch back to immediate mode
                SendEnd();
                SetStateImmediate();
            }
        }

        private bool DoFlush(Action drained)
        {
            if (_state == State.Immediate)
            {
                return false;
            }
            if (_drained == null)
            {
                _drained = drained;
            }
            else
            {
                var prior = _drained;
                _drained =
                    () =>
                    {
                        try
                        {
                            drained();
                        }
                        catch
                        {
                        }
                        prior();
                    };
            }
            return true;
        }

        private void SetStateBuffering()
        {
            Debug.Assert(_state == State.Immediate);
            Debug.Assert(_drained == null);
            _state = State.Buffering;
        }

        private void SetStateImmediate()
        {
            Debug.Assert(_state == State.Buffering);
            _state = State.Immediate;

            if (_drained != null)
            {
                var drained = _drained;
                _drained = null;
                drained.Invoke();
            }
        }

        private bool SendStart()
        {
            Debug.Assert(_state == State.Buffering);
            Debug.Assert(_sending.Count == 0);

            if (_pushed.Count == 0)
            {
                // simple case - only the _tail.Data needs to be transmit
                _socketEvent.BufferList = null;
                _socketEvent.SetBuffer(_tail.Data.Array, _tail.Data.Offset, _tail.Data.Count);
            }
            else
            {
                // complex case - move _pushed segments to _sending
                var empty = _sending;
                _sending = _pushed;
                _pushed = empty;

                // make a buffer list of all _sending[].Data and _tail.Data
                var bufferList = new ArraySegment<byte>[_sending.Count + 1];
                for (var index = 0; index != _sending.Count; ++index)
                {
                    bufferList[index] = _sending[index].Data;
                }
                bufferList[_sending.Count] = _tail.Data;

                if (_socketEvent.Buffer != null)
                {
                    _socketEvent.SetBuffer(null, 0, 0);
                }
                _socketEvent.BufferList = bufferList;
            }

            // cursor is advanced past this point, further buffering will copy after this mark 
            _tail.Data = new ArraySegment<byte>(_tail.Data.Array, _tail.Data.Offset + _tail.Data.Count, 0);

            return _socket.SendAsync(_socketEvent);
        }

        private void SendEnd()
        {
            //TODO: _asyncEvent socketerror? 

            //TODO: must not assume BytesTransferred == entire _sending array + tail?
            //  instead - clear incrementally? 
            

            Debug.Assert(_state == State.Buffering);
            if (_sending.Count != 0)
            {
                // return _sending segments to pool. _sending becomes empty.
                foreach (var sending in _sending)
                {
                    _service.Memory.FreeSegment(sending.Segment);
                }
                _sending.Clear();
            }

            // relocate _tail.Data cursor to front of _tail.Segment if it
            // is empty, to reduce shifts to _pushed
            if (_tail.Data.Count == 0)
            {
                _tail.Data = new ArraySegment<byte>(_tail.Segment.Array, _tail.Segment.Offset, 0);
            }
        }



    }
}