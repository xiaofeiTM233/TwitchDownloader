using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Avalonia.Styling;
using TwitchDownloaderAvalonia.Models;
using TwitchDownloaderAvalonia.Properties;

namespace TwitchDownloaderAvalonia.Services
{
    public class ThemeService
    {
        private readonly App _app;

        public ThemeService(App app)
        {
            _app = app;

            if (!Directory.Exists("Themes"))
            {
                try
                {
                    Directory.CreateDirectory("Themes");
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            if (!DefaultThemeService.WriteIncludedThemes())
            {
                MessageBoxService.ShowInfo(Translations.Strings.ThemesFailedToWrite, Translations.Strings.ThemesFailedToWrite);
            }

            // If the current theme is not system and the old theme file is not found
            if (!Settings.Default.GuiTheme.Equals("System", StringComparison.OrdinalIgnoreCase) && !File.Exists(Path.Combine("Themes", $"{Settings.Default.GuiTheme}.xaml")))
            {
                MessageBoxService.ShowInfo(
                    string.Format(Translations.Strings.ThemeNotFoundMessage, $"{Settings.Default.GuiTheme}.xaml"),
                    Translations.Strings.ThemeNotFound);

                Settings.Default.GuiTheme = "System";
                Settings.Default.Save();
            }

            TrySubscribeToPlatformThemeChanges();
        }

        private void TrySubscribeToPlatformThemeChanges()
        {
            try
            {
                var platformSettings = Application.Current?.PlatformSettings;
                if (platformSettings is not null)
                {
                    platformSettings.ColorValuesChanged += (_, _) =>
                    {
                        if (Settings.Default.GuiTheme.Equals("System", StringComparison.OrdinalIgnoreCase))
                        {
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => ChangeAppTheme(true));
                        }
                    };
                }
            }
            catch
            {
                // Platform theme watching is best effort
            }
        }

        public void ChangeAppTheme(bool forceRepaint = false)
        {
            var newTheme = Settings.Default.GuiTheme;
            if (newTheme.Equals("System", StringComparison.OrdinalIgnoreCase))
            {
                newTheme = GetSystemTheme();
            }

            ChangeThemePath(newTheme);
            ChangeFluentThemeVariant(newTheme);
        }

        private static string GetSystemTheme()
        {
            var platformSettings = Application.Current?.PlatformSettings;
            if (platformSettings is not null)
            {
                return platformSettings.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark
                    ? "Dark"
                    : "Light";
            }

            return Application.Current?.RequestedThemeVariant == ThemeVariant.Dark ? "Dark" : "Light";
        }

        private void ChangeFluentThemeVariant(string newTheme)
        {
            if (Application.Current is null)
                return;

            Application.Current.RequestedThemeVariant = newTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase)
                ? ThemeVariant.Dark
                : ThemeVariant.Light;
        }

        private void ChangeThemePath(string newTheme)
        {
            if (!Directory.Exists("Themes"))
                return;

            var themeFiles = Directory.GetFiles("Themes", "*.xaml");
            var newThemeString = Path.Combine("Themes", $"{newTheme}.xaml");

            foreach (var themeFile in themeFiles)
            {
                if (!newThemeString.Equals(themeFile, StringComparison.OrdinalIgnoreCase))
                    continue;

                var xmlReader = new XmlSerializer(typeof(ThemeResourceDictionaryModel));
                using var streamReader = new StreamReader(themeFile);
                var themeValues = (ThemeResourceDictionaryModel)xmlReader.Deserialize(streamReader)!;

                if (Application.Current?.Resources is not { } resources)
                    return;

                foreach (var solidBrush in themeValues.SolidColorBrush)
                {
                try
                {
                    var color = Color.Parse(solidBrush.Color);
                    resources[solidBrush.Key] = new ImmutableSolidColorBrush(color);
                }
                catch (FormatException) { }
                }

                return;
            }
        }
    }
}
