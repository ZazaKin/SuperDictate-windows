using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SuperDictate.Speech;

/// <summary>The speech models SuperDictate offers, and whether a folder holds them.</summary>
public static class ModelLibrary
{
    /// <param name="Repo">Hugging Face repository with the CTranslate2 build faster-whisper loads.</param>
    /// <param name="Revision">The repository commit to download, so a changed upstream can't swap the model.</param>
    /// <param name="ApproxBytes">Shown before downloading; the download itself reports the exact size.</param>
    public sealed record Model(string Id, string Name, string Repo, string Revision, long ApproxBytes);

    public static readonly IReadOnlyList<Model> All = new[]
    {
        new Model("whisper-large-v3-turbo", "Large v3 Turbo (recommended)", "mobiuslabsgmbh/faster-whisper-large-v3-turbo", "0a363e9161cbc7ed1431c9597a8ceaf0c4f78fcf", 1_622_000_000),
        new Model("whisper-small", "Small (balanced)", "Systran/faster-whisper-small", "536b0662742c02347bc0e980a01041f333bce120", 486_000_000),
        new Model("whisper-base", "Base (fastest)", "Systran/faster-whisper-base", "ebe41f70d5b6dfa9166e2c581c45c9c0cfc57b66", 148_000_000),
        new Model("whisper-tiny", "Tiny (least memory)", "Systran/faster-whisper-tiny", "d90ca5fe260221311c53c58e660288d3deb8d356", 78_000_000),
    };

    /// <summary>The model with this id, or the recommended one for an unknown id.</summary>
    public static Model Find(string? id) => All.FirstOrDefault(model => model.Id == id) ?? All[0];

    /// <summary>A model is usable once its weights are in place; downloads write model.bin last.</summary>
    public static bool IsPresent(string folder, string id) => File.Exists(Path.Combine(folder, id, "model.bin"));

    public static long SizeOnDisk(string folder, string id) => FolderSize(Path.Combine(folder, id));

    public static string FormatSize(long bytes) => bytes >= 1L << 30
        ? $"{bytes / (double)(1L << 30):0.0} GB"
        : $"{bytes / (double)(1L << 20):0} MB";

    internal static long FolderSize(string path)
    {
        try
        {
            return new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file =>
            {
                try
                {
                    // Directory listings keep the size from before the writer opened the file;
                    // Refresh reads the live size of a file that is still downloading.
                    file.Refresh();
                    return file.Length;
                }
                catch (FileNotFoundException)
                {
                    return 0L; // Renamed from .incomplete mid-scan.
                }
            });
        }
        catch (Exception error) when (error is DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}

/// <summary>
/// One model download at a time, run by the speech worker's Python. It lives
/// outside the settings window, so closing the window doesn't lose it.
/// </summary>
public sealed class ModelDownload
{
    private readonly Process _process;
    private volatile bool _cancelled;
    private long _totalBytes;

    private ModelDownload(ModelLibrary.Model model, string folder, Process process)
    {
        Model = model;
        Folder = folder;
        _process = process;

        // Set before watching starts: a download that fails at once still clears it afterwards.
        Current = this;
        Finished = Task.Run(WatchAsync);
    }

    /// <summary>The running download, if any.</summary>
    public static ModelDownload? Current { get; private set; }

    public ModelLibrary.Model Model { get; }

    public string Folder { get; }

    /// <summary>Exact size from Hugging Face; 0 until the worker has asked.</summary>
    public long TotalBytes => Interlocked.Read(ref _totalBytes);

    /// <summary>Includes partial files, so it moves while a file is still arriving.</summary>
    public long DownloadedBytes => ModelLibrary.FolderSize(Path.Combine(Folder, Model.Id));

    public bool Cancelled => _cancelled;

    /// <summary>Null when the model is in place; otherwise what went wrong.</summary>
    public Task<string?> Finished { get; }

    public static ModelDownload Start(ModelLibrary.Model model, string folder)
    {
        if (Current is { } running)
        {
            throw new InvalidOperationException($"{running.Model.Name} is still downloading.");
        }

        var target = Path.Combine(folder, model.Id);
        Directory.CreateDirectory(target);

        var startInfo = new ProcessStartInfo
        {
            FileName = WhisperSpeechEngine.ResolvePythonPath(),
            Arguments = $"\"{WhisperSpeechEngine.ResolveWorkerScriptPath()}\" --download {model.Repo} --revision {model.Revision} --output \"{target}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
        };
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
        startInfo.Environment["HF_HUB_DISABLE_PROGRESS_BARS"] = "1";
        startInfo.Environment["HF_HUB_DISABLE_TELEMETRY"] = "1";
        // Keep Hugging Face's own cache next to the models instead of in the user profile.
        // Plain HTTP instead of Xet: it grows a partial file inside the target folder, which
        // is what the progress bar measures; Xet only shows the file once it is complete.
        startInfo.Environment["HF_HOME"] = Path.Combine(folder, ".huggingface");
        startInfo.Environment["HF_HUB_DISABLE_XET"] = "1";
        startInfo.Environment["TMP"] = startInfo.Environment["TEMP"] = Storage.AppPaths.Temp;

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The download could not start.");
        return new ModelDownload(model, folder, process);
    }

    public void Cancel()
    {
        _cancelled = true;
        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already finished.
        }
    }

    private async Task<string?> WatchAsync()
    {
        // Drained so a chatty library can never fill the pipe and stall the download.
        var lastError = "";
        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data)) lastError = e.Data.Trim();
        };
        _process.BeginErrorReadLine();

        string? reported = null;
        try
        {
            while (await _process.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                try
                {
                    using var message = JsonDocument.Parse(line);
                    if (message.RootElement.TryGetProperty("total", out var total)) Interlocked.Exchange(ref _totalBytes, total.GetInt64());
                    if (message.RootElement.TryGetProperty("error", out var error)) reported = error.GetString();
                }
                catch (JsonException)
                {
                    // Not one of ours.
                }
            }

            await _process.WaitForExitAsync().ConfigureAwait(false);
            if (_cancelled) return "Cancelled";
            if (ModelLibrary.IsPresent(Folder, Model.Id)) return null;

            var detail = reported ?? lastError;
            return string.IsNullOrWhiteSpace(detail) ? $"The download stopped (exit code {_process.ExitCode})." : Shorten(detail);
        }
        finally
        {
            _process.Dispose();
            Current = null;
        }
    }

    private static string Shorten(string text) => text.Length <= 200 ? text : text[..197] + "…";
}

