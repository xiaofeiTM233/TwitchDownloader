using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace TwitchDownloaderAvalonia.Services
{
    internal static class DynamicResourceHelper
    {
        private static readonly Func<object, object> BrushConverter = value => value as IBrush;

        public static void BindBrush(this StyledElement element, AvaloniaProperty property, string resourceKey)
        {
            var resources = Application.Current?.Resources;
            if (resources is null)
            {
                return;
            }

            element.Bind(property, resources.GetResourceObservable(resourceKey, BrushConverter));
        }
    }
}
