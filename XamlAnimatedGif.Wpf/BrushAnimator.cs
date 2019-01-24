using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Animation; 
using XamlAnimatedGif.Decoding;

namespace XamlAnimatedGif
{
    public class BrushAnimator : Animator
    {
        private BrushAnimator(Stream sourceStream, Uri sourceUri, GifDataStream metadata, RepeatBehavior repeatBehavior) : base(sourceStream, sourceUri, metadata, repeatBehavior)
        {
            Brush = new ImageBrush {ImageSource = Bitmap};
            RepeatBehavior = _repeatBehavior;
        }

        protected override RepeatBehavior GetSpecifiedRepeatBehavior() => RepeatBehavior;

        protected override object AnimationSource => Brush;

        public ImageBrush Brush { get; }

        private RepeatBehavior _repeatBehavior;
        public RepeatBehavior RepeatBehavior
        {
            get { return _repeatBehavior; }
            set
            {
                _repeatBehavior = value;
                OnRepeatBehaviorChanged();
            }
        }

        public static Task<BrushAnimator> CreateAsync(Uri sourceUri, RepeatBehavior repeatBehavior, IProgress<int> progress = null)
        {
            return CreateAsyncCore(
                sourceUri,
                progress,
                (stream, metadata) => new BrushAnimator(stream, sourceUri, metadata, repeatBehavior));
        }

        public static Task<BrushAnimator> CreateAsync(Stream sourceStream, RepeatBehavior repeatBehavior)
        {
            return CreateAsyncCore(
                sourceStream,
                metadata => new BrushAnimator(sourceStream, null, metadata, repeatBehavior));
        }
    }
}