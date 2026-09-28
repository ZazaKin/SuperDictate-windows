using System;
using System.Collections.Generic;
using SuperDictate.Speech;

namespace SuperDictate.Support;

/// <summary>
/// Every address the app points to outside itself: the GitHub repository
/// (updates, help, bug reports) and the donation options on the Support page.
/// They come from release-settings.ini in the project root, embedded at build
/// time; build-release.ps1 checks that file before a release.
/// </summary>
public static class Project
{
    public enum DonationKind
    {
        /// <summary>A web page, opened in the browser.</summary>
        Link,

        /// <summary>A wallet address, copied to the clipboard.</summary>
        Address,
    }

    /// <param name="Note">Shown under the name, e.g. which network a wallet is on.</param>
    public sealed record Donation(string Name, DonationKind Kind, string Value, string? Note = null);

    private static readonly (string Repository, IReadOnlyList<Donation> Donations) Settings =
        Parse(System.Text.Encoding.UTF8.GetString(SpeechRuntime.ReadResource("release-settings.ini")));

    public static string Repository => Settings.Repository;

    /// <summary>update.json on the main branch; build-release.ps1 writes it next to the exe.</summary>
    public static string UpdateManifest =>
        Repository.Replace("https://github.com/", "https://raw.githubusercontent.com/") + "/main/update.json";

    public static string UserGuide => Repository + "/blob/main/docs/user-guide.md";

    public static string ReportProblem => Repository + "/issues/new";

    /// <summary>Shown on the Support page in file order; entries left empty are skipped.</summary>
    public static IReadOnlyList<Donation> Donations => Settings.Donations;

    internal static (string Repository, IReadOnlyList<Donation> Donations) Parse(string ini)
    {
        var repository = "";
        var donations = new List<Donation>();
        var section = "";
        foreach (var raw in ini.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == ';') continue;
            if (line[0] == '[')
            {
                section = line.Trim('[', ']').Trim().ToLowerInvariant();
                continue;
            }

            // Name | note = value: the answer always goes last.
            var equals = line.IndexOf('=');
            if (equals < 1) continue;
            var head = line[..equals];
            var bar = head.IndexOf('|');
            var key = (bar < 0 ? head : head[..bar]).Trim();
            var note = bar < 0 ? null : head[(bar + 1)..].Trim();
            var value = line[(equals + 1)..].Trim();

            if (section == "release" && key.Equals("repository", StringComparison.OrdinalIgnoreCase)) repository = value.TrimEnd('/');
            else if (section == "donations" && value.Length > 0)
            {
                var kind = value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? DonationKind.Link : DonationKind.Address;
                donations.Add(new Donation(key, kind, value, string.IsNullOrEmpty(note) ? null : note));
            }
        }

        return (repository, donations);
    }
}
