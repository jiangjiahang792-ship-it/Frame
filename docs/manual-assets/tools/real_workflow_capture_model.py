"""真实流程演示截图任务与核对清单模型。"""

from __future__ import annotations

import json
import argparse
import csv
from dataclasses import asdict, dataclass
from pathlib import Path


ALLOWED_CAPTURE_KINDS = {"真实流程", "真实参数", "硬件配置", "能力限制"}
FORBIDDEN_HARDWARE_CLAIMS = ("连接成功", "检测成功", "通信成功")
FLOW_MODULES = {
    "主界面与视图管理",
    "方案管理",
    "流程画布",
    "流程管理",
    "运行控制",
    "运行日志",
}
HARDWARE_MODULES = {
    "二维工业相机",
    "三维相机",
    "光源控制器",
    "PLC通信",
    "Modbus通信",
    "TCP通信",
    "串口通信",
    "图像源",
    "打开光源",
    "PLC读",
    "PLC写",
    "PLC软触发",
    "Modbus读",
    "Modbus写",
    "Modbus软触发",
    "TCP请求",
    "TCP响应",
    "串口发送",
    "相机IO",
    "相机IO手动控制",
    "科锐IO模块",
}
LIMITED_3D_MODULES = {"3D图像源", "3D图像显示"}


@dataclass(frozen=True)
class CaptureTask:
    """描述一个说明书模块在截图前必须具备的真实软件状态。"""

    module_name: str
    capture_kind: str
    source_window: str
    preconditions: tuple[str, ...]
    steps: tuple[str, ...]
    hardware_required: bool
    expected_visible_objects: tuple[str, ...]


@dataclass(frozen=True)
class ModuleReview:
    """记录一个模块原始截图、标注和正文的人工复核状态。"""

    module_name: str
    screenshot_file: str
    capture_kind: str
    precondition_state: str
    visible_objects: tuple[str, ...]
    hardware_state: str
    screenshot_approved: bool
    annotation_approved: bool
    text_approved: bool
    review_notes: str


def _flow_task(module_name: str, source_window: str) -> CaptureTask:
    """创建平台与流程模块的真实流程截图任务。"""

    common_preconditions = (
        "已打开客户演示方案",
        "已新建客户演示流程",
        "演示流程包含节点和连接线",
    )
    if module_name == "流程画布":
        steps = (
            "打开客户演示方案",
            "单击新建流程",
            "添加本地图像、图像处理、检测和结果节点",
            "建立节点连接线",
            "调整画布后截图",
        )
        visible = (
            "流程名称",
            "节点工具箱",
            "真实节点",
            "节点连接线",
            "运行控制",
        )
    elif module_name == "流程管理":
        steps = (
            "打开流程编辑器",
            "单击新建流程",
            "确认流程标签和新增删除入口可见",
            "截图",
        )
        visible = (
            "流程标签",
            "新增流程按钮",
            "删除流程按钮",
            "已建立的流程",
        )
    elif module_name == "方案管理":
        steps = (
            "新建客户演示方案",
            "保存演示方案",
            "打开方案管理入口",
            "截图",
        )
        visible = ("方案名称", "新建方案", "打开方案", "保存方案")
    elif module_name == "运行控制":
        steps = (
            "打开已建立流程",
            "确认节点和连接完整",
            "显示启动、循环和停止控制",
            "截图",
        )
        visible = (
            "已建立的流程",
            "启动按钮",
            "循环运行按钮",
            "停止按钮",
        )
    elif module_name == "运行日志":
        steps = (
            "打开客户演示方案",
            "打开已建立流程",
            "显示运行日志区域",
            "截图",
        )
        visible = ("已建立的流程", "日志等级", "日志内容", "日志时间")
    else:
        steps = (
            "打开客户演示方案",
            "打开已建立流程",
            "确认主界面显示流程和结果区域",
            "截图",
        )
        visible = (
            "已打开的方案",
            "已建立的流程",
            "功能工具栏",
            "图像或日志区域",
        )
    return CaptureTask(
        module_name=module_name,
        capture_kind="真实流程",
        source_window=source_window,
        preconditions=common_preconditions,
        steps=steps,
        hardware_required=False,
        expected_visible_objects=visible,
    )


def _hardware_task(module_name: str, source_window: str) -> CaptureTask:
    """创建未连接真实设备时的硬件配置截图任务。"""

    return CaptureTask(
        module_name=module_name,
        capture_kind="硬件配置",
        source_window=source_window,
        preconditions=(
            "已打开真实设备或通信配置窗口",
            "当前环境未连接真实设备",
        ),
        steps=(
            f"打开{module_name}真实配置窗口",
            "检查设备选择、地址、端口或站号等参数",
            "保留未连接状态并截图",
        ),
        hardware_required=True,
        expected_visible_objects=(
            "设备选择或通信参数",
            "连接或测试入口",
            "未连接状态",
        ),
    )


