using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XamlAnimatedGif.Decoding;
using XamlAnimatedGif.Decompression;

namespace TestDecode
{
    class Program
    {
        static void Main()
        {
            const string path = @"D:\tmp\gif\monster.gif";
            DumpFrames(path);
            //MakeImage(path);
            //TestLzwDecompression(path);
        }

        private static void TestLzwDecompression(string path)
        {
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

        static void MakeImage(string path)
        {
            using (var fileStream = File.OpenRead(path))
            {
                var gif = GifDataStream.ReadGifDataStream(fileStream);
                var firstFrame = gif.Frames[0];
                var colorTable = firstFrame.LocalColorTable ?? gif.GlobalColorTable;
                var colors = colorTable.Select(gc => Color.FromRgb(gc.R, gc.G, gc.B)).ToArray();
                var palette = new BitmapPalette(colors);
                var desc = gif.Header.LogicalScreenDescriptor;
                var image = new WriteableBitmap(
                    desc.Width, desc.Height,
                    96, 96,
                    PixelFormats.Indexed8,
                    palette);

                fileStream.Seek(firstFrame.ImageData.CompressedDataStartOffset, SeekOrigin.Begin);
                var data = GifHelpers.ReadDataBlocks(fileStream, false);
                using (var ms = new MemoryStream(data))
                using (var lzwStream = new LzwDecompressStream(ms, firstFrame.ImageData.LzwMinimumCodeSize))
                using (var indexStream = new MemoryStream())
                {
                    lzwStream.CopyTo(indexStream);

                    var pixelData = indexStream.ToArray();
                    image.Lock();

                    var fd = firstFrame.Descriptor;
                    var rect = new Int32Rect(fd.Left, fd.Top, fd.Width, fd.Height);
                    image.WritePixels(rect, pixelData, fd.Width, 0);
                    image.AddDirtyRect(rect);
                    image.Unlock();

                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(image));
                    using (var fs = File.OpenWrite(path + ".png"))
                    {
                        encoder.Save(fs);
                    }
                }
            }
        }

        static void DumpFrames(string path)
        {
            using (var fileStream = File.OpenRead(path))
            {
                var gif = GifDataStream.ReadGifDataStream(fileStream);
                var desc = gif.Header.LogicalScreenDescriptor;
                var colors = gif.GlobalColorTable.Select(gc => Color.FromRgb(gc.R, gc.G, gc.B)).ToArray();
                //colors[0] = Colors.Transparent;
                //colors[desc.BackgroundColorIndex] = Colors.Transparent;
                var gce = gif.Frames[0].Extensions.OfType<GifGraphicControlExtension>().FirstOrDefault();
                if (gce != null && gce.HasTransparency)
                {
                    colors[gce.TransparencyIndex] = Colors.Transparent;
                }
                var palette = new BitmapPalette(colors);
                var image = new WriteableBitmap(
                    desc.Width, desc.Height,
                    96, 96,
                    PixelFormats.Indexed8,
                    palette);

                for (int i = 0; i < gif.Frames.Count; i++)
                {
                    var frame = gif.Frames[i];
                    fileStream.Seek(frame.ImageData.CompressedDataStartOffset, SeekOrigin.Begin);
                    var data = GifHelpers.ReadDataBlocks(fileStream, false);
                    using (var ms = new MemoryStream(data))
                    using (var lzwStream = new LzwDecompressStream(ms, frame.ImageData.LzwMinimumCodeSize))
                    using (var indexStream = new MemoryStream())
                    {
                        lzwStream.CopyTo(indexStream);

                        var pixelData = indexStream.ToArray();
                        image.Lock();
                        var fd = frame.Descriptor;
                        var rect = new Int32Rect(fd.Left, fd.Top, fd.Width, fd.Height);
                        image.WritePixels(rect, pixelData, fd.Width, 0);
                        image.AddDirtyRect(rect);
                        image.Unlock();

                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(image));
                        string outPath = string.Format("{0}.{1}.png", path, i);
                        using (var outStream = File.OpenWrite(outPath))
                        {
                            encoder.Save(outStream);
                        }
                    }
                }
            }
        }
    }
}
