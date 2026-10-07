import AppKit
import FluidAudio
import Observation
import SuperDictateCore
import SwiftUI

/// Everything SuperDictate does, in one place the views watch: the speech model
/// getting ready, a dictation from key press to typed text, its live preview,
/// and the capsule at the top of the screen. The rules are `DictationSession`'s;
/// this carries out what it asks for, with the Mac's audio, speech and windows.
@MainActor
@Observable
final class DictationModel {
    typealias Phase = DictationSession.Phase
    typealias Overlay = DictationSession.Overlay

    private(set) var session = DictationSession()
    private(set) var downloadProgress: Double?
    private(set) var caption = Caption()
    private(set) var lastTranscript: String?
    /// When the recording started, for the capsule's time.
    private(set) var recordingStarted: Date?
    private(set) var granted: Set<Permission> = []
    let history: History

    private let audio = AudioCapture()
    private let recognizer = SpeechRecognizer()
    private let hotkey = HotkeyListener()
    @ObservationIgnored private var live: LiveSession?
    @ObservationIgnored private var recorded: [Float] = []
    @ObservationIgnored private var panel: CapsulePanel?
    @ObservationIgnored private var welcome: WelcomeWindow?
    @ObservationIgnored private var noticeTimer: Task<Void, Never>?
    @ObservationIgnored private var stageTask: Task<Void, Never>?

    /// - Parameter history: Left out, the history on disk.
    init(history: History? = nil) {
        self.history = history ?? History()
    }

    var phase: Phase { session.phase }
    var overlay: Overlay { session.overlay }
    var isModelReady: Bool { session.isModelReady }
    var hasAllPermissions: Bool { granted.count == Permission.allCases.count }
    var isSetUp: Bool { isModelReady && hasAllPermissions }

    /// The microphone level for the capsule's meter, read every frame while it shows.
    func currentLevel() -> Float {
        guard session.isSampling else { return audio.currentLevel }
        // The sample on the settings page: a voice that rises and falls.
        return Float(0.08 + 0.05 * sin(Date.timeIntervalSinceReferenceDate * 2.2))
    }

    func launch() {
        Preferences.register()
        panel = CapsulePanel(model: self)
        hotkey.onToggle = { [weak self] in self?.toggle() }
        hotkey.onHoldStart = { [weak self] in self?.send(.start(micAllowed: Permission.microphone.isGranted)) }
        hotkey.onHoldEnd = { [weak self] in self?.send(.finish(pressReturn: Preferences.pressReturn)) }
        hotkey.onCancel = { [weak self] in self?.send(.cancel) }
        hotkey.isDictating = { [weak self] in self?.phase == .recording }

        refreshPermissions()
        if SpeechRecognizer.isDownloaded { prepareModel() }
        if !hasAllPermissions || !SpeechRecognizer.isDownloaded { showWelcome() }

        // Permissions change in System Settings, outside the app; a cheap look every two seconds.
        Task { [weak self] in
            while let self {
                self.refreshPermissions()
                try? await Task.sleep(for: .seconds(2))
            }
        }
    }

    func refreshPermissions() {
        let now = Set(Permission.allCases.filter(\.isGranted))
        if now != granted { granted = now }
        if granted.contains(.inputMonitoring), !hotkey.isRunning { hotkey.start() }
    }

    /// Downloads the model if needed and gets it ready. Downloads only happen when the user asks.
    func prepareModel() {
        send(.prepare(SpeechRecognizer.isDownloaded ? "Getting ready…" : "Downloading…"))
    }

    func toggle() {
        send(.toggle(micAllowed: Permission.microphone.isGranted, pressReturn: Preferences.pressReturn))
    }

    /// While the Capsule settings page is open, the capsule shows on screen with
    /// sample words building up in it, so every change shows on it at once.
    func stage(_ on: Bool) {
        guard panel != nil else { return }
        send(.sample(on))
    }

    private func send(_ event: DictationSession.Event) {
        for effect in session.handle(event) {
            switch effect {
            case .showWelcome: showWelcome()
            case .loadModel: loadModel()
            case .record: record()
            case .stopRecording:
                stopRecording()
                if Preferences.playSounds { NSSound(named: "Pop")?.play() }
                send(.recorded(seconds: Double(recorded.count) / AudioCapture.sampleRate))
            case .dropRecording:
                stopRecording()
                recorded = []
            case .transcribe: transcribe()
            case .deliver(let text, let seconds, let pressReturn):
                lastTranscript = text
                history.add(text, seconds: seconds)
                send(.typed(TextInserter.type(text, pressReturn: pressReturn)))
            case .notice: endNoticeSoon()
            case .startSample: startSample()
            case .stopSample: stopSample()
            }
        }
        if overlay != .hidden { panel?.present() }
    }

    // MARK: - Speech model

    private func loadModel() {
        Task {
            do {
                try await recognizer.load { [weak self] progress in
                    Task { @MainActor in self?.show(progress) }
                }
                downloadProgress = nil
                send(.modelLoaded)
            } catch {
                downloadProgress = nil
                send(.modelFailed(error.localizedDescription))
            }
        }
    }

