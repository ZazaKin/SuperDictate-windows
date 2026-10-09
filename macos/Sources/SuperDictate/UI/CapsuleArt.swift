import SuperDictateCore
import SwiftUI

/// The moving picture inside an art skin, under the words: light pools under grain,
/// a horizon, chrome ribbons, a honeycomb, a pixel wave, tiles, a sky. The same
/// pictures, from the same numbers, as on Windows (`Ui/CapsuleArt.cs`). The voice
/// lifts or brightens them; with Reduce Motion on they hold still. A calm lane runs
/// behind the words so they always read: the art stays vivid above and below them
/// and behind the meter.
struct CapsuleArt: View {
    let art: String
    /// The accent color, for a picture that is the voice meter itself (Halftone).
    let accent: Color
    /// False draws a still, for the skin tiles.
    let live: Bool
    let processing: Bool
    let level: () -> Float
    @Environment(\.accessibilityReduceMotion) private var calm

    var body: some View {
        if live && !calm {
            TimelineView(.animation) { timeline in
                canvas(at: timeline.date.timeIntervalSinceReferenceDate)
            }
        } else {
            canvas(at: live ? 0 : 0.35)
        }
    }

    private func canvas(at seconds: Double) -> some View {
        let voice = processing ? 0 : min(Double(level()) * 6, 1)
        return Canvas { context, size in
            ArtPainter.paint(art, in: context, size: size, seconds: seconds, level: voice, accent: accent)
        }
    }
}

private enum ArtPainter {
    static func paint(_ art: String, in context: GraphicsContext, size: CGSize, seconds t: Double, level: Double, accent: Color) {
        guard size.width > 0, size.height > 0 else { return }
        switch art {
        case "bloom": bloom(context, size, t, level)
        case "eclipse": eclipse(context, size, t, level)
        case "chrome": chrome(context, size, t, level)
        case "hive": hive(context, size, t, level)
        case "halftone": halftone(context, size, t, level, accent)
        case "mosaic": mosaic(context, size, t, level)
        case "mesa": mesa(context, size, t, level)
        default: break
        }
    }

    // MARK: - The pictures

    /// Grainy pastel light pools, after a mesh-gradient poster: they drift, and swell as you speak.
    private static func bloom(_ context: GraphicsContext, _ size: CGSize, _ t: Double, _ level: Double) {
        let (w, h) = (size.width, size.height)
        fill(context, size, color(0xF6EEF0))
        for (index, pool) in [0x7C97FF, 0xFF7A6B, 0x6FE0C4, 0xFFB37E, 0xB98AFF].enumerated() {
            let i = Double(index)
            let extent = h * (2.3 + 0.5 * level + 0.25 * sin(t * 0.7 + i))
            let x = w * (0.08 + 0.22 * i + 0.07 * sin(t * 0.45 + i * 1.7))
            let y = h * (0.5 + 0.45 * cos(t * 0.55 + i * 2.1))
            glow(context, center: CGPoint(x: x, y: y), width: extent * 1.7, height: extent, color: UInt32(pool), alpha: 1)
        }
        grain(context, size, opacity: 0.3)
        lane(context, size, 0xF6EEF0, strength: 0.45)
    }

