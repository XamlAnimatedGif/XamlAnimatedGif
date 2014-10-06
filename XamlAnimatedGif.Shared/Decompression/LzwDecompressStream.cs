using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XamlAnimatedGif.Extensions;

namespace XamlAnimatedGif.Decompression
{
    class LzwDecompressStream : Stream
    {
        private const int MaxCodeLength = 12;
        private readonly BitReader _reader;
        private readonly int _minimumCodeLength;
        private int _codeLength;
        private short _prevCode;
        private List<Sequence> _codeTable;
        private byte[] _remainingBytes;
        private bool _endOfStream;

        public LzwDecompressStream(Stream compressedStream, int minimumCodeLength, bool leaveOpen = false)
        {
            _reader = new BitReader(compressedStream, leaveOpen);
            _minimumCodeLength = minimumCodeLength;
            InitCodeTable();
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateReadArgs(buffer, offset, count);

            if (_endOfStream)
                return 0;

            int read = 0;

            FlushRemainingBytes(buffer, offset, count, ref read);

            while (read < count)
            {
                var bits = _reader.ReadBits(_codeLength);
                short code = bits.ToInt16();
                if (!ProcessCode(code, buffer, offset, count, ref read))
                {
                    _endOfStream = true;
                    break;
                }
            }
            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateReadArgs(buffer, offset, count);

            if (_endOfStream)
                return 0;

            int read = 0;

            FlushRemainingBytes(buffer, offset, count, ref read);

            while (read < count)
            {
                var bits = await _reader.ReadBitsAsync(_codeLength, cancellationToken);
                short code = bits.ToInt16();
                if (!ProcessCode(code, buffer, offset, count, ref read))
                {
                    _endOfStream = true;
                    break;
                }
            }
            return read;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                _reader.Dispose();
        }

        private void InitCodeTable()
        {
            int initialEntries = 1 << _minimumCodeLength;
            _codeTable = Enumerable.Range(0, initialEntries)
                .Select(i => new Sequence(new[] { (byte)i }))
                .ToList();
            _codeTable.Add(Sequence.ClearCode);
            _codeTable.Add(Sequence.StopCode);
            _codeLength = _minimumCodeLength + 1;
            _prevCode = -1;
        }

        static int GetMinBitLength(int value)
        {
            int length = 0;
            do
            {
                length++;
                value = value >> 1;
            } while (value != 0);
            return length;
        }

        private byte[] CopySequenceToBuffer(byte[] sequence, byte[] buffer, int offset, int count, ref int read)
        {
            int bytesToRead = Math.Min(sequence.Length, count - read);
            Buffer.BlockCopy(sequence, 0, buffer, offset + read, bytesToRead);
            read += bytesToRead;
            byte[] remainingBytes = null;
            if (bytesToRead < sequence.Length)
            {
                int remainingBytesCount = sequence.Length - bytesToRead;
                remainingBytes = new byte[remainingBytesCount];
                Buffer.BlockCopy(sequence, bytesToRead, remainingBytes, 0, remainingBytesCount);
            }
            return remainingBytes;
        }

        private void AppendToCodeTable(Sequence sequence)
        {
            _codeTable.Add(sequence);
            if (_codeLength < GetMinBitLength(_codeTable.Count) && _codeLength < MaxCodeLength)
                _codeLength++;
        }

        private void FlushRemainingBytes(byte[] buffer, int offset, int count, ref int read)
        {
            // If we read too many bytes last time, copy them first;
            if (_remainingBytes != null)
                _remainingBytes = CopySequenceToBuffer(_remainingBytes, buffer, offset, count, ref read);
        }

        private void ValidateReadArgs(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset", "Offset can't be negative");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", "Count can't be negative");
            if (offset + count > buffer.Length)
                throw new ArgumentException("Buffer is to small to receive the requested data");
        }

        private bool ProcessCode(short code, byte[] buffer, int offset, int count, ref int read)
        {
            if (code < _codeTable.Count)
            {
                var sequence = _codeTable[code];
                if (sequence.IsStopCode)
                {
                    return false;
                }
                if (sequence.IsClearCode)
                {
                    InitCodeTable();
                    return true;
                }
                _remainingBytes = CopySequenceToBuffer(sequence.Bytes, buffer, offset, count, ref read);
                if (_prevCode >= 0)
                {
                    var prev = _codeTable[_prevCode];
                    var newSequence = prev.Append(sequence.Bytes[0]);
                    AppendToCodeTable(newSequence);
                }
            }
            else
            {
                var prev = _codeTable[_prevCode];
                var newSequence = prev.Append(prev.Bytes[0]);
                AppendToCodeTable(newSequence);
                _remainingBytes = CopySequenceToBuffer(newSequence.Bytes, buffer, offset, count, ref read);
            }
            _prevCode = code;
            return true;
        }

        struct Sequence
        {
            private readonly byte[] _bytes;
            private readonly bool _isClearCode;
            private readonly bool _isStopCode;

            public Sequence(byte[] bytes)
                : this()
            {
                if (bytes == null) throw new ArgumentNullException("bytes");
                _bytes = bytes;
            }

            private Sequence(bool isClearCode, bool isStopCode)
                : this()
            {
                _isClearCode = isClearCode;
                _isStopCode = isStopCode;
            }

            public byte[] Bytes
            {
                get { return _bytes; }
            }

            public bool IsClearCode
            {
                get { return _isClearCode; }
            }

            public bool IsStopCode
            {
                get { return _isStopCode; }
            }

            private static readonly Sequence _clearCode = new Sequence(true, false);
            public static Sequence ClearCode
            {
                get { return _clearCode; }
            }

            private static readonly Sequence _stopCode = new Sequence(false, true);
            public static Sequence StopCode
            {
                get { return _stopCode; }
            }

            public Sequence Append(byte b)
            {
                if (_bytes == null)
                    throw new InvalidOperationException("Can't append to clear code or stop code");
                var bytes = new byte[_bytes.Length + 1];
                _bytes.CopyTo(bytes, 0);
                bytes[_bytes.Length] = b;
                return new Sequence(bytes);
            }
        }
    }
}
