using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using SuperDictate.Audio;
using SuperDictate.Input;
using SuperDictate.Speech;
using SuperDictate.Storage;
using static SuperDictate.Ui.DictationSession;

namespace SuperDictate.Ui;

public sealed record DictationResultArgs(string Text, double DurationSeconds, string Model, string? ErrorMessage);

/// <summary>
/// Orchestrates the full dictation pipeline: keyboard hook → mic capture →
/// speech engine → text injection. The rules are <see cref="DictationSession"/>'s;
/// this carries out what it asks for. All UI callbacks are dispatched on the
/// WPF dispatcher thread.
/// </summary>
public sealed class DictationController : IDisposable
{
    private readonly Settings _settings;
    private readonly KeyboardHook _hook;
    private readonly MicrophoneCapture _mic;
    private readonly CapsuleOverlay _overlay;
    private ISpeechEngine _engine;
    private readonly Dispatcher _dispatcher;
    private readonly CancellationTokenSource _cts = new();

    private readonly DictationSession _session = new();

    // Engine restarts can overlap (a save while the engine still loads); only the newest
    // one may install its engine, so the engine in use is never one already disposed.
    private readonly object _engineGate = new();
    private int _engineGeneration;
    private LiveSession? _live;
    private float[] _recorded = Array.Empty<float>();
    private bool _disposed;

    public string? LastTranscript { get; private set; }
    public double LastAudioLevel { get; private set; }
    public string ActiveMicrophoneName => _mic.CurrentDeviceName ?? "Default Microphone";

    /// <summary>Loaded model and the device the worker reported, e.g. "whisper-small · cuda".</summary>
    public string EngineSummary => _engine is WhisperSpeechEngine { DeviceUsed: { } device }
        ? $"{_engine.ModelId} · {device}"
        : _engine.ModelId;

    /// <summary>Pauses hotkeys while settings records a new chord, so the chord can't start dictation.</summary>
    public bool HotkeysSuspended
    {
        get => _hook.Suspended;
        set => _hook.Suspended = value;
    }

    public event EventHandler<DictationResultArgs>? DictationCompleted;

    /// <summary>Someone tried to dictate before setup was finished; the tray opens the setup page.</summary>
    public event EventHandler? SetupNeeded;

    /// <summary>The speech runtime and the chosen model are installed, so dictation can work.</summary>
    public bool IsSetUp => SpeechRuntime.IsInstalled && ModelLibrary.IsPresent(AppPaths.Models, _settings.ModelId);

    /// <summary>The model the engine has loaded (or will load); its files are open and can't be deleted.</summary>
    public string LoadedModelId => _engine.ModelId;
    public event EventHandler<double>? LevelChanged;

    public DictationController(Settings settings)
    {
        _settings = settings;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _hook = new KeyboardHook();
        _mic = new MicrophoneCapture();
        _overlay = new CapsuleOverlay(CapsuleLook.From(settings));

        AppLogger.Info("Initializing SuperDictate DictationController...");

        try
        {
            _engine = new WhisperSpeechEngine(settings.Language, settings.ModelId, null, settings.SelectedLanguages);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Whisper speech engine instantiation failed, falling back to stub", ex);
            _engine = new StubSpeechEngine();
        }

        _hook.HoldMode = settings.PressAndHold;
        _hook.Bind(HotkeyKind.Primary, settings.PrimaryHotkey);
        _hook.Bind(HotkeyKind.History, settings.HistoryHotkey);

        if (settings.AlternateHotkeyEnabled)
        {
            _hook.Bind(HotkeyKind.Alternate, settings.AlternateHotkey);
        }

        _hook.Pressed += OnHotkeyPressed;
        _hook.Released += OnHotkeyReleased;
        _hook.Cancelled += OnCancelled;
        _mic.LevelChanged += OnLevel;
    }

    public event EventHandler<DictationState>? StateChanged;

    public DictationState State => _session.State;

    public void Start()
    {
        _hook.Install();
        RestartEngine();
        ShowMicButton(_settings.ShowMicButton);
    }

    private MicButton? _micButton;

    /// <summary>Shows or removes the floating microphone button. Only the running app calls this, never the self-test.</summary>
    public void ShowMicButton(bool show)
    {
        if (!show)
        {
            _micButton?.Close();
            _micButton = null;
            return;
        }

        _micButton ??= new MicButton(this, _settings);
        _micButton.ShowAtSavedSpot();
    }

