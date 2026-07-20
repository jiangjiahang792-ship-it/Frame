# TDJS-Vision 启动动画实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 增加独立线程启动动画，显示真实设备和方案加载进度，关键失败可重试，全部关键数据加载完成且至少展示 2 秒后再显示主窗体。

**Architecture:** `StartupApplicationContext` 在主 UI 线程协调启动，`StartupSplashController` 在独立 STA UI 线程保持动画响应。现有同步设备反序列化链通过 `StartupProgressContext` 上报进度和关键失败，不改变既有事件签名。

**Tech Stack:** C# 7.3、.NET Framework 4.8、Windows Forms、PowerShell 回归脚本、Visual Studio 2022 MSBuild。

## Global Constraints

- 所有中文内容必须使用简体中文。
- 新增 WinForms 控件必须在对应 `.Designer.cs` 中声明并加入控件树。
- 属性、字段、方法、类和接口必须添加中文 XML 注释或用途注释。
- 启动流程依赖接口，保持可替换、可测试，不新增第三方运行库。
- 启动页最少展示 2 秒，但不额外阻塞脚本预热和自动运行长期任务。
- 保留当前工作区已有 `FormMain` 授权菜单等未提交改动；由于同一文件存在用户改动，本计划不自动提交 Git。

---

### Task 1: 建立启动动画回归测试

**Files:**
- Create: `Tests/StartupSplash.Tests.ps1`

**Interfaces:**
- Consumes: 当前 `Program.cs`、`FormMain.cs`、`TDJS-Vision.csproj`。
- Produces: 可重复运行的启动架构、设计器控件和项目包含项回归检查。

- [ ] **Step 1: 编写失败测试**

测试脚本读取源码并断言以下真实契约：

```powershell
Assert-Contains $programSource 'new StartupApplicationContext(solutionPath)' '主程序应通过启动上下文运行。'
Assert-Contains $programSource 'Application.Run(startupContext);' '主程序应运行启动上下文消息循环。'
Assert-Contains $designerSource 'this.pictureBoxMascot' '机器人控件必须位于设计器文件。'
Assert-Contains $designerSource 'this.buttonRetry.Text = "重试";' '重试按钮必须显示简体中文。'
Assert-Contains $designerSource 'this.buttonExit.Text = "退出软件";' '退出按钮必须显示简体中文。'
Assert-Contains $formMainSource 'InitializeForStartupAsync' '主窗体应提供启动初始化入口。'
Assert-Contains $formMainSource 'Task.Run(() => { CSharpScriptEngine.Instance.WarmUp(); })' '脚本预热应保持后台执行。'
```

脚本还要断言项目文件包含 `Startup` 目录源码、`FormStartup.cs`、`FormStartup.Designer.cs`、`FormStartup.resx` 和机器人资源。

