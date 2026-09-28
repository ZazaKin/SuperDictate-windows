using System;
using System.Collections.Generic;
using System.Linq;
using SuperDictate.Interop;

namespace SuperDictate.Input;

public enum HotkeyKind
{
    Primary,
    Alternate,
    History,
}

/// <summary>
/// Global WH_KEYBOARD_LL hook. Must be installed on a thread that pumps
/// messages, which is why it is created on the WPF dispatcher thread.
///
/// Tap semantics for modifier-only chords match macOS: the chord fires on
/// release, and only when no other key was pressed while it was held, so
/// holding right Alt to type a character never starts dictation.
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _callback;
    private readonly HashSet<int> _pressed = new();
    private readonly Dictionary<HotkeyKind, Hotkey> _hotkeys = new();
    private readonly HashSet<HotkeyKind> _armed = new();

    private IntPtr _hook;
    private HotkeyKind? _held;
    private bool _suspended;

    public KeyboardHook()
    {
        // Keep the delegate rooted: the GC must not collect it while installed.
        _callback = OnKey;
    }

    /// <summary>Fires when a chord completes (hold mode) or is tapped.</summary>
    public event EventHandler<HotkeyKind>? Pressed;

    /// <summary>Fires when a held chord is released. Only used in hold mode.</summary>
    public event EventHandler<HotkeyKind>? Released;

    /// <summary>Hold to dictate. When false, chords use tap-on-release.</summary>
    public bool HoldMode { get; set; }

    /// <summary>
    /// Suspends dispatch while the user is recording a new chord in settings,
    /// so captured keys configure a hotkey instead of starting dictation.
    /// </summary>
    public bool Suspended
    {
        get => _suspended;
        set
        {
            _suspended = value;
            _armed.Clear();
            _held = null;
        }
    }

    public void Install()
    {
        if (_hook != IntPtr.Zero)
        {
            return;
        }

        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL, _callback, NativeMethods.GetModuleHandle(null), 0);

        if (_hook == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to install the global keyboard hook.");
        }
    }

    public void Bind(HotkeyKind kind, string? spec)
    {
        if (spec is not null && Hotkey.TryParse(spec, out var hotkey) && hotkey is not null)
        {
            _hotkeys[kind] = hotkey;
        }
        else
        {
            _hotkeys.Remove(kind);
        }
    }

    private IntPtr OnKey(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code != NativeMethods.HC_ACTION)
        {
            return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
        }

        var data = System.Runtime.InteropServices.Marshal
            .PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

        // Our own paste must never look like user input.
        if (data.dwExtraInfo == NativeMethods.SyntheticMarker)
        {
            return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
        }

        var message = (int)wParam;
        var virtualKey = (int)data.vkCode;

        if (message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN)
        {
            HandleDown(virtualKey);
        }
        else if (message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP)
        {
            HandleUp(virtualKey);
        }

        // Never swallow the key. Right Alt still has to reach the focused app.
        return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
    }

    private void HandleDown(int virtualKey)
    {
        var repeat = !_pressed.Add(virtualKey);
        if (repeat || _suspended)
        {
            return;
        }

        foreach (var (kind, hotkey) in _hotkeys)
        {
            if (!hotkey.MatchesExactly(_pressed))
            {
                // Anything else pressed during the chord cancels the tap.
                _armed.Remove(kind);
                continue;
            }

            if (hotkey.IsModifierOnly && !HoldMode)
            {
                _armed.Add(kind);
                continue;
            }

            _held = kind;
            Pressed?.Invoke(this, kind);
        }
    }

    private void HandleUp(int virtualKey)
    {
        if (_suspended)
        {
            _pressed.Remove(virtualKey);
            return;
        }

        var tapped = _armed
            .Where(kind => _hotkeys.TryGetValue(kind, out var hotkey) && hotkey.AllKeys.Contains(virtualKey))
            .ToList();

        foreach (var kind in tapped)
        {
            _armed.Remove(kind);
            Pressed?.Invoke(this, kind);
        }

        if (_held is { } heldKind
            && _hotkeys.TryGetValue(heldKind, out var heldHotkey)
            && heldHotkey.AllKeys.Contains(virtualKey))
        {
            _held = null;
            Released?.Invoke(this, heldKind);
        }

        _pressed.Remove(virtualKey);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
