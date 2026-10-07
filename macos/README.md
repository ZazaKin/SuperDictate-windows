# SuperDictate for Mac

**English** · [Русский](README.ru.md)

Dictation for any app on your Mac. Press **Right ⌘**, speak, press it again,
and your words are typed where the cursor is. Speech is recognized on the Mac
itself, on the Apple Neural Engine; your voice is never sent anywhere.

- **Install and use it:** the [Mac guide](../docs/mac-guide.md), step by step,
  from the download to the first dictation.
- **Download:** `SuperDictate-macOS-<version>.zip` from the
  [latest release](https://github.com/ZazaKin/SuperDictate-windows/releases/latest).
  It needs Apple Silicon (M1 or newer) and macOS 14 or newer.

This folder is the app's source: a Swift package with SwiftUI and AppKit, and
NVIDIA Parakeet TDT v3 speech recognition through
[FluidAudio](https://github.com/FluidInference/FluidAudio).

```bash
swift test --package-path macos              # the core logic's tests
bash macos/scripts/build-app.sh              # macos/dist/SuperDictate.app and a zip
bash macos/scripts/install-local.sh          # replace /Applications/SuperDictate.app
```

The layout, the GitHub build and its screenshots, and what the Mac and Windows
apps share are in [docs/development.md](../docs/development.md).

## Credits

Speech recognition: [NVIDIA Parakeet TDT 0.6B v3](https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3)
(CC BY 4.0), converted to Core ML by FluidInference and run through
[FluidAudio](https://github.com/FluidInference/FluidAudio) (Apache 2.0).
Based on [Parakey](https://github.com/rcourtman/parakey) by Richard Courtman (MIT).
