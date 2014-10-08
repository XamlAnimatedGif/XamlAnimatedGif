XamlAnimatedGif
===============

A library to display animated GIF images in XAML apps (WPF, Windows 8.1, Windows Phone 8.1).

Basic usage:

```xml
<Image gif:AnimationBehavior.SourceUri="/Images/animated.gif" />
```

About this project
------------------

This is a reboot of my [WpfAnimatedGif](https://github.com/thomaslevesque/WpfAnimatedGif) project. I rewrote everything from scratch with a completely different approach, with the following goals in mind:

- Support for all recent XAML platforms, not just WPF

  The WpfAnimatedGif library relied heavily on WPF-specific imaging features, which made it pretty much impossible to adapt to other platforms. XamlAnimatedGif, on the other hand, implements its own GIF decoding (metadata parsing and LZW decompression), which works on any XAML platform, and has as little dependency as possible on platform-specific types. The library currently works on WPF (.NET 4.5), Windows 8.1 and Windows Phone 8.1.

- Resource efficiency

  The old library used an awful lot of memory, because of the way it worked internally (prepare all frames beforehand, keep them in memory and assign them in turn to the `Image` control). XamlAnimatedGif renders the frames just-in-time using a `WriteableBitmap`, so only one frame at a time is loaded in memory. The CPU usage is still small, unless the image is very large. Also, since the `Image.Source` property doesn't change during animation, it avoids triggering a layout pass every time.

- Simplicity

  One major issue of WpfAnimatedGif was that it accepted an `ImageSource` as its input. The idea was to make it more natural to use, but it also made the code much more complex. To access the frames, it had to obtain a `BitmapDecoder` from the source; depending on the type of the source, this means it had to reload the image from a URL or stream, or use the `Decoder` property directly if the source was a `BitmapFrame`; if the image was from a remote URI and wasn't completely downloaded yet, this case had to be handled as well. It had to handle many different scenarios, which made for very complex and inefficient code, and it still didn't work in all cases... XamlAnimatedGif is much more conservative in what it accepts (either a URL to a local file or app resource, or a stream), which makes it simpler, more maintainable, and more reliable.

**This library is still a work in progress**. At this point, it has the following limitations:
- ~~transparency isn't handled~~ *(done)*
- ~~images where each frame has its own color table aren't supported~~ *(done)*
- ~~frame disposal methods other than the default aren't supported~~ *(done)*
- manual control of the animation is limited to play/stop/pause/resume (no previous/next/seek)
- ~~support for non-WPF XAML platform isn't implemented yet~~ *(added support for Win 8.1 and WP 8.1)*
- no support for Windows 8.0, Windows Phone 8.0 and Windows Phone Silverlight 8.1 *(might be easy to support)*
- no support for WPF 4.0 and earlier, because a large part of the code is asynchronous *(might be possible to support 4.0 using Microsoft.Bcl.Async, but 3.5 is definitely not going to be supported)*

Why didn't I just make a new version of WpfAnimatedGif?
-------------------------------------------------------

One reason is that th name obviously implied that it was for WPF; it would be weird to use a library named WpfAnimatedGif in a WinRT app.

The other, and more important reason, is that I couldn't make the old library evolve in the direction I wanted without breaking compatibility. XamlAnimatedGif is **not** compatible with WpfAnimatedGif, you can't just replace the library and recompile your app. Also, there might be some features of WpfAnimatedGif that won't be available in XamlAnimatedGif (at least, not immediately; for instance, jumping to a specific frame is going to be much harder to implement).
