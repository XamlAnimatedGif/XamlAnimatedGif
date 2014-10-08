using System.IO;
using System.Threading.Tasks;

namespace XamlAnimatedGif.Extensions
{
    static class StreamExtensions
    {
        public static async Task ReadAllAsync(this Stream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int n = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead);
                if (n == 0)
                    throw new EndOfStreamException();
                totalRead += n;
            }
        }

        public static void ReadAll(this Stream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int n = stream.Read(buffer, offset + totalRead, count - totalRead);
                if (n == 0)
                    throw new EndOfStreamException();
                totalRead += n;
            }
        }

        public static async Task<int> ReadByteAsync(this Stream stream)
        {
            var buffer = new byte[1];
            int n = await stream.ReadAsync(buffer, 0, 1);
            if (n == 0)
                return -1;
            return buffer[0];
        }

        public static Stream AsBuffered(this Stream stream)
        {
#if WPF
            var bs = stream as BufferedStream;
            if (bs != null)
                return bs;
            return new BufferedStream(stream);
#elif WINRT
            // WinRT stream wrapper is already buffered
            return stream;
#endif
        }
    }
}
