// // Parts of this source file is derived from Chromium's Android GifPlayer
// // as seen here (https://github.com/chromium/chromium/blob/master/third_party/gif_player/src/jp/tomorrowkey/android/gifplayer)

// // Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
// // Copyright (C) 2015 The Gifplayer Authors. All Rights Reserved.

// // The rest of the source file is licensed under MIT License.
// // Copyright (C) 2018 Jumar A. Macato, All Rights Reserved.

// using System;
// using System.Buffers;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using System.Net.Http.Headers;
// using System.Runtime.CompilerServices;
// using System.Runtime.InteropServices;
// using System.Threading;
// using System.Text;
// using Avalonia;
// using Avalonia.Platform;
// using Avalonia.Media.Imaging;
// using static AvaloniaGif.Decoding.StreamExtensions;
// using System.Threading.Tasks;

// namespace AvaloniaGif.Decoding
// {
//     public class ColorTableCache
//     {
//         class CacheEntry
//         {
//             public TimeSpan remTime;
//             public bool disposed;
//         }

//         private static ColorTableCache inst;
//         public static ColorTableCache Shared => inst ?? (inst = new ColorTableCache());

//         Dictionary<ulong, ReadOnlyMemory<GifColor>> tables = new Dictionary<ulong, ReadOnlyMemory<GifColor>>();
//         Dictionary<ulong, CacheEntry> cacheTime = new Dictionary<ulong, CacheEntry>();
//         List<ulong> removeEntries = new List<ulong>();
//         public static TimeSpan EntryLifetime = TimeSpan.FromSeconds(15);
//         private XXHash64 hasher = new XXHash64();
//         private object dictLock = new object();
//         private Task _bgThread;
//         private volatile bool currentlyWriting;

//         private ColorTableCache()
//         {
//             _bgThread = Task.Factory.StartNew(MainLoop, new CancellationToken(false), TaskCreationOptions.LongRunning,
//                 TaskScheduler.Current);
//         }

//         private void MainLoop()
//         {
//             bool hasRe = false;
//             while (true)
//             {
//                 foreach (var cachePair in cacheTime)
//                 {
//                     if (!cachePair.Value.disposed) continue;

//                     cachePair.Value.remTime -= TimeSpan.FromSeconds(1);
//                     if (cachePair.Value.remTime < TimeSpan.Zero)
//                     {
//                         removeEntries.Add(cachePair.Key);
//                         hasRe = true;
//                     }
//                 }

//                 if (hasRe)
//                 {
//                     foreach (var entry in removeEntries)
//                     {
//                         tables.Remove(entry);
//                         cacheTime.Remove(entry);
//                     }

//                     removeEntries.Clear();
                    
//                     hasRe = false;
//                 }

//                 Thread.Sleep(1000);
//             }
//         }

//         public ulong AddTable(ReadOnlySpan<byte> table)
//         {
//             Span<byte> outHash = stackalloc byte[sizeof(ulong)];
//             hasher.TryComputeHash(table, outHash, out var bytes);
//             var tableHash = BitConverter.ToUInt64(outHash);

//             lock (dictLock)
//             {
//                 if (tables.ContainsKey(tableHash))
//                     return tableHash;

//                 int nColors = (table.Length / 3);
//                 int i = 0, j = 0;

//                 var newColorTable = new GifColor[nColors];

//                 while (i < nColors)
//                 {
//                     var r = table[j++];
//                     var g = table[j++];
//                     var b = table[j++];
//                     newColorTable[i++] = new GifColor(r, g, b);
//                 }

//                 currentlyWriting = true;

//                 tables.Add(tableHash, newColorTable.AsMemory());
//                 cacheTime.Add(tableHash, new CacheEntry()
//                 {
//                     remTime = EntryLifetime,
//                     disposed = false
//                 });
//                 currentlyWriting = false;
//             }

//             return tableHash;
//         }


//         public ReadOnlyMemory<GifColor> GetTable(ulong hash)
//         {
//             if (!tables.TryGetValue(hash, out var table))
//                 throw new KeyNotFoundException($"Color table hash didn't find the following hashkey: {hash}");

// //            if (cacheTime[hash].disposed)
// //                cacheTime[hash].remTime = EntryLifetime;

//             return table;
//         }

//         internal void DisposeTables(IEnumerable<ulong> colorTablesHashes)
//         {
//             foreach (var hash in colorTablesHashes)
//             {
//                 cacheTime[hash].disposed = true;
//             }
//         }
//     }

//     public class GifDecoder : IDisposable
//     {
//         private static readonly ReadOnlyMemory<byte> G87AMagic
//             = Encoding.ASCII.GetBytes("GIF87a").AsMemory();

