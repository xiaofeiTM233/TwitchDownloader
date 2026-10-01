using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using TwitchDownloaderAvalonia.Translations;

namespace TwitchDownloaderAvalonia.Localization
{
    public class LocalizationService : INotifyPropertyChanged
    {
        public static LocalizationService Instance { get; } = new();

        private CultureInfo _culture = CultureInfo.CurrentUICulture;

        public CultureInfo Culture
        {
            get => _culture;
            set
            {
                if (EqualityComparer<CultureInfo>.Default.Equals(_culture, value))
                    return;

                _culture = value;
                OnPropertyChanged("Item[]");
            }
        }

        public string this[string key]
        {
            get
            {
                var value = Strings.ResourceManager.GetObject(key, Culture);
                return value as string ?? Strings.ResourceManager.GetObject(key, CultureInfo.InvariantCulture) as string;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