    /// A planet's lit edge low across the capsule; the light rises with the voice and a glint travels along it.
    private static func eclipse(_ context: GraphicsContext, _ size: CGSize, _ t: Double, _ level: Double) {
        let (w, h) = (size.width, size.height)
        context.fill(Path(CGRect(origin: .zero, size: size)),
                     with: .linearGradient(Gradient(colors: [color(0x02040A), color(0x061430)]),
                                           startPoint: .zero, endPoint: CGPoint(x: 0, y: h)))
        glow(context, center: CGPoint(x: w * (0.5 + 0.18 * sin(t * 0.35)), y: h * 0.9), width: w * 0.95,
             height: h * (1.3 + 0.7 * level), color: 0x2560D8, alpha: 190.0 / 255 * (0.55 + 0.45 * level))
        let radius = w * 1.4
        context.fill(Path(ellipseIn: CGRect(x: w / 2 - radius, y: h * 0.9 + 0.6, width: radius * 2, height: radius * 2)),
                     with: .color(color(0x010207)))
        // A glint runs along the rim and back.
        let centre = 0.5 + 0.35 * sin(t * 0.5)
        let rim = Gradient(stops: stops([(0x2F7BFF, 0, centre - 0.35), (0x9CCBFF, 170, centre - 0.12), (0xFFFFFF, 255, centre),
                                         (0x9CCBFF, 170, centre + 0.12), (0x2F7BFF, 0, centre + 0.35)]))
        context.stroke(Path(ellipseIn: CGRect(x: w / 2 - radius, y: h * 0.9, width: radius * 2, height: radius * 2)),
                       with: .linearGradient(rim, startPoint: CGPoint(x: w / 2 - radius, y: 0), endPoint: CGPoint(x: w / 2 + radius, y: 0)),
                       lineWidth: max(1, h * 0.03))
        lane(context, size, 0x02040A, strength: 0.3)
    }

    /// Curved black ribbons with iridescent edges, after dark chrome: they undulate, a glint slides across.
    private static func chrome(_ context: GraphicsContext, _ size: CGSize, _ t: Double, _ level: Double) {
        let (w, h) = (size.width, size.height)
        fill(context, size, color(0x07080B))
        // Rings around a point above and right of the capsule, so their arcs sweep across it.
        let centre = CGPoint(x: w * 1.1, y: -h * 2.1)
        let thickness = h * 0.55
        let edge = (150 + 105 * level) / 255.0
        // Enough ribbons to cross the whole capsule however wide it grows: from the corner nearest
        // their centre to the one farthest away, one ribbon width apart.
        let step = h * 0.62
        let nearest = ((w * 0.1) * (w * 0.1) + (h * 2.1) * (h * 2.1)).squareRoot() - step
        let farthest = ((w * 1.1) * (w * 1.1) + (h * 3.1) * (h * 3.1)).squareRoot() + step
        let count = Int(((farthest - nearest) / step).rounded(.up)) + 1
        for index in 0 ..< count {
            let k = Double(index)
            let radius = nearest + k * step + h * 0.08 * sin(t * 0.8 + k)
            func at(_ fraction: Double) -> Double { min(max((radius - thickness * fraction) / radius, 0), 1) }
            let shading = Gradient(stops: [
                .init(color: color(0x0B0C0F), location: at(1)),
                .init(color: color(0x23262C), location: at(0.55)),
                .init(color: color(0xE8A46A, edge), location: at(0.22)),
                .init(color: color(0xFFFFFF, edge), location: at(0.15)),
                .init(color: color(0x78AEFF, edge), location: at(0.08)),
                .init(color: color(0x0B0C0F), location: 1),
            ])
            let middle = radius - thickness / 2
            context.stroke(Path(ellipseIn: CGRect(x: centre.x - middle, y: centre.y - middle, width: middle * 2, height: middle * 2)),
                           with: .radialGradient(shading, center: centre, startRadius: 0, endRadius: radius), lineWidth: thickness)
        }
        // Everywhere a little light, and a band of full light that slides through.
        let sweep = (t * 0.22).truncatingRemainder(dividingBy: 1.4) - 0.2
        let shade = Gradient(stops: stops([(0x000000, 145, 0), (0x000000, 145, sweep - 0.12), (0x000000, 0, sweep),
                                           (0x000000, 145, sweep + 0.12), (0x000000, 145, 1)]))
        context.fill(Path(CGRect(origin: .zero, size: size)),
                     with: .linearGradient(shade, startPoint: .zero, endPoint: CGPoint(x: w, y: h * 0.4)))
        lane(context, size, 0x07080B, strength: 0.7)
    }

