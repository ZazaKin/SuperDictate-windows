import AppKit
import FluidAudio
import Observation
import SuperDictateCore
import SwiftUI

/// Everything SuperDictate does, in one place the views watch: the speech model
/// getting ready, a dictation from key press to typed text, its live preview,
/// and the capsule at the top of the screen.
@MainActor
@Observable
final class DictationModel {
    enum Phase: Equatable {
        /// The model isn't on this Mac yet, or loading it failed (with why).
        case needsModel(String?)
        /// Downloading or compiling the model; the detail says which.
        case preparing(String)
        case ready
        case recording
        case processing
    }

    /// What the capsule at the top of the screen shows.
    enum Overlay: Equatable {
        case hidden
        case listening
        case processing
        /// A short message with an SF Symbol that goes away by itself.
        case notice(String, String)
    }

    private(set) var phase: Phase = .needsModel(nil)
    private(set) var downloadProgress: Double?
    private(set) var overlay: Overlay = .hidden
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
    @ObservationIgnored private var panel: CapsulePanel?
    @ObservationIgnored private var welcome: WelcomeWindow?
    @ObservationIgnored private var noticeTimer: Task<Void, Never>?
    @ObservationIgnored private var isStaged = false
    @ObservationIgnored private var stageTask: Task<Void, Never>?

    /// - Parameter history: Left out, the history on disk.
    init(history: History? = nil) {
        self.history = history ?? History()
    }

    var isModelReady: Bool { [.ready, .recording, .processing].contains(phase) }
    var hasAllPermissions: Bool { granted.count == Permission.allCases.count }
    var isSetUp: Bool { isModelReady && hasAllPermissions }

    /// The microphone level for the capsule's meter, read every frame while it shows.
    func currentLevel() -> Float {
        guard isStaged else { return audio.currentLevel }
        // The sample on the settings page: a voice that rises and falls.
        return Float(0.08 + 0.05 * sin(Date.timeIntervalSinceReferenceDate * 2.2))
    }

