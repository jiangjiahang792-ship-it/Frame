"""真实流程演示版 Word 装配测试。"""

from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path


TOOLS_DIR = Path(__file__).resolve().parents[1]
ROOT = Path(__file__).resolve().parents[4]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from build_real_workflow_manual import build_real_workflow_manual


SOURCE = ROOT / "outputs" / "TDJS-Vision工业视觉应用框架功能与应用说明书-图文增强版.docx"
ANNOTATIONS = ROOT / "docs" / "manual-assets" / "real-workflow" / "annotations.json"
REVIEW = ROOT / "docs" / "manual-assets" / "real-workflow" / "module-review.csv"


class RealWorkflowManualTests(unittest.TestCase):
    """验证 86 个模块均被新图、新图注和编号说明一一替换。"""

    def test_replaces_each_module_with_approved_image_and_matching_legend(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "真实流程演示版.docx"
            report = build_real_workflow_manual(
                SOURCE, output, ANNOTATIONS, REVIEW
            )
            self.assertEqual(86, report.modules_replaced)
            self.assertEqual(86, report.captions_written)
            self.assertEqual(86, report.legends_written)
            self.assertEqual((), report.errors)
            self.assertTrue(report.source_unchanged)
            self.assertTrue(output.exists())


if __name__ == "__main__":
    unittest.main()
