using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TwitchDownloaderAvalonia.Behaviors
{
    public class TextBoxTripleClickBehavior
    {
        public static readonly AttachedProperty<bool> TripleClickSelectLineProperty =
            AvaloniaProperty.RegisterAttached<TextBoxTripleClickBehavior, Control, bool>(
                "TripleClickSelectLine", false);

        public static bool GetTripleClickSelectLine(Control control)
        {
            return control.GetValue(TripleClickSelectLineProperty);
        }

        public static void SetTripleClickSelectLine(Control control, bool value)
        {
            control.SetValue(TripleClickSelectLineProperty, value);
        }

        static TextBoxTripleClickBehavior()
        {
            TripleClickSelectLineProperty.Changed.AddClassHandler<TextBox>(OnPropertyChanged);
        }

        private static void OnPropertyChanged(TextBox textBox, AvaloniaPropertyChangedEventArgs e)
        {
            var enable = (bool)e.NewValue;
            if (enable)
            {
                textBox.PointerPressed += OnTextBoxPointerPressed;
            }
            else
            {
                textBox.PointerPressed -= OnTextBoxPointerPressed;
            }
        }

        private static void OnTextBoxPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.ClickCount == 3 && sender is TextBox textBox)
            {
                var (start, length) = GetCurrentLine(textBox);
                textBox.SelectionStart = start;
                textBox.SelectionEnd = start + length;
                e.Handled = true;
            }
        }

        private static (int start, int length) GetCurrentLine(TextBox textBox)
        {
            var caretPos = textBox.CaretIndex;
            var text = textBox.Text ?? string.Empty;

            var start = -1;
            var end = -1;

            // CaretIndex can be negative for some reason.
            if (caretPos >= 0 && caretPos <= text.Length)
            {
                start = text.LastIndexOf('\n', caretPos);
                end = text.IndexOf('\n', caretPos);
            }

            if (start == -1)
            {
                start = 0;
            }

            if (end == -1)
            {
                end = text.Length;
            }

            return (start, end - start);
        }
    }
}
