"""Draws macos/Resources/AppIcon.png, the 1024 px master of the Mac app icon.

Apple's macOS icon grid: an 824 px rounded square ("squircle", a superellipse)
centred on a 1024 canvas, with a soft shadow under it. Same blue gradient and
white microphone as the Windows icon (scripts/make-icon.py), so the two apps
read as one. build-app.sh cuts every size macOS needs from this file.

Run: python macos/scripts/make-icon.py   (needs Pillow)
"""
import math
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

OUT = Path(__file__).resolve().parent.parent / "Resources" / "AppIcon.png"
TOP, BOTTOM = (0x4C, 0xB8, 0xF0), (0x1A, 0x6F, 0xD0)
SCALE = 2           # drawn at 2048 and scaled down, for smooth edges
N = 1024 * SCALE


def squircle(size: int, exponent: float = 5.0) -> list[tuple[float, float]]:
    """Points of |x|^e + |y|^e = 1 scaled to a size x size box: Apple's continuous corners."""
    half = size / 2
    points = []
    steps = 720
    for i in range(steps):
        t = i / steps * 6.283185307179586
        c, s = math.cos(t), math.sin(t)
        x = half * (abs(c) ** (2 / exponent)) * (1 if c >= 0 else -1)
        y = half * (abs(s) ** (2 / exponent)) * (1 if s >= 0 else -1)
        points.append((x, y))
    return points


def main() -> None:
    body = 824 * SCALE
    offset = (N - body) / 2
    outline = [(offset + body / 2 + x, offset + body / 2 + y) for x, y in squircle(body)]

    mask = Image.new("L", (N, N), 0)
    ImageDraw.Draw(mask).polygon(outline, fill=255)

    # Shadow: the shape, blurred and dropped a little.
    shadow = Image.new("L", (N, N), 0)
    ImageDraw.Draw(shadow).polygon([(x, y + 12 * SCALE) for x, y in outline], fill=90)
    shadow = shadow.filter(ImageFilter.GaussianBlur(14 * SCALE))
    canvas = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    canvas.putalpha(shadow)

    # Body: a vertical gradient, with a faint light at the top edge for depth.
    gradient = Image.new("RGB", (1, N))
    for y in range(N):
        t = min(max((y - offset) / body, 0), 1)
        gradient.putpixel((0, y), tuple(round(a + (b - a) * t) for a, b in zip(TOP, BOTTOM)))
    gradient = gradient.resize((N, N))
    canvas.paste(gradient, (0, 0), mask)

    sheen = Image.new("L", (N, N), 0)
    ImageDraw.Draw(sheen).ellipse((offset - body * 0.2, offset - body * 0.55, offset + body * 1.2, offset + body * 0.45), fill=26)
    sheen = ImageChops.multiply(sheen.filter(ImageFilter.GaussianBlur(110 * SCALE)), mask)
    canvas = Image.composite(Image.new("RGBA", (N, N), (255, 255, 255, 255)), canvas, sheen)

    # The microphone, as on Windows, sized to the rounded square.
    d = ImageDraw.Draw(canvas)
    white = (255, 255, 255, 255)
    u = body / 1024
    cx = N / 2
    top = offset
    stroke = 62 * u
    half = 122 * u
    d.rounded_rectangle((cx - half, top + 190 * u, cx + half, top + 590 * u), radius=half, fill=white)
    r, cy = 222 * u, top + 452 * u
    d.arc((cx - r - stroke / 2, cy - r - stroke / 2, cx + r + stroke / 2, cy + r + stroke / 2), 0, 180, fill=white, width=round(stroke))
    for x in (cx - r, cx + r):
        d.ellipse((x - stroke / 2, cy - stroke / 2, x + stroke / 2, cy + stroke / 2), fill=white)
    base_y = top + 790 * u
    d.rectangle((cx - stroke / 2, cy + r, cx + stroke / 2, base_y), fill=white)
    base_half = 112 * u
    d.rounded_rectangle((cx - base_half - stroke / 2, base_y - stroke / 2, cx + base_half + stroke / 2, base_y + stroke / 2),
                        radius=stroke / 2, fill=white)

    OUT.parent.mkdir(parents=True, exist_ok=True)
    canvas.resize((1024, 1024), Image.LANCZOS).save(OUT)
    print(f"wrote {OUT}")


if __name__ == "__main__":
    main()
