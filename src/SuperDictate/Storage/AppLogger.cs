using System;
using System.Collections.Generic;
using System.IO;

namespace SuperDictate.Storage;

/// <summary>
/// Lightweight, reliable local logger for dictation diagnostics and debugging.
/// Writes to Logs\dictation.log in the install folder.
/// </summary>
public static class AppLogger
{
    private static readonly object Gate = new();
    // A property: the installer switches the install folder after this class is first used.
    private static string LogFilePath => Path.Combine(AppPaths.Logs, "dictation.log");

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex != null ? $"{message} | {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}" : message);

    private static void Write(string level, string message)
    {
        try
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
            lock (Gate)
            {
                // Simple log rotation if over 5 MB
                if (File.Exists(LogFilePath) && new FileInfo(LogFilePath).Length > 5 * 1024 * 1024)
                {
                    var backup = Path.Combine(AppPaths.Logs, "dictation.old.log");
                    try
                    {
                        File.Move(LogFilePath, backup, overwrite: true);
                    }
                    catch
                    {
                        // Ignore rotation errors
                    }
                }

                File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never crash the host application
        }
    }

    public static IReadOnlyList<string> GetRecent(int lines = 50)
    {
        try
        {
            lock (Gate)
            {
                if (!File.Exists(LogFilePath)) return Array.Empty<string>();
                var all = File.ReadAllLines(LogFilePath);
                var take = Math.Min(lines, all.Length);
                var result = new List<string>(take);
                for (var i = all.Length - take; i < all.Length; i++)
                {
                    result.Add(all[i]);
                }
                return result;
            }
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
