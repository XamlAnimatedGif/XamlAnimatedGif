using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XamlAnimatedGif.Decoding;
using XamlAnimatedGif.Decompression;

namespace TestDecode
{
    class Program
    {
        static void Main(string[] args)
        {
            string path = @"D:\tmp\gif\new.gif";
            using (var fileStream = File.OpenRead(path))
            {
                var gif = GifDataStream.ReadGifDataStream(fileStream);
                var firstFrame = gif.Frames[0];
                fileStream.Seek(firstFrame.ImageData.CompressedDataStartOffset, SeekOrigin.Begin);
                var data = GifHelpers.ReadDataBlocks(fileStream, false);
                File.WriteAllBytes(path + ".lzw", data);
                using (var ms = new MemoryStream(data))
                using (var lzwStream = new LzwDecompressStream(ms, firstFrame.ImageData.LzwMinimumCodeSize))
                using (var ms2 = new MemoryStream())
                {
                    lzwStream.CopyTo(ms2);
                    File.WriteAllBytes(path + ".ind", ms2.ToArray());
                }
            }
        }
    }
}
