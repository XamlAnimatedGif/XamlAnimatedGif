﻿using System;
using System.IO;
using AvaloniaGif.Extensions;
using Avalonia;
using Avalonia.Media.Imaging;

using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Portable.Xaml.Markup;
using System.Threading;

namespace AvaloniaGif
{
    public partial class GifImage
    {
        static GifImage()
        {
            SourceUriProperty.Changed.Subscribe(SourceChanged);
            SourceStreamProperty.Changed.Subscribe(SourceChanged);
            RepeatCountProperty.Changed.Subscribe(RepeatCountChanged);
            AutoStartProperty.Changed.Subscribe(AutoStartChanged);
        }

        public static readonly AttachedProperty<Uri> SourceUriProperty =
                    AvaloniaProperty.RegisterAttached<GifImage, Image, Uri>("SourceUri");

        public static Uri GetSourceUri(Image target)
        {
            return target.GetValue(SourceUriProperty);
        }

        public static void SetSourceUri(Image target, Uri value)
        {
            target.SetValue(SourceUriProperty, value);
        }

        public static readonly AttachedProperty<Stream> SourceStreamProperty =
                    AvaloniaProperty.RegisterAttached<GifImage, Image, Stream>("SourceStream");

        public static Stream GetSourceStream(Image target)
        {
            return target.GetValue(SourceStreamProperty);
        }

        public static void SetSourceStream(Image target, Stream value)
        {
            target.SetValue(SourceStreamProperty, value);
        }

        public static readonly AttachedProperty<RepeatCount> RepeatCountProperty =
                    AvaloniaProperty.RegisterAttached<GifImage, Image, RepeatCount>("RepeatCount", RepeatCount.Loop);

        public static RepeatCount GetRepeatCount(Image target)
        {
            return target.GetValue(RepeatCountProperty);
        }

        public static void SetRepeatCount(Image target, RepeatCount value)
        {
            target.SetValue(RepeatCountProperty, value);
        }

        public static readonly AttachedProperty<bool> AutoStartProperty =
                    AvaloniaProperty.RegisterAttached<GifImage, Image, bool>("AutoStart", true);

        public static bool GetAutoStart(Image target)
        {
            return target.GetValue(AutoStartProperty);
        }

        public static void SetAutoStart(Image target, bool value)
        {
            target.SetValue(AutoStartProperty, value);
        }

        public static readonly AttachedProperty<GifInstance> InstanceProperty =
                    AvaloniaProperty.RegisterAttached<GifImage, Image, GifInstance>("Instance");

        public static GifInstance GetInstance(Image target)
        {
            return target.GetValue(InstanceProperty);
        }

        public static void SetInstance(Image target, GifInstance value)
        {
            target.SetValue(InstanceProperty, value);
        }
        private static void AutoStartChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var image = e.Sender as Image;

            if (image == null)
                return;


            GetInstance(image)?.AutoStartChanged(e);
        }

        private static void RepeatCountChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var image = e.Sender as Image;

            if (image == null)
                return;

            GetInstance(image)?.RepeatCountChanged(e);
        }

        private static void SourceChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var image = e.Sender as Image;

            if (image == null)
                return;

            var instance = GetInstance(image);

            if (instance != null)
            {
                instance?.Dispose();
            }

            instance = new GifInstance();
            instance.Image = image;
            instance.SetSource(e.NewValue);
            SetInstance(image, instance);
        }
    }
}