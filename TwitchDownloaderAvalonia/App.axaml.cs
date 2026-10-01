using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderAvalonia.Properties;
using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia
{
    public partial class App : Application
    {
        public static ThemeService ThemeServiceSingleton { get; private set; }
        public static CultureService CultureServiceSingleton { get; private set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            UpgradeSettings();

            Dispatcher.UIThread.UnhandledException += Dispatcher_UnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += CurrentDomain_UnobservedTaskException;

            // Set the working dir to the app dir in case we inherited a different working dir
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);

            CultureServiceSingleton = new CultureService();
            RequestCultureChange();

            ThemeServiceSingleton = new ThemeService(this);

            CoreLicensor.EnsureFilesExist(null);

            var mainWindow = new MainWindow();
            mainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                desktopLifetime.MainWindow = mainWindow;
            }

            RequestAppThemeChange();
            mainWindow.Show();

            base.OnFrameworkInitializationCompleted();
        }

        private static void UpgradeSettings()
        {
            if (Settings.Default.UpgradeRequired)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpgradeRequired = false;
                Settings.Default.Save();
            }
        }

        private void Dispatcher_UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            ShowRecoverableExceptionMessage(e.Exception);
        }

        private void CurrentDomain_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            ShowRecoverableExceptionMessage(e.Exception);
            e.SetObserved();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = (Exception)e.ExceptionObject;

            if (e.IsTerminating)
            {
                MessageBoxService.ShowError(ex.ToString(), Translations.Strings.FatalError);
            }
            else
            {
                ShowRecoverableExceptionMessage(ex);
            }
        }

        private void ShowRecoverableExceptionMessage(Exception exception)
        {
            var message = exception + Environment.NewLine + Environment.NewLine + Environment.NewLine + string.Format(Translations.Strings.FatalErrorMessage, nameof(TwitchDownloaderAvalonia));
            _ = MessageBoxService.ShowAsync(message, Translations.Strings.FatalError, MessageBoxButton.YesNo, MessageBoxImage.Error)
                .ContinueWith(t =>
                {
                    if (t.Result is MessageBoxResult.No)
                    {
                        Dispatcher.UIThread.Post(Shutdown);
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void Shutdown()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                desktopLifetime.Shutdown();
            }
        }

        public static void RequestAppThemeChange(bool forceRepaint = false)
            => ThemeServiceSingleton.ChangeAppTheme(forceRepaint);

        public static void RequestCultureChange()
            => CultureServiceSingleton.SetApplicationCulture(Settings.Default.GuiCulture);
    }
}
