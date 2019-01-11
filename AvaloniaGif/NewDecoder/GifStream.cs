using System.IO;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Text;

namespace AvaloniaGif.NewDecoder
{
    public class GifStream
    {
        private int _width, _height, mHeaderSize, _gctSize, _bgIndex;
        private bool _globalColorTableUsed;
        private GifColor _bgColor;
        private Stream _fileStream;
        private GifHeader _gifHeader;
        private static Memory<GifColor> _globalColorTable
            = new Memory<GifColor>(new GifColor[256]);

        private static ReadOnlyMemory<byte> GIFMagic
                     = Encoding.ASCII.GetBytes("GIF").AsMemory();
        private static ReadOnlyMemory<byte> G87AMagic
                     = Encoding.ASCII.GetBytes("87a").AsMemory();
        private static ReadOnlyMemory<byte> G89AMagic
                     = Encoding.ASCII.GetBytes("89a").AsMemory();

        public GifHeader Header => _gifHeader;

        internal Stream FileStream => _fileStream;

        public GifStream(Stream fileStream)
        {
            _fileStream = fileStream;

            ReadHeader();
        }

        /// <summary>
        /// Processes GIF Header.
        /// </summary>
        private void ReadHeader()
        {

            Span<byte> val = stackalloc byte[1024];

            var triplet = val.Slice(0, 3);

            _fileStream.Read(triplet);

            if (!triplet.SequenceEqual(GIFMagic.Span))
                throw new InvalidFormatException("Invalid GIF Header");

            _fileStream.Read(triplet);

            if (!(triplet.SequenceEqual(G87AMagic.Span) | triplet.SequenceEqual(G89AMagic.Span)))
                throw new InvalidFormatException("Unsupported GIF Version: " +
                     Encoding.ASCII.GetString(triplet));

            ReadLogicalScreenDescriptor(val);

            if (_globalColorTableUsed)
            {
                ReadColorTable(ref _fileStream, val, _globalColorTable.Span, _gctSize);
                _bgColor = _globalColorTable.Span[_bgIndex];
            }

            _gifHeader = new GifHeader()
            {
                Width = _width,
                Height = _height,
                HasGlobalColorTable = _globalColorTableUsed,
                GlobalColorTable = _globalColorTable,
                GlobalColorTableSize = _gctSize,
                BackgroundColor = _bgColor,
                HeaderSize = _fileStream.Position
            };

        }

        /// <summary>
        /// Parses colors from file stream to target color table.
        /// </summary> 
        private static bool ReadColorTable(ref Stream stream, Span<byte> rentedBuf,
                                           Span<GifColor> targetColorTable, int ncolors)
        {
            var nbytes = 3 * ncolors;
            var rawBufSpan = rentedBuf.Slice(0, nbytes);
            var n = stream.Read(rawBufSpan);

            if (n < nbytes)
                return false;

            int i = 0;
            int j = 0;

            while (i < ncolors)
            {
                var r = rawBufSpan[j++];
                var g = rawBufSpan[j++];
                var b = rawBufSpan[j++];
                targetColorTable[i++] = new GifColor(r, g, b);
            }

            return true;
        }

        /// <summary>
        /// Parses screen and other GIF descriptors. 
        /// </summary>
        private void ReadLogicalScreenDescriptor(Span<byte> tempBuf)
        {
            _width = _fileStream.ReadUShortS(tempBuf);
            _height = _fileStream.ReadUShortS(tempBuf);

            var packed = _fileStream.ReadByteS(tempBuf);

            _globalColorTableUsed = (packed & 0x80) != 0; // 1   : global color table flag
                                                          // 2-4 : color resolution - ignore
                                                          // 5   : gct sort flag - ignore

            _gctSize = 2 << (packed & 7);                 // 6-8 : gct size
            _bgIndex = _fileStream.ReadByteS(tempBuf);

            _fileStream.Skip(1);                          // pixel aspect ratio - ignore
        }
    }
}