//         private static readonly ReadOnlyMemory<byte> G89AMagic
//             = Encoding.ASCII.GetBytes("GIF89a").AsMemory();

//         private static readonly ReadOnlyMemory<byte> NetscapeMagic
//             = Encoding.ASCII.GetBytes("NETSCAPE2.0").AsMemory();

//         private static readonly TimeSpan FrameDelayThreshold = TimeSpan.FromMilliseconds(10);
//         private static readonly TimeSpan FrameDelayDefault = TimeSpan.FromMilliseconds(100);
//         private static readonly GifColor TransparentColor = new GifColor(0, 0, 0, 0);

//         private const int MaxTempBuf = 768;
//         private const int MaxStackSize = 4096;
//         private const int MaxBits = 4097;

//         private int _iterationCount, _width, _height, _gctSize, _bgIndex, _prevFrame;
//         private bool _gctUsed;
//         private GifColor _bgColor;
//         private readonly Stream _fileStream;
//         private GifHeader _gifHeader;

//         public GifHeader Header => _gifHeader;
//         public List<GifFrame> Frames = new List<GifFrame>();
//         private List<ulong> _colorTablesHashes = new List<ulong>();
//         private readonly GifColor[] _bBuf;
//         private readonly int _backBufferBytes;

//         // LZW decoder working arrays
//         private readonly short[] _prefixBuf;
//         private readonly byte[] _suffixBuf;
//         private readonly byte[] _pixelStack;
//         private readonly byte[] _indexBuf;
//         private readonly byte[] _prevFrameIndexBuf;

//         internal readonly Mutex _hasNewFrameLock;
//         internal volatile bool _hasNewFrame;
//         private ulong _globalColorTableIndex;

//         public GifDecoder(Stream fileStream)
//         {
//             _fileStream = fileStream;

//             ProcessHeaderData();
//             ProcessFrameData();

//             var pixelCount = _gifHeader.Height * _gifHeader.Width;

//             _bBuf = new GifColor[pixelCount];
//             _indexBuf = ArrayPool<byte>.Shared.Rent(pixelCount);
//             _prevFrameIndexBuf = ArrayPool<byte>.Shared.Rent(pixelCount);
//             _prefixBuf = ArrayPool<short>.Shared.Rent(MaxStackSize);
//             _suffixBuf = ArrayPool<byte>.Shared.Rent(MaxStackSize);
//             _pixelStack = ArrayPool<byte>.Shared.Rent(MaxStackSize + 1);

//             _backBufferBytes = pixelCount * Marshal.SizeOf(typeof(GifColor));
//             _hasNewFrameLock = new Mutex();

//             // for (var pi = 0; pi < pixelCount; pi++)
//             //     _bBuf[pi] = new GifColor(255, 102, 23);
//         }

//         public void Dispose()
//         {
//             ArrayPool<short>.Shared.Return(_prefixBuf);
//             ArrayPool<byte>.Shared.Return(_indexBuf);
//             ArrayPool<byte>.Shared.Return(_prevFrameIndexBuf);
//             ArrayPool<byte>.Shared.Return(_pixelStack);
//             ArrayPool<byte>.Shared.Return(_suffixBuf);
//             ColorTableCache.Shared.DisposeTables(_colorTablesHashes);
//             _fileStream?.Dispose();
//         }

//         [MethodImpl(MethodImplOptions.AggressiveInlining)]
//         private int PixCoord(int x, int y) => x + y * _gifHeader.Width;

//         [MethodImpl(MethodImplOptions.AggressiveInlining)]
//         private IEnumerable<int> NormalRows(int height)
//         {
//             return Enumerable.Range(0, height);
//         }

//         [MethodImpl(MethodImplOptions.AggressiveInlining)]
//         private IEnumerable<int> InterlacedRows(int height)
//         {
//             /*
//              * 4 passes:
//              * Pass 1: rows 0, 8, 16, 24...
//              * Pass 2: rows 4, 12, 20, 28...
//              * Pass 3: rows 2, 6, 10, 14...
//              * Pass 4: rows 1, 3, 5, 7...
//              * */
//             var passes = new[]
//             {
//                 new {Start = 0, Step = 8},
//                 new {Start = 4, Step = 8},
//                 new {Start = 2, Step = 4},
//                 new {Start = 1, Step = 2}
//             };
//             foreach (var pass in passes)
//             {
//                 var y = pass.Start;
//                 while (y < height)
//                 {
//                     yield return y;
//                     y += pass.Step;
//                 }
//             }
//         }

