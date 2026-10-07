using System.Linq;
using System.Windows.Media;

namespace SuperDictate.Ui;

/// <summary>
/// A look for the capsule. Text on every skin meets WCAG AA (4.5:1) against its
/// fill, which the self-test checks; translucent skins are checked as if opaque,
/// and keep enough fill to hold that over most backgrounds.
/// </summary>
public sealed record CapsuleSkin(
    string Id,
    string Name,
    Color Fill,
    Color FillEnd,
    Color Border,
    double BorderWidth,
    Color Text,
    Color Muted,
    bool Rainbow = false,
    bool AccentRim = false)
{
    public static readonly CapsuleSkin[] All =
    {
        // The original: near-black with a hairline, readable over anything.
        new("midnight", "Midnight", Rgb(0x18, 0x18, 0x1C), Rgb(0x18, 0x18, 0x1C), Argb(40, 255, 255, 255), 1, Colors.White, Rgb(0xB4, 0xB4, 0xBE)),
        // Brushed dark grey, lit slightly from above.
        new("graphite", "Graphite", Rgb(0x3A, 0x3D, 0x44), Rgb(0x25, 0x27, 0x2C), Argb(26, 255, 255, 255), 1, Colors.White, Rgb(0xC6, 0xC9, 0xD0)),
        // Light, for light themes and bright apps.
        new("paper", "Paper", Rgb(0xFD, 0xFD, 0xFE), Rgb(0xF1, 0xF2, 0xF5), Argb(30, 0, 0, 0), 1, Rgb(0x1B, 0x1B, 0x1F), Rgb(0x55, 0x58, 0x60)),
        // Smoked glass: the screen shows through, a bright rim catches the light.
        new("glass", "Glass", Argb(150, 28, 30, 38), Argb(200, 20, 22, 30), Argb(90, 255, 255, 255), 1, Colors.White, Rgb(0xD6, 0xD9, 0xE0)),
        // Dark, with a rim that shifts from the accent through violet to teal.
        new("aurora", "Aurora", Rgb(0x12, 0x12, 0x1A), Rgb(0x0E, 0x0E, 0x16), Colors.Transparent, 1.5, Colors.White, Rgb(0xBF, 0xC2, 0xD0), Rainbow: true),
        // The app's own Telegram night colors.
        new("telegram", "Telegram", Rgb(0x17, 0x21, 0x2B), Rgb(0x17, 0x21, 0x2B), Rgb(0x2B, 0x3A, 0x4A), 1, Rgb(0xF5, 0xF5, 0xF5), Rgb(0x9D, 0xB0, 0xC4)),
        // Black and white, for the clearest possible read.
        new("contrast", "High contrast", Colors.Black, Colors.Black, Colors.White, 2, Colors.White, Colors.White),
        // Black, ringed in the accent color, as if lit from inside.
        new("neon", "Neon", Rgb(0x0B, 0x0B, 0x10), Rgb(0x0B, 0x0B, 0x10), Colors.Transparent, 1.5, Colors.White, Rgb(0xC4, 0xC6, 0xD2), AccentRim: true),
        // Deep sea blue, darker toward the bottom.
        new("ocean", "Ocean", Rgb(0x10, 0x3E, 0x60), Rgb(0x0A, 0x24, 0x3C), Argb(70, 0x7F, 0xD8, 0xFF), 1, Colors.White, Rgb(0xC2, 0xDD, 0xEE)),
        // Evening plum warming to ember.
        new("sunset", "Sunset", Rgb(0x4A, 0x1D, 0x3A), Rgb(0x2A, 0x10, 0x1E), Argb(80, 0xFF, 0x9F, 0x6B), 1, Colors.White, Rgb(0xF0, 0xCF, 0xD8)),
        // Pine green, quiet.
        new("forest", "Forest", Rgb(0x16, 0x34, 0x24), Rgb(0x0D, 0x22, 0x17), Argb(60, 0x8F, 0xE3, 0xB0), 1, Colors.White, Rgb(0xC5, 0xE2, 0xCF)),
        // Frosted white glass, for bright screens.
        new("frost", "Frost", Argb(225, 0xF7, 0xF8, 0xFB), Argb(240, 0xEC, 0xEF, 0xF4), Argb(200, 255, 255, 255), 1, Rgb(0x16, 0x18, 0x1D), Rgb(0x4E, 0x53, 0x5C)),
    };

    public static CapsuleSkin Find(string? id) => All.FirstOrDefault(skin => skin.Id == id) ?? All[0];

    public Brush FillBrush() => Fill == FillEnd
        ? Frozen(new SolidColorBrush(Fill))
        : Frozen(new LinearGradientBrush(Fill, FillEnd, 90));

    public Brush BorderBrush(Color accent) => AccentRim
        ? Frozen(new SolidColorBrush(accent))
        : Rainbow
        ? Frozen(new LinearGradientBrush(
            new GradientStopCollection
            {
                new(accent, 0),
                new(Color.FromRgb(0xB3, 0x6B, 0xFF), 0.5),
                new(Color.FromRgb(0x3D, 0xD6, 0xC4), 1),
            }, 0))
        : Frozen(new SolidColorBrush(Border));

    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    private static Color Argb(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);

    private static T Frozen<T>(T freezable) where T : System.Windows.Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
