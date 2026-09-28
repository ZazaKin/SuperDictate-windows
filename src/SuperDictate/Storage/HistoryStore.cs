using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SuperDictate.Storage;

public sealed record HistoryEntry(DateTimeOffset At, string Text, double Seconds, string Model);

/// <summary>
/// Append-only JSON Lines history. Append-only keeps an interrupted write from
/// destroying earlier transcripts, matching the macOS crash-recovery behaviour.
/// </summary>
public static class HistoryStore
{
    private static readonly object Gate = new();

    public static void Append(HistoryEntry entry)
    {
        lock (Gate)
        {
            using var stream = new FileStream(
                AppPaths.HistoryFile, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.WriteLine(JsonSerializer.Serialize(entry));
        }
    }

    public static IReadOnlyList<HistoryEntry> Recent(int limit)
    {
        lock (Gate)
        {
            if (!File.Exists(AppPaths.HistoryFile))
            {
                return Array.Empty<HistoryEntry>();
            }

            var entries = new List<HistoryEntry>();
            foreach (var line in File.ReadLines(AppPaths.HistoryFile))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var entry = JsonSerializer.Deserialize<HistoryEntry>(line);
                    if (entry is not null)
                    {
                        entries.Add(entry);
                    }
                }
                catch (JsonException)
                {
                    // Skip a partially written trailing line.
                }
            }

            return entries.TakeLast(limit).Reverse().ToList();
        }
    }
}
