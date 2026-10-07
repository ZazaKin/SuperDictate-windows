# Privacy

SuperDictate is built for local dictation. This page lists everything the
apps store and every place they connect to: [Windows](#windows) first, then
the [Mac](#mac).

## Windows

### What stays on your PC

- Microphone audio is recognized on your PC and never sent to a speech
  service. The temporary recording is deleted right after recognition.
- Transcripts, history and settings are stored in the `Data` folder inside the
  folder you chose at install.
- Logs (`Logs\dictation.log` in the same folder) contain only times, lengths
  and errors, never the text of what you said. The log starts over when it
  reaches 5 MB (the previous one is kept as `dictation.old.log`).
- Speech models and the engine are stored on your PC, in `Models` and `Runtime`
  in the install folder.

### When the app goes online

Only when you click:

| Button | Connects to | What is sent |
|---|---|---|
| **Speech runtime › Install** | `api.nuget.org`, `pypi.org`, `files.pythonhosted.org` | Ordinary package download requests |
| **Models › Download** | `huggingface.co` | A model download request |
| **Local LLM › Download** | Your Ollama, which connects to `ollama.com` | The model name |
| **Check for updates**, **Download and install** | `raw.githubusercontent.com`, `github.com` | A request for `update.json` and the installer |
| **Get Ollama**, **Donate**, **User guide**, **Report a problem** | Open a page in your browser | — |

Everything downloaded is verified: Python against the SHA-512 from the NuGet
catalog, packages against each file's SHA-256, models against a pinned commit,
updates against the SHA-256 in `update.json`.

### AI cleanup (optional, off by default)

- **Built-in rules** run entirely on your PC.
- **Local LLM** sends the finished transcript to the server address you set
  (by default `http://localhost:11434`, Ollama on your PC).
- **Cloud LLM** sends the finished transcript (never audio) to the
  OpenAI-compatible service you chose, with your API key. The key is kept only
  in Windows Credential Manager, never in settings files or logs. Some
  providers may keep the text they receive; check their policy before
  dictating anything personal. If the request fails, the original local text is
  pasted.

### Deleting your data

When uninstalling, tick **Also delete my settings, history and downloaded
models** to delete settings, history, the engine, downloaded models and the
saved API key. Without it, the data is kept for a later reinstall.

## Mac

### What stays on your Mac

- Microphone audio is recognized on the Mac, on the Apple Neural Engine, and
  never sent anywhere. It's kept in memory only while you dictate and never
  written to disk.
- Your last 100 dictations are kept in
  `~/Library/Application Support/SuperDictate/history.json`; **Settings ›
  History › Clear History…** deletes them.
- Settings are kept in macOS's preferences for `com.local.superdictate`.
- The speech model is kept in `~/Library/Application Support/FluidAudio/Models`.

### When the app goes online

Only when you click **Download** for the speech model, which comes from
`huggingface.co`. **Source Code** and **Support the Project** in **Settings ›
About** open a page in your browser.

### Permissions

The Mac app asks for Microphone (to hear you), Accessibility (to type your
words) and Input Monitoring (to notice the dictation key). It never resets or
changes them itself; you can take them back any time in **System Settings ›
Privacy & Security**.

### Deleting your data

Delete the app and the folders above, and run
`defaults delete com.local.superdictate` in Terminal. The
[Mac guide](docs/mac-guide.md#uninstalling) has the steps.

## What there isn't, in either app

No accounts, ads, analytics, telemetry, crash reports or automatic update
checks.
