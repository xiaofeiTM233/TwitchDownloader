using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using TwitchDownloaderCore;
using TwitchDownloaderCore.TwitchObjects.Gql;
using TwitchDownloaderAvalonia.Properties;
using TwitchDownloaderAvalonia.Services;
using TwitchDownloaderAvalonia.TwitchTasks;

namespace TwitchDownloaderAvalonia
{
    /// <summary>
    /// Interaction logic for WindowMassDownload.axaml
    /// </summary>
    public partial class WindowMassDownload : Window
    {
        private readonly ComboBoxItem _comboBoxItemClearChannels = new()
        {
            Content = Translations.Strings.ClearRecentChannels
        };

        public DownloadType downloaderType { get; set; }
        public ObservableCollection<TaskData> videoList { get; set; } = [];
        public readonly List<TaskData> selectedItems = [];
        public readonly List<string> cursorList = [];
        public int cursorIndex = 0;
        public User currentChannel;
        public string period = "";
        public string videoType = "";
        public int videoCount = 50;

        public WindowMassDownload(DownloadType type)
        {
            downloaderType = type;
            InitializeComponent();
            itemList.ItemsSource = videoList;
            if (downloaderType == DownloadType.Video)
            {
                ComboSortByDate.IsVisible = false;
            }
            else if (downloaderType == DownloadType.Clip)
            {
                ComboSortByVideoType.IsVisible = false;
            }
            btnNext.IsEnabled = false;
            btnPrev.IsEnabled = false;

            StatusImage.Source = LoadAssetImage("Images/ppOverheat.gif");

            UpdateRecentChannels();
            ComboChannel.Text = "";
        }

        private static IImage LoadAssetImage(string path)
        {
            try
            {
                var assetPath = path.Replace("Images/", "Assets/", StringComparison.Ordinal);
                var uri = new Uri($"avares://TwitchDownloaderAvalonia/{assetPath}");
                using var stream = AssetLoader.Open(uri);
                return new Bitmap(stream);
            }
            catch
            {
                return null;
            }
        }

        private async void btnChannel_Click(object sender, RoutedEventArgs e)
        {
            await ChangeCurrentChannel();
        }

        private void ResetLists()
        {
            videoList.Clear();
            cursorList.Clear();
            cursorList.Add("");
            cursorIndex = 0;
        }

        private async Task ChangeCurrentChannel()
        {
            if (!IsInitialized)
                return;

            var textTrimmed = ComboChannel.Text?.Trim() ?? "";
            if (!textTrimmed.Equals(currentChannel?.login, StringComparison.InvariantCultureIgnoreCase))
            {
                currentChannel = null;
                if (!string.IsNullOrEmpty(textTrimmed) && !textTrimmed.Any(char.IsWhiteSpace))
                {
                    try
                    {
                        var idRes = await TwitchHelper.GetUserIds([textTrimmed.ToLowerInvariant()]);
                        var infoRes = await TwitchHelper.GetUserInfo(idRes.data.users.Select(x => x.id));
                        currentChannel = infoRes.data.users[0];
                    }
                    catch (Exception ex)
                    {
                        if (Settings.Default.VerboseErrors)
                        {
                            await MessageBoxService.ShowAsync(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error, this);
                        }
                    }
                }
            }

            ResetLists();
            await UpdateList();
        }

