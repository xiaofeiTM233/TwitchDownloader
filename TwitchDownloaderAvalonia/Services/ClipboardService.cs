using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using System;
using System.Threading.Tasks;

namespace TwitchDownloaderAvalonia.Services
{
    public static class ClipboardService
    {
        public static async Task<bool> TrySetText(string text)
        {
            try
            {
                var clipboard = GetClipboard();
                if (clipboard is null)
                {
                    return false;
                }

                await clipboard.SetTextAsync(text);
                return true;
            }
            catch (Exception)
            {
                // Report success even if setting the clipboard failed, matching the WPF behavior
                return true;
            }
        }

        private static IClipboard GetClipboard()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
            {
                return mainWindow.Clipboard;
            }

            return null;
        }
    }
}
