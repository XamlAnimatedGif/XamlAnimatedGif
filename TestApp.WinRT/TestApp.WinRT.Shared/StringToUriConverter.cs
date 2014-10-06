using System;
using Windows.UI.Xaml.Data;

namespace TestApp.WinRT
{
    public class StringToUriConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string s = value as string;
            if (s == null)
                return null;

            Uri uri;
            if (Uri.TryCreate(s, UriKind.Absolute, out uri))
                return uri;

            if (Uri.TryCreate(s, UriKind.Relative, out uri))
                return uri;

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            Uri uri = value as Uri;
            if (uri == null)
                return null;

            return uri.OriginalString;
        }
    }
}
