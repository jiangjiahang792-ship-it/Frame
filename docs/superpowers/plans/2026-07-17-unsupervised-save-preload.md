# 无监督节点保存时预加载 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 点击无监督参数窗体“保存”时完成模型解包、加载和预热，成功后原子提交参数，失败时保留旧模型和旧参数。

**Architecture:** 复用大模型节点的配置门模式，让预加载提交与流程推理串行一致。运行时使用候选检测器完成加载和空图预热后再替换旧检测器，模板预览只读取压缩包清单。

**Tech Stack:** C# 7.3、.NET Framework WinForms、OpenCvSharp、PowerShell 5.1 回归测试、Visual Studio 2022 MSBuild。

## Global Constraints

- 所有界面文字和新增注释使用简体中文。
- 不修改 WinForms 设计器布局。
- 保留旧方案首次运行时加载模型的兼容路径。
- 不覆盖工作区内与本任务无关的已有修改。

---

### Task 1: 建立保存预加载回归测试

**Files:**
- Create: `Tests/UnsupervisedSavePreload.Tests.ps1`

**Interfaces:**
- Consumes: 现有无监督参数窗体、节点、运行时和模板包源码。
- Produces: 保存预加载结构与配置门并发行为的自动检查。

- [ ] **Step 1: 编写失败测试**
- [ ] **Step 2: 运行测试，确认因缺少 `ReadManifest`、`PreloadRuntimeAsync` 和 `Preload` 而失败**
- [ ] **Step 3: 保留失败输出作为修复前证据**

### Task 2: 实现轻量模板预览和异步保存

**Files:**
- Modify: `Forms/AiTrainForm/UnsupervisedTemplatePackage.cs`
- Modify: `Node/3-Detection/Unsupervised/ParamFormUnsupervisedDetection.cs`

**Interfaces:**
- Consumes: `UnsupervisedTemplateManifest`、`NodeUnsupervisedDetection.PreloadRuntimeAsync`。
- Produces: `UnsupervisedTemplatePackage.ReadManifest(string)` 和异步保存入口。

- [ ] **Step 1: 增加只读取 `manifest.json` 的公开方法**
- [ ] **Step 2: 将模板信息刷新改为调用轻量清单读取**
- [ ] **Step 3: 将保存改为构建候选参数、等待节点预加载成功后关闭窗体**
- [ ] **Step 4: 增加保存期间按钮禁用和中文加载状态**

### Task 3: 实现运行时预加载和原子切换

**Files:**
- Modify: `Node/3-Detection/Unsupervised/NodeUnsupervisedDetection.cs`
- Modify: `Node/3-Detection/Unsupervised/UnsupervisedDetectionRuntime.cs`

**Interfaces:**
- Consumes: `NodeParamUnsupervisedDetection`、`UnsupervisedAnomalibDetector`。
- Produces: `PreloadRuntimeAsync(NodeParamUnsupervisedDetection)`、`Preload(NodeParamUnsupervisedDetection)`、`UnsupervisedRuntimeConfigurationGate`。

- [ ] **Step 1: 增加节点配置门及后台预加载入口**
- [ ] **Step 2: 让流程参数读取和推理在配置门内执行**
- [ ] **Step 3: 运行时加载候选检测器并用模板尺寸黑图执行一次预热**
- [ ] **Step 4: 预热成功后替换旧检测器，失败时仅释放候选检测器**
- [ ] **Step 5: 增加保存后纯内存快速匹配，保留首次运行兜底**

### Task 4: 验证与记录

**Files:**
- Modify: `任务记录.md`

**Interfaces:**
- Consumes: 本次生产代码和回归测试。
- Produces: 可追溯任务记录及编译验证结果。

- [ ] **Step 1: 运行新增回归测试并确认通过**
- [ ] **Step 2: 运行无监督既有专项测试**
- [ ] **Step 3: 编译 `Debug|x64` 和 `Release|x64`**
- [ ] **Step 4: 更新任务记录中的行为、失败保护和验证结果**
