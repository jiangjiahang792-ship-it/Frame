# 点到线距离多目标订阅配对实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让点到线距离订阅模式按 `TargetIndex` 配对多目标点和直线，并为失败或缺失目标输出零值 NG 占位。

**Architecture:** 新增独立的通用编号配对器；测量结果读取器支持读取单目标明细对象；点到线距离在订阅模式一次性读取两侧明细、串行计算并复用现有汇总输出。

**Tech Stack:** C#、.NET Framework、WinForms、PowerShell 行为验证、MSBuild。

## Global Constraints

- 所有用户可见文本和代码注释使用简体中文。
- 不修改现有 WinForms 布局。
- 不引入并发，不重复读取上游结果。
- 不执行 Git 提交、切换分支或推送。

---

### Task 1: 通用目标编号配对器

**Files:**
- Create: `Node/4-Measurement/Common/MultiTargetMeasurementPairer.cs`
- Modify: `TDJS-Vision.csproj`
- Test: `Tests/PointLineDistanceMultiTargetSubscription.Tests.ps1`

- [x] 先写三目标配对、失败占位和编号缺失的失败测试。
- [x] 运行专项测试，确认因为配对器尚不存在而失败。
- [x] 实现按 `TargetIndex` 排序、校验重复编号和保留缺失项的通用配对器。
- [x] 再次运行专项测试，确认配对行为通过。

### Task 2: 点到线距离接入多目标订阅

**Files:**
- Modify: `Node/4-Measurement/Common/GeometryMeasurement.cs`
- Modify: `Node/4-Measurement/PointLineDistance/NodeParamFormPointLineDistance.cs`
- Test: `Tests/PointLineDistanceMultiTargetSubscription.Tests.ps1`

- [x] 增加订阅链路源代码约束，要求读取多目标 `Items` 并按编号配对。
- [x] 运行测试，确认旧的单摘要读取逻辑失败。
- [x] 抽取可读取任意结果项的点、线读取入口。
- [x] 订阅模式读取点、线明细一次，按编号串行计算；失败或缺失项生成零值 NG。
- [x] 验证单结果兼容和三目标配对通过。

### Task 3: 回归与编译

**Files:**
- Modify: `任务记录.md`

- [x] 运行点到线多目标专项测试。
- [x] 运行多目标测量相关回归测试。
- [x] 使用 Visual Studio 2022 MSBuild 编译完整解决方案。
- [x] 将根因、修改范围和验证结果写入任务记录。

### Task 4: 公共单结果广播

**Files:**
- Modify: `Node/4-Measurement/Common/MultiTargetMeasurementPairer.cs`
- Modify: `Tests/PointLineDistanceMultiTargetSubscription.Tests.ps1`
- Modify: `任务记录.md`

**Interfaces:**
- Consumes: `PairByTargetIndex<TFirst, TSecond>(IReadOnlyList<IndexedMeasurementValue<TFirst>>, IReadOnlyList<IndexedMeasurementValue<TSecond>>, string, string)`。
- Produces: 一侧集合数量为1时，把该值及其成功/失败状态广播到另一侧全部目标编号；双方数量大于1时仍严格按编号配对。

- [x] 添加8个点加1条成功公共线、1个点加8条线、唯一单结果NG必须广播为逐目标NG的行为测试。
- [x] 运行专项测试，确认当前严格编号规则在公共线目标2处失败。
- [x] 在通用配对器中实现对称的单结果广播，不在点到线窗体增加专用分支。
- [x] 运行专项测试和多目标测量回归测试。
- [x] 完整编译 `Debug|x64`，并更新任务记录。
