using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SkiaSharp;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.TwitchObjects;
using TwitchDownloaderAvalonia.Extensions;
using TwitchDownloaderAvalonia.Models;
using TwitchDownloaderAvalonia.Properties;
using TwitchDownloaderAvalonia.Services;
using TwitchDownloaderAvalonia.Utils;

namespace TwitchDownloaderAvalonia
{
    /// <summary>
    /// Interaction logic for PageChatRender.axaml
    /// </summary>
    public partial class PageChatRender : UserControl
    {
        public List<string> ffmpegLog = [];
        public SKFontManager fontManager = SKFontManager.CreateDefault();
        public string[] FileNames = [];
        private CancellationTokenSource _cancellationTokenSource;
        public ObservableCollection<BadgeItem> BadgeItems { get; } = [];

        public PageChatRender()
        {
            InitializeComponent();
            App.CultureServiceSingleton.CultureChanged += OnCultureChanged;
        }

        private void OnCultureChanged(object sender, CultureInfo e)
        {
            if (IsInitialized)
            {
                LoadSettings();
            }
        }

        private async void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
            {
                return;
            }

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open",
                AllowMultiple = true,
                FileTypeFilter = [new FilePickerFileType("JSON Files") { Patterns = ["*.json", "*.json.gz"] }]
            });

            if (files.Count == 0)
            {
                return;
            }

            FileNames = files.Select(f => f.Path.LocalPath).ToArray();
            textJson.Text = string.Join("&&", FileNames);
            UpdateActionButtons(false);
        }

        private void UpdateActionButtons(bool isRendering)
        {
            if (isRendering)
            {
                SplitBtnRender.IsVisible = false;
                BtnEnqueue.IsVisible = false;
                BtnCancel.IsVisible = true;
                return;
            }
            if (FileNames.Length > 1)
            {
                SplitBtnRender.IsVisible = false;
                BtnEnqueue.IsVisible = true;
                BtnCancel.IsVisible = false;
                return;
            }
            SplitBtnRender.IsVisible = true;
            BtnEnqueue.IsVisible = false;
            BtnCancel.IsVisible = false;
        }

        public ChatRenderOptions GetOptions(string filename)
        {
            var bgColor = colorBackground.Color;
            var altBgColor = colorAlternateBackground.Color;
            var fontColor = colorFont.Color;
            var highlightColor = colorHighlightUsers.Color;
            SKColor backgroundColor = new(bgColor.R, bgColor.G, bgColor.B, bgColor.A);
            SKColor altBackgroundColor = new(altBgColor.R, altBgColor.G, altBgColor.B, altBgColor.A);
            SKColor messageColor = new(fontColor.R, fontColor.G, fontColor.B);
            SKColor highlightUsersColor = new(highlightColor.R, highlightColor.G, highlightColor.B, highlightColor.A);
            ChatRenderOptions options = new()
            {
                OutputFile = filename,
                InputFile = textJson.Text,
                BackgroundColor = backgroundColor,
                AlternateBackgroundColor = altBackgroundColor,
                AlternateMessageBackgrounds = checkAlternateMessageBackgrounds.IsChecked.GetValueOrDefault(),
                ChatHeight = int.Parse(textHeight.Text),
                ChatWidth = int.Parse(textWidth.Text),
                BttvEmotes = checkBTTV.IsChecked.GetValueOrDefault(),
                FfzEmotes = checkFFZ.IsChecked.GetValueOrDefault(),
                StvEmotes = checkSTV.IsChecked.GetValueOrDefault(),
                Outline = checkOutline.IsChecked.GetValueOrDefault(),
                Font = (string)comboFont.SelectedItem,
                FontSize = (float)numFontSize.Value.GetValueOrDefault(),
                UpdateRate = double.Parse(textUpdateTime.Text, CultureInfo.CurrentCulture),
                EmoteScale = double.Parse(textEmoteScale.Text, CultureInfo.CurrentCulture),
                BadgeScale = double.Parse(textBadgeScale.Text, CultureInfo.CurrentCulture),
                EmojiScale = double.Parse(textEmojiScale.Text, CultureInfo.CurrentCulture),
                AvatarScale = double.Parse(textAvatarScale.Text, CultureInfo.CurrentCulture),
                SidePaddingScale = double.Parse(textSidePaddingScale.Text, CultureInfo.CurrentCulture),
                SectionHeightScale = double.Parse(textSectionHeightScale.Text, CultureInfo.CurrentCulture),
                WordSpacingScale = double.Parse(textWordSpaceScale.Text, CultureInfo.CurrentCulture),
                EmoteSpacingScale = double.Parse(textEmoteSpaceScale.Text, CultureInfo.CurrentCulture),
                AccentIndentScale = double.Parse(textAccentIndentScale.Text, CultureInfo.CurrentCulture),
                AccentStrokeScale = double.Parse(textAccentStrokeScale.Text, CultureInfo.CurrentCulture),
                VerticalSpacingScale = double.Parse(textVerticalScale.Text, CultureInfo.CurrentCulture),
                UsernameFontScale = double.Parse(textUsernameScale.Text, CultureInfo.CurrentCulture),
                HighlightUserColor = highlightUsersColor,
                HighlightUsersArray = textHighlightUsersList.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                IgnoreUsersArray = textIgnoreUsersList.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                BannedWordsArray = textBannedWordsList.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                Timestamp = checkTimestamp.IsChecked.GetValueOrDefault(),
                MessageColor = messageColor,
                Framerate = int.Parse(textFramerate.Text),
                InputArgs = CheckRenderSharpening.IsChecked == true ? textFfmpegInput.Text + " -filter_complex \"smartblur=lr=1:ls=-1.0\"" : textFfmpegInput.Text,
                OutputArgs = textFfmpegOutput.Text,
                MessageFontStyle = SKFontStyle.Normal,
                UsernameFontStyle = SKFontStyle.Bold,
                GenerateMask = checkMask.IsChecked.GetValueOrDefault(),
                OutlineSize = 4 * double.Parse(textOutlineScale.Text, CultureInfo.CurrentCulture),
                FfmpegPath = "ffmpeg",
                TempFolder = Settings.Default.TempPath,
                SubMessages = checkSub.IsChecked.GetValueOrDefault(),
                ChatBadges = checkBadge.IsChecked.GetValueOrDefault(),
                Offline = checkOffline.IsChecked.GetValueOrDefault(),
                RenderUserAvatars = checkRenderAvatars.IsChecked.GetValueOrDefault(),
                AllowUnlistedEmotes = true,
                DisperseCommentOffsets = checkDispersion.IsChecked.GetValueOrDefault(),
                AdjustUsernameVisibility = checkAdjustUsernameVisibility.IsChecked.GetValueOrDefault(),
            };
            if (RadioEmojiNotoColor.IsChecked == true)
                options.EmojiVendor = EmojiVendor.GoogleNotoColor;
            else if (RadioEmojiTwemoji.IsChecked == true)
                options.EmojiVendor = EmojiVendor.TwitterTwemoji;
            else if (RadioEmojiNone.IsChecked == true)
                options.EmojiVendor = EmojiVendor.None;
            foreach (var item in BadgeItems)
            {
                if (item.IsSelected)
                {
                    options.ChatBadgeMask += (int)item.Value;
                }
            }

            return options;
        }

        private void LoadSettings()
        {
            try
            {
                comboFont.SelectedItem = Settings.Default.Font;
                checkOutline.IsChecked = Settings.Default.Outline;
                checkTimestamp.IsChecked = Settings.Default.Timestamp;
                colorBackground.Color = Avalonia.Media.Color.FromArgb(Settings.Default.BackgroundColorA, Settings.Default.BackgroundColorR, Settings.Default.BackgroundColorG, Settings.Default.BackgroundColorB);
                colorAlternateBackground.Color = Avalonia.Media.Color.FromArgb(Settings.Default.AlternateBackgroundColorA, Settings.Default.AlternateBackgroundColorR, Settings.Default.AlternateBackgroundColorG, Settings.Default.AlternateBackgroundColorB);
                checkFFZ.IsChecked = Settings.Default.FFZEmotes;
                checkBTTV.IsChecked = Settings.Default.BTTVEmotes;
                checkSTV.IsChecked = Settings.Default.STVEmotes;
                textHeight.Text = Settings.Default.Height.ToString();
                textWidth.Text = Settings.Default.Width.ToString();
                numFontSize.Value = (decimal)Settings.Default.FontSize;
                textUpdateTime.Text = Settings.Default.UpdateTime.ToString("0.0#");
                colorFont.Color = Avalonia.Media.Color.FromRgb(Settings.Default.FontColorR, Settings.Default.FontColorG, Settings.Default.FontColorB);
                textFramerate.Text = Settings.Default.Framerate.ToString();
                checkMask.IsChecked = Settings.Default.GenerateMask;
                CheckRenderSharpening.IsChecked = Settings.Default.ChatRenderSharpening;
                checkSub.IsChecked = Settings.Default.SubMessages;
                checkBadge.IsChecked = Settings.Default.ChatBadges;
                textEmoteScale.Text = Settings.Default.EmoteScale.ToString("0.0#");
                textEmojiScale.Text = Settings.Default.EmojiScale.ToString("0.0#");
                textBadgeScale.Text = Settings.Default.BadgeScale.ToString("0.0#");
                textAvatarScale.Text = Settings.Default.AvatarScale.ToString("0.0#");
                textVerticalScale.Text = Settings.Default.VerticalSpacingScale.ToString("0.0#");
                textUsernameScale.Text = Settings.Default.UsernameFontScale.ToString("0.0#");
                textSidePaddingScale.Text = Settings.Default.LeftSpacingScale.ToString("0.0#");
                textSectionHeightScale.Text = Settings.Default.SectionHeightScale.ToString("0.0#");
                textWordSpaceScale.Text = Settings.Default.WordSpacingScale.ToString("0.0#");
                textEmoteSpaceScale.Text = Settings.Default.EmoteSpacingScale.ToString("0.0#");
                textAccentStrokeScale.Text = Settings.Default.AccentStrokeScale.ToString("0.0#");
                textAccentIndentScale.Text = Settings.Default.AccentIndentScale.ToString("0.0#");
                textOutlineScale.Text = Settings.Default.OutlineScale.ToString("0.0#");
                textIgnoreUsersList.Text = Settings.Default.IgnoreUsersList;
                textHighlightUsersList.Text = Settings.Default.HighlightUsersList;
                colorHighlightUsers.Color = Avalonia.Media.Color.FromArgb(Settings.Default.HighlightUsersColorA, Settings.Default.HighlightUsersColorR, Settings.Default.HighlightUsersColorG, Settings.Default.HighlightUsersColorB);
                textBannedWordsList.Text = Settings.Default.BannedWordsList;
                checkOffline.IsChecked = Settings.Default.Offline;
                checkRenderAvatars.IsChecked = Settings.Default.RenderUserAvatars;
                checkDispersion.IsChecked = Settings.Default.DisperseCommentOffsets;
                checkAlternateMessageBackgrounds.IsChecked = Settings.Default.AlternateMessageBackgrounds;
                checkAdjustUsernameVisibility.IsChecked = Settings.Default.AdjustUsernameVisibility;
                RadioEmojiNotoColor.IsChecked = (EmojiVendor)Settings.Default.RenderEmojiVendor == EmojiVendor.GoogleNotoColor;
                RadioEmojiTwemoji.IsChecked = (EmojiVendor)Settings.Default.RenderEmojiVendor == EmojiVendor.TwitterTwemoji;
                RadioEmojiNone.IsChecked = (EmojiVendor)Settings.Default.RenderEmojiVendor == EmojiVendor.None;

                var badgeMask = (ChatBadgeType)Settings.Default.ChatBadgeMask;
                foreach (var item in BadgeItems)
                {
                    item.IsSelected = badgeMask.HasFlag(item.Value);
                }
                UpdateBadgeSummary();

                foreach (VideoContainer container in comboFormat.Items)
                {
                    if (container.Name == Settings.Default.VideoContainer)
                    {
                        comboFormat.SelectedItem = container;

                        comboCodec.Items.Clear();
                        foreach (Codec codec in container.SupportedCodecs)
                        {
                            comboCodec.Items.Add(codec);
                            if (codec.Name == Settings.Default.VideoCodec)
                            {
                                comboCodec.SelectedItem = codec;
                            }
                        }

                        break;
                    }
                }

                LoadFfmpegArgs();
            }
            catch { }
        }

        private void ComboCodec_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboCodec.SelectedItem != null)
            {
                LoadFfmpegArgs();
            }
        }

        private void ComboFormat_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            VideoContainer currentContainer = (VideoContainer)comboFormat.SelectedItem;
            comboCodec.Items.Clear();
            foreach (Codec codec in currentContainer.SupportedCodecs)
            {
                comboCodec.Items.Add(codec);
                if (Settings.Default.VideoCodec == codec.Name)
                {
                    comboCodec.SelectedItem = codec;
                }
            }

            if (comboCodec.SelectedItem == null)
            {
                comboCodec.SelectedIndex = 0;
            }
        }

        public void SaveSettings()
        {
            Settings.Default.Font = comboFont.SelectedItem?.ToString() ?? "Inter Embedded";
            Settings.Default.Outline = checkOutline.IsChecked.GetValueOrDefault();
            Settings.Default.Timestamp = checkTimestamp.IsChecked.GetValueOrDefault();
            Settings.Default.BackgroundColorR = colorBackground.Color.R;
            Settings.Default.BackgroundColorG = colorBackground.Color.G;
            Settings.Default.BackgroundColorB = colorBackground.Color.B;
            Settings.Default.BackgroundColorA = colorBackground.Color.A;
            Settings.Default.AlternateBackgroundColorR = colorAlternateBackground.Color.R;
            Settings.Default.AlternateBackgroundColorG = colorAlternateBackground.Color.G;
            Settings.Default.AlternateBackgroundColorB = colorAlternateBackground.Color.B;
            Settings.Default.AlternateBackgroundColorA = colorAlternateBackground.Color.A;
            Settings.Default.FFZEmotes = checkFFZ.IsChecked.GetValueOrDefault();
            Settings.Default.BTTVEmotes = checkBTTV.IsChecked.GetValueOrDefault();
            Settings.Default.STVEmotes = checkSTV.IsChecked.GetValueOrDefault();
            Settings.Default.FontColorR = colorFont.Color.R;
            Settings.Default.FontColorG = colorFont.Color.G;
            Settings.Default.FontColorB = colorFont.Color.B;
            Settings.Default.GenerateMask = checkMask.IsChecked.GetValueOrDefault();
            Settings.Default.ChatRenderSharpening = CheckRenderSharpening.IsChecked.GetValueOrDefault();
            Settings.Default.SubMessages = checkSub.IsChecked.GetValueOrDefault();
            Settings.Default.ChatBadges = checkBadge.IsChecked.GetValueOrDefault();
            Settings.Default.Offline = checkOffline.IsChecked.GetValueOrDefault();
            Settings.Default.RenderUserAvatars = checkRenderAvatars.IsChecked.GetValueOrDefault();
            Settings.Default.DisperseCommentOffsets = checkDispersion.IsChecked.GetValueOrDefault();
            Settings.Default.AlternateMessageBackgrounds = checkAlternateMessageBackgrounds.IsChecked.GetValueOrDefault();
            Settings.Default.AdjustUsernameVisibility = checkAdjustUsernameVisibility.IsChecked.GetValueOrDefault();
            if (comboFormat.SelectedItem != null)
            {
                Settings.Default.VideoContainer = ((VideoContainer)comboFormat.SelectedItem).Name;
            }
            if (comboCodec.SelectedItem != null)
            {
                Settings.Default.VideoCodec = ((Codec)comboCodec.SelectedItem).Name;
            }
            Settings.Default.IgnoreUsersList = string.Join(",", textIgnoreUsersList.Text
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
            Settings.Default.HighlightUsersList = string.Join(",", textHighlightUsersList.Text
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
            Settings.Default.HighlightUsersColorR = colorHighlightUsers.Color.R;
            Settings.Default.HighlightUsersColorG = colorHighlightUsers.Color.G;
            Settings.Default.HighlightUsersColorB = colorHighlightUsers.Color.B;
            Settings.Default.HighlightUsersColorA = colorHighlightUsers.Color.A;
            Settings.Default.BannedWordsList = string.Join(",", textBannedWordsList.Text
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
            if (RadioEmojiNotoColor.IsChecked == true)
                Settings.Default.RenderEmojiVendor = (int)EmojiVendor.GoogleNotoColor;
            else if (RadioEmojiTwemoji.IsChecked == true)
                Settings.Default.RenderEmojiVendor = (int)EmojiVendor.TwitterTwemoji;
            else if (RadioEmojiNone.IsChecked == true)
                Settings.Default.RenderEmojiVendor = (int)EmojiVendor.None;
            int newMask = 0;
            foreach (var item in BadgeItems)
            {
                if (item.IsSelected)
                {
                    newMask += (int)item.Value;
                }
            }
            Settings.Default.ChatBadgeMask = newMask;

            try
            {
                Settings.Default.Height = int.Parse(textHeight.Text);
                Settings.Default.Width = int.Parse(textWidth.Text);
                Settings.Default.FontSize = (float)numFontSize.Value.GetValueOrDefault();
                Settings.Default.UpdateTime = double.Parse(textUpdateTime.Text, CultureInfo.CurrentCulture);
                Settings.Default.Framerate = int.Parse(textFramerate.Text);
                Settings.Default.EmoteScale = double.Parse(textEmoteScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.EmojiScale = double.Parse(textEmojiScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.BadgeScale = double.Parse(textBadgeScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.AvatarScale = double.Parse(textAvatarScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.VerticalSpacingScale = double.Parse(textVerticalScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.UsernameFontScale = double.Parse(textUsernameScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.LeftSpacingScale = double.Parse(textSidePaddingScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.SectionHeightScale = double.Parse(textSectionHeightScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.WordSpacingScale = double.Parse(textWordSpaceScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.EmoteSpacingScale = double.Parse(textEmoteSpaceScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.AccentStrokeScale = double.Parse(textAccentStrokeScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.AccentIndentScale = double.Parse(textAccentIndentScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.OutlineScale = double.Parse(textOutlineScale.Text, CultureInfo.CurrentCulture);
            }
            catch { }
            Settings.Default.Save();
        }

        private bool ValidateInputs()
        {
            if (FileNames.Length == 0)
            {
                AppendLog(Translations.Strings.ErrorLog + Translations.Strings.NoJsonFilesSelected);
                return false;
            }
            foreach (string fileName in FileNames)
            {
                if (!File.Exists(fileName))
                {
                    AppendLog(Translations.Strings.ErrorLog + Translations.Strings.FileNotFound + Path.GetFileName(fileName));
                    return false;
                }
            }

            try
            {
                _ = int.Parse(textHeight.Text);
                _ = int.Parse(textWidth.Text);
                _ = double.Parse(textUpdateTime.Text, CultureInfo.CurrentCulture);
                _ = int.Parse(textFramerate.Text);
                _ = double.Parse(textEmoteScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textBadgeScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textEmojiScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textAvatarScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textVerticalScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textUsernameScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textSidePaddingScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textSectionHeightScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textWordSpaceScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textEmoteSpaceScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textAccentStrokeScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textAccentIndentScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textOutlineScale.Text, CultureInfo.CurrentCulture);
            }
            catch (Exception ex)
            {
                AppendLog(Translations.Strings.ErrorLog + ex.Message);
                return false;
            }

            if (checkMask.IsChecked == false && (colorBackground.Color.A < 255 || (checkAlternateMessageBackgrounds.IsChecked.GetValueOrDefault() && colorAlternateBackground.Color.A < 255)))
            {
                if (((VideoContainer)comboFormat.SelectedItem).Name is not "MOV" and not "WEBM" ||
                    ((Codec)comboCodec.SelectedItem).Name is not "RLE" and not "ProRes" and not "VP8" and not "VP9")
                {
                    AppendLog(Translations.Strings.ErrorLog + Translations.Strings.AlphaNotSupportedByCodec);
                    return false;
                }
            }

            if (checkMask.IsChecked == true && colorBackground.Color.A == 255 && !(checkAlternateMessageBackgrounds.IsChecked.GetValueOrDefault() && colorAlternateBackground.Color.A != 255))
            {
                AppendLog(Translations.Strings.ErrorLog + Translations.Strings.MaskWithNoAlpha);
                return false;
            }

            if (int.Parse(textHeight.Text) % 2 != 0 || int.Parse(textWidth.Text) % 2 != 0)
            {
                AppendLog(Translations.Strings.ErrorLog + Translations.Strings.RenderWidthHeightMustBeEven);
                return false;
            }

            return true;
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

        public void SetImage(string imageUri, bool isGif)
        {
            var assetPath = imageUri.Replace("Images/", "Assets/", StringComparison.Ordinal);
            var uri = new Uri($"avares://TwitchDownloaderAvalonia/{assetPath}");

            using var stream = AssetLoader.Open(uri);
            var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);

            Dispatcher.UIThread.Post(() =>
            {
                statusImage.Source = bitmap;
                statusImage.InvalidateVisual();
            });
        }

        private void Page_Initialized(object sender, EventArgs e)
        {
            var fonts = fontManager.FontFamilies.ToList();
            fonts.Add("Inter Embedded");
            fonts.Sort();
            foreach (var font in fonts)
            {
                comboFont.Items.Add(font);
            }
            comboFont.SelectedItem = "Inter Embedded";

            Codec h264Codec = new Codec() { Name = "H264", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", OutputArgs = "-c:v libx264 -preset:v veryfast -crf 18 -pix_fmt yuv420p \"{save_path}\"" };
            Codec h264NvencCodec = new Codec() { Name = "H264 NVIDIA", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", OutputArgs = "-c:v h264_nvenc -preset:v p4 -cq 20 -pix_fmt yuv420p \"{save_path}\"" };
            Codec h264AmfCodec = new Codec() { Name = "H264 AMD", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", OutputArgs = "-c:v h264_amf -preset:v p4 -cq 20 -pix_fmt yuv420p \"{save_path}\"" };
            Codec h265Codec = new Codec() { Name = "H265", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", OutputArgs = "-c:v libx265 -preset:v veryfast -crf 18 -pix_fmt yuv420p \"{save_path}\"" };
            Codec h265NvencCodec = new Codec() { Name = "H265 NVIDIA", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", OutputArgs = "-c:v hevc_nvenc -preset:v p4 -cq 21 -pix_fmt yuv420p \"{save_path}\"" };
            Codec h265AmfCodec = new Codec() { Name = "H265 AMD", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", OutputArgs = "-c:v hevc_amf -preset:v p4 -cq 21 -pix_fmt yuv420p \"{save_path}\"" };
            Codec vp8Codec = new Codec() { Name = "VP8", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", OutputArgs = "-c:v libvpx -crf 18 -b:v 2M -pix_fmt yuva420p -auto-alt-ref 0 \"{save_path}\"" };
            Codec vp9Codec = new Codec() { Name = "VP9", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", OutputArgs = "-c:v libvpx-vp9 -crf 18 -b:v 2M -deadline realtime -quality realtime -speed 3 -pix_fmt yuva420p \"{save_path}\"" };
            Codec rleCodec = new Codec() { Name = "RLE", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", OutputArgs = "-c:v qtrle -pix_fmt argb \"{save_path}\"" };
            Codec proresCodec = new Codec() { Name = "ProRes", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", OutputArgs = "-c:v prores_ks -qscale:v 62 -pix_fmt argb \"{save_path}\"" };
            VideoContainer mp4Container = new VideoContainer() { Name = "MP4", SupportedCodecs = [h264Codec, h265Codec, h264NvencCodec, h265NvencCodec, h264AmfCodec, h265AmfCodec] };
            VideoContainer movContainer = new VideoContainer() { Name = "MOV", SupportedCodecs = [h264Codec, h265Codec, rleCodec, proresCodec, h264NvencCodec, h265NvencCodec, h264AmfCodec, h265AmfCodec] };
            VideoContainer webmContainer = new VideoContainer() { Name = "WEBM", SupportedCodecs = [vp8Codec, vp9Codec] };
            VideoContainer mkvContainer = new VideoContainer() { Name = "MKV", SupportedCodecs = [h264Codec, h265Codec, vp8Codec, vp9Codec, h264NvencCodec, h265NvencCodec, h264AmfCodec, h265AmfCodec] };
            comboFormat.Items.Add(mp4Container);
            comboFormat.Items.Add(movContainer);
            comboFormat.Items.Add(webmContainer);
            comboFormat.Items.Add(mkvContainer);

            BadgeItems.Add(new BadgeItem { Name = Translations.Strings.BadgeMaskBroadcaster, Value = ChatBadgeType.Broadcaster });
            BadgeItems.Add(new BadgeItem { Name = Translations.Strings.BadgeMaskModerator, Value = ChatBadgeType.Moderator });
            BadgeItems.Add(new BadgeItem { Name = Translations.Strings.BadgeMaskVIP, Value = ChatBadgeType.VIP });
            BadgeItems.Add(new BadgeItem { Name = Translations.Strings.BadgeMaskSubscriber, Value = ChatBadgeType.Subscriber });
            BadgeItems.Add(new BadgeItem { Name = Translations.Strings.BadgeMaskPredictions, Value = ChatBadgeType.Predictions });
            BadgeItems.Add(new BadgeItem { Name = Translations.Strings.BadgeMaskNoAudioNoVideo, Value = ChatBadgeType.NoAudioVisual });
            BadgeItems.Add(new BadgeItem { Name = Translations.Strings.BadgeMaskTwitchPrime, Value = ChatBadgeType.PrimeGaming });
            BadgeItems.Add(new BadgeItem { Name = Translations.Strings.BadgeMaskOthers, Value = ChatBadgeType.Other });
            badgeItemsControl.ItemsSource = BadgeItems;
            UpdateBadgeSummary();

            LoadSettings();
        }

        private void UpdateBadgeSummary()
        {
            var selected = BadgeItems.Where(b => b.IsSelected).Select(b => b.Name).ToList();
            badgeSummary.Text = selected.Count > 0
                ? string.Join(", ", selected)
                : Translations.Strings.ChatBadgeFilter;
        }

        private void BadgeItem_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (IsInitialized)
            {
                UpdateBadgeSummary();
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void btnDonate_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.buymeacoffee.com/lay295") { UseShellExecute = true });
        }

        private void btnFfmpegLink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://ffmpeg.org/ffmpeg.html") { UseShellExecute = true });
        }

        private async void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            var settings = new WindowSettings();
            var owner = TopLevel.GetTopLevel(this) as Window;
            await settings.ShowDialog(owner);

            btnDonate.IsVisible = !Settings.Default.HideDonation;
            statusImage.IsVisible = !Settings.Default.ReduceMotion;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            btnDonate.IsVisible = !Settings.Default.HideDonation;
            statusImage.IsVisible = !Settings.Default.ReduceMotion;
        }

        private void btnResetFfmpeg_Click(object sender, RoutedEventArgs e)
        {
            textFfmpegInput.Text = ((Codec)comboCodec.SelectedItem).InputArgs;
            textFfmpegOutput.Text = ((Codec)comboCodec.SelectedItem).OutputArgs;

            SaveArguments();
        }

        private void SaveArguments()
        {
            List<CustomFfmpegArgs> args = JsonSerializer.Deserialize<List<CustomFfmpegArgs>>(Settings.Default.FfmpegArguments);

            bool foundArg = false;
            foreach (CustomFfmpegArgs arg in args)
            {
                if (arg.CodecName == ((Codec)comboCodec.SelectedItem).Name && arg.ContainerName == ((VideoContainer)comboFormat.SelectedItem).Name)
                {
                    arg.InputArgs = textFfmpegInput.Text;
                    arg.OutputArgs = textFfmpegOutput.Text;
                    foundArg = true;
                    break;
                }
            }

            //Didn't find pre-existing save, make a new one
            if (!foundArg)
            {
                CustomFfmpegArgs newArgs = new CustomFfmpegArgs() { CodecName = ((Codec)comboCodec.SelectedItem).Name, ContainerName = ((VideoContainer)comboFormat.SelectedItem).Name, InputArgs = textFfmpegInput.Text, OutputArgs = textFfmpegOutput.Text };
                args.Add(newArgs);
            }

            Settings.Default.FfmpegArguments = JsonSerializer.Serialize(args);
            Settings.Default.Save();
        }

        private void LoadFfmpegArgs()
        {
            List<CustomFfmpegArgs> args = JsonSerializer.Deserialize<List<CustomFfmpegArgs>>(Settings.Default.FfmpegArguments);

            bool foundArg = false;
            foreach (CustomFfmpegArgs arg in args)
            {
                if (arg.CodecName == ((Codec)comboCodec.SelectedItem).Name && arg.ContainerName == ((VideoContainer)comboFormat.SelectedItem).Name)
                {
                    textFfmpegInput.Text = arg.InputArgs;
                    textFfmpegOutput.Text = arg.OutputArgs;
                    foundArg = true;
                    break;
                }
            }

            if (!foundArg)
            {
                textFfmpegInput.Text = ((Codec)comboCodec.SelectedItem).InputArgs;
                textFfmpegOutput.Text = ((Codec)comboCodec.SelectedItem).OutputArgs;
            }
        }

        private void textFfmpegInput_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveArguments();
        }

        private void textFfmpegOutput_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveArguments();
        }

        private async void SplitBtnRender_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs())
            {
                await ShowUnableToParse();
                return;
            }

            await DoRenderAsync(partialRender: false);
        }

        private async void MenuItemPartialRender_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs())
            {
                await ShowUnableToParse();
                return;
            }

            await DoRenderAsync(partialRender: true);
        }

        private async Task ShowUnableToParse()
        {
            await MessageBoxService.ShowAsync(Translations.Strings.UnableToParseInputsMessage, Translations.Strings.UnableToParseInputs, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async Task DoRenderAsync(bool partialRender)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
            {
                return;
            }

            string fileFormat = ((VideoContainer)comboFormat.SelectedItem).Name;
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save",
                SuggestedFileName = Path.GetFileNameWithoutExtension(textJson.Text.Replace(".gz", "")) + "." + fileFormat.ToLower(),
                FileTypeChoices = [new FilePickerFileType($"{fileFormat} Files") { Patterns = [$"*.{fileFormat.ToLower()}"] }]
            });
            if (file is null)
            {
                return;
            }

            SaveSettings();

            ChatRenderOptions options = GetOptions(file.Path.LocalPath);

            var renderProgress = new TaskProgress((LogLevel)Settings.Default.LogLevels, SetPercent, SetStatus, AppendLog, s => ffmpegLog.Add(s));
            ChatRenderer currentRender = new ChatRenderer(options, renderProgress);
            try
            {
                await currentRender.ParseJsonAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                AppendLog(Translations.Strings.ErrorLog + ex.Message);
                if (Settings.Default.VerboseErrors)
                {
                    await MessageBoxService.ShowAsync(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }

            if (partialRender)
            {
                var window = new WindowRangeSelect(currentRender);
                var owner = TopLevel.GetTopLevel(this) as Window;
                await window.ShowDialog(owner);

                if (window.OK)
                {
                    options.StartOverride = window.startSeconds;
                    options.EndOverride = window.endSeconds;
                }
                else
                {
                    if (window.Invalid)
                    {
                        AppendLog(Translations.Strings.ErrorLog + Translations.Strings.InvalidStartEndTime);
                    }
                    return;
                }
            }

            SetImage("Images/ppOverheat.gif", true);
            statusMessage.Text = Translations.Strings.StatusRendering;
            ffmpegLog.Clear();
            _cancellationTokenSource = new CancellationTokenSource();
            UpdateActionButtons(true);
            try
            {
                await currentRender.RenderVideoAsync(_cancellationTokenSource.Token);
                renderProgress.SetStatus(Translations.Strings.StatusDone);
                SetImage("Images/ppHop.gif", true);
            }
            catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException && _cancellationTokenSource.IsCancellationRequested)
            {
                renderProgress.SetStatus(Translations.Strings.StatusCanceled);
                SetImage("Images/ppHop.gif", true);
            }
            catch (Exception ex)
            {
                renderProgress.SetStatus(Translations.Strings.StatusError);
                SetImage("Images/peepoSad.png", false);
                AppendLog(Translations.Strings.ErrorLog + ex.Message);
                if (Settings.Default.VerboseErrors)
                {
                    if (ex.Message.Contains("The pipe has been ended"))
                    {
                        string errorLog = String.Join('\n', ffmpegLog.TakeLast(20).ToArray());
                        await MessageBoxService.ShowAsync(errorLog, Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                        await MessageBoxService.ShowAsync(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            renderProgress.ReportProgress(0);
            _cancellationTokenSource.Dispose();
            UpdateActionButtons(false);

            currentRender.Dispose();
            GC.Collect();
        }

        private void BtnEnqueue_Click(object sender, RoutedEventArgs e)
        {
            EnqueueRender();
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
            EnqueueRender();
        }

        private void EnqueueRender()
        {
            var queueOptions = new WindowQueueOptions(this);
            var owner = TopLevel.GetTopLevel(this) as Window;
            _ = queueOptions.ShowDialog(owner);
        }

        private void TextJson_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            FileNames = textJson.Text.Split("&&", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            UpdateActionButtons(false);
        }

        private void FfmpegParameter_Click(object sender, PointerPressedEventArgs e)
        {
            if (!IsInitialized || sender is not TextBlock { Text: var parameter })
                return;

            var topLevel = TopLevel.GetTopLevel(this);
            var focusedElement = topLevel?.FocusManager?.GetFocusedElement();
            var textBox = GetFfmpegTemplateTextBox(focusedElement);

            if (textBox is null)
                return;

            if (textBox.TryInsertAtCaret(parameter))
            {
                e.Handled = true;
            }
        }

        private TextBox GetFfmpegTemplateTextBox(IInputElement inputElement)
        {
            if (ReferenceEquals(inputElement, textFfmpegInput))
                return textFfmpegInput;

            if (ReferenceEquals(inputElement, textFfmpegOutput))
                return textFfmpegOutput;

            return null;
        }
    }

    public class BadgeItem : INotifyPropertyChanged
    {
        public string Name { get; init; }
        public ChatBadgeType Value { get; init; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class VideoContainer
    {
        public string Name;
        public List<Codec> SupportedCodecs;

        public override string ToString()
        {
            return Name;
        }
    }

    public class Codec
    {
        public string Name;
        public string InputArgs;
        public string OutputArgs;

        public override string ToString()
        {
            return Name;
        }
    }
}
