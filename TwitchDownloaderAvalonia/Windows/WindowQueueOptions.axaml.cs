using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Services;
using TwitchDownloaderAvalonia.Properties;
using TwitchDownloaderAvalonia.Services;
using TwitchDownloaderAvalonia.TwitchTasks;

namespace TwitchDownloaderAvalonia
{
    /// <summary>
    /// Interaction logic for WindowQueueOptions.axaml
    /// </summary>
    public partial class WindowQueueOptions : Window
    {
        // This file is absolutely atrocious, but fixing it would mean rewriting the entire GUI in a more abstract form

        private readonly List<TaskData> _dataList;
        private readonly UserControl _parentPage;
        public bool OK { get; private set; }

        public WindowQueueOptions(UserControl page)
        {
            _parentPage = page;
            InitializeComponent();

            textFolder.Text = Settings.Default.QueueFolder;

            TextPreferredQuality.IsVisible = false;
            ComboPreferredQuality.IsVisible = false;

            if (page is PageVodDownload)
            {
                checkVideo.IsChecked = true;
                checkVideo.IsEnabled = false;
            }
            else if (page is PageClipDownload)
            {
                checkVideo.IsChecked = true;
                checkVideo.IsEnabled = false;
                checkDelay.IsVisible = false;
                checkDelayChat.IsVisible = false;
            }
            else if (page is PageChatDownload chatPage)
            {
                checkVideo.IsVisible = false;
                checkDelay.IsVisible = false;
                checkChat.IsChecked = true;
                checkChat.IsEnabled = false;
                TextDownloadFormat.IsVisible = false;
                radioJson.IsVisible = false;
                radioTxt.IsVisible = false;
                radioHTML.IsVisible = false;
                TextCompression.IsVisible = false;
                RadioCompressionNone.IsVisible = false;
                RadioCompressionGzip.IsVisible = false;
                checkEmbed.IsVisible = false;
                StackThirdPartyEmbed.IsVisible = false;
                if (!chatPage.radioJson.IsChecked.GetValueOrDefault())
                {
                    checkRender.IsChecked = false;
                    checkRender.IsEnabled = false;
                }
                if (chatPage.downloadType == DownloadType.Clip)
                {
                    checkDelayChat.IsVisible = false;
                }
            }
            else if (page is PageChatUpdate)
            {
                checkVideo.IsVisible = false;
                checkDelay.IsVisible = false;
                checkChat.IsVisible = false;
                TextDownloadFormat.IsVisible = false;
                radioJson.IsVisible = false;
                radioTxt.IsVisible = false;
                radioHTML.IsVisible = false;
                TextCompression.IsVisible = false;
                RadioCompressionNone.IsVisible = false;
                RadioCompressionGzip.IsVisible = false;
                checkEmbed.IsVisible = false;
                StackThirdPartyEmbed.IsVisible = false;
                checkDelayChat.IsVisible = false;
                checkRender.IsVisible = false;
            }
            else if (page is PageChatRender)
            {
                checkVideo.IsVisible = false;
                checkDelay.IsVisible = false;
                checkChat.IsVisible = false;
                TextDownloadFormat.IsVisible = false;
                radioJson.IsVisible = false;
                radioTxt.IsVisible = false;
                radioHTML.IsVisible = false;
                TextCompression.IsVisible = false;
                RadioCompressionNone.IsVisible = false;
                RadioCompressionGzip.IsVisible = false;
                checkEmbed.IsVisible = false;
                StackThirdPartyEmbed.IsVisible = false;
                checkDelayChat.IsVisible = false;
                checkRender.IsChecked = true;
                checkRender.IsEnabled = false;
            }
        }

        public WindowQueueOptions(List<TaskData> dataList)
        {
            _dataList = dataList;
            InitializeComponent();

            textFolder.Text = Settings.Default.QueueFolder;

            if (_dataList.Any(x => x.Id.All(char.IsDigit)))
            {
                ComboPreferredQuality.Items.Add(new ComboBoxItem { Content = "Audio Only" });
            }
            else
            {
                checkDelay.IsVisible = false;
            }

            var preferredQuality = Settings.Default.PreferredQuality;
            for (var i = 0; i < ComboPreferredQuality.Items.Count; i++)
            {
                if (ComboPreferredQuality.Items[i] is ComboBoxItem { Content: string quality } && quality == preferredQuality)
                {
                    ComboPreferredQuality.SelectedIndex = i;
                    break;
                }
            }
        }