        private async Task UpdateList()
        {
            if (!IsInitialized)
                return;

            if (!Settings.Default.ReduceMotion)
            {
                StatusImage.IsVisible = true;
            }

            if (string.IsNullOrWhiteSpace(currentChannel?.login))
            {
                // Pretend we are doing something so the status icon has time to show
                await Task.Delay(50);
                ResetLists();
                StatusImage.IsVisible = false;
                return;
            }

            if (downloaderType == DownloadType.Video)
            {
                string currentCursor = "";
                if (cursorList.Count > 0 && cursorIndex >= 0)
                {
                    currentCursor = cursorList[cursorIndex];
                }

                GqlVideoSearchResponse res;
                try
                {
                    res = await TwitchHelper.GetGqlVideos(currentChannel.login, currentCursor, videoCount, videoType);
                }
                catch (Exception ex)
                {
                    await MessageBoxService.ShowAsync(string.Format(Translations.Strings.UnableToGetChannelVideosMessage, ex.Message), Translations.Strings.UnableToGetChannelVideos, MessageBoxButton.OK, MessageBoxImage.Error, this);

                    if (Settings.Default.VerboseErrors)
                    {
                        await MessageBoxService.ShowAsync(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error, this);
                    }

                    return;
                }

                UpdateRecentChannels();
                videoList.Clear();
                if (res.data.user != null)
                {
                    foreach (var video in res.data.user.videos.edges)
                    {
                        var thumbnail = await ThumbnailService.TryGetThumb(video.node.previewThumbnailURL)
                            ?? await ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL);

                        videoList.Add(new TaskData
                        {
                            Title = video.node.title,
                            Length = video.node.lengthSeconds,
                            Id = video.node.id,
                            Time = Settings.Default.UTCVideoTime ? video.node.createdAt : video.node.createdAt.ToLocalTime(),
                            Views = video.node.viewCount,
                            StreamerName = currentChannel.displayName,
                            StreamerId = currentChannel.id,
                            Game = video.node.game?.displayName ?? Translations.Strings.UnknownGame,
                            Thumbnail = thumbnail
                        });
                    }

                    btnPrev.IsEnabled = cursorIndex > 0;
                    if (res.data.user.videos.pageInfo.hasNextPage)
                    {
                        string newCursor = res.data.user.videos.edges.FirstOrDefault()?.cursor;
                        if (newCursor is not null)
                        {
                            btnNext.IsEnabled = true;
                            if (!cursorList.Contains(newCursor))
                            {
                                cursorList.Add(newCursor);
                            }
                        }
                    }
                }
            }
            else
            {
                string currentCursor = "";
                if (cursorList.Count > 0 && cursorIndex >= 0)
                {
                    currentCursor = cursorList[cursorIndex];
                }

                GqlClipSearchResponse res;
                try
                {
                    res = await TwitchHelper.GetGqlClips(currentChannel.login, period, currentCursor, videoCount);
                }
                catch (Exception ex)
                {
                    await MessageBoxService.ShowAsync(string.Format(Translations.Strings.UnableToGetChannelClipsMessage, ex.Message), Translations.Strings.UnableToGetChannelClips, MessageBoxButton.OK, MessageBoxImage.Error, this);

                    if (Settings.Default.VerboseErrors)
                    {
                        await MessageBoxService.ShowAsync(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error, this);
                    }

                    return;
                }

                UpdateRecentChannels();
                videoList.Clear();
                if (res.data.user != null)
                {
                    foreach (var clip in res.data.user.clips.edges)
                    {
                        var thumbnail = await ThumbnailService.TryGetThumb(clip.node.thumbnailURL)
                            ?? await ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL);

                        videoList.Add(new TaskData
                        {
                            Title = clip.node.title,
                            Length = clip.node.durationSeconds,
                            Id = clip.node.slug,
                            Time = Settings.Default.UTCVideoTime ? clip.node.createdAt : clip.node.createdAt.ToLocalTime(),
                            Views = clip.node.viewCount,
                            StreamerName = currentChannel.displayName,
                            StreamerId = currentChannel.id,
                            ClipperName = clip.node.curator?.displayName ?? Translations.Strings.UnknownUser,
                            ClipperId = clip.node.curator?.id,
                            Game = clip.node.game?.displayName ?? Translations.Strings.UnknownGame,
                            Thumbnail = thumbnail
                        });
                    }

                    btnPrev.IsEnabled = cursorIndex > 0;
                    if (res.data.user.clips.pageInfo.hasNextPage)
                    {
                        string newCursor = res.data.user.clips.edges.FirstOrDefault(x => x.cursor != null)?.cursor;
                        if (newCursor is not null)
                        {
                            btnNext.IsEnabled = true;
                            if (!cursorList.Contains(newCursor))
                            {
                                cursorList.Add(newCursor);
                            }
                        }
                    }
                }
            }

