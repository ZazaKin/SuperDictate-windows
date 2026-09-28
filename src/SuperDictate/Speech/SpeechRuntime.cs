using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SuperDictate.Storage;

namespace SuperDictate.Speech;

/// <summary>
/// The private Python that runs the speech worker. Developer machines may use a
/// venv; customers get it on first run: a CPython build from NuGet, then
/// faster-whisper and its libraries from PyPI at the versions pinned in
/// runtime-requirements.txt.
/// </summary>
public static class SpeechRuntime
{
    public const string PythonVersion = "3.13.15";

    /// <summary>SHA-512 of python.3.13.15.nupkg as NuGet's catalog publishes it; the download must match.</summary>
    public const string PythonPackageSha512 = "CtMWTkEpEkEtie6eip2Ckok0J4EqZ7nkPY72dmhx+qfxDcFYmeNpHBTgM2/XnaPTnqqEPqwePgVqkVGtM2usBA==";

    public static string Folder => FolderOverride ?? AppPaths.Runtime;

    /// <summary>Only tests set it, to install into a throwaway folder instead of the real runtime.</summary>
    internal static string? FolderOverride { get; set; }

    /// <summary>python.exe of a venv (developer machines) or of the NuGet layout the app installs.</summary>
    public static string? PythonPath =>
        new[] { Path.Combine(Folder, "Scripts", "python.exe"), Path.Combine(Folder, "python.exe") }.FirstOrDefault(File.Exists);

    private static string SitePackages => Path.Combine(Folder, "Lib", "site-packages");

    public static bool IsInstalled => PythonPath is not null && Directory.Exists(Path.Combine(SitePackages, "faster_whisper"));

    public static bool HasGpuPack => Directory.Exists(Path.Combine(SitePackages, "nvidia", "cudnn"));

    /// <summary>NVIDIA's driver installs nvcuda.dll; without it the GPU pack would be dead weight.</summary>
    public static bool NvidiaGpuPresent => File.Exists(Path.Combine(Environment.SystemDirectory, "nvcuda.dll"));

    /// <summary>The worker embedded in this build, written next to the runtime so each build runs its own copy.</summary>
    public static string WorkerScript() => WriteResource("worker.py", Path.Combine(Folder, "worker.py"));

