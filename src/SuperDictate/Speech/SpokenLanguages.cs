using System.Linq;

namespace SuperDictate.Speech;

/// <summary>
/// The languages SuperDictate offers, most widely spoken first: the same 25 European
/// languages as the Mac app. Whisper knows many more. worker.py has a punctuation
/// prompt for each.
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
    };

    public static bool IsKnown(string code) => All.Any(language => language.Code == code);

    public static string EnglishName(string code) =>
        All.FirstOrDefault(language => language.Code == code).English ?? code;
}
