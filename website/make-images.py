"""Draws the website's raster images from the app icon (run once, results are committed):

    python website/make-images.py      # needs Pillow and Windows' Segoe UI fonts

src/favicon.ico, src/assets/apple-touch-icon.png and src/assets/og.png, the
1200x630 card shown when the site is shared.
"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = Path(__file__).resolve().parent
ICON = HERE.parent / "src" / "SuperDictate" / "SuperDictate.ico"
FONTS = Path("C:/Windows/Fonts")


def font(names: list[str], size: int) -> ImageFont.FreeTypeFont:
    for name in names:
        if (FONTS / name).exists():
            return ImageFont.truetype(str(FONTS / name), size)
    raise SystemExit(f"None of {names} found in {FONTS}")


def main() -> None:
    icon = Image.open(ICON)
    icon.size = (256, 256)
    mark = icon.copy().convert("RGBA")

    mark.save(HERE / "src" / "favicon.ico", sizes=[(16, 16), (32, 32), (48, 48)])
    touch = Image.new("RGBA", (180, 180), (7, 10, 15, 255))
    touch.alpha_composite(mark.resize((150, 150), Image.LANCZOS), (15, 15))
    touch.convert("RGB").save(HERE / "src" / "assets" / "apple-touch-icon.png", optimize=True)

    width, height = 1200, 630
    card = Image.new("RGB", (width, height), (7, 10, 15))
    glow = Image.new("RGB", (width, height), (7, 10, 15))
    ImageDraw.Draw(glow).ellipse((-200, 260, 900, 1100), fill=(22, 64, 110))
    ImageDraw.Draw(glow).ellipse((700, -300, 1500, 300), fill=(14, 34, 58))
    card = Image.blend(card, glow.filter(ImageFilter.GaussianBlur(160)), 0.9)

    canvas = card.convert("RGBA")
    canvas.alpha_composite(mark.resize((132, 132), Image.LANCZOS), (96, 118))
    draw = ImageDraw.Draw(canvas)
    semibold = ["seguisb.ttf", "segoeuib.ttf"]
    draw.text((96, 300), "Speak. It's typed.", font=font(semibold, 92), fill=(245, 247, 250))
    draw.text((100, 420), "Private dictation for every Windows app.", font=font(["segoeui.ttf"], 38), fill=(143, 160, 179))
    draw.text((100, 520), "SuperDictate", font=font(semibold, 34), fill=(106, 178, 242))
    canvas.convert("RGB").save(HERE / "src" / "assets" / "og.png", optimize=True)
    print("wrote favicon.ico, apple-touch-icon.png, og.png")


if __name__ == "__main__":
    main()