    private func show(_ progress: DownloadUtils.DownloadProgress) {
        guard case .preparing = phase else { return }
        switch progress.phase {
        case .listing:
            send(.progress("Checking files…"))
        case .downloading(_, let total):
            // FluidAudio reports the download as the first half of the whole job.
            downloadProgress = total > 0 ? min(max(progress.fractionCompleted / 0.5, 0), 1) : nil
            send(.progress(total > 0 ? "Downloading…" : "Loading…"))
        case .compiling:
            downloadProgress = nil
            send(.progress("Optimizing for the Neural Engine…"))
        }
    }

    // MARK: - Dictation

    private func record() {
        do {
            try audio.start()
        } catch {
            send(.microphoneFailed)
            return
        }

        recordingStarted = Date()
        caption = Caption()
        if Preferences.playSounds { NSSound(named: "Tink")?.play() }

        let audio = self.audio
        let recognizer = self.recognizer
        let language = Preferences.languageHint
        let preview = LiveSession(
            drafting: Preferences.livePreview,
            audioSince: { audio.since($0) },
            transcribe: { try await recognizer.transcribe($0, language: language) })
        preview.onDraft = { [weak self, weak preview] settled, tail in
            guard let self, let preview, self.live === preview else { return }
            withAnimation(.spring(duration: 0.45, bounce: 0)) {
                self.caption.show(settled: settled, tail: tail)
            }
        }
        preview.onSilenceLimit = { [weak self] in self?.send(.silenceLimit) }
        live = preview
        preview.start()
    }

    private func stopRecording() {
        live?.stop()
        live = nil
        recordingStarted = nil
        recorded = audio.stop()
    }

    private func transcribe() {
        let samples = recorded
        recorded = []
        let language = Preferences.languageHint
        Task {
            do {
                send(.transcribed(try await recognizer.transcribe(samples, language: language)))
            } catch {
                send(.transcriptionFailed)
            }
        }
    }

    func copyLastTranscript() {
        guard let lastTranscript else { return }
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(lastTranscript, forType: .string)
    }

    private func endNoticeSoon() {
        noticeTimer?.cancel()
        noticeTimer = Task { [weak self] in
            try? await Task.sleep(for: .seconds(1.8))
            guard let self, !Task.isCancelled else { return }
            self.send(.noticeEnded)
        }
    }

    // MARK: - Settings sample

    private func stopSample() {
        stageTask?.cancel()
        caption = Caption()
        recordingStarted = nil
    }

    private func startSample() {
        stopSample()
        recordingStarted = Date()
        stageTask = Task { [weak self] in
            let words = "Every change you make shows up right here, as you make it.".split(separator: " ").map(String.init)
            while !Task.isCancelled {
                for count in 0 ... words.count {
                    try? await Task.sleep(for: .seconds(0.3))
                    guard let self, !Task.isCancelled else { return }
                    // The last two words are still settling, as in a real dictation.
                    let shown = Preferences.livePreview ? Array(words.prefix(count)) : []
                    let unsure = count == words.count ? 0 : min(2, shown.count)
                    withAnimation(.spring(duration: 0.45, bounce: 0)) {
                        self.caption.show(settled: shown.dropLast(unsure).joined(separator: " "),
                                          tail: shown.suffix(unsure).joined(separator: " "))
                    }
                }
                try? await Task.sleep(for: .seconds(1.8))
                guard let self, !Task.isCancelled else { return }
                withAnimation(.easeOut(duration: 0.3)) { self.caption = Caption() }
                try? await Task.sleep(for: .seconds(0.5))
            }
        }
    }

    // MARK: - Snapshots

    /// Example states for the snapshot pictures (`--snapshot`): halfway through
    /// setup, or set up and in the middle of a dictation. Never used otherwise.
    func showcase(setUp: Bool) {
        if setUp {
            granted = Set(Permission.allCases)
            session = DictationSession(phase: .ready)
            _ = session.handle(.sample(true))
            downloadProgress = nil
            lastTranscript = "Let's move the design review to Thursday afternoon, and I'll send the new mockups tonight."
            history.add("Let's move the design review to Thursday afternoon, and I'll send the new mockups tonight.", seconds: 6)
            history.add("Picking up the kids at five, then dinner at Marco's.", seconds: 4)
            history.add("Note for the release: the live preview now builds the sentence word by word.", seconds: 5)
            recordingStarted = Date(timeIntervalSinceNow: -14)
            caption = Caption()
            caption.show(settled: "So the plan for tomorrow is simple:", tail: "we ship the Mac version first")
        } else {
            granted = [.microphone]
            session = DictationSession(phase: .preparing("Downloading…"))
            downloadProgress = 0.42
        }
    }

    // MARK: - Windows

    func showWelcome() {
        if welcome == nil { welcome = WelcomeWindow(model: self) }
        welcome?.show()
    }
}
