XamlAnimatedGif
===============

[![Build status](https://ci.appveyor.com/api/projects/status/huf6heqkc2hvu7qd?svg=true)](https://ci.appveyor.com/project/thomaslevesque/xamlanimatedgif)

A library to display animated GIF images in XAML apps (WPF, Silverlight 5, Windows 8.1, Windows Phone 8.1, Universal Windows Platform).

A NuGet package is available here: [XamlAnimatedGif](http://www.nuget.org/packages/XamlAnimatedGif)

**Basic usage:**

```xml
<Image gif:AnimationBehavior.SourceUri="/Images/animated.gif" />
```

For more details, see the [documentation page](https://github.com/XamlAnimatedGif/XamlAnimatedGif/wiki/Documentation) in the wiki.

About this project
------------------

This is a reboot of my [WpfAnimatedGif](https://github.com/thomaslevesque/WpfAnimatedGif) project. I rewrote everything from scratch with a completely different approach, with the following goals in mind:

- Support for all recent XAML platforms, not just WPF

  The WpfAnimatedGif library relied heavily on WPF-specific imaging features, which made it pretty much impossible to adapt to other platforms. XamlAnimatedGif, on the other hand, implements its own GIF decoding (metadata parsing and LZW decompression), which works on any XAML platform, and has as little dependency as possible on platform-specific types. The library currently works on WPF (.NET 4.x, .NET Core 3.1), Silverlight 5, Windows 8.1, Windows Phone 8.1 and Universal Windows Platform (Windows 10).

- Resource efficiency

  The old library used an awful lot of memory, because of the way it worked internally (prepare all frames beforehand, keep them in memory and assign them in turn to the `Image` control). XamlAnimatedGif renders the frames just-in-time using a `WriteableBitmap`, so only one frame at a time is loaded in memory. The CPU usage is still small, unless the image is very large. Also, since the `Image.Source` property doesn't change during animation, it avoids triggering a layout pass every time.

- Simplicity

  One major issue of WpfAnimatedGif was that it accepted an `ImageSource` as its input. The idea was to make it more natural to use, but it also made the code much more complex. To access the frames, it had to obtain a `BitmapDecoder` from the source; depending on the type of the source, this means it had to reload the image from a URL or stream, or use the `Decoder` property directly if the source was a `BitmapFrame`; if the image was from a remote URI and wasn't completely downloaded yet, this case had to be handled as well. It had to handle many different scenarios, which made for very complex and inefficient code, and it still didn't work in all cases... XamlAnimatedGif is much more conservative in what it accepts (either a file or HTTP URI, a resource URI, or a stream), which makes it simpler, more maintainable, and more reliable.

Why didn't I just make a new version of WpfAnimatedGif?
-------------------------------------------------------

One reason is that the name obviously implied that it was for WPF; it would be weird to use a library named WpfAnimatedGif in a WinRT app.

The other, and more important reason, is that I couldn't make the old library evolve in the direction I wanted without breaking compatibility. XamlAnimatedGif is **not** compatible with WpfAnimatedGif, you can't just replace the library and recompile your app. Also, there might be some features of WpfAnimatedGif that won't be available in XamlAnimatedGif (at least, not immediately; for instance, jumping to a specific frame is going to be much harder to implement).


Supported platforms
-------------------

|Platform            | Supported | Could be supported |
|:-------------------|:---------:|:------------------:|
|WPF (.NET 4.5)      | :white_check_mark: |  |
|WPF (.NET 4.0)      |  :white_check_mark: |  |
|WPF (.NET 3.5 and earlier) |  | No |
|Windows Store apps (8.1) |  :white_check_mark: | |
|Windows Store apps (8.0) |  | Maybe |
|Windows Phone (8.1) |  :white_check_mark: |  |
|Windows Phone (SL 8.1) |  | Maybe |
|Windows Phone (8.0) |  | Maybe |
|Universal Windows Platform (Windows 10) | :white_check_mark: |  |
|Silverlight (5.0)   | :white_check_mark: |  |
|Xamarin.iOS         |  | No idea |
|Xamarin.Android     |  | No idea |

As you can see, there are still a few unsupported platforms. So far, I only focused on making it work, and didn't worry too much about platform support. I don't want to spend time supporting older platforms (Win 8.0, WP 8.0), and haven't looked into Xamarin yet for iOS and Android, but I'll be glad to accept contributions.

GIF Features
--------

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

How to build
------------

You will need Visual Studio 2015, because the code uses a few features from C# 6. You will also need a Windows Store developer license to be able to compile the WinRT targets.


How to contribute
-----------------

There are a few things that I would like to include in the library, but I don't have the time or will to do it myself, so I'd be glad to accept contributions:
- support for Windows 8.0 and Windows Phone 8.0
- support for iOS and Android via Xamarin (is it even possible? I haven't played with Xamarin yet, so I'm not sure). I don't even know if it would be useful; perhaps these platforms already have some support for animated GIFs. If not the implementation would probably be quite different, but the basic blocks (GiF decoding and decompression) could probably be reused.

Before you start working on something, please create an issue to discuss it and make sure we're on the same page regarding how it should be done.


Special thanks
--------------

I want to say a big "thank you" to Matthew Flickinger for his article [What's in a GIF](http://www.matthewflickinger.com/lab/whatsinagif/index.html). This article has been incredingly helpful to help me understand the structure of a GIF file, and more specifically the LZW decompression process.

Donate
------

XamlAnimatedGif is a personal open-source project. It is, and will remain, completely free of charge. That being said, if you want to reward me for the time I spent working on it, I'll  gladly accept donations.

[![Donate](https://www.paypalobjects.com/en_US/i/btn/btn_donate_SM.gif)](https://www.paypal.me/thomaslevesque)
