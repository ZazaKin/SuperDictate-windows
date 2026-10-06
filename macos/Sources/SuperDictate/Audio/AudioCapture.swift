import AVFoundation

/// Records the default microphone as 16 kHz mono floats, the format Parakeet
/// takes. The tap runs on an audio thread, so everything it shares with the app
/// sits behind one lock.
///
/// Learned from the original Mac app: the tap takes the input-scope format
/// (the output scope can keep a stale sample rate after a device change), the
/// channels are mixed down by hand, and the converter is told `.noDataNow`
/// rather than end of stream, or every later buffer would come out empty.
final class AudioCapture: @unchecked Sendable {
    static let sampleRate = 16_000.0

    enum Failure: LocalizedError {
        case noMicrophone
        var errorDescription: String? { "No microphone is available." }
    }

    private let engine = AVAudioEngine()
    private let lock = NSLock()
    private var samples: [Float] = []
    private var level: Float = 0
    private var running = false

    func start() throws {
        let input = engine.inputNode
        let format = input.inputFormat(forBus: 0)
        guard format.sampleRate > 0, format.channelCount > 0,
              let mono = AVAudioFormat(commonFormat: .pcmFormatFloat32, sampleRate: format.sampleRate,
                                       channels: 1, interleaved: false),
              let target = AVAudioFormat(commonFormat: .pcmFormatFloat32, sampleRate: Self.sampleRate,
                                         channels: 1, interleaved: false),
              let converter = AVAudioConverter(from: mono, to: target) else {
            throw Failure.noMicrophone
        }

        locked {
            samples.removeAll(keepingCapacity: true)
            level = 0
            running = true
        }
        input.installTap(onBus: 0, bufferSize: 1024, format: format) { [weak self] buffer, _ in
            self?.take(buffer, mono: mono, target: target, converter: converter)
        }
        engine.prepare()
        do {
            try engine.start()
        } catch {
            input.removeTap(onBus: 0)
            locked { running = false }
            throw error
        }
    }

    /// Stops and returns everything recorded.
    func stop() -> [Float] {
        engine.inputNode.removeTap(onBus: 0)
        engine.stop()
        return locked {
            running = false
            defer { samples = [] }
            return samples
        }
    }

    /// What has been recorded from sample `start` on.
    func since(_ start: Int) -> [Float] {
        locked { start >= samples.count ? [] : Array(samples[start...]) }
    }

    /// The voice level, smoothed, for the bars in the capsule.
    var currentLevel: Float { locked { running ? level : 0 } }

    private func take(_ buffer: AVAudioPCMBuffer, mono: AVAudioFormat, target: AVAudioFormat,
                      converter: AVAudioConverter) {
        guard locked({ running }), let channels = buffer.floatChannelData, buffer.frameLength > 0,
              let mixed = AVAudioPCMBuffer(pcmFormat: mono, frameCapacity: buffer.frameLength),
              let mixedData = mixed.floatChannelData?[0] else { return }

        let frames = Int(buffer.frameLength)
        let count = Int(buffer.format.channelCount)
        let interleaved = buffer.format.isInterleaved
        let stride = buffer.stride
        for frame in 0 ..< frames {
            var sum: Float = 0
            for channel in 0 ..< count {
                sum += interleaved ? channels[0][frame * stride + channel] : channels[channel][frame]
            }
            mixedData[frame] = sum / Float(count)
        }
        mixed.frameLength = buffer.frameLength

        let capacity = AVAudioFrameCount(Double(frames) * target.sampleRate / mono.sampleRate) + 64
        guard let converted = AVAudioPCMBuffer(pcmFormat: target, frameCapacity: capacity) else { return }
        let input = OneBuffer(mixed)
        var error: NSError?
        let status = converter.convert(to: converted, error: &error) { _, inputStatus in
            input.next(inputStatus)
        }
        guard status != .error, let data = converted.floatChannelData?[0] else { return }

        let output = Array(UnsafeBufferPointer(start: data, count: Int(converted.frameLength)))
        var squares: Float = 0
        for sample in output { squares += sample * sample }
        let blockLevel = output.isEmpty ? 0 : (squares / Float(output.count)).squareRoot()

        locked {
            guard running else { return }
            samples.append(contentsOf: output)
            level = level * 0.7 + blockLevel * 0.3 // Light smoothing, so plosives don't flicker.
        }
    }

    private func locked<T>(_ body: () -> T) -> T {
        lock.lock()
        defer { lock.unlock() }
        return body()
    }
}

/// Hands the converter one buffer, then "no data for now", which keeps it usable for the next one.
private final class OneBuffer: @unchecked Sendable {
    private var buffer: AVAudioPCMBuffer?

    init(_ buffer: AVAudioPCMBuffer) {
        self.buffer = buffer
    }

    func next(_ status: UnsafeMutablePointer<AVAudioConverterInputStatus>) -> AVAudioBuffer? {
        guard let buffer else {
            status.pointee = .noDataNow
            return nil
        }
        self.buffer = nil
        status.pointee = .haveData
        return buffer
    }
}
