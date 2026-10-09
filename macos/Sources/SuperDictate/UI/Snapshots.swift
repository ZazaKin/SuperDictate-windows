import AppKit
import SuperDictateCore
import SwiftUI

/// `SuperDictate --snapshot <folder>`: draws the main screens, in light and
/// dark, to PNG files and quits. The GitHub build runs it so the interface can
/// be checked without a Mac at hand. It shows made-up example states, starts
/// no recording, asks for no permission and saves nothing else.
@MainActor
enum Snapshots {
    static var folder: URL? {
        let arguments = CommandLine.arguments
        guard let index = arguments.firstIndex(of: "--snapshot"), index + 1 < arguments.count else { return nil }
        return URL(fileURLWithPath: arguments[index + 1], isDirectory: true)
    }

    static func render(to folder: URL) {
        try? FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        // A few languages chosen, for the Languages tab. Registered defaults are never saved.
        UserDefaults.standard.register(defaults: [Preferences.Key.languages: "en,de,fr,pl,ru,uk"])

        let setup = DictationModel(history: History(file: nil))
        setup.showcase(setUp: false)
        let model = DictationModel(history: History(file: nil))
        model.showcase(setUp: true)

        for (name, appearance) in [("light", NSAppearance.Name.aqua), ("dark", NSAppearance.Name.darkAqua)] {
            func save<V: View>(_ view: V, _ title: String) {
                write(view.environment(model), to: folder.appendingPathComponent("\(title)-\(name).png"), appearance: appearance)
            }

            write(WelcomeView(close: {}).environment(setup), to: folder.appendingPathComponent("welcome-\(name).png"),
                  appearance: appearance)
            save(MenuBarPanel(), "menu-bar")
            save(GeneralPane().frame(width: 620), "settings-general")
            save(DictationPane().frame(width: 620), "settings-dictation")
            // The whole page, rather than the part a 600-point window shows at once.
            save(LanguagesPane().frame(width: 620, height: 820), "settings-languages")
            save(CapsulePane().frame(width: 620, height: 1080), "settings-capsule")
            save(ModelPane().frame(width: 620), "settings-model")
            save(HistoryPane().frame(width: 620), "settings-history")
            save(AboutPane().frame(width: 620), "settings-about")
            // The capsule over a stand-in for whatever app is underneath.
            save(CapsuleView()
                    .frame(width: 560, height: 120)
                    .background(LinearGradient(colors: [.indigo, .teal], startPoint: .topLeading, endPoint: .bottomTrailing)),
                 "capsule")
        }

        write(SkinSheet(), to: folder.appendingPathComponent("capsule-skins.png"), appearance: .darkAqua)
        // The position editor on a 1440 × 900 screen with a menu bar, the capsule kept to the left edge.
        write(CapsuleLayoutView(area: CGRect(x: 0, y: 24, width: 1440, height: 876),
                                placement: CapsulePlacement(horizontal: .start, x: 0, vertical: .start, y: 0.28),
                                style: CapsuleStyle(skin: .find("aurora")), finish: { _ in })
                .frame(width: 1440, height: 900)
                .background(wallpaper),
              to: folder.appendingPathComponent("capsule-editor.png"), appearance: .darkAqua)
    }

    private static let wallpaper = LinearGradient(colors: [.indigo, .teal], startPoint: .topLeading, endPoint: .bottomTrailing)

    /// Every skin with words in it, over a stand-in for whatever is underneath.
    private struct SkinSheet: View {
        var body: some View {
            var caption = Caption()
            caption.show(settled: "Lunch at noon, then", tail: "the design review")
            let meters = CapsuleStyle.Meter.allCases
            return LazyVGrid(columns: [GridItem(.fixed(380), spacing: 24), GridItem(.fixed(380))], spacing: 18) {
                ForEach(Array(CapsuleSkin.all.enumerated()), id: \.element.id) { index, skin in
                    VStack(spacing: 6) {
                        CapsulePill(style: CapsuleStyle(skin: skin, meter: meters[index % meters.count], timer: index < 2),
                                    caption: caption, started: Date(timeIntervalSinceNow: -14), live: false, level: { 0.1 })
                        Text(skin.name).font(.caption).foregroundStyle(.white.opacity(0.85))
                    }
                }
            }
            .padding(32)
            .background(wallpaper)
        }
    }

    private static func write<V: View>(_ view: V, to file: URL, appearance: NSAppearance.Name) {
        let host = NSHostingView(rootView: view
            .environment(\.paintsGlass, true)
            .background(Color(nsColor: .windowBackgroundColor)))
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 600, height: 600),
                              styleMask: [.borderless], backing: .buffered, defer: false)
        window.appearance = NSAppearance(named: appearance)
        window.contentView = host
        let size = host.fittingSize
        window.setContentSize(size)
        host.frame = NSRect(origin: .zero, size: size)
        host.layoutSubtreeIfNeeded()

        // Twice the points, as on a Retina screen, whatever the runner's display is.
        guard let bitmap = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: Int(size.width * 2), pixelsHigh: Int(size.height * 2),
                                            bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
                                            colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)
        else { return }
        bitmap.size = size
        host.cacheDisplay(in: host.bounds, to: bitmap)
        try? bitmap.representation(using: .png, properties: [:])?.write(to: file)
    }
}
