using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace TwitchDownloaderAvalonia.Services
{
    public static class ThumbnailService
    {
        internal const string THUMBNAIL_MISSING_URL = @"https://vod-secure.twitch.tv/_404/404_processing_320x180.png";

        private static readonly HttpClient _httpClient = new();

        /// <exception cref="ArgumentNullException">The <paramref name="thumbUrl"/> was <see langword="null"/></exception>
        public static async Task<Bitmap> GetThumb(string thumbUrl)
        {
            ArgumentNullException.ThrowIfNull(thumbUrl);

            await using var stream = await _httpClient.GetStreamAsync(thumbUrl);
            return new Bitmap(stream);
        }

        /// <returns><see langword="null"/> if the thumbnail could not be fetched</returns>
        public static async Task<Bitmap> TryGetThumb(string thumbUrl)
        {
            if (string.IsNullOrWhiteSpace(thumbUrl))
            {
                return null;
            }

            try
            {
                return await GetThumb(thumbUrl);
            }
            catch
            {
                return null;
            }
        }
    }
}
