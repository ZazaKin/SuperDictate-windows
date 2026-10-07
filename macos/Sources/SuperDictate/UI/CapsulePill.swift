import SuperDictateCore
import SwiftUI

/// How the capsule looks: everything on the Capsule settings page except where it sits.
struct CapsuleStyle: Equatable {
    /// The capsule at its smallest, at its widest with words in it, and its height, at size 1.
    static let narrowest: CGFloat = 168
    static let widestAtOne: CGFloat = 460
    static let height: CGFloat = 40

    enum Meter: String, CaseIterable, Identifiable {
        case bars, wave, pulse

        var id: Self { self }
        var title: String { rawValue.capitalized }

        /// The meter's width at size 1, so the words know how much room is left.
        var width: CGFloat {
            switch self {
            case .bars: 27
            case .wave: 44
            case .pulse: 20
            }
        }
    }

    var skin = CapsuleSkin.all[0]
    /// "system", or "#RRGGBB".
    var accent = "system"
    var scale = 1.0
    var opacity = 1.0
    var meter = Meter.bars
    var timer = false

    var accentColor: Color { RGBA(hex: accent).map { Color($0) } ?? .accentColor }

    var widest: CGFloat { Self.widestAtOne * scale }

    /// Room for the live words: the widest capsule less its padding, the meter and the time.
    var textWidth: CGFloat {
        (Self.widestAtOne - 36 - 12 - meter.width - (timer ? 46 : 0)) * scale
    }
}

/// The colors and accents the Capsule page offers.
struct CapsuleAccent: Identifiable {
    let id: String
    let name: String

    static let all = [
        CapsuleAccent(id: "system", name: "Multicolor"),
        CapsuleAccent(id: "#5B8DEF", name: "Blue"),
        CapsuleAccent(id: "#635BFF", name: "Violet"),
        CapsuleAccent(id: "#00C853", name: "Green"),
        CapsuleAccent(id: "#FF4081", name: "Coral"),
        CapsuleAccent(id: "#FF9F0A", name: "Orange"),
        CapsuleAccent(id: "#2EC4B6", name: "Teal"),
    ]
}

/// The style as saved, kept up to date as the settings change.
struct StoredCapsuleStyle: DynamicProperty {
    @AppStorage(Preferences.Key.skin) var skin = "midnight"
    @AppStorage(Preferences.Key.accent) var accent = "system"
    @AppStorage(Preferences.Key.scale) var scale = 1.0
    @AppStorage(Preferences.Key.opacity) var opacity = 1.0
    @AppStorage(Preferences.Key.meter) var meter = CapsuleStyle.Meter.bars
    @AppStorage(Preferences.Key.timer) var timer = false

    var style: CapsuleStyle {
        CapsuleStyle(skin: .find(skin), accent: accent, scale: scale, opacity: opacity, meter: meter, timer: timer)
    }
}

extension Color {
    init(_ color: RGBA) {
        self.init(.sRGB, red: Double(color.red) / 255, green: Double(color.green) / 255,
                  blue: Double(color.blue) / 255, opacity: Double(color.alpha) / 255)
    }
}

/// The capsule itself, as the overlay, the position editor and the skin tiles
/// draw it: a voice meter, then a status or the live words, then the recording
/// time if the style asks for it. A black pill by default, in the manner of the
/// Dynamic Island.
struct CapsulePill: View {
    let style: CapsuleStyle
    var overlay = DictationModel.Overlay.listening
    var caption = Caption()
    /// Replaces "Listening…".
    var status: String?
    /// When the recording started, for the time.
    var started: Date?
    /// False draws a still, for the skin tiles.
    var live = true
    var level: () -> Float = { 0.1 }

    var body: some View {
        let scale = style.scale
        let skin = style.skin
        HStack(spacing: 12 * scale) {
            switch overlay {
            case .notice(let message, let symbol):
                Image(systemName: symbol)
                    .font(.system(size: 13 * scale, weight: .semibold))
                Text(message)
                    .font(.system(size: 13 * scale, weight: .medium))
            default:
                let processing = overlay == .processing
                VoiceMeter(style: style, processing: processing, live: live, level: level)
                if caption.isEmpty {
                    Text(status ?? (processing ? "Processing…" : "Listening…"))
                        .font(.system(size: 13 * scale, weight: .medium))
                        .foregroundStyle(Color(skin.muted))
                        .transition(.opacity)
                } else {
                    LiveWords(caption: caption, processing: processing, widest: style.textWidth, size: 14 * scale)
                }
                if style.timer, let started, !processing {
                    Text(timerInterval: started ... Date.distantFuture, countsDown: false)
                        .font(.system(size: 11 * scale, weight: .medium).monospacedDigit())
                        .foregroundStyle(Color(skin.muted))
                }
            }
        }
        .foregroundStyle(Color(skin.text))
        .padding(.horizontal, 18 * scale)
        .frame(minWidth: CapsuleStyle.narrowest * scale, minHeight: CapsuleStyle.height * scale)
        .background(LinearGradient(colors: [Color(skin.fill), Color(skin.fillEnd)], startPoint: .top, endPoint: .bottom),
                    in: Capsule(style: .continuous))
        .overlay(Capsule(style: .continuous).strokeBorder(rim, lineWidth: skin.borderWidth))
        // A see-through skin would show its own shadow through itself; it floats on its rim instead.
        .shadow(color: .black.opacity(skin.isTranslucent ? 0 : 0.3), radius: 16 * scale, y: 6 * scale)
        .opacity(style.opacity)
    }