//         public void RenderFrame(int fIndex)
//         {
//             if (fIndex < 0 | fIndex >= Frames.Count)
//                 return;

//             _hasNewFrameLock.WaitOne();
//             Span<byte> tempBuf = stackalloc byte[4];
//             var curFrame = Frames[fIndex];

//             // if (fIndex < _prevFrame)
//             //     ClearBackBuf();
//             // else
//             //     DisposePreviousFrame(ref curFrame);

//             DecompressFrameToIndexBuffer(curFrame, _indexBuf.AsSpan(), tempBuf);
//             DrawFrame(curFrame, _indexBuf.AsMemory());


//             _prevFrame = fIndex;
//             _hasNewFrame = true;

//             _hasNewFrameLock.ReleaseMutex();
//         }


//         private void DrawFrame(GifFrame curFrame, Memory<byte> _frameIndexSpan)
//         {
//             var activeColorTable = curFrame._lctUsed ? curFrame._localColorTable : _globalColorTableIndex;
//             var coltable = ColorTableCache.Shared.GetTable(activeColorTable);
//             var rows = curFrame._interlaced ? InterlacedRows(curFrame._frameH) : NormalRows(curFrame._frameH);

//             foreach (var row in rows)
//             {
//                 // Get the starting point of the current row on frame's index stream.
//                 var indexOffset = row * curFrame._frameW;

//                 // Get the buffer window from the offset. 
//                 var indexSpan = _frameIndexSpan.Slice(indexOffset, curFrame._frameW).Span;

//                 // Get the target backbuffer offset from the frames coords.
//                 var targetOffset = PixCoord(curFrame._frameX, row + curFrame._frameY);

//                 for (var i = 0; i < curFrame._frameW; i++)
//                 {
//                     var indexColor = indexSpan[i];
//                     targetOffset++;

//                     if (curFrame.HasTransparency & indexColor == curFrame._transparentColorIndex)
//                         continue;

//                     if (targetOffset < 0 | targetOffset >= _bBuf.Length) return;

//                     _bBuf[targetOffset - 1] = coltable.Span[indexColor];
//                 }
//             }
//         }

//         private void DisposePreviousFrame(GifFrame curFrame)
//         {
//         }

//         private void DecompressFrameToIndexBuffer(GifFrame curFrame, Span<byte> indexSpan, Span<byte> tempBuf)
//         {
//             var str = _fileStream;

//             str.Position = curFrame._lzwStreamPos;
//             var totalPixels = curFrame._frameH * curFrame._frameW;

//             // Initialize GIF data stream decoder.
//             var dataSize = curFrame._lzwMinCodeSize;
//             var clear = 1 << dataSize;
//             var endOfInformation = clear + 1;
//             var available = clear + 2;
//             var oldCode = -1;
//             var codeSize = dataSize + 1;
//             var codeMask = (1 << codeSize) - 1;

//             for (var code = 0; code < clear; code++)
//             {
//                 _prefixBuf[code] = 0;
//                 _suffixBuf[code] = (byte) code;
//             }

//             // Decode GIF pixel stream.
//             int bits, first, top, pixelIndex;
//             var datum = bits = first = top = pixelIndex = 0;

//             while (pixelIndex < totalPixels)
//             {
//                 var blockSize = str.ReadByteS();

//                 if (blockSize == 0)
//                     break;

//                 var blockEnd = str.Position + blockSize;

//                 while (str.Position < blockEnd)
//                 {
//                     datum += str.ReadByteS() << bits;
//                     bits += 8;

//                     while (bits >= codeSize)
//                     {
//                         // Get the next code.
//                         var code = datum & codeMask;
//                         datum >>= codeSize;
//                         bits -= codeSize;

//                         // Interpret the code
//                         if (code == clear)
//                         {
//                             // Reset decoder.
//                             codeSize = dataSize + 1;
//                             codeMask = (1 << codeSize) - 1;
//                             available = clear + 2;
//                             oldCode = -1;
//                             continue;
//                         }

//                         // Check for explicit end-of-stream
//                         if (code == endOfInformation)
//                         {
//                             str.Position = blockEnd;
//                             return;
//                         }

//                         if (oldCode == -1)
//                         {
//                             indexSpan[pixelIndex++] = _suffixBuf[code];
//                             oldCode = code;
//                             first = code;
//                             continue;
//                         }

//                         var inCode = code;
//                         if (code >= available)
//                         {
//                             _pixelStack[top++] = (byte) first;
//                             code = oldCode;

//                             if (top == MaxBits)
//                                 throw new LzwDecompressionException();
//                         }