    public void Toggle(bool pressEnter) => Send(new Event.Toggle(pressEnter, IsSetUp, _engine.IsLoaded));

    /// <summary>
    /// Installs the speech runtime (and optionally the GPU pack); the engine then
    /// starts on it, whether or not the settings window is still open.
    /// </summary>
    public RuntimeInstall InstallRuntime(bool includeGpu)
    {
        var install = RuntimeInstall.Start(includeGpu);
        _ = install.Finished.ContinueWith(task => _dispatcher.BeginInvoke(() =>
        {
            if (task.Result is null) Send(new Event.Installed(IsSetUp));
        }), TaskScheduler.Default);
        return install;
    }

    /// <summary>
    /// Downloads a model into the app's Models folder. When it is the chosen one, the
    /// engine (re)starts on it, whether or not the settings window is still open.
    /// </summary>
    public ModelDownload DownloadModel(ModelLibrary.Model model)
    {
        var download = ModelDownload.Start(model, AppPaths.Models);
        _ = download.Finished.ContinueWith(task => _dispatcher.BeginInvoke(() =>
        {
            AppLogger.Info(task.Result is null ? $"Downloaded {model.Id}" : $"Download of {model.Id} ended: {task.Result}");
            if (task.Result is null && model.Id == _settings.ModelId) Send(new Event.Installed(IsSetUp));
        }), TaskScheduler.Default);
        return download;
    }

    /// <summary>Copies the latest transcript to the clipboard and says so in the capsule.</summary>
    public void ShowHistory()
    {
        // While dictating, the capsule shows the recording; the copy still happens silently.
        var busy = _session.IsBusy;
        var entries = HistoryStore.Recent(1);
        if (entries.Count == 0)
        {
            if (!busy) _overlay.Notify("No transcripts yet");
            return;
        }

        try
        {
            System.Windows.Clipboard.SetDataObject(entries[0].Text, true);
            if (!busy) _overlay.Notify("Copied last transcript");
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Another app holds the clipboard.
            if (!busy) _overlay.Notify("Clipboard busy, try again");
        }
    }

    /// <summary>Resizes and recolors the capsule; the settings preview uses it before saving.</summary>
    public void ApplyCapsule(CapsuleLook look) => _overlay.Apply(look);

    /// <summary>
    /// Shows the capsule on screen as a live sample for the Capsule settings page,
    /// unless it is showing a dictation; ApplyCapsule then changes it in place.
    /// </summary>
    public void StageCapsule() => Send(new Event.Sample(true));

    public void UnstageCapsule() => Send(new Event.Sample(false));

    /// <param name="restartEngine">Only when language or model changed: a reload takes seconds.</param>
    public void ApplySettings(Settings newSettings, bool restartEngine = true)
    {
        _overlay.Apply(CapsuleLook.From(newSettings));
        ShowMicButton(newSettings.ShowMicButton);
        _hook.HoldMode = newSettings.PressAndHold;
        _hook.Bind(HotkeyKind.Primary, newSettings.PrimaryHotkey);
        _hook.Bind(HotkeyKind.History, newSettings.HistoryHotkey);
        if (newSettings.AlternateHotkeyEnabled)
        {
            _hook.Bind(HotkeyKind.Alternate, newSettings.AlternateHotkey);
        }
        else
        {
            _hook.Bind(HotkeyKind.Alternate, null);
        }

        if (restartEngine) RestartEngine();
    }

    public void RestartEngine() => Send(new Event.LoadEngine(IsSetUp));

    private void Send(Event happened)
    {
        var before = _session.State;
        var effects = _session.Handle(happened);
        if (_session.State != before)
        {
            AppLogger.Info($"DictationState -> {_session.State}");
            StateChanged?.Invoke(this, _session.State);
        }

        foreach (var effect in effects)
        {
            switch (effect)
            {
                case Effect.LoadEngine: LoadEngine(); break;
                case Effect.ShowSetup: SetupNeeded?.Invoke(this, EventArgs.Empty); break;
                case Effect.Notice(var text): _overlay.Notify(text); break;
                case Effect.Record: Record(); break;
                case Effect.StartLive: StartLive(); break;
                case Effect.StopRecording(var status): StopRecording(status); break;
                case Effect.DropRecording:
                    StopLive();
                    _mic.Stop();
                    AppLogger.Info("Recording cancelled.");
                    break;
                case Effect.Hide: _overlay.SetState(OverlayState.Hidden, ""); break;
                case Effect.Transcribe: _ = TranscribeAsync(); break;
                case Effect.Deliver deliver: _ = DeliverAsync(deliver); break;
                case Effect.Failed(var error, var seconds):
                    DictationCompleted?.Invoke(this, new DictationResultArgs("", seconds, _engine.ModelId, error));
                    break;
                case Effect.StartSample: _overlay.BeginStage(); break;
                case Effect.StopSample: _overlay.EndStage(); break;
            }
        }
    }

