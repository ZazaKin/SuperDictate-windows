using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SuperDictate.Storage;

/// <summary>
/// Everything SuperDictate keeps lives in one folder, the one chosen in the
/// installer (by default on the roomiest drive that isn't the system drive):
///
///   SuperDictate.exe, Uninstall SuperDictate.lnk
///                the program; an update replaces the exe and nothing else
///   Data\        settings.json, history.jsonl
///   Models\      speech models
///   Runtime\     the speech engine's private Python
///   Logs\, Recordings\, Temp\
///
/// Only what Windows keeps per user stays outside: the Apps entry, Start menu
/// and Startup shortcuts, and the API key in Credential Manager.
/// </summary>
public static class AppPaths
{
    /// <summary>Where Windows' Apps list keeps the install entry, including the chosen folder.</summary>
    public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\com.local.superdictate";

    /// <summary>Top-level folders of the layout; an install folder may already hold them.</summary>
    public static readonly IReadOnlyList<string> LayoutFolders = new[] { "Data", "Models", "Runtime", "Logs", "Recordings", "Temp" };

    private static string? _root;

    /// <summary>
    /// The install folder: recorded as InstallLocation in the Apps entry, or,
    /// before anything is installed, the first suggestion.
    /// </summary>
    public static string Root => _root ??= RecordedRoot() ?? Suggestions().First();

    /// <summary>The installer switches to the folder it just installed into.</summary>
    public static void UseRoot(string root) => _root = root;

    /// <summary>True once an install recorded its folder, whatever layout that install had.</summary>
    public static bool HasRecordedRoot => RecordedRoot() is not null;

    /// <summary>Candidate install folders, best first: other local drives by free space, then the user profile.</summary>
    public static IEnumerable<string> Suggestions()
    {
        var system = Path.GetPathRoot(Environment.SystemDirectory);
        IEnumerable<DriveInfo> drives;
        try
        {
            drives = DriveInfo.GetDrives()
                .Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady
                                && !string.Equals(drive.Name, system, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(drive => drive.AvailableFreeSpace)
                .ToList();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            drives = Array.Empty<DriveInfo>();
        }

        foreach (var drive in drives) yield return Path.Combine(drive.Name, "SuperDictate");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "SuperDictate");
    }

    /// <summary>Speech models: always Models in the install folder.</summary>
    public static string Models => EnsureDirectory(Path.Combine(Root, "Models"));

    public static string Runtime => Path.Combine(Root, "Runtime");

    public static string Logs => EnsureDirectory(Path.Combine(Root, "Logs"));

    public static string Recordings => EnsureDirectory(Path.Combine(Root, "Recordings"));

    /// <summary>Downloads in progress and scratch files, so nothing lands on the system drive's temp folder.</summary>
    public static string Temp => EnsureDirectory(Path.Combine(Root, "Temp"));

    private static string? _dataRoot;

    /// <summary>
    /// Folder holding settings and history. Only the self-test redirects it,
    /// to a temporary folder, so it can never overwrite the user's real data.
    /// </summary>
    internal static string DataRoot
    {
        get => _dataRoot ?? EnsureDirectory(Path.Combine(Root, "Data"));
        set => _dataRoot = value;
    }

    public static string SettingsFile => Path.Combine(DataRoot, "settings.json");

    public static string HistoryFile => Path.Combine(DataRoot, "history.jsonl");

    /// <summary>Where versions before the single-folder layout kept their data.</summary>
    public static string LegacyData => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SuperDictate");

    public static string DefaultExportFolder =>
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    private static string? RecordedRoot()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(UninstallKey);
            return key?.GetValue("InstallLocation") is string chosen && Path.IsPathFullyQualified(chosen) ? chosen : null;
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// The path with directory links (junctions, symlinks) followed at every level,
    /// so the settings window shows where files really are.
    /// </summary>
    public static string RealPath(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var real = Path.GetPathRoot(full)!;
            foreach (var part in full[real.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                real = Path.Combine(real, part);
                if (new DirectoryInfo(real).LinkTarget is not null)
                {
                    real = Directory.ResolveLinkTarget(real, returnFinalTarget: true)?.FullName ?? real;
                }
            }

            return real;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return path;
        }
    }

    public static bool SamePath(string a, string b) => string.Equals(
        RealPath(a).TrimEnd(Path.DirectorySeparatorChar),
        RealPath(b).TrimEnd(Path.DirectorySeparatorChar),
        StringComparison.OrdinalIgnoreCase);

    /// <summary>True when both paths are on the same volume, so a folder moves by renaming.</summary>
    public static bool SameVolume(string a, string b) => string.Equals(
        Path.GetPathRoot(RealPath(a)), Path.GetPathRoot(RealPath(b)), StringComparison.OrdinalIgnoreCase);

    private static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
