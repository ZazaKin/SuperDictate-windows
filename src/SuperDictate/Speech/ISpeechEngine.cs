using System;
using System.Threading;
using System.Threading.Tasks;

namespace SuperDictate.Speech;

/// <summary>
/// Everything below this interface runs locally. No implementation may send
/// audio off the machine.
/// </summary>
public interface ISpeechEngine : IDisposable
{
    string ModelId { get; }

    bool IsLoaded { get; }

    Task LoadAsync(CancellationToken cancellationToken);

    /// <param name="samples">16 kHz mono float samples in the range -1..1.</param>
    /// <param name="draft">
    /// A quick look for the live preview: it skips the slow second attempt on audio
    /// with no clear speech, and returns nothing rather than guess. Calls run one
    /// at a time, so a final transcription waits for a draft already running.
    /// </param>
    Task<string> TranscribeAsync(float[] samples, CancellationToken cancellationToken, bool draft = false);
}
