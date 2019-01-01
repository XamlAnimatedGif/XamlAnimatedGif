using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaGif.Extensions;

namespace AvaloniaGif.Decoding
{
    internal static class GifHelpers
    {
        public static string ReadString(Stream stream, int length)
        {
            byte[] bytes = new byte[length];
            stream.ReadAll(bytes, 0, length);
            return GetString(bytes);
        }

        public static void ConsumeDataBlocks(Stream sourceStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            CopyDataBlocksToStream(sourceStream, Stream.Null);
        }

        public static byte[] ReadDataBlocks(Stream stream, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var ms = new MemoryStream())
            {
                CopyDataBlocksToStream(stream, ms);
                return ms.ToArray();
            }
        }

        public static void CopyDataBlocksToStream(Stream sourceStream, Stream targetStream)
        {
            int len;
            // the length is on 1 byte, so each data sub-block can't be more than 255 bytes long
            byte[] buffer = new byte[255];
            while ((len = sourceStream.ReadByte()) > 0)
            {
                sourceStream.ReadAll(buffer, 0, len);
                targetStream.Write(buffer, 0, len);
            }
        }

        public static GifColor[] ReadColorTable(Stream stream, int size)
        {
            int length = 3 * size;
            byte[] bytes = new byte[length];
            stream.ReadAll(bytes, 0, length);
            GifColor[] colorTable = new GifColor[size];
            for (int i = 0; i < size; i++)
            {
                byte r = bytes[3 * i];
                byte g = bytes[3 * i + 1];
                byte b = bytes[3 * i + 2];
                colorTable[i] = new GifColor(r, g, b);
            }
            return colorTable;
        }

        public static bool IsNetscapeExtension(GifApplicationExtension ext)
        {
            return ext.ApplicationIdentifier == "NETSCAPE"
                && GetString(ext.AuthenticationCode) == "2.0";
        }

        public static ushort GetIterationCount(GifApplicationExtension ext)
        {
            if (ext.Data.Length >= 3)
            {
                return BitConverter.ToUInt16(ext.Data, 1);
            }
            return 1;
        }

        public static Exception UnknownBlockTypeException(int blockId)
        {
            return new UnknownBlockTypeException("Unknown block type: 0x" + blockId.ToString("x2"));
        }

        public static Exception UnknownExtensionTypeException(int extensionLabel)
        {
            return new UnknownExtensionTypeException("Unknown extension type: 0x" + extensionLabel.ToString("x2"));
        }

        public static Exception InvalidBlockSizeException(string blockName, int expectedBlockSize, int actualBlockSize)
        {
            return new InvalidBlockSizeException(
                $"Invalid block size for {blockName}. Expected {expectedBlockSize}, but was {actualBlockSize}");
        }

        public static Exception InvalidSignatureException(string signature)
        {
            return new InvalidSignatureException("Invalid file signature: " + signature);
        }

        public static Exception UnsupportedVersionException(string version)
        {
            return new UnsupportedGifVersionException("Unsupported version: " + version);
        }

        public static string GetString(byte[] bytes)
        {
            return GetString(bytes, 0, bytes.Length);
        }

        public static string GetString(byte[] bytes, int index, int count)
        {
            return Encoding.UTF8.GetString(bytes, index, count);
        }
    }
}
