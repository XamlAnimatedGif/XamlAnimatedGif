using System;

namespace XamlAnimatedGif.Decoding
{
    public class GifFrame
    {
        public bool HasTransparency, IsInterlaced, IsLocalColorTableUsed;
        public byte TransparentColorIndex;
        public int LZWMinCodeSize, LocalColorTableSize;
        public long LZWStreamPosition;
        public TimeSpan FrameDelay;
        public FrameDisposal FrameDisposalMethod;
        public ulong LocalColorTableCacheID;
        public bool ShouldBackup;
        public GifRect Dimensions;
    }
}