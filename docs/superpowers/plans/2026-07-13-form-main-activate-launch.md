# FormMain 授权程序启动实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 点击 `FormMain` 的“授权”菜单后启动运行目录中的 `activate.exe`，文件缺失或启动异常时显示简体中文错误提示。

**Architecture:** 使用 `IExternalProgramLauncher` 隔离 WinForm 与 `Process.Start`，由 `ExternalProgramLauncher` 提供默认实现。`FormMain` 通过构造方法接收启动器，点击事件只负责运行目录路径解析、存在性检查、启动调用和界面错误提示。

**Tech Stack:** C# 7.3、.NET Framework 4.8、WinForms、PowerShell 回归测试、MSBuild。

## Global Constraints

- 所有中文内容必须使用简体中文，并以 UTF-8 保存。
- `授权ToolStripMenuItem` 必须保留在 `FormMain.Designer.cs` 中，事件绑定也写入设计器文件。
- 新增接口、类、字段、构造方法和方法必须添加简体中文注释。
- 使用 `AppDomain.CurrentDomain.BaseDirectory` 定位运行目录，不依赖当前工作目录。
- 启动操作不得等待 `activate.exe` 退出，不阻塞主界面。
- 每次代码更改必须记录到 `FLOW_CANVAS_B_PLAN_TASKS.md`。

---

## 文件结构

- 新建 `ExternalProgramLauncher.cs`：声明外部程序启动接口及默认进程启动实现。
- 修改 `FormMain.cs`：注入启动器，并实现授权菜单点击流程和中文错误提示。
- 修改 `FormMain.Designer.cs`：绑定现有授权菜单点击事件。
- 修改 `TDJS-Vision.csproj`：编译新增启动器源文件。
- 新建 `Tests/FormMainAuthorizationMenu.Tests.ps1`：覆盖事件绑定、路径、错误提示和启动器实现约束。
- 修改 `FLOW_CANVAS_B_PLAN_TASKS.md`：记录功能与验证结果。

### Task 1: 用失败测试定义授权菜单行为

**Files:**
- Create: `Tests/FormMainAuthorizationMenu.Tests.ps1`
- Test: `Tests/FormMainAuthorizationMenu.Tests.ps1`

**Interfaces:**
- Consumes: 现有 `FormMain.cs`、`FormMain.Designer.cs` 和 `TDJS-Vision.csproj` 文本。
- Produces: 对 `IExternalProgramLauncher.Start(string executablePath)`、`ExternalProgramLauncher` 和 `授权ToolStripMenuItem_Click` 的源代码契约。

- [ ] **Step 1: 写入失败测试**

```powershell
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$designerPath = Join-Path $root "FormMain.Designer.cs"
$formPath = Join-Path $root "FormMain.cs"
$launcherPath = Join-Path $root "ExternalProgramLauncher.cs"
$projectPath = Join-Path $root "TDJS-Vision.csproj"

function Assert-Contains {
    param([string]$Content, [string]$Pattern, [string]$Message)
    if ($Content -notlike "*$Pattern*") { throw $Message }
}

$designer = Get-Content -Raw -Encoding UTF8 $designerPath
$form = Get-Content -Raw -Encoding UTF8 $formPath
$project = Get-Content -Raw -Encoding UTF8 $projectPath

Assert-Contains $designer 'this.授权ToolStripMenuItem.Click += new System.EventHandler(this.授权ToolStripMenuItem_Click);' '授权菜单未绑定点击事件。'
Assert-Contains $form 'Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "activate.exe")' '授权程序路径未从软件运行目录生成。'
Assert-Contains $form '未找到授权程序' '缺少授权程序不存在时的中文提示。'
Assert-Contains $form '启动授权程序失败' '缺少授权程序启动异常时的中文提示。'
Assert-Contains $form 'externalProgramLauncher.Start(activationProgramPath);' 'FormMain 未通过外部程序启动接口启动授权程序。'
Assert-Contains $project '<Compile Include="ExternalProgramLauncher.cs" />' '项目未编译外部程序启动器。'

if (-not (Test-Path -LiteralPath $launcherPath)) { throw '缺少 ExternalProgramLauncher.cs。' }
$launcher = Get-Content -Raw -Encoding UTF8 $launcherPath
Assert-Contains $launcher 'interface IExternalProgramLauncher' '缺少外部程序启动接口。'
Assert-Contains $launcher 'void Start(string executablePath);' '外部程序启动接口签名不正确。'
Assert-Contains $launcher 'sealed class ExternalProgramLauncher : IExternalProgramLauncher' '缺少默认外部程序启动实现。'
Assert-Contains $launcher 'UseShellExecute = true' '默认启动器未启用可执行文件 Shell 启动。'
Assert-Contains $launcher 'WorkingDirectory = Path.GetDirectoryName(executablePath)' '默认启动器未设置授权程序工作目录。'

Write-Host "FormMain 授权菜单检查通过。"
```

