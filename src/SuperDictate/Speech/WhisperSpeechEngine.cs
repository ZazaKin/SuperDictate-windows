using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SuperDictate.Storage;

namespace SuperDictate.Speech;

/// <summary>
/// Speech engine backed by faster-whisper. Runs a persistent local worker process,
/// eliminating model reloading overhead between dictations.
/// Supports multilingual transcription including English and Russian.
/// </summary>
public sealed class WhisperSpeechEngine : ISpeechEngine
{
    private readonly string _preferredLanguage;
    private readonly string? _preferredModel;
    private readonly string _device;
    private readonly System.Collections.Generic.IReadOnlyList<string> _selectedLanguages;
    private string? _deviceUsed;
    private string? _modelLoaded;
    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _isLoaded;
    private bool _disposed;

    public WhisperSpeechEngine(
        string? preferredLanguage = null,
        string? preferredModel = null,
        string? device = null,
        System.Collections.Generic.IReadOnlyList<string>? selectedLanguages = null)
    {
        _preferredLanguage = preferredLanguage ?? "auto";
        _preferredModel = preferredModel;
        _device = device ?? "auto";
        _selectedLanguages = selectedLanguages ?? new[] { "en", "ru", "de", "pl" };
    }

    public string ModelId => _modelLoaded ?? _preferredModel ?? "whisper";

    public string? DeviceUsed => _deviceUsed;