    private void LoadEngine()
    {
        var generation = Interlocked.Increment(ref _engineGeneration);
        _ = Task.Run(async () =>
        {
            // A failed load keeps the previous engine and reports Error. The stub engine
            // is for self-tests only: its placeholder text must never be pasted.
            var newEngine = new WhisperSpeechEngine(_settings.Language, _settings.ModelId, null, _settings.SelectedLanguages);
            try
            {
                await newEngine.LoadAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                newEngine.Dispose();
                AppLogger.Error("Engine load failed", ex);
                if (generation == Volatile.Read(ref _engineGeneration)) _dispatcher.Invoke(() => Send(new Event.EngineFailed()));
                return;
            }

            ISpeechEngine? replaced = null;
            lock (_engineGate)
            {
                if (generation == _engineGeneration)
                {
                    replaced = _engine;
                    _engine = newEngine;
                }
            }

            if (replaced is null)
            {
                newEngine.Dispose(); // A newer load has taken over.
                return;
            }

            replaced.Dispose();
            _dispatcher.Invoke(() => Send(new Event.EngineLoaded()));
        });
    }

    private void Record()
    {
        _overlay.SetState(OverlayState.Recording, "Listening…");

        // Opening the microphone takes about half a second on some devices. Off the UI
        // thread, the capsule appears at once instead of after it, so a press never
        // looks ignored. A stop pressed meanwhile waits for it (DictationSession).
        var deviceId = _settings.MicrophoneId;
        _ = Task.Run(() => _mic.Start(deviceId)).ContinueWith(task => _dispatcher.BeginInvoke(() =>
        {
            if (task.Exception?.GetBaseException() is { } ex)
            {
                AppLogger.Error("Failed to start microphone capture", ex);
                Send(new Event.MicrophoneFailed(ex.Message));
                return;
            }

            AppLogger.Info($"Recording started on: {_mic.CurrentDeviceName ?? "Default"} (ID: {_mic.CurrentDeviceId})");
            Send(new Event.MicrophoneOpened());
        }), TaskScheduler.Default);
    }

    /// <summary>
    /// The live side of the recording: drafts of what's said so far go to the
    /// capsule, and a minute without speech finishes the dictation on its own.
    /// </summary>
    private void StartLive()
    {
        LiveSession live = null!;
        live = new LiveSession(_mic.Since, samples => _engine.TranscribeAsync(samples, _cts.Token, draft: true),
            drafting: _settings.CapsuleLiveText);
        live.DraftChanged += (settled, tail) => _dispatcher.BeginInvoke(() =>
        {
            if (_live == live) _overlay.ShowDraft(settled, tail);
        });
        live.SilenceLimitReached += () => _dispatcher.BeginInvoke(() =>
        {
            if (_live != live) return;
            AppLogger.Info("No speech for a minute: finishing the dictation.");
            Send(new Event.SilenceLimit());
        });
        _live = live;
        live.Start();
    }

    private void StopLive()
    {
        _live?.Stop();
        _live = null;
    }

    private void StopRecording(string status)
    {
        // The draft on screen stays while the whole recording is transcribed again.
        StopLive();
        _overlay.SetState(OverlayState.Transcribing, status);

        var samples = _mic.Stop();
        var duration = (double)samples.Length / MicrophoneCapture.TargetSampleRate;
        var maxAmp = samples.Length > 0 ? samples.Max(Math.Abs) : 0f;
        var rms = samples.Length > 0 ? Math.Sqrt(samples.Average(s => s * s)) : 0.0;
        AppLogger.Info($"Recording stopped: {samples.Length} samples ({duration:0.00}s), Peak: {maxAmp:F4}, RMS: {rms:F4}");
        if (samples.Length == 0) AppLogger.Warn("Microphone returned 0 samples. Audio buffer was empty.");

        _recorded = samples;
        Send(new Event.Recorded(duration));
    }

