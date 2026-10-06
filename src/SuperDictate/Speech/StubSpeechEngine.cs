using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace SuperDictate.Speech;

/// <summary>
/// Placeholder engine that echoes recording duration instead of real speech.
/// Swap for a real ONNX-backed implementation once a model is available.
/// </summary>
public sealed class StubSpeechEngine : ISpeechEngine
{
    public string ModelId => "stub";

    public bool IsLoaded { get; private set; }

    public Task LoadAsync(CancellationToken cancellationToken)
    {
        IsLoaded = true;
        return Task.CompletedTask;
    }

    public Task<string> TranscribeAsync(float[] samples, CancellationToken cancellationToken, bool draft = false)
    {
        var seconds = (double)samples.Length / Audio.MicrophoneCapture.TargetSampleRate;
        var text = string.Format(
            CultureInfo.InvariantCulture,
            "[stub transcription – {0:F1}s of audio]",
            seconds);

        return Task.FromResult(text);
    }

    public void Dispose()
    {
        // Nothing to release.
    }
}
