"""Build the screenshot manifest used by the illustrated customer manual."""

from __future__ import annotations

import argparse
import csv
import importlib.util
import json
from collections import defaultdict
from pathlib import Path


def load_module(path: Path):
    """Load a Python module from an explicit path."""

    spec = importlib.util.spec_from_file_location("illustrated_manual", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load module: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main() -> None:
    """Create a UTF-8 CSV that maps screenshots to manual modules."""

    parser = argparse.ArgumentParser()
    parser.add_argument("screenshots", type=Path)
    parser.add_argument("capture_json", type=Path)
    parser.add_argument("manual_builder", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()

    builder = load_module(args.manual_builder)
    module_categories: dict[str, str] = {}
    for module in builder.PLATFORM:
        module_categories[module["name"]] = "平台基础"
    for module in builder.DEVICES:
        module_categories[module["name"]] = "设备能力"
    for category, modules in builder.CATEGORIES:
        for module in modules:
            module_categories[module["name"]] = category

    modules_by_file: dict[str, list[str]] = defaultdict(list)
    for module_name, file_name in builder.SCREENSHOT_BY_MODULE.items():
        modules_by_file[file_name].append(module_name)

    capture_items = json.loads(args.capture_json.read_text(encoding="utf-8"))
    capture_by_file = {item["file"]: item for item in capture_items}

    rows: list[list[str]] = []
    for screenshot in sorted(args.screenshots.glob("*.png")):
        file_name = screenshot.name
        module_names = modules_by_file.get(file_name, [])
        categories = sorted({module_categories.get(name, "实际窗体") for name in module_names})
        capture_item = capture_by_file.get(file_name)
        title = capture_item.get("title") if capture_item else ""
        source = "Release 真实窗体加载" if capture_item else "Release 主程序运行"
        rows.append([
            file_name[:3],
            "、".join(categories) if categories else "实际窗体",
            "、".join(module_names) if module_names else "补充界面",
            title or "",
            file_name,
            "已捕获",
            source,
        ])

    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.writer(stream)
        writer.writerow(["编号", "分类", "模块", "窗口标题", "文件名", "状态", "备注"])
        writer.writerows(rows)

    print(f"rows={len(rows)} output={args.output}")


if __name__ == "__main__":
    main()