    internal static string WriteResource(string name, string path)
    {
        var bytes = ReadResource(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
        {
            File.WriteAllBytes(path, bytes);
        }

        return path;
    }

    internal static byte[] ReadResource(string name)
    {
        using var stream = typeof(SpeechRuntime).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"This build is missing its {name} resource.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}

/// <summary>
/// Installs the speech runtime, and optionally the NVIDIA GPU pack. One at a time;
/// it lives outside the settings window, so closing the window doesn't stop it.
/// </summary>
public sealed class RuntimeInstall
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    private readonly CancellationTokenSource _cancel = new();
    private volatile string _status = "Starting…";
    private long _progressBits;

    private RuntimeInstall(bool includeGpu)
    {
        IncludesGpu = includeGpu;
        Current = this;
        Finished = Task.Run(RunAsync);
    }

    public static RuntimeInstall? Current { get; private set; }

    public bool IncludesGpu { get; }

    /// <summary>What is happening now, e.g. "Downloading ctranslate2 (19.4 MB)".</summary>
    public string Status => _status;

    /// <summary>0 to 1 across all steps.</summary>
    public double Progress => BitConverter.Int64BitsToDouble(Interlocked.Read(ref _progressBits));

    public bool Cancelled => _cancel.IsCancellationRequested;

    /// <summary>Null when the runtime is ready; otherwise what went wrong.</summary>
    public Task<string?> Finished { get; }

    public static RuntimeInstall Start(bool includeGpu)
    {
        if (Current is not null) throw new InvalidOperationException("The speech runtime is already being installed.");
        return new RuntimeInstall(includeGpu);
    }

    public void Cancel() => _cancel.Cancel();

    private void Report(double progress) => Interlocked.Exchange(ref _progressBits, BitConverter.DoubleToInt64Bits(Math.Clamp(progress, 0, 1)));

    private async Task<string?> RunAsync()
    {
        try
        {
            var gpu = IncludesGpu && !SpeechRuntime.HasGpuPack;
            var baseEnd = gpu ? 0.4 : 1.0;
            if (!SpeechRuntime.IsInstalled) await InstallBaseAsync(baseEnd).ConfigureAwait(false);
            if (gpu)
            {
                await PipInstallAsync(SpeechRuntime.PythonPath!, "runtime-requirements-gpu.txt", SpeechRuntime.IsInstalled ? baseEnd : 0, 1).ConfigureAwait(false);
            }

            _status = "Installed";
            Report(1);
            AppLogger.Info($"Speech runtime ready (GPU pack: {SpeechRuntime.HasGpuPack})");
            return null;
        }
        catch (OperationCanceledException)
        {
            return "Cancelled";
        }
        catch (Exception error)
        {
            AppLogger.Error("Speech runtime install failed", error);
            return error.Message;
        }
        finally
        {
            Current = null;
        }
    }

    /// <summary>Python first, into a staging folder that replaces the runtime only once it works.</summary>
    private async Task InstallBaseAsync(double end)
    {
        var token = _cancel.Token;
        var staging = SpeechRuntime.Folder + ".incoming";
        RemoveFolder(staging);
        Directory.CreateDirectory(staging);

        var version = SpeechRuntime.PythonVersion;
        var package = Path.Combine(staging, "python.nupkg");
        _status = $"Downloading Python {version}…";
        await DownloadAsync($"https://api.nuget.org/v3-flatcontainer/python/{version}/python.{version}.nupkg", package, 0, end * 0.08, token).ConfigureAwait(false);

        using (var stream = File.OpenRead(package))
        {
            var actual = Convert.ToBase64String(await System.Security.Cryptography.SHA512.HashDataAsync(stream, token).ConfigureAwait(false));
            if (actual != SpeechRuntime.PythonPackageSha512)
            {
                throw new InvalidOperationException("The Python download didn't match its published fingerprint, so nothing was installed. Try again later.");
            }
        }

        _status = "Unpacking Python…";
        using (var archive = ZipFile.OpenRead(package))
        {
            foreach (var entry in archive.Entries.Where(entry => entry.FullName.StartsWith("tools/", StringComparison.Ordinal) && entry.Name.Length > 0))
            {
                token.ThrowIfCancellationRequested();
                var target = Path.GetFullPath(Path.Combine(staging, entry.FullName["tools/".Length..]));
                if (!target.StartsWith(Path.GetFullPath(staging), StringComparison.OrdinalIgnoreCase)) continue; // Never write outside staging.
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: true);
            }
        }

        File.Delete(package);
        Report(end * 0.12);

        var python = Path.Combine(staging, "python.exe");
        await PipInstallAsync(python, "runtime-requirements.txt", end * 0.12, end * 0.97).ConfigureAwait(false);

        _status = "Checking the speech engine…";
        await RunPythonAsync(python, "-c \"import faster_whisper, ctranslate2\"", token).ConfigureAwait(false);

        // Swap in. A broken leftover is moved aside first; a link (developer setup) only loses the link.
        var previous = SpeechRuntime.Folder + ".previous";
        RemoveFolder(previous);
        if (Directory.Exists(SpeechRuntime.Folder)) Directory.Move(SpeechRuntime.Folder, previous);
        Directory.Move(staging, SpeechRuntime.Folder);
        RemoveFolder(previous);
        Report(end);
    }

    private async Task DownloadAsync(string url, string path, double from, double to, CancellationToken token)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? 0;
        await using var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        await using var target = File.Create(path);
        var buffer = new byte[81920];
        long done = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
            done += read;
            if (total > 0) Report(from + ((to - from) * done / total));
        }
    }

    /// <summary>pip with machine-readable progress, mapped onto the from..to part of the bar.</summary>
    private async Task PipInstallAsync(string python, string requirementsResource, double from, double to)
    {
        var requirements = SpeechRuntime.WriteResource(requirementsResource, Path.Combine(AppPaths.Temp, requirementsResource));
        // Requirement lines only: comments and the --hash continuation lines don't count.
        var count = File.ReadLines(requirements).Select(line => line.Trim()).Count(line => line.Length > 0 && line[0] is not ('#' or '-'));
        var progress = new PipProgress(count);
        _status = progress.Status;

        // No pip cache: it would keep a second copy of every wheel in the user profile.
        // --require-hashes: every wheel must match a sha256 pinned in the requirements file.
        await RunPythonAsync(python,
            $"-m pip install --disable-pip-version-check --no-warn-script-location --no-cache-dir --require-hashes --progress-bar raw -r \"{requirements}\"",
            _cancel.Token,
            line =>
            {
                if (!progress.Feed(line)) return;
                _status = progress.Status;
                Report(from + ((to - from) * progress.Share));
            }).ConfigureAwait(false);
        Report(to);
    }

    /// <summary>Runs Python hidden, feeding each output line to <paramref name="onLine"/>; throws with pip's last error line.</summary>
    private static async Task RunPythonAsync(string python, string arguments, CancellationToken token, Action<string?>? onLine = null)
    {
        var startInfo = new ProcessStartInfo(python, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
        startInfo.Environment["PYTHONNOUSERSITE"] = "1";
        startInfo.Environment["PIP_NO_INPUT"] = "1";
        // pip unpacks into the temp folder: keep that in the install folder, off the system drive.
        startInfo.Environment["TMP"] = startInfo.Environment["TEMP"] = AppPaths.Temp;

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {python}.");
        var lastError = "";
        process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;
            onLine?.Invoke(e.Data);
            if (!e.Data.StartsWith("Progress ", StringComparison.Ordinal)) lastError = e.Data.Trim();
        };
        process.BeginErrorReadLine();

        using var stop = token.Register(() =>
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        });

        while (await process.StandardOutput.ReadLineAsync(CancellationToken.None).ConfigureAwait(false) is { } line)
        {
            onLine?.Invoke(line);
        }

        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrEmpty(lastError) ? $"Python exited with code {process.ExitCode}." : lastError);
        }
    }

    /// <summary>Deletes a folder; for a link, only the link, never what it points to.</summary>
    private static void RemoveFolder(string path)
    {
        var folder = new DirectoryInfo(path);
        if (!folder.Exists) return;
        if (folder.LinkTarget is not null) folder.Delete();
        else folder.Delete(recursive: true);
    }
}

