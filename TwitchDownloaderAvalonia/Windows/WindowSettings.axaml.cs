using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TwitchDownloaderCore.Services;
using TwitchDownloaderAvalonia.Extensions;
using TwitchDownloaderAvalonia.Models;
using TwitchDownloaderAvalonia.Properties;
using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia
{
    /// <summary>
    /// Interaction logic for WindowSettings.axaml
    /// </summary>
    public partial class WindowSettings : Window
    {
        private bool _cancelSettingsChanges = true;
        private bool _refreshThemeOnCancel;
        private bool _refreshCultureOnCancel;

        public WindowSettings()
        {
            InitializeComponent();
        }

        private async void BtnTempBrowse_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
            {
                return;
            }

            var options = new FolderPickerOpenOptions
            {
                Title = "Select Folder",
                AllowMultiple = false
            };

            if (Directory.Exists(TextTempPath.Text))
            {
                options.SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(TextTempPath.Text);
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            if (folders.Count > 0)
            {
                TextTempPath.Text = folders[0].Path.LocalPath;
            }
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            if (Settings.Default.TempPath == "")
            {
                TextTempPath.Text = Path.GetTempPath().TrimEnd('\\', '/');
            }
            else
            {
                TextTempPath.Text = Settings.Default.TempPath;
            }

            TextVodTemplate.Text = Settings.Default.TemplateVod;
            TextClipTemplate.Text = Settings.Default.TemplateClip;
            TextChatTemplate.Text = Settings.Default.TemplateChat;
            CheckDonation.IsChecked = Settings.Default.HideDonation;
            CheckVerboseErrors.IsChecked = Settings.Default.VerboseErrors;
            NumMaximumBandwidth.Value = Settings.Default.MaximumBandwidthKib;
            NumMaximumBandwidth.IsEnabled = Settings.Default.DownloadThrottleEnabled;
            CheckThrottleEnabled.IsChecked = Settings.Default.DownloadThrottleEnabled;
            RadioTimeFormatUtc.IsChecked = Settings.Default.UTCVideoTime;
            CheckReduceMotion.IsChecked = Settings.Default.ReduceMotion;

            if (Directory.Exists("Themes"))
            {
                // Setup theme dropdown
                ComboTheme.Items.Add("System"); // Cannot be localized
                string[] themeFiles = Directory.GetFiles("Themes", "*.xaml");
                foreach (string themeFile in themeFiles)
                {
                    ComboTheme.Items.Add(Path.GetFileNameWithoutExtension(themeFile));
                }
                ComboTheme.SelectedItem = Settings.Default.GuiTheme;
            }

            // Setup culture dropdown
            var currentCulture = Settings.Default.GuiCulture;
            foreach (var (culture, index) in AvailableCultures.All.Select((x, index) => (x, index)))
            {
                ComboLocale.Items.Add(culture.NativeName);
                if (culture.Code == currentCulture) ComboLocale.SelectedIndex = index;
            }

            LogLevelItemsControl.ItemsSource = new[]
            {
                new LogLevelItem { Content = Translations.Strings.LogLevelVerbose, Tag = LogLevel.Verbose },
                new LogLevelItem { Content = Translations.Strings.LogLevelInfo, Tag = LogLevel.Info },
                new LogLevelItem { Content = Translations.Strings.LogLevelWarning, Tag = LogLevel.Warning },
                new LogLevelItem { Content = Translations.Strings.LogLevelError, Tag = LogLevel.Error },
            };
            var currentLogLevels = (LogLevel)Settings.Default.LogLevels;
            foreach (LogLevelItem item in LogLevelItemsControl.ItemsSource.Cast<LogLevelItem>())
            {
                item.IsChecked = currentLogLevels.HasFlag(item.Tag);
            }
            UpdateLogLevelSummary();

            var currentCollisionBehavior = (CollisionBehavior)Settings.Default.FileCollisionBehavior;
            for (var i = 0; i < ComboFileCollisionBehavior.Items.Count; i++)
            {
                var current = (ComboBoxItem)ComboFileCollisionBehavior.Items[i]!;
                if (currentCollisionBehavior == (CollisionBehavior)current.Tag)
                {
                    ComboFileCollisionBehavior.SelectedIndex = i;
                    break;
                }
            }
        }

        private void UpdateLogLevelSummary()
        {
            var selected = LogLevelItemsControl.ItemsSource
                .Cast<LogLevelItem>()
                .Where(x => x.IsChecked)
                .Select(x => x.Content)
                .ToArray();
            ComboLogLevels.Content = selected.Length > 0 ? string.Join(", ", selected) : " ";
        }

        private void LogLevelItem_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
                return;

            var newLogLevel = LogLevelItemsControl.ItemsSource
                .Cast<LogLevelItem>()
                .Where(x => x.IsChecked)
                .Sum(item => (int)item.Tag);
            Settings.Default.LogLevels = newLogLevel;
            UpdateLogLevelSummary();
        }

        private async void BtnClearCache_Click(object sender, RoutedEventArgs e)
        {
            var messageBoxResult = await MessageBoxService.ShowAsync(Translations.Strings.ClearCacheConfirmation.Replace(@"\n", Environment.NewLine), Translations.Strings.DeleteConfirmation, MessageBoxButton.YesNo);
            if (messageBoxResult == MessageBoxResult.Yes)
            {
                //Let's clear the user selected temp folder and the default one
                CacheDirectoryService.ClearCacheDirectory(Settings.Default.TempPath, out _);
                CacheDirectoryService.ClearCacheDirectory(Path.GetTempPath(), out _);
            }
        }

        private void Window_Closing(object sender, WindowClosingEventArgs e)
        {
            if (_cancelSettingsChanges)
            {
                Settings.Default.Reload();
                if (_refreshThemeOnCancel)
                {
                    App.RequestAppThemeChange();
                }

                if (_refreshCultureOnCancel)
                {
                    App.RequestCultureChange();
                }
            }
        }

        private void OpenUri(string uriString)
        {
            if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
            {
                string destinationPath = Path.Combine(Environment.CurrentDirectory, uriString);
                uri = new Uri(destinationPath);
            }

            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }

        private void ThemeHint_OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            OpenUri("Themes");
            e.Handled = true;
        }

        private void LanguageHint_OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            OpenUri("https://github.com/lay295/TwitchDownloader/blob/master/TwitchDownloaderWPF/README.md#localization");
            e.Handled = true;
        }

        private void DateFormattingLink_OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            OpenUri("https://learn.microsoft.com/dotnet/standard/base-types/custom-date-and-time-format-strings");
            e.Handled = true;
        }

        private void TimeSpanFormattingLink_OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            OpenUri("https://learn.microsoft.com/dotnet/standard/base-types/custom-timespan-format-strings");
            e.Handled = true;
        }

        private void ComboTheme_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            if (ComboTheme.SelectedItem is string selectedTheme && !selectedTheme.Equals(Settings.Default.GuiTheme, StringComparison.OrdinalIgnoreCase))
            {
                _refreshThemeOnCancel = true;
                Settings.Default.GuiTheme = selectedTheme;
                App.RequestAppThemeChange(true);
            }
        }

        private void ComboLocale_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            if (ComboLocale.SelectedIndex == -1)
                return;

            var selectedCulture = AvailableCultures.All[ComboLocale.SelectedIndex].Code;
            if (selectedCulture != Settings.Default.GuiCulture)
            {
                _refreshCultureOnCancel = true;
                Settings.Default.GuiCulture = selectedCulture;
                App.RequestCultureChange();
            }
        }

        private void CheckThrottleEnabled_OnCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
                return;

            NumMaximumBandwidth.IsEnabled = CheckThrottleEnabled.IsChecked.GetValueOrDefault();
            Settings.Default.DownloadThrottleEnabled = CheckThrottleEnabled.IsChecked.GetValueOrDefault();
        }

        private void NumMaximumBandwidth_OnValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.MaximumBandwidthKib = (int)NumMaximumBandwidth.Value.GetValueOrDefault();
        }

        private async void BtnResetSettings_OnClick(object sender, RoutedEventArgs e)
        {
            if (await MessageBoxService.ShowAsync(Translations.Strings.ResetSettingsConfirmationMessage, Translations.Strings.ResetSettingsConfirmation, MessageBoxButton.YesNo, MessageBoxImage.Warning) ==
                MessageBoxResult.Yes)
            {
                Settings.Default.Reset();
                Settings.Default.UpgradeRequired = false;
                Settings.Default.Save();

                // TODO: Don't require restarting the application to apply
                await MessageBoxService.ShowAsync(Translations.Strings.TheApplicationMustBeRestartedMessage,
                    string.Format(Translations.Strings.RestartTheApplication, nameof(TwitchDownloaderAvalonia)),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnSaveSettings_OnClick(object sender, RoutedEventArgs e)
        {
            _cancelSettingsChanges = false;
            Settings.Default.Save();
            Close();
        }

        private void BtnCancelSettings_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TextTempPath_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.TempPath = TextTempPath.Text;
        }

        private void CheckDonation_OnCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.HideDonation = CheckDonation.IsChecked.GetValueOrDefault();
        }

        private void RadioTimeFormat_OnCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.UTCVideoTime = RadioTimeFormatUtc.IsChecked.GetValueOrDefault();
        }

        private void CheckVerboseErrors_OnCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.VerboseErrors = CheckVerboseErrors.IsChecked.GetValueOrDefault();
        }

        private void TextVodTemplate_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.TemplateVod = TextVodTemplate.Text;
        }

        private void TextClipTemplate_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.TemplateClip = TextClipTemplate.Text;
        }

        private void TextChatTemplate_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.TemplateChat = TextChatTemplate.Text;
        }

        private void ComboFileCollisionBehavior_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            if (ComboFileCollisionBehavior.SelectedItem is ComboBoxItem behavior)
            {
                Settings.Default.FileCollisionBehavior = (int)behavior.Tag;
            }
        }

        private void FilenameParameter_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (!IsInitialized || sender is not TextBlock { Text: var parameter })
                return;

            var properties = e.GetCurrentPoint(this).Properties;
            var isLeft = properties.IsLeftButtonPressed;
            var isMiddle = e.Pointer.Type == PointerType.Mouse && properties.IsMiddleButtonPressed;
            if (!isLeft && !isMiddle)
                return;

            var focusedElement = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            var textBox = GetFilenameTemplateTextBox(focusedElement);

            if (textBox is null)
                return;

            var oldCaretPos = textBox.CaretIndex;
            if (!textBox.TryInsertAtCaret(parameter))
                return;

            if (isMiddle && oldCaretPos != -1)
            {
                // If we inserted a *_custom template, we can focus inside the quotation marks
                var quoteIndex = parameter.LastIndexOf('"');
                if (quoteIndex != -1)
                {
                    textBox.CaretIndex = oldCaretPos + quoteIndex;
                }
            }

            e.Handled = true;
        }

        [return: MaybeNull]
        private TextBox GetFilenameTemplateTextBox(object inputElement)
        {
            if (ReferenceEquals(inputElement, TextVodTemplate))
                return TextVodTemplate;

            if (ReferenceEquals(inputElement, TextClipTemplate))
                return TextClipTemplate;

            if (ReferenceEquals(inputElement, TextChatTemplate))
                return TextChatTemplate;

            return null;
        }

        private void CheckReduceMotion_OnCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.ReduceMotion = CheckReduceMotion.IsChecked.GetValueOrDefault();
        }
    }

    public class LogLevelItem : INotifyPropertyChanged
    {
        private bool _isChecked;

        public string Content { get; init; }
        public LogLevel Tag { get; init; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value)
                    return;

                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
