"""TDJS-Vision 说明书截图红框标注模型与绘制功能。"""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


RED = (230, 0, 18)
WHITE = (255, 255, 255)
NUMBER_FONT = Path(r"C:\Windows\Fonts\arialbd.ttf")

@dataclass(frozen=True)
class AnnotationRegion:
    """描述图片上的一个编号标注区域。"""

    index: int
    label: str
    box: tuple[float, float, float, float]


@dataclass(frozen=True)
class ModuleAnnotation:
    """描述一个说明书模块使用的原图和全部标注区域。"""

    module: str
    source_file: str
    regions: tuple[AnnotationRegion, ...]


def load_annotation_config(path: Path) -> dict[str, ModuleAnnotation]:
    """读取并验证模块红框标注配置。"""

    payload = json.loads(path.read_text(encoding="utf-8"))
    annotations: dict[str, ModuleAnnotation] = {}
    for module_name, raw_module in payload.items():
        raw_regions = raw_module["regions"]
        if not 1 <= len(raw_regions) <= 4:
            raise ValueError(f"模块“{module_name}”的标注区域数量必须为 1 至 4")
        indexes = [int(region["index"]) for region in raw_regions]
        expected_indexes = list(range(1, len(raw_regions) + 1))
        if indexes != expected_indexes:
            raise ValueError(f"模块“{module_name}”的编号必须连续并从 1 开始")

        regions = tuple(
            AnnotationRegion(
                index=int(region["index"]),
                label=str(region["label"]),
                box=tuple(float(value) for value in region["box"]),
            )
            for region in raw_regions
        )
        for region in regions:
            if len(region.box) != 4:
                raise ValueError(f"模块“{module_name}”的矩形必须包含四个坐标")
            if any(value < 0.0 or value > 1.0 for value in region.box):
                raise ValueError(f"模块“{module_name}”的坐标必须位于 0.0 至 1.0")
            left, top, right, bottom = region.box
            if left >= right or top >= bottom:
                raise ValueError(f"模块“{module_name}”的矩形边界顺序无效")
        annotations[module_name] = ModuleAnnotation(
            module=module_name,
            source_file=str(raw_module["source_file"]),
            regions=regions,
        )

    return annotations


def render_annotated_image(
    source: Path,
    output: Path,
    annotation: ModuleAnnotation,
) -> None:
    """在原图副本上绘制红框和白字红底编号。"""

    with Image.open(source) as source_image:
        image = source_image.convert("RGB")

    width, height = image.size
    line_width = max(4, round(min(width, height) * 0.006))
    radius = max(14, line_width * 3)
    font_size = max(16, round(radius * 1.15))
    font = ImageFont.truetype(str(NUMBER_FONT), font_size)
    draw = ImageDraw.Draw(image)

    for region in annotation.regions:
        left, top, right, bottom = region.box
        pixel_box = (
            round(left * width),
            round(top * height),
            round(right * width),
            round(bottom * height),
        )
        draw.rectangle(pixel_box, outline=RED, width=line_width)

        center_x = min(max(pixel_box[0] + radius, radius + line_width), width - radius - line_width)
        center_y = min(max(pixel_box[1] + radius, radius + line_width), height - radius - line_width)
        circle_box = (
            center_x - radius,
            center_y - radius,
            center_x + radius,
            center_y + radius,
        )
        draw.ellipse(circle_box, fill=RED)
        number = str(region.index)
        text_box = draw.textbbox((0, 0), number, font=font)
        text_width = text_box[2] - text_box[0]
        text_height = text_box[3] - text_box[1]
        draw.text(
            (center_x - text_width / 2, center_y - text_height / 2 - text_box[1]),
            number,
            fill=WHITE,
            font=font,
        )

    output.parent.mkdir(parents=True, exist_ok=True)
    image.save(output, format="PNG")
