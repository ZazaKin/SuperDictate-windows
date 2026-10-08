using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SuperDictate.Input;
using SuperDictate.Storage;

namespace SuperDictate.Ui;

/// <summary>A reason a save can't happen: the page it's on, what to say, and the setting to point at.</summary>
public sealed record SettingsProblem(string Page, string Message, string Setting);

/// <summary>
/// The settings window's pending changes. The pages read and write <see cref="Pending"/>,
/// a copy of the settings; <see cref="Validate"/> holds the rules a save must pass, and
/// <see cref="Commit"/> writes back only what was changed here, so a change made from the
/// tray or the microphone button while the window is open stays. No WPF, so the
/// self-test checks the rules directly.
/// </summary>
public sealed class SettingsDraft
{
    /// <summary>The page of every setting the window edits. Settings not listed aren't edited there.</summary>
    private static readonly Dictionary<string, string> PageOf = new()
    {
        [nameof(Settings.PrimaryHotkey)] = "dictation",
        [nameof(Settings.AlternateHotkey)] = "dictation",
        [nameof(Settings.PressAndHold)] = "dictation",
        [nameof(Settings.PressEnterAfterPaste)] = "dictation",
        [nameof(Settings.MicrophoneId)] = "dictation",
        [nameof(Settings.SelectedLanguages)] = "languages",
        [nameof(Settings.Language)] = "languages",
        [nameof(Settings.ModelId)] = "models",
        [nameof(Settings.AiCleanupEnabled)] = "ai_cleanup",
        [nameof(Settings.AiCleanupMode)] = "ai_cleanup",
        [nameof(Settings.AiRemoveFillers)] = "ai_cleanup",
        [nameof(Settings.AiFormatPunctuation)] = "ai_cleanup",
        [nameof(Settings.AiRemoveDuplicates)] = "ai_cleanup",
        [nameof(Settings.CustomFillerWords)] = "ai_cleanup",
        [nameof(Settings.LocalLlmEndpoint)] = "ai_cleanup",
        [nameof(Settings.LocalLlmModel)] = "ai_cleanup",
        [nameof(Settings.AiBaseUrl)] = "ai_cleanup",
        [nameof(Settings.AiModel)] = "ai_cleanup",
        [nameof(Settings.CapsuleSkin)] = "capsule",
        [nameof(Settings.CapsuleAccent)] = "capsule",
        [nameof(Settings.CapsuleScale)] = "capsule",
        [nameof(Settings.CapsuleOpacity)] = "capsule",
        [nameof(Settings.CapsuleMeter)] = "capsule",
        [nameof(Settings.CapsuleMaxWidth)] = "capsule",
        [nameof(Settings.CapsuleLiveGlass)] = "capsule",
        [nameof(Settings.CapsuleLiveText)] = "capsule",
        [nameof(Settings.CapsuleTimer)] = "capsule",
        [nameof(Settings.CapsuleHorizontal)] = "capsule",
        [nameof(Settings.CapsuleX)] = "capsule",
        [nameof(Settings.CapsuleVertical)] = "capsule",
        [nameof(Settings.CapsuleY)] = "capsule",
        [nameof(Settings.CapsuleScreen)] = "capsule",
        [nameof(Settings.ShowMicButton)] = "settings",
    };

    /// <summary>Reloading the engine takes seconds, so only a change to what it recognizes restarts it.</summary>
    private static readonly string[] Recognition =
        { nameof(Settings.ModelId), nameof(Settings.Language), nameof(Settings.SelectedLanguages) };

    private static readonly PropertyInfo[] Properties =
        typeof(Settings).GetProperties().Where(property => property.CanRead && property.CanWrite).ToArray();

    private readonly Settings _saved;
    private Settings _start;
    private string _savedCloudKey;

    /// <param name="saved">The settings in use, shared with the rest of the app; only Commit writes them.</param>
    /// <param name="cloudKey">The AI cleanup key, which lives in Credential Manager rather than the settings.</param>
    public SettingsDraft(Settings saved, string cloudKey)
    {
        _saved = saved;
        _savedCloudKey = cloudKey;
        _start = Copy(saved);
        Pending = Copy(saved);
        CloudKey = cloudKey;
    }

    /// <summary>The settings being edited. Replaced by a fresh copy after Commit and Discard.</summary>
    public Settings Pending { get; private set; }

