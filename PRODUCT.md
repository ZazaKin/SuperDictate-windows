# Product

<!-- impeccable:product-schema 1 -->

## Platform

Windows desktop (WPF, .NET 8). Not web: the settings window, setup window,
tray menu, capsule overlay and microphone button are native windows.

## Users

Everyday Windows users who dictate instead of typing into whatever app they
are in: chats, documents, browsers, code editors. Languages in scope: English,
Russian, German, Polish. Many are Russian speakers (the docs are Russian); the
interface is English for now (confirmed 2026-09-27; a Russian UI stays an open
decision).

## Product Purpose

Press a hotkey or the floating microphone button, speak, and the text appears
at the cursor. Recognition runs on the PC with faster-whisper; audio never
leaves the machine. Success: dictation that is faster than typing and never
steals focus from the app being dictated into.

## Positioning

Local, private dictation for any Windows app, free and source-available (MIT +
Commons Clause), from a single self-installing exe with no administrator
rights.

## Operating Context

The app lives in the tray. Most time is spent outside SuperDictate: the
settings window is opened for first-run setup (speech engine, model
download), occasional changes (hotkeys, languages, models, AI cleanup), and
support (updates, donations). The capsule overlay and microphone button are
on screen while dictating over other apps.

## Capabilities and Constraints

- Everything lives in the install folder the user chooses (SuperDictate.exe and
  Uninstall SuperDictate at the top; Data, Models, Runtime, Logs, Recordings,
  Temp); the default avoids the system drive. Models
  are always in its Models folder.
- Dictation can't start until the speech runtime and the chosen model are
  installed.
- Downloads happen only on explicit clicks and are verified against pins.
- The overlay and microphone button must never take focus.
- Interface language: English. Docs: English, plus a Russian README and user
  guide (README.ru.md, docs/user-guide.ru.md).
- Published on GitHub as ZazaKin/SuperDictate-windows; licensor ZazaKin.

## Brand Commitments

- Visual reference pinned by the user (2026-09-27): Telegram style. Telegram
  Desktop shell (solid blue selected item, blue section titles) with content in
  rounded cards.
- Dark only (Telegram's night blue), decided 2026-09-27 after the light theme
  shipped briefly; there is no theme setting.
- Save applies settings and keeps the window open.
- Must stay light, aesthetic and responsive (resizable window, compact layout
  when narrow).

## Product Principles

- The tool disappears into the task: dictation never interrupts the app in use.
- Nothing happens behind the user's back: downloads, updates and moves happen
  on a click, with visible progress and a Cancel.
- Plain language over jargon in every label and error.

## Accessibility & Inclusion

Keyboard reachable with visible focus; text meets WCAG AA (4.5:1), controls
3:1, checked by the self-test (`theme.contrast_*`); screen-reader names on
every control.
