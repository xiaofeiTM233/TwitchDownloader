using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderAvalonia.Properties;
using TwitchDownloaderAvalonia.Services;
using TwitchDownloaderAvalonia.Utils;

namespace TwitchDownloaderAvalonia.TwitchTasks
{
    public abstract class TwitchTask : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public TaskData Info { get; } = new();

        public int Progress
        {
            get;
            protected set => SetField(ref field, value);
        }

        public TwitchTaskStatus Status
        {
            get;
            private set => SetField(ref field, value);
        } = TwitchTaskStatus.Ready;

        public string DisplayStatus
        {
            get;
            protected set => SetField(ref field, value);
        }

        public IImage StatusImage
        {
            get;
            private set => SetField(ref field, value);
        }

        protected CancellationTokenSource TokenSource { get; set; } = new();
        public TwitchTask DependantTask { get; init; }
        public abstract string TaskType { get; }

        public Exception Exception
        {
            get;
            protected set => SetField(ref field, value);
        }

        public abstract string OutputFile { get; }

        public bool CanCancel
        {
            get;
            protected set => SetField(ref field, value);
        }

        public bool CanReinitialize
        {
            get;
            protected set => SetField(ref field, value);
        }

        public void Cancel()
        {
            if (!CanCancel)
                return;

            TokenSource.Cancel();

            ChangeStatus(Status is TwitchTaskStatus.Running ? TwitchTaskStatus.Stopping : TwitchTaskStatus.Canceled);
        }

        public abstract void Reinitialize();

        public abstract bool CanRun();

        public abstract Task RunAsync();

        public void ChangeStatus(TwitchTaskStatus newStatus)
        {
            Status = newStatus;
            DisplayStatus = newStatus.ToString();

            CanCancel = newStatus is not TwitchTaskStatus.Canceled and not TwitchTaskStatus.Failed and not TwitchTaskStatus.Finished and not TwitchTaskStatus.Stopping;

            if (Settings.Default.ReduceMotion)
            {
                StatusImage = null;
            }
            else
            {
                StatusImage = newStatus switch
                {
                    TwitchTaskStatus.Running => LoadAssetImage("Images/ppOverheat.gif"),
                    TwitchTaskStatus.Ready or TwitchTaskStatus.Waiting => LoadAssetImage("Images/ppHop.gif"),
                    TwitchTaskStatus.Stopping => LoadAssetImage("Images/ppStretch.gif"),
                    _ => null
                };
            }
        }

        protected async Task<bool> DelayUntilVideoOffline(long videoId, ITaskLogger logger)
        {
            try
            {
                ChangeStatus(TwitchTaskStatus.Waiting);

                var videoMonitor = new LiveVideoMonitor(videoId, logger);
                while (await videoMonitor.IsVideoRecording(TokenSource.Token))
                {
                    var waitTime = Random.Shared.NextDouble(8, 14);
                    await Task.Delay(TimeSpan.FromSeconds(waitTime), TokenSource.Token);
                }

                var thumbUrl = videoMonitor.LatestVideoResponse.data.video.thumbnailURLs.FirstOrDefault();
                var newThumb = await ThumbnailService.TryGetThumb(thumbUrl);
                if (newThumb is not null)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Info.Thumbnail = newThumb;
                        OnPropertyChanged(nameof(Info));
                    });
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException && TokenSource.IsCancellationRequested)
            {
                return true;
            }
            catch (Exception ex)
            {
                Exception = ex;
                return false;
            }

            return true;
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

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}