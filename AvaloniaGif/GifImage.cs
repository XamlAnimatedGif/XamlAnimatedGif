using System;
using System.IO;
using AvaloniaGif.Extensions;
using Avalonia;
using Avalonia.Media.Imaging;

using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Portable.Xaml.Markup;
using System.Threading;
using Avalonia.VisualTree;
using Avalonia.Media;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AvaloniaGif.Decoding;
using System.Diagnostics;
using Avalonia.Visuals.Media.Imaging;

namespace AvaloniaGif
{
    public class GifImage : Control, IRenderTimeCriticalVisual
    {

        private bool _streamCanDispose;
        private GifDataStream _gifDataStream;
        private GifRenderer _gifRenderer;
        private Rect viewPort;
        private Size sourceSize;
        private Vector scale;
        private Size scaledSize;
        private Rect destRect;
        private Rect sourceRect;
        private BitmapInterpolationMode interpolationMode;
        private static readonly Stopwatch _st = Stopwatch.StartNew();

        bool _isRunning = false;
        object _playbackLock = new object();
        TimeSpan prevTime;
        bool showFirstFrame = false;
        int CurrentFrame = 0, FrameCount = 0;
        CancellationTokenSource cts = new CancellationTokenSource();
        static GifImage()
        {
            AffectsRender<GifImage>(SourceProperty, StretchProperty);
            AffectsMeasure<GifImage>(SourceProperty, StretchProperty);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var source = _gifRenderer?._bitmap;

            if (source != null)
            {
                Size sourceSize = new Size(source.PixelSize.Width, source.PixelSize.Height);
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
            this.DetachedFromVisualTree += VisualDetached;
            this.GetPropertyChangedObservable(SourceProperty).Subscribe(SourceChanged);
            this.GetPropertyChangedObservable(SourceProperty).Subscribe(SetRenderBounds);
            this.GetPropertyChangedObservable(BoundsProperty).Subscribe(SetRenderBounds);
            this.GetPropertyChangedObservable(StretchProperty).Subscribe(SetRenderBounds);
            this.GetPropertyChangedObservable(RenderOptions.BitmapInterpolationModeProperty).Subscribe(SetRenderBounds);

        }

        private void VisualDetached(object sender, VisualTreeAttachmentEventArgs e)
        {

        }

        public bool HasNewFrame { get; set; }
        private static readonly byte[] GIFMagicNumber = new byte[] { 0x47, 0x49, 0x46, 0x38 };
        private void SourceChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (!(e.Sender is GifImage image))
                return;

            if (!(e.NewValue is Uri newSource))
                return;

            SetSource(newSource);

        }

        public async void SetSource(object newValue)
        {
            var sourceUri = newValue as Uri;
            var sourceStr = newValue as Stream;

            Stream stream;

            if (sourceUri != null)
            {
                _streamCanDispose = true;
                
                lock (_playbackLock)
                {
                    if (cts != null) cts?.Cancel();
                    _isRunning = false;
                    cts = new CancellationTokenSource();
                }

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

            var streamMagicNum = new byte[4];
            await stream.ReadAsync(streamMagicNum, 0, streamMagicNum.Length);
            stream.Position = 0;

            var isGIFstream = Enumerable.SequenceEqual(streamMagicNum, GIFMagicNumber);

            if (isGIFstream)
            {



                await Initialize(stream);
            }

            return;

        }

        private async Task Initialize(Stream stream)
        {


            _gifRenderer?.Dispose();
            _gifDataStream = await GifDataStream.ReadAsync(stream);
            _gifRenderer = new GifRenderer(stream, _gifDataStream);

            FrameCount = _gifDataStream.Frames.Count;
            CurrentFrame = 0;

            lock (_playbackLock)
                _isRunning = true;
            showFirstFrame = true;
            //base.Source = _gifRenderer.GifBitmap;
        }

        public void ThreadSafeRender(DrawingContext context, Size logicalSize, double scaling)
        {
            lock (_playbackLock)
                if (_isRunning)
                {
                    try
                    {
                        var t1 = _st.Elapsed;
                        var delta = t1 - prevTime;
                        if (showFirstFrame)
                        {
                            _gifRenderer.RenderFrameAsync(0, cts.Token).Wait();
                            showFirstFrame = false;
                        }
                        if (delta >= _gifRenderer.GifFrameTimes[CurrentFrame])
                        {
                            prevTime = t1;
                            _gifRenderer.RenderFrameAsync(CurrentFrame, cts.Token).Wait();
                            CurrentFrame = (CurrentFrame + 1) % FrameCount;
                            HasNewFrame = true;
                        }
                    }
                    catch (Exception e)
                    {
                        HasNewFrame = false;
                    }

                    context.DrawImage(_gifRenderer?._bitmap, 1, sourceRect, destRect, interpolationMode);
                }
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
            if (_gifRenderer != null)
            {
                var source = _gifRenderer._bitmap;
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
}