//                         while (code >= clear)
//                         {
//                             if (code >= MaxBits || code == _prefixBuf[code])
//                                 throw new LzwDecompressionException();

//                             _pixelStack[top++] = _suffixBuf[code];
//                             code = _prefixBuf[code];

//                             if (top == MaxBits)
//                                 throw new LzwDecompressionException();
//                         }

//                         first = _suffixBuf[code];
//                         _pixelStack[top++] = (byte) first;

//                         // Add new code to the dictionary
//                         if (available < MaxStackSize)
//                         {
//                             _prefixBuf[available] = (short) oldCode;
//                             _suffixBuf[available] = (byte) first;
//                             available++;

//                             if (((available & codeMask) == 0) && (available < MaxStackSize))
//                             {
//                                 codeSize++;
//                                 codeMask += available;
//                             }
//                         }

//                         oldCode = inCode;

//                         // Drain the pixel stack.
//                         do
//                         {
//                             indexSpan[pixelIndex++] = _pixelStack[--top];
//                         } while (top > 0);
//                     }
//                 }
//             }

//             while (pixelIndex < totalPixels)
//                 indexSpan[pixelIndex++] = 0; // clear missing pixels
//         }

//         /// <summary>
//         /// Directly copies the <see cref="GifColor"/> struct array to a <see cref="ILockedFramebuffer"/>.
//         /// </summary>
//         public void WriteBackBufToFb(ILockedFramebuffer lockBuf)
//         {
//             _hasNewFrameLock.WaitOne();
//             if (_hasNewFrame)
//                 unsafe
//                 {
//                     fixed (void* src = &_bBuf[0])
//                         Buffer.MemoryCopy(src, lockBuf.Address.ToPointer(), _backBufferBytes, _backBufferBytes);
//                     _hasNewFrame = false;
//                 }

//             _hasNewFrameLock.ReleaseMutex();
//         }

//         /// <summary>
//         /// Processes GIF Header.
//         /// </summary>
//         private void ProcessHeaderData()
//         {
//             var str = _fileStream;
//             var tmpB = ArrayPool<byte>.Shared.Rent(MaxTempBuf);
//             var tempBuf = tmpB.AsSpan();

//             var headerMagic = tempBuf.Slice(0, 6);

//             str.Read(headerMagic);

//             if (!(headerMagic.SequenceEqual(G87AMagic.Span) | headerMagic.SequenceEqual(G89AMagic.Span)))
//                 throw new InvalidGifStreamException("Unsupported GIF Version: " +
//                                                     Encoding.ASCII.GetString(headerMagic));

//             ProcessScreenDescriptor(tempBuf);

//             if (_gctUsed)
//             {
//                 _globalColorTableIndex = ProcessColorTable(ref str, tempBuf, _gctSize);
//             }
//             // else
//             // {
//             //     throw new InvalidOperationException("Empy global color table is not supported.");
//             // }

//             _gifHeader = new GifHeader()
//             {
//                 Width = _width,
//                 Height = _height,
//                 HasGlobalColorTable = _gctUsed,
//                 GlobalColorTable = _globalColorTableIndex,
//                 GlobalColorTableSize = _gctSize,
//                 BackgroundColorIndex = _bgIndex,
//                 HeaderSize = _fileStream.Position
//             };

//             ArrayPool<byte>.Shared.Return(tmpB);
//         }

//         public WriteableBitmap CreateBitmapForRender(Vector? dpi = null)
//         {
//             var defDpi = dpi ?? new Vector(96, 96);
//             var pxSize = new PixelSize(_gifHeader.Width, _gifHeader.Height);
//             return new WriteableBitmap(pxSize, defDpi, PixelFormat.Bgra8888);
//         }

//         /// <summary>
//         /// Parses colors from file stream to target color table.
//         /// </summary> 
//         private ulong ProcessColorTable(ref Stream stream, Span<byte> rentedBuf, int nColors)
//         {
//             var nBytes = 3 * nColors;
//             var rawBufSpan = rentedBuf.Slice(0, nBytes);
//             stream.Read(rawBufSpan);
//             var hash = ColorTableCache.Shared.AddTable(rawBufSpan);
//             _colorTablesHashes.Add(hash);
//             return hash;
//         }

//         /// <summary>
//         /// Parses screen and other GIF descriptors. 
//         /// </summary>
//         private void ProcessScreenDescriptor(Span<byte> tempBuf)
//         {
//             var str = _fileStream;

//             _width = str.ReadUShortS();
//             _height = str.ReadUShortS();

//             var packed = str.ReadByteS();

//             _gctUsed = (packed & 0x80) != 0;
//             _gctSize = 2 << (packed & 7);
//             _bgIndex = str.ReadByteS();

