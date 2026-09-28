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
    Task<string> TranscribeAsync(float[] samples, CancellationToken cancellationToken);
}
