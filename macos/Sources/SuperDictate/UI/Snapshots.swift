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
            let meters: [CapsuleStyle.Meter] = [.bars, .wave, .pulse]
            return LazyVGrid(columns: [GridItem(.fixed(380), spacing: 24), GridItem(.fixed(380))], spacing: 18) {
                ForEach(Array(CapsuleSkin.all.enumerated()), id: \.element.id) { index, skin in
                    VStack(spacing: 6) {
                        CapsulePill(style: CapsuleStyle(skin: skin, meter: meters[index % 3], timer: index < 2),
                                    caption: caption, started: Date(timeIntervalSinceNow: -14), live: false, level: { 0.1 })
                        Text(skin.name).font(.caption).foregroundStyle(.white.opacity(0.85))
                    }
                }
            }
            .padding(32)
            .background(wallpaper)
        }
    }

    // MARK: - Liquid Glass

    /// `SuperDictate --showcase`: the Liquid Glass capsule on a bright wallpaper,
    /// in a window over the top left of the main screen, left open for the GitHub
    /// build to photograph with `screencapture`. The window server draws glass,
    /// so it can't be drawn straight to a file like the other pictures.
    static var showcase: Bool { CommandLine.arguments.contains("--showcase") }

    private static var showcaseWindow: NSWindow?

    static func showShowcase() {
        guard let screen = NSScreen.screens.first else { return }
        let size = NSSize(width: 960, height: 420)
        let window = NSWindow(contentRect: NSRect(x: screen.frame.minX, y: screen.frame.maxY - size.height,
                                                  width: size.width, height: size.height),
                              styleMask: .borderless, backing: .buffered, defer: false)
        window.level = .screenSaver
        window.contentView = NSHostingView(rootView: GlassShowcase())
        window.orderFrontRegardless()
        showcaseWindow = window
    }

    private struct GlassShowcase: View {
        var body: some View {
            var caption = Caption()
            caption.show(settled: "Move the design review to Thursday,", tail: "and send the mockups")
            let style = CapsuleStyle(skin: .find("liquid"), scale: 1.3)
            return ZStack {
                // Bright shapes behind, for the glass to bend and blur.
                LinearGradient(colors: [Color(RGBA(0x4F46E5)), Color(RGBA(0x0D9488))], startPoint: .topLeading, endPoint: .bottomTrailing)
                Circle().fill(Color(RGBA(0xF59E0B))).frame(width: 260).offset(x: -250, y: -60)
                Circle().fill(Color(RGBA(0xEC4899))).frame(width: 200).offset(x: 260, y: 90)
                RoundedRectangle(cornerRadius: 30).fill(.white.opacity(0.9)).frame(width: 180, height: 60).offset(x: 40, y: -120)
                VStack(spacing: 28) {
                    CapsulePill(style: style, caption: caption, live: false, level: { 0.1 })
                    HStack(spacing: 24) {
                        CapsulePill(style: CapsuleStyle(skin: .find("liquid"), scale: 1.3, meter: .wave), live: false, level: { 0.1 })
                        CapsulePill(style: CapsuleStyle(skin: .find("liquid"), scale: 1.3, meter: .pulse, timer: true),
                                    status: "Processing…", started: Date(timeIntervalSinceNow: -42), live: false, level: { 0.1 })
                    }
                }
            }
            .frame(width: 960, height: 420)
            .environment(\.colorScheme, .dark)
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

        guard let bitmap = host.bitmapImageRepForCachingDisplay(in: host.bounds) else { return }
        host.cacheDisplay(in: host.bounds, to: bitmap)
        try? bitmap.representation(using: .png, properties: [:])?.write(to: file)
    }
}
