# SuperDictate for Windows: Development Invariants

These rules mirror the invariants of the macOS SuperDictate project,
translated to Windows platform semantics. They apply to this whole project.

## One Installed Application

- Everything SuperDictate keeps lives in one install folder, the one the
  customer chose, recorded as `InstallLocation` in the uninstall entry
  (`AppPaths.Root`): `SuperDictate.exe` and an `Uninstall SuperDictate.lnk` at
  the top, then `Data` (settings, history), `Models`, `Runtime`, `Logs`,
  `Recordings`, `Temp`. The default is the non-system fixed
  drive with the most free space (`AppPaths.Suggestions`); `%LOCALAPPDATA%` only
  when there is no other drive. Never write app data to the system drive's
  profile or temp folders: downloads, pip and Hugging Face get `Temp` as their
  temp folder. Only the Apps entry, the Start menu and Startup shortcuts, and the
  Credential Manager key live outside it.
- The only runnable installed build is `SuperDictate.exe` at the top of that
  folder. The installer accepts only an empty folder or one holding
  SuperDictate's own files and folders (`Installer.CheckTarget`); uninstall
  removes the program files (`Installer.ProgramFiles`), the data folders only
  when asked, and never anything else in the folder.
- Always use the application identity `com.local.superdictate`: the
  single-instance mutex is `Local\com.local.superdictate`. Sign-in start is a
  `SuperDictate.lnk` in the user's Startup folder; installing removes the old
  developer logon task `com.local.superdictate.agent` and old program copies.
- Installation stops the running copy, copies the new exe beside the installed
  one as `SuperDictate.exe.new`, swaps it in with one rename
  (`Installer.StageAndReplace`), and starts the new one. Windows locks running
  executables, so never write over a live exe, and never touch the data
  folders during an install. An `App` subfolder from an earlier layout is
  emptied of program files and removed.
- Data from versions before the single-folder layout (`%LOCALAPPDATA%\SuperDictate`)
  is taken over once, on first start (`LegacyData.Adopt`): copied when small,
  moved only when that is a rename on the same drive.
- Never launch a copied, test, smoke, or temporary build. Exercise diagnostics
  through `SuperDictate.exe --self-test`. Any copy outside the install root
  opens its installer instead of running, so the installed build stays the only
  runnable one.
- The customer installer is the exe itself (`SetupWindow`, `Installer`): it
  follows the same stage, stop, swap, restart sequence, installs per user, and
  registers its uninstaller under
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\com.local.superdictate`.
  Uninstall keeps user data unless the customer asks to delete it.
- Never change the Authenticode signing identity during an installation.
  Preserve the pinned release certificate. Never silently rotate the release
  key: SmartScreen reputation is attached to it.
- Never reset Windows privacy settings programmatically. Revoking microphone
  access is an explicit user action in Settings.
- Never open more than one Settings privacy page for a permission request.

## Input and Injection

- The global hotkey uses a `WH_KEYBOARD_LL` hook, never `RegisterHotKey`:
  modifier-only chords are a supported configuration.
- Every synthetic `SendInput` event carries the marker
  `dwExtraInfo = 0x5D1C7A7E` and the hook ignores marked events, so the app can
  never trigger itself.
- Before pasting, release any hotkey modifiers the user is still holding.
  Otherwise `Ctrl+V` reaches the target as `Ctrl+Alt+V`.
- Clipboard contents are saved and restored around every paste.
- The overlay window is `WS_EX_NOACTIVATE | WS_EX_TRANSPARENT |
  WS_EX_TOOLWINDOW` and is never activated. Stealing focus loses the caret and
  breaks the paste target.
- The microphone button is `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`, answers
  `WM_MOUSEACTIVATE` with `MA_NOACTIVATE`, and drags by polling the cursor
  instead of capturing the mouse. A click must never move focus away from the
  app being dictated into.

## Local Installation

Run `scripts/install-local.ps1` (`-InstallRoot <folder>` on a first install).
It builds next to the project, runs the self-test, then installs through the
customer code path (`SuperDictate.exe --install`), replacing only
`SuperDictate.exe` in the install folder, and starts the installed copy without
opening another window.
Without a speech runtime in the install folder the self-test reports the engine
checks as SKIP; `build-release.ps1` refuses such a build.

## Privacy

- Audio and transcripts never leave the machine. The only exception is the
  optional AI cleanup, which is disabled by default and sends only final text
  to a server the user configures.
- The AI cleanup key is stored only in Windows Credential Manager, never in
  `settings.json`.
- Logs never contain transcript text: lengths and timings only.
- Nothing is downloaded without an explicit click. The speech runtime (Python
  from NuGet, packages pinned in `runtime-requirements*.txt` from PyPI), models
  (Hugging Face) and Ollama models are fetched only from their buttons in
  Settings. The engine never falls back to a system Python or to a model name
  that faster-whisper would download on its own.
- Downloads keep their caches next to what they download (`HF_HOME` in the
  models folder, `pip --no-cache-dir`), never in the user profile.
- Everything downloaded is verified against a pin: the Python package against
  its NuGet SHA-512 (`SpeechRuntime.PythonPackageSha512`), every wheel against
  the SHA-256 list in `runtime-requirements*.txt` (`pip --require-hashes`),
  models against a Hugging Face commit (`ModelLibrary.Model.Revision`), and
  updates against the SHA-256 in `update.json`. Changing a version means
  refreshing its pin in the same change.
- The update check runs only when the user clicks **Check for updates**; there
  is no background check.
- Models always live in `Models` inside the install folder; there is no other
  model folder. A `ModelsFolder` left in settings by an earlier version is read
  once: its model folders move in when on the same drive, and it is cleared.
- Dictation never starts before the speech runtime and the chosen model are
  installed (`DictationController.IsSetUp`, state `NeedsSetup`): the hotkey,
  the microphone button and the tray open the setup page instead.
- One engine at a time: engine restarts are numbered and only the newest may
  install its engine (`RestartEngine`), and `IsLoaded` never throws.

## Release

- Every release answer (version, repository, signing thumbprint, donation
  options) lives in `release-settings.ini`. The app embeds it and reads the
  repository and donations through `Support/Project.cs`; empty donation lines
  are hidden. `scripts/build-release.ps1` (double-click `build-release.cmd`)
  validates it before building, then fills the version into the csproj, the
  repository into the docs, the thumbprint into `release-signing.sha1`, and the
  donation options into the README's closing section (between the
  `<!-- donations: -->` markers) and `.github/FUNDING.yml`. Edit donations only
  in the ini.
  Once pinned there, it refuses a different certificate.
- A release follows `RELEASE_CHECKLIST.md`. `update.json` is committed to the
  repository root only after the GitHub release with the exe is live.
- Unexpected errors are logged and the app keeps running
  (`Program.ReportUnexpectedErrors`); never swallow them silently elsewhere.
