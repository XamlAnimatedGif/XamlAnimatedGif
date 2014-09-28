using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace XamlAnimatedGif.Decompression
{
    class BitReader : IDisposable
    {
        private readonly Stream _stream;
        private readonly bool _leaveOpen;

        private readonly byte[] _buffer;
        private int _bitPositionInBuffer;
        private int _bufferLengthInBits;

        public BitReader(byte[] bytes)
            : this(new MemoryStream(bytes))
        {
        }

        public BitReader(Stream stream, bool leaveOpen = false)
        {
            _stream = stream;
            _leaveOpen = leaveOpen;
            _buffer = new byte[4096];
        }

        public BitArray ReadBits(int count)
        {
            var result = new BitArray(count);
            int n = 0;
            while (n < count)
            {
                // If the buffer is empty, fill it
                if (_bitPositionInBuffer >= _bufferLengthInBits)
                {
                    int len = _stream.Read(_buffer, 0, _buffer.Length);
                    if (len == 0)
                        throw new EndOfStreamException();
                    _bufferLengthInBits = len * 8;
                    _bitPositionInBuffer = 0;
                }

                ReadBitsFromBuffer(result, count, ref n);
            }
            return result;
        }

        public async Task<BitArray> ReadBitsAsync(int count, CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = new BitArray(count);
            int n = 0;
            while (n < count)
            {
                // If the buffer is empty, fill it
                if (_bitPositionInBuffer >= _bufferLengthInBits)
                {
                    int len = await _stream.ReadAsync(_buffer, 0, _buffer.Length, cancellationToken);
                    if (len == 0)
                        throw new EndOfStreamException();
                    _bufferLengthInBits = len * 8;
                    _bitPositionInBuffer = 0;
                }

                ReadBitsFromBuffer(result, count, ref n);
            }
            return result;
        }

        private void ReadBitsFromBuffer(BitArray result, int count, ref int n)
        {
            // As long as there are bits in the buffer, take them
            while (n < count && _bitPositionInBuffer < _bufferLengthInBits)
            {
                result[n++] = ReadBitFromBuffer(_bitPositionInBuffer++);
            }
        }

        private bool ReadBitFromBuffer(int bitPositionInBuffer)
        {
            int bytePosition = bitPositionInBuffer / 8;
            int bitIndex = 7 - bitPositionInBuffer % 8;
            byte b = _buffer[bytePosition];
            return ((b << bitIndex) & 0x80) != 0;
        }

        private bool _disposed;
        public void Dispose()
        {
            if (_leaveOpen || _disposed)
                return;

            lock (_stream)
            {
                // The stream might have been disposed while we were
                // waiting for the lock, so check again
                if (_disposed)
                    return;

                _stream.Dispose();
                _disposed = true;
            }
        }
    }
}
