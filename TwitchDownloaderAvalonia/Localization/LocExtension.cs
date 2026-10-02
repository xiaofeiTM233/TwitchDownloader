using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using TwitchDownloaderAvalonia.Translations;

namespace TwitchDownloaderAvalonia.Localization
{
    /// <summary>
    /// Avalonia equivalent of WPFLocalizeExtension's {lex:Loc} markup extension.
    /// Binds to LocalizationService.Culture with a key lookup converter so that
    /// all values refresh when the culture changes.
    /// </summary>
    public class LocExtension : MarkupExtension
    {
        public string Key { get; set; }

        public LocExtension() { }

        public LocExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
                return string.Empty;

            return new Binding
            {
                Path = "Culture",
                Source = LocalizationService.Instance,
                Mode = BindingMode.OneWay,
                Converter = LocKeyConverter.Instance,
                ConverterParameter = Key
            };
        }
    }

    public class LocKeyConverter : IValueConverter
    {
        public static LocKeyConverter Instance { get; } = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is not string key || string.IsNullOrEmpty(key))
                return string.Empty;

            var cultureInfo = value as CultureInfo ?? CultureInfo.CurrentUICulture;
            return Strings.ResourceManager.GetString(key, cultureInfo)
                ?? Strings.ResourceManager.GetString(key, CultureInfo.InvariantCulture)
                ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return BindingOperations.DoNothing;
        }
    }
}
