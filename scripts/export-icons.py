"""Export the Jerry mascot PNG and Windows ICO frames from the source artwork.

Optional asset tooling: Python 3 + Pillow. The Windows app needs neither.
Run from any directory: python scripts/export-icons.py
"""
from pathlib import Path
import io
import struct
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
BRANDING = ROOT / "assets" / "branding"
SIZES = (16, 20, 24, 32, 40, 48, 64, 96, 128, 256)


def fit_icon(artwork: Image.Image, size: int) -> Image.Image:
    icon = artwork.copy()
    icon.thumbnail((size, size), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (size, size))
    canvas.alpha_composite(icon, ((size - icon.width) // 2, (size - icon.height) // 2))
    return canvas


def main() -> None:
    with Image.open(BRANDING / "jerry-source.png") as source:
        artwork = source.convert("RGBA")
    bounds = artwork.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("Source mascot has no visible pixels")
    # Remove only unused transparent margins. The mascot's proportions, color
    # and close glow remain intact; every small frame uses the full icon slot.
    artwork = artwork.crop(bounds)
    fit_icon(artwork, 1024).save(BRANDING / "jerry.png", optimize=True)
    frames = []
    for size in SIZES:
        stream = io.BytesIO()
        fit_icon(artwork, size).save(stream, format="PNG", optimize=True)
        frames.append(stream.getvalue())
    header = bytearray(struct.pack("<HHH", 0, 1, len(SIZES)))
    offset = 6 + 16 * len(SIZES)
    for size, frame in zip(SIZES, frames):
        dimension = size if size < 256 else 0
        header.extend(struct.pack("<BBBBHHII", dimension, dimension, 0, 0, 1, 32, len(frame), offset))
        offset += len(frame)
    (BRANDING / "jerry.ico").write_bytes(header + b"".join(frames))
    print("Jerry icon exported:", ", ".join(str(size) for size in SIZES), "px")


if __name__ == "__main__":
    main()
