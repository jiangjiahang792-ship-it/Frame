# 复合测量双采样模式与多目标串行统一实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为四种复合测量工具增加可独立保存的快速/抗干扰采样模式，并把卡尺找圆改回多目标串行执行，使全部测量业务工具统一使用串行公共执行入口。

**Architecture:** 复用现有 `CaliperSamplingMode` 和公共卡尺算法。包含两套卡尺的工具把模式保存在各自节点参数与 `RoiRunSettings` 中，用同一个 Designer 下拉框跟随“运行ROI”切换；点区域距离直接保存单个模式。业务测量节点统一调用 `MultiTargetMeasurementRunner.Run`，保留但不调用公共 `RunParallel`。

**Tech Stack:** C#、.NET Framework 4.8、WinForms Designer、OpenCvSharp、Newtonsoft.Json、PowerShell 源码契约测试、MSBuild Debug|x64。

## Global Constraints

- 卡尺找线、卡尺找圆、卡尺找椭圆、找点、线线夹角、点点距离、点线距离、点区域距离的多个模板目标统一串行执行。
- 公共 `MultiTargetMeasurementRunner.RunParallel` 暂不删除，但上述业务窗体不得调用。
- `CaliperSamplingMode.Fast` 保持枚举零值；旧方案缺少字段时必须进入快速采样。
- 只有明确的 `AntiInterference` 值进入宽度平均双线性采样；未知值回落快速采样。
- 线线夹角、点点距离、点线距离的两套内部卡尺分别保存模式，界面下拉框跟随“运行ROI”切换。
- 点区域距离只给点圆卡尺保存一个模式，区域几何计算不增加模式。
- 找点算法不增加卡尺采样模式。
- 不改变位置修正、ROI 仿射、失败占位、结果数量、结果顺序和总体状态规则。
- 新增 WinForms 控件必须位于对应 `.Designer.cs`，控件文本使用简体中文。
- 新增属性、缓存字段和重要分支必须添加简体中文注释。

---

## 文件结构

- `Tests/CompositeMeasurementSamplingAndSerial.Tests.ps1`：约束四种复合工具的参数、传递、Designer 和八种测量工具串行入口。
- `Tests/MultiTargetCaliper.Tests.ps1`：把卡尺找圆并发断言更新为串行断言。
- 四个 `NodeParam*.cs`：保存快速模式默认值以及两套卡尺的独立模式。
- 四个 `NodeParamForm*.cs`：完成 ROI 缓存、界面切换、位置修正复制和公共算法参数传递。
- 四个对应 `.Designer.cs`：声明和布局中文采样模式控件。
- `Node/4-Measurement/CaliperCircle/NodeParamFormCaliperCircle.cs`：从 `RunParallel` 改为 `Run`。
- `FLOW_CANVAS_B_PLAN_TASKS.md`：记录红绿测试、兼容、回归和构建结果。

### Task 1: 双采样和串行行为失败测试

**Files:**
- Create: `Tests/CompositeMeasurementSamplingAndSerial.Tests.ps1`
- Modify: `Tests/MultiTargetCaliper.Tests.ps1`

**Interfaces:**
- Consumes: `CaliperSamplingMode`、四类复合节点参数、八类业务测量窗体。
- Produces: 参数默认值、独立模式、控件传递和串行入口的可重复源码契约。

- [ ] **Step 1: 创建复合测量失败测试**

创建脚本并实现 `Assert-ContainsText`、`Assert-NotContainsText`。对四个参数文件断言以下属性：

```powershell
$parameterExpectations = @{
    LineLineAngle = @('Line1SamplingMode', 'Line2SamplingMode')
    PointPointDistance = @('Point1SamplingMode', 'Point2SamplingMode')
    PointLineDistance = @('PointSamplingMode', 'LineSamplingMode')
    PointRegionDistance = @('SamplingMode')
}

foreach ($entry in $parameterExpectations.GetEnumerator()) {
    $path = Join-Path $projectRoot ("Node\4-Measurement\{0}\NodeParam{0}.cs" -f $entry.Key)
    $content = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    foreach ($property in $entry.Value) {
        Assert-ContainsText $content ("CaliperSamplingMode {0} {{ get; set; }} = CaliperSamplingMode.Fast;" -f $property) ($entry.Key + ' must default ' + $property + ' to fast.')
    }
}
```

