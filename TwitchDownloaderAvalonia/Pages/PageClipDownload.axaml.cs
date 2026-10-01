using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Models.Interfaces;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Services;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects.Gql;
using TwitchDownloaderAvalonia.Models;
using TwitchDownloaderAvalonia.Properties;
using TwitchDownloaderAvalonia.Services;
using TwitchDownloaderAvalonia.Utils;

namespace TwitchDownloaderAvalonia
{
    /// <summary>
    /// Interaction logic for PageClipDownload.axaml
    /// </summary>
    public partial class PageClipDownload : UserControl
    {
        public string clipId = "";
        public string streamerId;
        public string clipperName;
        public string clipperId;
        public DateTime currentVideoTime;
        public TimeSpan clipLength;
        public int viewCount;
        public string game;
        private CancellationTokenSource _cancellationTokenSource;

        public PageClipDownload()
        {
            InitializeComponent();
        }

        private async void btnGetInfo_Click(object sender, RoutedEventArgs e)
        {
            await GetClipInfo();
        }

        private async Task GetClipInfo()
        {
            clipId = ValidateUrl(textUrl.Text.Trim());
            if (string.IsNullOrWhiteSpace(clipId))
            {
                await MessageBoxService.ShowAsync(Translations.Strings.InvalidClipLinkIdMessage.Replace(@"\n", Environment.NewLine), Translations.Strings.InvalidClipLinkId, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                btnGetInfo.IsEnabled = false;
                comboQuality.Items.Clear();
                var clipRenderStatus = await TwitchHelper.GetShareClipRenderStatus(clipId);
                var clip = clipRenderStatus.data.clip;

                var thumbUrl = clip.thumbnailURL;
                var image = await ThumbnailService.TryGetThumb(thumbUrl);
                if (image is null)
                {
                    AppendLog(Translations.Strings.ErrorLog + Translations.Strings.UnableToFindThumbnail);
                    image = await ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL);
                }
                imgThumbnail.Source = image;

                clipLength = TimeSpan.FromSeconds(clip.durationSeconds);
                textStreamer.Text = clip.broadcaster?.displayName ?? Translations.Strings.UnknownUser;
                streamerId = clip.broadcaster?.id;
                clipperName = clip.curator?.displayName ?? Translations.Strings.UnknownUser;
                clipperId = clip.curator?.id;
                var clipCreatedAt = clip.createdAt;
                textCreatedAt.Text = Settings.Default.UTCVideoTime ? clipCreatedAt.ToString(CultureInfo.CurrentCulture) : clipCreatedAt.ToLocalTime().ToString(CultureInfo.CurrentCulture);
                currentVideoTime = Settings.Default.UTCVideoTime ? clipCreatedAt : clipCreatedAt.ToLocalTime();
                textTitle.Text = clip.title;
                labelLength.Text = clipLength.ToString("c");
                viewCount = clip.viewCount;
                game = clip.game?.displayName ?? Translations.Strings.UnknownGame;

                var clipQualities = VideoQualities.FromClip(clip);
                foreach (var quality in clipQualities.Qualities)
                {
                    var item = new ComboBoxItem { Content = quality.Name, Tag = quality };
                    comboQuality.Items.Add(item);
                }

                comboQuality.SelectedIndex = 0;

                UpdateVideoSizeEstimates();

                SetEnabled(true);
            }
            catch (Exception ex)
            {
                await MessageBoxService.ShowAsync(Translations.Strings.UnableToGetClipInfo, Translations.Strings.UnableToGetInfo, MessageBoxButton.OK, MessageBoxImage.Error);
                AppendLog(Translations.Strings.ErrorLog + ex);
                if (Settings.Default.VerboseErrors)
                {
                    await MessageBoxService.ShowAsync(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            btnGetInfo.IsEnabled = true;
        }

        private void UpdateActionButtons(bool isDownloading)
        {
            if (isDownloading)
            {
                SplitBtnDownload.IsVisible = false;
                BtnCancel.IsVisible = true;
                return;
            }
            SplitBtnDownload.IsVisible = true;
            BtnCancel.IsVisible = false;
        }

        private static string ValidateUrl(string text)
        {
            var clipIdMatch = IdParse.MatchClipId(text);
            return clipIdMatch is { Success: true }
                ? clipIdMatch.Value
                : null;
        }

        private void SetPercent(int percent)
        {
            Dispatcher.UIThread.Post(() =>
                statusProgressBar.Value = percent
            );
        }

        private void SetStatus(string message)
        {
            Dispatcher.UIThread.Post(() =>
                statusMessage.Text = message
            );
        }

        private void AppendLog(string message)
        {
            Dispatcher.UIThread.Post(() =>
            {
                BtnClearLog.IsEnabled = true;
                textLog.Inlines?.Add(message + Environment.NewLine);
            });
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            BtnClearLog.IsEnabled = false;
            textLog.Inlines?.Clear();
        }

        private void Page_Initialized(object sender, EventArgs e)
        {
            SetEnabled(false);
            CheckMetadata.IsChecked = Settings.Default.EncodeClipMetadata;
        }

        private void SetEnabled(bool enabled)
        {
            comboQuality.IsEnabled = enabled;
            SplitBtnDownload.IsEnabled = enabled;
            CheckMetadata.IsEnabled = enabled;
        }

        private void UpdateVideoSizeEstimates()
        {
            var selectedIndex = comboQuality.SelectedIndex;

            foreach (var item in comboQuality.Items.Cast<ComboBoxItem>())
            {
                var quality = (IVideoQuality<ShareClipRenderStatusVideoQuality>)item.Tag;
                var bandwidth = quality.BitRate;

                var sizeInBytes = VideoSizeEstimator.EstimateVideoSize(bandwidth, clipLength);
                if (sizeInBytes == 0)
                {
                    item.Content = quality.Name;
                }
                else
                {
                    var newVideoSize = VideoSizeEstimator.StringifyByteCount(sizeInBytes);
                    item.Content = $"{quality.Name} - {newVideoSize}";
                }
            }

            comboQuality.SelectedIndex = selectedIndex;
        }

        public void SetImage(string imageUri, bool isGif)
        {
            var assetPath = imageUri.Replace("Images/", "Assets/", StringComparison.Ordinal);
            var uri = new Uri($"avares://TwitchDownloaderAvalonia/{assetPath}");

            using var stream = AssetLoader.Open(uri);
            var bitmap = new Bitmap(stream);

            Dispatcher.UIThread.Post(() =>
            {
                statusImage.Source = bitmap;
                statusImage.InvalidateVisual();
            });
        }

        private void btnDonate_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.buymeacoffee.com/lay295") { UseShellExecute = true });
        }

        private async void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            var settings = new WindowSettings();
            var owner = TopLevel.GetTopLevel(this) as Window;
            await settings.ShowDialog(owner);

            btnDonate.IsVisible = !Settings.Default.HideDonation;
            statusImage.IsVisible = !Settings.Default.ReduceMotion;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            btnDonate.IsVisible = !Settings.Default.HideDonation;
            statusImage.IsVisible = !Settings.Default.ReduceMotion;
        }

        private async void SplitBtnDownload_Click(object sender, RoutedEventArgs e)
        {
            var defaultFilename = FilenameService.GetFilename(Settings.Default.TemplateClip, textTitle.Text, clipId, currentVideoTime, textStreamer.Text, streamerId, TimeSpan.Zero, clipLength, clipLength, viewCount, game,
                clipperName, clipperId) + ".mp4";

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
            {
                return;
            }

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save",
                SuggestedFileName = defaultFilename,
                FileTypeChoices = [new FilePickerFileType("MP4") { Patterns = ["*.mp4"] }]
            });

