using System;
#if WPF || SILVERLIGHT
using System.Windows.Media.Imaging;
#elif WINRT
using Windows.UI.Xaml.Media.Imaging;
#endif

namespace AvaloniaGif.Extensions
{
    static class WritableBitmapExtensions
    {
        public static IDisposable LockInScope(this WriteableBitmap bitmap)
        {
#if WPF
            return new WriteableBitmapLock(bitmap);
#else
            return null;
#endif
        }

#if WPF
        class WriteableBitmapLock : IDisposable
        {
            private readonly WriteableBitmap _bitmap;

            public WriteableBitmapLock(WriteableBitmap bitmap)
            {
                _bitmap = bitmap;
                _bitmap.Lock();
            }

            public void Dispose()
            {
                _bitmap.Unlock();
            }
        }
#endif
    }
}
