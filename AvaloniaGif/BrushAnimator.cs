using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Animation;
using Avalonia.Media;
using AvaloniaGif.Decoding;

namespace AvaloniaGif
{
    public class BrushAnimator : Animator
    {
        private BrushAnimator(Stream sourceStream, Uri sourceUri, GifDataStream metadata, RepeatCount RepeatCount) : base(sourceStream, sourceUri, metadata, RepeatCount)
        {
            Brush = new ImageBrush {Source = Bitmap};
            RepeatCount = _RepeatCount;
        }

        protected override RepeatCount GetSpecifiedRepeatCount() => RepeatCount;

        protected override object ErrorSource => Brush;

        public ImageBrush Brush { get; }

        private RepeatCount _RepeatCount;
        public RepeatCount RepeatCount
        {
            get { return _RepeatCount; }
            set
            {
                _RepeatCount = value;
                OnRepeatCountChanged();
            }
        }

        public static Task<BrushAnimator> CreateAsync(Uri sourceUri, RepeatCount RepeatCount, IProgress<int> progress = null)
        {
            return CreateAsyncCore(
                sourceUri,
                progress,
                (stream, metadata) => new BrushAnimator(stream, sourceUri, metadata, RepeatCount));
        }

        public static Task<BrushAnimator> CreateAsync(Stream sourceStream, RepeatCount RepeatCount)
        {
            return CreateAsyncCore(
                sourceStream,
                metadata => new BrushAnimator(sourceStream, null, metadata, RepeatCount));
        }
    }
}