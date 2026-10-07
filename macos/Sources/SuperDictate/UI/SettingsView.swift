import AppKit
import ServiceManagement
import SwiftUI

/// The Settings window (⌘,): toolbar tabs over grouped forms, the way Apple's own apps lay out their settings.
struct SettingsView: View {
    /// Remembered, and set by the menu bar panel to open a particular tab.
    @AppStorage(Preferences.Key.settingsTab) private var tab = "general"

    var body: some View {
        TabView(selection: $tab) {
            GeneralPane()
                .tabItem { Label("General", systemImage: "gearshape") }
                .tag("general")
            DictationPane()
                .tabItem { Label("Dictation", systemImage: "mic") }
                .tag("dictation")
            LanguagesPane()
                .tabItem { Label("Languages", systemImage: "globe") }
                .tag("languages")
            CapsulePane()
                .tabItem { Label("Capsule", systemImage: "capsule") }
                .tag("capsule")
            ModelPane()
                .tabItem { Label("Speech Model", systemImage: "waveform") }
                .tag("model")
            HistoryPane()
                .tabItem { Label("History", systemImage: "clock.arrow.circlepath") }
                .tag("history")
            AboutPane()
                .tabItem { Label("About", systemImage: "info.circle") }
                .tag("about")
        }
        .frame(width: 620)
    }
}

// MARK: - General

struct GeneralPane: View {
    @Environment(DictationModel.self) private var model
    @AppStorage(Preferences.Key.playSounds) private var playSounds = true
    @State private var openAtLogin = SMAppService.mainApp.status == .enabled

    var body: some View {
        Form {
            Section {
                Toggle("Open at login", isOn: $openAtLogin)
                    .onChange(of: openAtLogin) {
                        do {
                            if openAtLogin {
                                try SMAppService.mainApp.register()
                            } else {
                                try SMAppService.mainApp.unregister()
                            }
                        } catch {
                            openAtLogin = SMAppService.mainApp.status == .enabled
                        }
                    }
                Toggle("Play sounds", isOn: $playSounds)
            } footer: {
                Footnote("A soft sound when dictation starts and stops.")
            }

            Section {
                ForEach(Permission.allCases) { permission in
                    PermissionRow(permission: permission, granted: model.granted.contains(permission))
                }
            } header: {
                Text("Permissions")
            } footer: {
                Footnote("Your voice is recognized on this Mac and never sent anywhere.")
            }
        }
        .formStyle(.grouped)
        .toggleStyle(.switch)
        .frame(height: 400)
    }
}

// MARK: - Dictation

struct DictationPane: View {
    @AppStorage(Preferences.Key.hotkey) private var hotkey = Hotkey.rightCommand
    @AppStorage(Preferences.Key.mode) private var mode = TriggerMode.toggle
    @AppStorage(Preferences.Key.pressReturn) private var pressReturn = false

    var body: some View {
        Form {
            Section {
                Picker("Key", selection: $hotkey) {
                    ForEach(Hotkey.allCases) { Text($0.title).tag($0) }
                }
                Picker("Use", selection: $mode) {
                    ForEach(TriggerMode.allCases) { Text($0.title).tag($0) }
                }
            } header: {
                Text("Shortcut")
            } footer: {
                Footnote("Press Escape while dictating to cancel.")
            }

            Section {
                LabeledContent("Stops by itself", value: "After a minute without speech")
            } header: {
                Text("While You Speak")
            } footer: {
                Footnote("If you walk away, dictation ends by itself and types what you said. Languages and the capsule have tabs of their own.")
            }

            Section {
                Toggle("Press Return after typing", isOn: $pressReturn)
            } header: {
                Text("When You Finish")
            } footer: {
                Footnote("Sends a chat message right away. Never when dictation stopped by itself.")
            }
        }
        .formStyle(.grouped)
        .toggleStyle(.switch)
        .frame(height: 420)
    }
}

// MARK: - Speech model

struct ModelPane: View {
    @Environment(DictationModel.self) private var model

    var body: some View {
        Form {
            Section {
                HStack(spacing: 14) {
                    IconTile(symbol: "waveform", color: .purple, size: 40)
                    VStack(alignment: .leading, spacing: 2) {
                        Text("Parakeet TDT v3").font(.headline)
                        Text("By NVIDIA · 25 European languages · runs on the Neural Engine")
                            .font(.callout)
                            .foregroundStyle(.secondary)
                    }
                    Spacer(minLength: 12)
                    ModelStatus()
                }
                .padding(.vertical, 4)

                if let progress = model.downloadProgress {
                    ProgressView(value: progress)
                        .animation(.easeOut(duration: 0.3), value: progress)
                }
                if case .needsModel(let problem?) = model.phase {
                    Label(problem, systemImage: "exclamationmark.triangle.fill")
                        .foregroundStyle(.red)
                        .font(.callout)
                }
            }

            Section {
                LabeledContent("Download", value: "About 460 MB, once")
                LabeledContent("Location") {
                    Button("Show in Finder") {
                        NSWorkspace.shared.activateFileViewerSelecting([SpeechRecognizer.modelFolder])
                    }
                    .disabled(!SpeechRecognizer.isDownloaded)
                }
            } footer: {
                Footnote("The model comes from Hugging Face. After that, dictation works without the internet.")
            }
        }
        .formStyle(.grouped)
        .frame(height: 330)
    }
}

