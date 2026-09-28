# Contributing

Thanks for your interest in SuperDictate for Windows!

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
3. To try a change for real, install it only with `scripts\install-local.ps1`;
   don't run built copies directly.
4. In your pull request, describe what changed and how you tested it.

Bugs and ideas go to
[GitHub Issues](https://github.com/ZazaKin/SuperDictate-windows/issues).

By contributing, you agree that your changes are distributed under the
project's license (MIT with the Commons Clause, see [LICENSE](LICENSE)).
