import Foundation

/// One word of the live caption. Its `id` lasts as long as the word does, so a
/// view animates exactly the words that arrive and leave.
public struct CaptionWord: Identifiable, Equatable, Sendable {
    public let id: Int
    public let text: String
    /// Settled words won't change any more; the others may still be corrected.
    public internal(set) var settled: Bool
    /// How long after the update this word's entrance starts, so new words arrive one after another.
    public let delay: Double
}

/// The words of the live preview. A word that didn't change keeps its identity;
/// everything after the first change is new. Old words, long scrolled out of
/// view, are let go so a long dictation doesn't keep thousands of them.
public struct Caption: Equatable, Sendable {
    public static let mostWords = 48
    public static let stagger = 0.055

    public private(set) var words: [CaptionWord] = []
    private var dropped = 0
    private var nextID = 0

    public init() {}

    public var isEmpty: Bool { words.isEmpty }

    /// - Parameters:
    ///   - settled: Words that won't change any more.
    ///   - tail: Words after them that may still be corrected.
    public mutating func show(settled: String, tail: String) {
        let settledWords = Self.split(settled)
        let next = settledWords + Self.split(tail)
        if next.count < dropped {
            // Settled words only ever grow; start over rather than misalign.
            words.removeAll()
            dropped = 0
        }

        var keep = 0
        while keep < words.count, dropped + keep < next.count, words[keep].text == next[dropped + keep] {
            keep += 1
        }
        words.removeSubrange(keep ..< words.count)
        for index in words.indices {
            words[index].settled = dropped + index < settledWords.count
        }

        for (order, index) in (dropped + keep ..< next.count).enumerated() {
            words.append(CaptionWord(id: nextID, text: next[index],
                                     settled: index < settledWords.count,
                                     delay: Double(order) * Self.stagger))
            nextID += 1
        }

        let extra = words.count - Self.mostWords
        if extra > 0 {
            words.removeFirst(extra)
            dropped += extra
        }
    }

    static func split(_ text: String) -> [String] {
        text.split(whereSeparator: \.isWhitespace).map(String.init)
    }
}
