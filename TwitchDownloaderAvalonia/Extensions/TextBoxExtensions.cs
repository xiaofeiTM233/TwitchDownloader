using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;

namespace TwitchDownloaderAvalonia.Extensions
{
    public static class TextBoxExtensions
    {
        public static bool TryInsertAtCaret([AllowNull] this TextBox textBox, string textToInsert)
        {
            if (textBox is null || string.IsNullOrEmpty(textToInsert))
            {
                return false;
            }

            var caretPos = textBox.CaretIndex;
            if (caretPos < 0)
            {
                return false;
            }

            var selectionStart = textBox.SelectionStart;
            var selectionEnd = textBox.SelectionEnd;
            if (selectionStart != selectionEnd)
            {
                var start = Math.Min(selectionStart, selectionEnd);
                var end = Math.Max(selectionStart, selectionEnd);
                textBox.Text = textBox.Text.Remove(start, end - start);
                caretPos = start;
            }

            textBox.Text = textBox.Text.Insert(caretPos, textToInsert);
            textBox.CaretIndex = caretPos + textToInsert.Length;
            return true;
        }
    }
}
