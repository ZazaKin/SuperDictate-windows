import Foundation
#if canImport(CoreGraphics)
import CoreGraphics // CGRect's geometry; Foundation has it elsewhere.
#endif

/// Which side of the screen the capsule keeps to, on one axis.
public enum CapsuleEdge: String, Sendable, CaseIterable {
    /// Left, or top.
    case start
    case center
    /// Right, or bottom.
    case end
}

/// Where the capsule sits, independent of any screen's size, so one choice works
/// on every display. On each axis it keeps to an edge (or the centre), and `x`
/// and `y` say how far across the screen (0 to 1) that edge of the capsule is.
///
/// That edge is what holds still while the capsule grows with the words: against
/// the left side it grows to the right, against the right side to the left, in
/// the middle both ways. Whatever its size, it stays on screen, a margin away
/// from the edges.
///
/// Rectangles here run top down (y grows downward), as on Windows; the panel
/// flips them for AppKit. The Windows app's `CapsulePlacement` has the same rules
/// and numbers.
public struct CapsulePlacement: Equatable, Sendable {
    /// Gap kept between the capsule and the edges of the visible screen (points).
    public static let margin: CGFloat = 12

    /// How close to an edge or centre line a dragged capsule snaps to it (points).
    public static let snapDistance: CGFloat = 14

    public static let topCenter = CapsulePlacement(horizontal: .center, x: 0.5, vertical: .start, y: 0)

    public struct Preset: Identifiable, Sendable {
        public let id: String
        public let name: String
        public let placement: CapsulePlacement
    }

    public static let presets = [
        Preset(id: "top", name: "Top Center", placement: topCenter),
        Preset(id: "top-left", name: "Top Left", placement: CapsulePlacement(horizontal: .start, x: 0, vertical: .start, y: 0)),
        Preset(id: "top-right", name: "Top Right", placement: CapsulePlacement(horizontal: .end, x: 1, vertical: .start, y: 0)),
        Preset(id: "bottom", name: "Bottom Center", placement: CapsulePlacement(horizontal: .center, x: 0.5, vertical: .end, y: 1)),
        Preset(id: "bottom-left", name: "Bottom Left", placement: CapsulePlacement(horizontal: .start, x: 0, vertical: .end, y: 1)),
        Preset(id: "bottom-right", name: "Bottom Right", placement: CapsulePlacement(horizontal: .end, x: 1, vertical: .end, y: 1)),
    ]

    public var horizontal: CapsuleEdge
    public var x: Double
    public var vertical: CapsuleEdge
    public var y: Double

    public init(horizontal: CapsuleEdge, x: Double, vertical: CapsuleEdge, y: Double) {
        self.horizontal = horizontal
        self.x = x
        self.vertical = vertical
        self.y = y
    }

    /// The capsule is against the top (or bottom) of the screen, so it comes in from there.
    public var atTop: Bool { vertical == .start && y <= 0.001 }

    public var atBottom: Bool { vertical == .end && y >= 0.999 }

    /// The capsule's rectangle in this visible area. Its anchored edge goes where
    /// `x` and `y` say, pulled back so that even at `widest` the capsule fits.
    public func place(_ size: CGSize, widest: CGFloat, in area: CGRect) -> CGRect {
        let usable = Self.usable(area)
        let reach = min(max(widest, size.width), usable.width)

        let at = usable.minX + CGFloat(clamp(x)) * usable.width
        // Clamp the edge that holds still, so the capsule at its widest stays inside.
        let left: CGFloat = switch horizontal {
        case .start: clamp(at, usable.minX, usable.maxX - reach)
        case .end: clamp(at, usable.minX + reach, usable.maxX) - size.width
        case .center: clamp(at, usable.minX + reach / 2, usable.maxX - reach / 2) - size.width / 2
        }

        let down = usable.minY + CGFloat(clamp(y)) * usable.height
        let top: CGFloat = switch vertical {
        case .start: down
        case .end: down - size.height
        case .center: down - size.height / 2
        }

        return CGRect(x: left, y: clamp(top, usable.minY, max(usable.minY, usable.maxY - size.height)),
                      width: size.width, height: size.height)
    }

