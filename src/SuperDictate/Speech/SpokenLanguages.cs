using System.Linq;

namespace SuperDictate.Speech;

/// <summary>
/// The languages SuperDictate offers: the Mac app's 25 European languages, then
/// languages only Whisper knows. Whisper knows many more; these cover most people.
/// worker.py has a punctuation prompt for each.
/// </summary>
public static class SpokenLanguages
{
    public static readonly (string Code, string Native, string English)[] All =
    {
        ("en", "English", "English"),
        ("es", "Español", "Spanish"),
        ("fr", "Français", "French"),
        ("de", "Deutsch", "German"),
        ("it", "Italiano", "Italian"),
        ("pt", "Português", "Portuguese"),
        ("nl", "Nederlands", "Dutch"),
        ("pl", "Polski", "Polish"),
        ("ru", "Русский", "Russian"),
        ("uk", "Українська", "Ukrainian"),
        ("cs", "Čeština", "Czech"),
        ("sv", "Svenska", "Swedish"),
        ("el", "Ελληνικά", "Greek"),
        ("ro", "Română", "Romanian"),
        ("hu", "Magyar", "Hungarian"),
        ("bg", "Български", "Bulgarian"),
        ("da", "Dansk", "Danish"),
        ("fi", "Suomi", "Finnish"),
        ("sk", "Slovenčina", "Slovak"),
        ("hr", "Hrvatski", "Croatian"),
        ("lt", "Lietuvių", "Lithuanian"),
        ("sl", "Slovenščina", "Slovenian"),
        ("lv", "Latviešu", "Latvian"),
        ("et", "Eesti", "Estonian"),
        ("mt", "Malti", "Maltese"),
        ("tr", "Türkçe", "Turkish"),
        ("ar", "العربية", "Arabic"),
        ("hi", "हिन्दी", "Hindi"),
        ("zh", "中文", "Chinese"),
        ("ja", "日本語", "Japanese"),
        ("ko", "한국어", "Korean"),
        ("vi", "Tiếng Việt", "Vietnamese"),
    };

    public static string EnglishName(string code) =>
        All.FirstOrDefault(language => language.Code == code).English ?? code;
}
