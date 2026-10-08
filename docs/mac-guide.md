# SuperDictate for Mac: install and user guide

**English** · [Русский](mac-guide.ru.md)

SuperDictate turns your speech into text in any app on your Mac. Press
**Right ⌘**, speak, press it again, and your words are typed where the cursor
is. Speech is recognized on the Mac itself, on the Apple Neural Engine; your
voice is never sent anywhere.

- [Requirements](#requirements)
- [Install](#install)
- [First start: permissions and the speech model](#first-start-permissions-and-the-speech-model)
- [Dictating](#dictating)
- [The menu bar panel](#the-menu-bar-panel)
- [Settings](#settings)
- [Updating](#updating)
- [Privacy and where things are stored](#privacy-and-where-things-are-stored)
- [Uninstalling](#uninstalling)
- [Troubleshooting](#troubleshooting)

## Requirements

- A Mac with Apple Silicon: M1 or newer. Intel Macs aren't supported.
- macOS 14 Sonoma or newer. On macOS 26 the capsule can be real Liquid Glass.
- About 1 GB of free space, for the speech model.
- The internet once, to download the model (about 460 MB). After that,
  dictation works offline.

## Install

1. Open the [latest release](https://github.com/ZazaKin/SuperDictate-windows/releases/latest)
   and download `SuperDictate-macOS-<version>.zip`. If you like, compare its
   SHA-256 with `SHA256SUMS.txt` on the same page:
   `shasum -a 256 ~/Downloads/SuperDictate-macOS-*.zip` in Terminal.
2. Open the zip in **Downloads**. It unpacks to **SuperDictate**.
3. Drag **SuperDictate** into **Applications**. Run it only from there.
4. Open **SuperDictate** from Applications. Because the app isn't notarized by
   Apple yet, macOS stops it the first time with a message that it can't check
   the app for malicious software. Click **Done** (or **OK**), then:
   1. Open **System Settings › Privacy & Security**.
   2. Scroll down to **Security**. Next to "SuperDictate was blocked to protect
      your Mac", click **Open Anyway**.
   3. Enter your password, then click **Open Anyway** once more.

   On macOS 14 you can instead right-click SuperDictate in Applications, choose
   **Open**, then **Open** again. You only do this once.

> **"SuperDictate is damaged and can't be opened"?** That message appears when
> macOS keeps the download's quarantine mark on an app it can't verify. Move the
> app into Applications first, then run this once in Terminal and open it again:
>
> ```bash
> xattr -dr com.apple.quarantine /Applications/SuperDictate.app
> ```

SuperDictate is a menu bar app: it has no Dock icon. Look for the waveform in
the menu bar at the top of the screen.

## First start: permissions and the speech model

<p align="center"><img src="images/mac/welcome.png" width="540" alt="The setup window with three permissions and the speech model"></p>

The setup window lists what SuperDictate needs. Each row ticks itself off as
soon as it's done.

| Permission | Why | Where to allow it |
|---|---|---|
| **Microphone** | To hear you while you dictate | Click **Allow…**, then **Allow** in the macOS prompt |
| **Accessibility** | To type your words where the cursor is | **System Settings › Privacy & Security › Accessibility** |
| **Input Monitoring** | To notice the dictation key in any app | **System Settings › Privacy & Security › Input Monitoring** |

For Accessibility and Input Monitoring, **Allow…** opens the right page of
System Settings. Turn on the switch next to **SuperDictate**. If it isn't in the
list, click **+**, choose **SuperDictate** in Applications, and turn it on. macOS
may ask you to quit and reopen the app; do so from the menu bar panel
(**Quit SuperDictate**), then open it again from Applications.

Then click **Download** next to **Speech Model**. The model (about 460 MB)
downloads once from Hugging Face and is prepared for the Neural Engine, which
takes a minute or two the first time. When every row has a check, click
**Start Dictating**.

## Dictating

1. Click where you want the text: a message, a document, a search box.
2. Press **Right ⌘** (the Command key on the right). The capsule appears at the
   top of the screen and listens.
3. Speak. Your words appear in the capsule as you talk.
4. Press **Right ⌘** again. The recording is transcribed and typed at the
   cursor.

- **Escape** while dictating cancels; nothing is typed.
- A minute without speech ends the dictation by itself and types what you said.
- A very short tap is ignored, so a stray press does nothing.
- If Accessibility isn't allowed, the text is copied instead, and the capsule
  says so; paste it with ⌘V.

The draft in the capsule is quick and may change as you go; when you stop, the
whole recording is transcribed again, so the typed text can be a little better
than the draft.

## The menu bar panel

<p align="center"><img src="images/mac/menu-bar.png" width="300" alt="The menu bar panel"></p>

Click the waveform in the menu bar for:

- the status: ready, listening, processing, or what setup still needs;
- **Start Dictation** / **Stop Dictation**, for when you'd rather click;
- **Language**: switch between automatic detection and each of your languages,
  or **Choose Languages…**;
- your last dictation, with a copy button;
- **Finish Setup…** while something is missing, **Settings…** and **Quit**.

## Settings

Open **Settings…** from the menu bar panel.

### General

- **Open at login**: start SuperDictate when you log in.
- **Play sounds**: a soft sound when dictation starts and stops.
- **Permissions**: the three permissions, with **Allow…** for any that's
  missing.

### Dictation

- **Key**: Right Command ⌘ (default), Right Option ⌥, Right Control ⌃ or fn
  (Globe). Only keys that do nothing on their own are offered, so pressing one
  never types anything.
- **Use**: press to start and again to stop, or hold while talking.
- **Press Return after typing**: sends a chat message right away. Never when
  dictation stopped by itself.

### Languages

<p align="center"><img src="images/mac/languages.png" width="560" alt="The Languages tab with flag tiles"></p>

Tick the languages you speak. The speech model knows 25 European languages:
English, Spanish, French, German, Italian, Portuguese, Dutch, Polish, Russian,
Ukrainian, Czech, Swedish, Greek, Romanian, Hungarian, Bulgarian, Danish,
Finnish, Slovak, Croatian, Lithuanian, Slovenian, Latvian, Estonian and Maltese.

**Recognize** sets how it listens:

- **Automatically** tells your languages apart. When all of them use the same
  alphabet (for example English, German and Polish), it also keeps letters of
  other alphabets out of short, unclear phrases.
- **Only <language>** listens for that one. Choose it if short phrases still
  come out in the wrong alphabet.

### Capsule

<p align="center"><img src="images/mac/capsule.png" width="560" alt="The Capsule tab"></p>

While this tab is open, the real capsule shows on your screen with sample
words, and every change appears on it at once. Dictation waits meanwhile: the
shortcut shows "Close Capsule settings to dictate".

- **Skin**: thirteen looks. **Liquid Glass** is Apple's own glass on macOS 26
  (and the default there); on macOS 14 and 15 it's a close recreation.
- **Size**, **Opacity**, **Accent** (the voice meter's color; Multicolor follows
  your Mac's accent color) and **Voice meter** (bars, wave or pulse).
- **Widest**: how far the capsule grows as your words appear.
- **Place**: six corners and edges, or **Move…** to drag it anywhere.
- **Screen**: the screen you're working on, or always the main screen.
- **Show words as you speak** and **Show recording time**.

<p align="center"><img src="images/mac/editor.png" width="720" alt="Moving the capsule"></p>

In **Move…** the screen dims. Drag the capsule; it snaps to the edges and the
center lines, which light up. The dashed outline shows how far it grows as your
words appear: next to the left or right edge it grows away from that edge. Arrow
keys nudge it (with Shift, further). **Done** or Return keeps it, **Cancel** or
Escape doesn't.

### Speech Model

The model's status, its download, and **Show in Finder** for its folder. If a
download fails, **Try Again** picks it up.

### History

Your last 100 dictations, newest first. Hover over one to copy it; right-click
to copy or delete it. **Clear History…** deletes them all.

## Updating

SuperDictate for Mac doesn't update itself yet.

1. Download the new `SuperDictate-macOS-<version>.zip` from the
   [latest release](https://github.com/ZazaKin/SuperDictate-windows/releases/latest).
2. Quit SuperDictate (menu bar panel › **Quit SuperDictate**).
3. Drag the new **SuperDictate** into **Applications** and choose **Replace**.
4. Open it. The first time, allow it again in **Privacy & Security** as in
   [Install](#install).

Your settings, history and the speech model are kept. Because the app isn't
signed with an Apple certificate yet, macOS may treat the new version as a new
app and ask for Accessibility and Input Monitoring again. If a switch is on but
dictation doesn't type, select **SuperDictate** in that list, click **−** to
remove it, then **Allow…** again in SuperDictate's settings.

## Privacy and where things are stored

- Your voice is recognized on the Mac and never sent anywhere. Nothing is
  recorded to disk.
- The app goes online only when you click **Download** for the speech model,
  which comes from Hugging Face.
- No account, ads, analytics or telemetry.

| What | Where |
|---|---|
| The app | `/Applications/SuperDictate.app` |
| Speech model | `~/Library/Application Support/FluidAudio/Models/` (**Settings › Speech Model › Show in Finder**) |
| History (last 100 dictations) | `~/Library/Application Support/SuperDictate/history.json` |
| Settings | macOS preferences for `com.local.superdictate` |

## Uninstalling

1. Quit SuperDictate from the menu bar panel.
2. Drag **SuperDictate** from Applications to the Trash.
3. To remove your data as well, delete the folders
   `~/Library/Application Support/SuperDictate` and
   `~/Library/Application Support/FluidAudio/Models` (in Finder, **Go › Go to
   Folder…** and paste the path), and in Terminal run
   `defaults delete com.local.superdictate`. Skip the model folder if another
   app uses FluidAudio.
4. In **System Settings › Privacy & Security**, remove SuperDictate from
   Microphone, Accessibility and Input Monitoring with **−**.

## Troubleshooting

| Problem | What to do |
|---|---|
| Pressing the key does nothing | Allow **Input Monitoring**, then quit and reopen SuperDictate. Check the key in **Settings › Dictation**. |
| The capsule shows but nothing is typed | Allow **Accessibility**. Until then the text is copied; paste it with ⌘V. |
| "Allow the microphone first" | Allow **Microphone** in **System Settings › Privacy & Security › Microphone**. |
| "No speech detected" | Check the input device in **System Settings › Sound › Input**, and speak a little louder or closer. |
| Short phrases come out in the wrong alphabet | In **Settings › Languages**, untick languages you don't speak, or choose **Only <language>**. |
| Text in a language the model doesn't know | Parakeet knows 25 European languages; others aren't recognized well. The Windows app covers more. |
| The model download stops | Click **Try Again** in **Settings › Speech Model**. It needs about 1 GB free. |
| Permissions are on, but don't work after an update | Remove SuperDictate from the list with **−** and allow it again (see [Updating](#updating)). |
| "SuperDictate is damaged and can't be opened" | See the note under [Install](#install). |

Still stuck? Open an [issue on GitHub](https://github.com/ZazaKin/SuperDictate-windows/issues/new/choose)
with your macOS version and what you tried.
