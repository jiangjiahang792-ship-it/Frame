# 多目标位置修正与测量实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 模板匹配输出全部目标位姿，位置修正生成对应的变换集合，八类测量工具用唯一基准 ROI 对全部目标分别测量并输出多结果集合。

**Architecture:** 使用 `TemplateMatchPose` 传递模板目标位姿，使用可替换的 `IMultiTargetTransformService` 统一完成尺度规范化、目标归属、正向变换和逆向变换。测量节点复用现有单目标算法，通过公共多目标执行器逐目标构造运行参数、捕获单目标失败、保持结果顺序并合并绘制层。

**Tech Stack:** C# 7.3、.NET Framework 4.8、WinForms、OpenCvSharp、PowerShell 行为/契约测试、MSBuild `Debug|x64`。

## Global Constraints

- 所有中文内容必须使用简体中文和 UTF-8 编码。
- 新增或修改的属性、字段、方法、类和接口必须添加简体中文 XML 注释。
- WinForm 控件的声明、初始化、布局和事件绑定必须位于对应 `.Designer.cs` 文件中，控件文字使用中文。
- 新增服务必须接口化，默认实现可替换，测量算法不得复制八份公共变换逻辑。
- 多目标运行只展开“绘制模式＋启用位置修正”，订阅几何模式保持现状。
- 当前模板匹配的 `ScaleX`、`ScaleY` 固定输出 `1.0`；读取小于或等于 `0` 的尺度时按 `1.0` 处理。
- 单目标失败保留同序号结果项，所有数值写 `0`，节点总体为 NG，其他目标继续运行。
- 每个任务必须先看到专项测试因缺少功能而失败，再编写生产代码。
- 每次生产代码改动同步追加 `FLOW_CANVAS_B_PLAN_TASKS.md` 的阶段记录。

---

## 文件结构

### 新增文件

- `Node/3-Detection/MatchTemplate/TemplateMatchPose.cs`：模板目标位姿数据契约。
- `Node/4-Measurement/Common/MultiTargetTransformService.cs`：多目标坐标变换接口、默认实现和 ROI 归属判断。
- `Node/4-Measurement/Common/MultiTargetPositionCorrectionResult.cs`：基准位姿和当前全部目标变换的数据集合。
- `Node/4-Measurement/Common/MultiTargetMeasurementRunner.cs`：保持目标顺序、单项失败继续执行的通用运行器。
- `Tests/MultiTargetTransform.Tests.ps1`：变换、逆变换、尺度保护和目标归属行为测试。
- `Tests/MultiTargetPositionCorrectionNode.Tests.ps1`：模板位姿列表和位置修正集合契约测试。
- `Tests/MultiTargetMeasurementRunner.Tests.ps1`：多目标执行器行为测试。
- `Tests/MultiTargetCaliper.Tests.ps1`：三种卡尺多目标契约测试。
- `Tests/MultiTargetFindPoint.Tests.ps1`：找点多目标契约测试。
- `Tests/MultiTargetGeometryMeasurement.Tests.ps1`：四种几何测量多目标契约测试。

### 修改文件

- `TDJS-Vision.csproj`：登记全部新增 C# 文件。
- `Node/3-Detection/MatchTemplate/NodeResultMatchTemplate.cs`、`NodeMatchTemplate.cs`：输出全部模板位姿。
- `Node/4-Measurement/Common/PositionCorrectionInfo.cs`：增加目标序号、匹配框、得分和尺度。
- `Node/4-Measurement/PositionCorrection/*`：改为订阅位姿列表、以第一个目标创建基准并输出变换集合。
- `Node/4-Measurement/CaliperLine/*`、`CaliperCircle/*`、`CaliperEllipse/*`：多目标运行和结果集合。
- `Node/4-Measurement/FindPoint/*`：多目标区域变换、运行和结果集合。
- `Node/4-Measurement/LineLineAngle/*`、`PointPointDistance/*`、`PointLineDistance/*`、`PointRegionDistance/*`：绘制模式多目标运行和结果集合。
- `FLOW_CANVAS_B_PLAN_TASKS.md`：逐阶段记录实现与验证结果。

---

### Task 1: 模板位姿契约与公共变换服务

**Files:**
- Create: `Node/3-Detection/MatchTemplate/TemplateMatchPose.cs`
- Create: `Node/4-Measurement/Common/MultiTargetTransformService.cs`
- Modify: `Node/4-Measurement/Common/PositionCorrectionInfo.cs`
- Modify: `TDJS-Vision.csproj`
- Create: `Tests/MultiTargetTransform.Tests.ps1`
- Modify: `FLOW_CANVAS_B_PLAN_TASKS.md`

**Interfaces:**
- Produces: `TemplateMatchPose`、`IMultiTargetTransformService`、`MultiTargetTransformService`。
- Produces: `PositionCorrectionInfo.FromPoses(TemplateMatchPose basePose, TemplateMatchPose currentPose)`。
- Consumes: `System.Drawing.PointF`、模板匹配框中心/宽高/角度。

- [x] **Step 1: 写入失败行为测试**

在 `Tests/MultiTargetTransform.Tests.ps1` 中动态编译三个生产文件并验证：尺度 `0` 规范化为 `1`、平移旋转往返、ROI 中心落在第二个旋转框时返回索引 `1`、ROI 位于所有框外时选择最近目标。