- [ ] **Step 2: 运行测试并确认正确失败**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tests\FormMainAuthorizationMenu.Tests.ps1`

Expected: FAIL，首先报告“授权菜单未绑定点击事件。”，证明测试能检测尚未实现的行为。

### Task 2: 实现可插拔外部程序启动器与菜单点击逻辑

**Files:**
- Create: `ExternalProgramLauncher.cs`
- Modify: `FormMain.cs:135-165, 1016-1024`
- Modify: `FormMain.Designer.cs:622-628`
- Modify: `TDJS-Vision.csproj:234-240`
- Test: `Tests/FormMainAuthorizationMenu.Tests.ps1`

**Interfaces:**
- Consumes: `IExternalProgramLauncher.Start(string executablePath)`。
- Produces: `ExternalProgramLauncher` 默认实现和 `FormMain.授权ToolStripMenuItem_Click(object sender, EventArgs e)`。

- [ ] **Step 1: 新增启动器接口和默认实现**

```csharp
using System.Diagnostics;
using System.IO;

namespace TDJS_Vision
{
    /// <summary>
    /// 定义外部程序启动能力，便于窗体与具体进程启动方式解耦。
    /// </summary>
    internal interface IExternalProgramLauncher
    {
        /// <summary>
        /// 启动指定路径的外部程序。
        /// </summary>
        /// <param name="executablePath">外部程序的绝对路径。</param>
        void Start(string executablePath);
    }

    /// <summary>
    /// 使用系统进程 API 启动外部程序的默认实现。
    /// </summary>
    internal sealed class ExternalProgramLauncher : IExternalProgramLauncher
    {
        /// <inheritdoc />
        public void Start(string executablePath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                UseShellExecute = true
            };

            Process.Start(startInfo);
        }
    }
}
```

- [ ] **Step 2: 将启动器加入项目编译**

在 `TDJS-Vision.csproj` 的根目录源文件编译项中加入：

```xml
<Compile Include="ExternalProgramLauncher.cs" />
```

- [ ] **Step 3: 在 FormMain 中注入并复用启动器**

将两个公开构造方法收敛到内部构造方法，保留 WinForm 设计器需要的无参构造方法：

```csharp
/// <summary>
/// 用于启动授权程序的外部程序启动器。
/// </summary>
private readonly IExternalProgramLauncher externalProgramLauncher;

/// <summary>
/// 启动时需要打开的方案路径。
/// </summary>
private string args = string.Empty;

/// <summary>
/// 初始化主窗体。
/// </summary>
public FormMain() : this(string.Empty, new ExternalProgramLauncher())
{
}

/// <summary>
/// 使用指定启动参数初始化主窗体。
/// </summary>
/// <param name="args">启动时需要打开的方案路径。</param>
public FormMain(string args) : this(args, new ExternalProgramLauncher())
{
}

