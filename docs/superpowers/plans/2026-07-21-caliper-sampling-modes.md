# 卡尺快速采样与抗干扰采样双模式实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为卡尺找线、找圆和找椭圆增加可独立保存的快速采样与抗干扰采样模式，默认快速采样并兼容旧方案。

**Architecture:** 在公共卡尺算法参数中增加零值为快速模式的枚举，由边缘剖面生成函数选择旧版单中心线取样或当前宽度平均双线性取样。三个卡尺节点参数和参数窗体逐层传递该枚举，界面以中文下拉框显示，其他复用公共算法但未提供控件的测量工具自然使用快速模式默认值。

**Tech Stack:** C#、.NET Framework 4.8、WinForms Designer、OpenCvSharp、Newtonsoft.Json、PowerShell 契约测试、MSBuild Debug|x64。

## Global Constraints

- `CaliperSamplingMode.Fast` 必须固定为枚举零值，旧 `.Sol` 缺少字段时自动进入快速模式。
- 快速模式必须使用旧版中心线单像素最近邻采样，不得执行宽度平均或双线性插值。
- 抗干扰模式必须保持当前宽度平均、双线性插值、最大半宽限制和越界行为。
- 未知枚举值必须回落到快速模式。
- 卡尺找线、找圆和找椭圆必须分别保存自己的模式。
- 三个 WinForms 控件必须添加到对应 `.Designer.cs`，控件文本使用简体中文。
- 不改变位置修正、ROI 仿射、多目标结果顺序、失败隔离和并发策略。
- 所有新增枚举、属性、方法和重要分支使用简体中文 XML 或行内注释。

---

## 文件结构

- `Node/4-Measurement/Common/CaliperMeasurementAlgorithm.cs`：定义采样模式，保存公共参数，并在剖面生成时切换两种采样算法。
- `Node/4-Measurement/CaliperLine/NodeParamCaliperLine.cs`：保存卡尺找线模式。
- `Node/4-Measurement/CaliperCircle/NodeParamCaliperCircle.cs`：保存卡尺找圆模式。
- `Node/4-Measurement/CaliperEllipse/NodeParamCaliperEllipse.cs`：保存卡尺找椭圆模式。
- `Node/4-Measurement/CaliperLine/NodeParamFormCaliperLine.cs`：加载、保存并传递找线模式。
- `Node/4-Measurement/CaliperCircle/NodeParamFormCaliperCircle.cs`：加载、保存并传递找圆模式。
- `Node/4-Measurement/CaliperEllipse/NodeParamFormCaliperEllipse.cs`：加载、保存并传递找椭圆模式。
- 三个对应的 `.Designer.cs`：声明并布局中文采样模式控件。
- `Tests/CaliperSamplingMode.Tests.ps1`：约束算法分支、默认值、参数传递和设计器控件。
- `FLOW_CANVAS_B_PLAN_TASKS.md`：记录红绿测试、构建、兼容和性能结果。

### Task 1: 公共算法双采样分支

**Files:**
- Create: `Tests/CaliperSamplingMode.Tests.ps1`
- Modify: `Node/4-Measurement/Common/CaliperMeasurementAlgorithm.cs`

**Interfaces:**
- Consumes: `Mat`、卡尺宽度、扫描方向和现有边缘查找参数。
- Produces: `CaliperSamplingMode`、三类公共参数的 `SamplingMode`，以及根据模式生成灰度剖面的公共算法路径。

- [ ] **Step 1: 写入算法模式失败测试**

创建 `Tests/CaliperSamplingMode.Tests.ps1`：