    /// The placement for a capsule left at this rectangle. Its third of the screen
    /// decides the edge it keeps to: left third the left edge, right third the
    /// right edge, between them its centre. The same goes for top and bottom.
    public init(_ capsule: CGRect, in area: CGRect) {
        let usable = Self.usable(area)
        let across = Self.along(capsule.minX, capsule.maxX, usable.minX, usable.width)
        let down = Self.along(capsule.minY, capsule.maxY, usable.minY, usable.height)
        self.init(horizontal: across.0, x: across.1, vertical: down.0, y: down.1)
    }

    /// Which guides a dragged capsule is resting on.
    public struct Guides: OptionSet, Sendable {
        public let rawValue: Int
        public init(rawValue: Int) { self.rawValue = rawValue }

        public static let left = Guides(rawValue: 1)
        public static let centerX = Guides(rawValue: 2)
        public static let right = Guides(rawValue: 4)
        public static let top = Guides(rawValue: 8)
        public static let centerY = Guides(rawValue: 16)
        public static let bottom = Guides(rawValue: 32)
    }

    /// A dragged capsule, kept inside the visible area and pulled onto an edge or
    /// a centre line when it comes within ``snapDistance`` of one.
    public static func snap(_ capsule: CGRect, in area: CGRect) -> (rect: CGRect, guides: Guides) {
        let usable = Self.usable(area)
        var guides: Guides = []
        var left = clamp(capsule.minX, usable.minX, max(usable.minX, usable.maxX - capsule.width))
        var top = clamp(capsule.minY, usable.minY, max(usable.minY, usable.maxY - capsule.height))

        if abs(left - usable.minX) <= snapDistance {
            left = usable.minX
            guides.insert(.left)
        } else if abs(left + capsule.width - usable.maxX) <= snapDistance {
            left = usable.maxX - capsule.width
            guides.insert(.right)
        } else if abs(left + capsule.width / 2 - area.midX) <= snapDistance {
            left = area.midX - capsule.width / 2
            guides.insert(.centerX)
        }

        if abs(top - usable.minY) <= snapDistance {
            top = usable.minY
            guides.insert(.top)
        } else if abs(top + capsule.height - usable.maxY) <= snapDistance {
            top = usable.maxY - capsule.height
            guides.insert(.bottom)
        } else if abs(top + capsule.height / 2 - area.midY) <= snapDistance {
            top = area.midY - capsule.height / 2
            guides.insert(.centerY)
        }

        return (CGRect(x: left, y: top, width: capsule.width, height: capsule.height), guides)
    }

    public static func usable(_ area: CGRect) -> CGRect {
        let usable = area.insetBy(dx: margin, dy: margin)
        return usable.isNull || usable.isEmpty ? area : usable
    }

    private static func along(_ start: CGFloat, _ end: CGFloat, _ origin: CGFloat, _ length: CGFloat) -> (CapsuleEdge, Double) {
        guard length > 0 else { return (.center, 0.5) }
        let middle = Double(((start + end) / 2 - origin) / length)
        if middle < 1.0 / 3 { return (.start, clamp(Double((start - origin) / length))) }
        if middle > 2.0 / 3 { return (.end, clamp(Double((end - origin) / length))) }
        return (.center, clamp(middle))
    }
}

/// Stored as "center 0.5 start 0", one UserDefaults string.
extension CapsulePlacement: RawRepresentable {
    public init?(rawValue: String) {
        let parts = rawValue.split(separator: " ").map(String.init)
        guard parts.count == 4,
              let horizontal = CapsuleEdge(rawValue: parts[0]), let x = Double(parts[1]),
              let vertical = CapsuleEdge(rawValue: parts[2]), let y = Double(parts[3])
        else { return nil }
        self.init(horizontal: horizontal, x: x, vertical: vertical, y: y)
    }

    public var rawValue: String { "\(horizontal.rawValue) \(x) \(vertical.rawValue) \(y)" }
}

private func clamp<T: Comparable>(_ value: T, _ lowest: T, _ highest: T) -> T {
    min(max(value, lowest), highest)
}

private func clamp(_ fraction: Double) -> Double {
    clamp(fraction, 0, 1)
}