        private static Window GetOwnerWindow()
        {
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
            {
                return mainWindow;
            }

            return null;
        }

        private FileInfo HandleFileCollisionCallback(FileInfo fileInfo)
        {
            return FileCollisionService.HandleCollisionCallback(fileInfo, GetOwnerWindow());
        }

        private async void btnQueue_Click(object sender, RoutedEventArgs e)
        {
            if (_parentPage != null)
            {
                if (_parentPage is PageVodDownload vodDownloadPage)
                {
                    string folderPath = textFolder.Text;
                    if (!Directory.Exists(folderPath))
                    {
                        try
                        {
                            TwitchHelper.CreateDirectory(folderPath);
                        }
                        catch (Exception ex)
                        {
                            await MessageBoxService.ShowAsync(Translations.Strings.InvalidFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error, this);

                            if (Settings.Default.VerboseErrors)
                            {
                                await MessageBoxService.ShowAsync(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error, this);
                            }

                            return;
                        }
                    }

                    VideoDownloadOptions downloadOptions = vodDownloadPage.GetOptions(null, textFolder.Text);
                    downloadOptions.DelayDownload = checkDelay.IsChecked.GetValueOrDefault();
                    downloadOptions.FileCollisionCallback = HandleFileCollisionCallback;

                    VodDownloadTask downloadTask = new VodDownloadTask
                    {
                        DownloadOptions = downloadOptions,
                        Info =
                        {
                            Title = vodDownloadPage.textTitle.Text,
                            Thumbnail = vodDownloadPage.imgThumbnail.Source
                        }
                    };

                    lock (PageQueue.TaskLock)
                    {
                        PageQueue.TaskList.Add(downloadTask);
                    }

                    if (checkChat.IsChecked.GetValueOrDefault())
                    {
                        ChatDownloadOptions chatOptions = MainWindow.pageChatDownload.GetOptions(null);
                        chatOptions.Id = downloadOptions.Id.ToString();
                        if (radioJson.IsChecked == true)
                            chatOptions.DownloadFormat = ChatFormat.Json;
                        else if (radioHTML.IsChecked == true)
                            chatOptions.DownloadFormat = ChatFormat.Html;
                        else
                            chatOptions.DownloadFormat = ChatFormat.Text;
                        // TODO: Support non-json chat compression
                        if (RadioCompressionGzip.IsChecked.GetValueOrDefault() && chatOptions.DownloadFormat == ChatFormat.Json)
                            chatOptions.Compression = ChatCompression.Gzip;
                        chatOptions.EmbedData = checkEmbed.IsChecked.GetValueOrDefault();
                        chatOptions.BttvEmotes = CheckBttvEmbed.IsChecked.GetValueOrDefault();
                        chatOptions.FfzEmotes = CheckFfzEmbed.IsChecked.GetValueOrDefault();
                        chatOptions.StvEmotes = CheckStvEmbed.IsChecked.GetValueOrDefault();
                        chatOptions.DelayDownload = checkDelayChat.IsChecked.GetValueOrDefault();
                        chatOptions.Filename = Path.Combine(folderPath, Path.GetFileNameWithoutExtension(downloadOptions.Filename) + chatOptions.FileExtension);
                        chatOptions.FileCollisionCallback = HandleFileCollisionCallback;

                        if (downloadOptions.TrimBeginning)
                        {
                            chatOptions.TrimBeginning = true;
                            chatOptions.TrimBeginningTime = downloadOptions.TrimBeginningTime.TotalSeconds;
                        }

                        if (downloadOptions.TrimEnding)
                        {
                            chatOptions.TrimEnding = true;
                            chatOptions.TrimEndingTime = downloadOptions.TrimEndingTime.TotalSeconds;
                        }

                        ChatDownloadTask chatTask = new ChatDownloadTask
                        {
                            DownloadOptions = chatOptions,
                            Info =
                            {
                                Title = vodDownloadPage.textTitle.Text,
                                Thumbnail = vodDownloadPage.imgThumbnail.Source
                            }
                        };

                        lock (PageQueue.TaskLock)
                        {
                            PageQueue.TaskList.Add(chatTask);
                        }

                        if (checkRender.IsChecked.GetValueOrDefault() && chatOptions.DownloadFormat == ChatFormat.Json)
                        {
                            ChatRenderOptions renderOptions = MainWindow.pageChatRender.GetOptions(Path.ChangeExtension(chatOptions.Filename.Replace(".gz", ""), '.' + GetRenderFormat().ToLower()));
                            if (renderOptions.OutputFile.Trim() == downloadOptions.Filename!.Trim())
                            {
                                //Just in case VOD and chat paths are the same. Like the previous defaults
                                renderOptions.OutputFile = Path.ChangeExtension(chatOptions.Filename.Replace(".gz", ""), " - CHAT." + GetRenderFormat().ToLower());
                            }
                            renderOptions.InputFile = chatOptions.Filename;
                            renderOptions.FileCollisionCallback = HandleFileCollisionCallback;

                            ChatRenderTask renderTask = new ChatRenderTask
                            {
                                DownloadOptions = renderOptions,
                                Info =
                                {
                                    Title = vodDownloadPage.textTitle.Text,
                                    Thumbnail = vodDownloadPage.imgThumbnail.Source
                                },
                                DependantTask = chatTask,
                            };
                            renderTask.ChangeStatus(TwitchTaskStatus.Waiting);

                            lock (PageQueue.TaskLock)
                            {
                                PageQueue.TaskList.Add(renderTask);
                            }
                        }
                    }

                    OK = true;
                    Close();
                }

                if (_parentPage is PageClipDownload clipDownloadPage)
                {
                    string folderPath = textFolder.Text;
                    if (!Directory.Exists(folderPath))
                    {
                        try
                        {
                            TwitchHelper.CreateDirectory(folderPath);
                        }
                        catch (Exception ex)
                        {
                            await MessageBoxService.ShowAsync(Translations.Strings.InvalidFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error, this);

                            if (Settings.Default.VerboseErrors)
                            {
                                await MessageBoxService.ShowAsync(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error, this);
                            }

                            return;
                        }
                    }

                    ClipDownloadOptions downloadOptions = new ClipDownloadOptions
                    {
                        Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateClip, clipDownloadPage.textTitle.Text, clipDownloadPage.clipId,
                            clipDownloadPage.currentVideoTime, clipDownloadPage.textStreamer.Text, clipDownloadPage.streamerId, TimeSpan.Zero, clipDownloadPage.clipLength,
                            clipDownloadPage.clipLength, clipDownloadPage.viewCount, clipDownloadPage.game, clipDownloadPage.clipperName, clipDownloadPage.clipperId) + ".mp4"),
                        Id = clipDownloadPage.clipId,
                        Quality = clipDownloadPage.comboQuality.SelectedItem is ComboBoxItem qualityItem ? qualityItem.Tag?.ToString() : null,
                        ThrottleKib = Settings.Default.DownloadThrottleEnabled
                            ? Settings.Default.MaximumBandwidthKib
                            : -1,
                        TempFolder = Settings.Default.TempPath,
                        EncodeMetadata = clipDownloadPage.CheckMetadata.IsChecked!.Value,
                        FfmpegPath = "ffmpeg",
                        FileCollisionCallback = HandleFileCollisionCallback,
                    };

                    ClipDownloadTask downloadTask = new ClipDownloadTask
                    {
                        DownloadOptions = downloadOptions,
                        Info =
                        {
                            Title = clipDownloadPage.textTitle.Text,
                            Thumbnail = clipDownloadPage.imgThumbnail.Source
                        }
                    };

                    lock (PageQueue.TaskLock)
                    {
                        PageQueue.TaskList.Add(downloadTask);
                    }

                    if (checkChat.IsChecked.GetValueOrDefault())
                    {
                        ChatDownloadOptions chatOptions = MainWindow.pageChatDownload.GetOptions(null);
                        chatOptions.Id = downloadOptions.Id;
                        if (radioJson.IsChecked == true)
                            chatOptions.DownloadFormat = ChatFormat.Json;
                        else if (radioHTML.IsChecked == true)
                            chatOptions.DownloadFormat = ChatFormat.Html;
                        else
                            chatOptions.DownloadFormat = ChatFormat.Text;
                        // TODO: Support non-json chat compression
                        if (RadioCompressionGzip.IsChecked.GetValueOrDefault() && chatOptions.DownloadFormat == ChatFormat.Json)
                            chatOptions.Compression = ChatCompression.Gzip;
                        chatOptions.TimeFormat = TimestampFormat.Relative;
                        chatOptions.EmbedData = checkEmbed.IsChecked.GetValueOrDefault();
                        chatOptions.BttvEmotes = CheckBttvEmbed.IsChecked.GetValueOrDefault();
                        chatOptions.FfzEmotes = CheckFfzEmbed.IsChecked.GetValueOrDefault();
                        chatOptions.StvEmotes = CheckStvEmbed.IsChecked.GetValueOrDefault();
                        chatOptions.DelayDownload = checkDelayChat.IsChecked.GetValueOrDefault();
                        chatOptions.Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateChat, downloadTask.Info.Title, chatOptions.Id,
                            clipDownloadPage.currentVideoTime, clipDownloadPage.textStreamer.Text, clipDownloadPage.streamerId, TimeSpan.Zero, clipDownloadPage.clipLength, clipDownloadPage.clipLength,
                            clipDownloadPage.viewCount, clipDownloadPage.game, clipDownloadPage.clipperName, clipDownloadPage.clipId) + chatOptions.FileExtension);
                        chatOptions.FileCollisionCallback = HandleFileCollisionCallback;

