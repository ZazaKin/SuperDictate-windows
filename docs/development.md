# Developing SuperDictate

**English** · [Русский](development.ru.md)

This repository holds both apps. They share a design and the same numbers for
the capsule and the live preview, but no code: the Windows app is .NET 8 and
WPF, the Mac app is Swift and SwiftUI.

Read [AGENTS.md](../AGENTS.md) first: it lists the rules every change must keep
(one installed copy, windows that never take focus, downloads only on a click,
logs without transcript text, and more).

## Repository layout

| Part | Where |
|---|---|
| Windows app: settings, tray, capsule, microphone button, installer | `src/SuperDictate` |
| Speech worker (Python, faster-whisper), embedded in the exe | `src/SuperDictate/worker.py` |
| Pinned versions and SHA-256 hashes of the engine's packages (CPU and NVIDIA GPU) | `src/SuperDictate/runtime-requirements*.txt` |
| Windows build, install and release scripts | `scripts/` |
| Release answers: version, repository, signing, donations | `release-settings.ini` |
| Mac app (Swift package) | `macos/` |
| Mac build: tests, app, screenshots | `.github/workflows/macos.yml` |
| Windows build: app, screenshots | `.github/workflows/windows.yml` |
| User guides, release notes, README pictures | `docs/` |
| Website | `website/` |
| Release output (not committed) | `dist/` |

Shared on purpose, so change both together:

| What | Windows | Mac |
|---|---|---|
| Live preview and the silence limit | `Speech/LiveSession.cs` | `SuperDictateCore/LiveSession.swift` |
| Where the capsule sits | `Ui/CapsulePlacement.cs` | `SuperDictateCore/CapsulePlacement.swift` |
| Capsule skins | `Ui/CapsuleSkin.cs` | `SuperDictateCore/CapsuleSkin.swift` |

## Windows

### How it fits together

The app is one self-contained `SuperDictate.exe` (.NET 8). The speech worker,
package lists, license and notices are embedded in it. On first run it installs
its own Python from NuGet (checked against its SHA-512) and the pinned packages
from PyPI (each file checked against its SHA-256) into `Runtime` in the install
folder; the user then downloads a model from Hugging Face, pinned to a specific
commit.

The install folder (recorded as `InstallLocation` in the Apps entry):

| Item | Contents |
|---|---|
| `SuperDictate.exe` | the program; an update replaces only this |
| `Uninstall SuperDictate` | removes the program (same as Uninstall in Apps) |
| `Data` | settings and history |
| `Models` | speech models, always here |
| `Runtime` | the speech engine (its own Python with packages) |
| `Logs` | logs, without transcript text |
| `Recordings`, `Temp` | temporary recordings and downloads |

### Build and test

Needs the .NET 8 SDK (`build-app.ps1` also finds it in `%LOCALAPPDATA%\dotnet-sdk8`).

```powershell
scripts\build-app.ps1 -OutputDir <folder>      # build SuperDictate.exe
<folder>\SuperDictate.exe --self-test          # self-test; exit code 0 means all passed
<folder>\SuperDictate.exe --snapshot <folder>  # pictures of the settings and the capsule
```

Don't run a built copy directly: outside the install folder the exe opens its
installer. Use `--self-test` to check a build. `--snapshot` draws the settings
pages, every capsule skin and the frames of the capsule writing a sentence to
PNG files, with default settings, off screen; the README's Windows pictures
come from it.

### Install for development

```powershell
scripts\install-local.ps1
```

Builds next to the project, runs the self-test, and installs the build through
the same code as the customer installer (`SuperDictate.exe --install`):
replaces `SuperDictate.exe` in the install folder without touching data, then
starts the app. On a first install you can pick the folder:
`scripts\install-local.ps1 -InstallRoot D:\SuperDictate`.

If the speech engine isn't installed in the install folder yet, the self-test
skips its check (a `SKIP` line) so the install can go ahead; the release script
refuses such a build.

## Mac

Needs an Apple Silicon Mac with Xcode 26 or newer for the Liquid Glass capsule
(Xcode 16 builds everything else). Without a Mac, push a branch: GitHub builds
and tests it and keeps the app and screenshots as downloads (see below).

```bash
swift test --package-path macos              # the core logic's tests
bash macos/scripts/build-app.sh              # macos/dist/SuperDictate.app and a zip
bash macos/scripts/install-local.sh          # build and replace /Applications/SuperDictate.app
python macos/scripts/make-icon.py            # redraw the icon (needs Pillow)
```

| Path | What it is |
|---|---|
| `Sources/SuperDictateCore` | Live preview, voice detection, the caption, capsule placement and skins, languages; no Mac frameworks, fully tested |
| `Sources/SuperDictate/App` | The app, and `DictationModel`, which runs a dictation from key press to typed text |
| `Sources/SuperDictate/Audio` | Microphone capture, 16 kHz mono |
| `Sources/SuperDictate/Speech` | Parakeet TDT v3 through FluidAudio, one request at a time |
| `Sources/SuperDictate/Input` | The hotkey, typing the text, permissions |
| `Sources/SuperDictate/UI` | Capsule, menu bar panel, settings, setup window |
| `Resources` | `Info.plist`, entitlements, the icon master |

Ground rules: bundle identifier `com.local.superdictate`; the only copy anyone
runs is `/Applications/SuperDictate.app`; nothing resets permissions
automatically; a permission request shows either the system prompt or one
System Settings pane, never both.

### The GitHub build

Every push that changes `macos/` runs `.github/workflows/macos.yml` on macOS 26:
the core tests, the app build, then `SuperDictate --snapshot <folder>`, which
draws every screen, light and dark, to PNG. The window server draws Liquid
Glass, so the pictures show the painted glass of macOS 14 and 15 instead; check
the real glass on a Mac with macOS 26.

Download **SuperDictate-macOS** (the app) and **SuperDictate-screenshots** from
the run's page, or with `gh run download <run> -n SuperDictate-screenshots`.

## Releasing

A release carries both apps, with one version number.

1. Fill in `release-settings.ini`: version, repository, signing certificate
   thumbprint, donation options (see [donations-guide.md](donations-guide.md)),
   and write `docs/releases/<version>.md`. Raise `CFBundleShortVersionString`
   in `macos/Resources/Info.plist` to the same version.
2. Double-click `build-release.cmd`. The window stays open and ends with either
   "Release ready" or a list of what to fix.
3. Push, wait for the Mac build to pass, download its app zip and name it
   `SuperDictate-macOS-<version>.zip`; add its SHA-256 to `dist\SHA256SUMS.txt`.
4. Create the GitHub release with both files and `SHA256SUMS.txt`, then commit
   `dist\update.json` to the repository root (only after the release exists, or
   installed copies would be offered an update that isn't there yet).

The script checks the answers first, then fills them in where they belong: the
version into `SuperDictate.csproj`, the repository address into the docs, the
thumbprint into `release-signing.sha1`, and the donation options into the last
section of each README and `.github/FUNDING.yml` (GitHub's Sponsor button).
`release-settings.ini` itself is embedded in the Windows app: the links and
donation options on **Settings › Support** come from it, and empty lines are
hidden. The full procedure is in [RELEASE_CHECKLIST.md](../RELEASE_CHECKLIST.md).

Signing: the first certificate used is pinned in `release-signing.sha1`, and the
script refuses to sign with any other. Don't change certificates: SmartScreen
reputation is tied to it. Without a thumbprint the build is unsigned and users
see a SmartScreen warning.

The Windows self-test also transcribes speech with the installed engine and
models, so releases are built on a PC where SuperDictate is set up.
