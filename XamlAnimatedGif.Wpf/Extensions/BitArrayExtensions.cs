using System;
using System.Collections;

namespace XamlAnimatedGif.Extensions
{
    static class BitArrayExtensions
    {
        public static short ToInt16(this BitArray bitArray)
        {
            var bytes = bitArray.ReadBytes(16);
            Array.Reverse(bytes); // We want big-endian, so reverse the bytes
            return BitConverter.ToInt16(bytes, 0);
        }

        private static byte[] ReadBytes(this BitArray bitArray, int bitLength)
        {
            if (bitLength % 8 != 0)
                throw new ArgumentOutOfRangeException();
            var bytes = new byte[bitLength / 8];
            int outputIndex = Math.Max(0, bitLength - bitArray.Length);
            for (int inputIndex = 0; inputIndex < bitArray.Length; inputIndex++)
            {
                if (bitArray[inputIndex])
                {
                    int byteIndex = outputIndex / 8;
                    int bitIndex = outputIndex % 8;
                    bytes[byteIndex] |= (byte)(0x80 >> bitIndex);
                }
                outputIndex++;
            }
            return bytes;
        }
    }
}