```powershell
$service = New-Object TDJS_Vision.Node._4_Measurement.Common.MultiTargetTransformService
$basePose = [TDJS_Vision.Node._3_Detection.MatchTemplate.TemplateMatchPose]@{
    TargetIndex = 1; CenterX = 100; CenterY = 100; Angle = 0; ScaleX = 1; ScaleY = 1; Width = 40; Height = 40; IsValid = $true
}
$secondPose = [TDJS_Vision.Node._3_Detection.MatchTemplate.TemplateMatchPose]@{
    TargetIndex = 2; CenterX = 220; CenterY = 140; Angle = 30; ScaleX = 0; ScaleY = 0; Width = 40; Height = 40; IsValid = $true
}
$firstCorrection = [TDJS_Vision.Node._4_Measurement.Common.PositionCorrectionInfo]::FromPoses($basePose, $basePose)
$secondCorrection = [TDJS_Vision.Node._4_Measurement.Common.PositionCorrectionInfo]::FromPoses($basePose, $secondPose)
Assert-Near $secondCorrection.CurrentScaleX 1.0 '尺度0必须按1处理。'
$transformed = $service.TransformPoint([System.Drawing.PointF]::new(110, 100), $secondCorrection)
$restored = $service.InverseTransformPoint($transformed, $secondCorrection)
Assert-Near $restored.X 110 '正逆变换必须还原X。'
Assert-Near $restored.Y 100 '正逆变换必须还原Y。'
$index = $service.ResolveAnchorIndex([System.Drawing.PointF]::new(220, 140), @($firstCorrection, $secondCorrection))
Assert-True ($index -eq 1) 'ROI中心位于第二个目标时必须返回索引1。'
```