/// Ready, working on it (with what), or a button to get it.
struct ModelStatus: View {
    @Environment(DictationModel.self) private var model

    var body: some View {
        Group {
            switch model.phase {
            case .ready, .recording, .processing:
                Label("Ready", systemImage: "checkmark.circle.fill")
                    .foregroundStyle(.green)
            case .preparing(let detail):
                HStack(spacing: 6) {
                    ProgressView().controlSize(.small)
                    Text(detail).foregroundStyle(.secondary)
                }
            case .needsModel(let problem):
                Button(problem == nil ? "Download" : "Try Again") { model.prepareModel() }
                    .buttonStyle(.borderedProminent)
            }
        }
        .animation(.spring(duration: 0.35, bounce: 0), value: model.phase)
    }
}

// MARK: - History

struct HistoryPane: View {
    @Environment(DictationModel.self) private var model
    @State private var confirmClear = false

    var body: some View {
        VStack(spacing: 0) {
            if model.history.entries.isEmpty {
                ContentUnavailableView("No Dictations Yet",
                                       systemImage: "waveform",
                                       description: Text("What you dictate appears here, newest first. It stays on this Mac."))
            } else {
                List {
                    ForEach(model.history.entries) { entry in
                        HistoryRow(entry: entry)
                    }
                }
                .listStyle(.inset(alternatesRowBackgrounds: true))

                Divider()
                HStack {
                    Text("\(model.history.entries.count) dictations")
                        .foregroundStyle(.secondary)
                    Spacer()
                    Button("Clear History…", role: .destructive) { confirmClear = true }
                }
                .padding(12)
            }
        }
        .frame(height: 440)
        .confirmationDialog("Clear all dictations?", isPresented: $confirmClear) {
            Button("Clear History", role: .destructive) { model.history.clear() }
        } message: {
            Text("This can't be undone.")
        }
    }
}

private struct HistoryRow: View {
    @Environment(DictationModel.self) private var model
    let entry: HistoryEntry
    @State private var hovering = false
    @State private var copied = false

    var body: some View {
        HStack(alignment: .top, spacing: 10) {
            VStack(alignment: .leading, spacing: 4) {
                Text(entry.text)
                    .lineLimit(4)
                    .textSelection(.enabled)
                Text(entry.date, format: .dateTime.day().month().hour().minute())
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Spacer(minLength: 8)
            Button {
                NSPasteboard.general.clearContents()
                NSPasteboard.general.setString(entry.text, forType: .string)
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
            .opacity(hovering || copied ? 1 : 0)
        }
        .padding(.vertical, 4)
        .onHover { hovering = $0 }
        .contextMenu {
            Button("Copy") {
                NSPasteboard.general.clearContents()
                NSPasteboard.general.setString(entry.text, forType: .string)
            }
            Button("Delete", role: .destructive) { model.history.remove(entry) }
        }
    }
}

// MARK: - About

struct AboutPane: View {
    private var version: String {
        Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "–"
    }

    var body: some View {
        VStack(spacing: 14) {
            AppIcon(size: 96)
            VStack(spacing: 4) {
                Text("SuperDictate").font(.title.weight(.semibold))
                Text("Version \(version)").foregroundStyle(.secondary)
            }
            Text("Fast, private dictation for any app. Free and open source.")
                .multilineTextAlignment(.center)

            HStack(spacing: 12) {
                Link(destination: URL(string: "https://github.com/ZazaKin/SuperDictate-windows")!) {
                    Label("Source Code", systemImage: "chevron.left.forwardslash.chevron.right")
                }
                Link(destination: URL(string: "https://github.com/ZazaKin/SuperDictate-windows#donate")!) {
                    Label("Support the Project", systemImage: "heart")
                }
            }
            .buttonStyle(.bordered)

            Spacer()

            Text("Speech model: NVIDIA Parakeet TDT v3, CC BY 4.0, through FluidAudio. Based on Parakey by Richard Courtman, MIT license.")
                .font(.caption)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
        }
        .padding(30)
        .frame(height: 400)
    }
}
