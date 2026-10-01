using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TwitchDownloaderCore;
using TwitchDownloaderAvalonia.Properties;
using TwitchDownloaderAvalonia.Services;
using TwitchDownloaderAvalonia.TwitchTasks;

namespace TwitchDownloaderAvalonia
{
    /// <summary>
    /// Interaction logic for PageQueue.axaml
    /// </summary>
    public partial class PageQueue : UserControl
    {
        public static readonly Lock TaskLock = new();
        public static ObservableCollection<TwitchTask> TaskList { get; } = [];

        static PageQueue()
        {
            _ = Task.Run(TaskManagerLoop);
        }

        public PageQueue()
        {
            InitializeComponent();
            queueList.ItemsSource = TaskList;

            numVod.Value = Settings.Default.LimitVod;
            numClip.Value = Settings.Default.LimitClip;
            numChat.Value = Settings.Default.LimitChat;
            numRender.Value = Settings.Default.LimitRender;
        }

        private static async Task TaskManagerLoop()
        {
            while (true)
            {
                int maxVod = Settings.Default.LimitVod;
                int maxClip = Settings.Default.LimitClip;
                int maxChat = Settings.Default.LimitChat;
                int maxRender = Settings.Default.LimitRender;
                int currentVod = 0;
                int currentClip = 0;
                int currentChat = 0;
                int currentRender = 0;

                lock (TaskLock)
                {
                    foreach (var task in TaskList)
                    {
                        if (task.Status is not TwitchTaskStatus.Running)
                            continue;

                        switch (task)
                        {
                            case VodDownloadTask:
                                currentVod++;
                                break;
                            case ClipDownloadTask:
                                currentClip++;
                                break;
                            case ChatDownloadTask:
                                currentChat++;
                                break;
                            case ChatUpdateTask:
                                currentChat++;
                                break;
                            case ChatRenderTask:
                                currentRender++;
                                break;
                        }
                    }

                    foreach (var task in TaskList)
                    {
                        if (!task.CanRun())
                            continue;

                        switch (task)
                        {
                            case VodDownloadTask when currentVod < maxVod:
                                currentVod++;
                                _ = task.RunAsync();
                                break;
                            case ClipDownloadTask when currentClip < maxClip:
                                currentClip++;
                                _ = task.RunAsync();
                                break;
                            case ChatDownloadTask when currentChat < maxChat:
                                currentChat++;
                                _ = task.RunAsync();
                                break;
                            case ChatUpdateTask when currentChat < maxChat:
                                currentChat++;
                                _ = task.RunAsync();
                                break;
                            case ChatRenderTask when currentRender < maxRender:
                                currentRender++;
                                _ = task.RunAsync();
                                break;
                        }
                    }
                }

                await Task.Delay(1000);
            }
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
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            btnDonate.IsVisible = !Settings.Default.HideDonation;
        }

        private void numVod_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.LimitVod = (int)numVod.Value.GetValueOrDefault();
                Settings.Default.Save();
            }
        }

        private void numClip_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.LimitClip = (int)numClip.Value.GetValueOrDefault();
                Settings.Default.Save();
            }
        }

        private void numChat_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.LimitChat = (int)numChat.Value.GetValueOrDefault();
                Settings.Default.Save();
            }
        }

        private void numRender_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.LimitRender = (int)numRender.Value.GetValueOrDefault();
                Settings.Default.Save();
            }
        }

        private async void btnUrlList_Click(object sender, RoutedEventArgs e)
        {
            var window = new WindowUrlList();
            var owner = TopLevel.GetTopLevel(this) as Window;
            await window.ShowDialog(owner);
        }

        private async void btnVods_Click(object sender, RoutedEventArgs e)
        {
            var window = new WindowMassDownload(DownloadType.Video);
            var owner = TopLevel.GetTopLevel(this) as Window;
            await window.ShowDialog(owner);
        }

        private async void btnClips_Click(object sender, RoutedEventArgs e)
        {
            var window = new WindowMassDownload(DownloadType.Clip);
            var owner = TopLevel.GetTopLevel(this) as Window;
            await window.ShowDialog(owner);
        }

        private void BtnCancelTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: TwitchTask task })
            {
                return;
            }

            CancelTask(task);
        }

        private void MenuItemCancelTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: TwitchTask task })
            {
                return;
            }

            CancelTask(task);
        }

        private static void CancelTask(TwitchTask task)
        {
            if (task.CanCancel)
            {
                task.Cancel();
            }
        }

        private void BtnTaskError_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: TwitchTask task })
            {
                return;
            }

            ShowTaskException(task);
        }

        private void MenuItemTaskError_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: TwitchTask task })
            {
                return;
            }

            ShowTaskException(task);
        }

        private static async void ShowTaskException(TwitchTask task)
        {
            var taskException = task.Exception;

            if (taskException is null)
            {
                return;
            }

            var errorMessage = taskException.Message;
            if (Settings.Default.VerboseErrors)
            {
                errorMessage = taskException.ToString();
            }

            await MessageBoxService.ShowAsync(errorMessage, Translations.Strings.MessageBoxTitleError, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void BtnRemoveTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: TwitchTask task })
            {
                return;
            }

            RemoveTask(task);
        }

        private void MenuItemRemoveTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: TwitchTask task })
            {
                return;
            }

            RemoveTask(task);
        }

        private static async void RemoveTask(TwitchTask task)
        {
            if (task.Status is TwitchTaskStatus.Running || (task.Status is TwitchTaskStatus.Waiting && !task.CanCancel))
            {
                await MessageBoxService.ShowAsync(Translations.Strings.CancelTaskBeforeRemoving, Translations.Strings.TaskCouldNotBeRemoved, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            task.Cancel();

            lock (TaskLock)
            {
                if (!TaskList.Remove(task))
                {
                    _ = MessageBoxService.ShowAsync(Translations.Strings.TaskCouldNotBeRemoved, Translations.Strings.UnknownErrorOccurred, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MenuItemOpenTaskFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: TwitchTask task })
            {
                return;
            }

            FileService.OpenExplorerForFile(new FileInfo(task.OutputFile));
        }

        private async void MenuItemCopyTaskPath_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: TwitchTask task })
            {
                return;
            }

            if (!await ClipboardService.TrySetText(task.OutputFile))
            {
                await MessageBoxService.ShowAsync(Translations.Strings.FailedToCopyToClipboard, Translations.Strings.FailedToCopyToClipboard, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRetryTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: TwitchTask task })
            {
                return;
            }

            RetryTask(task);
        }

        private void MenuItemTaskRetry_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: TwitchTask task })
            {
                return;
            }

            RetryTask(task);
        }

        private static void RetryTask(TwitchTask task)
        {
            if (task.CanReinitialize)
            {
                task.Reinitialize();
            }
        }

        private void BtnMoveTaskUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: TwitchTask task })
            {
                return;
            }

            lock (TaskLock)
            {
                var index = TaskList.IndexOf(task);
                if (index < 1)
                    return;

                TaskList.Move(index, index - 1);
            }
        }

        private void BtnMoveTaskDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: TwitchTask task })
            {
                return;
            }

            lock (TaskLock)
            {
                var index = TaskList.IndexOf(task);
                if (index == -1 || index == TaskList.Count - 1)
                    return;

                TaskList.Move(index, index + 1);
            }
        }
    }
}
