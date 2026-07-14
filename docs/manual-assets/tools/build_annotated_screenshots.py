"""批量生成 TDJS-Vision 说明书模块红框标注图片。"""

from __future__ import annotations

import csv
import re
from dataclasses import dataclass
from pathlib import Path

from red_box_annotation import load_annotation_config, render_annotated_image


ROOT = Path(__file__).resolve().parents[3]
DEFAULT_CONFIG = ROOT / "docs" / "manual-assets" / "annotations" / "red-box-annotations.json"
DEFAULT_SCREENSHOTS = ROOT / "docs" / "manual-assets" / "screenshots"
DEFAULT_OUTPUT = ROOT / "docs" / "manual-assets" / "annotated-screenshots"


@dataclass(frozen=True)
class BuildSummary:
    """记录批量标注图片生成结果。"""

    module_count: int
    image_count: int
    region_count: int


def safe_file_name(module_name: str) -> str:
    """将模块名称转换为可用于 Windows 文件名的安全文本。"""

    return re.sub(r'[<>:"/\\|?*]', "_", module_name).strip() or "未命名模块"


def build_annotated_images(
    config_path: Path,
    screenshots_dir: Path,
    output_dir: Path,
) -> BuildSummary:
    """为配置中的全部模块生成红框图片和标注明细清单。"""

    config = load_annotation_config(config_path)
    output_dir.mkdir(parents=True, exist_ok=True)
    manifest_path = output_dir / "annotation-manifest.csv"
    manifest_rows: list[list[str]] = []

    for sequence, (module_name, annotation) in enumerate(config.items(), start=1):
        source_path = screenshots_dir / annotation.source_file
        if not source_path.exists():
            raise FileNotFoundError(f"模块“{module_name}”的原图不存在：{source_path}")
        output_name = f"{sequence:03d}-{safe_file_name(module_name)}.png"
        output_path = output_dir / output_name
        render_annotated_image(source_path, output_path, annotation)

        for region in annotation.regions:
            manifest_rows.append(
                [
                    module_name,
                    annotation.source_file,
                    output_name,
                    str(region.index),
                    region.label,
                    ",".join(f"{value:.4f}" for value in region.box),
                ]
            )

    with manifest_path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.writer(stream)
        writer.writerow(["模块", "原图", "标注图", "编号", "说明", "归一化矩形"])
        writer.writerows(manifest_rows)

    return BuildSummary(
        module_count=len(config),
        image_count=len(config),
        region_count=len(manifest_rows),
    )


def main() -> None:
    """使用工程默认目录生成全部模块标注图片。"""

    summary = build_annotated_images(DEFAULT_CONFIG, DEFAULT_SCREENSHOTS, DEFAULT_OUTPUT)
    print(
        f"MODULES={summary.module_count} "
        f"IMAGES={summary.image_count} "
        f"REGIONS={summary.region_count} ERRORS=0"
    )


if __name__ == "__main__":
    main()
