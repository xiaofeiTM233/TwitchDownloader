using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderAvalonia.Properties;
using TwitchDownloaderAvalonia.Services;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace TwitchDownloaderAvalonia
{
    public partial class MainWindow : Window
    {
        public static PageVodDownload pageVodDownload = new PageVodDownload();
        public static PageClipDownload pageClipDownload = new PageClipDownload();
        public static PageChatDownload pageChatDownload = new PageChatDownload();
        public static PageChatUpdate pageChatUpdate = new PageChatUpdate();
        public static PageChatRender pageChatRender = new PageChatRender();
        public static PageQueue pageQueue = new PageQueue();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnVodDownload_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            NavigateTo(pageVodDownload);
        }

        private void btnClipDownload_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            NavigateTo(pageClipDownload);
        }

        private void btnChatDownload_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            NavigateTo(pageChatDownload);
        }

        private void btnChatUpdate_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            NavigateTo(pageChatUpdate);
        }

        private void btnChatRender_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            NavigateTo(pageChatRender);
        }

        private void btnQueue_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            NavigateTo(pageQueue);
        }

        private void NavigateTo(UserControl page)
        {
            Main.Content = page;
            UpdateSelectedBigButton();
        }

        [GeneratedRegex("{crop_(?=(?:start|end)(?:_|}))")]
        private static partial Regex OldCropParametersRegex { get; }

        private async void Window_Loaded(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Main.Content = pageVodDownload;
            UpdateSelectedBigButton();

            // Replace old crop parameters with new trim parameters
            Settings.Default.TemplateVod = OldCropParametersRegex.Replace(Settings.Default.TemplateVod, "{trim_");
            Settings.Default.TemplateClip = OldCropParametersRegex.Replace(Settings.Default.TemplateClip, "{trim_");
            Settings.Default.TemplateChat = OldCropParametersRegex.Replace(Settings.Default.TemplateChat, "{trim_");
            Settings.Default.Save();

            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version!.StripRevisionIfDefault();
#if DEBUG
            Title = $"Twitch Downloader v{currentVersion} - DEBUG";
#else
            Title = $"Twitch Downloader v{currentVersion}";
#endif

            // TODO: extract FFmpeg handling to a dedicated service
            var ffmpegFileName = Path.ChangeExtension("ffmpeg", OperatingSystem.IsWindows() ? ".exe" : null);
            if (!File.Exists(ffmpegFileName) || File.GetLastWriteTime(ffmpegFileName) < DateTime.Now - TimeSpan.FromDays(365))
            {
                var oldTitle = Title;
                try
                {
                    await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, new FfmpegDownloadProgress());

                    Title = oldTitle;
                }
                catch (Exception ex)
                {
                    Title = oldTitle;
                    var messageBoxResult = await MessageBoxService.ShowAsync(string.Format(Translations.Strings.UnableToDownloadFfmpegFull, "https://ffmpeg.org/download.html", Path.Combine(Environment.CurrentDirectory, ffmpegFileName)),
                        Translations.Strings.UnableToDownloadFfmpeg, MessageBoxButton.OKCancel, MessageBoxImage.Information, this);
                    if (messageBoxResult == MessageBoxResult.OK)
                    {
                        Process.Start(new ProcessStartInfo("https://ffmpeg.org/download.html") { UseShellExecute = true });
                    }

                    if (Settings.Default.VerboseErrors)
                    {
                        await MessageBoxService.ShowAsync(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error, this);
                    }
                }
            }
        }

        private class FfmpegDownloadProgress : IProgress<ProgressInfo>
        {
            private int _lastPercent = -1;

            public void Report(ProgressInfo value)
            {
                var percent = (int)(value.DownloadedBytes / (double)value.TotalBytes * 100);

                if (percent > _lastPercent)
                {
                    var desktopLifetime = (Application.Current as App)?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
                    var window = desktopLifetime?.MainWindow;
                    if (window is null) return;

                    _lastPercent = percent;

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        var oldTitle = window.Title;
                        if (oldTitle.IndexOf('-') == -1) oldTitle += " -";

                        window.Title = string.Concat(
                            oldTitle.AsSpan(0, oldTitle.IndexOf('-')),
                            "- ",
                            string.Format(Translations.Strings.StatusDownloaderFFmpeg, percent.ToString())
                        );
                    });
                }
            }
        }

        private void UpdateSelectedBigButton()
        {
            SetUnderline(btnVodDownloadText, false);
            SetUnderline(btnClipDownloadText, false);
            SetUnderline(btnChatDownloadText, false);
            SetUnderline(btnChatUpdateText, false);
            SetUnderline(btnChatRenderText, false);
            SetUnderline(btnQueueText, false);

            var newPage = Main.Content;
            if (ReferenceEquals(newPage, pageVodDownload))
            {
                SetUnderline(btnVodDownloadText, true);
            }
            else if (ReferenceEquals(newPage, pageClipDownload))
            {
                SetUnderline(btnClipDownloadText, true);
            }
            else if (ReferenceEquals(newPage, pageChatDownload))
            {
                SetUnderline(btnChatDownloadText, true);
            }
            else if (ReferenceEquals(newPage, pageChatUpdate))
            {
                SetUnderline(btnChatUpdateText, true);
            }
            else if (ReferenceEquals(newPage, pageChatRender))
            {
                SetUnderline(btnChatRenderText, true);
            }
            else if (ReferenceEquals(newPage, pageQueue))
            {
                SetUnderline(btnQueueText, true);
            }
        }

        private static void SetUnderline(TextBlock textBlock, bool underline)
        {
            textBlock.TextDecorations = underline ? TextDecorations.Underline : null;
        }
    }
}
