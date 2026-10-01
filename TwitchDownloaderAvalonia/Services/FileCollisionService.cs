using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using TwitchDownloaderCore.Services;
using TwitchDownloaderAvalonia.Models;
using TwitchDownloaderAvalonia.Properties;

namespace TwitchDownloaderAvalonia.Services
{
    public static class FileCollisionService
    {
        private static CollisionBehavior? _sessionCollisionBehavior;

        /// <remarks>
        /// Invoked from worker threads as a download FileCollisionCallback. Marshals to the UI thread and blocks until resolved.
        /// </remarks>
        [return: MaybeNull]
        public static FileInfo HandleCollisionCallback(FileInfo fileInfo, Window owner)
        {
            return Dispatcher.UIThread.InvokeAsync(() => HandleCollisionCallbackAsync(fileInfo, owner))
                .GetAwaiter().GetResult();
        }

        private static async Task<FileInfo> HandleCollisionCallbackAsync(FileInfo fileInfo, Window owner)
        {
            var collisionBehavior = _sessionCollisionBehavior ?? (CollisionBehavior)Settings.Default.FileCollisionBehavior;

            if (collisionBehavior is not CollisionBehavior.Prompt)
            {
                return GetResult(fileInfo, collisionBehavior);
            }

            CollisionBehavior result;
            bool rememberChoice;

            var dialog = new FileCollisionWindow(fileInfo);
            if (owner is not null)
            {
                result = await dialog.ShowDialog<CollisionBehavior>(owner);
                rememberChoice = dialog.RememberChoice;
            }
            else
            {
                dialog.Show();
                result = await dialog.ResultTask;
                rememberChoice = dialog.RememberChoice;
            }

            if (rememberChoice)
            {
                _sessionCollisionBehavior = result;
            }

            return GetResult(fileInfo, result);
        }

        [return: MaybeNull]
        private static FileInfo GetResult(FileInfo fileInfo, CollisionBehavior command)
        {
            return command switch
            {
                CollisionBehavior.Overwrite => fileInfo,
                CollisionBehavior.Rename => FilenameService.GetNonCollidingName(fileInfo),
                CollisionBehavior.Cancel => null,
                _ => throw new ArgumentOutOfRangeException(nameof(command), command, null)
            };
        }
    }

    public class FileCollisionWindow : Window
    {
        private TaskCompletionSource<CollisionBehavior> _tcs = new();

        public bool RememberChoice { get; private set; }

        public Task<CollisionBehavior> ResultTask => _tcs.Task;

        public FileCollisionWindow(FileInfo fileInfo)
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;
            ShowInTaskbar = false;
            MinWidth = 460;
            MaxWidth = 700;
            SizeToContent = SizeToContent.WidthAndHeight;
            Title = Translations.Strings.TitleFileAlreadyExists;
            this.BindBrush(Window.BackgroundProperty, "AppBackground");

            var warningGlyph = new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse(MessageBoxWindow.WarningGlyphPath),
                Fill = new SolidColorBrush(Color.Parse("#FFB900")),
                Width = 36,
                Height = 36,
                Margin = new Thickness(0, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Top
            };

            var headerText = new TextBlock
            {
                Text = string.Format(Translations.Strings.FileAlreadyExistsHeader, fileInfo.Name),
                FontWeight = FontWeight.Bold,
                TextWrapping = TextWrapping.Wrap
            };
            headerText.BindBrush(TextBlock.ForegroundProperty, "AppText");

            var pathText = new TextBlock
            {
                Text = fileInfo.FullName,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            };
            pathText.BindBrush(TextBlock.ForegroundProperty, "AppText");

            var openFolderButton = new Button
            {
                Content = Translations.Strings.ContextMenuOpenTaskFolder,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 6, 0, 0)
            };
            openFolderButton.Click += (_, _) => FileService.OpenExplorerForFile(fileInfo);

            var messagePanel = new StackPanel { Orientation = Orientation.Horizontal };
            messagePanel.Children.Add(warningGlyph);

            var textColumn = new StackPanel { Orientation = Orientation.Vertical };
            textColumn.Children.Add(headerText);
            textColumn.Children.Add(pathText);
            textColumn.Children.Add(openFolderButton);
            messagePanel.Children.Add(textColumn);

            var scroll = new ScrollViewer
            {
                MaxHeight = 420,
                Content = messagePanel,
                Margin = new Thickness(24, 24, 24, 8)
            };

            var rememberBox = new CheckBox
            {
                Content = Translations.Strings.FileAlreadyExistsRememberMyChoice,
                Margin = new Thickness(24, 4, 0, 0)
            };
            rememberBox.BindBrush(CheckBox.ForegroundProperty, "AppText");

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(24, 8, 24, 24),
                Spacing = 6
            };

            buttonPanel.Children.Add(CreateCommandButton(Translations.Strings.FileAlreadyExistsOverwrite, Translations.Strings.FileAlreadyExistsOverwriteDescription, CollisionBehavior.Overwrite));
            buttonPanel.Children.Add(CreateCommandButton(Translations.Strings.FileAlreadyExistsRename, Translations.Strings.FileAlreadyExistsRenameDescription, CollisionBehavior.Rename));
            buttonPanel.Children.Add(CreateCommandButton(Translations.Strings.FileAlreadyExistsCancel, Translations.Strings.FileAlreadyExistsCancelDescription, CollisionBehavior.Cancel));

            var root = new DockPanel();
            DockPanel.SetDock(buttonPanel, Dock.Bottom);
            root.Children.Add(buttonPanel);

            var middle = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Children = { scroll, rememberBox }
            };

            root.Children.Add(middle);

            Content = root;

            Closed += (_, _) => _tcs.TrySetResult(CollisionBehavior.Cancel);

            Button CreateCommandButton(string label, string description, CollisionBehavior behavior)
            {
                var button = new Button
                {
                    MinHeight = 48,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(12, 6, 12, 6)
                };

                var stack = new StackPanel { Orientation = Orientation.Vertical };
                var labelBlock = new TextBlock
                {
                    Text = label,
                    FontWeight = FontWeight.Bold
                };
                labelBlock.BindBrush(TextBlock.ForegroundProperty, "AppText");
                var descriptionBlock = new TextBlock
                {
                    Text = description,
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap
                };
                descriptionBlock.BindBrush(TextBlock.ForegroundProperty, "AppTextDisabled");
                stack.Children.Add(labelBlock);
                stack.Children.Add(descriptionBlock);

                button.Content = stack;
                button.Click += (_, _) =>
                {
                    RememberChoice = rememberBox.IsChecked.GetValueOrDefault();
                    _tcs.TrySetResult(behavior);
                    Close();
                };
                return button;
            }
        }
    }
}
