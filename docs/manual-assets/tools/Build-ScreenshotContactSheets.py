"""Build contact sheets for manual screenshot visual verification."""

from __future__ import annotations

import argparse
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def fit_image(image: Image.Image, width: int, height: int) -> Image.Image:
    """Return a contained thumbnail on a white background."""

    copy = image.convert("RGB")
    copy.thumbnail((width, height), Image.Resampling.LANCZOS)
    canvas = Image.new("RGB", (width, height), "white")
    canvas.paste(copy, ((width - copy.width) // 2, (height - copy.height) // 2))
    return canvas


def main() -> None:
    """Build numbered contact sheets from all PNG screenshots."""

    parser = argparse.ArgumentParser()
    parser.add_argument("input_dir", type=Path)
    parser.add_argument("output_dir", type=Path)
    parser.add_argument("--per-sheet", type=int, default=12)
    args = parser.parse_args()

    args.output_dir.mkdir(parents=True, exist_ok=True)
    files = sorted(args.input_dir.glob("*.png"))
    columns = 3
    rows = math.ceil(args.per_sheet / columns)
    cell_width = 520
    cell_height = 330
    image_height = 285
    font = ImageFont.load_default()

    for start in range(0, len(files), args.per_sheet):
        subset = files[start : start + args.per_sheet]
        sheet = Image.new("RGB", (columns * cell_width, rows * cell_height), "#d9dde3")
        draw = ImageDraw.Draw(sheet)
        for offset, path in enumerate(subset):
            row, column = divmod(offset, columns)
            x = column * cell_width
            y = row * cell_height
            with Image.open(path) as source:
                thumbnail = fit_image(source, cell_width - 16, image_height - 8)
            sheet.paste(thumbnail, (x + 8, y + 6))
            label = path.name
            if len(label) > 70:
                label = label[:67] + "..."
            draw.rectangle((x + 8, y + image_height, x + cell_width - 8, y + cell_height - 8), fill="white")
            draw.text((x + 14, y + image_height + 10), label, fill="black", font=font)

        page = start // args.per_sheet + 1
        sheet.save(args.output_dir / f"contact-{page:02d}.png")


if __name__ == "__main__":
    main()
