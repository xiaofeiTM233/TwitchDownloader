using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Threading.Tasks;

namespace TwitchDownloaderAvalonia.Services
{
    public enum MessageBoxResult
    {
        None,
        OK,
        Cancel,
        Yes,
        No
    }

    public enum MessageBoxButton
    {
        OK,
        OKCancel,
        YesNo,
        YesNoCancel
    }

    public enum MessageBoxImage
    {
        None,
        Error,
        Information,
        Warning
    }

    public static class MessageBoxService
    {
        public static async Task<MessageBoxResult> ShowAsync(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None, Window owner = null)
        {
            var window = new MessageBoxWindow
            {
                Message = message,
                TitleText = title,
                Buttons = buttons,
                Image = image
            };
            window.Title = title ?? "Twitch Downloader";

            owner ??= GetOwnerWindow();

            if (owner is null)
            {
                window.Show();
                return await window.ResultTask;
            }

            return await window.ShowDialog<MessageBoxResult>(owner);
        }

        public static void ShowInfo(string message, string title)
        {
            _ = ShowAsync(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void ShowError(string message, string title)
        {
            _ = ShowAsync(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static Window GetOwnerWindow()
        {
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
            {
                return mainWindow;
            }

            return null;
        }
    }

    public class MessageBoxWindow : Window
    {
        public const string ErrorGlyphPath = "M 12,2 A 10,10 0 1 0 12,22 A 10,10 0 0 0 12,2 Z M 8.05,6.65 12,10.6 15.95,6.65 17.35,8.05 13.4,12 17.35,15.95 15.95,17.35 12,13.4 8.05,17.35 6.65,15.95 10.6,12 6.65,8.05 Z";
        public const string WarningGlyphPath = "M 12,2 23,21 1,21 Z M 11,9 v 6 h 2 v -6 Z m 0,8 v 2 h 2 v -2 Z";
        public const string InformationGlyphPath = "M 12,2 A 10,10 0 1 0 12,22 A 10,10 0 0 0 12,2 Z M 11,10 h 2 v 8 h -2 Z m 0,-4 v 2 h 2 v -2 Z";

        private TaskCompletionSource<MessageBoxResult> _tcs = new();

        public string Message { get; init; }
        public string TitleText { get; init; }
        public MessageBoxButton Buttons { get; init; }
        public MessageBoxImage Image { get; init; }

        public Task<MessageBoxResult> ResultTask => _tcs.Task;

        public MessageBoxWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;
            ShowInTaskbar = false;
            MinWidth = 420;
            MaxWidth = 700;
            SizeToContent = SizeToContent.WidthAndHeight;
            Title = TitleText ?? "Twitch Downloader";
            this.BindBrush(Window.BackgroundProperty, "AppBackground");
            BuildContent();
        }

        private void Complete(MessageBoxResult result)
        {
            _tcs.TrySetResult(result);
            Close();
        }

        private Control CreateIcon()
        {
            var path = Image switch
            {
                MessageBoxImage.Error => ErrorGlyphPath,
                MessageBoxImage.Warning => WarningGlyphPath,
                MessageBoxImage.Information => InformationGlyphPath,
                _ => null
            };

            if (path is null)
                return null;

            var colorKey = Image == MessageBoxImage.Error ? "#E81123" : Image == MessageBoxImage.Warning ? "#FFB900" : "#0078D4";

            return new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse(path),
                Fill = new SolidColorBrush(Color.Parse(colorKey)),
                Width = 36,
                Height = 36,
                Margin = new Avalonia.Thickness(0, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
        }

        private void BuildContent()
        {
            var icon = CreateIcon();

            var messageText = new TextBlock
            {
                Text = Message,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top
            };
            messageText.BindBrush(TextBlock.ForegroundProperty, "AppText");

            var messagePanel = new StackPanel { Orientation = Orientation.Horizontal };
            if (icon is not null)
            {
                messagePanel.Children.Add(icon);
            }
            messagePanel.Children.Add(messageText);

            var scroll = new ScrollViewer
            {
                MaxHeight = 420,
                Content = messagePanel,
                Margin = new Avalonia.Thickness(24, 24, 24, 8)
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Avalonia.Thickness(24, 8, 24, 24),
                Spacing = 8
            };

            switch (Buttons)
            {
                case MessageBoxButton.OK:
                    buttonPanel.Children.Add(CreateButton("OK", MessageBoxResult.OK));
                    break;
                case MessageBoxButton.OKCancel:
                    buttonPanel.Children.Add(CreateButton("OK", MessageBoxResult.OK));
                    buttonPanel.Children.Add(CreateButton("Cancel", MessageBoxResult.Cancel));
                    break;
                case MessageBoxButton.YesNo:
                    buttonPanel.Children.Add(CreateButton("Yes", MessageBoxResult.Yes));
                    buttonPanel.Children.Add(CreateButton("No", MessageBoxResult.No));
                    break;
                case MessageBoxButton.YesNoCancel:
                    buttonPanel.Children.Add(CreateButton("Yes", MessageBoxResult.Yes));
                    buttonPanel.Children.Add(CreateButton("No", MessageBoxResult.No));
                    buttonPanel.Children.Add(CreateButton("Cancel", MessageBoxResult.Cancel));
                    break;
            }

            var root = new DockPanel();
            DockPanel.SetDock(buttonPanel, Dock.Bottom);
            root.Children.Add(buttonPanel);
            root.Children.Add(scroll);

            Content = root;

            Button CreateButton(string label, MessageBoxResult result)
            {
                var button = new Button
                {
                    Content = label,
                    MinWidth = 96,
                    MinHeight = 32,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Padding = new Avalonia.Thickness(12, 4, 12, 4)
                };
                button.Click += (_, _) => Complete(result);
                return button;
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            _tcs.TrySetResult(MessageBoxResult.None);
            base.OnClosing(e);
        }
    }
}