对前三个双 ROI 窗体断言 `RoiRunSettings.SamplingMode`、从参数加载、加载到 `comboBoxSamplingMode`、保存回缓存、位置修正复制和 `Build*Params` 传递。对点区域距离断言直接加载、保存、复制和算法传递。

- [ ] **Step 2: 约束 Designer 中文控件**

四个 Designer 必须包含：

```powershell
Assert-ContainsText $designer 'this.groupBoxRunParams.Controls.Add(this.comboBoxSamplingMode);' 'Sampling selector must be in Designer.'
Assert-ContainsText $designer 'this.labelSamplingMode.Text = "采样模式";' 'Sampling label must use simplified Chinese.'
Assert-ContainsText $designer 'private System.Windows.Forms.ComboBox comboBoxSamplingMode;' 'Designer must declare sampling selector.'
```

脚本构造中文断言文本时沿用 `CaliperSamplingMode.Tests.ps1` 的 Unicode 字符拼接，避免 Windows PowerShell 无 BOM 解析乱码。

- [ ] **Step 3: 约束所有业务测量工具串行执行**

```powershell
$serialTools = @(
    'CaliperLine', 'CaliperCircle', 'CaliperEllipse', 'FindPoint',
    'LineLineAngle', 'PointPointDistance', 'PointLineDistance', 'PointRegionDistance'
)
foreach ($tool in $serialTools) {
    $form = Get-Content -LiteralPath (Join-Path $projectRoot ("Node\4-Measurement\{0}\NodeParamForm{0}.cs" -f $tool)) -Raw -Encoding UTF8
    Assert-ContainsText $form 'MultiTargetMeasurementRunner.Run(' ($tool + ' must use serial multi-target execution.')
    Assert-NotContainsText $form 'MultiTargetMeasurementRunner.RunParallel(' ($tool + ' must not use parallel multi-target execution.')
}
```

- [ ] **Step 4: 更新既有卡尺测试期望**

把 `MultiTargetCaliper.Tests.ps1` 中卡尺找圆的 `RunParallel` 断言改为：

```powershell
Assert-ContainsText $circleForm 'MultiTargetMeasurementRunner.Run(' 'CaliperCircle must use serial target execution.'
Assert-NotContainsText $circleForm 'MultiTargetMeasurementRunner.RunParallel(' 'CaliperCircle must not use target-level parallel execution.'
Assert-NotContainsText $circleForm 'Parallel.For' 'CaliperCircle must delegate target ordering to the common serial runner.'
```

