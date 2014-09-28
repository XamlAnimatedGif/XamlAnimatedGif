using System;
using System.Collections;

namespace XamlAnimatedGif.Extensions
{
    static class BitArrayExtensions
    {
        public static short ToInt16(this BitArray bitArray)
        {
            short n = 0;
            for (int i = bitArray.Length - 1; i >= 0; i--)
            {
                n = (short) ((n << 1) + (bitArray[i] ? 1 : 0));
            }
            return n;
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
