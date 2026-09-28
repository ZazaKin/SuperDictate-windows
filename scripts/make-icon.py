"""Draws src/SuperDictate/SuperDictate.ico: a white microphone on a Telegram-blue circle.

Each size is drawn on its own at 8x and scaled down, with heavier strokes at the
small sizes so the microphone stays readable in the tray and title bars. Frames
are stored as 32-bit BMP, which every Windows icon API reads.

Run: python scripts/make-icon.py   (needs Pillow)
"""
from pathlib import Path

from PIL import Image, ImageDraw

SIZES = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
TOP, BOTTOM = (0x37, 0xAE, 0xE2), (0x1E, 0x7F, 0xCF)  # Telegram's logo gradient, a shade deeper at the foot
OUT = Path(__file__).resolve().parent.parent / "src" / "SuperDictate" / "SuperDictate.ico"


def draw(size: int) -> Image.Image:
    scale = 8
    n = size * scale
    u = n / 1024  # design units: the mark is drawn on a 1024 grid
    bold = 1.35 if size <= 24 else 1.15 if size <= 48 else 1.0

    # Vertical gradient clipped to a circle.
    gradient = Image.new("RGB", (1, n))
    for y in range(n):
        t = y / (n - 1)
        gradient.putpixel((0, y), tuple(round(a + (b - a) * t) for a, b in zip(TOP, BOTTOM)))
    gradient = gradient.resize((n, n))
    mask = Image.new("L", (n, n), 0)
    inset = round(16 * u)
    ImageDraw.Draw(mask).ellipse((inset, inset, n - inset, n - inset), fill=255)
    image = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    image.paste(gradient, (0, 0), mask)

    d = ImageDraw.Draw(image)
    white = (255, 255, 255, 255)
    stroke = 60 * bold * u
    cx = 512 * u

    # Capsule.
    half = 118 * bold * u
    top, bottom = 198 * u, 582 * u
    d.rounded_rectangle((cx - half, top, cx + half, bottom), radius=half, fill=white)

    # Holder: the lower half of a ring around the capsule, with round ends.
    r, cy = 214 * u, 450 * u
    d.arc((cx - r - stroke / 2, cy - r - stroke / 2, cx + r + stroke / 2, cy + r + stroke / 2), 0, 180, fill=white, width=round(stroke))
    for x in (cx - r, cx + r):
        d.ellipse((x - stroke / 2, cy - stroke / 2, x + stroke / 2, cy + stroke / 2), fill=white)

    # Stem and base.
    stem_top, base_y = cy + r, 774 * u
    d.rectangle((cx - stroke / 2, stem_top, cx + stroke / 2, base_y), fill=white)
    base_half = 108 * u
    d.rounded_rectangle((cx - base_half - stroke / 2, base_y - stroke / 2, cx + base_half + stroke / 2, base_y + stroke / 2), radius=stroke / 2, fill=white)

    return image.resize((size, size), Image.LANCZOS)


def main() -> None:
    frames = [draw(size) for size in SIZES]
    frames[-1].save(OUT, format="ICO", sizes=[(s, s) for s in SIZES], append_images=frames[:-1], bitmap_format="bmp")
    frames[-1].save(OUT.parents[2] / "docs" / "icon.png")  # for the README
    print(f"wrote {OUT} ({OUT.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
