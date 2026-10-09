---
format: 1920x1080
duration: 24s
message: "Speak, and your words appear where you type, in a capsule you can make your own."
arc: Speak → it types → make it yours (glass, skins, languages) → the name
audience: people landing on the GitHub page
mode: collaborative
---

## Decisions

- **Format:** 16:9, 1920 × 1080, about 20 s, silent (no voice, no music). On-screen
  captions carry the words; they sit in the lower band, the UI above it.
- **The spine:** the capsule. One pill is on screen in every frame, at the same spot
  near the top, and each seam happens under it: it builds a sentence, turns to
  glass, changes skin, speaks other languages, and finally settles next to the name.
- **Brand, from the app (`DESIGN.md`):** field #0E1621, card #17212B, text #F5F5F5,
  muted #8395A7, accent #3272AA, link blue #6AB2F2. Segoe UI Variable Display for
  captions (semibold), Segoe UI Variable Text for small labels. Card radius 14,
  easing cubic-bezier(0.32, 0.72, 0, 1), the app's own page-rise.
- **Truthfulness:** every capsule, settings page and skin is drawn by the app's own
  code (`SuperDictate.exe --snapshot`, a tour mode). The document window in frame 01
  is a plain stand-in for "any app you type in"; it is not part of SuperDictate.
- **Bans:** no invented features or numbers; no glow, neon or gradient text; no
  fake app UI; no slideshow (the capsule carries every seam); no screensaver
  motion; no static end card (the close holds on purpose, once).
- **Held frame:** the close holds still for 1.5 s on the name and the message.
- **Seams:** the capsule stays put; the scene underneath slides left (one
  direction for the whole film).

## Locked

The v1 sheet (`storyboard.html`): placement, hierarchy and copy of all five frames.

## Changes from v1

"Improve video. Make in a style of big companies. Better animation, more motion,
aesthetic movements and shot choices." (v2, launch-film treatment)

## Changes from v2

"First 4 sec - the capsule goes out of edges of a frame but it shouldnt. It should
expand camera should be animated smoothly. second 8 - liquid glass should be
animated. 15 skins roll should take more time and roll should be animated slower and
smoother. show only 10 and good ones." (v3)

## As built (v3)

A camera per shot, the shots joined by named transitions; the app still draws every
capsule, page, skin and glass (`SuperDictate.exe --snapshot`, `tour/`).

| Shot | Time | Camera and motion | Into the next |
|---|---|---|---|
| 01 Dictate | 0–6.85 s | Close-up on the capsule, settling out of an 18° tilt, the page a dim out-of-focus shape; "Speak." rises blur-to-sharp. The zoom follows the capsule's measured width in the app's frames, easing out ahead of each widening, so it grows on screen and never leaves the frame (at most 1497 of 1920 px); then one pull-back to the whole desk as the words land at the cursor. | whip pan left, directional blur |
| 02 Glass | 6.85–10.6 s | The app's Liquid Glass, moving (`tour/glass-motion`): the page drifts behind both capsules and the window slides away under the top one, which turns from light glass to dark. On a card turning toward camera out of depth, a light crossing it; "Liquid Glass." beside it; push-in on that capsule as it turns. | zoom-through |
| 03 Skins | 10.45–16.3 s | Ten of the fifteen skins (Liquid Glass, the seven art skins, Aurora, Neon) on a 3D picker wheel that spins in and rolls on slowly, easing into each, the name beside "15 skins." | whip pan left |
| 04 Languages | 16.3–20.2 s | "25" counts up and lands; the Languages page floats in 3D, drifting toward camera; the capsule speaks German, French, Russian and each name lights up. | dissolve, pulling away |
| 05 Close | 19.7–24 s | The capsule comes into focus; the icon pops, the name cascades in letter by letter, one light passes through it; the line under it rises; still for the last 1.7 s. | end |

Length 24 s. Captions in Segoe UI; word reveals follow the registry's per-word-rise
landing. Background: the field with two drifting pools of the app's blue and the
registry vignette.

## Frame 1 — Dictate

- scene: A capsule builds a sentence word by word over a document; then the sentence is typed at the cursor
- duration: 6.5s
- poster: 3.5s
- transition_in: cut
- status: animated
- voiceover: onscreen
- src: compositions/01-dictate.html

0.0–6.5 s. A plain document window (stand-in) on the app's blue-grey field, a
blinking cursor after "Hi team," on its second line. At the top, the Liquid Glass
capsule slides down, voice bars moving, and builds "Move the design review to
Thursday at three, and send the new mockups tonight" word by word (the app's own
frames). Caption, lower left: "Speak." At 4.2 s the capsule shows Processing, the
sentence appears in the document at the cursor, and the caption changes to "Your
words appear where you type." Why: the value claim, shown, not said; it lands in
the first beat.

## Frame 2 — Glass

- scene: Liquid Glass over a bright window turns light; over a dark wallpaper it stays dark
- duration: 3.5s
- poster: 2s
- transition_in: slide-left
- status: animated
- voiceover: onscreen
- src: compositions/02-glass.html

6.5–10.0 s. The app's live-glass picture: one capsule over a bright document
(light glass, dark words), one over the wallpaper (dark glass, white words), with a
slow push in. Caption: "Liquid Glass, light or dark with what's behind it." Why:
the first way the capsule becomes yours, and the newest thing in 1.2.0.

## Frame 3 — Skins

- scene: The capsule cycles through all 15 skins in place, the same words in each
- duration: 3.5s
- poster: 2s
- transition_in: slide-left
- status: animated
- voiceover: onscreen
- src: compositions/03-skins.html

10.0–13.5 s. The capsule stays where it is and changes skin about every quarter
second (Midnight, Liquid Glass, Aurora…), the skin's name in small muted type
under it. Behind it, the Capsule settings page (the app's own) with its skin tiles.
Caption: "15 skins, any color, any size, anywhere on screen." Why: "a capsule you
can make your own", shown.

## Frame 4 — Languages

- scene: The Languages page with its flag tiles; the capsule speaks German, French, Russian
- duration: 3.5s
- poster: 2s
- transition_in: slide-left
- status: animated
- voiceover: onscreen
- src: compositions/04-languages.html

13.5–17.0 s. The app's Languages page, flag tiles chosen. The capsule above it
shows a short sentence in German, then French, then Russian (drawn by the app).
Caption: "25 languages." Why: it speaks your language too, which a visitor asks
next.

## Frame 5 — Close

- scene: The capsule settles beside the app icon and the name; the message holds
- duration: 3.5s
- poster: 3s
- transition_in: crossfade
- status: animated
- voiceover: onscreen
- src: compositions/05-close.html

17.0–20.5 s. The field clears. The app icon (white microphone on Telegram blue)
and "SuperDictate", with "Windows and macOS · speech recognized on your computer" in
muted type. Under them, the message: "Speak, and your words appear where you type,
in a capsule you can make your own." Holds still for the last 1.5 s. Why: the name
to remember and the claim, once.

Total: 6.5 + 3.5 + 3.5 + 3.5 + 3.5 = 20.5 s.