    /// <summary>Runs on the dispatcher; the awaits come back to it.</summary>
    private async Task TranscribeAsync()
    {
        var samples = _recorded;
        _recorded = Array.Empty<float>();
        try
        {
            var sw = Stopwatch.StartNew();
            var text = await _engine.TranscribeAsync(samples, _cts.Token);
            sw.Stop();
            // Logs never hold what was said: lengths and timings only.
            AppLogger.Info($"Speech engine ({_engine.ModelId}) transcribed in {sw.ElapsedMilliseconds}ms: {text.Length} characters");

            if (_settings.AiCleanupEnabled && !string.IsNullOrWhiteSpace(text))
            {
                var cleanSw = Stopwatch.StartNew();
                text = await Speech.AiCleanupService.CleanAsync(text, _settings, _cts.Token);
                cleanSw.Stop();
                AppLogger.Info($"AI Cleanup completed in {cleanSw.ElapsedMilliseconds}ms: {text.Length} characters");
            }

            if (string.IsNullOrWhiteSpace(text)) AppLogger.Warn($"No speech recognized from {samples.Length / (double)MicrophoneCapture.TargetSampleRate:0.00}s of audio.");
            Send(new Event.Transcribed(text));
        }
        catch (Exception ex)
        {
            AppLogger.Error("Dictation processing exception", ex);
            Send(new Event.TranscriptionFailed(ex.Message));
        }
    }

    private async Task DeliverAsync(Effect.Deliver deliver)
    {
        try
        {
            _overlay.SetState(OverlayState.Hidden, "");

            // Collect held hotkey modifiers so they can be released
            // before the paste chord to avoid ghost modifiers.
            var held = _hook.Suspended ? Array.Empty<int>() :
                new[] { 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5 }
                    .Where(IsKeyDown)
                    .ToArray();

            AppLogger.Info($"Injecting {deliver.Text.Length} characters into active application...");
            await TextInjector.PasteAsync(deliver.Text, deliver.PressEnter, held);

            HistoryStore.Append(new HistoryEntry(
                DateTimeOffset.UtcNow, deliver.Text, deliver.Seconds, _engine.ModelId));
            AppLogger.Info("History entry appended successfully.");

            LastTranscript = deliver.Text;
            DictationCompleted?.Invoke(this, new DictationResultArgs(deliver.Text, deliver.Seconds, _engine.ModelId, null));
            Send(new Event.Delivered());
        }
        catch (Exception ex)
        {
            AppLogger.Error("Dictation processing exception", ex);
            Send(new Event.TranscriptionFailed(ex.Message));
        }
    }

    private void OnHotkeyPressed(object? sender, HotkeyKind kind)
    {
        AppLogger.Info($"Hotkey pressed: {kind}");
        switch (kind)
        {
            case HotkeyKind.Primary:
                Toggle(_settings.PressEnterAfterPaste);
                break;
            case HotkeyKind.Alternate:
                Send(new Event.Finish(!_settings.PressEnterAfterPaste));
                break;
            case HotkeyKind.History:
                ShowHistory();
                break;
        }
    }

    private void OnHotkeyReleased(object? sender, HotkeyKind kind)
    {
        if (_settings.PressAndHold && kind == HotkeyKind.Primary && State == DictationState.Recording)
        {
            AppLogger.Info($"Hotkey released in HoldMode: finishing dictation.");
            Send(new Event.Finish(_settings.PressEnterAfterPaste));
        }
    }

    private void OnCancelled(object? sender, EventArgs e) => Send(new Event.Cancel());

    private void OnLevel(object? sender, double level)
    {
        LastAudioLevel = level;
        _dispatcher.BeginInvoke(() =>
        {
            _overlay.UpdateLevel(level);
            LevelChanged?.Invoke(this, level);
        });
    }

    private static bool IsKeyDown(int virtualKey)
    {
        return (Interop.NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        AppLogger.Info("Disposing DictationController.");
        _cts.Cancel();
        _hook.Pressed -= OnHotkeyPressed;
        _hook.Released -= OnHotkeyReleased;
        _hook.Cancelled -= OnCancelled;
        _mic.LevelChanged -= OnLevel;

        StopLive();
        _hook.Dispose();
        _mic.Dispose();
        _engine.Dispose();
        _overlay.Close();
        _micButton?.Close();
        _cts.Dispose();
    }
}