//             str.Skip(1);
//         }

//         /// <summary>
//         /// Parses all frame data for random-seeking.
//         /// </summary>
//         private void ProcessFrameData()
//         {
//             var str = _fileStream;
//             str.Position = _gifHeader.HeaderSize;

//             var tmpB = ArrayPool<byte>.Shared.Rent(MaxTempBuf);
//             var tempBuf = tmpB.AsSpan();
//             var terminate = false;
//             var curFrame = 0;

//             Frames.Add(new GifFrame());

//             do
//             {
//                 var blockType = (BlockTypes) str.ReadByteS();

//                 switch (blockType)
//                 {
//                     case BlockTypes.EMPTY:
//                         break;

//                     case BlockTypes.EXTENSION:
//                         ProcessExtensions(ref curFrame, tempBuf);
//                         break;

//                     case BlockTypes.IMAGE_DESCRIPTOR:
//                         ProcessImageDescriptor(ref curFrame, tempBuf);
//                         str.SkipBlocks();
//                         break;

//                     case BlockTypes.TRAILER:
//                         Frames.RemoveAt(Frames.Count - 1);
//                         terminate = true;
//                         break;

//                     default:
//                         str.SkipBlocks();
//                         break;
//                 }

//                 // Break the loop when the stream is not valid anymore.
//                 if (str.Position >= str.Length & terminate == false)
//                     throw new InvalidProgramException("Reach the end of the filestream without trailer block.");
//             } while (!terminate);

//             ArrayPool<byte>.Shared.Return(tmpB);
//         }

//         /// <summary>
//         /// Parses GIF Image Descriptor Block.
//         /// </summary>
//         private void ProcessImageDescriptor(ref int curFrame, Span<byte> tempBuf)
//         {
//             var str = _fileStream;
//             var currentFrame = Frames[curFrame];

//             // Parse frame dimensions.
//             currentFrame._frameX = str.ReadUShortS();
//             currentFrame._frameY = str.ReadUShortS();
//             currentFrame._frameW = str.ReadUShortS();
//             currentFrame._frameH = str.ReadUShortS();

//             // Unpack interlace and lct info.
//             var packed = str.ReadByteS();
//             currentFrame._interlaced = (packed & 0x40) != 0;
//             currentFrame._lctUsed = (packed & 0x80) != 0;
//             currentFrame._lctSize = (int) Math.Pow(2, (packed & 0x07) + 1);

//             if (currentFrame._lctUsed)
//             {
//                 currentFrame._localColorTable = ProcessColorTable(ref str, tempBuf, currentFrame._lctSize);
//             }

//             currentFrame._lzwMinCodeSize = str.ReadByteS();
//             currentFrame._lzwStreamPos = str.Position;

//             curFrame += 1;
//             Frames.Add(new GifFrame());
//         }

//         /// <summary>
//         /// Parses GIF Extension Blocks.
//         /// </summary>
//         private void ProcessExtensions(ref int curFrame, Span<byte> tempBuf)
//         {
//             var str = _fileStream;


//             var extType = (ExtensionType) str.ReadByteS();

//             switch (extType)
//             {
//                 case ExtensionType.GRAPHICS_CONTROL:

//                     str.ReadBlock(tempBuf);
//                     var currentFrame = Frames[curFrame];
//                     var packed = tempBuf[0];
//                     currentFrame._disposalMethod = (FrameDisposal) ((packed & 0x1c) >> 2);
//                     currentFrame.HasTransparency = (packed & 1) != 0;

//                     currentFrame._frameDelay =
//                         TimeSpan.FromMilliseconds(SpanToShort(tempBuf.Slice(1)) * 10);

//                     if (currentFrame._frameDelay <= FrameDelayThreshold)
//                         currentFrame._frameDelay = FrameDelayDefault;

//                     currentFrame._transparentColorIndex = tempBuf[3];
//                     break;

//                 case ExtensionType.APPLICATION:
//                     var blockLen = str.ReadBlock(tempBuf);
//                     var blockSpan = tempBuf.Slice(0, blockLen);
//                     var blockHeader = tempBuf.Slice(0, NetscapeMagic.Length);

//                     if (blockHeader.SequenceEqual(NetscapeMagic.Span))
//                     {
//                         var count = 1;

//                         while (count > 0)
//                             count = str.ReadBlock(tempBuf);

//                         _iterationCount = SpanToShort(tempBuf.Slice(1));
//                     }
//                     else
//                         str.SkipBlocks();

//                     break;

//                 default:
//                     str.SkipBlocks();
//                     break;
//             }
//         }
//     }
// }