- [ ] **Step 5: 运行测试并确认按预期失败**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "Tests\CompositeMeasurementSamplingAndSerial.Tests.ps1"
```

Expected: FAIL，首先指出 `NodeParamLineLineAngle` 缺少 `Line1SamplingMode`，证明测试能够捕获未实现功能。

### Task 2: 四种复合测量参数和运行传递

**Files:**
- Modify: `Node/4-Measurement/LineLineAngle/NodeParamLineLineAngle.cs`
- Modify: `Node/4-Measurement/LineLineAngle/NodeParamFormLineLineAngle.cs`
- Modify: `Node/4-Measurement/PointPointDistance/NodeParamPointPointDistance.cs`
- Modify: `Node/4-Measurement/PointPointDistance/NodeParamFormPointPointDistance.cs`
- Modify: `Node/4-Measurement/PointLineDistance/NodeParamPointLineDistance.cs`
- Modify: `Node/4-Measurement/PointLineDistance/NodeParamFormPointLineDistance.cs`
- Modify: `Node/4-Measurement/PointRegionDistance/NodeParamPointRegionDistance.cs`
- Modify: `Node/4-Measurement/PointRegionDistance/NodeParamFormPointRegionDistance.cs`

**Interfaces:**
- Consumes: `CaliperSamplingMode`、现有 `RoiRunSettings` 和 `BuildLineParams`/`BuildCircleParams`。
- Produces: 可序列化的独立模式以及界面到公共算法的完整传递链。

- [ ] **Step 1: 增加节点参数默认值和读取方法**

线线夹角：

```csharp
/// <summary>获取或设置直线1卡尺的灰度剖面采样模式。</summary>
public CaliperSamplingMode Line1SamplingMode { get; set; } = CaliperSamplingMode.Fast;
/// <summary>获取或设置直线2卡尺的灰度剖面采样模式。</summary>
public CaliperSamplingMode Line2SamplingMode { get; set; } = CaliperSamplingMode.Fast;
/// <summary>获取指定直线卡尺的采样模式。</summary>
public CaliperSamplingMode GetSamplingMode(bool firstLine) => firstLine ? Line1SamplingMode : Line2SamplingMode;
```

点点距离使用 `Point1SamplingMode`、`Point2SamplingMode` 和 `GetSamplingMode(bool firstPoint)`；点线距离使用 `PointSamplingMode`、`LineSamplingMode` 和 `GetSamplingMode(bool point)`；点区域距离使用单个 `SamplingMode`。

- [ ] **Step 2: 扩展双 ROI 运行缓存**

在前三个窗体的 `RoiRunSettings` 增加：

```csharp
/// <summary>获取或设置当前运行 ROI 的灰度剖面采样模式。</summary>
public CaliperSamplingMode SamplingMode { get; set; } = CaliperSamplingMode.Fast;
```

`LoadRunSettingsFromParam` 分别加载两套参数；`LoadRunSettingsToControls` 只有明确抗干扰时选择索引1；`SaveCurrentRunSettingsFromControls` 把索引1保存为抗干扰，其余保存快速模式。

- [ ] **Step 3: 初始化和切换中文选项**

四个 `InitializeCombos()` 增加：

```csharp
comboBoxSamplingMode.Items.Add("快速采样");
comboBoxSamplingMode.Items.Add("抗干扰采样");
comboBoxSamplingMode.SelectedIndex = 0;
```

前三个窗体继续复用现有 `comboBoxRunRoiTarget_SelectedIndexChanged`：保存当前缓存后加载新目标缓存，不增加第二组采样控件。

- [ ] **Step 4: 复制位置修正运行参数并保存节点参数**

位置修正只原样复制模式：

```csharp
Line1SamplingMode = param.Line1SamplingMode,
Line2SamplingMode = param.Line2SamplingMode,
```

另外两种双 ROI 工具使用各自属性名；点区域距离复制 `SamplingMode = param.SamplingMode`。`SaveParams` 从对应 ROI 缓存写回两套属性，点区域距离从下拉框直接写回。

- [ ] **Step 5: 传给公共卡尺算法**

所有 `BuildLineParams` 和 `BuildCircleParams` 增加对应模式，例如：

```csharp
SamplingMode = param.GetSamplingMode(firstLine),
```

点区域距离使用 `SamplingMode = param.SamplingMode`。

- [ ] **Step 6: 运行测试并确认失败推进到 Designer**

Run: Task 1 的新测试。

Expected: 参数与传递断言通过，下一条失败明确指出 Designer 缺少 `comboBoxSamplingMode`。

### Task 3: 四个 WinForms Designer 采样控件

**Files:**
- Modify: `Node/4-Measurement/LineLineAngle/NodeParamFormLineLineAngle.Designer.cs`
- Modify: `Node/4-Measurement/PointPointDistance/NodeParamFormPointPointDistance.Designer.cs`
- Modify: `Node/4-Measurement/PointLineDistance/NodeParamFormPointLineDistance.Designer.cs`
- Modify: `Node/4-Measurement/PointRegionDistance/NodeParamFormPointRegionDistance.Designer.cs`

**Interfaces:**
- Consumes: 四个窗体代码使用的 `labelSamplingMode` 和 `comboBoxSamplingMode`。
- Produces: 设计器可见的中文采样模式选择器。

- [ ] **Step 1: 声明并加入算法参数组**

四个 Designer 的 `InitializeComponent` 创建控件，加入 `groupBoxRunParams.Controls`，字段区声明：

```csharp
private System.Windows.Forms.Label labelSamplingMode;
private System.Windows.Forms.ComboBox comboBoxSamplingMode;
```

- [ ] **Step 2: 布局双 ROI 工具控件**

线线夹角、点点距离、点线距离在运行 ROI 行下方增加：

```csharp
this.labelSamplingMode.AutoSize = true;
this.labelSamplingMode.Location = new System.Drawing.Point(18, 275);
this.labelSamplingMode.Name = "labelSamplingMode";
this.labelSamplingMode.Size = new System.Drawing.Size(80, 18);
this.labelSamplingMode.Text = "采样模式";