    /// A honeycomb on black, lit by a violet-blue light that wanders and brightens with the voice.
    private static func hive(_ context: GraphicsContext, _ size: CGSize, _ t: Double, _ level: Double) {
        let (w, h) = (size.width, size.height)
        fill(context, size, color(0x050509))
        let cells = honeycomb(size, radius: h / 7)
        context.fill(cells, with: .color(color(0xFFFFFF, 22.0 / 255)))
        var lit = context
        lit.clip(to: cells)
        lit.opacity = 0.7 + 0.3 * level
        let radiusX = w * (0.28 + 0.12 * level)
        let radiusY = h * 1.4
        lit.translateBy(x: w * (0.5 + 0.4 * sin(t * 0.55)), y: h * (0.55 + 0.2 * sin(t * 0.9)))
        lit.scaleBy(x: radiusX / radiusY, y: 1)
        let light = Gradient(stops: [
            .init(color: color(0x6A2FD6), location: 0),
            .init(color: color(0x3F38D0, 200.0 / 255), location: 0.4),
            .init(color: color(0x2450D8, 90.0 / 255), location: 0.7),
            .init(color: color(0x2450D8, 0), location: 1),
        ])
        lit.fill(Path(ellipseIn: CGRect(x: -radiusY, y: -radiusY, width: radiusY * 2, height: radiusY * 2)),
                 with: .radialGradient(light, center: .zero, startRadius: 0, endRadius: radiusY))
        lane(context, size, 0x050509, strength: 0.65)
    }

