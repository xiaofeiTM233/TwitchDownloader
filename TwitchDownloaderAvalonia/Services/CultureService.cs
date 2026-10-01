using System;
using System.Globalization;

namespace TwitchDownloaderAvalonia.Services
{
    public class CultureService
    {
        public event EventHandler<CultureInfo> CultureChanged;

        public void SetApplicationCulture(string culture)
        {
            try
            {
                var newCulture = CultureInfo.GetCultureInfo(culture);
                CultureInfo.DefaultThreadCurrentCulture = newCulture;
                CultureInfo.DefaultThreadCurrentUICulture = newCulture;
                Thread.CurrentThread.CurrentCulture = newCulture;
                Thread.CurrentThread.CurrentUICulture = newCulture;

                Localization.LocalizationService.Instance.Culture = newCulture;

                CultureChanged?.Invoke(this, newCulture);
            }
            catch (CultureNotFoundException) { }
        }
    }
}