```powershell
$ErrorActionPreference = 'Stop'

function Assert-ContainsText {
    param([string]$Content, [string]$Expected, [string]$Message)
    if (-not $Content.Contains($Expected)) { throw $Message }
}

function Assert-MatchText {
    param([string]$Content, [string]$Pattern, [string]$Message)
    if ($Content -notmatch $Pattern) { throw $Message }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$algorithmPath = Join-Path $projectRoot 'Node\4-Measurement\Common\CaliperMeasurementAlgorithm.cs'
$algorithm = Get-Content -LiteralPath $algorithmPath -Raw -Encoding UTF8

Assert-MatchText $algorithm 'public\s+enum\s+CaliperSamplingMode\s*\{[^}]*Fast\s*=\s*0[^}]*AntiInterference\s*=\s*1' 'Sampling enum must keep Fast at zero and AntiInterference at one.'
foreach ($type in @('CaliperLineParams', 'CaliperCircleParams', 'CaliperEllipseParams')) {
    $start = $algorithm.IndexOf('public class ' + $type)
    $next = $algorithm.IndexOf('public class ', $start + 13)
    $body = $algorithm.Substring($start, $next - $start)
    Assert-ContainsText $body 'CaliperSamplingMode SamplingMode { get; set; } = CaliperSamplingMode.Fast;' ($type + ' must default to fast sampling.')
}

Assert-ContainsText $algorithm 'p.SamplingMode' 'Every caliper algorithm must pass its sampling mode to profile generation.'
Assert-ContainsText $algorithm 'SampleFastPixel(gray, px, py)' 'Profile generation must expose the legacy fast center-line path.'
Assert-ContainsText $algorithm 'samplingMode == CaliperSamplingMode.AntiInterference' 'Only the explicit anti-interference value may use averaged sampling.'
Assert-ContainsText $algorithm 'SampleAveragedPixel(gray, px, py, averageDirX, averageDirY, averageWidth)' 'Anti-interference mode must retain averaged bilinear sampling.'

$nodeParamFiles = @(
    'CaliperLine\NodeParamCaliperLine.cs',
    'CaliperCircle\NodeParamCaliperCircle.cs',
    'CaliperEllipse\NodeParamCaliperEllipse.cs'
)
foreach ($relative in $nodeParamFiles) {
    $content = Get-Content -LiteralPath (Join-Path $projectRoot ('Node\4-Measurement\' + $relative)) -Raw -Encoding UTF8
    Assert-ContainsText $content 'CaliperSamplingMode SamplingMode { get; set; } = CaliperSamplingMode.Fast;' ($relative + ' must persist fast sampling by default.')
}

$tools = @('CaliperLine', 'CaliperCircle', 'CaliperEllipse')
foreach ($tool in $tools) {
    $folder = Join-Path $projectRoot ('Node\4-Measurement\' + $tool)
    $form = Get-Content -LiteralPath (Join-Path $folder ('NodeParamForm' + $tool + '.cs')) -Raw -Encoding UTF8
    $designer = Get-Content -LiteralPath (Join-Path $folder ('NodeParamForm' + $tool + '.Designer.cs')) -Raw -Encoding UTF8
    Assert-ContainsText $form 'comboBoxSamplingMode.Items.Add("快速采样");' ($tool + ' must show the fast Chinese option.')
    Assert-ContainsText $form 'comboBoxSamplingMode.Items.Add("抗干扰采样");' ($tool + ' must show the anti-interference Chinese option.')
    Assert-ContainsText $form 'param.SamplingMode == CaliperSamplingMode.AntiInterference ? 1 : 0' ($tool + ' must load invalid or fast values as fast.')
    Assert-ContainsText $form 'SamplingMode = comboBoxSamplingMode.SelectedIndex == 1' ($tool + ' must save the selected mode.')
    Assert-ContainsText $form 'SamplingMode = param.SamplingMode' ($tool + ' must preserve the mode during ROI transformation.')
    Assert-ContainsText $form 'SamplingMode = runtimeParam.SamplingMode' ($tool + ' must pass the mode to the public algorithm parameter.')
    Assert-ContainsText $designer 'this.groupBoxCaliper.Controls.Add(this.comboBoxSamplingMode);' ($tool + ' sampling selector must live in the Designer file.')
    Assert-ContainsText $designer 'this.labelSamplingMode.Text = "采样模式";' ($tool + ' sampling label must use simplified Chinese.')
    Assert-ContainsText $designer 'private System.Windows.Forms.ComboBox comboBoxSamplingMode;' ($tool + ' Designer must declare the sampling selector.')
}

Write-Host 'Caliper sampling mode source checks passed.'
```