    public string CloudKey { get; set; }

    public bool CloudKeyChanged => CloudKey != _savedCloudKey;

    public bool HasChanges => Changed().Any() || CloudKeyChanged;

    public bool HasChangesOn(string page) =>
        Changed().Any(name => PageOf[name] == page) || (page == "ai_cleanup" && CloudKeyChanged);

    /// <summary>The first reason the pending settings can't be saved, or null.</summary>
    public SettingsProblem? Validate()
    {
        var pending = Pending;
        if ((pending.SelectedLanguages?.Count ?? 0) == 0)
        {
            return new("languages", "Select at least one language to recognize.", nameof(Settings.SelectedLanguages));
        }

        // An unparseable or clashing chord would silently unbind dictation, so it never saves.
        if (HotkeyProblem(pending.PrimaryHotkey, out var primary) is { } primaryProblem)
        {
            return new("dictation", primaryProblem, nameof(Settings.PrimaryHotkey));
        }

        if (HotkeyProblem(pending.AlternateHotkey, out var alternate) is { } alternateProblem)
        {
            return new("dictation", alternateProblem, nameof(Settings.AlternateHotkey));
        }

        if (SameChord(primary!, alternate!))
        {
            return new("dictation", "The dictation and alternate hotkeys are the same. Choose a different alternate hotkey.",
                nameof(Settings.AlternateHotkey));
        }

        if (Hotkey.TryParse(pending.HistoryHotkey, out var history))
        {
            foreach (var (chord, setting, name) in new[]
                     {
                         (primary!, nameof(Settings.PrimaryHotkey), "dictation"),
                         (alternate!, nameof(Settings.AlternateHotkey), "alternate"),
                     })
            {
                if (SameChord(chord, history!))
                {
                    return new("dictation", $"{pending.HistoryHotkey} already opens history. Choose a different {name} hotkey.", setting);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Writes what was changed here into the settings in use, and starts over from them.
    /// Call after Validate, and after storing a changed <see cref="CloudKey"/>.
    /// </summary>
    /// <returns>Whether the engine must restart.</returns>
    public bool Commit()
    {
        var changed = Changed().ToList();
        foreach (var name in changed)
        {
            var property = typeof(Settings).GetProperty(name)!;
            property.SetValue(_saved, Clone(property.GetValue(Pending)));
        }

        _savedCloudKey = CloudKey;
        Discard();
        return changed.Intersect(Recognition).Any();
    }

    /// <summary>Back to the settings in use, including any change made meanwhile outside the window.</summary>
    public void Discard()
    {
        _start = Copy(_saved);
        Pending = Copy(_saved);
        CloudKey = _savedCloudKey;
    }

    private IEnumerable<string> Changed() =>
        PageOf.Keys.Where(name =>
        {
            var property = typeof(Settings).GetProperty(name)!;
            return !Same(property.GetValue(Pending), property.GetValue(_start));
        });

    /// <summary>Lists compare as sets; numbers from sliders compare within rounding.</summary>
    private static bool Same(object? a, object? b) => (a, b) switch
    {
        (IEnumerable<string> x, IEnumerable<string> y) => x.OrderBy(item => item).SequenceEqual(y.OrderBy(item => item)),
        (double x, double y) => Math.Abs(x - y) < 1e-9,
        _ => Equals(a, b),
    };

    private static Settings Copy(Settings from)
    {
        var copy = new Settings();
        foreach (var property in Properties) property.SetValue(copy, Clone(property.GetValue(from)));
        return copy;
    }

    private static object? Clone(object? value) => value is List<string> list ? new List<string>(list) : value;

    /// <summary>Null when the text is a chord the keyboard hook can bind; otherwise what to tell the user.</summary>
    private static string? HotkeyProblem(string? text, out Hotkey? hotkey)
    {
        hotkey = null;
        var spec = (text ?? "").Trim();
        if (spec.Length == 0) return "Enter a hotkey, like RightAlt.";
        return Hotkey.TryParse(spec, out hotkey)
            ? null
            : $"SuperDictate doesn't recognize “{spec}” as a hotkey. Use key names joined by +, like RightAlt or RightCtrl+Space.";
    }

    private static bool SameChord(Hotkey a, Hotkey b) => a.Key == b.Key && a.Modifiers.SetEquals(b.Modifiers);
}
