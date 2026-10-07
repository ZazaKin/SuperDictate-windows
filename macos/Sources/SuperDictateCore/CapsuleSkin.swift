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
/// keep enough fill to hold that over most backgrounds. The same twelve skins,
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

    init(_ id: String, _ name: String, fill: RGBA, fillEnd: RGBA? = nil, border: RGBA, borderWidth: Double = 1,
         text: RGBA = .white, muted: RGBA, rim: Rim = .plain) {
        self.id = id
        self.name = name
        self.fill = fill
        self.fillEnd = fillEnd ?? fill
        self.border = border
        self.borderWidth = borderWidth
        self.text = text
        self.muted = muted
        self.rim = rim
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
        // Brushed dark grey, lit slightly from above.
        CapsuleSkin("graphite", "Graphite", fill: RGBA(0x3A3D44), fillEnd: RGBA(0x25272C), border: RGBA(0xFFFFFF, alpha: 26),
                    muted: RGBA(0xC6C9D0)),
        // Light, for light themes and bright apps.
        CapsuleSkin("paper", "Paper", fill: RGBA(0xFDFDFE), fillEnd: RGBA(0xF1F2F5), border: RGBA(0x000000, alpha: 30),
                    text: RGBA(0x1B1B1F), muted: RGBA(0x555860)),
        // Smoked glass: the screen shows through, a bright rim catches the light.
        CapsuleSkin("glass", "Glass", fill: RGBA(0x1C1E26, alpha: 150), fillEnd: RGBA(0x14161E, alpha: 200),
                    border: RGBA(0xFFFFFF, alpha: 90), muted: RGBA(0xD6D9E0)),
        // Dark, with a rim that shifts from the accent through violet to teal.
        CapsuleSkin("aurora", "Aurora", fill: RGBA(0x12121A), fillEnd: RGBA(0x0E0E16), border: .clear, borderWidth: 1.5,
                    muted: RGBA(0xBFC2D0), rim: .rainbow),
        // The Windows app's Telegram night colors.
        CapsuleSkin("telegram", "Telegram", fill: RGBA(0x17212B), border: RGBA(0x2B3A4A), text: RGBA(0xF5F5F5),
                    muted: RGBA(0x9DB0C4)),
        // Black and white, for the clearest possible read.
        CapsuleSkin("contrast", "High Contrast", fill: .black, border: .white, borderWidth: 2, muted: .white),
        // Black, ringed in the accent color.
        CapsuleSkin("neon", "Neon", fill: RGBA(0x0B0B10), border: .clear, borderWidth: 1.5, muted: RGBA(0xC4C6D2), rim: .accent),
        // Deep sea blue, darker toward the bottom.
        CapsuleSkin("ocean", "Ocean", fill: RGBA(0x103E60), fillEnd: RGBA(0x0A243C), border: RGBA(0x7FD8FF, alpha: 70),
                    muted: RGBA(0xC2DDEE)),
        // Evening plum warming to ember.
        CapsuleSkin("sunset", "Sunset", fill: RGBA(0x4A1D3A), fillEnd: RGBA(0x2A101E), border: RGBA(0xFF9F6B, alpha: 80),
                    muted: RGBA(0xF0CFD8)),
        // Pine green, quiet.
        CapsuleSkin("forest", "Forest", fill: RGBA(0x163424), fillEnd: RGBA(0x0D2217), border: RGBA(0x8FE3B0, alpha: 60),
                    muted: RGBA(0xC5E2CF)),
        // Frosted white glass, for bright screens.
        CapsuleSkin("frost", "Frost", fill: RGBA(0xF7F8FB, alpha: 225), fillEnd: RGBA(0xECEFF4, alpha: 240),
                    border: RGBA(0xFFFFFF, alpha: 200), text: RGBA(0x16181D), muted: RGBA(0x4E535C)),
    ]

    public static func find(_ id: String?) -> CapsuleSkin {
        all.first { $0.id == id } ?? all[0]
    }
}
