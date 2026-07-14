# TDJS-Vision 真实流程截图重制实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 创建一份基于真实演示流程和真实参数状态的客户说明书，使全部 86 个模块的截图、红框、编号与文字说明逐项一致。

**Architecture:** 不修改业务源代码，通过独立的 PowerShell/WinForms 截图驱动加载 Release 程序集，构建演示流程、节点和连接关系，再从真实窗体捕获截图。Python 工具负责截图质量审计、红框渲染、模块核对清单和 Word 装配，最后使用 Microsoft Word 与 Poppler 完成全文验收。

**Tech Stack:** C#/.NET Framework 4.8 Release 程序集、PowerShell STA、Windows Forms、Python 3、Pillow、python-docx、Microsoft Word COM、Poppler。

## Global Constraints

- 所有中文内容使用中文简体字。
- 不修改业务源代码、WinForms 设计器和用户现有文件，只修改文档制作工具、截图素材、标注配置、测试和任务记录。
- 无硬件功能必须构造真实演示状态；硬件模块不得伪造连接成功、检测成功或通信成功。
- 三维只覆盖三维相机取图、`3D图像源`和`3D图像显示`。
- 新文件固定输出为 `outputs/TDJS-Vision工业视觉应用框架功能与应用说明书-真实流程演示版.docx`，保留现有版本。
- 当前工作区含用户已有改动和未跟踪素材，实施时严格按路径提交，不清理、不重置无关文件。

## 文件结构

- `docs/manual-assets/tools/real_workflow_capture_model.py`：截图任务和核对状态模型。
- `docs/manual-assets/tools/tests/test_real_workflow_capture_model.py`：86 模块覆盖和硬件边界测试。
- `docs/manual-assets/tools/Capture-RealWorkflowDemo.ps1`：建立真实流程并捕获真实窗体。
- `docs/manual-assets/tools/Test-RealWorkflowCapture.ps1`：检查节点数、连接数和截图结果。
- `docs/manual-assets/real-workflow/capture-plan.json`：86 模块截图计划。
- `docs/manual-assets/real-workflow/screenshots/`：真实状态原始截图。
- `docs/manual-assets/real-workflow/annotations.json`：逐模块红框配置。
- `docs/manual-assets/real-workflow/annotated-screenshots/`：红框成图。
- `docs/manual-assets/real-workflow/module-review.csv`：86 模块核对清单。
- `docs/manual-assets/tools/build_real_workflow_annotations.py`：生成标注图和清单。
- `docs/manual-assets/tools/tests/test_real_workflow_annotations.py`：标注一致性测试。
- `docs/manual-assets/tools/build_real_workflow_manual.py`：装配新 Word。
- `docs/manual-assets/tools/tests/test_real_workflow_manual.py`：Word 一一替换测试。
- `TDJS_VISION_MANUAL_TASKS.md`：任务记录。

---

### Task 1: 建立 86 模块截图状态模型和失败基线

**Files:**
- Create: `docs/manual-assets/tools/real_workflow_capture_model.py`
- Create: `docs/manual-assets/tools/tests/test_real_workflow_capture_model.py`
- Create: `docs/manual-assets/real-workflow/capture-plan.json`

**Interfaces:**
- Produces: `CaptureTask(module_name, capture_kind, source_window, preconditions, steps, hardware_required, expected_visible_objects)`。
- Produces: `load_capture_plan(path: Path) -> dict[str, CaptureTask]`。
- Produces: `validate_capture_plan(tasks, expected_modules) -> list[str]`。

- [ ] **Step 1: 编写失败测试**

```python
def test_capture_plan_covers_all_manual_modules():
    expected = load_manual_modules(SCREENSHOT_MANIFEST)
    assert set(load_capture_plan(CAPTURE_PLAN)) == expected

def test_canvas_capture_requires_nodes_and_connections():
    task = load_capture_plan(CAPTURE_PLAN)["流程画布"]
    assert "节点" in task.expected_visible_objects
    assert "连接线" in task.expected_visible_objects
    assert "新建流程" in " ".join(task.steps)

def test_hardware_capture_cannot_claim_success():
    for task in load_capture_plan(CAPTURE_PLAN).values():
        if task.hardware_required:
            text = " ".join(task.steps + task.expected_visible_objects)
            assert "连接成功" not in text
            assert "检测成功" not in text
```

- [ ] **Step 2: 运行测试并确认因模型或计划不存在而失败**

Run: `python -m unittest docs.manual-assets.tools.tests.test_real_workflow_capture_model -v`

Expected: FAIL，提示导入失败或缺少 `capture-plan.json`。

- [ ] **Step 3: 实现模型与校验器**

