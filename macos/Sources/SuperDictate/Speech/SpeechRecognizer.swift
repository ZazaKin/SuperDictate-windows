import FluidAudio
import Foundation
import SuperDictateCore

extension Preferences {
    /// The alphabet to hold recognition to, from the user's languages; nil lets it hear anything.
    static var languageHint: Language? {
        SpokenLanguage.filter(mode: language, chosen: languages).flatMap(Language.init(rawValue:))
    }
}

/// NVIDIA Parakeet TDT v3 on the Apple Neural Engine, through FluidAudio. The
/// model downloads once from Hugging Face into Application Support; after that
/// everything happens on this Mac.
///
/// The Neural Engine must never run two transcriptions at once, and an actor
/// alone doesn't prevent that (it is re-entered while one awaits). So every
/// request waits for the one before it: a live-preview draft and the final
/// transcription simply queue.
actor SpeechRecognizer {
    enum Failure: LocalizedError {
        case notLoaded
        var errorDescription: String? { "The speech model isn't loaded yet." }
    }

    /// Where FluidAudio keeps the model.
    static var modelFolder: URL { AsrModels.defaultCacheDirectory(for: .v3) }

    static var isDownloaded: Bool {
        FileManager.default.fileExists(atPath: modelFolder.path)
    }

    private var manager: AsrManager?
    private var last: Task<String, Error>?

    var isLoaded: Bool { manager != nil }

    /// Downloads the model if it isn't here yet, then compiles it for the Neural Engine.
    func load(progress: DownloadUtils.ProgressHandler?) async throws {
        guard manager == nil else { return }
        let folder = try await AsrModels.download(version: .v3, progressHandler: progress)
        let models = try await AsrModels.load(from: folder, version: .v3, progressHandler: progress)
        manager = AsrManager(config: .default, models: models)
    }

    func transcribe(_ samples: [Float], language: Language?) async throws -> String {
        let previous = last
        let job = Task { () async throws -> String in
            _ = try? await previous?.value
            return try await self.run(samples, language: language)
        }
        last = job
        return try await job.value
    }

    private func run(_ samples: [Float], language: Language?) async throws -> String {
        guard let manager else { throw Failure.notLoaded }
        var state = try TdtDecoderState()
        let result = try await manager.transcribe(samples, decoderState: &state, language: language)
        return result.text.trimmingCharacters(in: .whitespacesAndNewlines)
    }
}