- [ ] **Step 2: 运行测试并确认按预期失败**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "Tests\CaliperSamplingMode.Tests.ps1"
```

Expected: FAIL，首先指出缺少 `CaliperSamplingMode`。

- [ ] **Step 3: 实现公共枚举和参数默认值**

在 `CaliperEdgeFindMode` 后增加：

```csharp
/// <summary>
/// 指定卡尺灰度剖面的采样方式。
/// </summary>
public enum CaliperSamplingMode
{
    /// <summary>只读取扫描中心线的单个最近邻像素，优先保证运行速度。</summary>
    Fast = 0,
    /// <summary>沿卡尺宽度进行多点双线性采样并求平均，优先保证抗干扰能力。</summary>
    AntiInterference = 1
}
```

在 `CaliperLineParams`、`CaliperCircleParams`、`CaliperEllipseParams` 中分别增加：

```csharp
/// <summary>获取或设置灰度剖面采样模式。</summary>
public CaliperSamplingMode SamplingMode { get; set; } = CaliperSamplingMode.Fast;
```

- [ ] **Step 4: 实现剖面采样分支**

三个 `FindLine`、`FindCircle`、`FindEllipse` 调用 `FindEdgeOnProfile` 时，在 `averageWidth` 后传入 `p.SamplingMode`。在 `FindEdgeOnProfile` 的同一位置增加 `CaliperSamplingMode samplingMode` 参数，并将剖面赋值改为：

```csharp
// 只有明确选择抗干扰模式才执行高成本宽度平均；未知枚举值安全回落到快速模式。
profile[j] = samplingMode == CaliperSamplingMode.AntiInterference
    ? SampleAveragedPixel(gray, px, py, averageDirX, averageDirY, averageWidth)
    : SampleFastPixel(gray, px, py);
```

在 `SampleAveragedPixel` 前增加旧版快速取样函数：

```csharp
/// <summary>
/// 使用旧版最近邻方式读取扫描中心线像素，避免宽度平均和双线性插值开销。
/// </summary>
private static float SampleFastPixel(Mat gray, float x, float y)
{
    int ix = (int)(x + 0.5f);
    int iy = (int)(y + 0.5f);
    return IsInside(gray, ix, iy) ? gray.At<byte>(iy, ix) : 0;
}
```

- [ ] **Step 5: 暂不要求全测试通过，确认失败推进到节点参数**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "Tests\CaliperSamplingMode.Tests.ps1"
```

Expected: FAIL，公共算法断言通过，下一条失败明确指出 `NodeParamCaliperLine` 缺少 `SamplingMode`。

### Task 2: 三种卡尺参数保存和算法传递

**Files:**
- Modify: `Node/4-Measurement/CaliperLine/NodeParamCaliperLine.cs`
- Modify: `Node/4-Measurement/CaliperCircle/NodeParamCaliperCircle.cs`
- Modify: `Node/4-Measurement/CaliperEllipse/NodeParamCaliperEllipse.cs`
- Modify: `Node/4-Measurement/CaliperLine/NodeParamFormCaliperLine.cs`
- Modify: `Node/4-Measurement/CaliperCircle/NodeParamFormCaliperCircle.cs`
- Modify: `Node/4-Measurement/CaliperEllipse/NodeParamFormCaliperEllipse.cs`

**Interfaces:**
- Consumes: Task 1 的 `CaliperSamplingMode` 和三类公共算法参数。
- Produces: 可序列化节点属性，以及界面、运行时仿射参数和算法参数之间的完整传递链。

- [ ] **Step 1: 为三个节点参数增加默认快速模式**

在三个 `NodeParamCaliper*` 类的 `BlurSize` 前增加：

```csharp
/// <summary>获取或设置卡尺灰度剖面采样模式。</summary>
public CaliperSamplingMode SamplingMode { get; set; } = CaliperSamplingMode.Fast;
```

- [ ] **Step 2: 为三个窗体增加中文选项和加载映射**

在每个 `InitializeCombos()` 末尾增加：

```csharp
comboBoxSamplingMode.Items.Add("快速采样");
comboBoxSamplingMode.Items.Add("抗干扰采样");
comboBoxSamplingMode.SelectedIndex = 0;
```

在每个 `SetParam2Form()` 设置方向后增加：

