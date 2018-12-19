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

namespace AvaloniaGif
{
    public class GifImage : Image, IRenderTimeCriticalVisual
    {
        GifImage()
        {
            // SourceUriProperty.Changed.Subscribe(SourceChanged);
            // SourceStreamProperty.Changed.Subscribe(SourceChanged);
            // IterationCountProperty.Changed.Subscribe(IterationCountChanged);
            // AutoStartProperty.Changed.Subscribe(AutoStartChanged);
            SourceProperty.Changed.Subscribe(SourceChanged);
        }

        public bool HasNewFrame { get; set; }
        private static readonly byte[] GIFMagicNumber = new byte[] { 47, 49, 46, 38 };
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
                stream = await new UriLoader().GetStreamFromUriAsync(sourceUri, this.DownloadProgress);
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
            
            if(!isGIFstream)
                base.SetValue(Image.SourceProperty, sourceUri);

        }

        public void ThreadSafeRender(DrawingContext context, Size logicalSize, double scaling)
        {

        }

        public static readonly DirectProperty<GifImage, Progress<int>> DownloadProgressProperty =
            AvaloniaProperty.RegisterDirect<GifImage, Progress<int>>(
                nameof(_DownloadProgress),
                o => o._DownloadProgress,
                (o, v) => o._DownloadProgress = v);

        private Progress<int> _DownloadProgress = new Progress<int>();
        private bool _streamCanDispose;

        public Progress<int> DownloadProgress
        {
            get { return _DownloadProgress; }
            set { SetAndRaise(DownloadProgressProperty, ref _DownloadProgress, value); }
        }

        public static new readonly StyledProperty<Uri> SourceProperty =
            AvaloniaProperty.Register<GifImage, Uri>(nameof(Source));

        public new Uri Source
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




        // public static readonly AttachedProperty<Uri> SourceUriProperty =
        //             AvaloniaProperty.RegisterAttached<GifImage, Image, Uri>("SourceUri");

        // public static Uri GetSourceUri(Image target)
        // {
        //     return target.GetValue(SourceUriProperty);
        // }

        // public static void SetSourceUri(Image target, Uri value)
        // {
        //     target.SetValue(SourceUriProperty, value);
        // }

        // public static readonly AttachedProperty<Stream> SourceStreamProperty =
        //             AvaloniaProperty.RegisterAttached<GifImage, Image, Stream>("SourceStream");

        // public static Stream GetSourceStream(Image target)
        // {
        //     return target.GetValue(SourceStreamProperty);
        // }

        // public static void SetSourceStream(Image target, Stream value)
        // {
        //     target.SetValue(SourceStreamProperty, value);
        // }

        // public static readonly AttachedProperty<IterationCount> IterationCountProperty =
        //             AvaloniaProperty.RegisterAttached<GifImage, Image, IterationCount>("IterationCount", IterationCount.Infinite);

        // public static IterationCount GetIterationCount(Image target)
        // {
        //     return target.GetValue(IterationCountProperty);
        // }

        // public static void SetIterationCount(Image target, IterationCount value)
        // {
        //     target.SetValue(IterationCountProperty, value);
        // }

        // public static readonly AttachedProperty<bool> AutoStartProperty =
        //             AvaloniaProperty.RegisterAttached<GifImage, Image, bool>("AutoStart", true);

        // public static bool GetAutoStart(Image target)
        // {
        //     return target.GetValue(AutoStartProperty);
        // }

        // public static void SetAutoStart(Image target, bool value)
        // {
        //     target.SetValue(AutoStartProperty, value);
        // }

        // public static readonly AttachedProperty<GifInstance> InstanceProperty =
        //             AvaloniaProperty.RegisterAttached<GifImage, Image, GifInstance>("Instance");

        // public static GifInstance GetInstance(Image target)
        // {
        //     return target.GetValue(InstanceProperty);
        // }

        // public static void SetInstance(Image target, GifInstance value)
        // {
        //     target.SetValue(InstanceProperty, value);
        // }
        // private static void AutoStartChanged(AvaloniaPropertyChangedEventArgs e)
        // {
        //     var image = e.Sender as Image;

        //     if (image == null)
        //         return;


        //     GetInstance(image)?.AutoStartChanged(e);
        // }

        // private static void IterationCountChanged(AvaloniaPropertyChangedEventArgs e)
        // {
        //     var image = e.Sender as Image;

        //     if (image == null)
        //         return;

        //     GetInstance(image)?.IterationCountChanged(e);
        // }

        // private static void SourceChanged(AvaloniaPropertyChangedEventArgs e)
        // {
        //     var image = e.Sender as Image;

        //     if (image == null)
        //         return;

        //     var instance = GetInstance(image);

        //     if (instance != null)
        //     {
        //         instance?.Dispose();
        //     }

        //     instance = new GifInstance();
        //     instance.Image = image;
        //     instance.SetSource(e.NewValue);
        //     SetInstance(image, instance);
        // }
    }
}