# Design

SuperDictate's windows follow Telegram Desktop's night theme: a dark sidebar
whose selected page is a solid blue pill, content in rounded cards on a deep
blue-grey field, blue card titles and soft-blue buttons. Every value below lives in
`src/SuperDictate/Ui/Theme.cs` (colors, control templates) and the building
blocks at the end of `Ui/SettingsWindow.cs` (Page, Group, Row); views never
build their own colors.

## Color

Dark only: Telegram's night theme. There is no light theme or theme setting.

| Token | Value | Use |
|---|---|---|
| Bg | #0E1621 | the field behind cards; status chip |
| Side, Card | #17212B | sidebar, cards |
| Field | #242F3D | text boxes, combo boxes |
| Control | #202B36 | hover fill, slider and progress tracks, chips |
| Stroke | #243140 | hairlines between rows, window outline |
| StrokeHover | #34424F | input outlines, scroll thumb |
| Text | #F5F5F5 | primary text |
| Muted | #8395A7 | hints, secondary text |
| Raised | #1E2A36 | menus and drop-downs |
| Accent | #3272AA | fills under white text: selected page, primary button, switch on, progress |
| Link | #6AB2F2 | blue text: card titles, soft buttons, selected chip |
| AccentSoft | #1F3448 | soft button fill, info notice, selected chip |
| Ok / Warn / Bad | #4FCB7C / #F5B14C / #F25C63 | status dots and text |
| BadFill / BadSoft | #C93A42 / #301A1E | confirm-delete button / error notice |

Every text pair is 4.5:1 and every control outline 3:1; the self-test
(`theme.contrast`) fails the build otherwise. Add a pair there when a new
text-on-fill combination appears.

## Type

Segoe UI Variable Text, one family. Page title 20 semibold; card title 13.5
semibold in Link; row label 13.5; body 13; hints and paths 12 Muted; buttons 13
semibold. Addresses and licence text use Cascadia Mono.

## Shape and space

- Cards: radius 12, no border, 14 between cards. Rows: 18 × 11 padding,
  inset hairline starting 18 from the left.
- Controls: radius 10, height 32 (buttons) and 34 (fields, combos). Sidebar
  items 42 high, radius 10. Switch 40 × 22.
- Window: radius 12 with a soft drop shadow in a 16 px transparent margin,
  which is also the resize border. Page content 28 from the edges, at most 760
  wide, centered.

## Components

- **Buttons:** soft (AccentSoft fill, Link text) by default; one primary
  (`AccentButton`, Accent fill, white) per job; `DangerButton` for the second
  click of a delete; `GhostButton` for icon-only actions.
- **Sidebar item:** icon 18 + label; selected is a solid Accent pill with white
  text; hover is Control.
- **Switch:** Accent track with white knob when on; Muted outline and knob when
  off; the knob slides in 160 ms (ease-out) and rests in place when a window
  opens.
- **Save:** applies and keeps the window open; the button reads "Saved" with a
  check for a moment.
- **Setup needed:** until the runtime and chosen model are installed, the
  status chip says so (Warn dot) and Dictate stays disabled.
- **Notice:** under the title bar, AccentSoft (info, Link icon) or BadSoft
  (error, Bad icon), radius 12.
- **Status chip:** bottom of the sidebar, Bg fill, a colored dot plus state and
  engine.
- **Logo:** the app icon (`SuperDictate.ico`, drawn by `scripts/make-icon.py`):
  a white microphone on a Telegram-blue circle. The floating microphone button
  uses the same Accent circle.

## Responsive

- Below 780 px the sidebar folds into a 72 px icon rail; page names move to
  tooltips and the status chip shows only its dot (tooltip for the text).
- A row narrower than 420 px puts its control under the label.
- The window resizes within the work area (minimum 520 × 440); maximized, it
  drops the shadow margin.

## Focus and motion

Keyboard focus is a 2 px Accent ring outside buttons, switches and swatches,
and a 2 px Accent outline inside fields; the selected sidebar item shows a
white inner ring. Motion is limited to the switch knob and the capsule overlay;
nothing animates on page changes.
