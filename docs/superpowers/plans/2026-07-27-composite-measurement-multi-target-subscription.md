# 复合测量工具多目标订阅统一实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让线到线、点到点和点到区域在订阅模式下统一按目标编号配对，并支持公共单结果广播。

**Architecture:** 继续使用现有 `MultiTargetMeasurementPairer` 作为唯一配对策略。三个窗体分别把上游明细转换成带编号的几何值，配对后由串行 `MultiTargetMeasurementRunner` 生成结果；公共读取器补齐多目标明细中的线点集和区域读取能力。

**Tech Stack:** C#、.NET Framework、WinForms、PowerShell行为检查、MSBuild。

## Global Constraints

- 所有用户可见文本和代码注释使用简体中文。
- 不修改WinForms布局和Designer文件。
- 多目标继续串行执行，不增加线程池任务。
- 旧方案的单摘要订阅保持兼容。

---

### Task 1: 建立统一订阅回归检查

**Files:**
- Create: `Tests/CompositeMeasurementMultiTargetSubscription.Tests.ps1`

- [x] 编写三个工具必须读取多目标明细、调用公共配对器和执行订阅多目标方法的失败检查。
- [x] 增加线点集与区域支持单目标明细对象读取的失败检查。
- [x] 运行专项检查，确认在旧实现上按预期失败。

### Task 2: 扩展公共几何读取入口

**Files:**
- Modify: `Node/4-Measurement/Common/GeometryMeasurement.cs`

- [x] 为线点集读取增加 `object` 重载，原 `INodeResult` 入口转发到新入口。
- [x] 为区域读取增加 `object` 重载，原 `INodeResult` 入口转发到新入口。
- [x] 运行专项检查确认公共读取契约通过。

### Task 3: 三个复合测量工具接入公共配对器

**Files:**
- Modify: `Node/4-Measurement/LineLineAngle/NodeParamFormLineLineAngle.cs`
- Modify: `Node/4-Measurement/PointPointDistance/NodeParamFormPointPointDistance.cs`
- Modify: `Node/4-Measurement/PointRegionDistance/NodeParamFormPointRegionDistance.cs`

- [x] 线到线一次读取两侧全部目标直线及边缘点，按编号配对后串行计算。
- [x] 点到点一次读取两侧全部目标点，按编号配对后串行计算。
- [x] 点到区域一次读取全部目标点和区域，按编号配对后串行计算。
- [x] 三个工具均沿用上游修正上下文或按目标编号创建恒等修正，保证输出编号稳定。
- [x] 运行专项检查并确认通过。

### Task 4: 回归、编译与任务记录

**Files:**
- Modify: `任务记录.md`

- [x] 运行复合测量、多目标测量、订阅兼容相关回归检查。
- [x] 使用Visual Studio 2022 MSBuild完整编译解决方案。
- [x] 把规则、实现范围、红绿验证和编译结果追加到任务记录。
