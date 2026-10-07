# Contributing

Thanks for your interest in SuperDictate! The Windows app is in
`src/SuperDictate`, the Mac app in `macos/`; how to build and test each is in
[docs/development.md](docs/development.md).

1. Read [AGENTS.md](AGENTS.md): it lists the rules every change must keep (a
   single installed copy, windows that never take focus, downloads only on a
   click, logs without transcript text, and more).
2. Build and test:

   ```powershell
   scripts\build-app.ps1 -OutputDir <folder>
   <folder>\SuperDictate.exe --self-test
   ```

   Every check must pass. New logic gets a new check in
   `src/SuperDictate/SelfTest.cs`.

   For the Mac app, `swift test --package-path macos` must pass, and new
   logic that needs no Mac frameworks goes in `SuperDictateCore` with a test.
   Without a Mac, push a branch: GitHub builds and tests it and keeps
   screenshots of every screen.
4. Some things are shared between the apps on purpose (the capsule's skins and
   placement, the live preview); change both together. The list is in
   [docs/development.md](docs/development.md).
3. To try a change for real, install it only with `scripts\install-local.ps1`
   (Windows) or `bash macos/scripts/install-local.sh` (Mac); don't run built
   copies directly.
5. In your pull request, describe what changed and how you tested it.

Bugs and ideas go to
[GitHub Issues](https://github.com/ZazaKin/SuperDictate-windows/issues).

By contributing, you agree that your changes are distributed under the
project's license (MIT with the Commons Clause, see [LICENSE](LICENSE)).
