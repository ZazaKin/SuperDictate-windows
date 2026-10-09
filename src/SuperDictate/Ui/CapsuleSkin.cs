using System.Linq;
using System.Windows.Media;

namespace SuperDictate.Ui;

/// <summary>
/// A look for the capsule. Text on every skin meets WCAG AA (4.5:1) against its
/// fill, which the self-test checks; translucent skins are checked as if opaque,
/// and keep enough fill to hold that over most backgrounds. An art skin draws a
/// moving picture under the words (<see cref="CapsuleArt"/>).
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
    bool AccentRim = false,
    bool Glass = false,
    string? Art = null,
    string? Meter = null)
{
    public static readonly CapsuleSkin[] All =
    {
        // The original: near-black with a hairline, readable over anything.
        new("midnight", "Midnight", Rgb(0x18, 0x18, 0x1C), Rgb(0x18, 0x18, 0x1C), Argb(40, 255, 255, 255), 1, Colors.White, Rgb(0xB4, 0xB4, 0xBE)),
        // Apple's Liquid Glass, recreated: smoky see-through glass, a rim that catches
        // the light at the top and bottom, and a sheen across the top. On a Mac with
        // macOS 26 the same skin is the real thing.
        new("liquid", "Liquid Glass", Argb(120, 0x16, 0x1A, 0x24), Argb(165, 0x0E, 0x10, 0x18), Colors.White, 1.2, Colors.White, Rgb(0xD8, 0xDC, 0xE4), Glass: true),

        // The art skins (Ui/CapsuleArt.cs): a moving picture under the words, calmed behind
        // them. Fill and FillEnd are the two extremes the words sit on, so both are checked.
        // Grainy pastel light pools drifting, swelling as you speak; light, dark words.
        new("bloom", "Bloom", Rgb(0xF6, 0xEE, 0xF0), Rgb(0xFF, 0x7A, 0x6B), Argb(150, 255, 255, 255), 1, Rgb(0x12, 0x14, 0x26), Rgb(0x33, 0x36, 0x4A), Art: "bloom"),
        // A planet's lit edge low across the capsule; the light rises with the voice.
        new("eclipse", "Eclipse", Rgb(0x02, 0x04, 0x0A), Rgb(0x1F, 0x4F, 0xB4), Argb(36, 255, 255, 255), 1, Colors.White, Rgb(0xD4, 0xE0, 0xF5), Art: "eclipse"),
        // Curved black ribbons with iridescent edges and a sliding glint.
        new("chrome", "Chrome", Rgb(0x07, 0x08, 0x0B), Rgb(0x52, 0x53, 0x56), Argb(50, 255, 255, 255), 1, Colors.White, Rgb(0xC9, 0xCC, 0xD3), Art: "chrome"),
        // A honeycomb on black, lit by a wandering violet light.
        new("hive", "Hive", Rgb(0x05, 0x05, 0x09), Rgb(0x28, 0x15, 0x4F), Argb(40, 255, 255, 255), 1, Colors.White, Rgb(0xD8, 0xD4, 0xEE), Art: "hive"),
        // A pixel wave in ordered dither on navy, in the accent color, rising as you speak: it is its
        // own voice meter, so picking it turns the meter off.
        new("halftone", "Halftone", Rgb(0x0A, 0x10, 0x22), Rgb(0x38, 0x14, 0x32), Argb(40, 255, 255, 255), 1, Colors.White, Rgb(0xF7, 0xE6, 0xEE), Art: "halftone", Meter: "none"),
        // Lavender-to-pink tiles with a shimmer running through them; light.
        new("mosaic", "Mosaic", Rgb(0xF2, 0xEE, 0xF9), Rgb(0xF0, 0x7A, 0xA5), Argb(150, 255, 255, 255), 1, Rgb(0x17, 0x12, 0x1C), Rgb(0x4A, 0x2C, 0x3A), Art: "mosaic"),
        // A pale sky with drifting clouds over a terracotta horizon; light, terracotta words.
        new("mesa", "Mesa", Rgb(0xC5, 0xD6, 0xE6), Rgb(0xE6, 0xA0, 0x7E), Argb(40, 0, 0, 0), 1, Rgb(0x6E, 0x24, 0x1C), Rgb(0x5E, 0x2F, 0x29), Art: "mesa"),

        // Dark, with a rim that shifts from the accent through violet to teal.
        new("aurora", "Aurora", Rgb(0x12, 0x12, 0x1A), Rgb(0x0E, 0x0E, 0x16), Colors.Transparent, 1.5, Colors.White, Rgb(0xBF, 0xC2, 0xD0), Rainbow: true),
        // Black, ringed in the accent color, as if lit from inside.
        new("neon", "Neon", Rgb(0x0B, 0x0B, 0x10), Rgb(0x0B, 0x0B, 0x10), Colors.Transparent, 1.5, Colors.White, Rgb(0xC4, 0xC6, 0xD2), AccentRim: true),
        // Smoked glass: the screen shows through, a bright rim catches the light.
        new("glass", "Glass", Argb(150, 28, 30, 38), Argb(200, 20, 22, 30), Argb(90, 255, 255, 255), 1, Colors.White, Rgb(0xD6, 0xD9, 0xE0)),
        // Brushed dark grey, lit slightly from above.
        new("graphite", "Graphite", Rgb(0x3A, 0x3D, 0x44), Rgb(0x25, 0x27, 0x2C), Argb(26, 255, 255, 255), 1, Colors.White, Rgb(0xC6, 0xC9, 0xD0)),
        // Light, for light themes and bright apps.
        new("paper", "Paper", Rgb(0xFD, 0xFD, 0xFE), Rgb(0xF1, 0xF2, 0xF5), Argb(30, 0, 0, 0), 1, Rgb(0x1B, 0x1B, 0x1F), Rgb(0x55, 0x58, 0x60)),
        // Black and white, for the clearest possible read.
        new("contrast", "High contrast", Colors.Black, Colors.Black, Colors.White, 2, Colors.White, Colors.White),
    };

    public static CapsuleSkin Find(string? id) => All.FirstOrDefault(skin => skin.Id == id) ?? All[0];

    public Brush FillBrush() => Fill == FillEnd
        ? Frozen(new SolidColorBrush(Fill))
        : Frozen(new LinearGradientBrush(Fill, FillEnd, 90));

    public Brush BorderBrush(Color accent) => Glass
        ? Frozen(new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromArgb(200, 255, 255, 255), 0),
                new(Color.FromArgb(30, 255, 255, 255), 0.5),
                new(Color.FromArgb(110, 255, 255, 255), 1),
            }, 90))
        : AccentRim
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
