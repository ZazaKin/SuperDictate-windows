import Foundation

/// An sRGB color with alpha, 0 to 255 per channel, as the skins and accents are written.
public struct RGBA: Equatable, Sendable {
    public var red: UInt8
    public var green: UInt8
    public var blue: UInt8
    public var alpha: UInt8

    /// - Parameter rgb: 0xRRGGBB.
    public init(_ rgb: UInt32, alpha: UInt8 = 255) {
        red = UInt8(rgb >> 16 & 0xFF)
        green = UInt8(rgb >> 8 & 0xFF)
        blue = UInt8(rgb & 0xFF)
        self.alpha = alpha
    }

    /// "#5B8DEF"; nil for anything else.
    public init?(hex: String) {
        let digits = hex.hasPrefix("#") ? hex.dropFirst() : Substring(hex)
        guard digits.count == 6, let rgb = UInt32(digits, radix: 16) else { return nil }
        self.init(rgb)
    }

    public static let white = RGBA(0xFFFFFF)
    public static let black = RGBA(0x000000)
    public static let clear = RGBA(0x000000, alpha: 0)

    /// WCAG contrast ratio of two colors, ignoring alpha.
    public static func contrast(_ first: RGBA, _ second: RGBA) -> Double {
        let (lighter, darker) = (max(first.luminance, second.luminance), min(first.luminance, second.luminance))
        return (lighter + 0.05) / (darker + 0.05)
    }

    private var luminance: Double {
        func linear(_ channel: UInt8) -> Double {
            let value = Double(channel) / 255
            return value <= 0.03928 ? value / 12.92 : pow((value + 0.055) / 1.055, 2.4)
        }
        return 0.2126 * linear(red) + 0.7152 * linear(green) + 0.0722 * linear(blue)
    }
}

/// A look for the capsule. Text on every skin meets WCAG AA (4.5:1) against its
/// fill, which the tests check; translucent skins are checked as if opaque, and
/// keep enough fill to hold that over most backgrounds. The same fifteen skins,
/// with the same colors, as on Windows (`Ui/CapsuleSkin.cs`).
public struct CapsuleSkin: Identifiable, Equatable, Sendable {
    /// How the rim is drawn.
    public enum Rim: Sendable {
        case plain
        /// In the accent color, as if lit from inside.
        case accent
        /// From the accent through violet to teal.
        case rainbow
        /// Catching the light at the top and bottom, as glass does.
        case glass
    }

    public let id: String
    public let name: String
    /// Top of the fill, and its bottom.
    public let fill: RGBA
    public let fillEnd: RGBA
    public let border: RGBA
    public let borderWidth: Double
    public let text: RGBA
    /// Status text such as "Listening…", and the recording time.
    public let muted: RGBA
    public let rim: Rim
    /// The moving picture under the words, for an art skin; nil for a plain one.
    public let art: String?

    init(_ id: String, _ name: String, fill: RGBA, fillEnd: RGBA? = nil, border: RGBA, borderWidth: Double = 1,
         text: RGBA = .white, muted: RGBA, rim: Rim = .plain, art: String? = nil) {
        self.id = id
        self.name = name
        self.fill = fill
        self.fillEnd = fillEnd ?? fill
        self.border = border
        self.borderWidth = borderWidth
        self.text = text
        self.muted = muted
        self.rim = rim
        self.art = art
    }

    /// The fill shows what's behind it, so it gets no shadow.
    public var isTranslucent: Bool { fill.alpha < 255 }

    /// Liquid Glass: the real thing on macOS 26 and later, its fill the tint.
    public var isGlass: Bool { rim == .glass }

