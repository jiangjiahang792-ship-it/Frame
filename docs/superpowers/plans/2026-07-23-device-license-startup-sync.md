# 设备许可证启动自动同步实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 主程序每次取得单实例运行权后，以运行目录根部的 `device.license` 为唯一来源，自动同步所有已部署 AI 运行目录，任何同步问题只写日志且不阻止启动。

**Architecture:** 在 `Startup` 下增加可替换的 `IDeviceLicenseSynchronizer` 文件同步边界、结构化同步结果和默认文件系统实现。`Program` 在正常启动链最前部调用该服务，并把每个目标的中文结果写入现有日志系统；同步器逐目标隔离异常，缺失目录不创建，内容相同不重复写入。

**Tech Stack:** C# 7.3、.NET Framework 4.8、WinForms、PowerShell 回归测试、Visual Studio 2022 MSBuild。

## Global Constraints

- 所有中文内容使用简体中文。
- 新增接口、类、枚举、属性、字段和方法必须包含简体中文 XML 注释，关键思路包含简体中文注释。
- 根目录 `device.license` 是唯一来源，并继续供 CPU YOLO 直接使用。
- 目标固定为 `LargeModelDll`、`UnsupervisedDll`、`YoloGPUDll`、`YoloGPUDll\1050tidll` 和 `YoloGPUDll\750dll`。
- 只处理已经存在的目标目录，不创建缺少的运行库目录。
- 同步失败只写日志，不显示弹窗、不退出程序、不阻止其它目标同步。
- 日志不得输出许可证内容。
- 不把三个大型运行库目录绑定到项目内容或生成复制事件。
- 保留工作区既有改动，只修改本功能涉及的最小位置。

---

### Task 1: 用真实临时目录定义许可证同步行为

**Files:**
- Create: `Tests/DeviceLicenseStartupSync.Tests.ps1`
- Create: `Startup/IDeviceLicenseSynchronizer.cs`
- Create: `Startup/DeviceLicenseSynchronizationResult.cs`
- Create: `Startup/DeviceLicenseSynchronizer.cs`

**Interfaces:**
- Consumes: 程序运行目录绝对路径和其中的 `device.license`。
- Produces: `IReadOnlyList<DeviceLicenseSynchronizationResult> IDeviceLicenseSynchronizer.Synchronize(string applicationDirectory)`。

- [x] **Step 1: 编写失败测试**

测试脚本使用 `Add-Type -Path` 编译三个独立源码文件，通过反射创建内部 `DeviceLicenseSynchronizer`，并在唯一临时目录中验证：目标缺失时复制、过期时覆盖、内容相同不修改时间、目录缺失不创建、源缺失不抛出、单目标失败不影响其它目标，以及五个目标目录名称完整。

关键断言如下：

```powershell
$results = @($synchronizeMethod.Invoke($synchronizer, @($applicationDirectory)))
Assert-True ((Get-Content -LiteralPath $largeModelLicense -Raw) -eq $sourceContent) '大模型许可证未同步。'
Assert-True ((Test-Path -LiteralPath $missingRuntimeDirectory) -eq $false) '同步器不得创建缺少的运行库目录。'
Assert-True (@($results | Where-Object { $_.Status.ToString() -eq '失败' }).Count -ge 1) '复制失败必须转换为结构化失败结果。'
```

- [x] **Step 2: 运行测试确认红灯**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\DeviceLicenseStartupSync.Tests.ps1`

Expected: FAIL，首先报告缺少 `Startup\IDeviceLicenseSynchronizer.cs` 或同步器类型，证明测试能检测尚未实现的功能。

- [x] **Step 3: 增加同步契约与结果模型**

`IDeviceLicenseSynchronizer.cs` 定义：

```csharp
internal interface IDeviceLicenseSynchronizer
{
    IReadOnlyList<DeviceLicenseSynchronizationResult> Synchronize(string applicationDirectory);
}
```

`DeviceLicenseSynchronizationResult.cs` 定义 `设备许可证同步状态` 枚举（`已复制`、`无需更新`、`已跳过`、`失败`）和只读结果对象，结果包含 `Status`、`TargetPath`、`Message`。

- [x] **Step 4: 实现最小文件同步服务**

`DeviceLicenseSynchronizer` 使用以下目标相对目录：

```csharp
private static readonly string[] TargetRelativeDirectories =
{
    "LargeModelDll",
    "UnsupervisedDll",
    "YoloGPUDll",
    Path.Combine("YoloGPUDll", "1050tidll"),
    Path.Combine("YoloGPUDll", "750dll")
};
```

`Synchronize` 先验证根目录和源文件，再逐目标调用私有 `SynchronizeTarget`。目标目录不存在返回 `已跳过`；目标内容一致返回 `无需更新`；缺失或内容不同时先在目标目录写唯一临时文件，再使用 `File.Replace` 或 `File.Move` 原子切换并返回 `已复制`；每个目标独立捕获异常并返回 `失败`，失败时清理临时文件并保留原目标。`FilesHaveSameContent` 使用两个只读 `FileStream` 分块比较字节，不解析或记录内容。

- [x] **Step 5: 运行测试确认绿灯**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\DeviceLicenseStartupSync.Tests.ps1`

