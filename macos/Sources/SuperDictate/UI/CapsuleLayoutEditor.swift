import AppKit
import SuperDictateCore
import SwiftUI

/// Where the capsule goes, chosen by dragging it, in the manner of NVIDIA's
/// overlay layout. The screen dims; the capsule can be dragged anywhere and
/// snaps to the edges and centre lines, which light up when it rests on them.
/// A dashed outline shows how far it grows as words arrive, and from which
/// edge: next to the left or right side it grows away from that side.
/// Arrow keys nudge it (Shift for bigger steps), Return keeps it, Escape cancels.
@MainActor
final class CapsuleLayoutEditor {
    /// The latest editor, kept until the next one: letting go of a window from
    /// inside its own button's action isn't safe.
    private static var current: CapsuleLayoutEditor?
    private let window: NSWindow

    /// - Parameter done: The placement the user chose, or nil if they cancelled.
    static func show(on screen: NSScreen, placement: CapsulePlacement, style: CapsuleStyle,
                     done: @escaping (CapsulePlacement?) -> Void) {
        current?.window.orderOut(nil)
        current = CapsuleLayoutEditor(screen: screen, placement: placement, style: style, done: done)
    }

    private init(screen: NSScreen, placement: CapsulePlacement, style: CapsuleStyle,
                 done: @escaping (CapsulePlacement?) -> Void) {
        let frame = screen.frame
        let visible = screen.visibleFrame
        // The visible part of the screen, top down, in the window's own points.
        let area = CGRect(x: visible.minX - frame.minX, y: frame.maxY - visible.maxY,
                          width: visible.width, height: visible.height)

        let window = KeyableWindow(contentRect: frame, styleMask: .borderless, backing: .buffered, defer: false)
        window.level = NSWindow.Level(rawValue: NSWindow.Level.statusBar.rawValue + 1)
        window.backgroundColor = .clear
        window.isOpaque = false
        window.hasShadow = false
        window.isReleasedWhenClosed = false
        window.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        self.window = window

        window.contentView = NSHostingView(rootView: CapsuleLayoutView(area: area, placement: placement, style: style) {
            [weak window] chosen in
            window?.orderOut(nil)
            done(chosen)
        })
        window.setFrame(frame, display: true)
        NSApp.activate(ignoringOtherApps: true)
        window.makeKeyAndOrderFront(nil)
    }
}

/// A borderless window that takes the keyboard, for the arrow keys, Return and Escape.
private final class KeyableWindow: NSWindow {
    override var canBecomeKey: Bool { true }
}

/// The editor's content, the size of the screen. Also drawn for the snapshots.
struct CapsuleLayoutView: View {
    private static let space = "editor"
    private static let panelSize = CGSize(width: 380, height: 200)

    let area: CGRect
    let style: CapsuleStyle
    let finish: (CapsulePlacement?) -> Void
    @State private var spot: CGRect
    @State private var guides: CapsulePlacement.Guides = []
    /// Where the capsule was grabbed, from its corner, while it's being dragged.
    @State private var grab: CGSize?
    @FocusState private var focused: Bool

    init(area: CGRect, placement: CapsulePlacement, style: CapsuleStyle, finish: @escaping (CapsulePlacement?) -> Void) {
        self.area = area
        self.style = style
        self.finish = finish
        let size = CGSize(width: CapsuleStyle.narrowest * style.scale, height: CapsuleStyle.height * style.scale)
        _spot = State(initialValue: placement.place(size, widest: style.widest, in: area))
    }

    private var placement: CapsulePlacement { CapsulePlacement(spot, in: area) }

    /// How far it grows, from the edge it keeps to.
    private var reach: CGRect {
        placement.place(CGSize(width: style.widest, height: spot.height), widest: style.widest, in: area)
    }

