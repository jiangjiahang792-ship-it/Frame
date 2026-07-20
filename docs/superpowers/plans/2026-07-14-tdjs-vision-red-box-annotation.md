# TDJS-Vision 红框标注版说明书实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在保留当前图文增强版 Word 和原始截图的前提下，为 86 个模块截图增加与正文对应的红框、编号和图下注释，生成独立的红框标注版 Word。

**Architecture:** 标注配置以模块名称为键，保存原图文件名、归一化矩形坐标和编号说明。图片渲染器只负责生成带红框和编号的 PNG；Word 装配器按二级标题识别模块图片，为每个图片实例建立独立图片关系，并在原图注后追加编号说明，因此同一原图可在不同模块使用不同标注。

**Tech Stack:** Python 3、Pillow、python-docx、OOXML、Microsoft Word PDF 导出、Poppler 页面渲染、PowerShell 验证脚本。

## Global Constraints

- 所有中文内容使用简体中文和 UTF-8 编码。
- 保留 `outputs/TDJS-Vision工业视觉应用框架功能与应用说明书-图文增强版.docx`，不得覆盖。
- 新输出为 `outputs/TDJS-Vision工业视觉应用框架功能与应用说明书-红框标注版.docx`。
- 原始截图目录 `docs/manual-assets/screenshots/` 保持不变。
- 标注图片写入 `docs/manual-assets/annotated-screenshots/`。
- 每张图片标注 1～4 个正文相关区域；红框编号与图下注释严格一一对应。
- 三维模块只标注取图与显示相关区域，不加入三维测量或缺陷算法内容。
- 不修改业务源代码、WinForms 设计器、Release 程序和现有测试。

---

### Task 1: 建立标注模型和配置验证

**Files:**
- Create: `docs/manual-assets/tools/red_box_annotation.py`
- Create: `docs/manual-assets/tools/tests/test_red_box_annotation.py`
- Create: `docs/manual-assets/annotations/red-box-annotations.json`

**Interfaces:**
- Produces: `AnnotationRegion(index: int, label: str, box: tuple[float, float, float, float])`
- Produces: `ModuleAnnotation(module: str, source_file: str, regions: tuple[AnnotationRegion, ...])`
- Produces: `load_annotation_config(path: Path) -> dict[str, ModuleAnnotation]`
- Validation: 坐标范围为 `0.0～1.0`，矩形满足 `left < right`、`top < bottom`，编号从 1 连续递增，每模块 1～4 个区域。

- [ ] **Step 1: 编写配置验证失败测试**

```python
def test_rejects_non_contiguous_numbers(tmp_path: Path) -> None:
    path = write_config(tmp_path, regions=[
        {"index": 1, "label": "输入区域", "box": [0.1, 0.1, 0.4, 0.3]},
        {"index": 3, "label": "执行按钮", "box": [0.7, 0.8, 0.9, 0.95]},
    ])
    with pytest.raises(ValueError, match="编号必须连续"):
        load_annotation_config(path)
```

- [ ] **Step 2: 运行测试并确认失败**

Run:

```powershell
& $BundledPython -m pytest docs/manual-assets/tools/tests/test_red_box_annotation.py -v
```

Expected: FAIL，提示 `load_annotation_config` 尚未定义。

- [ ] **Step 3: 实现不可变标注模型和配置验证**

```python
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
```

- [ ] **Step 4: 建立 86 个模块的配置骨架并验证完整性**

配置键必须与图文说明书中的 86 个模块名称完全一致；每个条目的 `source_file` 使用 `screenshot-manifest.csv` 中的现有 PNG 文件名。共享原图的模块仍建立独立配置，例如“流程画布”和“运行控制”分别配置不同矩形。

- [ ] **Step 5: 运行配置测试**

Expected: 全部测试通过；输出 `MODULES=86 INVALID=0`。

- [ ] **Step 6: 提交标注模型与配置结构**

```powershell
git add docs/manual-assets/tools/red_box_annotation.py docs/manual-assets/tools/tests/test_red_box_annotation.py docs/manual-assets/annotations/red-box-annotations.json
git commit -m "docs: 建立说明书红框标注模型"
```

