using System;

namespace AvaloniaGif.Decompression
{
    class BitReader
    {
        private readonly byte[] _buffer;

        public BitReader(byte[] buffer)
        {
            _buffer = buffer;
        }

        private int _bytePosition = -1;
        private int _bitPosition;
        private int _currentValue = -1;
        public int ReadBits(int bitCount)
        {
            // The following code assumes it's running on a little-endian architecture.

            if (!BitConverter.IsLittleEndian)
                throw new InvalidProgramException("GIF BitReader does not support Big-Endian architectures.");

            if (_bytePosition == -1)
            {
                _bytePosition = 0;
                _bitPosition = 0;
                _currentValue = ReadInt32();
            }
            else if (bitCount > 32 - _bitPosition)
            {
                int n = _bitPosition >> 3;
                _bytePosition += n;
                _bitPosition &= 0x07;
                _currentValue = ReadInt32() >> _bitPosition;
            }
            int mask = (1 << bitCount) - 1;
            int value = _currentValue & mask;
            _currentValue >>= bitCount;
            _bitPosition += bitCount;
            return value;
        }

        private int ReadInt32()
        {
            var value = 0;
            for (var i = 0; i < 4; i++)
            {
                if (_bytePosition + i >= _buffer.Length)
                    break;
                value |= _buffer[_bytePosition + i] << (i << 3);
            }
            return value;
        }
    }
}
