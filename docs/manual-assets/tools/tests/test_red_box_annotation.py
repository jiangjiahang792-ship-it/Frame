"""红框标注配置和图片绘制测试。"""

from __future__ import annotations

import json
import csv
import sys
import tempfile
import unittest
from pathlib import Path

TOOLS_DIR = Path(__file__).resolve().parents[1]
ROOT = Path(__file__).resolve().parents[4]
CONFIG = ROOT / "docs" / "manual-assets" / "annotations" / "red-box-annotations.json"
MANIFEST = ROOT / "docs" / "manual-assets" / "screenshots" / "screenshot-manifest.csv"
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from red_box_annotation import load_annotation_config


def write_config(tmp_path: Path, regions: list[dict[str, object]]) -> Path:
    """写入只包含一个模块的测试配置。"""

    path = tmp_path / "annotations.json"
    payload = {
        "测试模块": {
            "source_file": "sample.png",
            "regions": regions,
        }
    }
    path.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")
    return path


class AnnotationConfigTests(unittest.TestCase):
    """验证标注配置的结构约束。"""

    def test_rejects_non_contiguous_numbers(self) -> None:
        """编号不连续时必须拒绝配置。"""

        with tempfile.TemporaryDirectory() as directory:
            path = write_config(
                Path(directory),
                regions=[
                    {"index": 1, "label": "输入区域", "box": [0.1, 0.1, 0.4, 0.3]},
                    {"index": 3, "label": "执行按钮", "box": [0.7, 0.8, 0.9, 0.95]},
                ],
            )

            with self.assertRaisesRegex(ValueError, "编号必须连续"):
                load_annotation_config(path)

    def test_rejects_out_of_bounds_box(self) -> None:
        """矩形坐标超出归一化范围时必须拒绝配置。"""

        with tempfile.TemporaryDirectory() as directory:
            path = write_config(
                Path(directory),
                regions=[
                    {"index": 1, "label": "输入区域", "box": [-0.1, 0.1, 0.4, 0.3]},
                ],
            )

            with self.assertRaisesRegex(ValueError, "坐标必须位于 0.0 至 1.0"):
                load_annotation_config(path)

    def test_rejects_more_than_four_regions(self) -> None:
        """单个模块超过四个标注区域时必须拒绝配置。"""

        with tempfile.TemporaryDirectory() as directory:
            regions = [
                {
                    "index": index,
                    "label": f"区域{index}",
                    "box": [0.1, 0.1, 0.4, 0.3],
                }
                for index in range(1, 6)
            ]
            path = write_config(Path(directory), regions=regions)

            with self.assertRaisesRegex(ValueError, "标注区域数量必须为 1 至 4"):
                load_annotation_config(path)

    def test_rejects_reversed_box(self) -> None:
        """矩形右边界不大于左边界时必须拒绝配置。"""

        with tempfile.TemporaryDirectory() as directory:
            path = write_config(
                Path(directory),
                regions=[
                    {"index": 1, "label": "错误区域", "box": [0.6, 0.1, 0.4, 0.3]},
                ],
            )

            with self.assertRaisesRegex(ValueError, "矩形边界顺序无效"):
                load_annotation_config(path)

    def test_all_manual_modules_have_annotations(self) -> None:
        """截图清单中的 86 个说明书模块必须全部具备独立标注。"""

        with MANIFEST.open("r", encoding="utf-8-sig", newline="") as stream:
            rows = list(csv.DictReader(stream))
        expected_modules = {
            module_name
            for row in rows
            for module_name in row["模块"].split("、")
            if module_name and module_name != "补充界面"
        }

        config = load_annotation_config(CONFIG)

        self.assertEqual(86, len(expected_modules))
        self.assertEqual(expected_modules, set(config))
        self.assertTrue(all(1 <= len(item.regions) <= 4 for item in config.values()))


if __name__ == "__main__":
    unittest.main()
