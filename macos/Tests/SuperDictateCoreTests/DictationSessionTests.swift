import XCTest
@testable import SuperDictateCore

final class DictationSessionTests: XCTestCase {
    private func recording() -> DictationSession {
        var session = DictationSession(phase: .ready)
        _ = session.handle(.start(micAllowed: true))
        return session
    }

    func testModelGetsReady() {
        var session = DictationSession()
        XCTAssertEqual(session.handle(.prepare("Downloading…")), [.loadModel])
        XCTAssertEqual(session.handle(.prepare("Downloading…")), [], "already on its way")
        _ = session.handle(.progress("Optimizing…"))
        XCTAssertEqual(session.phase, .preparing("Optimizing…"))
        _ = session.handle(.modelFailed("No internet"))
        XCTAssertEqual(session.phase, .needsModel("No internet"))
        _ = session.handle(.progress("Late"))
        XCTAssertEqual(session.phase, .needsModel("No internet"), "a late step doesn't restart it")
        _ = session.handle(.prepare("Downloading…"))
        _ = session.handle(.modelLoaded)
        XCTAssertEqual(session.phase, .ready)
        XCTAssertEqual(session.handle(.prepare("Downloading…")), [])
    }

    func testWelcomeUntilTheModelIsReady() {
        for phase in [DictationSession.Phase.needsModel(nil), .preparing("Downloading…")] {
            var session = DictationSession(phase: phase)
            XCTAssertEqual(session.handle(.toggle(micAllowed: true, pressReturn: false)), [.showWelcome])
            XCTAssertEqual(session.handle(.start(micAllowed: true)), [.showWelcome])
            XCTAssertEqual(session.phase, phase)
        }
    }

    func testMicrophone() {
        var session = DictationSession(phase: .ready)
        XCTAssertEqual(session.handle(.start(micAllowed: false)), [.notice(.allowMicrophone), .showWelcome])
        XCTAssertEqual(session.phase, .ready)
        XCTAssertEqual(session.overlay, .notice(.allowMicrophone))

        XCTAssertEqual(session.handle(.start(micAllowed: true)), [.record])
        XCTAssertEqual(session.overlay, .listening, "a new dictation clears the notice")
        XCTAssertEqual(session.handle(.microphoneFailed), [.notice(.microphoneUnavailable)])
        XCTAssertEqual(session.phase, .ready)
    }

    func testDictation() {
        var session = recording()
        XCTAssertEqual(session.handle(.toggle(micAllowed: true, pressReturn: true)), [.stopRecording])
        XCTAssertEqual(session.overlay, .processing)
        XCTAssertEqual(session.handle(.toggle(micAllowed: true, pressReturn: true)), [], "busy")
        XCTAssertEqual(session.handle(.recorded(seconds: 4)), [.transcribe])
        XCTAssertEqual(session.handle(.transcribed("Hello there")), [.deliver("Hello there", seconds: 4, pressReturn: true)])
        XCTAssertEqual(session.phase, .ready)
        XCTAssertEqual(session.handle(.typed(true)), [])
        XCTAssertEqual(session.overlay, .hidden)
    }

    func testATapIsNotADictation() {
        var session = recording()
        _ = session.handle(.finish(pressReturn: false))
        XCTAssertEqual(session.handle(.recorded(seconds: 0.2)), [])
        XCTAssertEqual(session.phase, .ready)
        XCTAssertEqual(session.overlay, .hidden)
    }

    func testSilenceLimitNeverPressesReturn() {
        var session = recording()
        XCTAssertEqual(session.handle(.silenceLimit), [.stopRecording])
        _ = session.handle(.recorded(seconds: 60))
        XCTAssertEqual(session.handle(.transcribed("Done")), [.deliver("Done", seconds: 60, pressReturn: false)])
    }

    func testCancelOnlyWhileRecording() {
        var session = DictationSession(phase: .ready)
        XCTAssertEqual(session.handle(.cancel), [])
        session = recording()
        XCTAssertEqual(session.handle(.cancel), [.dropRecording, .notice(.cancelled)])
        XCTAssertEqual(session.phase, .ready)

        session = recording()
        _ = session.handle(.finish(pressReturn: false))
        XCTAssertEqual(session.handle(.cancel), [], "too late once it's processing")
        XCTAssertEqual(session.phase, .processing)
    }

    func testWhatComesBack() {
        var session = recording()
        _ = session.handle(.finish(pressReturn: false))
        _ = session.handle(.recorded(seconds: 2))
        XCTAssertEqual(session.handle(.transcribed("")), [.notice(.noSpeech)])

        session = recording()
        _ = session.handle(.finish(pressReturn: false))
        _ = session.handle(.recorded(seconds: 2))
        XCTAssertEqual(session.handle(.transcriptionFailed), [.notice(.failed)])
        XCTAssertEqual(session.phase, .ready)

        XCTAssertEqual(session.handle(.typed(false)), [.notice(.copied)])
        XCTAssertEqual(session.overlay, .notice(.copied))
        _ = session.handle(.noticeEnded)
        XCTAssertEqual(session.overlay, .hidden)
    }

    func testSample() {
        var session = DictationSession(phase: .ready)
        XCTAssertEqual(session.handle(.sample(true)), [.startSample])
        XCTAssertEqual(session.handle(.sample(true)), [])
        XCTAssertEqual(session.overlay, .listening)

        XCTAssertEqual(session.handle(.start(micAllowed: true)), [.stopSample, .record], "a real dictation takes over")
        XCTAssertFalse(session.isSampling)
        XCTAssertEqual(session.handle(.sample(true)), [], "never during a recording")
        _ = session.handle(.finish(pressReturn: false))
        XCTAssertEqual(session.handle(.sample(true)), [], "or while processing")
        _ = session.handle(.recorded(seconds: 0))
        XCTAssertEqual(session.handle(.sample(true)), [.startSample])
        XCTAssertEqual(session.handle(.sample(false)), [.stopSample])
        XCTAssertEqual(session.overlay, .hidden)
    }
}
