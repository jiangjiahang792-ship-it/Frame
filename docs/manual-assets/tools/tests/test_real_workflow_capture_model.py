"""真实流程截图计划模型测试。"""

from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path


TOOLS_DIR = Path(__file__).resolve().parents[1]
ROOT = Path(__file__).resolve().parents[4]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from real_workflow_capture_model import (
    build_default_capture_plan,
    load_capture_plan,
    load_module_review,
    save_capture_plan,
    validate_capture_plan,
    verify_module_review,
)


OLD_ANNOTATIONS = ROOT / "docs" / "manual-assets" / "annotations" / "red-box-annotations.json"
CAPTURE_PLAN = ROOT / "docs" / "manual-assets" / "real-workflow" / "capture-plan.json"
MODULE_REVIEW = ROOT / "docs" / "manual-assets" / "real-workflow" / "module-review.csv"


class RealWorkflowCapturePlanTests(unittest.TestCase):
    """验证截图计划覆盖范围和真实状态约束。"""

    def test_capture_plan_covers_all_manual_modules(self) -> None:
        """新计划必须完整覆盖原说明书中的 86 个模块。"""

        expected = set(json.loads(OLD_ANNOTATIONS.read_text(encoding="utf-8")))
        tasks = load_capture_plan(CAPTURE_PLAN)
        self.assertEqual(86, len(expected))
        self.assertEqual(expected, set(tasks))

    def test_default_plan_round_trip_preserves_all_tasks(self) -> None:
        """默认计划生成后必须能够无损保存并重新读取。"""

        source = json.loads(OLD_ANNOTATIONS.read_text(encoding="utf-8"))
        generated = build_default_capture_plan(source)
        self.assertEqual(86, len(generated))
        save_capture_plan(generated, CAPTURE_PLAN)
        loaded = load_capture_plan(CAPTURE_PLAN)
        self.assertEqual(generated, loaded)

    def test_canvas_capture_requires_nodes_connections_and_new_process(self) -> None:
        """流程画布必须来自新建流程后的真实节点连接状态。"""

        task = load_capture_plan(CAPTURE_PLAN)["流程画布"]
        visible = " ".join(task.expected_visible_objects)
        steps = " ".join(task.steps)
        self.assertIn("节点", visible)
        self.assertIn("连接线", visible)
        self.assertIn("新建流程", steps)

    def test_hardware_capture_cannot_claim_success(self) -> None:
        """未接硬件的截图计划不得宣称连接、检测或通信成功。"""

        forbidden = ("连接成功", "检测成功", "通信成功")
        for task in load_capture_plan(CAPTURE_PLAN).values():
            if not task.hardware_required:
                continue
            combined = " ".join(
                task.preconditions + task.steps + task.expected_visible_objects
            )
            for text in forbidden:
                self.assertNotIn(text, combined, task.module_name)

    def test_capture_plan_has_no_validation_errors(self) -> None:
        """所有截图任务必须填写完整且使用允许的状态分类。"""

        expected = set(json.loads(OLD_ANNOTATIONS.read_text(encoding="utf-8")))
        errors = validate_capture_plan(load_capture_plan(CAPTURE_PLAN), expected)
        self.assertEqual([], errors)

    def test_module_review_approves_all_86_original_screenshots(self) -> None:
        """逐模块复核清单必须覆盖 86 项并批准全部原始截图。"""

        rows = load_module_review(MODULE_REVIEW)
        self.assertEqual(86, len(rows))
        self.assertEqual([], verify_module_review(rows, set(load_capture_plan(CAPTURE_PLAN))))
        self.assertTrue(all(row.screenshot_approved for row in rows.values()))


if __name__ == "__main__":
    unittest.main()
