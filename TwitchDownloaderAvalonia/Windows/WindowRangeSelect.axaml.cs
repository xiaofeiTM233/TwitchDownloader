using Avalonia.Controls;
using Avalonia.Interactivity;
using TwitchDownloaderCore;

namespace TwitchDownloaderAvalonia
{
    /// <summary>
    /// Interaction logic for WindowRangeSelect.axaml
    /// </summary>
    public partial class WindowRangeSelect : Window
    {
        public ChatRenderer CurrentRender { get; set; }
        public bool OK { get; set; } = false;
        public bool Invalid { get; set; } = false;
        public int startSeconds { get; set; }
        public int endSeconds { get; set; }

        public WindowRangeSelect(ChatRenderer currentRender)
        {
            CurrentRender = currentRender;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            startSeconds = (int)Math.Floor(CurrentRender.chatRoot.video.start);
            endSeconds = (int)Math.Ceiling(CurrentRender.chatRoot.video.end);

            numStart.Value = startSeconds;
            numEnd.Value = endSeconds;
        }

        private void numStart_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
        {
            if (IsInitialized)
            {
                startSeconds = (int)numStart.Value.GetValueOrDefault();
            }
        }

        private void numEnd_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
        {
            if (IsInitialized)
            {
                endSeconds = (int)numEnd.Value.GetValueOrDefault();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            startSeconds = (int)numStart.Value.GetValueOrDefault();
            endSeconds = (int)numEnd.Value.GetValueOrDefault();
            OK = true;
            Close();
        }
    }
}
