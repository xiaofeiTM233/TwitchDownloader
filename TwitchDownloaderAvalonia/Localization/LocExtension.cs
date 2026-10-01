using System;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using TwitchDownloaderAvalonia.Localization;

namespace TwitchDownloaderAvalonia.Localization
{
    /// <summary>
    /// Avalonia equivalent of WPFLocalizeExtension's {lex:Loc} markup extension.
    /// Binds to LocalizationService's indexer so that values refresh when the culture changes.
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
                Path = $"Item['{Key}']",
                Source = LocalizationService.Instance,
                Mode = BindingMode.OneWay,
                Priority = BindingPriority.Style
            };
        }
    }
}