    func launch() {
        Preferences.register()
        panel = CapsulePanel(model: self)
        hotkey.onToggle = { [weak self] in self?.toggle() }
        hotkey.onHoldStart = { [weak self] in self?.start() }
        hotkey.onHoldEnd = { [weak self] in self?.finish(pressReturn: Preferences.pressReturn) }
        hotkey.onCancel = { [weak self] in self?.cancel() }
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

    // MARK: - Speech model

    /// Downloads the model if needed and gets it ready. Downloads only happen when the user asks.
    func prepareModel() {
        if case .preparing = phase { return }
        guard !isModelReady else { return }
        phase = .preparing(SpeechRecognizer.isDownloaded ? "Getting ready…" : "Downloading…")
        Task {
            do {
                try await recognizer.load { [weak self] progress in
                    Task { @MainActor in self?.show(progress) }
                }
                downloadProgress = nil
                phase = .ready
            } catch {
                downloadProgress = nil
                phase = .needsModel(error.localizedDescription)
            }
        }
    }

    private func show(_ progress: DownloadUtils.DownloadProgress) {
        guard case .preparing = phase else { return }
        switch progress.phase {
        case .listing:
            phase = .preparing("Checking files…")
        case .downloading(_, let total):
            // FluidAudio reports the download as the first half of the whole job.
            downloadProgress = total > 0 ? min(max(progress.fractionCompleted / 0.5, 0), 1) : nil
            phase = .preparing(total > 0 ? "Downloading…" : "Loading…")
        case .compiling:
            downloadProgress = nil
            phase = .preparing("Optimizing for the Neural Engine…")
        }
    }

    // MARK: - Dictation

    func toggle() {
        switch phase {
        case .recording: finish(pressReturn: Preferences.pressReturn)
        case .ready: start()
        case .processing: break
        case .needsModel, .preparing: showWelcome()
        }
    }

    func start() {
        guard phase == .ready else {
            if !isModelReady { showWelcome() }
            return
        }
        guard Permission.microphone.isGranted else {
            notice("Allow the microphone first", symbol: "mic.slash.fill")
            showWelcome()
            return
        }
        do {
            try audio.start()
        } catch {
            notice("Microphone unavailable", symbol: "mic.slash.fill")
            return
        }

        // A real dictation takes over from the settings page's sample.
        stageTask?.cancel()
        isStaged = false
        phase = .recording
        recordingStarted = Date()
        caption = Caption()
        overlay = .listening
        panel?.present()
        if Preferences.playSounds { NSSound(named: "Tink")?.play() }

        let audio = self.audio
        let recognizer = self.recognizer
        let language = Preferences.languageHint
        let session = LiveSession(
            drafting: Preferences.livePreview,
            audioSince: { audio.since($0) },
            transcribe: { try await recognizer.transcribe($0, language: language) })
        session.onDraft = { [weak self, weak session] settled, tail in
            guard let self, let session, self.live === session else { return }
            withAnimation(.spring(duration: 0.45, bounce: 0)) {
                self.caption.show(settled: settled, tail: tail)
            }
        }
        session.onSilenceLimit = { [weak self] in
            // Nobody may be at the keyboard, so it types the text but never presses Return.
            self?.finish(pressReturn: false)
        }
        live = session
        session.start()
    }

    func finish(pressReturn: Bool) {
        guard phase == .recording else { return }
        live?.stop()
        live = nil
        recordingStarted = nil
        let samples = audio.stop()
        if Preferences.playSounds { NSSound(named: "Pop")?.play() }

        let seconds = Double(samples.count) / AudioCapture.sampleRate
        guard seconds >= 0.3 else {
            // A tap rather than a dictation.
            phase = .ready
            overlay = .hidden
            return
        }

        phase = .processing
        overlay = .processing
        let language = Preferences.languageHint
        Task {
            do {
                let text = try await recognizer.transcribe(samples, language: language)
                deliver(text, seconds: seconds, pressReturn: pressReturn)
            } catch {
                phase = .ready
                notice("Couldn't transcribe that", symbol: "exclamationmark.triangle.fill")
            }
        }
    }

    /// Escape, or another key during a hold: the recording is dropped.
    func cancel() {
        guard phase == .recording else { return }
        live?.stop()
        live = nil
        recordingStarted = nil
        _ = audio.stop()
        phase = .ready
        notice("Cancelled", symbol: "xmark")
    }

    private func deliver(_ text: String, seconds: Double, pressReturn: Bool) {
        phase = .ready
        guard !text.isEmpty else {
            notice("No speech detected", symbol: "waveform.slash")
            return
        }

        lastTranscript = text
        history.add(text, seconds: seconds)
        if TextInserter.type(text, pressReturn: pressReturn) {
            overlay = .hidden
        } else {
            notice("Copied: allow Accessibility to type", symbol: "doc.on.clipboard")
        }
    }

    func copyLastTranscript() {
        guard let lastTranscript else { return }
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(lastTranscript, forType: .string)
    }

    private func notice(_ text: String, symbol: String) {
        overlay = .notice(text, symbol)
        panel?.present()
        noticeTimer?.cancel()
        noticeTimer = Task { [weak self] in
            try? await Task.sleep(for: .seconds(1.8))
            guard let self, !Task.isCancelled, case .notice = self.overlay else { return }
            self.overlay = .hidden
        }
    }

    // MARK: - Settings sample

    /// While the Capsule settings page is open, the capsule shows on screen with
    /// sample words building up in it, so every change shows on it at once.
    /// A real dictation takes over from it.
    func stage(_ on: Bool) {
        guard panel != nil, on != isStaged, phase != .recording, phase != .processing else { return }
        isStaged = on
        stageTask?.cancel()
        caption = Caption()
        guard on else {
            recordingStarted = nil
            overlay = .hidden
            return
        }

        recordingStarted = Date()
        overlay = .listening
        panel?.present()
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
            phase = .ready
            downloadProgress = nil
            lastTranscript = "Let's move the design review to Thursday afternoon, and I'll send the new mockups tonight."
            history.add("Let's move the design review to Thursday afternoon, and I'll send the new mockups tonight.", seconds: 6)
            history.add("Picking up the kids at five, then dinner at Marco's.", seconds: 4)
            history.add("Note for the release: the live preview now builds the sentence word by word.", seconds: 5)
            overlay = .listening
            recordingStarted = Date(timeIntervalSinceNow: -14)
            caption = Caption()
            caption.show(settled: "So the plan for tomorrow is simple:", tail: "we ship the Mac version first")
        } else {
            granted = [.microphone]
            phase = .preparing("Downloading…")
            downloadProgress = 0.42
        }
    }

    // MARK: - Windows

    func showWelcome() {
        if welcome == nil { welcome = WelcomeWindow(model: self) }
        welcome?.show()
    }
}
