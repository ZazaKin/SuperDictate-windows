# Security

## Reporting a vulnerability

Please don't report vulnerabilities in public issues. Report them privately
through
[GitHub Security Advisories](https://github.com/ZazaKin/SuperDictate-windows/security/advisories/new).
Include the version, steps to reproduce and the possible impact. You'll get a
reply as soon as possible, and the fix ships in the next release.

## Supported versions

Fixes are released for the latest version. Update from **Settings › Support ›
Check for updates**.

## What's already in place

- Installs without administrator rights, for the current user only.
- Everything the app downloads is verified: Python against the SHA-512 from the
  NuGet catalog, the engine's packages against each file's SHA-256
  (`pip --require-hashes`), models against a pinned Hugging Face commit, and
  updates against the SHA-256 in `update.json`, over HTTPS only.
- Downloads and network access happen only when you click a button.
- The AI cleanup API key is kept in Windows Credential Manager.
- Logs never contain transcript text.
- The windows that appear while dictating never take focus, so text can't land
  in the wrong app.

## Known limitations

- The installer isn't signed with a certificate yet. Check a download's SHA-256
  against `SHA256SUMS.txt` on the release page.
- The app uses a global keyboard hook (for hotkeys) and pastes through the
  clipboard. Antivirus software sometimes treats that as suspicious; the source
  is open for review.
