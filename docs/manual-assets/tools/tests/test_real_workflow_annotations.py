"""真实流程截图红框配置与渲染测试。"""

from __future__ import annotations

import sys
import unittest
from pathlib import Path


TOOLS_DIR = Path(__file__).resolve().parents[1]
ROOT = Path(__file__).resolve().parents[4]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from build_real_workflow_annotations import load_annotations
from real_workflow_capture_model import load_module_review


ANNOTATIONS = ROOT / "docs" / "manual-assets" / "real-workflow" / "annotations.json"
REVIEW = ROOT / "docs" / "manual-assets" / "real-workflow" / "module-review.csv"
SCREENSHOTS = ROOT / "docs" / "manual-assets" / "real-workflow" / "screenshots"


class RealWorkflowAnnotationTests(unittest.TestCase):
    """保证每个红框均引用已复核且真实可见的界面对象。"""

    def test_all_annotations_reference_approved_screenshots(self) -> None:
        review = load_module_review(REVIEW)
        annotations = load_annotations(ANNOTATIONS)
        self.assertEqual(86, len(annotations))
        self.assertEqual(set(review), set(annotations))
        for name, annotation in annotations.items():
            self.assertTrue(review[name].screenshot_approved, name)
            self.assertTrue((SCREENSHOTS / annotation.source_file).exists(), name)

    def test_annotation_labels_are_visible_objects(self) -> None:
        review = load_module_review(REVIEW)
        for name, annotation in load_annotations(ANNOTATIONS).items():
            visible = set(review[name].visible_objects)
            self.assertTrue(1 <= len(annotation.regions) <= 4, name)
            for region in annotation.regions:
                self.assertIn(region.object_key, visible, name)

    def test_boxes_do_not_use_large_blank_regions(self) -> None:
        for name, annotation in load_annotations(ANNOTATIONS).items():
            for region in annotation.regions:
                left, top, right, bottom = region.box
                self.assertLessEqual((right - left) * (bottom - top), 0.45, name)


if __name__ == "__main__":
    unittest.main()