/// <summary>
/// 使用指定启动参数和外部程序启动器初始化主窗体。
/// </summary>
/// <param name="args">启动时需要打开的方案路径。</param>
/// <param name="externalProgramLauncher">外部程序启动器。</param>
internal FormMain(string args, IExternalProgramLauncher externalProgramLauncher)
{
    this.externalProgramLauncher = externalProgramLauncher ?? throw new ArgumentNullException(nameof(externalProgramLauncher));
    InitializeComponent();
    InitializeLanguageUI();
    dockPanel1.Theme = new GreenTheme();
    this.args = args ?? string.Empty;
}
```

- [ ] **Step 4: 实现授权菜单点击方法**

```csharp
/// <summary>
/// 启动软件运行目录中的授权程序。
/// </summary>
/// <param name="sender">触发事件的菜单项。</param>
/// <param name="e">事件参数。</param>
private void 授权ToolStripMenuItem_Click(object sender, EventArgs e)
{
    string activationProgramPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "activate.exe");
    if (!File.Exists(activationProgramPath))
    {
        MessageBoxTD.Show($"未找到授权程序：{activationProgramPath}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return;
    }

    try
    {
        externalProgramLauncher.Start(activationProgramPath);
    }
    catch (Exception ex)
    {
        MessageBoxTD.Show($"启动授权程序失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
```

- [ ] **Step 5: 在设计器文件中绑定点击事件**

在授权菜单配置中加入：

```csharp
this.授权ToolStripMenuItem.Click += new System.EventHandler(this.授权ToolStripMenuItem_Click);
```

- [ ] **Step 6: 运行测试并确认通过**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tests\FormMainAuthorizationMenu.Tests.ps1`

Expected: PASS，输出“FormMain 授权菜单检查通过。”。

### Task 3: 更新任务记录并完成整体验证

**Files:**
- Modify: `FLOW_CANVAS_B_PLAN_TASKS.md`
- Verify: `FormMain.cs`、`FormMain.Designer.cs`、`ExternalProgramLauncher.cs`、`TDJS-Vision.csproj`

**Interfaces:**
- Consumes: Task 2 已通过的授权程序启动功能。
- Produces: 可追溯任务记录和构建验证结果。

- [ ] **Step 1: 添加任务记录**

在 `FLOW_CANVAS_B_PLAN_TASKS.md` 末尾添加：

```markdown
## 2026-07-13: 主窗体授权程序启动

- [x] `FormMain` 的“授权”菜单点击后从软件运行目录启动 `activate.exe`，不依赖当前工作目录且不等待子进程退出。
- [x] 新增可插拔 `IExternalProgramLauncher` 接口及默认 `ExternalProgramLauncher` 实现，窗体只负责路径、存在性检查和界面反馈。
- [x] `activate.exe` 不存在或启动失败时显示简体中文错误提示，避免异常终止主程序。
- [x] “授权”菜单点击事件在 `FormMain.Designer.cs` 中绑定，控件继续可在 WinForm 设计器中查看。
- [x] 新增 `FormMainAuthorizationMenu.Tests.ps1` 回归测试，覆盖事件绑定、运行目录路径、中文错误提示和启动器实现约束。
```

- [ ] **Step 2: 运行相关回归测试**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tests\FormMainAuthorizationMenu.Tests.ps1`

Expected: PASS。

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tests\FormMainUnsupervisedMenu.Tests.ps1`

Expected: PASS，确认构造方法调整未破坏现有训练菜单约束。

- [ ] **Step 3: 检查补丁格式**

Run: `git diff --check`

Expected: 退出码 0，无空白错误。

- [ ] **Step 4: 构建解决方案**

Run: `& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "D:\MyCode\PublicWook\TDJS-Vision\TDJS-Vision.sln" /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /m`

Expected: 构建成功，0 个错误；既有警告数量单独记录，不将既有警告误报为本次回归。

- [ ] **Step 5: 回填验证结果并复查改动范围**

把实际测试、构建结果补充到 `FLOW_CANVAS_B_PLAN_TASKS.md`，再运行 `git status --short` 和 `git diff --stat`，确认未覆盖用户无关改动。