    /// The 4 × 4 ordered-dither thresholds.
    private static let bayer = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]

    /// A wave in ordered dither on navy, after a halftone poster, in the accent color: it rolls, and rises
    /// and falls with the voice, so it is the voice meter itself.
    private static func halftone(_ context: GraphicsContext, _ size: CGSize, _ t: Double, _ level: Double, _ accent: Color) {
        let rows = 14
        let cell = size.height / CGFloat(rows)
        let columns = Int((size.width / cell).rounded(.up))
        fill(context, size, color(0x0A1022))
        var dots = Path()
        for row in 0 ..< rows {
            let height = 1 - (Double(row) + 0.5) / Double(rows)
            for column in 0 ..< columns {
                let c = Double(column)
                // A pink mass under the calm lane, its dithered fringe climbing around the meter as the voice comes.
                let crest = 0.3 + 0.12 * sin(c * 0.11 + t * 1.1) + 0.07 * sin(c * 0.29 - t * 1.7) + 0.55 * level
                let density = min(max(0.5 + (crest - height) * 2.6, 0), 1)
                if (Double(bayer[row & 3][column & 3]) + 0.5) / 16 < density {
                    dots.addRect(CGRect(x: CGFloat(column) * cell, y: CGFloat(row) * cell, width: cell * 0.75, height: cell * 0.75))
                }
            }
        }
        context.fill(dots, with: .color(accent))
        lane(context, size, 0x0A1022, strength: 0.75)
    }

    /// Lavender-to-pink tiles with a shimmer running across them diagonally, after a tile-grid poster.
    private static func mosaic(_ context: GraphicsContext, _ size: CGSize, _ t: Double, _ level: Double) {
        let rows = 6
        let cell = size.height / CGFloat(rows)
        let columns = Int((size.width / cell).rounded(.up))
        fill(context, size, color(0xFBF7FB))
        let (lavender, rose, pink) = (RGBA(0xF2EEF9), RGBA(0xF6B3CC), RGBA(0xF07AA5))
        for row in 0 ..< rows {
            for column in 0 ..< columns {
                let (c, r) = (Double(column), Double(row))
                let across = (c + r * 0.6) / max(1, Double(columns) + Double(rows) * 0.6 - 1)
                let base = across < 0.5 ? mix(lavender, rose, across * 2) : mix(rose, pink, (across - 0.5) * 2)
                let shimmer = pow(0.5 + 0.5 * sin(c * 0.45 + r * 0.9 - t * 2.4), 6)
                let tile = mix(base, .white, 0.45 * shimmer + 0.1 * level)
                context.fill(Path(roundedRect: CGRect(x: CGFloat(column) * cell, y: CGFloat(row) * cell,
                                                      width: cell * 0.875, height: cell * 0.875), cornerRadius: cell * 0.15),
                             with: .color(Color(tile)))
            }
        }
        lane(context, size, 0xFBF7FB, strength: 0.5)
    }

    /// A pale sky over a terracotta horizon, after a landscape poster: soft clouds drift by.
    private static func mesa(_ context: GraphicsContext, _ size: CGSize, _ t: Double, _ level: Double) {
        let (w, h) = (size.width, size.height)
        context.fill(Path(CGRect(origin: .zero, size: size)),
                     with: .linearGradient(Gradient(colors: [color(0xC5D6E6), color(0xF2E9E2)]),
                                           startPoint: .zero, endPoint: CGPoint(x: 0, y: h)))
        for index in 0 ..< 4 {
            let i = Double(index)
            let cloudWidth = w * (0.38 + 0.1 * i)
            let cloudHeight = h * (0.95 + 0.2 * Double(index % 2))
            let travel = w * 1.5 + cloudWidth
            let x = ((i * w * 0.42 - t * w * 0.035).truncatingRemainder(dividingBy: travel) + travel)
                .truncatingRemainder(dividingBy: travel) - cloudWidth
            let y = h * (0.05 + 0.18 * Double(index % 3)) - cloudHeight * 0.2
            glow(context, center: CGPoint(x: x + cloudWidth / 2, y: y + cloudHeight / 2), width: cloudWidth, height: cloudHeight,
                 color: 0xFFFFFF, alpha: 1)
        }
        // A far hill, sunlit, and the near land in shadow, under the words.
        context.fill(hills(size, level: 0.8, rise: 0.06, waves: 1.7, shift: 2.1),
                     with: .linearGradient(Gradient(colors: [color(0xE6A07E), color(0xCF7A5C)]),
                                           startPoint: CGPoint(x: 0, y: h * 0.74), endPoint: CGPoint(x: 0, y: h)))
        let land = hills(size, level: 0.88, rise: 0.04, waves: 2.6, shift: 0.8)
        context.fill(land, with: .linearGradient(Gradient(colors: [color(0xB8473A), color(0x7E2820)]),
                                                 startPoint: CGPoint(x: 0, y: h * 0.84), endPoint: CGPoint(x: 0, y: h)))
        context.stroke(land, with: .color(color(0xE08A63)), lineWidth: max(0.8, h * 0.02))
        grain(context, size, opacity: 0.25)
    }

    // MARK: - Helpers

    private static func color(_ rgb: UInt32, _ alpha: Double = 1) -> Color {
        Color(RGBA(rgb)).opacity(alpha)
    }

    private static func fill(_ context: GraphicsContext, _ size: CGSize, _ color: Color) {
        context.fill(Path(CGRect(origin: .zero, size: size)), with: .color(color))
    }

    /// Gradient stops from (color, alpha 0–255, location), kept in order inside 0…1.
    private static func stops(_ list: [(UInt32, Double, Double)]) -> [Gradient.Stop] {
        var last = 0.0
        return list.map { entry in
            last = max(last, min(max(entry.2, 0), 1))
            return Gradient.Stop(color: color(entry.0, entry.1 / 255), location: last)
        }
    }

    /// A soft round light: the color in the middle, nothing at the edge.
    private static func glow(_ context: GraphicsContext, center: CGPoint, width: CGFloat, height: CGFloat, color rgb: UInt32,
                             alpha: Double) {
        var context = context
        context.translateBy(x: center.x, y: center.y)
        context.scaleBy(x: width / height, y: 1)
        let radius = height / 2
        let gradient = Gradient(stops: [
            .init(color: color(rgb, alpha), location: 0),
            .init(color: color(rgb, alpha * 0.45), location: 0.45),
            .init(color: color(rgb, 0), location: 1),
        ])
        context.fill(Path(ellipseIn: CGRect(x: -radius, y: -radius, width: radius * 2, height: radius * 2)),
                     with: .radialGradient(gradient, center: .zero, startRadius: 0, endRadius: radius))
    }

    /// A calm lane along the middle, where the words run: the skin's base color over the art.
    private static func lane(_ context: GraphicsContext, _ size: CGSize, _ rgb: UInt32, strength: Double) {
        let gradient = Gradient(stops: [
            .init(color: color(rgb, 0), location: 0.16),
            .init(color: color(rgb, strength), location: 0.3),
            .init(color: color(rgb, strength), location: 0.72),
            .init(color: color(rgb, 0), location: 0.88),
        ])
        context.fill(Path(CGRect(origin: .zero, size: size)),
                     with: .linearGradient(gradient, startPoint: .zero, endPoint: CGPoint(x: 0, y: size.height)))
    }

    private static func mix(_ from: RGBA, _ to: RGBA, _ amount: Double) -> RGBA {
        let amount = min(max(amount, 0), 1)
        func channel(_ a: UInt8, _ b: UInt8) -> UInt32 { UInt32((Double(a) + (Double(b) - Double(a)) * amount).rounded()) }
        return RGBA(channel(from.red, to.red) << 16 | channel(from.green, to.green) << 8 | channel(from.blue, to.blue))
    }

    /// Hexagons with a hairline gap: pointy-top, each row shifted half a cell.
    private static func honeycomb(_ size: CGSize, radius: CGFloat) -> Path {
        var path = Path()
        let across = 3.0.squareRoot() * radius
        let rows = Int(size.height / (1.5 * radius)) + 2
        let columns = Int(size.width / across) + 2
        for row in -1 ... rows {
            for column in -1 ... columns {
                let cx = CGFloat(column) * across + (row.isMultiple(of: 2) ? 0 : across / 2)
                let cy = CGFloat(row) * 1.5 * radius
                for corner in 0 ..< 6 {
                    let angle = Double.pi / 180 * Double(60 * corner - 90)
                    let point = CGPoint(x: cx + radius * 0.84 * cos(angle), y: cy + radius * 0.84 * sin(angle))
                    if corner == 0 { path.move(to: point) } else { path.addLine(to: point) }
                }
                path.closeSubpath()
            }
        }
        return path
    }

    /// Rolling land, from `level` down to the bottom edge.
    private static func hills(_ size: CGSize, level: Double, rise: Double, waves: Double, shift: Double) -> Path {
        let (w, h) = (size.width, size.height)
        var path = Path()
        path.move(to: CGPoint(x: 0, y: h))
        for step in 0 ... 48 {
            let x = w * CGFloat(step) / 48
            let y = h * (level + rise * sin(x / w * .pi * waves + shift) + 0.015 * sin(x / w * .pi * 7))
            path.addLine(to: CGPoint(x: x, y: y))
        }
        path.addLine(to: CGPoint(x: w, y: h))
        path.closeSubpath()
        return path
    }

    /// Film grain: a fixed speckle, the same every run, tiled.
    private static let grainImage: Image = {
        let side = 96
        var state: UInt64 = 7
        func next() -> Int {
            state = state &* 6_364_136_223_846_793_005 &+ 1_442_695_040_888_963_407
            return Int(state >> 33)
        }
        var pixels = [UInt8](repeating: 0, count: side * side * 4)
        for index in 0 ..< side * side {
            let light = next() % 2 == 0
            let alpha = UInt8(next() % 70)
            let value = light ? alpha : 0 // Premultiplied: white at this alpha, or black.
            pixels[index * 4] = value
            pixels[index * 4 + 1] = value
            pixels[index * 4 + 2] = value
            pixels[index * 4 + 3] = alpha
        }
        guard let provider = CGDataProvider(data: Data(pixels) as CFData),
              let image = CGImage(width: side, height: side, bitsPerComponent: 8, bitsPerPixel: 32, bytesPerRow: side * 4,
                                  space: CGColorSpaceCreateDeviceRGB(),
                                  bitmapInfo: CGBitmapInfo(rawValue: CGImageAlphaInfo.premultipliedLast.rawValue),
                                  provider: provider, decode: nil, shouldInterpolate: false, intent: .defaultIntent)
        else { return Image(systemName: "circle") }
        return Image(decorative: image, scale: 2)
    }()

    private static func grain(_ context: GraphicsContext, _ size: CGSize, opacity: Double) {
        var context = context
        context.opacity = opacity
        context.fill(Path(CGRect(origin: .zero, size: size)),
                     with: .tiledImage(grainImage, origin: .zero, sourceRect: CGRect(x: 0, y: 0, width: 1, height: 1), scale: 1))
    }
}
