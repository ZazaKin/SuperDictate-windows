# Design

SuperDictate's windows follow Telegram Desktop's night theme, a little refined:
a dark sidebar whose selected page is a solid blue pill, content in rounded
cards on a deep blue-grey field, blue card titles and soft-blue buttons. Every
value below lives in `src/SuperDictate/Ui/Theme.cs` (colors, control
templates) and the building blocks in `Ui/SettingsWindow.cs` (Page, Group,
Row); views never build their own colors.

## Color

Dark only: Telegram's night theme. There is no light theme or theme setting.

| Token | Value | Use |
|---|---|---|
| Bg | #0E1621 | the field behind cards; status chip |
| Side, Card | #17212B | sidebar, cards |
| Field | #242F3D | text boxes, combo boxes |
| Control | #202B36 | hover fill, slider and progress tracks, chips and tiles |
| Stroke | #243140 | hairlines between rows, window outline |
| StrokeHover | #34424F | input outlines, scroll thumb |
| Text | #F5F5F5 | primary text |
| Muted | #8395A7 | hints, secondary text |
| Raised | #1E2A36 | menus and drop-downs |
| Accent | #3272AA | fills under white text: selected page, primary button, switch on, chosen tiles |
| Link | #6AB2F2 | blue text: card titles, soft buttons, selected count |
| AccentSoft | #1F3448 | soft button fill, info notice, chosen tile |
| Ok / Warn / Bad | #4FCB7C / #F5B14C / #F25C63 | status dots and text |
| BadFill / BadSoft | #C93A42 / #301A1E | confirm-delete button / error notice |

Every text pair is 4.5:1 and every control outline 3:1; the self-test
(`theme.contrast`) fails the build otherwise. Add a pair there when a new
text-on-fill combination appears.

## Type

Segoe UI Variable: Display for page titles (22 semibold), Text everywhere
else. Card title 13.5 semibold in Link; row label 13.5; body 13; hints and
paths 12 Muted; buttons 13 semibold. Addresses and licence text use Cascadia
Mono.

## Shape and space

- Cards: radius 14, no border, 16 between cards. Rows: 18 × 11 padding,
  inset hairline starting 18 from the left.
- Controls: radius 10, height 34 (buttons, fields, combos). Sidebar items 42
  high, radius 10. Tiles (languages, skins) radius 12. Switch 40 × 22.
- Window: radius 12 with a soft drop shadow in a 16 px transparent margin,
  which is also the resize border. Page content 28 from the edges, at most 760
  wide, centered.
- Pages rise in quietly: each card fades up 10 px over 380 ms
  (cubic-bezier 0.32, 0.72, 0, 1), 35 ms after the one before. With Windows
  animations off, the page just appears.
- Language tiles carry a round flag (`Ui/LanguageIcon.cs`, vector shapes, since
  Windows has no flag emoji). Arabic shows ع on green rather than one country's
  flag.

## Components

- **Buttons:** soft (AccentSoft fill, Link text) by default; one primary
  (`AccentButton`, Accent fill, white) per job; `DangerButton` for the second
  click of a delete; `GhostButton` for icon-only actions.
- **Sidebar item:** icon 18 + label; selected is a solid Accent pill with white
  text; hover is a light wash.
- **Switch:** Accent track with white knob when on; Muted outline and knob when
  off; the knob slides over 240 ms while the fill fades, and rests in place when
  a window opens.
- **Capsule:** a dark pill (#18181C, hairline edge) that slides down at the top
  of the active screen while dictating: voice bars over a caption. Once speech is
  heard, a live draft replaces the caption.
  - Each new word rises half a line, sharpens out of a blur and fades in over
    420 ms. Each word starts 55 ms after the one before.
  - Words that may still change sit at 55 % opacity and brighten as they settle.
  - The capsule widens with the text from 168 to 460 px. Past that, the line
    glides left and the oldest words fade out at the left edge.
  - The draft stays visible while processing, and the capsule never takes focus.
  - Skins (`Ui/CapsuleSkin.cs`), 13 of them. Each skin's text and status text
    meet 4.5:1 on its own fill; the self-test checks this (`capsule.skins`).
  - Liquid Glass recreates Apple's glass without a blur: a smoky see-through
    fill, a rim that is bright at the top, faint at the middle and brighter
    again at the bottom, and a white sheen fading down across the top half.
    On a Mac with macOS 26 the same skin is Apple's own glass.
  - Placement (`Ui/CapsulePlacement.cs`) keeps the capsule to an edge on each
    axis. It grows away from the side it keeps to, stays a 12 px margin
    inside the work area, and snaps within 14 px while dragged.
  - Against the top or bottom edge it slides in from that edge; anywhere else
    it zooms in from 90 % at its anchored corner.
  - While the Capsule settings page is open, the real capsule shows on screen
    as a live sample.
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

Keyboard focus is a 2 px Accent ring outside buttons, chips, switches, sliders
and swatches, and a 2 px Accent outline inside fields; the focused sidebar item
shows a light inner ring. Rings appear only when focus came from the keyboard
(WPF's FocusVisualStyle), never after a click. Text fields always show theirs.

Motion is feedback, never decoration:

- Hover fades a 6 % white wash in over 100 ms and out over 240 ms.
- Pressing shows a 16 % black shade at once and sinks the control to 97 % in
  100 ms. Releasing eases both back over 240 ms. Darkening on press keeps white
  text above 4.5:1.
- A switch knob grows on press and slides across over 240 ms while the blue
  fill fades in. The animations are additive, so a switch flipped back
  mid-slide turns around where it is.
- The slider knob grows while dragged, and the floating mic button sinks the
  moment it's pressed. The Saved and Copied check marks grow into place.
- Every animation starts from the control's current on-screen value. All of
  it is transform and opacity only, and lands instantly when Windows'
  animation effects are off.
- Page changes: the cards rise in (see Shape and space).
