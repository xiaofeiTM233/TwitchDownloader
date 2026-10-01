using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace TwitchDownloaderAvalonia.Services
{
    public static class FileService
    {
        public static void OpenExplorerForFile(FileInfo fileInfo)
        {
            var directoryInfo = fileInfo.Directory;
            if (directoryInfo is null || !directoryInfo.Exists)
            {
                return;
            }

            fileInfo.Refresh();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var args = fileInfo.Exists
                    ? $"/select,\"{fileInfo.FullName}\""
                    : $"\"{directoryInfo.FullName}\"";

                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = args,
                    UseShellExecute = false
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"\"{directoryInfo.FullName}\"",
                    UseShellExecute = false
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = $"\"{directoryInfo.FullName}\"",
                    UseShellExecute = false
                });
            }
        }
    }
}