```python
@dataclass(frozen=True)
class CaptureTask:
    module_name: str
    capture_kind: str
    source_window: str
    preconditions: tuple[str, ...]
    steps: tuple[str, ...]
    hardware_required: bool
    expected_visible_objects: tuple[str, ...]

ALLOWED_CAPTURE_KINDS = {"真实流程", "真实参数", "硬件配置", "能力限制"}
```

校验模块数、空字段、允许类型、流程画布节点/连接要求和硬件成功状态禁用词。

- [ ] **Step 4: 填写 86 模块截图计划**

每个模块明确窗口、前置条件、操作步骤、硬件限制和应出现对象。`流程画布` 必须包含打开演示方案、单击新增流程、添加节点、建立连接和调整画布。

- [ ] **Step 5: 运行测试并提交**

Run: `python -m unittest docs.manual-assets.tools.tests.test_real_workflow_capture_model -v`

Expected: `OK`，模块数量 86。

Commit: `git commit -m "test: 定义真实流程截图状态规则"`，只提交本任务三个文件。

---

### Task 2: 构建真实演示流程截图驱动

**Files:**
- Create: `docs/manual-assets/tools/Capture-RealWorkflowDemo.ps1`
- Create: `docs/manual-assets/tools/Test-RealWorkflowCapture.ps1`

**Interfaces:**
- Consumes: `capture-plan.json` 和 `bin/x64/Release/机器视觉AI检测系统V1.0.exe`。
- Produces: `real-workflow/screenshots/*.png`。
- Produces: `capture-result.json`，含 `module,file,status,node_count,connection_count,window_title,error`。

- [ ] **Step 1: 编写流程状态失败验证**

```powershell
$canvas = $results | Where-Object module -eq '流程画布'
if ($canvas.node_count -lt 4) { throw '流程画布节点数量不足。' }
if ($canvas.connection_count -lt 3) { throw '流程画布连接数量不足。' }
if (-not (Test-Path -LiteralPath $canvas.file)) { throw '流程画布截图不存在。' }
```

- [ ] **Step 2: 运行验证并确认因结果文件不存在而失败**

Run: `powershell -ExecutionPolicy Bypass -File docs/manual-assets/tools/Test-RealWorkflowCapture.ps1`

Expected: FAIL，提示 `capture-result.json` 不存在。

- [ ] **Step 3: 实现 STA、DPI 感知和 Release 程序集加载**

脚本参数固定为 `ReleaseExe`、`CapturePlan`、`OutputDirectory`、`ResultJson`，设置当前目录为 Release 目录后用 `[Reflection.Assembly]::LoadFrom()` 加载程序集。

- [ ] **Step 4: 执行真实新增流程动作**

显示 `FormNewProcessWizard`，通过反射取得 `buttonAdd` 和 `tabControl1`，调用 `$buttonAdd.PerformClick()`，再从选中页取得 `ProcessEditPanel`。不得直接截取空白窗体。

- [ ] **Step 5: 调用真实节点工厂建立演示节点**

反射调用 `ProcessEditPanel.CreateNode(NodeType,string,int,Point?,bool,bool)`，依次创建：

```text
LocalPicture 本地图像
GrayScale 灰度处理
ImageCrop ROI裁剪
LineFind 直线查找
CircleFind 圆查找
Summarize 结果汇总
ImageShow 图像显示
ImageSave 图片保存
```

节点从左到右分两行排列，名称和状态必须可读。

- [ ] **Step 6: 建立真实连接并刷新画布**

从 `ProcessEditPanel._process` 取得流程，向 `Connections` 添加 `ProcessConnection`：

```text
本地图像 → 灰度处理 → ROI裁剪 → 直线查找 → 结果汇总 → 图像显示
ROI裁剪 → 圆查找 → 结果汇总
图像显示 → 图片保存
```

完成后调用 `NotifyConnectionsChanged()` 并重绘画布。

- [ ] **Step 7: 捕获真实流程与实际节点参数窗体**

输出方案主界面、流程管理、流程画布、运行控制和各节点参数窗体。参数窗体必须使用演示流程中节点自身的 `ParamForm`，等待 `Shown` 初始化完成，并选中合理上游。

- [ ] **Step 8: 捕获硬件窗口并记录边界**

相机、3D 相机、光源、PLC、Modbus、TCP 和串口使用真实 Release 窗体；无硬件时记录 `hardware_state=未连接演示`，不触发写入或伪造成功。

- [ ] **Step 9: 运行截图和状态验证**

```powershell
powershell -STA -ExecutionPolicy Bypass -File docs/manual-assets/tools/Capture-RealWorkflowDemo.ps1 -ReleaseExe bin/x64/Release/机器视觉AI检测系统V1.0.exe -CapturePlan docs/manual-assets/real-workflow/capture-plan.json -OutputDirectory docs/manual-assets/real-workflow/screenshots -ResultJson docs/manual-assets/real-workflow/capture-result.json
powershell -ExecutionPolicy Bypass -File docs/manual-assets/tools/Test-RealWorkflowCapture.ps1
```

