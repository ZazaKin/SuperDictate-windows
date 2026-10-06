using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SuperDictate.Storage;

namespace SuperDictate.Speech;

/// <summary>
/// The live side of one recording. While the microphone runs, it keeps a draft
/// of what has been said so far, and it notices when nothing has been said for
/// <see cref="SilenceLimit"/>. Time here is audio time (samples heard), so the
/// result doesn't depend on how often <see cref="Tick"/> runs.
///
/// The draft has two parts. Settled words come from phrases that ended in a
/// pause and never change. The tail is everything since, transcribed again on
/// every pass, so its words can still be corrected. A pass only ever hears the
/// tail, so drafts stay quick however long the dictation runs. Nothing here is
/// pasted: the whole recording is transcribed again when it stops.
/// </summary>
internal sealed class LiveSession : IDisposable
{
    public static readonly TimeSpan SilenceLimit = TimeSpan.FromMinutes(1);

    private const int Rate = Audio.MicrophoneCapture.TargetSampleRate;
    private const int Frame = Rate / 50;                     // 20 ms
    private const int Pause = Rate * 7 / 10;                 // A gap this long ends a phrase.
    private const int ShortestTail = Rate;                   // Less than this mid-phrase isn't worth a pass.
    private const int Cadence = Rate * 6 / 10;               // New audio between passes while talking.
    private const int LeadIn = Rate / 2;                     // Kept before detected speech: detection lags its start.
    private const int LongestTail = Rate * 12;               // Settle anyway after this much talk without a pause.
    private static readonly TimeSpan SlowPass = TimeSpan.FromSeconds(2);

    private readonly Func<int, float[]> _audioSince;
    private readonly Func<float[], Task<string>> _transcribe;
    private readonly object _gate = new();
    private readonly VoiceActivity _voice = new();
    private Timer? _timer;

    private int _heard;        // Samples run through voice detection so far.
    private int _lastSpeech;   // Where speech was last heard.
    private int _tailStart;    // Where the unsettled tail begins.
    private int _draftedTo;    // Where the audio of the latest pass ended.
    private string _settled = "";
    private bool _passRunning;
    private bool _drafting = true;
    private bool _limitReached;
    private bool _stopped;

    /// <param name="audioSince">The recording from a sample index on, as <see cref="Audio.MicrophoneCapture.Since"/> gives it.</param>
    /// <param name="transcribe">A draft transcription of some samples.</param>
    public LiveSession(Func<int, float[]> audioSince, Func<float[], Task<string>> transcribe)
    {
        _audioSince = audioSince;
        _transcribe = transcribe;
    }

    /// <summary>The draft changed: the settled words, then the tail that may still change. Raised off the UI thread.</summary>
    public event Action<string, string>? DraftChanged;

    /// <summary>Nothing that sounds like speech for <see cref="SilenceLimit"/>. Raised once, off the UI thread.</summary>
    public event Action? SilenceLimitReached;

    public void Start() => _timer ??= new Timer(_ =>
    {
        // A throw on a timer thread would end the app; the preview isn't worth that.
        try
        {
            Tick();
        }
        catch (Exception error)
        {
            AppLogger.Error("Live preview tick failed", error);
            Stop();
        }
    }, null, 100, 100);

    /// <summary>Stops listening. A pass still running finishes, but its draft is dropped.</summary>
    public void Stop()
    {
        lock (_gate) _stopped = true;
        _timer?.Dispose();
    }

    public void Dispose() => Stop();

    /// <summary>Takes in the audio recorded since the last call. The timer calls this; the self-test calls it directly.</summary>
    internal void Tick()
    {
        bool limit;
        (int Start, float[] Samples, bool Settle)? pass = null;
        lock (_gate)
        {
            if (_stopped) return;

            var fresh = _audioSince(_heard);
            for (var offset = 0; offset + Frame <= fresh.Length; offset += Frame)
            {
                _heard += Frame;
                if (!_voice.Hear(fresh.AsSpan(offset, Frame))) continue;

                // The first speech of a new phrase: the tail starts just before it rather
                // than back in the silence, which nobody needs transcribed again.
                if (_lastSpeech <= _tailStart) _tailStart = Math.Max(_tailStart, _heard - LeadIn);
                _lastSpeech = _heard;
            }

            limit = !_limitReached && _heard - _lastSpeech >= SilenceLimit.TotalSeconds * Rate;
            _limitReached |= limit;

            // A pass when there's speech nobody has transcribed yet, or when a phrase just
            // ended in a pause and its tail should settle. Silence alone starts nothing.
            var paused = _heard - _lastSpeech >= Pause;
            var tail = _heard - _tailStart;
            if (_drafting && !_passRunning && _lastSpeech > _tailStart
                && ((_lastSpeech > _draftedTo && tail >= ShortestTail && _heard - _draftedTo >= Cadence) || (paused && _draftedTo < _heard)))
            {
                var samples = _audioSince(_tailStart);
                _passRunning = true;
                _draftedTo = _tailStart + samples.Length;
                pass = (_tailStart, samples, paused || tail >= LongestTail);
            }
        }

        if (limit) SilenceLimitReached?.Invoke();
        if (pass is { } next) _ = RunPass(next.Start, next.Samples, next.Settle);
    }

    private async Task RunPass(int start, float[] samples, bool settle)
    {
        var clock = Stopwatch.StartNew();
        string text;
        try
        {
            text = (await _transcribe(samples).ConfigureAwait(false)).Trim();
        }
        catch (Exception error)
        {
            AppLogger.Warn($"Live preview stopped: {error.Message}");
            lock (_gate)
            {
                _passRunning = false;
                _drafting = false;
            }

            return;
        }

        string settled, tail;
        lock (_gate)
        {
            _passRunning = false;
            if (_stopped) return;

            // A draft this slow no longer feels live, and every pass makes the final
            // transcription wait for it. The words shown so far stay.
            if (clock.Elapsed > SlowPass)
            {
                _drafting = false;
                AppLogger.Info($"Live preview off for this dictation: a pass took {clock.ElapsedMilliseconds} ms.");
            }

            if (settle)
            {
                _settled = Join(_settled, text);
                _tailStart = start + samples.Length;
                text = "";
            }

            settled = _settled;
            tail = text;
        }

        DraftChanged?.Invoke(settled, tail);
    }

    private static string Join(string first, string second) =>
        first.Length == 0 ? second : second.Length == 0 ? first : $"{first} {second}";

    /// <summary>
    /// Speech versus room noise, 20 ms at a time. The noise floor drops to any
    /// quieter frame at once and creeps up over about ten seconds, so a fan or a
    /// hum soon stops counting as speech. A voice has to keep going for about a
    /// fifth of a second to count, so a key click or a cough doesn't.
    /// </summary>
    private sealed class VoiceActivity
    {
        private const double QuietestVoice = 0.01;
        private double _floor = -1;
        private double _activity;

        public bool Hear(ReadOnlySpan<float> frame)
        {
            var sum = 0.0;
            foreach (var sample in frame) sum += sample * sample;
            var level = Math.Sqrt(sum / frame.Length);

            _floor = _floor < 0 || level < _floor ? level : _floor + ((level - _floor) * 0.002);
            var voiced = level > Math.Max(QuietestVoice, _floor * 2.5);
            _activity = Math.Clamp(_activity + (voiced ? 0.02 : -0.006), 0, 0.3);
            return _activity >= 0.2;
        }
    }
}