                        ChatDownloadTask chatTask = new ChatDownloadTask
                        {
                            DownloadOptions = chatOptions,
                            Info =
                            {
                                Title = clipDownloadPage.textTitle.Text,
                                Thumbnail = clipDownloadPage.imgThumbnail.Source
                            }
                        };

                        lock (PageQueue.TaskLock)
                        {
                            PageQueue.TaskList.Add(chatTask);
                        }

                        if (checkRender.IsChecked.GetValueOrDefault() && chatOptions.DownloadFormat == ChatFormat.Json)
                        {
                            ChatRenderOptions renderOptions = MainWindow.pageChatRender.GetOptions(Path.ChangeExtension(chatOptions.Filename.Replace(".gz", ""), '.' + GetRenderFormat().ToLower()));
                            if (renderOptions.OutputFile.Trim() == downloadOptions.Filename.Trim())
                            {
                                //Just in case VOD and chat paths are the same. Like the previous defaults
                                renderOptions.OutputFile = Path.ChangeExtension(chatOptions.Filename.Replace(".gz", ""), " - CHAT." + GetRenderFormat().ToLower());
                            }
                            renderOptions.InputFile = chatOptions.Filename;
                            renderOptions.FileCollisionCallback = HandleFileCollisionCallback;

                            ChatRenderTask renderTask = new ChatRenderTask
                            {
                                DownloadOptions = renderOptions,
                                Info =
                                {
                                    Title = clipDownloadPage.textTitle.Text,
                                    Thumbnail = clipDownloadPage.imgThumbnail.Source
                                },
                                DependantTask = chatTask
                            };
                            renderTask.ChangeStatus(TwitchTaskStatus.Waiting);

                            lock (PageQueue.TaskLock)
                            {
                                PageQueue.TaskList.Add(renderTask);
                            }
                        }
                    }

