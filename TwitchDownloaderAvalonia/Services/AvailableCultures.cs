namespace TwitchDownloaderAvalonia.Services
{
    public record struct Culture(string Code, string NativeName);

    public static class AvailableCultures
    {
        // Notes for translators:
        //
        // Please create a new record for your culture and place it in the 'All' array in alphabetical order according to the culture code.
        // The order of the 'All' array is the order that cultures will appear in the language dropdown menu.

        public static readonly Culture English;
        public static readonly Culture Spanish;
        public static readonly Culture German;
        public static readonly Culture French;
        public static readonly Culture Italian;
        public static readonly Culture Japanese;
        public static readonly Culture Polish;
        public static readonly Culture Portuguese;
        public static readonly Culture Russian;
        public static readonly Culture Turkish;
        public static readonly Culture Ukrainian;
        public static readonly Culture SimplifiedChinese;
        public static readonly Culture TraditionalChinese;

        public static readonly Culture[] All;

        static AvailableCultures()
        {
            All =
            [
                English = new Culture("en-US", "English"),
                Spanish = new Culture("es-ES", "Español"),
                German = new Culture("de-DE", "Deutsch"),
                French = new Culture("fr-FR", "Français"),
                Italian = new Culture("it-it", "Italiano"),
                Japanese = new Culture("ja-JP", "日本語"),
                Polish = new Culture("pl-PL", "Polski"),
                Portuguese = new Culture("pt-BR", "Português (Brasil)"),
                Russian = new Culture("ru-RU", "Русский"),
                Turkish = new Culture("tr-TR", "Türkçe"),
                Ukrainian = new Culture("uk-ua", "Українська"),
                SimplifiedChinese = new Culture("zh-CN", "简体中文"),
                TraditionalChinese = new Culture("zh-TW", "繁體中文"),
            ];
        }
    }
}
