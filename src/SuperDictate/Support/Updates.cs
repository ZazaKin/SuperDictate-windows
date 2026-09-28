using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SuperDictate.Setup;
using SuperDictate.Storage;

namespace SuperDictate.Support;

/// <param name="Url">The new SuperDictate-Setup exe on GitHub Releases.</param>
/// <param name="Sha256">Hex SHA-256 of that exe; the download must match it.</param>
public sealed record UpdateInfo(string Version, string Url, string Sha256, string? Notes);

/// <summary>
/// Checks for a newer version only when the user asks, then downloads its setup
/// exe, verifies it against the published SHA-256 and starts it. The setup
/// replaces this copy the usual way: stop, swap, restart.
/// </summary>
public static class Updates
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(15) };

    /// <summary>The newer version, or null when this one is current.</summary>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken token)
    {
        var json = await Http.GetStringAsync(Project.UpdateManifest, token).ConfigureAwait(false);
        var update = JsonSerializer.Deserialize<UpdateInfo>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("The update information is empty.");
        return IsNewer(update.Version, Installer.Version) ? update : null;
    }

    internal static bool IsNewer(string candidate, string current) =>
        System.Version.TryParse(candidate, out var next) && System.Version.TryParse(current, out var now) && next > now;

    /// <summary>Downloads, verifies and launches the setup. Throws when the file doesn't match.</summary>
    public static async Task DownloadAndRunAsync(UpdateInfo update, IProgress<double> progress, CancellationToken token)
    {
        if (!Uri.TryCreate(update.Url, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidDataException("The update address isn't a secure download link.");
        }

        var path = Path.Combine(AppPaths.Temp, $"SuperDictate-Setup-{update.Version}.exe");
        using (var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
        {
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
                if (total > 0) progress.Report(done / (double)total);
            }
        }

        string actual;
        await using (var file = File.OpenRead(path))
        {
            actual = Convert.ToHexString(await SHA256.HashDataAsync(file, token).ConfigureAwait(false));
        }

        if (!actual.Equals(update.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(path);
            throw new InvalidDataException("The downloaded update didn't match its published fingerprint and was deleted.");
        }

        AppLogger.Info($"Starting the setup for version {update.Version}");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