---

### Task 2: 实现确定性的红框和编号绘制

**Files:**
- Modify: `docs/manual-assets/tools/red_box_annotation.py`
- Modify: `docs/manual-assets/tools/tests/test_red_box_annotation.py`

**Interfaces:**
- Consumes: `ModuleAnnotation`
- Produces: `render_annotated_image(source: Path, output: Path, annotation: ModuleAnnotation) -> None`
- Drawing rules: `#E60012` 红框、白字红底编号圆、线宽 `max(4, round(min(width, height) * 0.006))`。

- [ ] **Step 1: 编写像素级失败测试**

```python
def test_draws_red_box_without_changing_source(tmp_path: Path) -> None:
    source = create_white_image(tmp_path / "source.png", 1000, 600)
    before = source.read_bytes()
    annotation = module_annotation(box=(0.1, 0.2, 0.5, 0.7))
    output = tmp_path / "annotated.png"
    render_annotated_image(source, output, annotation)
    assert source.read_bytes() == before
    with Image.open(output) as image:
        assert image.getpixel((100, 120))[:3] == (230, 0, 18)
```

- [ ] **Step 2: 运行测试并确认失败**

Expected: FAIL，提示 `render_annotated_image` 尚未定义。

- [ ] **Step 3: 实现坐标换算、红框和编号圆绘制**

绘制时先把归一化坐标换算为像素坐标；编号圆放在红框左上角内侧，若空间不足则放在框外上方并限制在图像边界内。使用支持中文的微软雅黑字体；编号只绘制阿拉伯数字，避免字体缺失。

- [ ] **Step 4: 增加越界、极小矩形和重复输出测试**

Expected: 越界配置被拒绝；相同输入重复生成的 PNG 像素哈希一致。

- [ ] **Step 5: 提交绘制器**

```powershell
git add docs/manual-assets/tools/red_box_annotation.py docs/manual-assets/tools/tests/test_red_box_annotation.py
git commit -m "docs: 实现截图红框编号绘制"
```

---

### Task 3: 完成 86 个模块的正文对应标注

**Files:**
- Modify: `docs/manual-assets/annotations/red-box-annotations.json`
- Create: `docs/manual-assets/tools/build_annotated_screenshots.py`
- Modify: `docs/manual-assets/tools/tests/test_red_box_annotation.py`

**Interfaces:**
- Consumes: 98 张原始截图、86 个模块标注配置。
- Produces: `docs/manual-assets/annotated-screenshots/<模块安全文件名>.png`
- Produces: `docs/manual-assets/annotated-screenshots/annotation-manifest.csv`

- [ ] **Step 1: 为每个模块测量并填写归一化矩形**

按照设计规范逐图填写：平台和设备界面框配置区及操作入口；算法节点框输入、参数、ROI/预览和执行；逻辑节点框条件及保存；结果节点框订阅、判定、路径和保存。每个矩形的 `label` 使用客户可理解的简短说明，不直接使用内部控件变量名。

- [ ] **Step 2: 编写覆盖率失败测试**

```python
def test_all_manual_modules_have_annotations() -> None:
    config = load_annotation_config(CONFIG)
    assert set(config) == set(EXPECTED_86_MODULES)
    assert all(1 <= len(item.regions) <= 4 for item in config.values())
```

- [ ] **Step 3: 实现批量生成和清单输出**

`annotation-manifest.csv` 字段为：模块、原图、标注图、编号、说明、归一化矩形。生成器必须只写标注目录，不覆盖原图。

- [ ] **Step 4: 生成 86 张模块标注图并制作接触表检查**

Run:

```powershell
& $BundledPython docs/manual-assets/tools/build_annotated_screenshots.py
```

Expected: `MODULES=86 IMAGES=86 ERRORS=0`。

- [ ] **Step 5: 修正遮挡、框选偏移和说明不一致问题**

逐张检查标注图，确保编号圆不盖住控件文字，红框与介绍区域一致。

