import AppKit
import SuperDictateCore
import SwiftUI

/// The window that carries the capsule: a transparent, click-through panel that
/// never becomes key, so focus, the cursor and the text target stay in the app
/// the user is typing in. It stays up once shown; when idle it is empty.
///
/// It is as wide as the capsule at its widest and sits where the settings say,
/// so the capsule inside it can keep to its edge and grow away from it.
@MainActor
final class CapsulePanel {
    /// Room around the capsule for its shadow and its spring.
    static let inset: CGFloat = 24
    private let panel: NSPanel
    private var settingsChanged: (any NSObjectProtocol)?

    init(model: DictationModel) {
        panel = NSPanel(contentRect: .zero,
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

        // A new place, size or screen applies at once, which the Capsule settings page relies on.
        settingsChanged = NotificationCenter.default.addObserver(
            forName: UserDefaults.didChangeNotification, object: nil, queue: .main
        ) { [weak self] _ in
            MainActor.assumeIsolated { self?.place() }
        }
    }

    func present() {
        place()
        panel.orderFrontRegardless()
    }

    private func place() {
        let screens = NSScreen.screens
        guard let screen = Preferences.capsuleOnMainScreen ? screens.first : NSScreen.main ?? screens.first else { return }
        let size = CGSize(width: CapsuleStyle(scale: Preferences.capsuleScale, maxWidth: Preferences.capsuleMaxWidth).widest,
                          height: CapsuleStyle.height * Preferences.capsuleScale)

        // Placement works top down; AppKit counts up from the bottom of the main screen.
        let top = screen.frame.maxY
        let visible = screen.visibleFrame
        let area = CGRect(x: visible.minX, y: top - visible.maxY, width: visible.width, height: visible.height)
        let spot = Preferences.placement.place(size, widest: size.width, in: area)
        let frame = NSRect(x: spot.minX, y: top - spot.maxY, width: spot.width, height: spot.height)
            .insetBy(dx: -Self.inset, dy: -Self.inset)
        if panel.frame != frame { panel.setFrame(frame, display: true) }
    }
}

/// What the panel shows: the capsule, kept to the edge the placement says. At
/// the top of the screen it springs down out of the menu bar, in the manner of
/// the Dynamic Island; at the bottom it rises the same way; anywhere else it
/// grows in from the corner it keeps to.
struct CapsuleView: View {
    @Environment(DictationModel.self) private var model
    private var stored = StoredCapsuleStyle()
    @AppStorage(Preferences.Key.placement) private var placement = CapsulePlacement.topCenter

    var body: some View {
        ZStack {
            if model.overlay != .hidden {
                CapsulePill(style: stored.style, overlay: model.overlay, caption: model.caption,
                            started: model.recordingStarted, level: model.currentLevel)
                    .transition(placement.arrival)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: placement.alignment)
        .padding(CapsulePanel.inset)
        .animation(.spring(duration: 0.45, bounce: 0.2), value: model.overlay)
    }
}

extension CapsulePlacement {
    /// The corner or side the capsule keeps to, as SwiftUI names it.
    var alignment: Alignment {
        let across: HorizontalAlignment = switch horizontal {
        case .start: .leading
        case .center: .center
        case .end: .trailing
        }
        let down: VerticalAlignment = switch vertical {
        case .start: .top
        case .center: .center
        case .end: .bottom
        }
        return Alignment(horizontal: across, vertical: down)
    }

    var arrival: AnyTransition {
        if atTop { return .scale(scale: 0.5, anchor: .top).combined(with: .opacity) }
        if atBottom { return .scale(scale: 0.5, anchor: .bottom).combined(with: .opacity) }
        let corner = UnitPoint(x: Self.fraction(horizontal), y: Self.fraction(vertical))
        return .scale(scale: 0.9, anchor: corner).combined(with: .opacity)
    }

    private static func fraction(_ edge: CapsuleEdge) -> Double {
        switch edge {
        case .start: 0
        case .center: 0.5
        case .end: 1
        }
    }
}
