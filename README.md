# XamlAnimatedGif

[![NuGet version](https://img.shields.io/nuget/v/XamlAnimatedGif.svg?logo=nuget)](https://www.nuget.org/packages/XamlAnimatedGif)
[![AppVeyor build](https://img.shields.io/appveyor/ci/thomaslevesque/xamlanimatedgif.svg?logo=appveyor&logoColor=cccccc)](https://ci.appveyor.com/project/thomaslevesque/xamlanimatedgif)

A library to display animated GIF images in WPF applications on .NET Framework 4.5, .NET Core 3.1 and .NET 5.0.

**Basic usage:**

```xml
<Image gif:AnimationBehavior.SourceUri="/Images/animated.gif" />
```

For more details, see the [documentation page](https://github.com/XamlAnimatedGif/XamlAnimatedGif/wiki/Documentation) in the wiki.

## About Silverlight 5, Windows 8.1, Windows Phone 8.1, UWP .NET 4.0 support

This project used to support many XAML platforms; that was actually one of its design goals.
However, most of the supported frameworks are now obsolete or out of support, and maintaining them was becoming painful. In version 2.0.0, I added support for .NET Core 3.1 and .NET 5.0, and dropped support for older platforms:
- Silverlight 5: has been dead for several years.
- .NET Framework 4.0: has been out of support for several years.
- Windows Phone 8.1: has been dead for several years
- Windows 8.1: obsolete
- UWP: this library has never explicitly supported UWP. The Windows 8.1 version happened to (mostly) work on UWP, but that was never intentional. UWP natively supports animated GIFs, with much better performance than this library. So, XamlAnimatedGif for UWP isn't very useful anyway, and I don't want to keep maintaining it.

If you need to support one of these platforms, pre-2.0.0 versions of the package are still available on NuGet, but I won't do any more work on them.

## How is this project different from WpfAnimatedGif?

Good question, especially now that it only supports WPF!

TL;DR: it's a complete rewrite of the older [WpfAnimatedGif](https://github.com/thomaslevesque/WpfAnimatedGif) project, which is more memory efficient, but puts more work on the CPU. It might be a bit slow for very large GIFs, but uses much less memory than WpfAnimatedGif.

For reference, I'll include the relevant sections of the previous README here (parts of it no longer apply, of course):

> ### About this project
> 
> This is a reboot of my [WpfAnimatedGif](https://github.com/thomaslevesque/WpfAnimatedGif) project. I rewrote everything from scratch with a completely different approach, with the following goals in mind:
>
> - Support for all recent XAML platforms, not just WPF
>
>  The WpfAnimatedGif library relied heavily on WPF-specific imaging features, which made it pretty much impossible to adapt to other platforms. XamlAnimatedGif, on the other hand, implements its own GIF decoding (metadata parsing and LZW decompression), which works on any XAML platform, and has as little dependency as possible on platform-specific types. The library currently works on WPF (.NET 4.x), Silverlight 5, Windows 8.1, Windows Phone 8.1 and Universal Windows Platform (Windows 10).
>
>- Resource efficiency
>
>  The old library used an awful lot of memory, because of the way it worked internally (prepare all frames beforehand, keep them in memory and assign them in turn to the `Image` control). XamlAnimatedGif renders the frames just-in-time using a `WriteableBitmap`, so only one frame at a time is loaded in memory. The CPU usage is still small, unless the image is very large. Also, since the `Image.Source` property doesn't change during animation, it avoids triggering a layout pass every time.
>
> - Simplicity
> 
>   One major issue of WpfAnimatedGif was that it accepted an `ImageSource` as its input. The idea was to make it more natural to use, but it also made the code much more complex. To access the frames, it had to obtain a `BitmapDecoder` from the source; depending on the type of the source, this means it had to reload the image from a URL or stream, or use the `Decoder` property directly if the source was a `BitmapFrame`; if the image was from a remote URI and wasn't completely downloaded yet, this case had to be handled as well. It had to handle many different scenarios, which made for very complex and inefficient code, and it still didn't work in all cases... XamlAnimatedGif is much more conservative in what it accepts (either a file or HTTP URI, a resource URI, or a stream), which makes it simpler, more maintainable, and more reliable.
> 
> ### Why didn't I just make a new version of WpfAnimatedGif?
> 
> One reason is that the name obviously implied that it was for WPF; it would be weird to use a library named WpfAnimatedGif in a WinRT app.
> 
> The other, and more important reason, is that I couldn't make the old library evolve in the direction I wanted without breaking compatibility. XamlAnimatedGif is **not** compatible with WpfAnimatedGif, you can't just replace the library and recompile your app. Also, there might be some features of WpfAnimatedGif that won't be available in XamlAnimatedGif (at least, not immediately; for instance, jumping to a specific frame is going to be much harder to implement).

## GIF Features

| Feature | Supported | Comments |
|:--------|:---------:|:---------|
|Transparency|:white_check_mark:||
|Frame local color table|:white_check_mark:||
|Interlaced images|:white_check_mark:||
|Various frame disposal methods|:white_check_mark:||
|Frame delay|:white_check_mark:||
|Automatic repeat count|:white_check_mark:|from the [Netscape Application Block](http://www.vurdalakov.net/misc/gif/netscape-looping-application-extension)|
|Override repeat count|:white_check_mark:|Specify repeat count manually|
|Manual animation control|Partially|Only pause/resume are supported; support for next/previous/seek might be added later|

## How to build

You will need Visual Studio 2019 v16.8 or higher and the .NET 5.0 SDK.

To build from the command line, run `build.cmd`.


## How to contribute

I'm always glad to accept contributions. However, before you start working on something (especially if it's a big change), please create an issue to discuss it and make sure we're on the same page regarding if and how it should be done.

## Special thanks

I want to say a big "thank you" to Matthew Flickinger for his article [What's in a GIF](http://www.matthewflickinger.com/lab/whatsinagif/index.html). This article has been incredingly helpful to help me understand the structure of a GIF file, and more specifically the LZW decompression process.
