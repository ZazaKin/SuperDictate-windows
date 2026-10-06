import Foundation

/// The live side of one recording. While the microphone runs, it keeps a draft
/// of what has been said so far, and it notices when nothing has been said for
/// ``silenceLimit``. Time here is audio time (samples heard), so the result
/// doesn't depend on how often ``tick()`` runs.
///
/// The draft has two parts. Settled words come from phrases that ended in a
/// pause and never change. The tail is everything since, transcribed again on
/// every pass, so its words can still be corrected. A pass only ever hears the
/// tail, so drafts stay quick however long the dictation runs. Nothing here is
/// typed: the whole recording is transcribed again when it stops.
///
/// The Windows app's `LiveSession` follows the same rules with the same numbers.
@MainActor
public final class LiveSession {
    public static let silenceLimit: TimeInterval = 60
    public static let sampleRate = 16_000

    private static let frame = sampleRate / 50                 // 20 ms
    private static let pause = sampleRate * 7 / 10             // A gap this long ends a phrase.
    private static let shortestTail = sampleRate               // Less than this mid-phrase isn't worth a pass.
    private static let longestTail = sampleRate * 12           // Settle anyway after this much talk without a pause.
    private static let cadence = sampleRate * 6 / 10           // New audio between passes while talking.
    private static let leadIn = sampleRate / 2                 // Kept before detected speech: detection lags its start.
    private static let slowPass: TimeInterval = 2              // Slower drafts would hold up the final transcription.

    /// The draft changed: the settled words, then the tail that may still change.
    public var onDraft: ((String, String) -> Void)?
    /// Nothing that sounds like speech for ``silenceLimit``. Called once.
    public var onSilenceLimit: (() -> Void)?

    private let audioSince: (Int) -> [Float]
    private let transcribe: ([Float]) async throws -> String
    private var voice = VoiceActivity()
    private var timer: Timer?
    private var pass: Task<Void, Never>?

    private var heard = 0        // Samples run through voice detection so far.
    private var lastSpeech = 0   // Where speech was last heard.
    private var tailStart = 0    // Where the unsettled tail begins.
    private var draftedTo = 0    // Where the audio of the latest pass ended.
    private var settled = ""
    private var drafting: Bool
    private var limitReached = false
    private var stopped = false

    /// - Parameters:
    ///   - drafting: Whether to keep a draft at all; the silence limit works either way.
    ///   - audioSince: The recording from a sample index on: 16 kHz mono floats.
    ///   - transcribe: A transcription of some samples.
    public init(drafting: Bool = true,
                audioSince: @escaping (Int) -> [Float],
                transcribe: @escaping ([Float]) async throws -> String) {
        self.drafting = drafting
        self.audioSince = audioSince
        self.transcribe = transcribe
    }

    public func start() {
        guard timer == nil else { return }
        timer = Timer.scheduledTimer(withTimeInterval: 0.1, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.tick() }
        }
    }

    /// Stops listening. A pass still running finishes, but its draft is dropped.
    public func stop() {
        stopped = true
        timer?.invalidate()
        timer = nil
    }

    /// Takes in the audio recorded since the last call. The timer calls this; tests call it directly.
    func tick() {
        guard !stopped else { return }

        let fresh = audioSince(heard)
        var offset = 0
        while offset + Self.frame <= fresh.count {
            heard += Self.frame
            if voice.hear(fresh[offset ..< offset + Self.frame]) {
                // The first speech of a new phrase: the tail starts just before it rather
                // than back in the silence, which nobody needs transcribed again.
                if lastSpeech <= tailStart { tailStart = max(tailStart, heard - Self.leadIn) }
                lastSpeech = heard
            }
            offset += Self.frame
        }

        if !limitReached, Double(heard - lastSpeech) >= Self.silenceLimit * Double(Self.sampleRate) {
            limitReached = true
            onSilenceLimit?()
            if stopped { return }
        }

        // A pass when there's speech nobody has transcribed yet, or when a phrase just
        // ended in a pause and its tail should settle. Silence alone starts nothing.
        let paused = heard - lastSpeech >= Self.pause
        let tail = heard - tailStart
        let fresher = lastSpeech > draftedTo && tail >= Self.shortestTail && heard - draftedTo >= Self.cadence
        guard drafting, pass == nil, lastSpeech > tailStart, fresher || (paused && draftedTo < heard) else { return }

        let start = tailStart
        let samples = audioSince(start)
        let settle = paused || tail >= Self.longestTail
        draftedTo = start + samples.count
        pass = Task { [weak self] in
            await self?.run(start: start, samples: samples, settle: settle)
        }
    }

    /// Waits for the pass in flight, if there is one; for tests.
    func finishPass() async {
        await pass?.value
    }

    private func run(start: Int, samples: [Float], settle: Bool) async {
        defer { pass = nil }
        let began = Date()
        let text: String
        do {
            text = try await transcribe(samples).trimmingCharacters(in: .whitespacesAndNewlines)
        } catch {
            drafting = false
            return
        }
        guard !stopped else { return }

        // A draft this slow no longer feels live, and every pass makes the final
        // transcription wait for it. The words shown so far stay.
        if Date().timeIntervalSince(began) > Self.slowPass { drafting = false }

        var tailText = text
        if settle {
            settled = settled.isEmpty ? text : text.isEmpty ? settled : "\(settled) \(text)"
            tailStart = start + samples.count
            tailText = ""
        }
        onDraft?(settled, tailText)
    }
}

/// Speech versus room noise, 20 ms at a time. The noise floor drops to any
/// quieter frame at once and creeps up over about ten seconds, so a fan or a
/// hum soon stops counting as speech. A voice has to keep going for about a
/// fifth of a second to count, so a key click or a cough doesn't.
struct VoiceActivity {
    private static let quietestVoice = 0.01
    private var floor = -1.0
    private var activity = 0.0

    mutating func hear(_ frame: ArraySlice<Float>) -> Bool {
        var sum = 0.0
        for sample in frame { sum += Double(sample * sample) }
        let level = (sum / Double(frame.count)).squareRoot()

        floor = floor < 0 || level < floor ? level : floor + (level - floor) * 0.002
        let voiced = level > max(Self.quietestVoice, floor * 2.5)
        activity = min(max(activity + (voiced ? 0.02 : -0.006), 0), 0.3)
        return activity >= 0.2
    }
}
