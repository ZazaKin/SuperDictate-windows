# The README video

The 25-second tour at the top of the README (`docs/media/showcase.mp4`, and its
opening shot as `docs/images/showcase.gif`), made with
[HyperFrames](https://hyperframes.heygen.com): HTML scenes animated with GSAP,
rendered to video. What it says and why is in `BRIEF.md`; the shots, as planned
and as built, in `STORYBOARD.md`.

Every capsule, settings page and skin in it is drawn by the app itself. To bring
them up to date after the app changes:

1. Build the Windows app and run `SuperDictate.exe --snapshot <folder>`.
2. From `<folder>/tour`, encode the four capsule clips (each folder of frames, at
   30 frames a second) to see-through WebM, for example:
   `ffmpeg -framerate 30 -i dictate/frame-%03d.png -c:v libvpx-vp9 -pix_fmt yuva420p -auto-alt-ref 0 -crf 24 -b:v 0 assets/capsule-dictate.webm`
   (likewise `languages` and `waiting`), and encode `glass-motion` as an ordinary
   MP4: `ffmpeg -framerate 30 -i glass-motion/frame-%03d.png -c:v libx264 -crf 16 -pix_fmt yuv420p assets/glass-motion.mp4`.
3. Copy `settings-capsule.png` and `settings-languages.png` from `<folder>` to
   `assets/`, and the last frame of `tour/waiting` to `assets/capsule-waiting-still.png`.
4. For the skin wheel, encode each `tour/skin-<id>` folder the same way as the
   capsule clips, cropped to the capsule (`-vf crop=648:154:316:32`), into
   `assets/skins/<id>.webm`.
5. The sound is synthesized: `python score.py score.wav`, then
   `ffmpeg -i score.wav -af loudnorm=I=-21:TP=-3:LRA=11 -ar 48000 -c:a aac -b:a 192k assets/score.m4a`. Its cues follow the
   film's timings; move a shot and move its cue in `score.py` too.

Then, with Node 22 and FFmpeg installed:

```bash
npx hyperframes check
npx hyperframes preview
npx hyperframes render --output showcase.mp4
```
