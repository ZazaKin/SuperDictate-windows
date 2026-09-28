# SuperDictate for Windows: release checklist

Everything to check before releasing the app to a wide audience. `[x]` means
done and verified, `[ ]` means still open; open items say whose step it is.

Status for release 1.0.0, published 28 September 2026 at
https://github.com/ZazaKin/SuperDictate-windows/releases/tag/v1.0.0 (unsigned).

## 1. Code and build

- [x] Every release answer lives in one file, `release-settings.ini`: version,
      repository, signing thumbprint, donations. The release script checks them
      and fills them into `SuperDictate.csproj`, the docs and
      `release-signing.sha1`.
- [x] A release builds with a double-click on `build-release.cmd`; the window
      stays open and shows the result or what to fix.
- [x] The build has no compiler warnings.
- [x] The self-test (`--self-test`) passes completely; any failure stops the
      release.
- [x] Unused dependencies removed (ONNX Runtime); no debug code.
- [x] Git repository with commits under GitHub's private address
      (`187373027+ZazaKin@users.noreply.github.com`).
- [ ] Later: build on GitHub Actions for every change. The full self-test can't
      pass there (it needs the engine and a microphone), so only the build and
      the hardware-free checks.

## 2. Testing

- [x] First run from scratch: the engine installs into an empty folder (Python
      and packages with checksum verification, about 30 seconds), then a model
      downloads and transcribes.
- [x] Cancelling a download leaves no half-installed model.
- [x] Replacing the installed version (self-test `installer.swap`, a real
      reinstall).
- [x] Uninstall removes the program once the uninstaller has exited, keeps the
      data unless asked, and removes the folder only when it's empty
      (self-test `installer.uninstall`).
- [ ] **You:** install on a clean PC or virtual machine that never had
      SuperDictate. Windows Sandbox is handy (turn it on in "Windows Features";
      needs administrator rights and a restart).
- [ ] **You:** uninstall on a clean PC, with and without deleting data, from
      both **Settings › Apps** and **Uninstall SuperDictate** in the install
      folder.
- [ ] **You:** install into a folder of your choice (**Change…**, for example
      `D:\Programs`): the engine, models, settings and logs appear there and
      nothing is written to `C:`. An update lands in the same folder. A folder
      with other files in it, and `C:\Program Files`, are refused with a clear
      message.
- [ ] **You:** before the engine and a model are installed, dictation stays
      off: the status says **Setup needed**, and the hotkey and microphone
      button open the Speech model page.
- [ ] **You:** Windows 10 and Windows 11; scaling at 100/150/200%; two monitors
      with different scaling.
- [ ] **You:** a PC without an NVIDIA card; a PC with NVIDIA and the GPU pack
      (about 1.3 GB).
