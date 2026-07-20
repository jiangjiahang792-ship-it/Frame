# TDJS-Vision 独占工作区与方案加载动画实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让流程编辑和 AI 训练成为独占工作区，并为手动打开方案复用启动动画和真实加载进度。

**Architecture:** `IExclusiveWorkspacePresenter` 统一隐藏/恢复主窗体并显示最大化工作窗体；`ISolutionLoadingCoordinator` 复用独立 STA 启动页，在主 UI 线程执行同步方案加载。`FormMain` 只负责入口编排和用户提示。

**Tech Stack:** C# 7.3、.NET Framework 4.8、Windows Forms、PowerShell 源码回归测试、Visual Studio 2022 MSBuild。

## Global Constraints

- 所有中文使用简体中文。
- 新增 WinForms 控件必须位于 `.Designer.cs`；本计划不新增控件。
- 新增接口、类、字段、属性和方法必须添加中文注释。
- 方案反序列化必须在主 UI 线程执行，不允许用 `Task.Run` 包裹。
- 不新增第三方依赖，不覆盖现有未提交改动，不自动提交 Git。

---

### Task 1: 建立独占工作区和动画加载回归测试

**Files:**
- Create: `Tests/ExclusiveWorkspace.Tests.ps1`

**Interfaces:**
- Consumes: `FormMain.cs`、`TDJS-Vision.csproj`。
- Produces: 独占窗口调用顺序、三个入口覆盖、动画方案加载线程约束的源码契约。

- [ ] 编写断言，要求存在 `IExclusiveWorkspacePresenter`、`ISolutionLoadingCoordinator`，三个工作窗口入口使用统一 presenter，手动打开方案使用 coordinator。
- [ ] 运行测试，确认因接口文件缺失而失败。

### Task 2: 实现独占工作区显示接口

**Files:**
- Create: `Forms\Workspace\IExclusiveWorkspacePresenter.cs`
- Create: `Forms\Workspace\ExclusiveWorkspacePresenter.cs`
- Modify: `FormMain.cs`
- Modify: `TDJS-Vision.csproj`
- Test: `Tests\ExclusiveWorkspace.Tests.ps1`

**Interfaces:**
- Produces: `DialogResult ShowDialog(Form mainForm, Form workspaceForm)`。

- [ ] 在实现中先预定位工作窗体，再 `mainForm.Hide()`。
- [ ] 在 `try` 中调用 `workspaceForm.ShowDialog(mainForm)`。
- [ ] 在 `finally` 中执行 `mainForm.Show()`、`Activate()`、`BringToFront()`。
- [ ] 将流程编辑、无监督训练、大模型训练三个入口改为统一接口调用。
- [ ] 运行专项测试并确认相关断言通过。

### Task 3: 实现手动方案动画加载接口

**Files:**
- Create: `Startup\ISolutionLoadingCoordinator.cs`
- Create: `Startup\SolutionLoadingCoordinator.cs`
- Modify: `FormMain.cs`
- Modify: `TDJS-Vision.csproj`
- Test: `Tests\ExclusiveWorkspace.Tests.ps1`

**Interfaces:**
- Produces: `void Load(Form mainForm, string solutionPath, bool showInfo)`。

- [ ] 启动独立 `StartupSplashController`，隐藏主窗体并建立进度作用域。
- [ ] 在当前主 UI 线程调用 `ConfigHelper.SolLoad(solutionPath, showInfo)` 和 `ThrowIfFailures()`。
- [ ] 保持至少 2 秒动画，成功或异常时均恢复、激活主窗体。
- [ ] 修改手动打开方案入口，移除 `Solution.Instance.Load`，失败写日志并显示中文摘要。
- [ ] 运行专项测试并确认通过。

### Task 4: 记录与完整验证

**Files:**
- Modify: `任务记录.md`

**Interfaces:**
- Consumes: 全部实现。
- Produces: 可追溯的任务记录和验证证据。

- [ ] 更新任务记录，说明独占工作区、方案动画和异常恢复策略。
- [ ] 运行 `ExclusiveWorkspace.Tests.ps1`、`StartupSplash.Tests.ps1`、单实例和既有 FormMain 回归。
- [ ] 使用 Visual Studio 2022 MSBuild 完整编译解决方案，要求 0 个错误，并如实记录既有警告数。
- [ ] 检查差异，确认没有覆盖工作区中与本任务无关的内容。