- [x] **Step 2: 运行测试并确认按预期失败**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\MultiTargetTransform.Tests.ps1`

Expected: FAIL，提示 `TemplateMatchPose` 或 `MultiTargetTransformService` 类型不存在。

- [x] **Step 3: 实现位姿数据契约**

在 `TemplateMatchPose.cs` 中实现带完整 XML 注释的类型：

```csharp
public sealed class TemplateMatchPose
{
    public int TargetIndex { get; set; }
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double Angle { get; set; }
    public double ScaleX { get; set; } = 1.0;
    public double ScaleY { get; set; } = 1.0;
    public double Score { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsValid { get; set; }
}
```

- [x] **Step 4: 扩展位置修正信息并实现公共服务**

`PositionCorrectionInfo` 增加 `TargetIndex`、`BaseScaleX/Y`、`CurrentScaleX/Y`、`TargetWidth/Height`、`Score`，并通过工厂方法统一防御尺度：

```csharp
public static PositionCorrectionInfo FromPoses(TemplateMatchPose basePose, TemplateMatchPose currentPose)
{
    if (basePose == null || currentPose == null)
        throw new ArgumentNullException();

    return new PositionCorrectionInfo
    {
        IsValid = basePose.IsValid && currentPose.IsValid,
        TargetIndex = currentPose.TargetIndex,
        BaseX = basePose.CenterX,
        BaseY = basePose.CenterY,
        BaseAngle = basePose.Angle,
        BaseScaleX = NormalizeScale(basePose.ScaleX),
        BaseScaleY = NormalizeScale(basePose.ScaleY),
        CurrentX = currentPose.CenterX,
        CurrentY = currentPose.CenterY,
        CurrentAngle = currentPose.Angle,
        CurrentScaleX = NormalizeScale(currentPose.ScaleX),
        CurrentScaleY = NormalizeScale(currentPose.ScaleY),
        TargetWidth = currentPose.Width,
        TargetHeight = currentPose.Height,
        Score = currentPose.Score
    };
}
```

`IMultiTargetTransformService` 必须提供以下精确签名：

```csharp
PointF TransformPoint(PointF point, PositionCorrectionInfo correction);
PointF InverseTransformPoint(PointF point, PositionCorrectionInfo correction);
List<PointF> TransformPoints(IEnumerable<PointF> points, PositionCorrectionInfo correction);
List<PointF> InverseTransformPoints(IEnumerable<PointF> points, PositionCorrectionInfo correction);
int ResolveAnchorIndex(PointF roiCenter, IReadOnlyList<PositionCorrectionInfo> corrections);
```

默认实现先把点移到基准局部坐标，按 `CurrentScale/BaseScale` 缩放，再按角度差旋转并平移到当前中心。逆变换严格按相反顺序执行。旋转框命中使用逆旋转后的局部点与半宽、半高比较，框外时使用中心欧氏距离选择最近目标。

- [x] **Step 5: 把新增文件登记到项目并记录任务**

在 `TDJS-Vision.csproj` 的模板匹配和测量公共文件区域分别加入：

```xml
<Compile Include="Node\3-Detection\MatchTemplate\TemplateMatchPose.cs" />
<Compile Include="Node\4-Measurement\Common\MultiTargetTransformService.cs" />
```

在任务记录中追加位姿契约和变换服务阶段完成项。

- [x] **Step 6: 运行测试并确认通过**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\MultiTargetTransform.Tests.ps1`

Expected: PASS，输出 `多目标位置变换行为检查通过。`

- [x] **Step 7: 提交本阶段**

```powershell
git add Tests/MultiTargetTransform.Tests.ps1 Node/3-Detection/MatchTemplate/TemplateMatchPose.cs Node/4-Measurement/Common/MultiTargetTransformService.cs Node/4-Measurement/Common/PositionCorrectionInfo.cs TDJS-Vision.csproj FLOW_CANVAS_B_PLAN_TASKS.md
git commit -m "feat: 增加多目标位姿变换基础"
```

---

### Task 2: 模板匹配位姿列表与位置修正集合

**Files:**
- Modify: `Node/3-Detection/MatchTemplate/NodeResultMatchTemplate.cs`
- Modify: `Node/3-Detection/MatchTemplate/NodeMatchTemplate.cs`
- Modify: `Node/4-Measurement/PositionCorrection/NodeParamPositionCorrection.cs`
- Modify: `Node/4-Measurement/PositionCorrection/NodeParamFormPositionCorrection.cs`
- Modify: `Node/4-Measurement/PositionCorrection/NodeParamFormPositionCorrection.Designer.cs`
- Modify: `Node/4-Measurement/PositionCorrection/NodePositionCorrection.cs`
- Modify: `Node/4-Measurement/PositionCorrection/NodeResultPositionCorrection.cs`
- Create: `Node/4-Measurement/Common/MultiTargetPositionCorrectionResult.cs`
- Modify: `TDJS-Vision.csproj`
- Create: `Tests/MultiTargetPositionCorrectionNode.Tests.ps1`
- Modify: `FLOW_CANVAS_B_PLAN_TASKS.md`

**Interfaces:**
- Consumes: `List<TemplateMatchPose>`、`PositionCorrectionInfo.FromPoses`。
- Produces: `NodeResultMatchTemplate.Poses`。
- Produces: `NodeResultPositionCorrection.Items`、`BasePose`、`Count`、`IsOk`。
- Produces: `MultiTargetPositionCorrectionResult`，由 `NodeResultPositionCorrection` 继承。

- [x] **Step 1: 写入失败契约测试**

`Tests/MultiTargetPositionCorrectionNode.Tests.ps1` 读取源码并断言：模板结果存在 `List<TemplateMatchPose> Poses`；`BuildResult` 遍历全部 `Matches` 并写入 `ScaleX = 1.0`、`ScaleY = 1.0`；位置修正参数只保存位姿列表订阅文本和基准位姿；位置修正结果存在 `List<PositionCorrectionInfo> Items`；Designer 中只有一个位姿列表订阅控件。

```powershell
Assert-Contains $templateResult 'List<TemplateMatchPose> Poses' '模板结果必须公开位姿列表。'
Assert-Contains $templateNode 'ScaleX = 1.0' '当前模型必须输出ScaleX=1。'
Assert-Contains $templateNode 'ScaleY = 1.0' '当前模型必须输出ScaleY=1。'
Assert-Contains $correctionResult 'List<PositionCorrectionInfo> Items' '位置修正必须输出变换集合。'
Assert-Contains $correctionForm 'poses[0]' '创建基准必须默认使用第一个目标。'
Assert-Contains $designer 'nodeSubscriptionPoses' '设计器必须包含位姿列表订阅控件。'
```

- [x] **Step 2: 运行测试并确认按预期失败**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\MultiTargetPositionCorrectionNode.Tests.ps1`

Expected: FAIL，首先报告模板结果缺少 `Poses`。

- [x] **Step 3: 输出全部模板位姿**

`NodeResultMatchTemplate` 增加：

```csharp
[DisplayName("匹配位姿列表")]
public List<TemplateMatchPose> Poses { get; set; } = new List<TemplateMatchPose>();
```

`NodeMatchTemplate.BuildResult` 用一次循环填充完整列表：

```csharp
for (int i = 0; i < matchResult.Matches.Count; i++)
{
    FastTemplateMatchInfo match = matchResult.Matches[i];
    nodeResult.Poses.Add(new TemplateMatchPose
    {
        TargetIndex = i + 1,
        CenterX = match.Box.CenterX,
        CenterY = match.Box.CenterY,
        Angle = match.Box.Angle,
        ScaleX = 1.0,
        ScaleY = 1.0,
        Score = match.Score * 100.0,
        Width = match.Box.Width,
        Height = match.Box.Height,
        IsValid = true
    });
}
```

删除位置修正对 `MatchX/MatchY/Angle` 三个单值的依赖；是否保留这些显示属性不得影响新链路。

- [x] **Step 4: 把位置修正窗体改为单一位姿列表订阅**

`NodeParamPositionCorrection` 使用以下字段：

```csharp
public string PoseText1 { get; set; }
public string PoseText2 { get; set; }
public bool HasBaseline { get; set; }
public TemplateMatchPose BasePose { get; set; }
```

在 `.Designer.cs` 中删除 X、Y、角度三个订阅控件，新增并布局 `nodeSubscriptionPoses`，标签文字为“模板目标位姿列表”。业务文件只调用 `nodeSubscriptionPoses.Init(node)`、`SetText` 和 `GetValue<List<TemplateMatchPose>>()`。

- [x] **Step 5: 构造并发布多目标位置修正结果**

新增公共数据类并让节点结果继承：

```csharp
public class MultiTargetPositionCorrectionResult
{
    public List<PositionCorrectionInfo> Items { get; set; } = new List<PositionCorrectionInfo>();
    public TemplateMatchPose BasePose { get; set; }
    public int Count { get { return Items.Count; } }
    public bool IsValid { get; set; }
}

public class NodeResultPositionCorrection : MultiTargetPositionCorrectionResult, INodeResult, IJudgmentResult
{
    public int RunTime { get; set; }
    public bool IsOk { get; set; }
    public bool JudgeOk { get; set; } = true;
}
```

在 `TDJS-Vision.csproj` 的测量公共区域加入：

```xml
<Compile Include="Node\4-Measurement\Common\MultiTargetPositionCorrectionResult.cs" />
```

创建基准读取 `poses[0]` 的副本。运行时过滤无效位姿并保持原始顺序：

```csharp
List<TemplateMatchPose> poses = ReadPoses();
TemplateMatchPose basePose = param.BasePose;
List<PositionCorrectionInfo> items = poses
    .Where(pose => pose != null && pose.IsValid)
    .Select(pose => PositionCorrectionInfo.FromPoses(basePose, pose))
    .ToList();
```

目标列表为空时输出空 `Items`、`IsOk = false`，使用简体中文日志说明“模板目标位姿列表为空”。

- [x] **Step 6: 运行专项测试和编译**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\MultiTargetPositionCorrectionNode.Tests.ps1`

Run: `& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "D:\MyCode\PublicWook\TDJS-Vision\TDJS-Vision.sln" /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /m`

Expected: 专项测试 PASS；编译 0 个错误。

- [x] **Step 7: 提交本阶段**

```powershell
git add Tests/MultiTargetPositionCorrectionNode.Tests.ps1 Node/3-Detection/MatchTemplate/NodeResultMatchTemplate.cs Node/3-Detection/MatchTemplate/NodeMatchTemplate.cs Node/4-Measurement/Common/MultiTargetPositionCorrectionResult.cs Node/4-Measurement/PositionCorrection TDJS-Vision.csproj FLOW_CANVAS_B_PLAN_TASKS.md
git commit -m "feat: 输出多目标位置修正集合"
```

---

### Task 3: 通用多目标测量执行器

**Files:**
- Create: `Node/4-Measurement/Common/MultiTargetMeasurementRunner.cs`
- Modify: `TDJS-Vision.csproj`
- Create: `Tests/MultiTargetMeasurementRunner.Tests.ps1`
- Modify: `FLOW_CANVAS_B_PLAN_TASKS.md`

**Interfaces:**
- Consumes: `IReadOnlyList<PositionCorrectionInfo>`、取消令牌、工具算法委托。
- Produces: `IMultiTargetMeasurementItem`、`MultiTargetMeasurementItemBase`、`MultiTargetMeasurementRunner.Run<TItem>`。

- [x] **Step 1: 写入失败行为测试**

测试使用三个变换项，第二项执行委托抛出异常，断言输出仍为三项、序号为 `1/2/3`、第二项失败且数值由失败工厂置 `0`、第三项仍执行；取消后不得继续生成普通失败项。

```powershell
$items = [TDJS_Vision.Node._4_Measurement.Common.MultiTargetMeasurementRunner]::Run(
    $corrections,
    [System.Threading.CancellationToken]::None,
    $execute,
    $failure)
Assert-True ($items.Count -eq 3) '输出数量必须与目标数量一致。'
Assert-True (-not $items[1].IsOk) '第二个目标必须保留失败项。'
Assert-True ($items[2].IsOk) '单目标失败后必须继续运行第三个目标。'
```

- [x] **Step 2: 运行测试并确认按预期失败**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\MultiTargetMeasurementRunner.Tests.ps1`

Expected: FAIL，提示 `MultiTargetMeasurementRunner` 不存在。

- [x] **Step 3: 实现公共结果接口和运行器**

```csharp
public interface IMultiTargetMeasurementItem
{
    int TargetIndex { get; set; }
    bool IsOk { get; set; }
    string ErrorMessage { get; set; }
    PositionCorrectionInfo Correction { get; set; }
}

public abstract class MultiTargetMeasurementItemBase : IMultiTargetMeasurementItem
{
    public int TargetIndex { get; set; }
    public bool IsOk { get; set; }
    public string ErrorMessage { get; set; }
    public PositionCorrectionInfo Correction { get; set; }
}

public static List<TItem> Run<TItem>(
    IReadOnlyList<PositionCorrectionInfo> corrections,
    CancellationToken token,
    Func<PositionCorrectionInfo, TItem> execute,
    Func<PositionCorrectionInfo, Exception, TItem> createFailure)
    where TItem : IMultiTargetMeasurementItem
```

实现要求：预分配容量；按输入顺序循环；每项前调用 `token.ThrowIfCancellationRequested()`；普通异常交给 `createFailure`；`OperationCanceledException` 直接重新抛出；强制写回 `TargetIndex` 和 `Correction`，避免工具遗漏公共字段。

- [x] **Step 4: 登记项目、记录任务并运行测试**

在项目文件中加入：

```xml
<Compile Include="Node\4-Measurement\Common\MultiTargetMeasurementRunner.cs" />
```

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\MultiTargetMeasurementRunner.Tests.ps1`

Expected: PASS，输出 `多目标测量执行器行为检查通过。`

- [x] **Step 5: 提交本阶段**

```powershell
git add Tests/MultiTargetMeasurementRunner.Tests.ps1 Node/4-Measurement/Common/MultiTargetMeasurementRunner.cs TDJS-Vision.csproj FLOW_CANVAS_B_PLAN_TASKS.md
git commit -m "feat: 增加多目标测量执行器"
```

---

### Task 4: 三种卡尺多目标测量

**Files:**
- Modify: `Node/4-Measurement/CaliperLine/NodeParamFormCaliperLine.cs`
- Modify: `Node/4-Measurement/CaliperLine/NodeCaliperLine.cs`
- Modify: `Node/4-Measurement/CaliperLine/NodeResultCaliperLine.cs`
- Modify: `Node/4-Measurement/CaliperCircle/NodeParamFormCaliperCircle.cs`
- Modify: `Node/4-Measurement/CaliperCircle/NodeCaliperCircle.cs`
- Modify: `Node/4-Measurement/CaliperCircle/NodeResultCaliperCircle.cs`
- Modify: `Node/4-Measurement/CaliperEllipse/NodeParamFormCaliperEllipse.cs`
- Modify: `Node/4-Measurement/CaliperEllipse/NodeCaliperEllipse.cs`
- Modify: `Node/4-Measurement/CaliperEllipse/NodeResultCaliperEllipse.cs`
- Create: `Tests/MultiTargetCaliper.Tests.ps1`
- Modify: `FLOW_CANVAS_B_PLAN_TASKS.md`

**Interfaces:**
- Consumes: `NodeResultPositionCorrection.Items`、`IMultiTargetTransformService`、`MultiTargetMeasurementRunner.Run<TItem>`。
- Produces: `NodeResultCaliperLine.Items`、`NodeResultCaliperCircle.Items`、`NodeResultCaliperEllipse.Items`。

- [x] **Step 1: 写入失败契约测试**

测试逐文件断言存在 `Items` 列表、`ExecuteMeasures`、只获取一次灰度 Mat、调用公共运行器、失败工厂把数值写 `0`、运行按钮使用多结果绘制；同时断言没有循环调用会重复获取输入图像的旧 `ExecuteMeasure`。

```powershell
Assert-Contains $lineResult 'List<CaliperLineTargetResult> Items' '找线结果必须输出多结果集合。'
Assert-Contains $lineForm 'ExecuteMeasures' '找线必须提供多目标执行入口。'
Assert-Contains $lineForm 'ResolveAnchorIndex' '找线保存ROI时必须自动识别所属目标。'
Assert-Contains $circleResult 'List<CaliperCircleTargetResult> Items' '找圆结果必须输出多结果集合。'
Assert-Contains $ellipseResult 'List<CaliperEllipseTargetResult> Items' '找椭圆结果必须输出多结果集合。'
Assert-Contains $lineNode 'Items.All(item => item.IsOk)' '节点总体状态必须由全部目标决定。'
```

- [x] **Step 2: 运行测试并确认按预期失败**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\MultiTargetCaliper.Tests.ps1`

Expected: FAIL，首先报告找线结果缺少 `Items`。

- [x] **Step 3: 定义三种卡尺目标结果项**

每个原结果文件增加目标项并把节点结果改为集合。找线项字段为 `EdgePointCount`、`EdgePoints`、`StartX/Y`、`EndX/Y`、`Length`、`Angle`、`AlgorithmMs`；找圆项为 `EdgePointCount`、`CenterX/Y`、`Radius`、`Diameter`、`AlgorithmMs`；找椭圆项为 `EdgePointCount`、`CenterX/Y`、`Width`、`Height`、`MajorAxis`、`MinorAxis`、`Angle`、`AlgorithmMs`。所有数值使用非空值类型，失败工厂显式赋 `0`。

```csharp
[DisplayName("目标测量结果")]
public List<CaliperLineTargetResult> Items { get; set; } = new List<CaliperLineTargetResult>();

[DisplayName("是否OK")]
public bool IsOk { get; set; }
```

- [x] **Step 4: 重构卡尺参数窗体为一次取图、多目标运行**

三个窗体都实现以下流程，但使用各自参数和算法：

```csharp
internal List<CaliperLineTargetResult> ExecuteMeasures(NodeParamCaliperLine param, CancellationToken token)
{
    bool disposeAfterUse;
    Mat gray = GetInputGrayMat(out disposeAfterUse);
    try
    {
        IReadOnlyList<PositionCorrectionInfo> corrections = ReadCorrections(param);
        return MultiTargetMeasurementRunner.Run(
            corrections,
            token,
            correction => ExecuteOne(gray, BuildRuntimeParam(param, correction), correction),
            CreateFailure);
    }
    finally
    {
        if (disposeAfterUse)
            gray?.Dispose();
    }
}
```

三个参数窗体必须分别提供以下精确私有方法，避免运行入口依赖控件状态以外的隐式变量：

```csharp
// 卡尺找线
private IReadOnlyList<PositionCorrectionInfo> ReadCorrections(NodeParamCaliperLine param);
private NodeParamCaliperLine BuildRuntimeParam(NodeParamCaliperLine param, PositionCorrectionInfo correction);
private CaliperLineTargetResult ExecuteOne(Mat gray, NodeParamCaliperLine runtimeParam, PositionCorrectionInfo correction);
private CaliperLineTargetResult CreateFailure(PositionCorrectionInfo correction, Exception exception);

// 卡尺找圆
private IReadOnlyList<PositionCorrectionInfo> ReadCorrections(NodeParamCaliperCircle param);
private NodeParamCaliperCircle BuildRuntimeParam(NodeParamCaliperCircle param, PositionCorrectionInfo correction);
private CaliperCircleTargetResult ExecuteOne(Mat gray, NodeParamCaliperCircle runtimeParam, PositionCorrectionInfo correction);
private CaliperCircleTargetResult CreateFailure(PositionCorrectionInfo correction, Exception exception);

// 卡尺找椭圆
private IReadOnlyList<PositionCorrectionInfo> ReadCorrections(NodeParamCaliperEllipse param);
private NodeParamCaliperEllipse BuildRuntimeParam(NodeParamCaliperEllipse param, PositionCorrectionInfo correction);
private CaliperEllipseTargetResult ExecuteOne(Mat gray, NodeParamCaliperEllipse runtimeParam, PositionCorrectionInfo correction);
private CaliperEllipseTargetResult CreateFailure(PositionCorrectionInfo correction, Exception exception);
```

`BuildRuntimeParam` 不再自行从订阅控件取单个修正信息，而是接收明确的 `PositionCorrectionInfo correction`。找线变换起终点；找圆变换圆心并叠加角度差；找椭圆变换中心并叠加角度差。

- [x] **Step 5: 实现唯一 ROI 的目标归属和标准化**

保存或运行按钮读取唯一动态 ROI 的几何中心，调用 `ResolveAnchorIndex`。用返回目标对应的 `InverseTransformPoint` 把起终点、圆心或椭圆中心转换为第一个基准坐标系。拖动过程不得调用多目标循环，也不得添加其他动态或静态 ROI。

- [x] **Step 6: 构建节点结果和合并绘制层**

节点用目标项集合构建结果：

```csharp
nodeResult.Items = items;
nodeResult.IsOk = items.Count > 0 && items.All(item => item.IsOk);
nodeResult.JudgeOk = nodeResult.IsOk;
nodeResult.Result = BuildDisplayResult(items);
nodeResult.OutputImage.DisplayResult = nodeResult.Result;
```

绘制层为每项增加“目标N”中文文本；失败项显示红色和数值 `0`，成功项绘制对应测量几何。参数窗体点击运行调用同一多目标入口并一次性更新预览。

- [x] **Step 7: 运行专项测试和编译**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\MultiTargetCaliper.Tests.ps1`

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\MultiTargetTransform.Tests.ps1`

Run: `& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "D:\MyCode\PublicWook\TDJS-Vision\TDJS-Vision.sln" /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /m`

Expected: 两个专项测试 PASS；编译 0 个错误。

- [x] **Step 8: 提交本阶段**

```powershell
git add Tests/MultiTargetCaliper.Tests.ps1 Node/4-Measurement/CaliperLine Node/4-Measurement/CaliperCircle Node/4-Measurement/CaliperEllipse FLOW_CANVAS_B_PLAN_TASKS.md
git commit -m "feat: 支持多目标卡尺测量"
```

---

### Task 5: 找点多目标测量

**Files:**
- Modify: `Node/4-Measurement/FindPoint/NodeParamFormFindPoint.cs`
- Modify: `Node/4-Measurement/FindPoint/NodeFindPoint.cs`
- Modify: `Node/4-Measurement/FindPoint/NodeResultFindPoint.cs`
- Create: `Tests/MultiTargetFindPoint.Tests.ps1`
- Modify: `FLOW_CANVAS_B_PLAN_TASKS.md`

**Interfaces:**
- Consumes: `MultiTargetMeasurementRunner`、`IMultiTargetTransformService.TransformPoints`。
- Produces: `NodeResultFindPoint.Items : List<FindPointTargetResult>`。

- [x] **Step 1: 写入失败契约测试**

断言找点结果包含 `List<FindPointTargetResult> Items`；运行参数为每个修正项变换所有 `Regions`；只读取一次输入灰度图；失败项 `CenterX/CenterY/PointCount/ContourCount/AlgorithmMs` 为 `0`；运行绘制层包含全部目标序号。

- [x] **Step 2: 运行测试并确认按预期失败**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\MultiTargetFindPoint.Tests.ps1`

Expected: FAIL，报告找点结果缺少多目标集合。

- [x] **Step 3: 实现找点目标结果项和运行参数变换**

`FindPointTargetResult` 包含公共字段以及 `PointCount`、`ContourCount`、`CenterX/Y`、`Points`、`RegionPoints`、`Contours`、`Message`、`AlgorithmMs`。`BuildRuntimeParam` 对每一个搜索区域调用：

```csharp
Regions = param.Regions
    .Select(region => transformService.TransformPoints(region, correction))
    .ToList();
```

找点窗体使用以下精确方法边界：

```csharp
internal List<FindPointTargetResult> ExecuteMeasures(NodeParamFindPoint param, CancellationToken token);
private IReadOnlyList<PositionCorrectionInfo> ReadCorrections(NodeParamFindPoint param);
private NodeParamFindPoint BuildRuntimeParam(NodeParamFindPoint param, PositionCorrectionInfo correction);
private FindPointTargetResult ExecuteOne(Mat gray, NodeParamFindPoint runtimeParam, PositionCorrectionInfo correction);
private FindPointTargetResult CreateFailure(PositionCorrectionInfo correction, Exception exception);
```

保存唯一多边形 ROI 时使用其几何中心解析所属目标，再把全部顶点逆变换到基准坐标系。

- [x] **Step 4: 实现多目标运行、失败填0和合并绘制**

一次获取输入 Mat，调用公共运行器逐目标执行现有 `FindPointAlgorithm`。失败工厂保留空点集、空轮廓、数值 `0` 和中文原因。节点总体状态使用 `items.Count > 0 && items.All(...)`。

- [x] **Step 5: 运行测试、编译并提交**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\MultiTargetFindPoint.Tests.ps1`

Run: `& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "D:\MyCode\PublicWook\TDJS-Vision\TDJS-Vision.sln" /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /m`

Expected: 专项测试 PASS；编译 0 个错误。

```powershell
git add Tests/MultiTargetFindPoint.Tests.ps1 Node/4-Measurement/FindPoint FLOW_CANVAS_B_PLAN_TASKS.md
git commit -m "feat: 支持多目标找点测量"
```

---

### Task 6: 四种几何测量多目标运行

**Files:**
- Modify: `Node/4-Measurement/LineLineAngle/NodeParamFormLineLineAngle.cs`
- Modify: `Node/4-Measurement/LineLineAngle/NodeLineLineAngle.cs`
- Modify: `Node/4-Measurement/LineLineAngle/NodeResultLineLineAngle.cs`
- Modify: `Node/4-Measurement/PointPointDistance/NodeParamFormPointPointDistance.cs`
- Modify: `Node/4-Measurement/PointPointDistance/NodePointPointDistance.cs`
- Modify: `Node/4-Measurement/PointPointDistance/NodeResultPointPointDistance.cs`
- Modify: `Node/4-Measurement/PointLineDistance/NodeParamFormPointLineDistance.cs`
- Modify: `Node/4-Measurement/PointLineDistance/NodePointLineDistance.cs`
- Modify: `Node/4-Measurement/PointLineDistance/NodeResultPointLineDistance.cs`
- Modify: `Node/4-Measurement/PointRegionDistance/NodeParamFormPointRegionDistance.cs`
- Modify: `Node/4-Measurement/PointRegionDistance/NodePointRegionDistance.cs`
- Modify: `Node/4-Measurement/PointRegionDistance/NodeResultPointRegionDistance.cs`
- Create: `Tests/MultiTargetGeometryMeasurement.Tests.ps1`
- Modify: `FLOW_CANVAS_B_PLAN_TASKS.md`

**Interfaces:**
- Consumes: 公共变换服务、公共运行器、现有几何测量算法。
- Produces: 四种节点各自的 `Items` 结果集合。

- [x] **Step 1: 写入失败契约测试**

逐工具断言：结果类存在目标项列表；只有 `SourceMode == MeasurementDataSourceMode.Draw && UsePositionCorrection` 才展开多目标；绘制点、线和区域逐点变换；订阅模式保持单次旧路径；失败项数值为 `0`；节点总体状态由全部目标决定。

```powershell
Assert-Contains $angleResult 'List<LineLineAngleTargetResult> Items' '线线角度必须输出多目标结果。'
Assert-Contains $pointPointForm 'MeasurementDataSourceMode.Draw' '点点距离只在绘制模式展开。'
Assert-Contains $pointLineForm 'MultiTargetMeasurementRunner.Run' '点线距离必须使用公共运行器。'
Assert-Contains $pointRegionForm 'TransformPoints' '点区域距离必须变换区域全部顶点。'
```

- [x] **Step 2: 运行测试并确认按预期失败**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\MultiTargetGeometryMeasurement.Tests.ps1`

Expected: FAIL，首先报告线线角度缺少 `Items`。

- [x] **Step 3: 实现四种目标结果项**

- `LineLineAngleTargetResult`：`Angle`、`IntersectionX/Y`、`AverageDistance`、`MinDistance`、`MaxDistance`、`DistancePointCount`、两条线四组端点、`AlgorithmMs`。
- `PointPointDistanceTargetResult`：`Distance`、两个点坐标、`AlgorithmMs`。
- `PointLineDistanceTargetResult`：`Distance`、目标点、线端点、垂足、`AlgorithmMs`。
- `PointRegionDistanceTargetResult`：`MinDistance`、`MaxDistance`、`IsInsideRegion`、目标点、最近点、最远点、区域点数、`AlgorithmMs`。

全部数值使用非空类型，失败工厂显式写 `0`。

- [x] **Step 4: 实现绘制模式的多目标参数生成**

每个窗体保留订阅模式原逻辑；绘制模式且启用位置修正时读取 `NodeResultPositionCorrection.Items` 并逐目标变换。四个公开给节点调用的多目标入口签名固定为：

```csharp
internal List<LineLineAngleTargetResult> ExecuteMeasures(NodeParamLineLineAngle param, CancellationToken token);
internal List<PointPointDistanceTargetResult> ExecuteMeasures(NodeParamPointPointDistance param, CancellationToken token);
internal List<PointLineDistanceTargetResult> ExecuteMeasures(NodeParamPointLineDistance param, CancellationToken token);
internal List<PointRegionDistanceTargetResult> ExecuteMeasures(NodeParamPointRegionDistance param, CancellationToken token);
```

每个入口都使用对应工具已有的单次测量方法构造订阅模式的一项结果；绘制多目标路径调用公共运行器：

```csharp
internal List<LineLineAngleTargetResult> ExecuteMeasures(NodeParamLineLineAngle param, CancellationToken token)
{
    if (param.SourceMode != MeasurementDataSourceMode.Draw || !param.UsePositionCorrection)
        return new List<LineLineAngleTargetResult> { ExecuteSubscribedOrSingleDraw(param, token) };
    return MultiTargetMeasurementRunner.Run(
        ReadCorrections(param), token,
        correction => ExecuteOne(BuildRuntimeParam(param, correction), correction, token),
        CreateFailure);
}

internal List<PointPointDistanceTargetResult> ExecuteMeasures(NodeParamPointPointDistance param, CancellationToken token)
{
    if (param.SourceMode != MeasurementDataSourceMode.Draw || !param.UsePositionCorrection)
        return new List<PointPointDistanceTargetResult> { ExecuteSubscribedOrSingleDraw(param, token) };
    return MultiTargetMeasurementRunner.Run(
        ReadCorrections(param), token,
        correction => ExecuteOne(BuildRuntimeParam(param, correction), correction, token),
        CreateFailure);
}

internal List<PointLineDistanceTargetResult> ExecuteMeasures(NodeParamPointLineDistance param, CancellationToken token)
{
    if (param.SourceMode != MeasurementDataSourceMode.Draw || !param.UsePositionCorrection)
        return new List<PointLineDistanceTargetResult> { ExecuteSubscribedOrSingleDraw(param, token) };
    return MultiTargetMeasurementRunner.Run(
        ReadCorrections(param), token,
        correction => ExecuteOne(BuildRuntimeParam(param, correction), correction, token),
        CreateFailure);
}

internal List<PointRegionDistanceTargetResult> ExecuteMeasures(NodeParamPointRegionDistance param, CancellationToken token)
{
    if (param.SourceMode != MeasurementDataSourceMode.Draw || !param.UsePositionCorrection)
        return new List<PointRegionDistanceTargetResult> { ExecuteSubscribedOrSingleDraw(param, token) };
    return MultiTargetMeasurementRunner.Run(
        ReadCorrections(param), token,
        correction => ExecuteOne(BuildRuntimeParam(param, correction), correction, token),
        CreateFailure);
}
```

四个窗体分别提供下列强类型辅助方法：

```csharp
private LineLineAngleTargetResult ExecuteSubscribedOrSingleDraw(NodeParamLineLineAngle param, CancellationToken token);
private IReadOnlyList<PositionCorrectionInfo> ReadCorrections(NodeParamLineLineAngle param);
private NodeParamLineLineAngle BuildRuntimeParam(NodeParamLineLineAngle param, PositionCorrectionInfo correction);
private LineLineAngleTargetResult ExecuteOne(NodeParamLineLineAngle runtimeParam, PositionCorrectionInfo correction, CancellationToken token);
private LineLineAngleTargetResult CreateFailure(PositionCorrectionInfo correction, Exception exception);

private PointPointDistanceTargetResult ExecuteSubscribedOrSingleDraw(NodeParamPointPointDistance param, CancellationToken token);
private IReadOnlyList<PositionCorrectionInfo> ReadCorrections(NodeParamPointPointDistance param);
private NodeParamPointPointDistance BuildRuntimeParam(NodeParamPointPointDistance param, PositionCorrectionInfo correction);
private PointPointDistanceTargetResult ExecuteOne(NodeParamPointPointDistance runtimeParam, PositionCorrectionInfo correction, CancellationToken token);
private PointPointDistanceTargetResult CreateFailure(PositionCorrectionInfo correction, Exception exception);

private PointLineDistanceTargetResult ExecuteSubscribedOrSingleDraw(NodeParamPointLineDistance param, CancellationToken token);
private IReadOnlyList<PositionCorrectionInfo> ReadCorrections(NodeParamPointLineDistance param);
private NodeParamPointLineDistance BuildRuntimeParam(NodeParamPointLineDistance param, PositionCorrectionInfo correction);
private PointLineDistanceTargetResult ExecuteOne(NodeParamPointLineDistance runtimeParam, PositionCorrectionInfo correction, CancellationToken token);
private PointLineDistanceTargetResult CreateFailure(PositionCorrectionInfo correction, Exception exception);

private PointRegionDistanceTargetResult ExecuteSubscribedOrSingleDraw(NodeParamPointRegionDistance param, CancellationToken token);
private IReadOnlyList<PositionCorrectionInfo> ReadCorrections(NodeParamPointRegionDistance param);
private NodeParamPointRegionDistance BuildRuntimeParam(NodeParamPointRegionDistance param, PositionCorrectionInfo correction);
private PointRegionDistanceTargetResult ExecuteOne(NodeParamPointRegionDistance runtimeParam, PositionCorrectionInfo correction, CancellationToken token);
private PointRegionDistanceTargetResult CreateFailure(PositionCorrectionInfo correction, Exception exception);
```

线线角度变换四个端点；点点距离变换两个点及绘制搜索区域；点线距离变换点、线和搜索区域；点区域距离变换点和区域全部顶点。

- [x] **Step 5: 为唯一编辑几何实现所属目标判断**

使用全部绘制几何的包围盒中心作为 ROI 中心：线线角度取四端点包围盒中心；点点距离取两点中点；点线距离取目标点与线段包围盒中心；点区域距离取目标点和区域合并包围盒中心。保存或运行时解析目标并把全部绘制几何逆变换到基准坐标系。编辑时不生成复制几何。

- [x] **Step 6: 合并多目标结果和显示**

四个节点结果都设置 `Items`、总体 `IsOk`、`JudgeOk`、合并 `AlgorithmResult` 和 `OutputImage.DisplayResult`。每个目标绘制文本以“目标N”开头；失败目标显示红色和数值 `0`。

- [x] **Step 7: 运行专项测试和编译**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\MultiTargetGeometryMeasurement.Tests.ps1`

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\MultiTargetFindPoint.Tests.ps1`

Run: `& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "D:\MyCode\PublicWook\TDJS-Vision\TDJS-Vision.sln" /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /m`

Expected: 两个专项测试 PASS；编译 0 个错误。

- [x] **Step 8: 提交本阶段**

```powershell
git add Tests/MultiTargetGeometryMeasurement.Tests.ps1 Node/4-Measurement/LineLineAngle Node/4-Measurement/PointPointDistance Node/4-Measurement/PointLineDistance Node/4-Measurement/PointRegionDistance FLOW_CANVAS_B_PLAN_TASKS.md
git commit -m "feat: 支持多目标几何测量"
```

---

### Task 7: 全量回归、性能检查和任务收尾

**Files:**
- Modify: `FLOW_CANVAS_B_PLAN_TASKS.md`
- Modify: 本计划文件中的复选状态。

**Interfaces:**
- Consumes: Tasks 1-6 的全部实现和专项测试。
- Produces: 可编译、可回退、任务记录完整的多目标测量版本。

- [x] **Step 1: 运行全部多目标专项测试**

```powershell
$tests = @(
    'Tests\MultiTargetTransform.Tests.ps1',
    'Tests\MultiTargetPositionCorrectionNode.Tests.ps1',
    'Tests\MultiTargetMeasurementRunner.Tests.ps1',
    'Tests\MultiTargetCaliper.Tests.ps1',
    'Tests\MultiTargetFindPoint.Tests.ps1',
    'Tests\MultiTargetGeometryMeasurement.Tests.ps1'
)
foreach ($test in $tests) {
    powershell -ExecutionPolicy Bypass -File $test
    if ($LASTEXITCODE -ne 0) { throw "专项测试失败：$test" }
}
```

Expected: 六个脚本全部 PASS。

- [x] **Step 2: 运行相关既有回归测试**

运行仓库中包含 `MatchTemplate`、`PositionCorrection`、`Caliper`、`Measurement`、`ProcessCanvas`、`NodeSubscription` 关键字的既有测试脚本；每个脚本退出码必须为 `0`。若仓库没有对应脚本，在任务记录中明确写“未发现对应既有脚本”，不得伪造通过记录。

- [x] **Step 3: 检查源码规范和项目登记**

```powershell
git diff --check
rg -n "T[O]DO|T[B]D|待定" Node/3-Detection/MatchTemplate Node/4-Measurement Tests/MultiTarget*.Tests.ps1
```

Expected: `git diff --check` 无输出；新增改动无占位词；全部新增 `.cs` 文件在 `TDJS-Vision.csproj` 中存在唯一登记。

- [x] **Step 4: 完整编译解决方案**

Run: `& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "D:\MyCode\PublicWook\TDJS-Vision\TDJS-Vision.sln" /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /m`

Expected: Build succeeded，0 个错误；既有警告数量记录到任务文件。

- [x] **Step 5: 完成任务记录**

把 `FLOW_CANVAS_B_PLAN_TASKS.md` 中“多目标位置修正与测量”的实现、专项测试和编译项改为 `[x]`，记录实际测试名称、成功数量、编译警告数和 0 错误结果。

- [x] **Step 6: 提交最终验证节点**

```powershell
git add FLOW_CANVAS_B_PLAN_TASKS.md docs/superpowers/plans/2026-07-20-multi-target-position-correction-measurement.md
git commit -m "test: 完成多目标测量回归验证"
```

- [x] **Step 7: 确认最终状态**

```powershell
git status --short
git log -8 --oneline
```

Expected: 工作区干净；日志包含改造前快照 `c92bd79`、设计提交 `6a201ff` 和 Tasks 1-7 的阶段提交。
