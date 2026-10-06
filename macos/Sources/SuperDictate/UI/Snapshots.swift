import AppKit
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
            save(GeneralPane().frame(width: 560), "settings-general")
            save(DictationPane().frame(width: 560), "settings-dictation")
            save(ModelPane().frame(width: 560), "settings-model")
            save(HistoryPane().frame(width: 560), "settings-history")
            save(AboutPane().frame(width: 560), "settings-about")
            // The capsule over a stand-in for whatever app is underneath.
            save(CapsuleView()
                    .frame(width: 560, height: 120)
                    .background(LinearGradient(colors: [.indigo, .teal], startPoint: .topLeading, endPoint: .bottomTrailing)),
                 "capsule")
        }
    }

    private static func write<V: View>(_ view: V, to file: URL, appearance: NSAppearance.Name) {
        let host = NSHostingView(rootView: view.background(Color(nsColor: .windowBackgroundColor)))
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
