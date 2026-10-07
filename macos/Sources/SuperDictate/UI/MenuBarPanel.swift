import AppKit
import SuperDictateCore
import SwiftUI

/// The menu bar icon: a waveform, filled while dictating.
struct MenuBarLabel: View {
    @Environment(DictationModel.self) private var model

    var body: some View {
        Image(systemName: model.phase == .recording ? "waveform.circle.fill" : "waveform")
    }
}

/// What opens from the menu bar icon, laid out like a Control Center module:
/// status at the top, one big button, the last dictation, then the usual items.
struct MenuBarPanel: View {
    @Environment(DictationModel.self) private var model
    @Environment(\.openSettings) private var openSettings
    @AppStorage(Preferences.Key.settingsTab) private var settingsTab = "general"

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack(spacing: 10) {
                AppIcon(size: 32)
                VStack(alignment: .leading, spacing: 1) {
                    Text("SuperDictate").font(.headline)
                    StatusLine()
                }
            }

            DictateButton()

            LanguageMenu { showSettings("languages") }

            if let last = model.lastTranscript {
                LastTranscript(text: last)
            }

            Divider()

            VStack(spacing: 2) {
                if !model.isSetUp {
                    MenuItem(title: "Finish Setup…", symbol: "checklist") { model.showWelcome() }
                }
                MenuItem(title: "Settings…", symbol: "gearshape") { showSettings(nil) }
                MenuItem(title: "Quit SuperDictate", symbol: "power") { NSApp.terminate(nil) }
            }
        }
        .padding(14)
        .frame(width: 300)
    }

    /// Opens Settings, at a particular tab if one is given.
    private func showSettings(_ tab: String?) {
        if let tab { settingsTab = tab }
        NSApp.activate(ignoringOtherApps: true)
        openSettings()
    }
}

/// The language to listen for, switched from the menu bar: automatic, or one of
/// the user's languages. The full list is in Settings › Languages.
private struct LanguageMenu: View {
    @AppStorage(Preferences.Key.language) private var mode = "auto"
    @AppStorage(Preferences.Key.languages) private var stored = Preferences.defaultLanguages
    let chooseLanguages: () -> Void

    var body: some View {
        let chosen = SpokenLanguage.codes(stored).compactMap(SpokenLanguage.find)
        let current = chosen.first { $0.code == mode }
        HStack {
            Label("Language", systemImage: "globe")
                .foregroundStyle(.secondary)
            Spacer()
            Menu {
                Picker("Language", selection: $mode) {
                    Text("Automatic").tag("auto")
                    ForEach(chosen) { language in
                        Text("\(language.flag)  \(language.native)").tag(language.code)
                    }
                }
                .pickerStyle(.inline)
                .labelsHidden()
                Divider()
                Button("Choose Languages…", action: chooseLanguages)
            } label: {
                Text(current.map { "\($0.flag) \($0.native)" } ?? "Automatic")
            }
            .menuStyle(.borderlessButton)
            .fixedSize()
        }
        .font(.callout)
        .padding(.horizontal, 2)
    }
}

private struct StatusLine: View {
    @Environment(DictationModel.self) private var model

    var body: some View {
        HStack(spacing: 5) {
            Circle().fill(color).frame(width: 7, height: 7)
            Text(text)
                .font(.subheadline)
                .foregroundStyle(.secondary)
                .contentTransition(.opacity)
        }
        .animation(.easeOut(duration: 0.2), value: text)
    }

    private var text: String {
        switch model.phase {
        case .ready: model.hasAllPermissions ? "Ready · press \(Preferences.hotkey.shortName)" : "Needs permissions"
        case .recording: "Listening…"
        case .processing: "Processing…"
        case .preparing(let detail): detail
        case .needsModel: "Speech model needed"
        }
    }

    private var color: Color {
        switch model.phase {
        case .ready: model.hasAllPermissions ? .green : .orange
        case .recording: .red
        case .processing, .preparing: .orange
        case .needsModel: .secondary
        }
    }
}

/// Start or stop, the size of a Control Center toggle. Disabled until the model is ready.
private struct DictateButton: View {
    @Environment(DictationModel.self) private var model

    var body: some View {
        let recording = model.phase == .recording
        Button {
            model.toggle()
        } label: {
            Label(recording ? "Stop Dictation" : "Start Dictation",
                  systemImage: recording ? "stop.fill" : "mic.fill")
                .font(.body.weight(.semibold))
                .frame(maxWidth: .infinity)
                .contentTransition(.symbolEffect(.replace))
        }
        .buttonStyle(.borderedProminent)
        .tint(recording ? .red : .accentColor)
        .controlSize(.large)
        .disabled(!model.isModelReady || model.phase == .processing)
        .animation(.spring(duration: 0.3, bounce: 0), value: recording)
    }
}

/// The last dictation, a few lines of it, with a copy button.
private struct LastTranscript: View {
    @Environment(DictationModel.self) private var model
    let text: String
    @State private var copied = false

    var body: some View {
        HStack(alignment: .top, spacing: 8) {
            Text(text)
                .font(.callout)
                .lineLimit(3)
                .textSelection(.enabled)
                .frame(maxWidth: .infinity, alignment: .leading)
            Button {
                model.copyLastTranscript()
                copied = true
                Task {
                    try? await Task.sleep(for: .seconds(1.5))
                    copied = false
                }
            } label: {
                Image(systemName: copied ? "checkmark" : "doc.on.doc")
                    .contentTransition(.symbolEffect(.replace))
            }
            .buttonStyle(.borderless)
            .help("Copy")
        }
        .padding(10)
        .background(Color.primary.opacity(0.06), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
    }
}

/// A menu-like row: highlighted under the pointer, like the items of a system menu.
private struct MenuItem: View {
    let title: String
    let symbol: String
    let action: () -> Void
    @State private var hovering = false

    var body: some View {
        Button(action: action) {
            Label(title, systemImage: symbol)
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(.horizontal, 8)
                .padding(.vertical, 5)
                .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .background(hovering ? Color.primary.opacity(0.1) : .clear,
                    in: RoundedRectangle(cornerRadius: 6, style: .continuous))
        .onHover { hovering = $0 }
    }
}
