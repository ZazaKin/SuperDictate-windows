import Foundation
import Observation

struct HistoryEntry: Codable, Identifiable, Equatable {
    var id = UUID()
    let date: Date
    let text: String
    let seconds: Double
}

/// The most recent dictations, newest first, in Application Support. They stay
/// on this Mac; clearing removes the file's contents for good.
@MainActor
@Observable
final class History {
    static let keep = 100

    private(set) var entries: [HistoryEntry] = []

    private let file: URL

    init() {
        let support = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        let folder = support.appendingPathComponent("SuperDictate", isDirectory: true)
        try? FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        file = folder.appendingPathComponent("history.json")
        if let data = try? Data(contentsOf: file),
           let saved = try? JSONDecoder().decode([HistoryEntry].self, from: data) {
            entries = saved
        }
    }

    func add(_ text: String, seconds: Double) {
        entries.insert(HistoryEntry(date: .now, text: text, seconds: seconds), at: 0)
        if entries.count > Self.keep { entries.removeLast(entries.count - Self.keep) }
        save()
    }

    func remove(_ entry: HistoryEntry) {
        entries.removeAll { $0.id == entry.id }
        save()
    }

    func clear() {
        entries.removeAll()
        save()
    }

    private func save() {
        guard let data = try? JSONEncoder().encode(entries) else { return }
        try? data.write(to: file, options: .atomic)
    }
}
