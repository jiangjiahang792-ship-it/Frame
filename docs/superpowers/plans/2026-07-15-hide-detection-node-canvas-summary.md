# Hide Detection Node Canvas Summary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 隐藏无监督检测与大模型调用节点的画布参数摘要，并让节点恢复默认高度。

**Architecture:** 两个节点不再重写 `GetCanvasParameterSummary()`，从而继承基类的空摘要。画布通用摘要绘制和高度策略保持不变，变更只影响指定的两个节点类型。

**Tech Stack:** C#、.NET Framework 4.8、WinForms、PowerShell 回归检查

## Global Constraints

- 所有中文内容使用简体中文。
- 不修改参数窗体、运行参数、推理逻辑和画布通用绘制能力。
- 保留工作区中已有的用户改动，只修改本任务直接涉及的代码和断言。
- 使用测试先行方式验证行为变化。

---

### Task 1: 隐藏两个检测节点的画布参数摘要

**Files:**
- Modify: `Tests/UnsupervisedDetectionNode.Tests.ps1`
- Modify: `Tests/LargeModelIntegration.Tests.ps1`
- Modify: `Node/3-Detection/Unsupervised/NodeUnsupervisedDetection.cs`
- Modify: `Node/3-Detection/LargeModel/NodeLargeModelDetection.cs`
- Modify: `FLOW_CANVAS_B_PLAN_TASKS.md`

**Interfaces:**
- Consumes: `NodeBase.GetCanvasParameterSummary()` 返回空字符串的默认实现。
- Produces: 两个检测节点不再提供非空摘要，`ProcessFlowCanvas.GetNodeSize()` 因此使用节点默认高度。

- [x] **Step 1: 修改专项检查以表达新行为**

```powershell
Assert-NotContains $node "public override string GetCanvasParameterSummary()" "节点不应在画布显示运行参数摘要。"
```

- [x] **Step 2: 运行专项检查并确认按预期失败**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests\UnsupervisedDetectionNode.Tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests\LargeModelIntegration.Tests.ps1
```

Expected: 两项检查都因为节点仍包含 `GetCanvasParameterSummary()` 重写而失败。

- [x] **Step 3: 删除两个节点的摘要重写**

从 `NodeUnsupervisedDetection` 和 `NodeLargeModelDetection` 中删除完整的 `GetCanvasParameterSummary()` 方法，保留其余运行逻辑不变。

- [x] **Step 4: 记录任务变更**

在 `FLOW_CANVAS_B_PLAN_TASKS.md` 追加已完成记录，说明两个检测节点不再显示画布参数摘要并恢复默认高度。

- [x] **Step 5: 运行专项检查并确认通过**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests\UnsupervisedDetectionNode.Tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests\LargeModelIntegration.Tests.ps1
```

Expected: 两项检查均输出通过信息并返回退出码 0。

- [x] **Step 6: 编译完整解决方案**

Run:

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "D:\MyCode\PublicWook\TDJS-Vision\TDJS-Vision.sln" /t:Build /p:RestorePackages=false /m
```

Expected: 编译成功，0 个错误。
