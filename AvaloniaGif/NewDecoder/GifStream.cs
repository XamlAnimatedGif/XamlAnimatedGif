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
        private static Memory<GifColor> _globalColorTable = new Memory<GifColor>(new GifColor[256]);

        public GifHeader Header => _gifHeader;

        public void Start()
        {
            ReadHeader();
        }

        public GifStream(Stream fileStream)
        {
            _fileStream = fileStream;
        }

        /// <summary>
        /// Processes GIF Header.
        /// </summary>
        private void ReadHeader()
        {
            var GIFMagic = Encoding.ASCII.GetBytes("GIF").AsSpan();
            var G87AMagic = Encoding.ASCII.GetBytes("87a").AsSpan();
            var G89AMagic = Encoding.ASCII.GetBytes("89a").AsSpan();

            var rentedBuf = ArrayPool<byte>.Shared.Rent(1024);
            
            var val = new Span<byte>(rentedBuf, 0, 3);

            _fileStream.Read(val);

            if (!val.SequenceEqual(GIFMagic))
                throw new InvalidFormatException("Invalid GIF Header");

            _fileStream.Read(val);

            if (!(val.SequenceEqual(G87AMagic) | val.SequenceEqual(G89AMagic)))
                throw new InvalidFormatException("Unsupported GIF Version: " + Encoding.ASCII.GetString(val));

            ReadLogicalScreenDescriptor(ref rentedBuf);

            if (_globalColorTableUsed)
            {
                ReadColorTable(ref _fileStream, ref rentedBuf, _globalColorTable.Span, _gctSize);
                _bgColor = _globalColorTable.Span[_bgIndex];
            }

            _gifHeader = new GifHeader()
            {
                Width = _width,
                Height = _height,
                HasGlobalColorTable = _globalColorTableUsed,
                GlobalColorTable = _globalColorTable,
                GlobalColorTableSize = _gctSize,
                BackgroundColor = _bgColor
            };


            ArrayPool<byte>.Shared.Return(rentedBuf);
        }

        /// <summary>
        /// Parses colors from file stream to target color table.
        /// </summary> 
        private static bool ReadColorTable(ref Stream stream, ref byte[] rentedBuf,
                                           Span<GifColor> targetColorTable, int ncolors)
        {
            var nbytes = 3 * ncolors;
            var rawBufSpan = rentedBuf.AsSpan().Slice(0, nbytes);
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
        private void ReadLogicalScreenDescriptor(ref byte[] tempBuf)
        {
            _width = _fileStream.ReadUInt16A(ref tempBuf);
            _height = _fileStream.ReadUInt16A(ref tempBuf);

            var packed = _fileStream.ReadByteA(ref tempBuf);

            _globalColorTableUsed = (packed & 0x80) != 0; // 1   : global color table flag
                                                          // 2-4 : color resolution - ignore
                                                          // 5   : gct sort flag - ignore

            _gctSize = 2 << (packed & 7);                 // 6-8 : gct size
            _bgIndex = _fileStream.ReadByteA(ref tempBuf);

            _fileStream.Skip(1);                          // pixel aspect ratio - ignore
        }
    }
}