    public static let all = [
        // The original: near-black with a hairline, readable over anything.
        CapsuleSkin("midnight", "Midnight", fill: RGBA(0x18181C), border: RGBA(0xFFFFFF, alpha: 40), muted: RGBA(0xB4B4BE)),
        // Apple's Liquid Glass. Before macOS 26, and on Windows, a recreation: smoky
        // see-through glass, a rim that catches the light, a sheen across the top.
        CapsuleSkin("liquid", "Liquid Glass", fill: RGBA(0x161A24, alpha: 120), fillEnd: RGBA(0x0E1018, alpha: 165),
                    border: .white, borderWidth: 1.2, muted: RGBA(0xD8DCE4), rim: .glass),

        // The art skins: a moving picture under the words (the app's CapsuleArt), calmed
        // behind them. Fill and fillEnd are the two extremes the words sit on, so both are checked.
        // Grainy pastel light pools drifting, swelling as you speak; light, dark words.
        CapsuleSkin("bloom", "Bloom", fill: RGBA(0xF6EEF0), fillEnd: RGBA(0xFF7A6B), border: RGBA(0xFFFFFF, alpha: 150),
                    text: RGBA(0x121426), muted: RGBA(0x33364A), art: "bloom"),
        // A planet's lit edge low across the capsule; the light rises with the voice.
        CapsuleSkin("eclipse", "Eclipse", fill: RGBA(0x02040A), fillEnd: RGBA(0x1F4FB4), border: RGBA(0xFFFFFF, alpha: 36),
                    muted: RGBA(0xD4E0F5), art: "eclipse"),
        // Curved black ribbons with iridescent edges and a sliding glint.
        CapsuleSkin("chrome", "Chrome", fill: RGBA(0x07080B), fillEnd: RGBA(0x525356), border: RGBA(0xFFFFFF, alpha: 50),
                    muted: RGBA(0xC9CCD3), art: "chrome"),
        // A honeycomb on black, lit by a wandering violet light.
        CapsuleSkin("hive", "Hive", fill: RGBA(0x050509), fillEnd: RGBA(0x28154F), border: RGBA(0xFFFFFF, alpha: 40),
                    muted: RGBA(0xD8D4EE), art: "hive"),
        // A pink pixel wave in ordered dither on navy, rising as you speak.
        CapsuleSkin("halftone", "Halftone", fill: RGBA(0x0A1022), fillEnd: RGBA(0x381432), border: RGBA(0xFFFFFF, alpha: 40),
                    muted: RGBA(0xF7E6EE), art: "halftone"),
        // Lavender-to-pink tiles with a shimmer running through them; light.
        CapsuleSkin("mosaic", "Mosaic", fill: RGBA(0xF2EEF9), fillEnd: RGBA(0xF07AA5), border: RGBA(0xFFFFFF, alpha: 150),
                    text: RGBA(0x17121C), muted: RGBA(0x4A2C3A), art: "mosaic"),
        // A pale sky with drifting clouds over a terracotta horizon; light, terracotta words.
        CapsuleSkin("mesa", "Mesa", fill: RGBA(0xC5D6E6), fillEnd: RGBA(0xE6A07E), border: RGBA(0x000000, alpha: 40),
                    text: RGBA(0x6E241C), muted: RGBA(0x5E2F29), art: "mesa"),

        // Dark, with a rim that shifts from the accent through violet to teal.
        CapsuleSkin("aurora", "Aurora", fill: RGBA(0x12121A), fillEnd: RGBA(0x0E0E16), border: .clear, borderWidth: 1.5,
                    muted: RGBA(0xBFC2D0), rim: .rainbow),
        // Black, ringed in the accent color.
        CapsuleSkin("neon", "Neon", fill: RGBA(0x0B0B10), border: .clear, borderWidth: 1.5, muted: RGBA(0xC4C6D2), rim: .accent),
        // Smoked glass: the screen shows through, a bright rim catches the light.
        CapsuleSkin("glass", "Glass", fill: RGBA(0x1C1E26, alpha: 150), fillEnd: RGBA(0x14161E, alpha: 200),
                    border: RGBA(0xFFFFFF, alpha: 90), muted: RGBA(0xD6D9E0)),
        // Brushed dark grey, lit slightly from above.
        CapsuleSkin("graphite", "Graphite", fill: RGBA(0x3A3D44), fillEnd: RGBA(0x25272C), border: RGBA(0xFFFFFF, alpha: 26),
                    muted: RGBA(0xC6C9D0)),
        // Light, for light themes and bright apps.
        CapsuleSkin("paper", "Paper", fill: RGBA(0xFDFDFE), fillEnd: RGBA(0xF1F2F5), border: RGBA(0x000000, alpha: 30),
                    text: RGBA(0x1B1B1F), muted: RGBA(0x555860)),
        // Black and white, for the clearest possible read.
        CapsuleSkin("contrast", "High Contrast", fill: .black, border: .white, borderWidth: 2, muted: .white),
    ]

    public static func find(_ id: String?) -> CapsuleSkin {
        all.first { $0.id == id } ?? all[0]
    }
}
