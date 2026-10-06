<img src="docs/icon.png" width="96" alt="">

# SuperDictate for Windows

**English** · [Русский](README.ru.md)

Private dictation for any Windows app: press a hotkey or the floating
microphone button, speak, and the text appears where your cursor is. Speech is
recognized on your PC with faster-whisper; your voice never leaves it.

This is the Windows edition. The Mac version, for Apple Silicon, is in
[`macos/`](macos/README.md).

## Install in a minute

**Requires 64-bit Windows 10 (version 2004 or later) or Windows 11.**

1. Download `SuperDictate-Setup-<version>.exe` from the
   [latest release](https://github.com/ZazaKin/SuperDictate-windows/releases/latest).
2. Run it. If Windows shows "Windows protected your PC", click **More info** ›
   **Run anyway**.
3. Choose a folder and click **Install**. The default is `SuperDictate` on the
   drive with the most free space (not `C:` when you have another drive).
   Everything the app needs (speech engine, models, settings, logs) stays in
   that folder. No administrator rights needed.
4. In the settings that open, click **Install** under **Speech runtime**, then
   **Download** next to a model (Large v3 Turbo is recommended).
5. When the status says **Ready**, press **Right Alt** and speak. Press
   **Right Alt** again to paste the text. Or click the round microphone button.

First-time setup downloads the speech engine (about 100 MB) and a model (75 MB
to 1.5 GB). After that, dictation works offline.

Installation, settings, AI cleanup, privacy, uninstalling and troubleshooting
are covered in the [user guide](docs/user-guide.md). What's new is in the
[release notes](docs/releases/).

## Updates

Open **Settings › Support** and click **Check for updates**. If there is a new
version, **Download and install** downloads it, verifies its checksum and runs
the installer. The app never goes online on its own, only when you click.

## Found a bug?

Describe it in [GitHub Issues](https://github.com/ZazaKin/SuperDictate-windows/issues/new)
(in the app: **Settings › Support › Report a problem**). For security issues,
see [SECURITY.md](SECURITY.md).

## Data and privacy

- Audio and transcripts stay on your PC; logs never contain transcript text.
- The internet is used only when you click: downloading the engine, models and
  Ollama models, checking for updates, and for Cloud LLM AI cleanup if you
  turn it on.
- The AI cleanup API key is kept in Windows Credential Manager.
- Everything lives in one folder chosen at install: the program, engine,
  models, settings, history and logs. Only shortcuts, the Apps entry and the API
  key in Credential Manager live outside it.
- No accounts, ads, analytics or telemetry.

More in [PRIVACY.md](PRIVACY.md).

## For developers

Read [AGENTS.md](AGENTS.md) first: it lists the rules every change must keep.
How to contribute is in [CONTRIBUTING.md](CONTRIBUTING.md); how to release is in
[RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md).

### Layout

| Part | Where |
|---|---|
| WPF app: settings, tray, capsule, microphone button, installer | `src/SuperDictate` |
| Speech worker (Python, faster-whisper), embedded in the exe | `src/SuperDictate/worker.py` |
| Pinned versions and SHA-256 hashes of the engine's packages (CPU and NVIDIA GPU) | `src/SuperDictate/runtime-requirements*.txt` |
| Release answers: version, repository, signing, donations | `release-settings.ini` |
| Build, install and release scripts | `scripts/` |
| Release output (not committed) | `dist/` |

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
scripts\build-app.ps1 -OutputDir <folder>   # build SuperDictate.exe
<folder>\SuperDictate.exe --self-test       # self-test; exit code 0 means all passed
```

Don't run a built copy directly: outside the install folder the exe opens its
installer. Use `--self-test` to check a build.

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

### Release

1. Fill in `release-settings.ini`: version, repository, signing certificate
   thumbprint, donation options. Setting up donations is covered in
   [docs/donations-guide.md](docs/donations-guide.md).
2. Double-click `build-release.cmd`. The window stays open and ends with either
   "Release ready" or a list of what to fix.

The script checks the answers first, then fills them in where they belong: the
version into `SuperDictate.csproj`, the repository address into the docs, the
thumbprint into `release-signing.sha1`, and the donation options into the last
section of this README and `.github/FUNDING.yml` (GitHub's Sponsor button).
`release-settings.ini` itself is embedded in the app: the links and donation
options on **Settings › Support** come from it, and empty lines are hidden.
After a passing self-test, `dist\` holds `SuperDictate-Setup-<version>.exe`,
`SHA256SUMS.txt` and `update.json`. The full release procedure is in
[RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md).

Signing: the first certificate used is pinned in `release-signing.sha1`, and the
script refuses to sign with any other. Don't change certificates: SmartScreen
reputation is tied to it. Without a thumbprint the build is unsigned and users
see a SmartScreen warning.

The self-test also transcribes speech with the installed engine and models, so
releases are built on a PC where SuperDictate is set up.

## License

SuperDictate for Windows is licensed under MIT with the
[Commons Clause](https://commonsclause.com): you may use it for free (including
at work), change it and share it, but you may not sell it, nor paid services
whose value comes entirely or substantially from it. Full text in
[LICENSE](LICENSE).

The project is based on [Parakey](https://github.com/rcourtman/parakey) by
Richard Courtman; those parts remain under their original MIT license, kept in
`LICENSE`. Third-party components and models are listed in
[NOTICE.md](NOTICE.md).

<a id="donate"></a>

## Support the project

SuperDictate is free. If it saves you time, you can support its development:

<!-- donations: build-release.cmd fills this list from release-settings.ini -->
- **Ko-fi**: [ko-fi.com/zazakin](https://ko-fi.com/zazakin) - Card or PayPal
- **Buy Me a Coffee**: [buymeacoffee.com/zazakin](https://buymeacoffee.com/zazakin) - Card
- **USDT / USDC (EVM)**: `0xEB571a20373a439f9c97EAA203b6c86B927B3482` - Same address on Ethereum, BNB Chain, Polygon, Arbitrum, Base
- **USDT (TRC20)**: `TAUv9GctsU5hCbd5K48otaZBt6H5fLULeb` - TRON network only
- **Solana**: `22qrMU3Jk5rrace5sWS88pqo1AoTftmfHNe4QaNs8nTQ` - USDC or SOL on Solana only
- **Bitcoin**: `bc1q28r8zrnug2jhkn4p33836flhkp78dz6neghhe4` - Bitcoin network only
<!-- /donations -->

The same options are in the app under **Settings › Support**. Send crypto only
on the network named next to the address. Thank you!