```csharp
comboBoxSamplingMode.SelectedIndex = param.SamplingMode == CaliperSamplingMode.AntiInterference ? 1 : 0;
```

- [ ] **Step 3: 传递运行时参数和算法参数**

在每个 `BuildRuntimeParam` 返回的新节点参数中增加：

```csharp
SamplingMode = param.SamplingMode,
```

在每个 `ExecuteOne` 构造的公共算法参数中增加：

```csharp
SamplingMode = runtimeParam.SamplingMode,
```

- [ ] **Step 4: 保存界面选择**

在每个 `SaveParams()` 构造节点参数时增加：

```csharp
SamplingMode = comboBoxSamplingMode.SelectedIndex == 1
    ? CaliperSamplingMode.AntiInterference
    : CaliperSamplingMode.Fast,
```

该条件保证下拉框未选中或方案含未知枚举时仍保存快速模式。

- [ ] **Step 5: 运行测试并确认失败推进到 Designer**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "Tests\CaliperSamplingMode.Tests.ps1"
```

Expected: FAIL，参数与传递断言通过，下一条失败明确指出 Designer 尚未声明 `comboBoxSamplingMode`。

### Task 3: 三个 WinForms Designer 控件

**Files:**
- Modify: `Node/4-Measurement/CaliperLine/NodeParamFormCaliperLine.Designer.cs`
- Modify: `Node/4-Measurement/CaliperCircle/NodeParamFormCaliperCircle.Designer.cs`
- Modify: `Node/4-Measurement/CaliperEllipse/NodeParamFormCaliperEllipse.Designer.cs`

**Interfaces:**
- Consumes: Task 2 窗体代码使用的 `labelSamplingMode` 和 `comboBoxSamplingMode`。
- Produces: 设计器可见的中文采样模式下拉框。

- [ ] **Step 1: 在三个 Designer 中创建并加入控件**

在 `InitializeComponent()` 的控件实例化区域增加：

```csharp
this.labelSamplingMode = new System.Windows.Forms.Label();
this.comboBoxSamplingMode = new System.Windows.Forms.ComboBox();
```

在 `groupBoxCaliper.Controls.Add` 区域增加：

```csharp
this.groupBoxCaliper.Controls.Add(this.labelSamplingMode);
this.groupBoxCaliper.Controls.Add(this.comboBoxSamplingMode);
```

在字段声明区域增加：

```csharp
private System.Windows.Forms.Label labelSamplingMode;
private System.Windows.Forms.ComboBox comboBoxSamplingMode;
```

- [ ] **Step 2: 布局卡尺找线采样模式**

卡尺找线在现有第四行下方还有空间，使用：

```csharp
this.labelSamplingMode.AutoSize = true;
this.labelSamplingMode.Location = new System.Drawing.Point(16, 200);
this.labelSamplingMode.Name = "labelSamplingMode";
this.labelSamplingMode.Size = new System.Drawing.Size(67, 15);
this.labelSamplingMode.TabIndex = 16;
this.labelSamplingMode.Text = "采样模式";

this.comboBoxSamplingMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
this.comboBoxSamplingMode.FormattingEnabled = true;
this.comboBoxSamplingMode.Location = new System.Drawing.Point(88, 196);
this.comboBoxSamplingMode.Name = "comboBoxSamplingMode";
this.comboBoxSamplingMode.Size = new System.Drawing.Size(120, 23);
this.comboBoxSamplingMode.TabIndex = 17;
```

- [ ] **Step 3: 布局找圆和找椭圆采样模式**

两个窗体保留第5行的拟合有效点数控件，把采样模式放在第6行：

```csharp
this.labelSamplingMode.AutoSize = true;
this.labelSamplingMode.Location = new System.Drawing.Point(16, 232);
this.labelSamplingMode.Name = "labelSamplingMode";
this.labelSamplingMode.Size = new System.Drawing.Size(67, 15);
this.labelSamplingMode.TabIndex = 19;
this.labelSamplingMode.Text = "采样模式";