def _limited_3d_task(module_name: str, source_window: str) -> CaptureTask:
    """创建仅说明三维取图与显示能力的截图任务。"""

    return CaptureTask(
        module_name=module_name,
        capture_kind="能力限制",
        source_window=source_window,
        preconditions=(
            "已打开三维取图配置",
            "当前仅展示三维取图与显示配置",
        ),
        steps=(
            f"打开{module_name}真实参数窗口",
            "确认三维取图或显示参数可见",
            "保留未连接状态并截图",
        ),
        hardware_required=True,
        expected_visible_objects=(
            "三维设备选择",
            "取图或显示参数",
            "未连接状态",
        ),
    )


def _parameter_task(module_name: str, source_window: str) -> CaptureTask:
    """创建演示流程中实际节点参数窗体的截图任务。"""

    return CaptureTask(
        module_name=module_name,
        capture_kind="真实参数",
        source_window=source_window,
        preconditions=(
            "已打开客户演示方案",
            "演示流程中已添加对应节点",
            "对应节点具备合理上游或输入",
        ),
        steps=(
            f"在演示流程中添加{module_name}节点",
            "从节点打开真实参数窗口",
            "选择合理上游并填写演示参数",
            "确认关键参数和保存入口可见",
            "截图",
        ),
        hardware_required=False,
        expected_visible_objects=(
            "上游输入选择",
            "关键参数区",
            "确认或保存按钮",
        ),
    )


def build_default_capture_plan(
    existing_annotations: dict[str, object],
) -> dict[str, CaptureTask]:
    """根据原标注模块与截图来源生成真实状态截图计划。"""

    tasks: dict[str, CaptureTask] = {}
    for module_name, raw_item in existing_annotations.items():
        if not isinstance(raw_item, dict):
            raise ValueError(f"模块“{module_name}”的原标注配置无效。")
        source_window = _required_text(
            raw_item.get("source_file"), "source_file", module_name
        )
        if module_name in FLOW_MODULES:
            task = _flow_task(module_name, source_window)
        elif module_name in LIMITED_3D_MODULES:
            task = _limited_3d_task(module_name, source_window)
        elif module_name in HARDWARE_MODULES:
            task = _hardware_task(module_name, source_window)
        else:
            task = _parameter_task(module_name, source_window)
        tasks[module_name] = task
    return tasks


def save_capture_plan(tasks: dict[str, CaptureTask], path: Path) -> None:
    """以稳定的 UTF-8 JSON 格式保存截图任务。"""

    path.parent.mkdir(parents=True, exist_ok=True)
    payload = {module_name: asdict(task) for module_name, task in tasks.items()}
    path.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def _required_text(value: object, field_name: str, module_name: str) -> str:
    """读取必填文本，并在配置缺失时给出明确错误。"""

    text = str(value or "").strip()
    if not text:
        raise ValueError(f"模块“{module_name}”缺少字段“{field_name}”。")
    return text


def _required_text_tuple(
    value: object,
    field_name: str,
    module_name: str,
) -> tuple[str, ...]:
    """读取必填文本数组，过滤空白项。"""

    if not isinstance(value, list):
        raise ValueError(f"模块“{module_name}”的“{field_name}”必须是数组。")
    items = tuple(str(item).strip() for item in value if str(item).strip())
    if not items:
        raise ValueError(f"模块“{module_name}”的“{field_name}”不能为空。")
    return items


def load_capture_plan(path: Path) -> dict[str, CaptureTask]:
    """从 UTF-8 JSON 文件读取全部模块截图任务。"""

    raw = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(raw, dict):
        raise ValueError("截图计划根节点必须是对象。")

    tasks: dict[str, CaptureTask] = {}
    for module_name, item in raw.items():
        if not isinstance(item, dict):
            raise ValueError(f"模块“{module_name}”的配置必须是对象。")
        tasks[module_name] = CaptureTask(
            module_name=module_name,
            capture_kind=_required_text(item.get("capture_kind"), "capture_kind", module_name),
            source_window=_required_text(item.get("source_window"), "source_window", module_name),
            preconditions=_required_text_tuple(
                item.get("preconditions"), "preconditions", module_name
            ),
            steps=_required_text_tuple(item.get("steps"), "steps", module_name),
            hardware_required=bool(item.get("hardware_required", False)),
            expected_visible_objects=_required_text_tuple(
                item.get("expected_visible_objects"),
                "expected_visible_objects",
                module_name,
            ),
        )
    return tasks