this.comboBoxSamplingMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
this.comboBoxSamplingMode.Location = new System.Drawing.Point(104, 270);
this.comboBoxSamplingMode.Name = "comboBoxSamplingMode";
this.comboBoxSamplingMode.Size = new System.Drawing.Size(108, 26);
```

线线夹角和点点距离保留 `groupBoxRunParams` 高度307；点线距离从286增至307，防止控件被裁剪。

- [ ] **Step 3: 布局点区域距离控件**

点区域距离在方向右侧的空位增加采样模式，标签位置 `(227, 240)`，下拉框位置 `(310, 235)`、大小 `(78, 26)`，保留算法参数组原高度。

- [ ] **Step 4: 运行新测试并确认采样部分通过**

Run: Task 1 的新测试。

Expected: 采样参数、传递和 Designer 断言通过，只剩卡尺找圆串行断言失败。

### Task 4: 卡尺找圆回退串行并完成回归

**Files:**
- Modify: `Node/4-Measurement/CaliperCircle/NodeParamFormCaliperCircle.cs`
- Modify: `Tests/MultiTargetCaliper.Tests.ps1`

**Interfaces:**
- Consumes: 公共 `MultiTargetMeasurementRunner.Run`。
- Produces: 全部业务测量工具统一串行的调用行为。

- [ ] **Step 1: 卡尺找圆改用串行入口**

把唯一业务调用：

```csharp
return MultiTargetMeasurementRunner.RunParallel(
```

改为：

```csharp
// 多目标统一按模板顺序串行测量，避免业务工具之间出现不同调度语义。
return MultiTargetMeasurementRunner.Run(
```

- [ ] **Step 2: 完成既有测试期望更新**

应用 Task 1 Step 4 的串行断言，保留单次灰度图获取、高精度墙钟计时、结果顺序和失败隔离断言。

- [ ] **Step 3: 运行新测试和卡尺测试**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "Tests\CompositeMeasurementSamplingAndSerial.Tests.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "Tests\MultiTargetCaliper.Tests.ps1"
```

Expected: 2/2 PASS。

- [ ] **Step 4: 提交实现**

```powershell
git add -- "Tests/CompositeMeasurementSamplingAndSerial.Tests.ps1" "Tests/MultiTargetCaliper.Tests.ps1" "Node/4-Measurement/CaliperCircle/NodeParamFormCaliperCircle.cs" "Node/4-Measurement/LineLineAngle" "Node/4-Measurement/PointPointDistance" "Node/4-Measurement/PointLineDistance" "Node/4-Measurement/PointRegionDistance"
git commit -m "feat: 统一复合测量采样与串行执行"
```

### Task 5: 兼容、专项回归、构建和任务记录

**Files:**
- Modify: `FLOW_CANVAS_B_PLAN_TASKS.md`

**Interfaces:**
- Consumes: Task 1至4完成的参数、界面、算法传递和串行入口。
- Produces: 可审计的兼容、测试和构建结论。

- [ ] **Step 1: 验证旧方案默认值**

使用 Newtonsoft.Json 对四类节点参数反序列化 `{}`，断言全部新增模式为枚举值0；重新序列化断言字段写出0。只读检查用户旧 `.Sol` 不包含本次新增字段，不覆盖原方案。

- [ ] **Step 2: 运行专项回归**

至少运行：

```powershell
$tests=@(
  'Tests\CompositeMeasurementSamplingAndSerial.Tests.ps1',
  'Tests\CaliperSamplingMode.Tests.ps1',
  'Tests\PositionCorrectionSubscriptionCompatibility.Tests.ps1',
  'Tests\MultiTargetTransform.Tests.ps1',
  'Tests\MultiTargetPositionCorrectionNode.Tests.ps1',
  'Tests\MultiTargetMeasurementRunner.Tests.ps1',
  'Tests\MultiTargetCaliper.Tests.ps1',
  'Tests\MultiTargetFindPoint.Tests.ps1',
  'Tests\MultiTargetGeometryMeasurement.Tests.ps1'
)
```

Expected: 9/9 PASS。公共执行器测试继续覆盖保留的 `RunParallel`，业务工具测试确认没有调用它。

- [ ] **Step 3: 执行 Debug|x64 非增量构建**

Run:

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "D:\MyCode\PublicWook\TDJS-Vision\TDJS-Vision.sln" /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /m:1 /nr:false
```

Expected: 0 个错误；既有警告数量如实记录。

- [ ] **Step 4: 更新任务记录并提交**

在 `FLOW_CANVAS_B_PLAN_TASKS.md` 完成本任务剩余勾选，记录红绿过程、旧方案默认值、9项测试结果和构建结果。

```powershell
git diff --check
git add -- FLOW_CANVAS_B_PLAN_TASKS.md
git commit -m "docs: 记录复合测量采样与串行验证"
git status --short
```

Expected: 提交成功后工作区干净。
