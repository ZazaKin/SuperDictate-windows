import FluidAudio
import Foundation

/// The language to listen for. Parakeet recognizes 25 European languages and
/// tells them apart by itself; naming one helps short phrases come out in the
/// right alphabet.
enum DictationLanguage: String, CaseIterable, Identifiable {
    case auto
    case english = "en"
    case german = "de"
    case russian = "ru"
    case ukrainian = "uk"
    case polish = "pl"
    case spanish = "es"
    case french = "fr"
    case italian = "it"
    case portuguese = "pt"
    case czech = "cs"
    case romanian = "ro"
    case bulgarian = "bg"

    var id: Self { self }

    var title: String {
        switch self {
        case .auto: "Automatic"
        case .english: "English"
        case .german: "German"
        case .russian: "Russian"
        case .ukrainian: "Ukrainian"
        case .polish: "Polish"
        case .spanish: "Spanish"
        case .french: "French"
        case .italian: "Italian"
        case .portuguese: "Portuguese"
        case .czech: "Czech"
        case .romanian: "Romanian"
        case .bulgarian: "Bulgarian"
        }
    }

    var hint: Language? {
        switch self {
        case .auto: nil
        case .english: .english
        case .german: .german
        case .russian: .russian
        case .ukrainian: .ukrainian
        case .polish: .polish
        case .spanish: .spanish
        case .french: .french
        case .italian: .italian
        case .portuguese: .portuguese
        case .czech: .czech
        case .romanian: .romanian
        case .bulgarian: .bulgarian
        }
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