- [ ] **Step 6: 提交完整标注配置和图片生成器**

```powershell
git add docs/manual-assets/annotations docs/manual-assets/tools/build_annotated_screenshots.py docs/manual-assets/tools/tests/test_red_box_annotation.py
git commit -m "docs: 完成功能模块截图标注配置"
```

---

### Task 4: 将标注图按模块实例写入 Word

**Files:**
- Create: `docs/manual-assets/tools/build_red_box_manual.py`
- Create: `docs/manual-assets/tools/tests/test_red_box_manual.py`

**Interfaces:**
- Consumes: 当前图文增强版 Word、86 张标注图、标注配置。
- Produces: `outputs/TDJS-Vision工业视觉应用框架功能与应用说明书-红框标注版.docx`
- Produces: `build_red_box_manual(source_docx: Path, output_docx: Path, config: dict[str, ModuleAnnotation]) -> BuildReport`

- [ ] **Step 1: 编写最小 DOCX 失败测试**

测试文档包含两个二级标题，两个模块复用同一原图但配置不同；构建后必须产生两个不同的图片关系，并在各自图注中写入正确编号说明。

- [ ] **Step 2: 运行测试并确认失败**

Expected: FAIL，提示 `build_red_box_manual` 尚未定义。

- [ ] **Step 3: 实现按二级标题追踪模块和替换图片关系**

使用 `python-docx` 遍历正文 XML：遇到 `Heading 2` 时提取去掉章节号后的模块名；遇到该模块的第一张图片时，用对应标注 PNG 建立新的图片 Part 和关系，只替换当前 `a:blip` 的 `r:embed`，不得全局替换共享媒体。

- [ ] **Step 4: 在原图注后追加编号说明**

图注格式：`标注说明：① 输入图像　② 参数设置　③ 绘制并确认 ROI　④ 执行或保存`。若重新运行构建器，先替换已有“标注说明”行，保证幂等。

- [ ] **Step 5: 增加源文件保护和结构验证**

构建前后对源 Word 计算 SHA-256，必须保持一致；输出必须包含 86 个模块标注实例、87 张内嵌图片（含 Logo）和完整标题结构。

- [ ] **Step 6: 生成红框标注版 Word**

Expected: 输出文件存在，`ANNOTATED_MODULES=86 MISSING=0 SOURCE_UNCHANGED=true`。

- [ ] **Step 7: 提交 Word 装配器**

```powershell
git add docs/manual-assets/tools/build_red_box_manual.py docs/manual-assets/tools/tests/test_red_box_manual.py
git commit -m "docs: 生成红框标注版功能说明书"
```

---

### Task 5: 渲染、逐页检查和任务记录

**Files:**
- Modify: `TDJS_VISION_MANUAL_TASKS.md`
- Verify: `outputs/TDJS-Vision工业视觉应用框架功能与应用说明书-红框标注版.docx`

- [ ] **Step 1: 使用 Microsoft Word 更新域并导出临时 PDF**

记录最终页数、字数和输出文件大小。

- [ ] **Step 2: 使用 Poppler 将全部 PDF 页面渲染为 PNG**

Expected: PNG 数量与 PDF 页数完全一致。

- [ ] **Step 3: 制作页面接触表并检查全部页面**

检查红框清晰度、编号与图注一致性、图片比例、分页、页眉页脚、表格和标题；发现问题后修改配置或装配器并重新生成、重新渲染。

- [ ] **Step 4: 执行结构和可访问性审计**

Run: `a11y_audit.py`、`heading_audit.py`、`images_audit.py`、`section_audit.py`。

Expected: 可访问性高、中、低问题均为 0；无缺失图片关系；模块标注覆盖率为 86/86。

- [ ] **Step 5: 更新任务记录**

记录输出文件、标注模块数量、标注区域数量、最终页数、结构审计和逐页检查结果。

- [ ] **Step 6: 最终验证**

运行独立验证脚本，确认源 Word 哈希不变、输出 Word 可打开、86 个模块均包含红框和说明、三维范围仅为取图与显示。