- [ ] **Step 2: 运行测试并确认正确失败**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tests\StartupSplash.Tests.ps1
```

Expected: FAIL，原因是 `StartupApplicationContext` 或 `FormStartup.Designer.cs` 尚不存在，不是脚本语法错误。

---

### Task 2: 实现启动进度模型和作用域

**Files:**
- Create: `Startup/IStartupProgressReporter.cs`
- Create: `Startup/StartupProgressInfo.cs`
- Create: `Startup/StartupProgressContext.cs`
- Modify: `TDJS-Vision.csproj`

**Interfaces:**
- Produces: `void Report(StartupProgressInfo progress)`；`IDisposable StartupProgressContext.Begin(IStartupProgressReporter reporter)`；`ReportItem(...)`；`ReportFailure(...)`；`ThrowIfFailures()`。

- [ ] **Step 1: 在测试中增加进度边界契约**

```powershell
Assert-Contains $progressSource 'Math.Max(0, Math.Min(100, percentage))' '启动进度必须限制在 0 到 100。'
Assert-Contains $contextSource 'AsyncLocal<StartupProgressState>' '启动进度上下文必须隔离到当前异步启动链。'
```

- [ ] **Step 2: 运行测试确认仍因缺少进度模型而失败**

Expected: FAIL，提示缺少 `StartupProgressInfo.cs`。

- [ ] **Step 3: 实现最小进度模型**

`StartupProgressInfo` 使用只读属性：

```csharp
public string StageName { get; }
public string ItemName { get; }
public int Current { get; }
public int Total { get; }
public int Percentage { get; }
```

`StartupProgressContext` 使用 `AsyncLocal<StartupProgressState>` 保存当前接口和 `List<StartupFailure>`。所有公开类型、属性和方法写简体中文注释。

- [ ] **Step 4: 运行测试确认进度模型契约通过**

Expected: 进度模型相关断言通过，整体仍因启动窗体缺失而失败。

---

### Task 3: 实现轻盈科技启动窗体和独立线程控制器

**Files:**
- Create: `Forms/Startup/FormStartup.cs`
- Create: `Forms/Startup/FormStartup.Designer.cs`
- Create: `Forms/Startup/FormStartup.resx`
- Create: `Startup/StartupFailureAction.cs`
- Create: `Startup/StartupSplashController.cs`
- Modify: `TDJS-Vision.csproj`

**Interfaces:**
- Consumes: `IStartupProgressReporter`、`StartupProgressInfo`。
- Produces: `Start()`、`Report(...)`、`Task<StartupFailureAction> ShowFailureAsync(...)`、`Close()`。

- [ ] **Step 1: 增加设计器和控制器失败测试**

断言 `FormStartup.Designer.cs` 中存在并加入控件树：`pictureBoxMascot`、`labelTitle`、`labelStatus`、`labelDetail`、`panelProgressTrack`、`panelProgressValue`、`panelError`、`buttonRetry`、`buttonExit`、`timerAnimation`。

- [ ] **Step 2: 运行测试确认失败原因是控件缺失**

- [ ] **Step 3: 在设计器中创建全部控件**

窗体固定为 720×430、无边框、屏幕居中、浅色背景。控件 `Text` 使用：

```text
TDJS-Vision 工业视觉平台
正在启动软件……
正在准备运行环境
重试
退出软件
```

- [ ] **Step 4: 实现窗体行为**

`UpdateProgress` 更新标签和进度条宽度；`ShowFailure` 显示错误面板；定时器只更新机器人纵向位置、眨眼绘制状态和天线呼吸强度，并在 `prefers-reduced-motion` 不适用的 WinForms 环境保持低频 30 FPS 上限。

- [ ] **Step 5: 实现独立 STA 线程控制器**

控制器启动后台 STA 线程并调用 `Application.Run(formStartup)`；使用 `ManualResetEventSlim` 等待窗体就绪；所有跨线程更新通过 `BeginInvoke`；失败按钮通过 `TaskCompletionSource<StartupFailureAction>` 返回选择。

- [ ] **Step 6: 运行回归测试**

Expected: 启动窗体和项目包含项断言通过。

---

### Task 4: 将主程序切换到启动上下文

**Files:**
- Create: `Startup/StartupApplicationContext.cs`
- Modify: `Program.cs`
- Modify: `TDJS-Vision.csproj`

**Interfaces:**
- Consumes: `FormMain.InitializeForStartupAsync(IStartupProgressReporter)`、`StartupSplashController`。
- Produces: `StartupApplicationContext(string solutionPath)`。

- [ ] **Step 1: 增加 Program 启动顺序失败测试**

保留现有互斥锁、线程池和授权顺序断言，并增加：

```powershell
$contextIndex = $programSource.IndexOf('new StartupApplicationContext(solutionPath)')
$runContextIndex = $programSource.IndexOf('Application.Run(startupContext);')
Assert-True ($contextIndex -gt $authorizationIndex) '启动上下文必须在授权后创建。'
Assert-True ($runContextIndex -gt $contextIndex) '创建启动上下文后才能运行消息循环。'
```

- [ ] **Step 2: 运行测试确认失败**

- [ ] **Step 3: 实现 `StartupApplicationContext`**

上下文记录 `Stopwatch`，启动启动页后通过主消息循环执行 `InitializeAsync`。关键初始化失败时调用 `ShowFailureAsync`；重试复用已有隐藏主窗体，构造失败才重新创建。成功时等待：

```csharp
TimeSpan remaining = TimeSpan.FromSeconds(2) - stopwatch.Elapsed;
if (remaining > TimeSpan.Zero)
    await Task.Delay(remaining);
