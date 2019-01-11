#define TEST

using System;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Visuals.Media.Imaging;
using Avalonia.VisualTree;

namespace AvaloniaGif
{
    public class GifImage : Control, IRenderTimeCriticalVisual
    {
        private bool _streamCanDispose;
        private GifRenderer _gifRenderer;
        private Rect viewPort;
        private Size sourceSize;
        private Vector scale;
        private Size scaledSize;
        private Rect destRect;
        private Rect sourceRect;
        private BitmapInterpolationMode interpolationMode;
        private WriteableBitmap _bitmap;

        readonly CancellationTokenSource cts = new CancellationTokenSource();

        public bool HasNewFrame => true;

        private GifBackgroundWorker _bgWorker;

        private static readonly byte[] GIFMagicNumber = new byte[] { 0x47, 0x49, 0x46, 0x38 };

        static GifImage()
        {
            AffectsRender<GifImage>(SourceProperty, StretchProperty);
            AffectsMeasure<GifImage>(SourceProperty, StretchProperty);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_bitmap != null)
            {
                Size sourceSize = new Size(_bitmap.PixelSize.Width, _bitmap.PixelSize.Height);
                if (double.IsInfinity(availableSize.Width) || double.IsInfinity(availableSize.Height))
                {
                    return sourceSize;
                }
                else
                {
                    return Stretch.CalculateSize(availableSize, sourceSize);
                }
            }
            else
            {
                return new Size();
            }
        }

        public GifImage()
        {
            this.GetPropertyChangedObservable(SourceProperty).Subscribe(SourceChanged);
            this.GetPropertyChangedObservable(SourceProperty).Subscribe(SetRenderBounds);
            this.GetPropertyChangedObservable(BoundsProperty).Subscribe(SetRenderBounds);
            this.GetPropertyChangedObservable(StretchProperty).Subscribe(SetRenderBounds);
            this.GetPropertyChangedObservable(RenderOptions.BitmapInterpolationModeProperty).Subscribe(SetRenderBounds);
        }

        private void SourceChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (!(e.Sender is GifImage image))
                return;

            if (!(e.NewValue is Uri newSource))
                return;

            SetSource(newSource);
        }

        private async void SetSource(object newValue)
        {
            setSourceMutex.WaitOne();

            var sourceUri = newValue as Uri;
            var sourceStr = newValue as Stream;

            Stream stream;

            if (sourceUri != null)
            {
                _streamCanDispose = true;
                _bgWorker?.SendCommand(GifBackgroundWorker.Command.Stop);
                stream = await new UriLoader().GetStreamFromUriAsync(sourceUri, this.DownloadProgress, cts.Token);
            }
            else if (sourceStr != null)
            {
                stream = sourceStr;
            }
            else
            {
                throw new InvalidDataException("Missing valid URI or Stream.");
            }

            if (!stream.CanSeek) throw new InvalidDataException("Stream must be seekable.");

            Initialize(stream);

            setSourceMutex.ReleaseMutex();
        }

        readonly Mutex setSourceMutex = new Mutex();

        private void Initialize(Stream stream)
        {
#if TEST
            var tmp = new AvaloniaGif.NewDecoder.GifStream(stream);
            tmp.Start();
            stream.Position = 0;

#endif
            _gifRenderer = new GifRenderer(stream);
            _bgWorker = new GifBackgroundWorker(_gifRenderer, _gifRenderer.GifFrameTimes, cts.Token);
            _bgWorker.SendCommand(GifBackgroundWorker.Command.Start);
            _bitmap = _gifRenderer.CreateBitmapForRender();
        }

        public void ThreadSafeRender(DrawingContext context, Size logicalSize, double scaling)
        {
            setSourceMutex.WaitOne();

            var bgwState = _bgWorker?.GetState();

            if (bgwState == GifBackgroundWorker.State.Start | bgwState == GifBackgroundWorker.State.Running & _bitmap != null)
            {
                using (var lockbitmap = _bitmap.Lock())
                    _gifRenderer.TransferScratchToBitmap(lockbitmap);
            }

            if (_bitmap != null)
                context.DrawImage(_bitmap, 1, sourceRect, destRect, interpolationMode);

            setSourceMutex.ReleaseMutex();
        }

        /// <summary>
        /// Defines the <see cref="Stretch"/> property.
        /// </summary>
        public static readonly StyledProperty<Stretch> StretchProperty =
            AvaloniaProperty.Register<Image, Stretch>(nameof(Stretch), Stretch.Uniform);


        /// <summary>
        /// Gets or sets a value controlling how the image will be stretched.
        /// </summary>
        public Stretch Stretch
        {
            get { return GetValue(StretchProperty); }
            set { SetValue(StretchProperty, value); }
        }

        public static readonly DirectProperty<GifImage, Progress<double>> DownloadProgressProperty =
            AvaloniaProperty.RegisterDirect<GifImage, Progress<double>>(
                nameof(_DownloadProgress),
                o => o._DownloadProgress,
                (o, v) => o._DownloadProgress = v);

        private Progress<double> _DownloadProgress = new Progress<double>();

        public Progress<double> DownloadProgress
        {
            get { return _DownloadProgress; }
            set { SetAndRaise(DownloadProgressProperty, ref _DownloadProgress, value); }
        }

        public static readonly StyledProperty<Uri> SourceProperty =
            AvaloniaProperty.Register<GifImage, Uri>(nameof(Source));

        public Uri Source
        {
            get { return GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly StyledProperty<bool> AutoStartProperty =
            AvaloniaProperty.Register<GifImage, bool>(nameof(AutoStart), defaultValue: true);

        public bool AutoStart
        {
            get { return GetValue(AutoStartProperty); }
            set { SetValue(AutoStartProperty, value); }
        }

        public static readonly StyledProperty<IterationCount> IterationCountProperty =
            AvaloniaProperty.Register<GifImage, IterationCount>(nameof(IterationCount));

        public IterationCount IterationCount
        {
            get { return GetValue(IterationCountProperty); }
            set { SetValue(IterationCountProperty, value); }
        }

        public void SetRenderBounds(AvaloniaPropertyChangedEventArgs e)
        {
            var source = _bitmap;

            if (!(_gifRenderer != null & _bitmap != null)) return;

            viewPort = new Rect(Bounds.Size);
            sourceSize = new Size(source.PixelSize.Width, source.PixelSize.Height);
            scale = Stretch.CalculateScaling(Bounds.Size, sourceSize);
            scaledSize = sourceSize * scale;
            destRect = viewPort
                .CenterRect(new Rect(scaledSize))
                .Intersect(viewPort);
            sourceRect = new Rect(sourceSize)
                .CenterRect(new Rect(destRect.Size / scale));
            interpolationMode = RenderOptions.GetBitmapInterpolationMode(this);
        }
    }
}