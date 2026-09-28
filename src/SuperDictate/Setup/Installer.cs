using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Win32;
using SuperDictate.Speech;
using SuperDictate.Storage;

namespace SuperDictate.Setup;

/// <summary>
/// Per-user install and uninstall into the folder the customer chose.
/// SuperDictate.exe sits at the top of it, next to an "Uninstall SuperDictate"
/// shortcut and the Data, Models and Runtime folders, which an update never
/// touches. No administrator rights.
/// </summary>
public static class Installer
{
    /// <summary>Signalled to ask a running SuperDictate to quit cleanly.</summary>
    public const string QuitEventName = @"Local\com.local.superdictate.quit";

    private const string UninstallKey = AppPaths.UninstallKey;

    /// <summary>In the install folder, next to the exe; runs the uninstaller.</summary>
    public const string UninstallShortcutName = "Uninstall SuperDictate.lnk";

    /// <summary>
    /// The program's own files at the top of the install folder: what an install
    /// replaces and an uninstall removes (worker.py and the .pdb are from older builds).
    /// </summary>
    private static readonly string[] ProgramFiles =
        { "SuperDictate.exe", UninstallShortcutName, "SuperDictate.exe.new", "SuperDictate.exe.old", "SuperDictate.pdb", "worker.py" };

    /// <summary>Where one earlier layout kept the exe; an install moves it out.</summary>
    private const string OldProgramFolder = "App";

    /// <summary>The developer script's sign-in task from before the Startup shortcut.</summary>
    private const string OldLogonTask = "com.local.superdictate.agent";

    public static string InstalledExe => Path.Combine(AppPaths.Root, "SuperDictate.exe");