this.comboBoxSamplingMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
this.comboBoxSamplingMode.FormattingEnabled = true;
this.comboBoxSamplingMode.Location = new System.Drawing.Point(88, 228);
this.comboBoxSamplingMode.Name = "comboBoxSamplingMode";
this.comboBoxSamplingMode.Size = new System.Drawing.Size(120, 23);
this.comboBoxSamplingMode.TabIndex = 20;
```

两个 `groupBoxCaliper.Size` 从 `390, 246` 调整为 `390, 276`，两个窗体 `ClientSize` 从 `962, 646` 调整为 `962, 676`，避免新增控件被裁剪。

- [ ] **Step 4: 运行双模式专项测试并确认通过**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "Tests\CaliperSamplingMode.Tests.ps1"
```

Expected: PASS，输出 `Caliper sampling mode source checks passed.`。

- [ ] **Step 5: 提交双模式实现**

```powershell
git add -- "Tests/CaliperSamplingMode.Tests.ps1" "Node/4-Measurement/Common/CaliperMeasurementAlgorithm.cs" "Node/4-Measurement/CaliperLine" "Node/4-Measurement/CaliperCircle" "Node/4-Measurement/CaliperEllipse"
git commit -m "perf: 增加卡尺双采样模式"
```

### Task 4: 兼容、回归、构建和性能记录

**Files:**
- Modify: `FLOW_CANVAS_B_PLAN_TASKS.md`

**Interfaces:**
- Consumes: Task 1至3完成的双模式算法、参数和界面。
- Produces: 可审计的回归结果、构建结果和性能结论。

- [ ] **Step 1: 验证旧方案兼容默认值**

读取 `C:\Users\34652\Desktop\222.Sol`，确认文件不包含 `SamplingMode`；加载后三个节点参数依靠枚举零值进入快速模式。保存新副本后确认每个三种卡尺节点均写入 `SamplingMode: 0`。如果当前自动化环境无法安全驱动交互保存，只执行只读确认，并在任务记录中明确由用户界面复测保存结果。

- [ ] **Step 2: 运行测量专项回归**

Run:

```powershell
$tests=@(
  'Tests\CaliperSamplingMode.Tests.ps1',
  'Tests\PositionCorrectionSubscriptionCompatibility.Tests.ps1',
  'Tests\MultiTargetTransform.Tests.ps1',
  'Tests\MultiTargetPositionCorrectionNode.Tests.ps1',
  'Tests\MultiTargetMeasurementRunner.Tests.ps1',
  'Tests\MultiTargetCaliper.Tests.ps1',
  'Tests\MultiTargetFindPoint.Tests.ps1',
  'Tests\MultiTargetGeometryMeasurement.Tests.ps1'
)
foreach($test in $tests){
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File $test
  if($LASTEXITCODE -ne 0){ throw "$test failed with exit code $LASTEXITCODE" }
}
```

Expected: 8/8 PASS。

- [ ] **Step 3: 完整编译 Debug|x64**

Run:

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "D:\MyCode\PublicWook\TDJS-Vision\TDJS-Vision.sln" /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /m
```

Expected: `0 个错误`；既有警告数量如实记录。

- [ ] **Step 4: 比较两种模式性能**

使用 `222.Sol`、同一张1280×1024纽扣灰度图、卡尺找圆 `Count=30`、宽10、高60。每种模式先热身5次，再记录至少20次高精度耗时，计算中位数和P95；同时记录单目标和5目标墙钟时间。快速模式必须显著低于抗干扰模式，实际数字如实写入任务记录；无法自动驱动正式方案时使用运行日志复核，不编造结果。

- [ ] **Step 5: 更新任务记录并提交**

把 `FLOW_CANVAS_B_PLAN_TASKS.md` 中本任务剩余项改为完成，并补充：

```markdown
- [x] 新增快速采样与抗干扰采样，旧方案默认进入快速模式。
- [x] 三种卡尺参数窗体增加设计器可见的中文采样模式下拉框。
- [x] 记录专项测试、Debug|x64构建、旧方案兼容和两种模式性能对比结果。
```

Run:

```powershell
git diff --check
git add -- "FLOW_CANVAS_B_PLAN_TASKS.md"
git commit -m "docs: 记录卡尺双模式验证"
git status --short
```

Expected: `git diff --check` 返回0，提交成功后工作区干净。
