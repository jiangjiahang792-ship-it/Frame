"""生成并校验 TDJS-Vision 真实流程演示截图的红框标注图。"""

from __future__ import annotations

import argparse
import csv
import json
import re
from dataclasses import dataclass
from pathlib import Path

from real_workflow_capture_model import load_module_review
from red_box_annotation import (
    AnnotationRegion as LegacyRegion,
    ModuleAnnotation as LegacyAnnotation,
    render_annotated_image,
)


ROOT = Path(__file__).resolve().parents[3]
REAL_WORKFLOW = ROOT / "docs" / "manual-assets" / "real-workflow"
DEFAULT_CONFIG = REAL_WORKFLOW / "annotations.json"
DEFAULT_REVIEW = REAL_WORKFLOW / "module-review.csv"
DEFAULT_SCREENSHOTS = REAL_WORKFLOW / "screenshots"
DEFAULT_OUTPUT = REAL_WORKFLOW / "annotated-screenshots"
OLD_CONFIG = ROOT / "docs" / "manual-assets" / "annotations" / "red-box-annotations.json"


@dataclass(frozen=True)
class AnnotationRegion:
    """描述与核对清单可见对象绑定的一个红框。"""

    index: int
    label: str
    object_key: str
    box: tuple[float, float, float, float]


@dataclass(frozen=True)
class ModuleAnnotation:
    """描述单个模块使用的真实原图及其全部红框。"""

    module: str
    source_file: str
    regions: tuple[AnnotationRegion, ...]


@dataclass(frozen=True)
class AnnotationReport:
    """记录批量渲染结果。"""

    module_count: int
    image_count: int
    region_count: int
    errors: tuple[str, ...]


FLOW_OVERRIDES: dict[str, list[tuple[str, tuple[float, float, float, float]]]] = {
    "流程画布": [
        ("流程标签与新增删除入口", (0.00, 0.00, 0.12, 0.065)),
        ("8个真实节点与8条连接线", (0.04, 0.16, 0.66, 0.48)),
        ("启动循环停止控制", (0.91, 0.07, 0.995, 0.12)),
        ("图像预览区", (0.80, 0.13, 0.98, 0.33)),
    ],
    "流程管理": [
        ("流程标签", (0.00, 0.00, 0.06, 0.045)),
        ("新增流程按钮", (0.055, 0.00, 0.083, 0.045)),
        ("删除流程按钮", (0.080, 0.00, 0.108, 0.045)),
        ("8个真实节点与连接线", (0.02, 0.13, 0.65, 0.44)),
    ],
    "运行控制": [
        ("节点数量与运行耗时", (0.00, 0.00, 0.20, 0.065)),
        ("启动循环停止按钮", (0.91, 0.00, 0.995, 0.06)),
        ("已建立流程节点与连接线", (0.015, 0.10, 0.65, 0.44)),
    ],
    "运行参数": [
        ("客户演示流程标签", (0.12, 0.06, 0.28, 0.12)),
        ("曝光增益与AI阈值区", (0.02, 0.13, 0.49, 0.42)),
        ("单图批量测试与保存设置", (0.50, 0.13, 0.99, 0.42)),
        ("运行参数字段表", (0.01, 0.43, 0.99, 0.50)),
    ],
    "方案管理": [
        ("选择或新建流程", (0.00, 0.04, 0.20, 0.15)),
        ("配置方案流程工作区", (0.00, 0.15, 0.65, 0.79)),
    ],
    "主界面与视图管理": [
        ("菜单和常用工具栏", (0.00, 0.00, 0.76, 0.11)),
        ("方案与流程标签页", (0.00, 0.10, 0.48, 0.18)),
        ("图像和运行监控区域", (0.02, 0.17, 0.65, 0.82)),
    ],
}

LIST_ONLY_MODULES = {"PLC通信", "Modbus通信", "TCP通信", "串口通信", "光源控制器"}


def _validate_box(module: str, box: tuple[float, float, float, float]) -> None:
    """校验归一化矩形的坐标和面积。"""

    if len(box) != 4 or any(value < 0.0 or value > 1.0 for value in box):
        raise ValueError(f"模块“{module}”的红框坐标无效。")
    left, top, right, bottom = box
    if left >= right or top >= bottom:
        raise ValueError(f"模块“{module}”的红框边界顺序无效。")
    if (right - left) * (bottom - top) > 0.45:
        raise ValueError(f"模块“{module}”的红框覆盖面积过大。")


def load_annotations(path: Path) -> dict[str, ModuleAnnotation]:
    """读取包含 object_key 的真实流程标注配置。"""

    payload = json.loads(path.read_text(encoding="utf-8"))
    annotations: dict[str, ModuleAnnotation] = {}
    for module_name, raw in payload.items():
        regions = tuple(
            AnnotationRegion(
                index=int(item["index"]),
                label=str(item["label"]),
                object_key=str(item["object_key"]),
                box=tuple(float(value) for value in item["box"]),
            )
            for item in raw["regions"]
        )
        if not 1 <= len(regions) <= 4:
            raise ValueError(f"模块“{module_name}”必须包含 1 至 4 个红框。")
        if [region.index for region in regions] != list(range(1, len(regions) + 1)):
            raise ValueError(f"模块“{module_name}”的红框编号必须从 1 连续排列。")
        for region in regions:
            _validate_box(module_name, region.box)
        annotations[module_name] = ModuleAnnotation(
            module=module_name,
            source_file=str(raw["source_file"]),
            regions=regions,
        )
    return annotations