            if (file is null)
            {
                return;
            }

            SetEnabled(false);

            ClipDownloadOptions downloadOptions = GetOptions(file.Path.LocalPath);
            _cancellationTokenSource = new CancellationTokenSource();

            var downloadProgress = new TaskProgress((LogLevel)Settings.Default.LogLevels, SetPercent, SetStatus, AppendLog);
            var currentDownload = new ClipDownloader(downloadOptions, downloadProgress);

            SetImage("Images/ppOverheat.gif", true);
            statusMessage.Text = Translations.Strings.StatusDownloading;
            UpdateActionButtons(true);
            try
            {
                await currentDownload.DownloadAsync(_cancellationTokenSource.Token);
                downloadProgress.SetStatus(Translations.Strings.StatusDone);
                SetImage("Images/ppHop.gif", true);
            }
            catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException && _cancellationTokenSource.IsCancellationRequested)
            {
                downloadProgress.SetStatus(Translations.Strings.StatusCanceled);
                SetImage("Images/ppHop.gif", true);
            }
            catch (Exception ex)
            {
                downloadProgress.SetStatus(Translations.Strings.StatusError);
                SetImage("Images/peepoSad.png", false);
                AppendLog(Translations.Strings.ErrorLog + ex.Message);
                if (Settings.Default.VerboseErrors)
                {
                    await MessageBoxService.ShowAsync(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            btnGetInfo.IsEnabled = true;
            downloadProgress.ReportProgress(0);
            _cancellationTokenSource.Dispose();
            UpdateActionButtons(false);
        }

        private ClipDownloadOptions GetOptions(string fileName)
        {
            return new ClipDownloadOptions
            {
                Filename = fileName,
                Id = clipId,
                Quality = ((ComboBoxItem)comboQuality.SelectedItem).Tag.ToString(),
                ThrottleKib = Settings.Default.DownloadThrottleEnabled
                    ? Settings.Default.MaximumBandwidthKib
                    : -1,
                TempFolder = Settings.Default.TempPath,
                EncodeMetadata = CheckMetadata.IsChecked!.Value,
                FfmpegPath = "ffmpeg",
            };
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            statusMessage.Text = Translations.Strings.StatusCanceling;
            SetImage("Images/ppStretch.gif", true);
            try
            {
                _cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException) { }
        }

        private void MenuItemEnqueue_Click(object sender, RoutedEventArgs e)
        {
            var queueOptions = new WindowQueueOptions(this);
            var owner = TopLevel.GetTopLevel(this) as Window;
            _ = queueOptions.ShowDialog(owner);
        }

        private async void TextUrl_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await GetClipInfo();
                e.Handled = true;
            }
        }

        private void CheckMetadata_OnCheckStateChanged(object sender, RoutedEventArgs e)
        {
            if (IsInitialized)
            {
                Settings.Default.EncodeClipMetadata = CheckMetadata.IsChecked!.Value;
                Settings.Default.Save();
            }
        }
    }
}