def validate_capture_plan(
    tasks: dict[str, CaptureTask],
    expected_modules: set[str],
) -> list[str]:
    """校验模块覆盖、截图分类、真实流程状态和硬件边界。"""

    errors: list[str] = []
    missing = sorted(expected_modules - set(tasks))
    extra = sorted(set(tasks) - expected_modules)
    if missing:
        errors.append("缺少模块：" + "、".join(missing))
    if extra:
        errors.append("多余模块：" + "、".join(extra))

    for module_name, task in tasks.items():
        if task.capture_kind not in ALLOWED_CAPTURE_KINDS:
            errors.append(f"{module_name} 使用了不允许的截图类型：{task.capture_kind}")
        if task.hardware_required:
            combined = " ".join(
                task.preconditions + task.steps + task.expected_visible_objects
            )
            for forbidden in FORBIDDEN_HARDWARE_CLAIMS:
                if forbidden in combined:
                    errors.append(f"{module_name} 不得宣称“{forbidden}”。")

    canvas = tasks.get("流程画布")
    if canvas is not None:
        visible = " ".join(canvas.expected_visible_objects)
        steps = " ".join(canvas.steps)
        if "节点" not in visible:
            errors.append("流程画布必须显示真实节点。")
        if "连接线" not in visible:
            errors.append("流程画布必须显示真实连接线。")
        if "新建流程" not in steps:
            errors.append("流程画布截图前必须执行新建流程。")
    return errors


def _is_approved(value: object) -> bool:
    """把核对清单中的中文或英文肯定值转换为布尔值。"""

    return str(value or "").strip().lower() in {"是", "通过", "true", "yes", "1"}


def load_module_review(path: Path) -> dict[str, ModuleReview]:
    """读取 UTF-8 BOM 兼容的 86 模块核对清单。"""

    rows: dict[str, ModuleReview] = {}
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        for raw in csv.DictReader(stream):
            module_name = str(raw.get("模块") or "").strip()
            if not module_name:
                raise ValueError("核对清单存在未填写模块名的行。")
            visible_objects = tuple(
                item.strip()
                for item in str(raw.get("可见对象") or "").split("｜")
                if item.strip()
            )
            rows[module_name] = ModuleReview(
                module_name=module_name,
                screenshot_file=str(raw.get("截图文件") or "").strip(),
                capture_kind=str(raw.get("截图类型") or "").strip(),
                precondition_state=str(raw.get("前置状态") or "").strip(),
                visible_objects=visible_objects,
                hardware_state=str(raw.get("硬件状态") or "").strip(),
                screenshot_approved=_is_approved(raw.get("截图合格")),
                annotation_approved=_is_approved(raw.get("红框合格")),
                text_approved=_is_approved(raw.get("文字合格")),
                review_notes=str(raw.get("复核备注") or "").strip(),
            )
    return rows


def verify_module_review(
    rows: dict[str, ModuleReview], expected_modules: set[str]
) -> list[str]:
    """校验核对清单覆盖范围和进入标注阶段所需的截图状态。"""

    errors: list[str] = []
    missing = sorted(expected_modules - set(rows))
    extra = sorted(set(rows) - expected_modules)
    if missing:
        errors.append("核对清单缺少模块：" + "、".join(missing))
    if extra:
        errors.append("核对清单存在多余模块：" + "、".join(extra))
    for module_name, row in rows.items():
        if not row.screenshot_file:
            errors.append(f"{module_name} 未填写截图文件。")
        if not row.visible_objects:
            errors.append(f"{module_name} 未填写可见对象。")
        if not row.screenshot_approved:
            errors.append(f"{module_name} 的原始截图尚未复核通过。")
        if row.capture_kind in {"硬件配置", "能力限制"} and "未连接" not in row.hardware_state:
            errors.append(f"{module_name} 的硬件状态必须明确为未连接。")
    return errors


def _main() -> int:
    """提供命令行核对入口，供说明书制作流程调用。"""

    parser = argparse.ArgumentParser(description="校验真实流程截图核对清单。")
    parser.add_argument("--verify-review", type=Path, help="需要校验的 module-review.csv")
    parser.add_argument(
        "--capture-plan",
        type=Path,
        default=Path(__file__).resolve().parents[1] / "real-workflow" / "capture-plan.json",
        help="86 模块截图计划。",
    )
    args = parser.parse_args()
    if args.verify_review is None:
        parser.error("必须指定 --verify-review。")
    tasks = load_capture_plan(args.capture_plan)
    rows = load_module_review(args.verify_review)
    errors = verify_module_review(rows, set(tasks))
    approved = sum(1 for row in rows.values() if row.screenshot_approved)
    print(f"MODULES={len(rows)} SCREENSHOT_APPROVED={approved} ERRORS={len(errors)}")
    for error in errors:
        print("ERROR=" + error)
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(_main())