    var body: some View {
        ZStack(alignment: .topLeading) {
            Color.black.opacity(0.55)
            GuideLines(area: area, lit: guides, dragging: grab != nil)

            Capsule(style: .continuous)
                .strokeBorder(.white.opacity(0.5), style: StrokeStyle(lineWidth: 1.5, dash: [4, 3]))
                .frame(width: reach.width, height: reach.height)
                .offset(x: reach.minX, y: reach.minY)

            Text(growth)
                .font(.callout)
                .foregroundStyle(.white)
                .padding(.horizontal, 10)
                .padding(.vertical, 5)
                .background(.black.opacity(0.65), in: Capsule())
                .fixedSize()
                .position(x: min(max(spot.midX, area.minX + 100), area.maxX - 100),
                          y: spot.midY < area.midY ? spot.maxY + 24 : spot.minY - 24)

            instructions
                .frame(width: Self.panelSize.width)
                .position(panelCenter)

            CapsulePill(style: style, status: "Drag to move", level: Self.breathing)
                .frame(width: spot.width, height: spot.height)
                .scaleEffect(grab == nil ? 1 : 1.04)
                .animation(.spring(duration: 0.25, bounce: 0), value: grab == nil)
                .offset(x: spot.minX, y: spot.minY)
                .gesture(drag)
                .onHover { inside in
                    if inside { NSCursor.openHand.push() } else { NSCursor.pop() }
                }
                .accessibilityLabel("Capsule")
                .accessibilityHint("Drag it, or use the arrow keys")
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .coordinateSpace(name: Self.space)
        .environment(\.colorScheme, .dark)
        .focusable()
        .focused($focused)
        .focusEffectDisabled()
        .onKeyPress(keys: [.leftArrow, .rightArrow, .upArrow, .downArrow]) { press in
            nudge(press)
            return .handled
        }
        .onAppear { focused = true }
    }

    private var instructions: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("Move the Capsule")
                .font(.title3.weight(.semibold))
            Text("Drag it anywhere on the screen. It snaps to the edges and the center. Next to the left or right edge, it grows away from that edge as your words appear.")
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)
            Text("Arrow keys nudge it, Shift moves further.")
                .font(.callout)
                .foregroundStyle(.secondary)
            HStack {
                Button("Reset") {
                    withAnimation(.spring(duration: 0.3, bounce: 0)) {
                        spot = CapsulePlacement.topCenter.place(spot.size, widest: style.widest, in: area)
                    }
                }
                Spacer()
                Button("Cancel") { finish(nil) }
                    .keyboardShortcut(.cancelAction)
                Button("Done") { finish(placement) }
                    .keyboardShortcut(.defaultAction)
                    .buttonStyle(.borderedProminent)
            }
            .controlSize(.large)
            .padding(.top, 8)
        }
        .padding(20)
        .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 16, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 16, style: .continuous).strokeBorder(.white.opacity(0.12)))
        .shadow(color: .black.opacity(0.35), radius: 24, y: 10)
    }

    private var growth: String {
        switch placement.horizontal {
        case .start: "Grows to the right  →"
        case .end: "←  Grows to the left"
        case .center: "←  Grows both ways  →"
        }
    }

    /// Mid-screen, unless the capsule or its reach is there; then well above or below them.
    private var panelCenter: CGPoint {
        let size = Self.panelSize
        let middle = CGPoint(x: area.midX, y: area.midY)
        let taken = spot.union(reach).insetBy(dx: -24, dy: -24)
        guard taken.intersects(CGRect(x: middle.x - size.width / 2, y: middle.y - size.height / 2,
                                      width: size.width, height: size.height)) else { return middle }
        let lower = taken.midY < area.midY
        return CGPoint(x: middle.x, y: lower ? area.maxY - area.height * 0.12 - size.height / 2
                                             : area.minY + area.height * 0.12 + size.height / 2)
    }

    private var drag: some Gesture {
        DragGesture(minimumDistance: 0, coordinateSpace: .named(Self.space))
            .onChanged { value in
                // Where it was grabbed stays under the pointer.
                let grab = self.grab ?? CGSize(width: value.startLocation.x - spot.minX, height: value.startLocation.y - spot.minY)
                self.grab = grab
                let moved = CGRect(origin: CGPoint(x: value.location.x - grab.width, y: value.location.y - grab.height),
                                   size: spot.size)
                let snapped = CapsulePlacement.snap(moved, in: area)
                spot = snapped.rect
                guides = snapped.guides
            }
            .onEnded { _ in
                grab = nil
                guides = []
                // Settles where it will really show: the placement keeps it, at its widest, on screen.
                withAnimation(.spring(duration: 0.3, bounce: 0)) {
                    spot = placement.place(spot.size, widest: style.widest, in: area)
                }
            }
    }

    private func nudge(_ press: KeyPress) {
        let step: CGFloat = press.modifiers.contains(.shift) ? 10 : 1
        var by = CGSize.zero
        switch press.key {
        case .leftArrow: by.width = -step
        case .rightArrow: by.width = step
        case .upArrow: by.height = -step
        case .downArrow: by.height = step
        default: return
        }
        let usable = CapsulePlacement.usable(area)
        spot.origin = CGPoint(x: min(max(spot.minX + by.width, usable.minX), max(usable.minX, usable.maxX - spot.width)),
                              y: min(max(spot.minY + by.height, usable.minY), max(usable.minY, usable.maxY - spot.height)))
    }

    /// The meter breathes, as if someone were talking.
    private static func breathing() -> Float {
        Float(0.06 + 0.04 * sin(Date.timeIntervalSinceReferenceDate * 2.2))
    }
}

/// The edges and centre lines a dragged capsule snaps to. The centre lines show
/// faintly while dragging; a line lights up while the capsule rests on it.
private struct GuideLines: View {
    let area: CGRect
    let lit: CapsulePlacement.Guides
    let dragging: Bool

    private struct Line: Identifiable {
        let id: Int
        let guide: CapsulePlacement.Guides
        let from: CGPoint
        let to: CGPoint
    }

    var body: some View {
        let usable = CapsulePlacement.usable(area)
        let lines = [
            Line(id: 0, guide: .left, from: CGPoint(x: usable.minX, y: area.minY), to: CGPoint(x: usable.minX, y: area.maxY)),
            Line(id: 1, guide: .centerX, from: CGPoint(x: area.midX, y: area.minY), to: CGPoint(x: area.midX, y: area.maxY)),
            Line(id: 2, guide: .right, from: CGPoint(x: usable.maxX, y: area.minY), to: CGPoint(x: usable.maxX, y: area.maxY)),
            Line(id: 3, guide: .top, from: CGPoint(x: area.minX, y: usable.minY), to: CGPoint(x: area.maxX, y: usable.minY)),
            Line(id: 4, guide: .centerY, from: CGPoint(x: area.minX, y: area.midY), to: CGPoint(x: area.maxX, y: area.midY)),
            Line(id: 5, guide: .bottom, from: CGPoint(x: area.minX, y: usable.maxY), to: CGPoint(x: area.maxX, y: usable.maxY)),
        ]
        ZStack {
            ForEach(lines) { line in
                let on = lit.contains(line.guide)
                if on || (dragging && (line.guide == .centerX || line.guide == .centerY)) {
                    Path { path in
                        path.move(to: line.from)
                        path.addLine(to: line.to)
                    }
                    .stroke(on ? Color.accentColor : .white.opacity(0.22),
                            style: StrokeStyle(lineWidth: 1, dash: on ? [] : [6, 6]))
                }
            }
        }
    }
}