- [ ] **You:** an Ollama model download with **Download** (not tested here:
      Ollama isn't installed).
- [ ] **You:** dictating into common apps: browser, Word, Telegram, VS Code,
      input fields at the bottom of the screen; the microphone button in each.
- [ ] **You:** AltGr layouts (Polish, German) and the hotkey.
- [ ] **You:** a day in the tray: memory and stability.

## 3. Security

- [x] Everything downloaded is verified: Python against the SHA-512 from the
      NuGet catalog, every package against its SHA-256
      (`pip --require-hashes`), models against a pinned commit, updates against
      the SHA-256 in `update.json`. HTTPS only.
- [x] The API key is in Windows Credential Manager, not in files.
- [x] Logs contain no transcript text.
- [x] Dictation windows never take focus (text can't go to the wrong window).
- [x] Installs without administrator rights, for the current user only.
- [ ] **You, before a later release:** a code signing certificate (see section 9); 1.0.0 shipped unsigned. Put its thumbprint
      into `signing_thumbprint` in `release-settings.ini`; the script signs and
      pins the certificate in `release-signing.sha1`. Unsigned, SmartScreen
      warns users and antivirus software is stricter.
- [ ] **You:** check the finished exe on VirusTotal. The app hooks the keyboard
      and pastes text, which antivirus software can mistake for malicious
      behavior. On a false positive, submit the file to Microsoft:
      https://www.microsoft.com/wdsi/filesubmission.
- [x] Private vulnerability reporting is on for the GitHub repository
      (`SECURITY.md` points to it).

## 4. Privacy and legal

- [x] `PRIVACY.md` and a section in the user guide: what is stored and when the
      app goes online.
- [x] `LICENSE`: MIT with the Commons Clause (licensor: ZazaKin); Parakey's
      original MIT notice is kept, as MIT requires.
- [x] `NOTICE.md`: third-party components and models (all models are MIT). The
      license and notices are embedded in the exe and open from **Settings ›
      Support**.
- [ ] **You:** have a lawyer look at the license if that matters in your
      situation. The Commons Clause isn't an OSI open source license, so the
      accurate description is "source-available, non-commercial".
- [ ] **You:** donations: check whether you need to report them in your
      country.

## 5. Interface

- [x] The first run guides you step by step: engine, model, ready.
- [x] Clear error messages; modal windows only where needed.
- [x] Accessibility: visible keyboard focus, AA contrast, screen reader names.
- [x] Dark Telegram-style theme; resizable window; different DPI.
- [ ] **Decide:** interface language. It's English; the docs are English with
      a Russian README and user guide.

## 6. Install, update, uninstall

- [x] Installer: no administrator rights, choice of install folder, Start menu
      entry, start at sign-in (a Startup shortcut), an entry in **Settings ›
      Apps**, and **Uninstall SuperDictate** in the install folder. Updates
      install into the same folder.
- [x] Updates on a click, with SHA-256 verification; the installer stops the
      running copy and replaces it.
- [x] Uninstall keeps data by default; with the checkbox it deletes everything,
      including the API key.

## 7. Reliability and support

- [x] Unexpected errors are logged and the app keeps running.
- [x] The log rotates at 5 MB.
- [x] A GitHub bug report template, `SECURITY.md`, `CONTRIBUTING.md`.
- [ ] **You:** decide whether you answer only in GitHub Issues or also by
      e-mail / Telegram, and say so in the README.

## 8. Documentation

- [x] English `README.md`, user guide, release notes, `PRIVACY.md`,
      `SECURITY.md`, `CONTRIBUTING.md`; Russian `README.ru.md` and
      `docs/user-guide.ru.md`; `AGENTS.md` holds the rules for developers.

## 9. Code signing

Since 2023 every public code signing key lives on hardware or in a cloud HSM,
and since 27 February 2026 a certificate is valid for at most 459 days, so
signing is renewed about once a year.

Options for an individual developer:

| Option | Cost | Works with the release script | Notes |
|---|---|---|---|
| **Certum Code Signing in the cloud** (individual) | from about $116 a year | Yes: SimplySign Desktop puts the certificate in the Windows certificate store | Identity check with an ID and a recent utility bill or bank statement |
| **Certum Open Source Code Signing** | cheaper | Yes, same way | Only for open source, non-commercial projects; the Commons Clause isn't OSI open source, so Certum may refuse it |
| **Azure Artifact Signing** | about $10 a month | Needs a script change (signtool `/dlib`) | Individuals in the US and Canada only |

Steps with Certum:

1. Order the certificate, verify your identity, activate it in SimplySign.
2. Install SimplySign Desktop and sign in; the certificate appears in
   `certmgr.msc` › Personal › Certificates.
3. Copy its **Thumbprint** into `signing_thumbprint` in `release-settings.ini`.
4. Run `build-release.cmd`: it signs the exe, checks the signature and pins the
   certificate in `release-signing.sha1`. Commit that file.

A renewed certificate has a new thumbprint: delete `release-signing.sha1`
before the first release with it, so the script accepts it.

## 10. Releasing, step by step

1. **Donations.** Set up pages and wallets as in
   [docs/donations-guide.md](docs/donations-guide.md).
2. **Answers.** Open `release-settings.ini` in Notepad and fill in the version,
   repository, certificate thumbprint, and donation links and addresses. Empty
   donation lines are hidden.
3. **Build:** double-click `build-release.cmd`. If an answer is wrong, the
   window lists what to fix; fix it and run again. The result is in `dist\`:
   the installer, `SHA256SUMS.txt`, `update.json`. The script also fills the
   "Support the project" section at the end of the README and
   `.github/FUNDING.yml`, which go to GitHub with the code.
4. **Antivirus:** check the installer on VirusTotal.
5. **Repository:** create `ZazaKin/SuperDictate-windows` on GitHub and push the
   code (the `dist` folder isn't committed):

   ```powershell
   cd E:\System\Downloads\SuperDictate-windows
   git config user.name "ZazaKin"
   git config user.email "187373027+ZazaKin@users.noreply.github.com"
   git add .
   git commit -m "SuperDictate for Windows 1.0.0"
   gh repo create ZazaKin/SuperDictate-windows --public --source . --push
   ```

6. **Sponsor button:** in the repository's Settings › General › Features, turn
   on **Sponsorships**. The button appears once `.github/FUNDING.yml` is on
   `main`.
7. **GitHub release:**

   ```powershell
   gh release create v1.0.0 dist\SuperDictate-Setup-1.0.0.exe dist\SHA256SUMS.txt --title "SuperDictate for Windows 1.0.0" --notes-file docs\releases\1.0.0.md
   ```

8. **Update file:** copy `dist\update.json` to the repository root, commit and
   push to `main`, only after step 7, or the app would offer an update that
   doesn't exist yet.
9. **Check:** in the installed app, **Settings › Support › Check for updates**
   says "up to date"; **User guide** and **Report a problem** open; the
   repository page shows the **Sponsor** button and the "Support the project"
   section with your options.
10. **Clean PC:** download the installer from the release page on another
    computer and go through the install and first run.

## 11. After the release

- Watch the Issues for the first few days.
- A fix: raise `version` in `release-settings.ini`, add
  `docs/releases/<version>.md` (the release won't build without it), and repeat
  section 10 steps 3–9. Installed copies see the update on a click.
- Keep the same signing identity: SmartScreen reputation is tied to it.
