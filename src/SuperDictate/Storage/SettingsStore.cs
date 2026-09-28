using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;

namespace SuperDictate.Storage;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    private static readonly object Gate = new();

    public static Settings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var json = File.ReadAllText(AppPaths.SettingsFile);
                var loaded = JsonSerializer.Deserialize<Settings>(json, Options);
                if (loaded is not null)
                {
                    MigrateLegacyAiKey(json, loaded);
                    return loaded;
                }
            }
        }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
        {
            // A damaged settings file must never stop dictation from starting.
        }

        return new Settings();
    }

    /// <summary>
    /// Earlier builds wrote the AI cleanup key into settings.json. Move it to
    /// Credential Manager, then rewrite the file without it. The file is only
    /// rewritten after the credential is stored, so a failure never loses the key.
    /// </summary>
    private static void MigrateLegacyAiKey(string json, Settings loaded)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("AiApiKey", out var legacy))
            {
                return;
            }

            if (legacy.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(legacy.GetString()))
            {
                CredentialStore.Write(CredentialStore.AiCleanupTarget, legacy.GetString()!.Trim());
                AppLogger.Info("Moved the AI cleanup key from settings.json to Credential Manager.");
            }

            Save(loaded);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Win32Exception)
        {
            // Settings still load; the migration retries on the next start.
            AppLogger.Error("AI cleanup key migration failed", error);
        }
    }

    /// <summary>Writes through a temporary file so a crash cannot truncate settings.</summary>
    public static void Save(Settings settings)
    {
        lock (Gate)
        {
            var temporary = AppPaths.SettingsFile + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Options));

            if (File.Exists(AppPaths.SettingsFile))
            {
                File.Replace(temporary, AppPaths.SettingsFile, null);
            }
            else
            {
                File.Move(temporary, AppPaths.SettingsFile);
            }
        }
    }
}
