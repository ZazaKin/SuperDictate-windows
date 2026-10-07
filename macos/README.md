# SuperDictate for Mac

Dictation for any app on your Mac. Press **Right ⌘**, speak, press it again,
and your words are typed where the cursor is. Speech is recognized on the Mac
itself, on the Apple Neural Engine; your voice is never sent anywhere.

It is the same app as SuperDictate for Windows, made the Mac way:

- **Menu bar app.** No Dock icon. A waveform in the menu bar opens a small panel
  with the status, a Start Dictation button and your last dictation.
- **The capsule.** While you dictate, a black pill springs down under the menu
  bar, in the manner of the Dynamic Island. Its bars follow your voice.
- **Your capsule, your way.** Twelve skins, accent colors, size, opacity and
  three voice meters. Put it in any corner, or drag it anywhere: next to the
  left or right edge it grows away from that edge. While Settings › Capsule
  is open, the real capsule shows on screen with every change.
- **Your languages.** Pick the languages you speak from the 25 the model
  knows, each with its flag, and switch between them from the menu bar.
- **Live preview.** Your words build up in the capsule as you talk. Each word
  rises in out of a blur, and the newest ones brighten as they settle.
- **Silence limit.** A minute without speech ends the dictation by itself.
- **Settings.** Toolbar tabs and grouped forms, like Apple's own apps.
- **Setup.** A setup window walks you through the three permissions and the
  speech model download, ticking each one off as it's done.

## Requirements

- A Mac with Apple Silicon (M1 or newer)
- macOS 14 Sonoma or newer
- About 1 GB free for the speech model (a one-time 460 MB download)

## Install

**Download.** From the [latest release](https://github.com/ZazaKin/SuperDictate-windows/releases/latest),
download `SuperDictate-macOS-<version>.zip`. Unzip it and move SuperDictate to
Applications. Every change to `macos/` is also built automatically, under
[Actions › macOS app](https://github.com/ZazaKin/SuperDictate-windows/actions/workflows/macos.yml).

The app isn't notarized by Apple yet. The first time, right-click
SuperDictate in Applications, choose **Open**, then **Open** again. If macOS
still refuses, open **System Settings › Privacy & Security** and click
**Open Anyway**.

**From source.** With the Xcode 16 command line tools or newer:

```bash
bash macos/scripts/install-local.sh
```

It builds the app, replaces `/Applications/SuperDictate.app` and opens it.

## First start

The setup window asks for three permissions. Each one opens the right place
in System Settings:

| Permission | Why |
|---|---|
| Microphone | To hear you while you dictate |
| Accessibility | To type your words where the cursor is |
| Input Monitoring | To notice the dictation key in any app |

Then click **Download** for the speech model. When every row has a check,
press **Right ⌘** in any app and talk.

## Using it

- **Right ⌘**: start, then stop and type. Choose another key, or hold-to-talk,
  in **Settings › Dictation**.
- **Escape** while dictating cancels.
- **Settings › Dictation** has **Press Return after typing** for chats.
- **Settings › Languages**: tick the languages you speak. Automatic tells
  them apart; when they all use one alphabet it also keeps other alphabets
  out. Or listen for just one. The menu bar panel switches too.
- **Settings › Capsule**: skin, look, position (**Move…** to drag it), the
  screen it shows on, the live preview and the recording time.
- **Settings › History** keeps your last 100 dictations on this Mac.

## Development

```bash
swift test --package-path macos              # the core logic's tests
bash macos/scripts/build-app.sh              # macos/dist/SuperDictate.app and a zip
python macos/scripts/make-icon.py            # redraw the icon (needs Pillow)
```

| Path | What it is |
|---|---|
| `Sources/SuperDictateCore` | Live preview, voice detection, the caption model, capsule placement and skins, languages; no Mac frameworks, fully tested |
| `Sources/SuperDictate/App` | The app, and `DictationModel`, which runs a dictation from key press to typed text |
| `Sources/SuperDictate/Audio` | Microphone capture, 16 kHz mono |
| `Sources/SuperDictate/Speech` | Parakeet TDT v3 through FluidAudio, one request at a time |
| `Sources/SuperDictate/Input` | The hotkey, typing the text, permissions |
| `Sources/SuperDictate/UI` | Capsule, menu bar panel, settings, setup window |
| `Resources` | `Info.plist`, entitlements, the icon master |

The live preview, the capsule's placement and its skins work as on Windows,
with the same numbers (`Speech/LiveSession.cs`, `Ui/CapsulePlacement.cs` and
`Ui/CapsuleSkin.cs` under `src/SuperDictate` there). Change both together.

Ground rules, kept from the original Mac app:

- Bundle identifier `com.local.superdictate`.
- The only copy anyone runs is `/Applications/SuperDictate.app`.
- Nothing resets permissions automatically.
- A permission request shows either the system prompt or one System Settings
  pane, never both.

## Credits

Speech recognition: [NVIDIA Parakeet TDT 0.6B v3](https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3)
(CC BY 4.0), converted to Core ML by FluidInference and run through
[FluidAudio](https://github.com/FluidInference/FluidAudio) (Apache 2.0).
Based on [Parakey](https://github.com/rcourtman/parakey) by Richard Courtman (MIT).
