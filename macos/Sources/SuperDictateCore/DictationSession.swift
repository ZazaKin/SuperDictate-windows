import Foundation

/// The rules of a dictation, from the speech model getting ready to typed text:
/// what each key press does in each phase, when a recording counts, what the
/// capsule shows and which message it gives. No audio, speech or windows here:
/// the app sends what happened with `handle(_:)` and carries out the effects it
/// gets back, sending their outcome in turn.
public struct DictationSession: Equatable, Sendable {
    /// A recording shorter than this is a tap of the key, not a dictation.
    public static let shortest = 0.3

    public enum Phase: Equatable, Sendable {
        /// The model isn't on this Mac yet, or loading it failed (with why).
        case needsModel(String?)
        /// Downloading or compiling the model; the detail says which.
        case preparing(String)
        case ready
        case recording
        case processing
    }

    /// What the capsule at the top of the screen shows.
    public enum Overlay: Equatable, Sendable {
        case hidden
        case listening
        case processing
        /// A short message that goes away by itself.
        case notice(Notice)
    }

    public enum Notice: Equatable, Sendable {
        case allowMicrophone, microphoneUnavailable, cancelled, noSpeech, failed, copied, closeCapsuleSettings

        public var text: String {
            switch self {
            case .allowMicrophone: "Allow the microphone first"
            case .microphoneUnavailable: "Microphone unavailable"
            case .cancelled: "Cancelled"
            case .noSpeech: "No speech detected"
            case .failed: "Couldn't transcribe that"
            case .copied: "Copied: allow Accessibility to type"
            case .closeCapsuleSettings: "Close Capsule settings to dictate"
            }
        }

        /// An SF Symbol.
        public var symbol: String {
            switch self {
            case .allowMicrophone, .microphoneUnavailable: "mic.slash.fill"
            case .cancelled: "xmark"
            case .noSpeech: "waveform.slash"
            case .failed: "exclamationmark.triangle.fill"
            case .copied: "doc.on.clipboard"
            case .closeCapsuleSettings: "slider.horizontal.3"
            }
        }
    }

    public enum Event: Equatable, Sendable {
        /// Get the model ready (the detail says how) unless it is, or is on its way.
        case prepare(String)
        /// A step while the model gets ready.
        case progress(String)
        case modelLoaded
        case modelFailed(String)

        /// The shortcut pressed, or the menu's button: start, or finish.
        case toggle(micAllowed: Bool, pressReturn: Bool)
        /// The shortcut held down.
        case start(micAllowed: Bool)
        /// The shortcut let go.
        case finish(pressReturn: Bool)
        /// A minute without speech. Nobody may be at the keyboard, so it never presses Return.
        case silenceLimit
        /// Escape, or another key during a hold: the recording is dropped.
        case cancel

        case microphoneFailed
        /// How long the recording that just stopped is.
        case recorded(seconds: Double)
        case transcribed(String)
        case transcriptionFailed
        /// Whether the text was typed; otherwise it is on the clipboard.
        case typed(Bool)
        case noticeEnded

        /// The settings page's sample in the capsule, on or off.
        case sample(Bool)
    }

    public enum Effect: Equatable, Sendable {
        case showWelcome
        /// Download and load the model; send `.modelLoaded` or `.modelFailed`.
        case loadModel
        /// Open the microphone and the live preview; send `.microphoneFailed` if it won't open.
        case record
        /// Close them and keep the audio; send `.recorded(seconds:)`.
        case stopRecording
        /// Close them and throw the audio away.
        case dropRecording
        /// Turn the kept audio into text; send `.transcribed` or `.transcriptionFailed`.
        case transcribe
        /// Keep the text in the history and type it; send `.typed`.
        case deliver(String, seconds: Double, pressReturn: Bool)
        /// Show it for a moment, then send `.noticeEnded`.
        case notice(Notice)
        case startSample
        case stopSample
    }

    public private(set) var phase: Phase
    public private(set) var notice: Notice?
    public private(set) var isSampling = false
    private var seconds = 0.0
    private var pressReturn = false

    public init(phase: Phase = .needsModel(nil)) {
        self.phase = phase
    }

    public var isModelReady: Bool { [.ready, .recording, .processing].contains(phase) }

    public var overlay: Overlay {
        if let notice { return .notice(notice) }
        switch phase {
        case .recording: return .listening
        case .processing: return .processing
        default: return isSampling ? .listening : .hidden
        }
    }

    public mutating func handle(_ event: Event) -> [Effect] {
        switch event {
        case .prepare(let detail):
            guard case .needsModel = phase else { return [] }
            phase = .preparing(detail)
            return [.loadModel]
        case .progress(let detail):
            guard case .preparing = phase else { return [] }
            phase = .preparing(detail)
        case .modelLoaded:
            guard case .preparing = phase else { return [] }
            phase = .ready
        case .modelFailed(let problem):
            guard case .preparing = phase else { return [] }
            phase = .needsModel(problem)

        case .toggle(let micAllowed, let pressReturn):
            switch phase {
            case .recording: return handle(.finish(pressReturn: pressReturn))
            case .ready: return handle(.start(micAllowed: micAllowed))
            case .processing: return []
            case .needsModel, .preparing: return [.showWelcome]
            }
        case .start(let micAllowed):
            guard phase == .ready else { return isModelReady ? [] : [.showWelcome] }
            // While the Capsule settings page shows its sample, the capsule is being edited.
            guard !isSampling else { return show(.closeCapsuleSettings) }
            guard micAllowed else { return show(.allowMicrophone) + [.showWelcome] }
            notice = nil
            phase = .recording
            return [.record]
        case .finish(let pressReturn):
            guard phase == .recording else { return [] }
            self.pressReturn = pressReturn
            phase = .processing
            return [.stopRecording]
        case .silenceLimit:
            return handle(.finish(pressReturn: false))
        case .cancel:
            guard phase == .recording else { return [] }
            phase = .ready
            return [.dropRecording] + show(.cancelled)

        case .microphoneFailed:
            guard phase == .recording else { return [] }
            phase = .ready
            return show(.microphoneUnavailable)
        case .recorded(let seconds):
            guard phase == .processing else { return [] }
            guard seconds >= Self.shortest else {
                phase = .ready
                return []
            }
            self.seconds = seconds
            return [.transcribe]
        case .transcribed(let text):
            guard phase == .processing else { return [] }
            phase = .ready
            guard !text.isEmpty else { return show(.noSpeech) }
            return [.deliver(text, seconds: seconds, pressReturn: pressReturn)]
        case .transcriptionFailed:
            guard phase == .processing else { return [] }
            phase = .ready
            return show(.failed)
        case .typed(let typed):
            return typed ? [] : show(.copied)
        case .noticeEnded:
            notice = nil

        case .sample(let on):
            guard on != isSampling, phase != .recording, phase != .processing else { return [] }
            isSampling = on
            return [on ? .startSample : .stopSample]
        }
        return []
    }

    private mutating func show(_ notice: Notice) -> [Effect] {
        self.notice = notice
        return [.notice(notice)]
    }
}
