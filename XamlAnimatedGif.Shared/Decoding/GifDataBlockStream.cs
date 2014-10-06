using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XamlAnimatedGif.Extensions;

namespace XamlAnimatedGif.Decoding
{
    class GifDataBlockStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly bool _leaveOpen;
        private readonly byte[] _currentBlock;
        private int _currentBlockLength;
        private int _currentPositionInBlock;
        private bool _endOfStream;

        public GifDataBlockStream(Stream baseStream, bool leaveOpen = false)
        {
            _baseStream = baseStream;
            _leaveOpen = leaveOpen;
            _currentBlock = new byte[256];
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
            while (read < count)
            {
                if (_currentPositionInBlock >= _currentBlockLength)
                {
                    int blockLength = _baseStream.ReadByte();
                    if (blockLength <= 0)
                    {
                        _endOfStream = true;
                        return read;
                    }

                    _baseStream.ReadAll(_currentBlock, 0, blockLength);
                    _currentBlockLength = blockLength;
                    _currentPositionInBlock = 0;
                }

                ReadBytesFromCurrentBlock(buffer, offset + read, count, ref read);
            }
            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateReadArgs(buffer, offset, count);

            if (_endOfStream)
                return 0;

            int read = 0;
            while (read < count)
            {
                if (_currentPositionInBlock >= _currentBlockLength)
                {
                    int blockLength = await _baseStream.ReadByteAsync();
                    if (blockLength <= 0)
                    {
                        _endOfStream = true;
                        return read;
                    }

                    await _baseStream.ReadAllAsync(_currentBlock, 0, blockLength);
                    _currentBlockLength = blockLength;
                    _currentPositionInBlock = 0;
                }

                ReadBytesFromCurrentBlock(buffer, offset + read, count, ref read);
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
            get { return false; }
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
            if (disposing && !_leaveOpen)
                _baseStream.Dispose();
        }

        private void ReadBytesFromCurrentBlock(byte[] buffer, int offset, int count, ref int read)
        {
            // As long as there are bytes in the buffer, take them
            int i = offset;
            while (read < count && _currentPositionInBlock < _currentBlockLength)
            {
                buffer[i++] = _currentBlock[_currentPositionInBlock++];
                read++;
            }
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
    }
}