    public static string Version =>
        typeof(Installer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    /// <summary>True for the copy in the install folder; any other copy offers to install itself.</summary>
    public static bool IsInstalledCopy =>
        Environment.ProcessPath is { } running && AppPaths.SamePath(running, InstalledExe);

    /// <summary>The version already installed, or null.</summary>
    public static string? InstalledVersion =>
        File.Exists(InstalledExe) ? FileVersionInfo.GetVersionInfo(InstalledExe).ProductVersion?.Split('+')[0] : null;

    /// <summary>The folder of an earlier install, or else the first suggested folder that can take one.</summary>
    public static string SuggestedRoot() => AppPaths.HasRecordedRoot
        ? AppPaths.Root
        : AppPaths.Suggestions().FirstOrDefault(root => CheckTarget(root) is null) ?? AppPaths.Suggestions().Last();

    private static string StartMenuShortcut => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), "SuperDictate.lnk");

    private static string StartupShortcut => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "SuperDictate.lnk");

    /// <summary>
    /// Why a folder can't take the install, or null when it can. It must be
    /// empty or hold only SuperDictate's own folders, from an earlier install.
    /// </summary>
    public static string? CheckTarget(string root)
    {
        if (!Path.IsPathFullyQualified(root)) return "Choose a full folder path, such as D:\\SuperDictate.";
        var full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        if (Path.GetPathRoot(full)!.TrimEnd(Path.DirectorySeparatorChar) == full) return "Choose a folder, not the whole drive.";

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (full.StartsWith(windows, StringComparison.OrdinalIgnoreCase)) return "Choose a folder outside Windows.";

        var folder = new DirectoryInfo(full);
        if (folder.Exists)
        {
            if (folder.LinkTarget is not null) return "Choose a regular folder, not a link.";
            var ours = AppPaths.LayoutFolders.Concat(ProgramFiles).Append(OldProgramFolder);
            var foreign = folder.EnumerateFileSystemInfos().FirstOrDefault(entry => !ours.Contains(entry.Name, StringComparer.OrdinalIgnoreCase));
            if (foreign is not null) return $"This folder already contains other files ({foreign.Name}). Choose an empty folder.";
        }

        // Probe where the install will write: the folder itself, or the nearest folder that exists above it.
        var probeIn = full;
        while (!Directory.Exists(probeIn) && Path.GetDirectoryName(probeIn) is { } up) probeIn = up;
        try
        {
            var probe = Path.Combine(probeIn, $".superdictate-write-test-{Environment.ProcessId}");
            Directory.CreateDirectory(probe);
            Directory.Delete(probe);
        }
        catch (Exception error) when (error is UnauthorizedAccessException or IOException)
        {
            return "You can't write there without administrator rights. Choose a folder on another drive or in your user folder.";
        }

        return null;
    }

    /// <summary>Installs this exe into <paramref name="root"/> for the current user and returns the installed exe.</summary>
    public static string Install(string root, bool startAtSignIn, Action<string> report)
    {
        if (CheckTarget(root) is { } problem) throw new InvalidOperationException(problem);
        var exe = Path.Combine(root, "SuperDictate.exe");
        var previousRoot = AppPaths.HasRecordedRoot ? AppPaths.Root : null;

        report("Closing SuperDictate if it is running…");
        StopRunningCopies();

        report("Copying SuperDictate…");
        Directory.CreateDirectory(root);
        StageAndReplace(Environment.ProcessPath!, exe);

        report("Adding it to the Start menu…");
        CreateShortcut(StartMenuShortcut, exe, "--show", "Speech to text for any app");
        if (startAtSignIn) CreateShortcut(StartupShortcut, exe, "", "Starts SuperDictate when you sign in");
        else File.Delete(StartupShortcut);
        CreateShortcut(Path.Combine(root, UninstallShortcutName), exe, "--uninstall", "Removes SuperDictate from this PC");

        // InstallLocation is also how the app, updates and the developer script find this folder.
        report("Registering the uninstaller…");
        using (var key = Registry.CurrentUser.CreateSubKey(UninstallKey))
        {
            key.SetValue("DisplayName", "SuperDictate");
            key.SetValue("DisplayVersion", Version);
            key.SetValue("Publisher", "SuperDictate");
            key.SetValue("DisplayIcon", exe);
            key.SetValue("InstallLocation", root);
            key.SetValue("UninstallString", $"\"{exe}\" --uninstall");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            key.SetValue("EstimatedSize", (int)(new FileInfo(exe).Length / 1024), RegistryValueKind.DWord);
        }

        AppPaths.UseRoot(root);
        RemoveOldInstalls(previousRoot, root);
        AppLogger.Info($"Installed SuperDictate {Version} to {root}");
        return exe;
    }

    /// <summary>
    /// Copies the new exe beside the installed one as SuperDictate.exe.new, then
    /// swaps it in with one rename, so a failure never leaves a half-written
    /// program. The old exe survives as .old until the swap has worked.
    /// </summary>
    internal static void StageAndReplace(string sourceExe, string exe)
    {
        var incoming = exe + ".new";
        var previous = exe + ".old";
        File.Copy(sourceExe, incoming, overwrite: true);
        if (File.Exists(exe)) File.Replace(incoming, exe, previous, ignoreMetadataErrors: true);
        else File.Move(incoming, exe);

        try { File.Delete(previous); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { /* Still in use; the next install removes it. */ }
    }

    /// <summary>
    /// Program files left by an earlier install (another folder, the App
    /// subfolder of an earlier layout, the developer script's old default)
    /// would start at sign-in beside the new one. Only program files go; data stays.
    /// </summary>
    private static void RemoveOldInstalls(string? previousRoot, string root)
    {
        var oldDefault = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "SuperDictate");
        var folders = new[]
        {
            Path.Combine(root, OldProgramFolder),
            previousRoot is null ? null : Path.Combine(previousRoot, OldProgramFolder),
            previousRoot is not null && !AppPaths.SamePath(previousRoot, root) ? previousRoot : null,
            AppPaths.SamePath(oldDefault, root) ? null : oldDefault,
        };
        foreach (var folder in folders)
        {
            if (folder is null || !Directory.Exists(folder)) continue;
            try
            {
                foreach (var name in ProgramFiles) File.Delete(Path.Combine(folder, name));
                if (!Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                AppLogger.Error($"Couldn't remove the old program in {folder}", error);
            }
        }

        try
        {
            using var tasks = Process.Start(new ProcessStartInfo("schtasks.exe", $"/Delete /TN {OldLogonTask} /F")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            tasks?.WaitForExit(5000);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // No Task Scheduler: then there is no old task either.
        }
    }

    /// <param name="removeData">Also delete settings, history, the speech runtime, downloaded models and the saved API key.</param>
    public static void Uninstall(bool removeData, Action<string> report)
    {
        var root = AppPaths.Root;

        report("Closing SuperDictate…");
        StopRunningCopies();

        report("Removing shortcuts and the Apps entry…");
        File.Delete(StartMenuShortcut);
        File.Delete(StartupShortcut);
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false);

        if (removeData)
        {
            report("Deleting your settings, history and models…");
            try { CredentialStore.Delete(CredentialStore.AiCleanupTarget); }
            catch (System.ComponentModel.Win32Exception) { /* Nothing stored. */ }
            foreach (var name in AppPaths.LayoutFolders) RemoveFolder(Path.Combine(root, name));
        }

        // The uninstaller is the installed exe itself, and Windows won't delete a running exe:
        // a hidden PowerShell waits until this process has exited (the user may keep the
        // "removed" window open for a while), then deletes the program files, and the
        // install folder too when nothing else is left in it.
        report("Removing the program…");
        Process.Start(new ProcessStartInfo("powershell.exe",
            $"-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand {Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(RemoveProgramScript(Environment.ProcessId, root)))}")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.SystemDirectory,
        });
        AppLogger.Info($"Uninstalled SuperDictate (data removed: {removeData})");
    }

    /// <summary>Waits for process <paramref name="pid"/> to exit, then removes the program files from <paramref name="root"/>.</summary>
    internal static string RemoveProgramScript(int pid, string root)
    {
        static string Quote(string path) => "'" + path.Replace("'", "''") + "'";
        var files = string.Join(", ", ProgramFiles.Select(name => Quote(Path.Combine(root, name))));
        return $"Wait-Process -Id {pid} -ErrorAction SilentlyContinue; " +
               $"Remove-Item -LiteralPath {files} -Force -ErrorAction SilentlyContinue; " +
               $"if (-not (Get-ChildItem -LiteralPath {Quote(root)} -Force -ErrorAction SilentlyContinue)) {{ Remove-Item -LiteralPath {Quote(root)} -ErrorAction SilentlyContinue }}";
    }

    /// <summary>Asks running copies to quit, then ends any that don't within a few seconds.</summary>
    private static void StopRunningCopies()
    {
        var others = Process.GetProcessesByName("SuperDictate").Where(process => process.Id != Environment.ProcessId).ToList();
        if (others.Count == 0) return;

        if (EventWaitHandle.TryOpenExisting(QuitEventName, out var quit))
        {
            using (quit) quit.Set();
        }

        foreach (var process in others)
        {
            using (process)
            {
                if (process.WaitForExit(5000)) continue;
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { /* Exited meanwhile. */ }
                process.WaitForExit(3000);
            }
        }
    }

    private static void CreateShortcut(string path, string target, string arguments, string description)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows Script Host is unavailable.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(path);
        shortcut.TargetPath = target;
        shortcut.Arguments = arguments;
        shortcut.WorkingDirectory = Path.GetDirectoryName(target);
        shortcut.Description = description;
        shortcut.IconLocation = target + ",0";
        shortcut.Save();
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