    private var rim: AnyShapeStyle {
        switch style.skin.rim {
        case .plain:
            AnyShapeStyle(Color(style.skin.border))
        case .accent:
            AnyShapeStyle(style.accentColor)
        case .rainbow:
            AnyShapeStyle(LinearGradient(colors: [style.accentColor, Color(RGBA(0xB36BFF)), Color(RGBA(0x3DD6C4))],
                                         startPoint: .leading, endPoint: .trailing))
        }
    }
}

/// The voice meter, in the accent color: five bars, a wave of thin bars taller
/// in the middle like a voice print, or a pulsing dot. Recording, it follows the
/// voice; processing, it settles into a low ripple that no longer reacts to
/// sound, so it never looks like it's still listening. With Reduce Motion on it
/// holds still.
private struct VoiceMeter: View {
    let style: CapsuleStyle
    let processing: Bool
    let live: Bool
    let level: () -> Float
    @Environment(\.accessibilityReduceMotion) private var calm

    var body: some View {
        if live {
            TimelineView(.animation) { timeline in
                marks(at: timeline.date.timeIntervalSinceReferenceDate)
            }
        } else {
            marks(at: 0.35)
        }
    }

    private func marks(at seconds: Double) -> some View {
        let scale = style.scale
        let phase = seconds * 9
        let voice: Double = processing ? 0 : min(Double(level()) * 6, 1)
        return Group {
            switch style.meter {
            case .pulse:
                let wave: Double = calm ? 1 : 0.5 + 0.5 * sin(phase * 0.6)
                let swell: Double = processing ? 0.15 + 0.15 * wave : voice
                ZStack {
                    Circle()
                        .strokeBorder(style.accentColor, lineWidth: 1.5 * scale)
                        .frame(width: 12 * scale, height: 12 * scale)
                        .scaleEffect(1 + 0.9 * swell)
                        .opacity(processing ? 0.25 : 0.15 + 0.6 * voice)
                    Circle()
                        .fill(style.accentColor)
                        .frame(width: 8 * scale, height: 8 * scale)
                        .scaleEffect(1 + 1.1 * swell)
                }
            case .bars, .wave:
                let wavy = style.meter == .wave
                let count = wavy ? 13 : 5
                let gap: Double = wavy ? 1.5 : 3
                let barWidth: Double = wavy ? 2 : 3
                let lowest: Double = wavy ? 3 : 5
                let range: Double = wavy ? 17 : 15
                let stagger: Double = wavy ? 0.45 : 0.7
                HStack(spacing: gap * scale) {
                    ForEach(0 ..< count, id: \.self) { index in
                        let wave: Double = calm ? 1 : 0.5 + 0.5 * sin(phase + Double(index) * stagger)
                        // The wave is taller in the middle, like a voice print.
                        let shape: Double = wavy ? sin(.pi * (Double(index) + 0.5) / Double(count)) : 1
                        let fill: Double = processing ? (calm ? 0.35 : 0.2 + 0.3 * wave) : wave * voice * shape
                        Capsule()
                            .fill(style.accentColor)
                            .frame(width: barWidth * scale, height: (lowest + range * fill) * scale)
                    }
                }
            }
        }
        .frame(width: style.meter.width * scale, height: 20 * scale)
    }
}

/// The live preview, building up word by word. Each new word rises into place,
/// sharpening out of a blur as it fades in, a beat after the one before. Words
/// that may still change are dimmer and brighten once they settle. The line
/// widens to fit, up to its maximum; past that the newest words stay in view
/// and the oldest fade out at the left edge.
private struct LiveWords: View {
    let caption: Caption
    let processing: Bool
    let widest: CGFloat
    let size: CGFloat
    @State private var lineWidth: CGFloat = 0

    var body: some View {
        TrailingLine(widest: widest) {
            HStack(spacing: size * 0.3) {
                ForEach(caption.words) { word in
                    Text(word.text)
                        .opacity(word.settled ? 1 : 0.5)
                        .transition(WordArrival().animation(.spring(duration: 0.5, bounce: 0).delay(word.delay)))
                }
            }
            .font(.system(size: size, weight: .medium))
            .fixedSize()
            .background {
                GeometryReader { geometry in
                    Color.clear.preference(key: LineWidth.self, value: geometry.size.width)
                }
            }
        }
        .onPreferenceChange(LineWidth.self) { lineWidth = $0 }
        .clipped()
        .mask {
            LinearGradient(stops: [.init(color: lineWidth > widest ? .clear : .black, location: 0),
                                   .init(color: .black, location: 0.12)],
                           startPoint: .leading, endPoint: .trailing)
        }
        .opacity(processing ? 0.75 : 1)
    }
}

/// As wide as the line, up to `widest`, so the capsule grows with the words.
/// Past that it keeps the end of the line in view; the start runs off the left.
private struct TrailingLine: Layout {
    let widest: CGFloat

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) -> CGSize {
        guard let line = subviews.first?.sizeThatFits(.unspecified) else { return .zero }
        return CGSize(width: min(line.width, widest), height: line.height)
    }

    func placeSubviews(in bounds: CGRect, proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) {
        guard let line = subviews.first else { return }
        let width = line.sizeThatFits(.unspecified).width
        line.place(at: CGPoint(x: bounds.maxX - width, y: bounds.midY), anchor: .leading, proposal: .unspecified)
    }
}

private struct LineWidth: PreferenceKey {
    static let defaultValue: CGFloat = 0
    static func reduce(value: inout CGFloat, nextValue: () -> CGFloat) {
        value = max(value, nextValue())
    }
}

/// How a word arrives: from a little below, out of a blur, fading in.
private struct WordArrival: Transition {
    func body(content: Content, phase: TransitionPhase) -> some View {
        content
            .opacity(phase.isIdentity ? 1 : 0)
            .blur(radius: phase.isIdentity ? 0 : 6)
            .offset(y: phase == .willAppear ? 9 : 0)
    }
}
