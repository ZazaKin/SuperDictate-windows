import XCTest
@testable import SuperDictateCore

/// Drives a live session with made-up audio, 100 ms per tick like its timer.
/// The same cases as the Windows self-test (`live.*`).
@MainActor
final class LiveSessionTests: XCTestCase {
    private struct Run {
        var limitAt: Double?
        var settled = ""
        var tail = ""
        var passes = 0
        var sawTail = false
    }

    private var random = SystemRandomNumberGenerator()

    private func noise(_ amplitude: Double) -> Float {
        Float(Double.random(in: -amplitude ... amplitude, using: &random))
    }

    private func quiet(_ t: Double) -> Float { noise(0.002) }

    /// Syllables: a quarter second of voice, then a short gap.
    private func talk(_ t: Double) -> Float {
        (t * 1000).truncatingRemainder(dividingBy: 350) < 250
            ? Float(sin(2 * Double.pi * 220 * t) * 0.12) + noise(0.002)
            : quiet(t)
    }

    private func drive(seconds: Double, _ signal: (Double) -> Float) async -> Run {
        var audio: [Float] = []
        var run = Run()
        let session = LiveSession(
            audioSince: { start in start >= audio.count ? [] : Array(audio[start...]) },
            transcribe: { _ in
                run.passes += 1
                return "phrase"
            })
        session.onSilenceLimit = { run.limitAt = run.limitAt ?? Double(audio.count) / 16_000 }
        session.onDraft = { settled, tail in
            run.settled = settled
            run.tail = tail
            run.sawTail = run.sawTail || (settled.isEmpty && tail == "phrase")
        }
        for _ in 0 ..< Int(seconds * 10) {
            for _ in 0 ..< 1600 { audio.append(signal(Double(audio.count) / 16_000)) }
            session.tick()
            await session.finishPass()
        }
        session.stop()
        return run
    }

    func testSilenceLimit() async {
        let silent = await drive(seconds: 61, quiet)
        XCTAssertNotNil(silent.limitAt)
        XCTAssertEqual(silent.limitAt ?? 0, 60, accuracy: 0.3)

        let spoke = await drive(seconds: 61) { t in t < 3 ? self.talk(t) : self.quiet(t) }
        XCTAssertNil(spoke.limitAt, "speech moves the minute forward")
    }

    func testNoiseIsNotSpeech() async {
        let fan = await drive(seconds: 61) { _ in self.noise(0.05) }
        XCTAssertNotNil(fan.limitAt, "a steady fan doesn't count as speech")
        let clicks = await drive(seconds: 61) { t in
            (t * 1000).truncatingRemainder(dividingBy: 500) < 15 ? self.noise(0.3) : self.quiet(t)
        }
        XCTAssertNotNil(clicks.limitAt, "key clicks don't count as speech")
    }

    func testPhrasesSettle() async {
        // Two phrases with a pause after each: each shows as a tail first, then settles.
        // The long silence between them runs no passes (it would take about 30).
        let run = await drive(seconds: 25) { t in t < 2 || (t >= 20 && t < 22) ? self.talk(t) : self.quiet(t) }
        XCTAssertEqual(run.settled, "phrase phrase")
        XCTAssertEqual(run.tail, "")
        XCTAssertTrue(run.sawTail)
        XCTAssertLessThanOrEqual(run.passes, 10)
    }

    func testSilenceStartsNoPasses() async {
        let silent = await drive(seconds: 5, quiet)
        XCTAssertEqual(silent.passes, 0)
        let fan = await drive(seconds: 5) { _ in self.noise(0.05) }
        XCTAssertEqual(fan.passes, 0)
    }
}

final class CaptionTests: XCTestCase {
    func testUnchangedWordsKeepTheirIdentity() {
        var caption = Caption()
        caption.show(settled: "", tail: "hello")
        let hello = caption.words[0].id
        caption.show(settled: "hello there", tail: "")
        caption.show(settled: "hello there", tail: "how are")
        caption.show(settled: "hello there", tail: "how is it")

        XCTAssertEqual(caption.words.map(\.text), ["hello", "there", "how", "is", "it"])
        XCTAssertEqual(caption.words[0].id, hello, "a word that didn't change is the same word")
        XCTAssertEqual(caption.words.filter(\.settled).count, 2)
        XCTAssertEqual(caption.words.last?.delay ?? 0, 2 * Caption.stagger, accuracy: 1e-9,
                       "new words arrive one after another")
    }

    func testOldWordsAreLetGo() {
        var caption = Caption()
        caption.show(settled: (0 ..< 100).map { "w\($0)" }.joined(separator: " "), tail: "tail")
        XCTAssertLessThanOrEqual(caption.words.count, Caption.mostWords)
        XCTAssertEqual(caption.words.suffix(2).map(\.text), ["w99", "tail"])

        caption.show(settled: (0 ..< 100).map { "w\($0)" }.joined(separator: " "), tail: "tail end")
        XCTAssertEqual(caption.words.suffix(3).map(\.text), ["w99", "tail", "end"])
    }
}
