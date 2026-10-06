import AppKit
import SwiftUI

/// The first-run window, in the manner of Apple's setup assistants. It opens
/// whenever something is missing, and each row ticks itself off as the user
/// allows it in System Settings.
@MainActor
final class WelcomeWindow {
    private let window: NSWindow

    init(model: DictationModel) {
        let window = NSWindow(contentRect: .zero,
                              styleMask: [.titled, .closable, .fullSizeContentView],
                              backing: .buffered, defer: false)
        window.titlebarAppearsTransparent = true
        window.titleVisibility = .hidden
        window.isMovableByWindowBackground = true
        window.isReleasedWhenClosed = false
        window.contentViewController = NSHostingController(
            rootView: WelcomeView(close: { [weak window] in window?.close() }).environment(model))
        self.window = window
    }

    func show() {
        window.center()
        NSApp.activate(ignoringOtherApps: true)
        window.makeKeyAndOrderFront(nil)
    }
}

struct WelcomeView: View {
    @Environment(DictationModel.self) private var model
    let close: () -> Void

    var body: some View {
        VStack(spacing: 0) {
            VStack(spacing: 10) {
                AppIcon(size: 96)
                Text("Welcome to SuperDictate")
                    .font(.largeTitle.weight(.bold))
                Text("Talk instead of typing, in any app.\nYour voice never leaves this Mac.")
                    .font(.title3)
                    .multilineTextAlignment(.center)
                    .foregroundStyle(.secondary)
            }
            .padding(.top, 36)
            .padding(.bottom, 24)

            VStack(spacing: 0) {
                ForEach(Permission.allCases) { permission in
                    PermissionRow(permission: permission, granted: model.granted.contains(permission))
                        .padding(.vertical, 10)
                    Divider().padding(.leading, 38)
                }
                ModelRow()
                    .padding(.vertical, 10)
            }
            .padding(.horizontal, 16)
            .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 12, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 12, style: .continuous).strokeBorder(.separator))
            .padding(.horizontal, 32)

            HStack {
                if model.isSetUp {
                    Label("Press \(Preferences.hotkey.shortName) to dictate", systemImage: "keyboard")
                        .foregroundStyle(.secondary)
                        .transition(.opacity)
                }
                Spacer()
                Button(model.isSetUp ? "Start Dictating" : "Later", action: close)
                    .keyboardShortcut(.defaultAction)
                    .buttonStyle(.borderedProminent)
                    .controlSize(.large)
            }
            .padding(.horizontal, 32)
            .padding(.vertical, 24)
            .animation(.spring(duration: 0.35, bounce: 0), value: model.isSetUp)
        }
        .frame(width: 540)
        .task {
            // Ticks rows off as permissions arrive, without waiting for the app-wide check.
            while !Task.isCancelled {
                model.refreshPermissions()
                try? await Task.sleep(for: .seconds(0.5))
            }
        }
    }
}

/// The speech model as the fourth step: download, progress, then a check.
private struct ModelRow: View {
    @Environment(DictationModel.self) private var model

    var body: some View {
        VStack(spacing: 8) {
            HStack(spacing: 12) {
                IconTile(symbol: "waveform", color: .purple)
                VStack(alignment: .leading, spacing: 1) {
                    Text("Speech Model")
                    Text("About 460 MB, downloaded once.")
                        .font(.callout)
                        .foregroundStyle(.secondary)
                }
                Spacer(minLength: 12)
                ModelStatus()
            }
            if let progress = model.downloadProgress {
                ProgressView(value: progress)
                    .padding(.leading, 38)
            }
        }
    }
}