Expected: `Captured=86 Failed=0`，流程画布节点不少于 4、连接不少于 3。

- [ ] **Step 10: 提交截图驱动**

Commit: `git commit -m "feat: 构建真实流程演示截图驱动"`，只提交两个脚本。

---

### Task 3: 逐模块检查原始截图并建立核对清单

**Files:**
- Create: `docs/manual-assets/real-workflow/screenshots/`
- Create: `docs/manual-assets/real-workflow/capture-result.json`
- Create: `docs/manual-assets/real-workflow/module-review.csv`

**Interfaces:**
- Produces: CSV 字段 `模块,截图文件,截图类型,前置状态,可见对象,硬件状态,截图合格,红框合格,文字合格,复核备注`。

- [ ] **Step 1: 生成原始截图联系表**

每张联系表 12 图并显示文件名和模块名；联系表只筛查，最终判断回到单张原图。

- [ ] **Step 2: 检查所有流程类截图**

流程画布必须看到流程名、节点名和连接线；空白画布直接退回 Task 2。

- [ ] **Step 3: 检查所有节点参数截图**

确认上游选择、参数区、ROI/预览区和保存按钮与模块一致。窗口未初始化、引用不存在节点或大面积无意义空白时重新捕获。

- [ ] **Step 4: 检查硬件与三维边界**

硬件备注写明“配置演示，未连接真实设备”；三维只保留三项已实现能力。

- [ ] **Step 5: 完成并验证 86 行核对清单**

Run: `python docs/manual-assets/tools/real_workflow_capture_model.py --verify-review docs/manual-assets/real-workflow/module-review.csv`

Expected: `MODULES=86 SCREENSHOT_APPROVED=86 ERRORS=0`。任何模块为“否”时禁止进入标注。

---

### Task 4: 重做红框配置和一致性测试

**Files:**
- Create: `docs/manual-assets/real-workflow/annotations.json`
- Create: `docs/manual-assets/tools/build_real_workflow_annotations.py`
- Create: `docs/manual-assets/tools/tests/test_real_workflow_annotations.py`
- Create: `docs/manual-assets/real-workflow/annotated-screenshots/`

**Interfaces:**
- Produces: `render_annotations(config_path, source_dir, output_dir) -> AnnotationReport`。

- [ ] **Step 1: 编写失败测试**

```python
def test_all_annotations_reference_approved_screenshots():
    review = load_review(REVIEW_CSV)
    annotations = load_annotations(ANNOTATIONS_JSON)
    assert set(annotations) == set(review)
    assert all(review[name].screenshot_approved for name in annotations)

def test_annotation_labels_are_visible_objects():
    review = load_review(REVIEW_CSV)
    for name, annotation in load_annotations(ANNOTATIONS_JSON).items():
        visible = set(review[name].visible_objects)
        assert all(region.object_key in visible for region in annotation.regions)

def test_boxes_do_not_use_large_blank_regions():
    for annotation in load_annotations(ANNOTATIONS_JSON).values():
        for region in annotation.regions:
            assert region.width * region.height <= 0.45
```

- [ ] **Step 2: 运行测试并确认因配置不存在而失败**

Run: `python -m unittest docs.manual-assets.tools.tests.test_real_workflow_annotations -v`

- [ ] **Step 3: 为 86 模块逐张定义 1 至 4 个红框**

每个区域记录 `index,label,object_key,box`。`流程画布` 只框节点工具箱、真实节点与连接线、运行控制，不框整片空白区域。

- [ ] **Step 4: 实现渲染器和可见对象强校验**

复用 `red_box_annotation.py`，增加 `object_key` 必须存在于 CSV 可见对象的校验；成功后更新 `红框合格=是`。

- [ ] **Step 5: 生成并检查全部标注图**

Run: `python docs/manual-assets/tools/build_real_workflow_annotations.py`

Expected: `MODULES=86 IMAGES=86 ERRORS=0`。联系表筛查后，逐张放大检查编号、边界和遮挡。

- [ ] **Step 6: 运行测试并提交**

Run: `python -m unittest docs.manual-assets.tools.tests.test_real_workflow_annotations -v`

Commit: `git commit -m "feat: 重做真实流程截图红框说明"`，只提交配置、构建器和测试。

---

### Task 5: 装配真实流程演示版 Word

