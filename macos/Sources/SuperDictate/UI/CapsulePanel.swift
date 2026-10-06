import AppKit
import SuperDictateCore
import SwiftUI

/// The window that carries the capsule: a transparent, click-through panel just
/// under the menu bar that never becomes key, so focus, the cursor and the text
/// target stay in the app the user is typing in. It stays up once shown; when
/// idle it is empty.
@MainActor
final class CapsulePanel {
    private static let size = NSSize(width: 560, height: 120)
    private let panel: NSPanel

    init(model: DictationModel) {
        panel = NSPanel(contentRect: NSRect(origin: .zero, size: Self.size),
                        styleMask: [.borderless, .nonactivatingPanel],
                        backing: .buffered, defer: false)
        panel.isFloatingPanel = true
        panel.level = .statusBar
        panel.backgroundColor = .clear
        panel.isOpaque = false
        panel.hasShadow = false
        panel.ignoresMouseEvents = true
        panel.hidesOnDeactivate = false
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary, .ignoresCycle]

        let host = NSHostingView(rootView: CapsuleView().environment(model))
        host.sizingOptions = []
        panel.contentView = host
    }

    /// Top centre of the screen the user is working on.
    func present() {
        guard let screen = NSScreen.main ?? NSScreen.screens.first else { return }
        let area = screen.visibleFrame
        panel.setFrameOrigin(NSPoint(x: area.midX - Self.size.width / 2, y: area.maxY - Self.size.height))
        panel.orderFrontRegardless()
    }
}

/// The capsule: a black pill in the manner of the Dynamic Island. It springs
/// down out of the menu bar and goes back the same way.
struct CapsuleView: View {
    @Environment(DictationModel.self) private var model

    var body: some View {
        ZStack(alignment: .top) {
            if model.overlay != .hidden {
                Pill()
                    .padding(.top, 8)
                    .transition(.scale(scale: 0.5, anchor: .top).combined(with: .opacity))
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
        .animation(.spring(duration: 0.45, bounce: 0.2), value: model.overlay)
    }
}

private struct Pill: View {
    @Environment(DictationModel.self) private var model

    var body: some View {
        HStack(spacing: 12) {
            switch model.overlay {
            case .notice(let message, let symbol):
                Image(systemName: symbol)
                    .font(.system(size: 13, weight: .semibold))
                Text(message)
                    .font(.system(size: 13, weight: .medium))
            default:
                VoiceBars(processing: model.overlay == .processing)
                if model.caption.isEmpty {
                    Text(model.overlay == .processing ? "Processing…" : "Listening…")
                        .font(.system(size: 13, weight: .medium))
                        .foregroundStyle(.secondary)
                        .transition(.opacity)
                } else {
                    LiveWords(caption: model.caption, processing: model.overlay == .processing)
                }
            }
        }
        .foregroundStyle(.white)
        .padding(.horizontal, 18)
        .frame(minWidth: 168, minHeight: 40)
        .background(.black, in: Capsule(style: .continuous))
        .overlay(Capsule(style: .continuous).strokeBorder(.white.opacity(0.1), lineWidth: 1))
        .shadow(color: .black.opacity(0.3), radius: 16, y: 6)
        .environment(\.colorScheme, .dark)
    }
}

/// Five bars in the system accent color. Recording, they follow the voice in a
/// travelling wave; processing, they settle into a low ripple that no longer
/// reacts to sound, so it never looks like it's still listening.
private struct VoiceBars: View {
    let processing: Bool
    @Environment(DictationModel.self) private var model

    var body: some View {
        TimelineView(.animation) { timeline in
            let phase = timeline.date.timeIntervalSinceReferenceDate * 9
            let level = processing ? 0 : min(Double(model.currentLevel()) * 6, 1)
            HStack(spacing: 3) {
                ForEach(0 ..< 5, id: \.self) { index in
                    let wave = 0.5 + 0.5 * sin(phase + Double(index) * 0.7)
                    let fill = processing ? 0.15 + 0.25 * wave : wave * level
                    Capsule()
                        .fill(Color.accentColor)
                        .frame(width: 3, height: 5 + 15 * fill)
                }
            }
            .frame(height: 20)
        }
    }
}

/// The live preview, building up word by word. Each new word rises into place,
/// sharpening out of a blur as it fades in, a beat after the one before. Words
/// that may still change are dimmer and brighten once they settle. The line
/// widens to fit, up to its maximum; past that the newest words stay in view
/// and the oldest fade out at the left edge.
private struct LiveWords: View {
    static let widest: CGFloat = 400

    let caption: Caption
    let processing: Bool
    @State private var lineWidth: CGFloat = 0

    var body: some View {
        HStack(spacing: 4) {
            ForEach(caption.words) { word in
                Text(word.text)
                    .opacity(word.settled ? 1 : 0.5)
                    .transition(WordArrival().animation(.spring(duration: 0.5, bounce: 0).delay(word.delay)))
            }
        }
        .font(.system(size: 14, weight: .medium))
        .fixedSize()
        .background {
            GeometryReader { geometry in
                Color.clear.preference(key: LineWidth.self, value: geometry.size.width)
            }
        }
        .onPreferenceChange(LineWidth.self) { lineWidth = $0 }
        .frame(maxWidth: Self.widest, alignment: .trailing)
        .clipped()
        .mask {
            LinearGradient(stops: [.init(color: lineWidth > Self.widest ? .clear : .black, location: 0),
                                   .init(color: .black, location: 0.12)],
                           startPoint: .leading, endPoint: .trailing)
        }
        .opacity(processing ? 0.75 : 1)
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
