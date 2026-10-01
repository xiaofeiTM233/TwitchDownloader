using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace TwitchDownloaderAvalonia.Converters
{
    /// <summary>
    /// Converts a value to a bool for IsVisible bindings: non-null becomes true.
    /// </summary>
    public class NullToBoolConverter : IValueConverter
    {
        public static readonly NullToBoolConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is not null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
