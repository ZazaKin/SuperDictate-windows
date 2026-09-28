using System;
using System.IO;
using System.Linq;
using SuperDictate.Speech;

namespace SuperDictate.Storage;

/// <summary>
/// Versions before the single-folder layout kept their data in
/// %LOCALAPPDATA%\SuperDictate, and could keep models in any folder. On start,
/// settings and history are copied into Data, and the speech runtime and models
/// move into the install folder when that is a rename on the same drive. A copy
/// across drives would take minutes, so those stay where they are and the
/// Speech model page offers them for download again.
/// </summary>
internal static class LegacyData
{
    /// <summary>Runs before settings load: copies them over the first time.</summary>
    public static void Adopt()
    {
        var legacy = AppPaths.LegacyData;
        if (!Directory.Exists(legacy) || File.Exists(AppPaths.SettingsFile) || AppPaths.SamePath(legacy, AppPaths.Root)) return;

        try
        {
            foreach (var name in new[] { "settings.json", "history.jsonl" })
            {
                var source = Path.Combine(legacy, name);
                if (File.Exists(source)) File.Copy(source, Path.Combine(AppPaths.DataRoot, name), overwrite: false);
            }

            var runtime = Path.Combine(legacy, "runtime");
            if (Directory.Exists(runtime) && !Directory.Exists(AppPaths.Runtime) && AppPaths.SameVolume(runtime, AppPaths.Root))
            {
                Directory.Move(AppPaths.RealPath(runtime), AppPaths.Runtime);
            }

            GatherModels(Path.Combine(legacy, "Models"));
            AppLogger.Info($"Took over data from {legacy} into {AppPaths.Root}");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Nothing is lost: whatever didn't move is still in the old folder.
            AppLogger.Error($"Couldn't take over everything from {legacy}", error);
        }
    }

    /// <summary>Runs after settings load: models kept in another folder move into Models.</summary>
    /// <returns>True when settings changed and need saving.</returns>
    public static bool GatherModels(Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ModelsFolder)) return false;
        try
        {
            GatherModels(settings.ModelsFolder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            AppLogger.Error($"Couldn't move the models from {settings.ModelsFolder}", error);
        }

        settings.ModelsFolder = null;
        return true;
    }

    private static void GatherModels(string folder)
    {
        if (!Directory.Exists(folder) || AppPaths.SamePath(folder, AppPaths.Models)) return;
        if (!AppPaths.SameVolume(folder, AppPaths.Root))
        {
            AppLogger.Warn($"Models in {folder} are on another drive and stay there; download them again in Settings > Speech model.");
            return;
        }

        // Only SuperDictate's own folders: a chosen folder may hold anything else.
        var real = AppPaths.RealPath(folder);
        foreach (var name in ModelLibrary.All.Select(model => model.Id).Append(".huggingface"))
        {
            var source = Path.Combine(real, name);
            var target = Path.Combine(AppPaths.Models, name);
            if (Directory.Exists(source) && !Directory.Exists(target)) Directory.Move(source, target);
        }
    }
}
