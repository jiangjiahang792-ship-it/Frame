# TDJS-Vision 检测能力演示文稿实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 制作一份约 15 页、可编辑、全简体中文的 PowerPoint，说明 TDJS-Vision 当前可实现的测量、定位、缺陷检测能力，并介绍 YOLO、大模型、无监督检测及运行参数上下限调整。

**Architecture:** 以项目当前代码、`README.md`、`ToolTreeView.xml` 和真实界面截图为事实依据。叙事采用“能力总览—三类任务—四类算法—参数调优—落地路径”，视觉采用 Codex Grid 的白底、黑字、浅灰结构面和单一蓝绿色强调色。

**Tech Stack:** JavaScript ES Modules、`@oai/artifact-tool`、PowerPoint `.pptx`、项目现有 PNG/JPG 截图。

## Global Constraints

- 页面约 15 页，全部使用简体中文。
- 标题字号不小于 35 磅，正文不小于 16 磅。
- 不虚构精度、节拍、收益或现场验证结论。
- 真实界面截图优先，内容以当前项目代码为准。
- 最终逐页渲染检查文字截断、越界和非预期重叠。

---

### Task 1: 内容事实与素材清单

**Files:**
- Read: `README.md`
- Read: `ToolTreeView.xml`
- Read: `Node/3-Detection/TDAI/AIInputInfo.cs`
- Read: `Forms/SolRunParam/MyDataGridViewForm.cs`
- Read: `docs/manual-assets/screenshots/*.png`

- [x] 核对测量、定位、缺陷、YOLO、无监督、大模型和运行参数能力。
- [x] 选择主界面、流程画布、算法参数、训练界面与运行参数截图。

### Task 2: 15 页叙事结构

**Produces:** 15 页页面清单与每页单一结论。

- [x] 采用“封面、总览、闭环、测量、测量示例、定位、缺陷全景、传统视觉、YOLO 两页、无监督、大模型、选型矩阵、参数上下限、收束”结构。

### Task 3: 演示文稿实现

**Files:**
- Create: external scratch `tdjs-vision-detection-capabilities/tmp/build-deck.mjs`
- Create: `outputs/TDJS-Vision视觉检测能力说明.pptx`

- [x] 初始化 artifact-tool 工作区。
- [x] 使用 1280×720 画布构建 15 页可编辑演示文稿。
- [x] 嵌入项目 Logo 与真实界面截图。

### Task 4: 渲染与质量检查

**Files:**
- Create: external scratch `tmp/preview/*.png`
- Create: external scratch `tmp/qa/*.layout.json`

- [x] 导出每页 PNG 与整套联系表。
- [x] 使用 `slides_test.py` 检查越界。
- [x] 逐页查看并修复截断、错位和重叠。

### Task 5: 任务记录与交付

**Files:**
- Modify: `任务记录.md`

- [x] 记录演示文稿页数、内容范围、事实依据、生成位置和检查结果。
- [x] 返回最终 PPTX 的可点击链接。