```

随后关闭启动页、订阅 `FormClosed`、设置 `Program.MainForm` 并显示主窗体。

- [ ] **Step 4: 修改 `Program.RunApplication`**

统一解析 `.Sol` 参数，只保留一处：

```csharp
using (StartupApplicationContext startupContext = new StartupApplicationContext(solutionPath))
{
    Application.Run(startupContext);
}
```

- [ ] **Step 5: 运行单实例和启动动画测试**

Expected: `ProgramSingleInstance.Tests.ps1` 和新增测试通过相关断言。

---

### Task 5: 拆分并等待主窗体关键初始化

**Files:**
- Modify: `FormMain.cs`

**Interfaces:**
- Produces: `internal async Task InitializeForStartupAsync(IStartupProgressReporter reporter)`。

- [ ] **Step 1: 增加主窗体初始化失败测试**

断言存在 `_startupStructureInitialized`、`InitializeStartupStructure()`、`LoadStartupSolution()`、`StartNonBlockingWarmups()`，并断言 `FormMain_Load` 不再直接调用 `AutoLoadSolutionAsync()`。

- [ ] **Step 2: 运行测试确认失败**

- [ ] **Step 3: 提取幂等结构初始化**

将布局、语言、SDK、工具窗口状态、事件订阅、权限状态移动到 `InitializeStartupStructure()`；使用布尔字段保证只执行一次。

- [ ] **Step 4: 实现方案路径优先级和同步关键加载**

命令行 `.Sol` 路径优先；否则仅在 `Settings.Default.IsAutoLoad` 时加载默认路径。文件不存在或反序列化失败必须抛出到启动上下文。加载结束调用 `StartupProgressContext.ThrowIfFailures()`。

- [ ] **Step 5: 启动非阻塞任务**

脚本预热保持 `Task.Run`；自动运行通过单独的 `RunAutoSolutionInBackgroundAsync` 启动，不在启动初始化中等待循环方案结束，异常写日志。

- [ ] **Step 6: 让 `FormMain_Load` 只做兜底幂等调用**

正常启动上下文已经完成初始化时，`Load` 事件立即返回；设计器单独预览或旧入口调用时仍可安全初始化。

- [ ] **Step 7: 运行新增测试和现有 FormMain 回归脚本**

Expected: 相关测试全部通过。

---

### Task 6: 接入真实设备和流程加载进度

**Files:**
- Modify: `ConfigHelper.cs`
- Modify: `Forms/LightAdd/FrmLightListView.cs`
- Modify: `Forms/CameraAdd/FrmCameraListView.cs`
- Modify: `Forms/PLCAdd/FrmPLCListView.cs`
- Modify: `Forms/ModbusAdd/FrmModbusListView.cs`
- Modify: `Forms/TCPAdd/FrmTCPListView.cs`
- Modify: `Forms/COMAdd/FrmCOMListView.cs`
- Modify: `Forms/ProcessNew/FormNewProcessWizard.cs`

**Interfaces:**
- Consumes: `StartupProgressContext.ReportItem(...)`、`ReportFailure(...)`。
- Produces: 用户可见的当前设备或流程名称和序号。

- [ ] **Step 1: 增加每类加载器的进度失败测试**

测试分别断言源文件包含对应阶段名称和 `StartupProgressContext.ReportItem`，并断言空 `catch` 被替换为 `ReportFailure`。

- [ ] **Step 2: 运行测试确认失败**

- [ ] **Step 3: 为方案读取和解析上报进度**

在 `ConfigHelper.SolLoad` 的文件读取、JSON 解析、清理旧方案和触发恢复链之前上报固定阶段，不改变 JSON 格式。

- [ ] **Step 4: 为每种设备上报当前对象**

各窗体先使用 `OfType<T>().ToList()` 计算总数，再按索引上报，例如：

```csharp
StartupProgressContext.ReportItem("正在恢复相机设备", camera.DevName, index + 1, cameras.Count, 45, 55);
```

阶段范围依次为光源 35—43、相机 43—55、PLC 55—63、Modbus 63—70、TCP 70—77、串口 77—84、流程 84—95。

- [ ] **Step 5: 收集关键失败**

原有 `finally` 继续触发完成事件；`catch (Exception ex)` 调用 `ReportFailure` 并写异常日志，禁止继续静默吞掉启动错误。

- [ ] **Step 6: 运行启动动画测试**

Expected: 所有阶段断言和异常收集断言通过。

---

### Task 7: 生成并嵌入机器人素材

**Files:**
- Create: `Resources/StartupMascot.png`
- Modify: `Properties/Resources.resx`
- Modify: `Properties/Resources.Designer.cs`
- Modify: `TDJS-Vision.csproj`
- Modify: `Forms/Startup/FormStartup.Designer.cs`

**Interfaces:**
- Produces: `Properties.Resources.StartupMascot`。

- [ ] **Step 1: 使用参考图生成独立素材**

使用内置图像生成工具，参考图只作为角色方向。提示词要求：红色圆角菱形机身、蓝色单眼、白蓝手臂和下部推进器、亲和科技感、正面居中、浅蓝白纯净背景、无文字、无水印、四周留白。

- [ ] **Step 2: 将最终 PNG 保存到项目资源目录**

固定路径为 `Resources/StartupMascot.png`，不依赖生成工具默认目录。

- [ ] **Step 3: 注册项目资源**

`Resources.resx` 使用 `ResXFileRef`，`Resources.Designer.cs` 暴露只读 `Bitmap StartupMascot`，设计器直接赋值给 `pictureBoxMascot.Image`。

- [ ] **Step 4: 运行启动动画测试**

Expected: 资源包含项和设计器资源引用断言通过。

---

### Task 8: 记录任务并完成验证

**Files:**
- Modify: `任务记录.md`

**Interfaces:**
- Consumes: 全部启动动画实现。
- Produces: 可追溯的中文任务记录和验证证据。

- [ ] **Step 1: 更新任务记录**

记录设计选择、独立线程结构、加载阶段、错误策略、机器人资源路径、测试结果和构建结果。

- [ ] **Step 2: 运行全部相关 PowerShell 回归脚本**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tests\StartupSplash.Tests.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\ProgramSingleInstance.Tests.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\FormMainAuthorizationMenu.Tests.ps1
```

Expected: 三个脚本均输出通过，退出代码均为 0。

- [ ] **Step 3: 编译解决方案**

Run:

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "D:\MyCode\PublicWook\TDJS-Vision\TDJS-Vision.sln" /t:Build /p:RestorePackages=false /m
```

Expected: `0 个错误`。已有警告单独记录，不冒充零警告。

- [ ] **Step 4: 启动人工验收**

检查首次显示启动页、机器人持续动画、加载对象文字和进度变化、快速加载仍显示至少 2 秒、完成后才出现主页面、关键失败按钮为“重试”和“退出软件”。

- [ ] **Step 5: 检查工作区差异**

确认没有覆盖用户原有授权菜单、说明书和其他未提交改动，只汇报本任务实际修改文件。