    /// <summary>Never throws: a disposed or crashed worker simply isn't loaded.</summary>
    public bool IsLoaded
    {
        get
        {
            if (_disposed || !_isLoaded || _process is not { } process) return false;
            try
            {
                return !process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return false; // The Process object was already released.
            }
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (IsLoaded) return;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsLoaded) return;

            var pythonExe = ResolvePythonPath();
            var workerScript = ResolveWorkerScriptPath();
            var modelPath = ResolveModelPath(_preferredModel);
            _modelLoaded = Path.GetFileName(modelPath);

            if (!File.Exists(pythonExe))
            {
                throw new FileNotFoundException($"Python runtime not found at {pythonExe}");
            }

            if (!File.Exists(workerScript))
            {
                throw new FileNotFoundException($"Worker script not found at {workerScript}");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{workerScript}\" --model \"{modelPath}\" --device {_device} --compute-type auto",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
            };
            startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            startInfo.EnvironmentVariables["PYTHONUTF8"] = "1";

            var process = new Process { StartInfo = startInfo };
            process.Start();

            _process = process;
            _stdin = new StreamWriter(process.StandardInput.BaseStream, new System.Text.UTF8Encoding(false))
            {
                AutoFlush = true,
            };
            _stdout = process.StandardOutput;

            // Wait for the {"ready": true} handshake with timeout
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var readyLine = await _stdout.ReadLineAsync(linkedCts.Token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(readyLine))
            {
                var stderr = await process.StandardError.ReadToEndAsync(CancellationToken.None).ConfigureAwait(false);
                throw new InvalidOperationException($"Speech worker failed to start. Error: {stderr}");
            }

            using var doc = JsonDocument.Parse(readyLine);
            if (doc.RootElement.TryGetProperty("ready", out var readyProp) && readyProp.GetBoolean())
            {
                _isLoaded = true;
                _deviceUsed = doc.RootElement.TryGetProperty("device", out var dProp) ? dProp.GetString() : "unknown";
            }
            else
            {
                var error = doc.RootElement.TryGetProperty("error", out var errProp) ? errProp.GetString() : readyLine;
                throw new InvalidOperationException($"Speech worker reported initialization error: {error}");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string> TranscribeAsync(float[] samples, CancellationToken cancellationToken, bool draft = false)
    {
        if (samples.Length == 0) return string.Empty;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // No reload from here: LoadAsync takes this same lock, so it would wait forever.
            // A crashed worker fails the call, and the next dictation restarts the engine.
            if (!IsLoaded || _process is null || _stdin is null || _stdout is null)
            {
                throw new InvalidOperationException("Speech worker process is not running.");
            }

            // Write raw 16kHz float32 audio to a temporary file
            var tempAudioPath = Path.Combine(AppPaths.Recordings, $"{Guid.NewGuid():N}.f32");
            try
            {
                var byteCount = samples.Length * sizeof(float);
                var byteBuffer = new byte[byteCount];
                Buffer.BlockCopy(samples, 0, byteBuffer, 0, byteCount);
                await File.WriteAllBytesAsync(tempAudioPath, byteBuffer, cancellationToken).ConfigureAwait(false);

                // Send request JSON
                var request = new
                {
                    path = tempAudioPath,
                    language = _preferredLanguage == "auto" ? null : _preferredLanguage,
                    selected_languages = _selectedLanguages,
                    draft,
                };
                var requestJson = JsonSerializer.Serialize(request);

                await _stdin.WriteLineAsync(requestJson.AsMemory(), cancellationToken).ConfigureAwait(false);

                // Read response JSON
                var responseLine = await _stdout.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(responseLine))
                {
                    return string.Empty;
                }

                using var doc = JsonDocument.Parse(responseLine);
                if (doc.RootElement.TryGetProperty("error", out var errProp) && !string.IsNullOrWhiteSpace(errProp.GetString()))
                {
                    var errMsg = errProp.GetString();
                    AppLogger.Error($"Speech worker reported error: {errMsg}");
                    throw new InvalidOperationException($"Transcription error: {errMsg}");
                }

                if (doc.RootElement.TryGetProperty("text", out var textProp))
                {
                    return textProp.GetString()?.Trim() ?? string.Empty;
                }

                return string.Empty;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempAudioPath))
                    {
                        File.Delete(tempAudioPath);
                    }
                }
                catch
                {
                    // Non-fatal if temp file cleanup is delayed
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>The app's own Python; a system Python would lack faster-whisper or have other versions.</summary>
    internal static string ResolvePythonPath() => SpeechRuntime.IsInstalled
        ? SpeechRuntime.PythonPath!
        : throw new FileNotFoundException("The speech runtime isn't installed. Install it in Settings > Speech model.");

    internal static string ResolveWorkerScriptPath() => SpeechRuntime.WorkerScript();

    private static string ResolveModelPath(string? requestedModel = null)
    {
        if (!string.IsNullOrEmpty(requestedModel))
        {
            var requestedDir = Path.Combine(AppPaths.Models, requestedModel);
            if (Directory.Exists(requestedDir) && File.Exists(Path.Combine(requestedDir, "model.bin")))
            {
                return requestedDir;
            }
        }

        // Priority 1: Large-v3-Turbo (State of the art)
        var turboDir = Path.Combine(AppPaths.Models, "whisper-large-v3-turbo");
        if (Directory.Exists(turboDir) && File.Exists(Path.Combine(turboDir, "model.bin")))
        {
            return turboDir;
        }

        // Priority 2: Small (High accuracy)
        var smallDir = Path.Combine(AppPaths.Models, "whisper-small");
        if (Directory.Exists(smallDir) && File.Exists(Path.Combine(smallDir, "model.bin")))
        {
            return smallDir;
        }

        // Check for base model (good accuracy, fast)
        var baseDir = Path.Combine(AppPaths.Models, "whisper-base");
        if (Directory.Exists(baseDir) && File.Exists(Path.Combine(baseDir, "model.bin")))
        {
            return baseDir;
        }

        // Check for tiny model
        var tinyDir = Path.Combine(AppPaths.Models, "whisper-tiny");
        if (Directory.Exists(tinyDir) && File.Exists(Path.Combine(tinyDir, "model.bin")))
        {
            return tinyDir;
        }

        // Never fall back to a model name: faster-whisper would quietly download it
        // into the user profile. Downloads happen only from Settings, where the user asks.
        throw new FileNotFoundException(
            $"No speech model in {AppPaths.RealPath(AppPaths.Models)}. Download one in Settings > Speech model.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _isLoaded = false;

        try
        {
            _stdin?.Dispose();
            _stdout?.Dispose();
            if (_process is { HasExited: false })
            {
                _process.Kill();
                // Until it has exited the worker still holds the model files open (a model move needs them).
                _process.WaitForExit(3000);
                _process.Dispose();
            }
        }
        catch
        {
            // Suppress cleanup errors on dispose
        }
        finally
        {
            _lock.Dispose();
        }
    }
}
