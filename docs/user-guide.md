# SuperDictate for Windows: user guide

**English** · [Русский](user-guide.ru.md)

SuperDictate turns speech into text in any app. Press a hotkey or the
microphone button, speak, and the text appears where your cursor is. Speech is
recognized on your PC: your voice is never sent anywhere.

## Requirements

- 64-bit Windows 10 (version 2004 or later) or Windows 11.
- A microphone.
- About 2 GB of free space: 300 MB for the speech engine plus the model
  (75 MB to 1.5 GB).
- Internet for the one-time downloads during setup. After that, dictation works
  offline.
- Optional: an NVIDIA graphics card speeds up recognition.

## Installing

1. Download `SuperDictate-Setup-<version>.exe` from the
   [latest release](https://github.com/ZazaKin/SuperDictate-windows/releases/latest).
   If you like, compare its SHA-256 with `SHA256SUMS.txt` on the same page.
2. Run it. If Windows shows "Windows protected your PC", click **More info**,
   then **Run anyway**.
3. Choose a folder and click **Install**. SuperDictate installs for your Windows
   account only, without administrator rights. It suggests `SuperDictate` on the
   drive with the most free space, not the system drive `C:` when you have
   another one: the engine and models take gigabytes. Pick another folder with
   **Change…**; a `SuperDictate` folder is created inside the one you choose.
   Any empty folder you can write to without administrator rights works.
   Everything the app needs stays in that folder (see "Where things are
   stored"), and updates install there too. Leave **Start SuperDictate when I
   sign in** on so dictation is ready after every restart.

After installing, SuperDictate opens its settings on the **Speech model** page
to finish setup.

## First run: engine and model

Until the engine and the chosen model are installed, dictation stays off: the
status shows **Setup needed**, the **Dictate** button is disabled, and the
hotkey, the microphone button and the tray icon open this setup page instead.

You do this once.

1. Under **Speech runtime**, click **Install**. SuperDictate downloads its own
   copy of Python and the faster-whisper engine (about 100 MB, 300 MB on disk).
   A Python you already have is left alone.
2. If your PC has an NVIDIA graphics card, a **GPU acceleration** row appears.
   It's optional (about 1.3 GB) and makes recognition several times faster.
3. Under **Models**, click **Download** next to the model you want:

   | Model | Size | Good for |
   |---|---|---|
   | Large v3 Turbo (recommended) | 1.5 GB | Best accuracy; fast with a graphics card |
   | Small (balanced) | 464 MB | Good accuracy on most PCs |
   | Base (fastest) | 141 MB | Older or slower PCs |
   | Tiny (least memory) | 75 MB | Very little memory; lowest accuracy |

4. Choose the same model under **Engine › Model** and click **Save**.

The status at the bottom left of the settings window changes to **Ready**.

## Dictating

- **Hotkey:** press **Right Alt**, speak, press **Right Alt** again. The text is
  typed into the app you were working in.
- **Microphone button:** click the round floating button to start and again to
  finish. Drag it anywhere on screen; it remembers the spot. It never takes the
  cursor away from the app you're typing in.
- **Tray icon:** one click starts or finishes dictation, a double click opens
  the settings.

While you speak, a small capsule slides down at the top of the screen; its bars
follow your voice. A live preview of your words builds up under them as you
talk. The newest words are dimmer until they settle, and the capsule widens to
fit them. The preview is a quick draft: when you finish, the whole recording
is transcribed again, so what gets typed can be a little better than the
preview. On a slow computer the preview switches itself off so it never
delays your text. While the text is prepared it says **Processing…**.

If you say nothing for a minute, SuperDictate finishes the dictation by itself
and types what it heard, without pressing Enter.

Other hotkeys:

- **Right Ctrl + Right Alt** finishes dictation with the opposite of **Press
  Enter after paste**, for example to send a chat message right away.
- **Right Shift + Right Alt** copies the last transcript to the clipboard.

Change hotkeys in **Settings › Dictation**: click the keyboard button next to a
hotkey and press the combination you want.

## Settings

Open the settings from the Start menu (**SuperDictate**) or by double-clicking
the tray icon. **Save** applies your changes and keeps the window open.

- **Dictation:** microphone, hotkeys, hold-to-talk, pressing Enter after paste,
  and a button to try it out.
- **Languages:** pick the languages you speak from 20 widely used ones,
  including English, Spanish, French, German, Portuguese, Russian, Arabic,
  Hindi, Chinese, Japanese and Korean. Type in the search box to find one.
  Automatic detection only chooses among the languages you picked, so fewer
  means fewer mix-ups. **Mode** can fix one language, which skips detection.
- **Speech model:** which model to use, the speech engine, GPU acceleration,
  model downloads (**Download**, **Cancel**, **Delete**) and the model folder.
  Models always live in `Models` inside the install folder; **Open** shows it.
- **AI cleanup** (off by default) tidies the transcript after recognition:
  - **Built-in rules** remove filler words, repeats and spoken punctuation
    ("comma" → `,`). Works offline.
  - **Local LLM** uses a model running in [Ollama](https://ollama.com) or LM
    Studio on your PC. With Ollama, SuperDictate shows whether the model is
    installed and can download it. If Ollama isn't running, **Get Ollama**
    opens its download page.
  - **Cloud LLM** sends the finished text (never audio) to an
    OpenAI-compatible service you choose, with your own API key.
- **Capsule:** how the capsule looks and where it appears. While this page is
  open, the real capsule shows on your screen with example words, and every
  change shows on it at once; **Save** keeps it.
  - **Skin:** 12 looks, from Midnight and Glass to Paper, Neon and High
    contrast.
  - **Look:** size, opacity, accent color and the voice meter (bars, wave or
    pulse).
  - **Position:** presets, or **Move…** to drag it anywhere. The screen dims,
    the capsule snaps to the edges and the center, and arrow keys nudge it.
    Next to the left or right edge it grows away from that edge as your
    words appear. **Screen** picks where you're working or the main screen.
  - **While you dictate:** the live words, and an optional recording time.
- **History:** recent transcripts with copy buttons.
- **General:** the microphone button and the SuperDictate folder.
- **Support:** support the project, check for updates, open this guide, report
  a problem, read the license and third-party notices.

The settings window uses a dark, Telegram-style look. It resizes, and on a
narrow window the menu folds into a column of icons.

## Privacy

- Microphone audio and transcripts stay on your PC. Logs record only times and
  lengths, never what you said.
- SuperDictate goes online only when you click a download button:
  - **Speech runtime › Install:** `api.nuget.org` (Python) and `pypi.org` /
    `files.pythonhosted.org` (the engine's packages).
  - **Models › Download:** `huggingface.co`.
  - **Local LLM › Download:** through your own Ollama, which downloads from
    `ollama.com`.
  - **Support › Check for updates:** `raw.githubusercontent.com` and
    `github.com`.
- Everything downloaded is checked against checksums, so a tampered file
  won't install.
- With **Cloud LLM** AI cleanup on, the finished transcript is sent to the
  service you configured. The API key is kept in Windows Credential Manager,
  not in the settings file.
- No accounts, ads, analytics or telemetry.

## Where things are stored

Everything SuperDictate needs is in the folder you chose at install. Open it
from **Settings › General › Storage**.

| Item | Contents |
|---|---|
| `SuperDictate.exe` | the program; an update replaces only this |
| `Uninstall SuperDictate` | removes the program (same as Uninstall in Apps) |
| `Data` | settings and history |
| `Models` | speech models, always here |
| `Runtime` | the speech engine (its own Python with packages) |
| `Logs` | logs, without transcript text |
| `Recordings`, `Temp` | temporary recordings and downloads |

Outside that folder there are only the Start menu and Startup shortcuts, the
entry in **Settings › Apps**, and the AI cleanup API key in Windows Credential
Manager.

If you had a version that kept its data in `%LOCALAPPDATA%\SuperDictate`, the
first start of the new version moves settings and history into `Data`, and the
engine and models into the install folder when it's on the same drive (models
from a folder chosen in the old version move too). Models on another drive stay
where they are; download them again on the **Speech model** page. You can then
delete the old folder.

## Updates

Open **Settings › Support** and click **Check for updates**. If there's a new
version, click **Download and install**: SuperDictate downloads it, verifies
its checksum and opens the installer, which replaces the program. Settings,
history and models are kept. SuperDictate never checks for updates on its own,
only when you click.

## Supporting the project

SuperDictate is free. If it saves you time, **Settings › Support** lets you
support its development: **Donate** opens the payment page in your browser, and
crypto options show the wallet address with a copy button. Thank you!

## Reporting a problem

**Settings › Support › Report a problem** opens a form on GitHub. Attach
`Logs\dictation.log` from the install folder; it contains no transcript text.

## License

You may use SuperDictate for free, including at work, change it and share it,
but not sell it (MIT with the Commons Clause). The text is in **Settings ›
Support › License**.

## Uninstalling

Open Windows **Settings › Apps** (on Windows 10: **Apps & features**), find
**SuperDictate** and choose **Uninstall**. Or open the install folder and run
**Uninstall SuperDictate**. The program is removed; settings, history, the
engine and models stay in the install folder unless you tick **Also delete my
settings, history and downloaded models**. The install folder itself is removed
only when nothing is left in it.

## Troubleshooting

- **The hotkey does nothing.** Check that the status in the settings says
  **Ready**. If it says **Setup needed**, finish the steps in "First run". If
  it says **Engine error**, open **Speech model** and make sure the engine is
  installed and the chosen model is downloaded. **Restart engine** in the tray
  menu restarts the engine.
- **The capsule says "Finish setup in Settings":** the speech runtime or the
  chosen model isn't installed yet. Install them on the **Speech model** page.
- **"Microphone unavailable":** allow microphone access in Windows **Settings ›
  Privacy & security › Microphone** (on Windows 10: **Privacy › Microphone**),
  including **Let desktop apps access your microphone**, and check the device
  in **Settings › Dictation** in SuperDictate.
- **Right Alt types characters in your keyboard layout** (AltGr): choose another
  hotkey in **Settings › Dictation**.
- **The engine install or a model download failed:** check your internet
  connection and try again; the download resumes where it stopped.
- **Local LLM can't connect to Ollama:** start Ollama (or install it with **Get
  Ollama**) and reopen the AI cleanup page.
- **Logs:** `Logs\dictation.log` in the install folder (**Settings › General ›
  Storage › Open**).