/// <summary>
/// Turns pip's output into a share of the pip step (0 to 0.9 until pip exits) and
/// a status line. pip first resolves every package ("Collecting"), then downloads
/// the wheels one by one, so downloads carry most of the bar.
/// </summary>
internal sealed class PipProgress
{
    private static readonly Regex ProgressLine = new(@"^Progress (\d+) of (\d+)", RegexOptions.Compiled);
    private static readonly Regex WheelLine = new(@"^\s*(?:Downloading|Using cached) (\S+?)-\d\S*\.whl \(([^)]+)\)", RegexOptions.Compiled);

    private readonly object _gate = new();
    private readonly int _count;
    private int _collected;
    private int _wheels;
    private double _file;

    public PipProgress(int count) => _count = Math.Max(1, count);

    public string Status { get; private set; } = "Checking packages…";

    public double Share { get; private set; }

    /// <summary>Returns true when the line changed the status or the share.</summary>
    public bool Feed(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;
        lock (_gate)
        {
            if (ProgressLine.Match(line) is { Success: true } progress)
            {
                var total = double.Parse(progress.Groups[2].Value);
                _file = total > 0 ? double.Parse(progress.Groups[1].Value) / total : 0;
            }
            else if (WheelLine.Match(line) is { Success: true } wheel)
            {
                _wheels++;
                _file = 0;
                Status = $"Downloading {wheel.Groups[1].Value} ({wheel.Groups[2].Value})";
            }
            else if (line.StartsWith("Collecting ", StringComparison.Ordinal))
            {
                _collected++;
            }
            else if (line.StartsWith("Installing collected packages", StringComparison.Ordinal))
            {
                Status = "Installing…";
                Share = 0.9;
                return true;
            }
            else
            {
                return false;
            }

            var checking = 0.1 * Math.Min(1, _collected / (double)_count);
            var downloading = _wheels == 0 ? 0 : 0.8 * Math.Min(1, (_wheels - 1 + _file) / _count);
            Share = Math.Max(Share, Math.Min(0.9, checking + downloading));
            return true;
        }
    }
}