Expected: PASS，输出“设备许可证启动同步行为检查通过”。

### Task 2: 接入主程序启动链与项目编译

**Files:**
- Modify: `Program.cs`
- Modify: `TDJS-Vision.csproj`
- Modify: `Tests/DeviceLicenseStartupSync.Tests.ps1`

**Interfaces:**
- Consumes: Task 1 的 `IDeviceLicenseSynchronizer`、`DeviceLicenseSynchronizer` 和结构化结果。
- Produces: `Program.SynchronizeDeviceLicense()` 启动入口，确保调用失败不离开正常启动链。

- [x] **Step 1: 扩展失败测试**

增加源码契约断言：项目文件编译三个新增 C# 文件；`Program` 持有接口类型字段；`RunApplication` 在 `StartupApplicationContext` 之前调用 `SynchronizeDeviceLicense()`；失败结果使用 `MsgLevel.Warn`，未处理异常使用 `MsgLevel.Exception`；代码中不存在许可证弹窗或 `return` 终止启动。

- [x] **Step 2: 运行测试确认红灯**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\DeviceLicenseStartupSync.Tests.ps1`

Expected: FAIL，报告 `Program` 尚未调用许可证同步器或项目尚未包含新增源码。

- [x] **Step 3: 接入 Program**

在 `Program` 中增加只读接口字段：

```csharp
private static readonly IDeviceLicenseSynchronizer DeviceLicenseSynchronizer = new DeviceLicenseSynchronizer();
```

在 `RunApplication` 的启动诊断日志之后调用 `SynchronizeDeviceLicense()`。该方法用最外层 `try/catch` 兜底，遍历结果：`失败` 使用 `MsgLevel.Warn`，其它状态使用 `MsgLevel.Info`；兜底异常使用 `MsgLevel.Exception`。所有分支执行后自然返回 `RunApplication` 后续流程，不显示消息框。

- [x] **Step 4: 更新项目编译项**

在现有 `Startup` 编译项旁加入：

```xml
<Compile Include="Startup\IDeviceLicenseSynchronizer.cs" />
<Compile Include="Startup\DeviceLicenseSynchronizationResult.cs" />
<Compile Include="Startup\DeviceLicenseSynchronizer.cs" />
```

不修改 `RuntimeDependency` 复制规则，也不增加三个大型运行库目录的项目项。

- [x] **Step 5: 运行专项与启动回归测试**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tests\DeviceLicenseStartupSync.Tests.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\ProgramSingleInstance.Tests.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\RuntimeDependencyTemplate.Tests.ps1
```

Expected: 三组测试全部 PASS。

### Task 3: 记录任务并完成整体验证

**Files:**
- Modify: `任务记录.md`
- Modify: `docs/superpowers/plans/2026-07-23-device-license-startup-sync.md`

**Interfaces:**
- Consumes: Task 1 和 Task 2 已通过的同步行为与启动接入。
- Produces: 可审计的中文任务记录和最终验证证据。

- [x] **Step 1: 执行 Debug|x64 完整构建**

Run: `& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "D:\MyCode\PublicWook\TDJS-Vision\TDJS-Vision.sln" /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /m`

Expected: `0 Error(s)`；允许保留项目既有警告，但不得增加本功能相关编译错误。

- [x] **Step 2: 验证真实 Debug 输出目录同步**

先记录 `bin\x64\Debug\device.license` 与已有目标副本哈希，再启动同步专项的运行态调用，确认已存在的 `LargeModelDll`、`UnsupervisedDll`、`YoloGPUDll`、`1050tidll` 和 `750dll` 中许可证内容与根目录一致。不得输出许可证内容。

- [x] **Step 3: 更新任务记录**

在 `任务记录.md` 追加“设备许可证启动自动同步”，记录唯一来源、五个目标、同内容跳过、目录缺失跳过、异常只写日志、专项测试和构建结果。

- [x] **Step 4: 最终静态检查**

Run:

```powershell
git diff --check
git status --short
```

Expected: `git diff --check` 无输出；状态中只新增或修改本功能文件以及进入任务前已经存在的用户改动。

- [x] **Step 5: 标记计划完成**

把本计划所有已执行步骤改为 `[x]`，最终回复列出实现结果、验证证据和本次实际修改文件，不声称或提交用户原有改动。