                    OK = true;
                    Close();
                }

                if (_parentPage is PageChatDownload chatDownloadPage)
                {
                    string folderPath = textFolder.Text;
                    if (!Directory.Exists(folderPath))
                    {
                        try
                        {
                            TwitchHelper.CreateDirectory(folderPath);
                        }
                        catch (Exception ex)
                        {
                            await MessageBoxService.ShowAsync(Translations.Strings.InvalidFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error, this);

                            if (Settings.Default.VerboseErrors)
                            {
                                await MessageBoxService.ShowAsync(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error, this);
                            }

                            return;
                        }
                    }

                    ChatDownloadOptions chatOptions = MainWindow.pageChatDownload.GetOptions(null);
                    chatOptions.Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateChat, chatDownloadPage.textTitle.Text, chatOptions.Id, chatDownloadPage.currentVideoTime, chatDownloadPage.textStreamer.Text,
                        chatDownloadPage.streamerId,
                        chatOptions.TrimBeginning ? TimeSpan.FromSeconds(chatOptions.TrimBeginningTime) : TimeSpan.Zero,
                        chatOptions.TrimEnding ? TimeSpan.FromSeconds(chatOptions.TrimEndingTime) : chatDownloadPage.vodLength,
                        chatDownloadPage.vodLength, chatDownloadPage.viewCount, chatDownloadPage.game) + chatOptions.FileExtension);
                    chatOptions.DelayDownload = checkDelayChat.IsChecked.GetValueOrDefault();
                    chatOptions.FileCollisionCallback = HandleFileCollisionCallback;

                    ChatDownloadTask chatTask = new ChatDownloadTask
                    {
                        DownloadOptions = chatOptions,
                        Info =
                        {
                            Title = chatDownloadPage.textTitle.Text,
                            Thumbnail = chatDownloadPage.imgThumbnail.Source
                        }
                    };

                    lock (PageQueue.TaskLock)
                    {
                        PageQueue.TaskList.Add(chatTask);
                    }

                    if (checkRender.IsChecked.GetValueOrDefault() && chatOptions.DownloadFormat == ChatFormat.Json)
                    {
                        ChatRenderOptions renderOptions = MainWindow.pageChatRender.GetOptions(Path.ChangeExtension(chatOptions.Filename.Replace(".gz", ""), '.' + GetRenderFormat().ToLower()));
                        renderOptions.InputFile = chatOptions.Filename;
                        renderOptions.FileCollisionCallback = HandleFileCollisionCallback;

                        ChatRenderTask renderTask = new ChatRenderTask
                        {
                            DownloadOptions = renderOptions,
                            Info =
                            {
                                Title = chatDownloadPage.textTitle.Text,
                                Thumbnail = chatDownloadPage.imgThumbnail.Source
                            },
                            DependantTask = chatTask
                        };
                        renderTask.ChangeStatus(TwitchTaskStatus.Waiting);

                        lock (PageQueue.TaskLock)
                        {
                            PageQueue.TaskList.Add(renderTask);
                        }
                    }

                    OK = true;
                    Close();
                }

                if (_parentPage is PageChatUpdate chatUpdatePage)
                {
                    string folderPath = textFolder.Text;
                    if (!Directory.Exists(folderPath))
                    {
                        try
                        {
                            TwitchHelper.CreateDirectory(folderPath);
                        }
                        catch (Exception ex)
                        {
                            await MessageBoxService.ShowAsync(Translations.Strings.InvalidFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error, this);

                            if (Settings.Default.VerboseErrors)
                            {
                                await MessageBoxService.ShowAsync(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error, this);
                            }

                            return;
                        }
                    }

                    ChatUpdateOptions chatOptions = MainWindow.pageChatUpdate.GetOptions(null);
                    chatOptions.InputFile = chatUpdatePage.InputFile;
                    chatOptions.OutputFile = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateChat, chatUpdatePage.textTitle.Text, chatUpdatePage.VideoId, chatUpdatePage.VideoCreatedAt, chatUpdatePage.textStreamer.Text,
                        chatUpdatePage.StreamerId,
                        chatOptions.TrimBeginning ? TimeSpan.FromSeconds(chatOptions.TrimBeginningTime) : TimeSpan.Zero,
                        chatOptions.TrimEnding ? TimeSpan.FromSeconds(chatOptions.TrimEndingTime) : chatUpdatePage.VideoLength,
                        chatUpdatePage.VideoLength, chatUpdatePage.ViewCount, chatUpdatePage.Game, chatUpdatePage.ClipperName, chatUpdatePage.ClipperId) + chatOptions.FileExtension);
                    chatOptions.FileCollisionCallback = HandleFileCollisionCallback;

                    ChatUpdateTask chatTask = new ChatUpdateTask
                    {
                        UpdateOptions = chatOptions,
                        Info =
                        {
                            Title = chatUpdatePage.textTitle.Text,
                            Thumbnail = chatUpdatePage.imgThumbnail.Source
                        }
                    };

                    lock (PageQueue.TaskLock)
                    {
                        PageQueue.TaskList.Add(chatTask);
                    }

                    OK = true;
                    Close();
                }

                if (_parentPage is PageChatRender chatRenderPage)
                {
                    string folderPath = textFolder.Text;
                    foreach (string fileName in chatRenderPage.FileNames)
                    {
                        if (!Directory.Exists(folderPath))
                        {
                            try
                            {
                                TwitchHelper.CreateDirectory(folderPath);
                            }
                            catch (Exception ex)
                            {
                                await MessageBoxService.ShowAsync(Translations.Strings.InvalidFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error, this);

                                if (Settings.Default.VerboseErrors)
                                {
                                    await MessageBoxService.ShowAsync(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error, this);
                                }

                                return;
                            }
                        }

                        string fileFormat = GetRenderFormat();
                        string filePath = Path.Combine(folderPath, Path.GetFileNameWithoutExtension(fileName) + "." + fileFormat.ToLower());
                        ChatRenderOptions renderOptions = MainWindow.pageChatRender.GetOptions(filePath);
                        renderOptions.InputFile = fileName;
                        renderOptions.FileCollisionCallback = HandleFileCollisionCallback;

                        ChatRenderTask renderTask = new ChatRenderTask
                        {
                            DownloadOptions = renderOptions,
                            Info =
                            {
                                Title = Path.GetFileNameWithoutExtension(filePath)
                            }
                        };

                        var image = await ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL);
                        if (image is not null)
                        {
                            renderTask.Info.Thumbnail = image;
                        }

                        lock (PageQueue.TaskLock)
                        {
                            PageQueue.TaskList.Add(renderTask);
                        }

                        OK = true;
                        Close();
                    }
                }
            }
            else if (_dataList.Count > 0)
            {
                EnqueueDataList();
            }
        }

        private string GetRenderFormat()
        {
            return MainWindow.pageChatRender.comboFormat.SelectedItem is VideoContainer container
                ? container.Name
                : "MP4";
        }

        private void EnqueueDataList()
        {
            string folderPath = textFolder.Text;
            if (!Directory.Exists(folderPath))
            {
                try
                {
                    TwitchHelper.CreateDirectory(folderPath);
                }
                catch (Exception ex)
                {
                    _ = MessageBoxService.ShowAsync(Translations.Strings.InvalidFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error, this);

                    if (Settings.Default.VerboseErrors)
                    {
                        _ = MessageBoxService.ShowAsync(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error, this);
                    }

                    return;
                }
            }

            foreach (var taskData in _dataList)
            {
                if (checkVideo.IsChecked.GetValueOrDefault())
                {
                    if (taskData.Id.All(char.IsDigit))
                    {
                        VideoDownloadOptions downloadOptions = new VideoDownloadOptions
                        {
                            Oauth = Settings.Default.OAuth,
                            TempFolder = Settings.Default.TempPath,
                            Id = long.Parse(taskData.Id),
                            Quality = (ComboPreferredQuality.SelectedItem as ComboBoxItem)?.Content as string,
                            FfmpegPath = "ffmpeg",
                            TrimBeginning = false,
                            TrimEnding = false,
                            DownloadThreads = Settings.Default.VodDownloadThreads,
                            ThrottleKib = Settings.Default.DownloadThrottleEnabled
                                ? Settings.Default.MaximumBandwidthKib
                                : -1,
                            FileCollisionCallback = HandleFileCollisionCallback,
                            DelayDownload = checkDelay.IsChecked.GetValueOrDefault()
                        };
                        downloadOptions.Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateVod, taskData.Title, taskData.Id, taskData.Time, taskData.StreamerName, taskData.StreamerId,
                            downloadOptions.TrimBeginning ? downloadOptions.TrimBeginningTime : TimeSpan.Zero,
                            downloadOptions.TrimEnding ? downloadOptions.TrimEndingTime : TimeSpan.FromSeconds(taskData.Length),
                            TimeSpan.FromSeconds(taskData.Length), taskData.Views, taskData.Game) + FilenameService.GuessVodFileExtension(downloadOptions.Quality));

                        VodDownloadTask downloadTask = new VodDownloadTask
                        {
                            DownloadOptions = downloadOptions,
                            Info =
                            {
                                Title = taskData.Title,
                                Thumbnail = taskData.Thumbnail
                            }
                        };

                        lock (PageQueue.TaskLock)
                        {
                            PageQueue.TaskList.Add(downloadTask);
                        }
                    }
                    else
                    {
                        ClipDownloadOptions downloadOptions = new ClipDownloadOptions
                        {
                            Id = taskData.Id,
                            Quality = (ComboPreferredQuality.SelectedItem as ComboBoxItem)?.Content as string,
                            Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateClip, taskData.Title, taskData.Id, taskData.Time, taskData.StreamerName, taskData.StreamerId,
                                TimeSpan.Zero, TimeSpan.FromSeconds(taskData.Length), TimeSpan.FromSeconds(taskData.Length), taskData.Views, taskData.Game, taskData.ClipperName, taskData.ClipperId) + ".mp4"),
                            ThrottleKib = Settings.Default.DownloadThrottleEnabled
                                ? Settings.Default.MaximumBandwidthKib
                                : -1,
                            TempFolder = Settings.Default.TempPath,
                            EncodeMetadata = Settings.Default.EncodeClipMetadata,
                            FfmpegPath = "ffmpeg",
                            FileCollisionCallback = HandleFileCollisionCallback,
                        };

                        ClipDownloadTask downloadTask = new ClipDownloadTask
                        {
                            DownloadOptions = downloadOptions,
                            Info =
                            {
                                Title = taskData.Title,
                                Thumbnail = taskData.Thumbnail
                            }
                        };

                        lock (PageQueue.TaskLock)
                        {
                            PageQueue.TaskList.Add(downloadTask);
                        }
                    }
                }

                if (checkChat.IsChecked.GetValueOrDefault())
                {
                    ChatDownloadOptions downloadOptions = new ChatDownloadOptions
                    {
                        EmbedData = checkEmbed.IsChecked.GetValueOrDefault(),
                        BttvEmotes = CheckBttvEmbed.IsChecked.GetValueOrDefault(),
                        FfzEmotes = CheckFfzEmbed.IsChecked.GetValueOrDefault(),
                        StvEmotes = CheckStvEmbed.IsChecked.GetValueOrDefault(),
                        TimeFormat = TimestampFormat.Relative,
                        Id = taskData.Id,
                        TrimBeginning = false,
                        TrimEnding = false,
                        FileCollisionCallback = HandleFileCollisionCallback,
                        DelayDownload = checkDelayChat.IsChecked.GetValueOrDefault()
                    };
                    if (radioJson.IsChecked == true)
                        downloadOptions.DownloadFormat = ChatFormat.Json;
                    else if (radioHTML.IsChecked == true)
                        downloadOptions.DownloadFormat = ChatFormat.Html;
                    else
                        downloadOptions.DownloadFormat = ChatFormat.Text;
                    // TODO: Support non-json chat compression
                    if (RadioCompressionGzip.IsChecked.GetValueOrDefault() && downloadOptions.DownloadFormat == ChatFormat.Json)
                        downloadOptions.Compression = ChatCompression.Gzip;
                    downloadOptions.Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateChat, taskData.Title, taskData.Id, taskData.Time, taskData.StreamerName, taskData.StreamerId,
                        downloadOptions.TrimBeginning ? TimeSpan.FromSeconds(downloadOptions.TrimBeginningTime) : TimeSpan.Zero,
                        downloadOptions.TrimEnding ? TimeSpan.FromSeconds(downloadOptions.TrimEndingTime) : TimeSpan.FromSeconds(taskData.Length),
                        TimeSpan.FromSeconds(taskData.Length), taskData.Views, taskData.Game, taskData.ClipperName, taskData.ClipperId) + downloadOptions.FileExtension);

                    ChatDownloadTask downloadTask = new ChatDownloadTask
                    {
                        DownloadOptions = downloadOptions,
                        Info =
                        {
                            Title = taskData.Title,
                            Thumbnail = taskData.Thumbnail
                        }
                    };

                    lock (PageQueue.TaskLock)
                    {
                        PageQueue.TaskList.Add(downloadTask);
                    }

                    if (checkRender.IsChecked.GetValueOrDefault() && downloadOptions.DownloadFormat == ChatFormat.Json)
                    {
                        ChatRenderOptions renderOptions =
                            MainWindow.pageChatRender.GetOptions(Path.ChangeExtension(downloadOptions.Filename.Replace(".gz", ""), '.' + GetRenderFormat().ToLower()));
                        if (renderOptions.OutputFile.Trim() == downloadOptions.Filename.Trim())
                        {
                            //Just in case VOD and chat paths are the same. Like the previous defaults
                            renderOptions.OutputFile = Path.ChangeExtension(downloadOptions.Filename.Replace(".gz", ""), " - CHAT." + GetRenderFormat().ToLower());
                        }
                        renderOptions.InputFile = downloadOptions.Filename;
                        renderOptions.FileCollisionCallback = HandleFileCollisionCallback;

                        ChatRenderTask renderTask = new ChatRenderTask
                        {
                            DownloadOptions = renderOptions,
                            Info =
                            {
                                Title = taskData.Title,
                                Thumbnail = taskData.Thumbnail
                            },
                            DependantTask = downloadTask
                        };
                        renderTask.ChangeStatus(TwitchTaskStatus.Waiting);

                        lock (PageQueue.TaskLock)
                        {
                            PageQueue.TaskList.Add(renderTask);
                        }
                    }
                }
            }

            OK = true;
            Close();
        }

        private async void btnFolder_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
            {
                return;
            }

            FolderPickerOpenOptions options = new FolderPickerOpenOptions
            {
                Title = "Select Folder",
                AllowMultiple = false
            };

            if (Directory.Exists(textFolder.Text))
            {
                var startLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(textFolder.Text);
                options.SuggestedStartLocation = startLocation;
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            if (folders.Count > 0)
            {
                textFolder.Text = folders[0].Path.LocalPath;
            }
        }

        private void TextFolder_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.QueueFolder = textFolder.Text;
            Settings.Default.Save();
        }

        private static IBrush GetThemeBrush(string resourceKey, IBrush fallback)
        {
            if (Application.Current is { } app && app.Resources.TryGetResource(resourceKey, ThemeVariant.Default, out var value) && value is IBrush brush)
            {
                return brush;
            }

            return fallback;
        }

        private void checkChat_Checked(object sender, RoutedEventArgs e)
        {
            checkRender.IsEnabled = true;
            radioJson.IsEnabled = true;
            radioTxt.IsEnabled = true;
            radioHTML.IsEnabled = true;
            checkEmbed.IsEnabled = true;
            CheckBttvEmbed.IsEnabled = CheckFfzEmbed.IsEnabled = CheckStvEmbed.IsEnabled = checkEmbed.IsChecked.GetValueOrDefault();
            checkDelayChat.IsEnabled = true;
            RadioCompressionNone.IsEnabled = true;
            RadioCompressionGzip.IsEnabled = true;
            try
            {
                var appTextBrush = GetThemeBrush("AppText", Brushes.Black);
                TextDownloadFormat.Foreground = appTextBrush;
                TextCompression.Foreground = appTextBrush;
            }
            catch { /* Ignored */ }
        }

        private void checkChat_Unchecked(object sender, RoutedEventArgs e)
        {
            checkRender.IsEnabled = false;
            checkRender.IsChecked = false;
            radioJson.IsEnabled = false;
            radioTxt.IsEnabled = false;
            radioHTML.IsEnabled = false;
            checkEmbed.IsEnabled = false;
            CheckBttvEmbed.IsEnabled = CheckFfzEmbed.IsEnabled = CheckStvEmbed.IsEnabled = false;
            checkDelayChat.IsEnabled = false;
            RadioCompressionNone.IsEnabled = false;
            RadioCompressionGzip.IsEnabled = false;
            try
            {
                var appTextDisabledBrush = GetThemeBrush("AppTextDisabled", Brushes.Gray);
                TextDownloadFormat.Foreground = appTextDisabledBrush;
                TextCompression.Foreground = appTextDisabledBrush;
            }
            catch { /* Ignored */ }
        }

        private void radioJson_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                checkEmbed.IsEnabled = true;
                CheckBttvEmbed.IsEnabled = CheckFfzEmbed.IsEnabled = CheckStvEmbed.IsEnabled = checkEmbed.IsChecked.GetValueOrDefault();
                checkRender.IsEnabled = true;
                StackChatCompression.IsVisible = true;
            }
        }

        private void radioTxt_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                checkEmbed.IsEnabled = false;
                CheckBttvEmbed.IsEnabled = CheckFfzEmbed.IsEnabled = CheckStvEmbed.IsEnabled = false;
                checkRender.IsEnabled = false;
                StackChatCompression.IsVisible = false;
            }
        }

        private void radioHTML_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                checkEmbed.IsEnabled = true;
                CheckBttvEmbed.IsEnabled = CheckFfzEmbed.IsEnabled = CheckStvEmbed.IsEnabled = checkEmbed.IsChecked.GetValueOrDefault();
                checkRender.IsEnabled = false;
                StackChatCompression.IsVisible = false;
            }
        }

        private void CheckVideo_OnChecked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                ComboPreferredQuality.IsEnabled = checkVideo.IsChecked.GetValueOrDefault();
                checkDelay.IsEnabled = checkVideo.IsChecked.GetValueOrDefault();
                try
                {
                    var newBrush = GetThemeBrush(checkVideo.IsChecked.GetValueOrDefault() ? "AppText" : "AppTextDisabled", Brushes.Gray);
                    TextPreferredQuality.Foreground = newBrush;
                }
                catch { /* Ignored */ }
            }
        }

        private void ComboPreferredQuality_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            if (ComboPreferredQuality.SelectedItem is ComboBoxItem { Content: string preferredQuality })
            {
                Settings.Default.PreferredQuality = preferredQuality;
            }
        }

        private void CheckEmbed_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
                return;

            CheckBttvEmbed.IsEnabled = CheckFfzEmbed.IsEnabled = CheckStvEmbed.IsEnabled = checkEmbed.IsChecked.GetValueOrDefault();
        }
    }
}
