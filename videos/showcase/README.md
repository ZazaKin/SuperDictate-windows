# The README video

The 22-second tour at the top of the README (`docs/media/showcase.mp4`, and its
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
   (likewise `languages` and `waiting`), and copy `glass.png` to `assets/`.
3. Copy `settings-capsule.png` and `settings-languages.png` from `<folder>` to
   `assets/`, and the last frame of `tour/waiting` to `assets/capsule-waiting-still.png`.
4. For the skin wheel, cut one still per skin from `tour/skins` (the order is in
   `tour/skins.txt`), all to the same box, into `assets/skins/00.png` onward.

Then, with Node 22 and FFmpeg installed:

```bash
npx hyperframes check
npx hyperframes preview
npx hyperframes render --output showcase.mp4
```
