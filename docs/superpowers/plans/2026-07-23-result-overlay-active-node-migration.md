# ROI结果绘制实际入口迁移 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax跟踪。

**Goal:** 将文本与自动ROI简化能力迁移到现场实际使用的 ResultOverlayDraw 节点，同时保持旧方案兼容。

**Architecture:** 原位扩展旧参数和构建器，新ROI复用 ResultOverlayDraw2 命名空间中的共享几何适配器；旧枚举和旧专用绘制分支继续保留。参数窗体重做为两类型界面。

**Tech Stack:** C#、.NET Framework 4.8、WinForms、Newtonsoft.Json、OpenCvSharp、PowerShell检查、MSBuild Debug|x64。

**实施状态：** 已完成。实际 ResultOverlayDraw 入口、旧方案兼容、手动调参依赖、运行构建器和真实窗体均已验证。

## Global Constraints

- 不改变 NodeType.ResultOverlayDraw、节点结果类型及原有输出名称。
- 所有WinForms控件必须位于Designer文件。
- 所有界面文字和新增注释使用简体中文。
- 旧Line、Rectangle、Region和每项颜色字段继续兼容。
- 不执行Git操作。

### Task 1: 实际入口失败测试

**Files:**
- Create: Tests/ResultOverlayDrawActiveToolSimplification.Tests.ps1
- Inspect: Node/7-ResultProcessing/ResultOverlayDraw/*

- [ ] 断言旧参数新增 JudgeText1、JudgeText2、HasNewJudgeSubscription 和 Roi。
- [ ] 断言实际Designer包含“添加ROI”，不再包含加线、加矩形、加区域和类型下拉框。
- [ ] 断言实际构建器调用 OverlayGeometryAdapterRegistry.TryAppend。
- [ ] 运行测试并确认因当前旧界面失败。

### Task 2: 参数与运行构建器

**Files:**
- Modify: Node/7-ResultProcessing/ResultOverlayDraw/NodeParamResultOverlayDraw.cs
- Modify: Node/7-ResultProcessing/ResultOverlayDraw/NodeResultOverlayDraw.cs

- [ ] 增加新版单布尔订阅、全局OK/NG颜色和Roi枚举，保留旧字段。
- [ ] 依赖收集加入JudgeText1。
- [ ] 构建开始时严格读取bool判定。
- [ ] Roi分支只读取一次值并调用共享适配器。
- [ ] 新ROI空AlgorithmResult不误报未找到；普通无法识别值保留提示。
- [ ] 运行专项测试确认构建器断言通过。

### Task 3: 实际参数窗体

**Files:**
- Modify: Node/7-ResultProcessing/ResultOverlayDraw/NodeParamFormResultOverlayDraw.cs
- Modify: Node/7-ResultProcessing/ResultOverlayDraw/NodeParamFormResultOverlayDraw.Designer.cs

- [ ] Designer重建为基础设置、三按钮、三列表列、当前项和右侧预览。
- [ ] 图像订阅声明OutputImage契约，判定订阅声明bool契约。
- [ ] 文本与ROI分别声明明确输入类别。
- [ ] 旧几何项显示“ROI（旧配置）”，保存时不改其枚举和旧样式字段。
- [ ] 文本位置使用简体中文选项。
- [ ] 运行专项测试确认界面断言通过。

### Task 4: 依赖、兼容与验证

**Files:**
- Modify: Forms/ImageViewer/FrmSingleImage.cs
- Modify: 任务记录.md

- [ ] 手动调参过滤加入旧节点新版JudgeText1来源。
- [ ] 对新旧JSON参数执行序列化往返。
- [ ] 运行实际入口、共享适配器、订阅契约和位置修正回归。
- [ ] 完整编译Debug|x64。
- [ ] 实际打开“ROI结果绘制”窗体并截图检查空白和新增ROI状态。
