using System;
using System.Collections.Generic;
using System.Linq;

namespace SuperDictate.Input;

/// <summary>
/// A side-aware chord. Left and right modifiers are distinct, exactly as on
/// macOS: a chord bound to right Alt never fires from left Alt. A chord may be
/// modifier-only ("RightAlt"), which is why the app uses a low-level hook
/// instead of RegisterHotKey.
/// </summary>
public sealed class Hotkey
{
    private static readonly Dictionary<string, int> Names = BuildNames();

    private Hotkey(string spec, IReadOnlyCollection<int> modifiers, int? key)
    {
        Spec = spec;
        Modifiers = new HashSet<int>(modifiers);
        Key = key;
    }

    public string Spec { get; }

    public HashSet<int> Modifiers { get; }

    public int? Key { get; }

    public bool IsModifierOnly => Key is null;

    /// <summary>All virtual keys the chord occupies, used for release tracking.</summary>
    public IEnumerable<int> AllKeys => Key is null ? Modifiers : Modifiers.Append(Key.Value);

    public static bool TryParse(string? spec, out Hotkey? hotkey)
    {
        hotkey = null;
        if (string.IsNullOrWhiteSpace(spec))
        {
            return false;
        }

        var modifiers = new List<int>();
        int? key = null;

        foreach (var part in spec.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Names.TryGetValue(part, out var virtualKey))
            {
                return false;
            }

            if (IsModifier(virtualKey))
            {
                modifiers.Add(virtualKey);
            }
            else if (key is null)
            {
                key = virtualKey;
            }
            else
            {
                // Only one non-modifier key per chord.
                return false;
            }
        }

        if (modifiers.Count == 0 && key is null)
        {
            return false;
        }

        hotkey = new Hotkey(spec, modifiers, key);
        return true;
    }

    public static bool IsModifier(int virtualKey) => virtualKey is >= 0xA0 and <= 0xA5 or 0x5B or 0x5C;

    /// <summary>True when the pressed set is exactly this chord and nothing else.</summary>
    public bool MatchesExactly(IReadOnlyCollection<int> pressed)
    {
        var expected = Key is null ? Modifiers.Count : Modifiers.Count + 1;
        return pressed.Count == expected && AllKeys.All(pressed.Contains);
    }

    private static Dictionary<string, int> BuildNames()
    {
        var names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["LeftShift"] = 0xA0,
            ["RightShift"] = 0xA1,
            ["LeftCtrl"] = 0xA2,
            ["RightCtrl"] = 0xA3,
            ["LeftAlt"] = 0xA4,
            ["RightAlt"] = 0xA5,
            ["LeftWin"] = 0x5B,
            ["RightWin"] = 0x5C,
            ["Space"] = 0x20,
            ["Enter"] = 0x0D,
            ["Tab"] = 0x09,
            ["Escape"] = 0x1B,
            ["CapsLock"] = 0x14,
            ["Insert"] = 0x2D,
            ["Delete"] = 0x2E,
        };

        for (var index = 0; index < 24; index++)
        {
            names["F" + (index + 1)] = 0x70 + index;
        }

        for (var letter = 'A'; letter <= 'Z'; letter++)
        {
            names[letter.ToString()] = letter;
        }

        for (var digit = '0'; digit <= '9'; digit++)
        {
            names["D" + digit] = digit;
        }

        return names;
    }
}
