"""TDJS-Vision 说明书截图红框标注模型与绘制功能。"""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path


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