**Files:**
- Create: `docs/manual-assets/tools/build_real_workflow_manual.py`
- Create: `docs/manual-assets/tools/tests/test_real_workflow_manual.py`
- Create: `outputs/TDJS-Vision工业视觉应用框架功能与应用说明书-真实流程演示版.docx`

**Interfaces:**
- Produces: `build_real_workflow_manual(source_docx, output_docx, annotations, review_csv) -> BuildReport`。
- `BuildReport` 包含 `modules_replaced,captions_written,legends_written,source_unchanged,errors`。

- [ ] **Step 1: 编写 Word 一一替换失败测试**

```python
def test_replaces_each_module_with_approved_image_and_matching_legend():
    report = build_real_workflow_manual(source, output, annotations, review)
    assert report.modules_replaced == 86
    assert report.captions_written == 86
    assert report.legends_written == 86
    assert report.errors == ()
    assert report.source_unchanged
```

夹具覆盖共享原图、原章节缺图、旧标注说明和硬件限制说明。

- [ ] **Step 2: 运行测试并确认构建器不存在而失败**

Run: `python -m unittest docs.manual-assets.tools.tests.test_real_workflow_manual -v`

- [ ] **Step 3: 实现按 Heading 2 定位和图片替换**

只允许使用 CSV 中 `截图合格=是` 且 `红框合格=是` 的图；缺图时在“使用方法”前插入，禁止静默跳过。

- [ ] **Step 4: 写入匹配图注和限制说明**

```text
图：流程画布真实演示界面
标注说明：① 节点工具箱　② 已建立的流程节点与连接线　③ 运行与停止控制
```

硬件模块追加“配置演示，当前未连接真实设备”；三维模块追加当前能力边界。

- [ ] **Step 5: 局部修正与真实界面不一致的正文**

只修改截图相邻的“使用方法”和“使用建议”，删除不存在的按钮名或伪造状态，保持章节结构与算法逻辑不变。

- [ ] **Step 6: 测试并生成 Word**

```powershell
python -m unittest docs.manual-assets.tools.tests.test_real_workflow_manual -v
python docs/manual-assets/tools/build_real_workflow_manual.py
```

Expected: `MODULES_REPLACED=86 CAPTIONS=86 LEGENDS=86 ERRORS=0 SOURCE_UNCHANGED=true`。

- [ ] **Step 7: 提交 Word 构建工具**

Commit: `git commit -m "feat: 生成真实流程演示版说明书"`，只提交构建器和测试。

---

### Task 6: 全文渲染、审计和最终记录

**Files:**
- Modify: `TDJS_VISION_MANUAL_TASKS.md`
- Verify: `outputs/TDJS-Vision工业视觉应用框架功能与应用说明书-真实流程演示版.docx`

**Interfaces:**
- Produces: 最终验收结果；临时 PDF、PNG 和联系表不作为交付物。

- [ ] **Step 1: 使用 Microsoft Word 更新目录并导出临时 PDF**

只打开目标 Word，更新目录和字段并保存；不得关闭用户已经打开的其他 Word 文档。

- [ ] **Step 2: 渲染全部页面并逐页检查**

使用 Poppler 转换全部页面，页数必须与 Word 统计一致。重点放大流程、测量、缺陷检测、通信和三维章节，确认无空白流程、截图越界、红框遮字、说明错位或内容截断。

- [ ] **Step 3: 运行结构和无障碍审计**

```powershell
$Python='C:\Users\34652\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$DocScripts='C:\Users\34652\.codex\plugins\cache\openai-primary-runtime\documents\26.709.11516\skills\documents\scripts'
$FinalWord='D:\MyCode\PublicWook\TDJS-Vision\outputs\TDJS-Vision工业视觉应用框架功能与应用说明书-真实流程演示版.docx'
& $Python "$DocScripts\a11y_audit.py" $FinalWord
& $Python "$DocScripts\heading_audit.py" $FinalWord
& $Python "$DocScripts\images_audit.py" $FinalWord
& $Python "$DocScripts\section_audit.py" $FinalWord
```

Expected: 无障碍高、中、低问题均为 0；图片、标题和分节与构建报告一致。

- [ ] **Step 4: 运行最终一致性断言**

```text
模块数=86
流程画布节点数>=4
流程画布连接数>=3
红框图=86
图注=86
编号说明=86
模块核对通过=86
源Word未变化=true
```

- [ ] **Step 5: 更新任务记录并运行完整测试**

Run: `python -m unittest discover -s docs/manual-assets/tools/tests -p 'test_*workflow*.py' -v`

Expected: 全部 `OK`，失败数 0。任务记录写明根因、流程结构、截图数量、硬件限制、Word 页数和审计结果。

- [ ] **Step 6: 保留当前分支和工作区状态**

不自动合并、不推送、不清理用户工作区；只报告本任务文件和最终 Word 路径。
