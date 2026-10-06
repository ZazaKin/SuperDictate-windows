import AppKit
import SwiftUI

/// SuperDictate lives in the menu bar (LSUIElement: no Dock icon). Its windows
/// are the menu bar panel, Settings (⌘,), the first-run welcome and the capsule
/// that shows while dictating.
@main
struct SuperDictateApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var delegate

    var body: some Scene {
        MenuBarExtra {
            MenuBarPanel()
                .environment(delegate.model)
        } label: {
            MenuBarLabel()
                .environment(delegate.model)
        }
        .menuBarExtraStyle(.window)

        Settings {
            SettingsView()
                .environment(delegate.model)
        }
    }
}

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    let model = DictationModel()

    func applicationDidFinishLaunching(_ notification: Notification) {
        if let folder = Snapshots.folder {
            Snapshots.render(to: folder)
            NSApp.terminate(nil)
            return
        }
        model.launch()
    }
}
