using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SuperDictate.Interop;

namespace SuperDictate.Input;

/// <summary>
/// Pastes the transcript into the focused field. Windows has no accessibility
/// permission to request, but it also has no supported way to insert text into
/// an arbitrary control, so this uses the clipboard and restores it afterwards.
/// </summary>
public static class TextInjector
{
    private static readonly int[] RightSideModifiers = { 0xA1, 0xA3, 0xA5 };

    public static async Task PasteAsync(string text, bool pressEnter, IEnumerable<int> heldModifiers)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        // A modifier the user is still holding would turn Ctrl+V into
        // Ctrl+Alt+V or Ctrl+Shift+V in the target application.
        var modifiers = heldModifiers.ToList();
        ReleaseKeys(modifiers);

        // If Alt was in the held keys or active, send a neutral key up to prevent
        // Windows from putting the target window into menu bar activation mode
        if (modifiers.Any(k => k is 0x12 or 0xA4 or 0xA5))
        {
            SendKey(NativeMethods.VK_CONTROL, down: true);
            SendKey(NativeMethods.VK_CONTROL, down: false);
        }

        var previous = ReadClipboard();
        if (!WriteClipboard(text))
        {
            return;
        }

        // Give the target application a moment to observe the new clipboard contents.
        await Task.Delay(60).ConfigureAwait(true);
        SendChord(NativeMethods.VK_CONTROL, NativeMethods.VK_V);

        if (pressEnter)
        {
            await Task.Delay(60).ConfigureAwait(true);
            SendKey(NativeMethods.VK_RETURN, down: true);
            SendKey(NativeMethods.VK_RETURN, down: false);
        }

        // Give the target application plenty of time (800ms) to read the clipboard
        // buffer before restoring previous clipboard content.
        await Task.Delay(800).ConfigureAwait(true);
        if (previous != null)
        {
            RestoreClipboard(previous);
        }
    }

    private static void ReleaseKeys(IEnumerable<int> keys)
    {
        foreach (var key in keys.Distinct())
        {
            SendKey(key, down: false);
        }
    }

    private static void SendChord(int modifier, int key)
    {
        SendKey(modifier, down: true);
        SendKey(key, down: true);
        SendKey(key, down: false);
        SendKey(modifier, down: false);
    }

    private static void SendKey(int virtualKey, bool down)
    {
        var flags = down ? 0u : NativeMethods.KEYEVENTF_KEYUP;
        if (RightSideModifiers.Contains(virtualKey))
        {
            flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
        }

        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = (ushort)virtualKey,
                    dwFlags = flags,
                    dwExtraInfo = NativeMethods.SyntheticMarker,
                },
            },
        };

        NativeMethods.SendInput(1, new[] { input }, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static string? ReadClipboard()
    {
        return Retry(() => System.Windows.Clipboard.ContainsText()
            ? System.Windows.Clipboard.GetText()
            : null);
    }

    private static bool WriteClipboard(string text)
    {
        var written = false;
        Retry<object?>(() =>
        {
            System.Windows.Clipboard.SetDataObject(text, true);
            written = true;
            return null;
        });

        return written;
    }

    private static void RestoreClipboard(string? previous)
    {
        Retry<object?>(() =>
        {
            if (previous is null)
            {
                System.Windows.Clipboard.Clear();
            }
            else
            {
                System.Windows.Clipboard.SetDataObject(previous, true);
            }

            return null;
        });
    }

    /// <summary>The clipboard is a shared resource and is often briefly locked.</summary>
    private static T? Retry<T>(Func<T?> action)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                return action();
            }
            catch (Exception error) when (error is System.Runtime.InteropServices.COMException or System.Runtime.InteropServices.ExternalException)
            {
                Thread.Sleep(20);
            }
        }

        return default;
    }
}