            StatusImage.IsVisible = false;
        }

        private void Border_OnPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton != MouseButton.Left) return;
            if (sender is not Border border) return;
            if (border.DataContext is not TaskData taskData) return;

            if (selectedItems.Any(x => x.Id == taskData.Id))
            {
                border.Background = Brushes.Transparent;
                selectedItems.RemoveAll(x => x.Id == taskData.Id);
            }
            else
            {
                border.Background = Brushes.LightBlue;
                selectedItems.Add(taskData);
            }
            textCount.Text = selectedItems.Count.ToString();
        }

        private async void btnNext_Click(object sender, RoutedEventArgs e)
        {
            btnNext.IsEnabled = false;
            btnPrev.IsEnabled = false;
            if (cursorIndex < cursorList.Count - 1)
            {
                cursorIndex++;
            }

            await UpdateList();
        }

        private async void btnPrev_Click(object sender, RoutedEventArgs e)
        {
            btnNext.IsEnabled = false;
            btnPrev.IsEnabled = false;
            if (cursorIndex > 0)
            {
                cursorIndex--;
            }

            await UpdateList();
        }

        private void Border_Initialized(object sender, EventArgs e)
        {
            if (sender is not Border border) return;
            if (border.DataContext is not TaskData taskData) return;

            if (selectedItems.Any(x => x.Id == taskData.Id))
            {
                border.Background = Brushes.LightBlue;
            }
        }

        private async void btnQueue_Click(object sender, RoutedEventArgs e)
        {
            if (selectedItems.Count > 0)
            {
                var queue = new WindowQueueOptions(selectedItems)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                await queue.ShowDialog(this);
                if (queue.OK)
                    Close();
            }
        }

        private async void ComboSortByDate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            period = ((ComboBoxItem)ComboSortByDate.SelectedItem).Tag.ToString();
            ResetLists();
            await UpdateList();
        }

        private async void ComboSortByVideoType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            videoType = ((ComboBoxItem)ComboSortByVideoType.SelectedItem).Tag.ToString();
            ResetLists();
            await UpdateList();
        }

        private void btnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            //I'm sure there is a much better way to do this. Could not find a way to iterate over each itemcontrol border
            foreach (var video in videoList)
            {
                if (selectedItems.All(x => x.Id != video.Id))
                {
                    selectedItems.Add(video);
                }
            }

            // Remove and re-add all of the items to trigger Border_Initialized
            var oldData = videoList.ToArray();
            videoList.Clear();
            foreach (var item in oldData)
            {
                videoList.Add(item);
            }

            textCount.Text = selectedItems.Count.ToString();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Title = downloaderType == DownloadType.Video
                ? Translations.Strings.TitleVideoMassDownloader
                : Translations.Strings.TitleClipMassDownloader;
        }

        private async void ComboChannel_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await ChangeCurrentChannel();
                e.Handled = true;
            }
        }

        private async void ComboVideoCount_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            videoCount = int.Parse((string)((ComboBoxItem)ComboVideoCount.SelectedItem).Content);
            ResetLists();
            await UpdateList();
        }

        private async void MenuItemCopyVideoID_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: TaskData taskData }) return;

            var id = taskData.Id;
            if (!await ClipboardService.TrySetText(id))
            {
                await MessageBoxService.ShowAsync(Translations.Strings.FailedToCopyToClipboard, Translations.Strings.FailedToCopyToClipboard, MessageBoxButton.OK, MessageBoxImage.Error, this);
            }

            e.Handled = true;
        }

        private async void MenuItemCopyVideoUrl_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: TaskData taskData }) return;

            var id = taskData.Id;
            var url = id.All(char.IsDigit)
                ? $"https://twitch.tv/videos/{id}"
                : $"https://clips.twitch.tv/{id}";

            if (!await ClipboardService.TrySetText(url))
            {
                await MessageBoxService.ShowAsync(Translations.Strings.FailedToCopyToClipboard, Translations.Strings.FailedToCopyToClipboard, MessageBoxButton.OK, MessageBoxImage.Error, this);
            }

            e.Handled = true;
        }

        private void MenuItemOpenInBrowser_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: TaskData taskData }) return;

            var id = taskData.Id;
            var url = id.All(char.IsDigit)
                ? $"https://twitch.tv/videos/{id}"
                : $"https://clips.twitch.tv/{id}";

            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

            e.Handled = true;
        }

        private async void ComboChannel_OnDropDownClosed(object sender, EventArgs e)
        {
            if (!IsInitialized || ComboChannel.SelectedItem is not ComboBoxItem comboBoxItem)
                return;

            if (string.Equals(comboBoxItem.Content as string, currentChannel?.login, StringComparison.OrdinalIgnoreCase))
                return;

            if (ReferenceEquals(comboBoxItem, _comboBoxItemClearChannels))
            {
                Settings.Default.RecentChannels.Clear();
                currentChannel = null;
                ComboChannel.Text = "";
                UpdateRecentChannels();
            }

            await ChangeCurrentChannel();
        }

        private void UpdateRecentChannels()
        {
            var recentChannels = Settings.Default.RecentChannels ?? new System.Collections.Specialized.StringCollection();

            if (!string.IsNullOrWhiteSpace(currentChannel?.login))
            {
                // Move the current channel to the top of the list
                recentChannels.Remove(currentChannel.login);
                recentChannels.Insert(0, currentChannel.login);
            }

            while (recentChannels.Count > 15)
            {
                recentChannels.RemoveAt(recentChannels.Count - 1);
            }

            var oldText = ComboChannel.Text;

            ComboChannel.Items.Clear();
            foreach (var channel in recentChannels)
            {
                ComboChannel.Items.Add(new ComboBoxItem { Content = channel });
            }

            // Restore the previous text
            ComboChannel.Text = oldText;

            ComboChannel.Items.Add(_comboBoxItemClearChannels);

            Settings.Default.RecentChannels = recentChannels;
            Settings.Default.Save();
        }
    }
}