def _constrain_box(box: list[float]) -> tuple[float, float, float, float]:
    """保留原标注中心，同时把过大的泛化区域收敛到有效界面范围。"""

    left, top, right, bottom = map(float, box)
    width, height = right - left, bottom - top
    if width * height <= 0.44:
        return left, top, right, bottom
    target_height = min(height, 0.48)
    target_width = min(width, 0.44 / target_height)
    center_x, center_y = (left + right) / 2, (top + bottom) / 2
    left = max(0.0, center_x - target_width / 2)
    right = min(1.0, left + target_width)
    top = max(0.0, center_y - target_height / 2)
    bottom = min(1.0, top + target_height)
    return round(left, 4), round(top, 4), round(right, 4), round(bottom, 4)


def initialize_annotations(output: Path = DEFAULT_CONFIG) -> None:
    """从旧模块说明迁移文字，并绑定到新截图中已复核的真实对象。"""

    review = load_module_review(DEFAULT_REVIEW)
    old = json.loads(OLD_CONFIG.read_text(encoding="utf-8"))
    payload: dict[str, object] = {}
    for module_name, row in review.items():
        if module_name in FLOW_OVERRIDES:
            definitions = FLOW_OVERRIDES[module_name]
        else:
            old_regions = old[module_name]["regions"]
            if module_name in LIST_ONLY_MODULES:
                old_regions = old_regions[:1]
            definitions = [
                (str(item["label"]), _constrain_box(item["box"]))
                for item in old_regions[:4]
            ]
        visible = set(row.visible_objects)
        regions = []
        for index, (label, box) in enumerate(definitions, start=1):
            if label not in visible:
                raise ValueError(f"模块“{module_name}”的对象“{label}”未通过原图复核。")
            _validate_box(module_name, tuple(box))
            regions.append(
                {"index": index, "label": label, "object_key": label, "box": list(box)}
            )
        payload[module_name] = {
            "source_file": Path(row.screenshot_file).name,
            "regions": regions,
        }
    output.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )


def _safe_name(module_name: str) -> str:
    """生成 Windows 可用的标注图文件名。"""

    return re.sub(r'[<>:"/\\|?*]', "_", module_name).strip() or "未命名模块"


def _mark_annotations_approved(review_path: Path) -> None:
    """在全部图片成功渲染后，把核对清单的红框状态更新为通过。"""

    with review_path.open("r", encoding="utf-8-sig", newline="") as stream:
        rows = list(csv.DictReader(stream))
    if not rows:
        raise ValueError("模块核对清单为空。")
    fieldnames = list(rows[0])
    for row in rows:
        row["红框合格"] = "是"
    with review_path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def render_annotations(
    config_path: Path = DEFAULT_CONFIG,
    source_dir: Path = DEFAULT_SCREENSHOTS,
    output_dir: Path = DEFAULT_OUTPUT,
    review_path: Path = DEFAULT_REVIEW,
) -> AnnotationReport:
    """校验 object_key 后批量绘制红框图和标注明细。"""

    annotations = load_annotations(config_path)
    review = load_module_review(review_path)
    if set(annotations) != set(review):
        raise ValueError("标注配置与模块核对清单覆盖范围不一致。")
    output_dir.mkdir(parents=True, exist_ok=True)
    manifest_rows: list[list[str]] = []
    for sequence, (module_name, annotation) in enumerate(annotations.items(), start=1):
        row = review[module_name]
        if not row.screenshot_approved:
            raise ValueError(f"模块“{module_name}”的原始截图尚未批准。")
        visible = set(row.visible_objects)
        for region in annotation.regions:
            if region.object_key not in visible:
                raise ValueError(f"模块“{module_name}”的红框对象未在原图中复核。")
        source = source_dir / annotation.source_file
        if not source.exists():
            raise FileNotFoundError(source)
        output_name = f"{sequence:03d}-{_safe_name(module_name)}.png"
        legacy = LegacyAnnotation(
            module=module_name,
            source_file=annotation.source_file,
            regions=tuple(
                LegacyRegion(region.index, region.label, region.box)
                for region in annotation.regions
            ),
        )
        render_annotated_image(source, output_dir / output_name, legacy)
        for region in annotation.regions:
            manifest_rows.append(
                [module_name, annotation.source_file, output_name, str(region.index), region.label, region.object_key]
            )
    with (output_dir / "annotation-manifest.csv").open(
        "w", encoding="utf-8-sig", newline=""
    ) as stream:
        writer = csv.writer(stream)
        writer.writerow(["模块", "原图", "标注图", "编号", "说明", "可见对象键"])
        writer.writerows(manifest_rows)
    _mark_annotations_approved(review_path)
    return AnnotationReport(len(annotations), len(annotations), len(manifest_rows), ())


def main() -> None:
    """初始化配置或生成全部真实流程标注图。"""

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--initialize", action="store_true", help="重新生成 annotations.json")
    args = parser.parse_args()
    if args.initialize:
        initialize_annotations()
    report = render_annotations()
    print(
        f"MODULES={report.module_count} IMAGES={report.image_count} "
        f"REGIONS={report.region_count} ERRORS={len(report.errors)}"
    )


if __name__ == "__main__":
    main()
