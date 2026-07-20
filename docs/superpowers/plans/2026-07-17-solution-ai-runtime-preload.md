# 方案打开时 AI 节点预加载实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 打开或启动恢复方案时顺序预加载所有启用的大模型和无监督节点。

**Architecture:** 通过统一节点预加载接口隔离方案恢复层与具体 AI 节点；统一协调器遍历恢复后的流程节点并顺序等待每个预加载任务。协调器由 `ConfigHelper.SolLoad` 在反序列化完成事件返回后调用，因此覆盖所有方案打开入口。

**Tech Stack:** C#、.NET Framework 4.8、WinForms、Task、现有 StartupProgressContext、PowerShell 回归检查。

## Global Constraints

- 仅预加载启用流程中的启用节点。
- 节点顺序加载，禁止并行初始化 GPU 模型。
- 单个节点失败后继续处理其余节点，并交由启动失败汇总机制报告。
- 不改变参数保存时已有的预加载逻辑。

---

### Task 1: 建立统一预加载契约和协调器

**Files:**
- Create: `Node/INodeRuntimePreloader.cs`
- Create: `Startup/SolutionAiRuntimePreloader.cs`
- Modify: `TDJS-Vision.csproj`
- Test: `Tests/SolutionAiRuntimePreload.Tests.ps1`

**Interfaces:**
- Produces: `Task PreloadSavedRuntimeAsync()`，由方案预加载协调器顺序调用。

- [ ] **Step 1: 编写失败检查**

检查统一接口、顺序等待、启用状态过滤、异常继续和启动进度报告均存在。

- [ ] **Step 2: 运行检查并确认因接口缺失而失败**

Run: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests/SolutionAiRuntimePreload.Tests.ps1`
Expected: FAIL，提示缺少 `INodeRuntimePreloader`。

- [ ] **Step 3: 实现最小接口和协调器**

协调器遍历 `Solution.Instance.AllProcesses`，筛选启用流程和节点，逐个执行 `GetAwaiter().GetResult()`；每个节点独立捕获异常并调用 `StartupProgressContext.ReportFailure`。

- [ ] **Step 4: 运行检查确认协调器要求通过**

Run: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests/SolutionAiRuntimePreload.Tests.ps1`
Expected: PASS。

### Task 2: 大模型与无监督节点接入统一接口

**Files:**
- Modify: `Node/3-Detection/LargeModel/NodeLargeModelDetection.cs`
- Modify: `Node/3-Detection/Unsupervised/NodeUnsupervisedDetection.cs`
- Test: `Tests/SolutionAiRuntimePreload.Tests.ps1`

**Interfaces:**
- Consumes: `INodeRuntimePreloader.PreloadSavedRuntimeAsync()`。
- Produces: 两类节点从参数窗体已恢复的 `Params` 调用现有 `PreloadRuntimeAsync(param)`。

- [ ] **Step 1: 扩展失败检查，要求两类节点实现统一接口**
- [ ] **Step 2: 运行检查并确认因节点尚未实现接口而失败**
- [ ] **Step 3: 添加参数校验和接口实现，复用现有配置门及预热逻辑**
- [ ] **Step 4: 运行检查并确认通过**

### Task 3: 接入全部方案加载入口

**Files:**
- Modify: `ConfigHelper.cs`
- Test: `Tests/SolutionAiRuntimePreload.Tests.ps1`

**Interfaces:**
- Consumes: `SolutionAiRuntimePreloader.PreloadEnabledNodes()`。

- [ ] **Step 1: 扩展失败检查，要求反序列化完成事件之后调用协调器**
- [ ] **Step 2: 运行检查并确认缺少调用而失败**
- [ ] **Step 3: 在 `DeserializationCompletionEvent?.Invoke` 返回后调用协调器**
- [ ] **Step 4: 运行检查并确认通过**

### Task 4: 记录与完整验证

**Files:**
- Modify: `任务记录.md`

- [ ] **Step 1: 记录方案打开顺序预加载、失败策略和验证范围**
- [ ] **Step 2: 运行 AI 专项回归检查**
- [ ] **Step 3: 编译 Debug x64，要求 0 错误**
- [ ] **Step 4: 编译 Release x64，要求 0 错误**
