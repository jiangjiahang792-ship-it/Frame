# ROI结果绘制两类型简化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将ROI结果绘制2简化为文本和ROI两种绘制项，并按订阅值实际类型自动绘制几何结果。

**Architecture:** 参数层保留旧字段并新增自动ROI与单布尔判定字段；运行层通过 `IOverlayGeometryAdapter` 和注册表分派几何类型；参数窗体只负责两类项目编辑。旧枚举与旧颜色规则继续兼容，新配置进入简化路径。

**Tech Stack:** C#、.NET Framework 4.8、WinForms、Newtonsoft.Json、OpenCvSharp、PowerShell结构与行为检查、Visual Studio 2022 MSBuild。

**实施状态：** 已完成。所有任务均按本计划执行了失败测试、最小实现、回归验证和 Debug|x64 完整编译；详细结果见“任务记录.md”的2026-07-22对应条目。

## Global Constraints

- 所有新增类、接口、字段、属性和方法使用简体中文XML注释。
- 所有新增或调整的WinForms控件必须写在 `.Designer.cs` 文件中，控件文字使用简体中文。
- 每个绘制项每轮只读取一次订阅值，运行时类型和反射提取器必须缓存。
- 旧 `.Sol` 的旧几何枚举和旧多颜色规则必须继续运行。
- 本次按用户要求不执行Git提交、分支切换或推送。

---

### Task 1: 参数模型与兼容数据

**Files:**
- Modify: `Node/7-ResultProcessing/ResultOverlayDraw2/NodeParamResultOverlayDraw2.cs`
- Test: `Tests/ResultOverlayDraw2Simplification.Tests.ps1`

**Interfaces:**
- Produces: `ResultOverlayDraw2ItemType.Roi`、`JudgeText1`、`JudgeText2`、`HasNewJudgeSubscription`。
- Preserves: `Line`、`Rectangle`、`Region`、`ColorRules`、`RuleMode`。

- [ ] **Step 1: 写失败测试**

测试必须断言参数类型包含新ROI枚举和单判定字段，并断言旧字段仍存在：

```powershell
Assert-ContainsText $paramSource 'Roi' '必须增加自动ROI类型。'
Assert-ContainsText $paramSource 'public string JudgeText1' '必须保存颜色判定节点。'
Assert-ContainsText $paramSource 'public string JudgeText2' '必须保存颜色判定结果。'
Assert-ContainsText $paramSource 'public List<ResultOverlayDraw2ColorRule> ColorRules' '必须保留旧规则。'
```

- [ ] **Step 2: 运行测试确认失败**

Run: `powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& ([scriptblock]::Create((Get-Content -Raw -Encoding UTF8 '.\Tests\ResultOverlayDraw2Simplification.Tests.ps1')))"`

Expected: FAIL，提示缺少 `Roi` 或 `JudgeText1`。

- [ ] **Step 3: 实现最小参数变更**

在参数类中加入带注释的两个订阅文本属性和只读判定辅助属性，在枚举尾部加入 `Roi`。不删除任何旧字段。

- [ ] **Step 4: 运行测试确认本任务断言通过**

Run: 与Step 2相同。

Expected: 参数模型断言通过，后续尚未实现的界面断言仍失败。

### Task 2: 可扩展几何适配器

**Files:**
- Create: `Node/7-ResultProcessing/ResultOverlayDraw2/OverlayGeometryAdapterRegistry.cs`
- Modify: `TDJS-Vision.csproj`
- Test: `Tests/ResultOverlayGeometryAdapter.Tests.ps1`

**Interfaces:**
- Produces: `IOverlayGeometryAdapter`。
- Produces: `OverlayGeometryRenderContext.ResolveColor(Color)`。
- Produces: `OverlayGeometryAdapterRegistry.TryAppend(AlgorithmResult, object, INodeResult, SubscriptionDataCategory, OverlayGeometryRenderContext, out int)`。

- [ ] **Step 1: 写失败测试**

结构测试必须检查接口、注册表、算法结果几何复制、文本排除、类型缓存和项目编译项：

```powershell
Assert-ContainsText $adapterSource 'interface IOverlayGeometryAdapter' '缺少几何适配器接口。'
Assert-ContainsText $adapterSource 'ConcurrentDictionary<Type' '适配器分派必须缓存。'
Assert-ContainsText $adapterSource 'source.Rects' '必须复制矩形。'
Assert-True (-not $geometryCopyBlock.Contains('source.Texts')) 'ROI适配器不能复制文本。'
```

- [ ] **Step 2: 运行测试确认失败**

Run: `powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& ([scriptblock]::Create((Get-Content -Raw -Encoding UTF8 '.\Tests\ResultOverlayGeometryAdapter.Tests.ps1')))"`

Expected: FAIL，提示适配器文件不存在。

- [ ] **Step 3: 实现适配器和注册表**

注册表内置以下适配器并保持固定顺序：

```csharp
private static readonly IReadOnlyList<IOverlayGeometryAdapter> Adapters =
    new IOverlayGeometryAdapter[]
    {
        new AlgorithmResultGeometryAdapter(),
        new PointGeometryAdapter(),
        new EnumerableGeometryAdapter(),
        new MeasurementResultGeometryAdapter()
    };
```

`AlgorithmResultGeometryAdapter` 强类型复制 `Rects`、`Lines`、`Circles`、`Arcs`、`Ellipses`、`Contours`，不读取 `Texts`。点与点集生成十字线；轮廓按类别生成 `ColorContour`；测量结果优先读取来源结果中的完整 `AlgorithmResult`。无法追加时返回false和零数量。

- [ ] **Step 4: 把新文件加入项目并运行测试**

Run: 与Step 2相同。

Expected: PASS。

### Task 3: 构建器接入自动ROI和单布尔判定

**Files:**
- Modify: `Node/7-ResultProcessing/ResultOverlayDraw2/NodeResultOverlayDraw2.cs`
- Test: `Tests/ResultOverlayDraw2Simplification.Tests.ps1`
- Test: `Tests/ResultOverlayGeometryAdapter.Tests.ps1`

**Interfaces:**
- Consumes: `ResultOverlayDraw2ItemType.Roi`。
- Consumes: `OverlayGeometryAdapterRegistry.TryAppend(...)`。
- Produces: 新判定优先、旧规则回退、无判定保留来源颜色的构建流程。

- [ ] **Step 1: 扩展失败测试**

```powershell
Assert-ContainsText $builderSource 'case ResultOverlayDraw2ItemType.Roi:' '构建器必须处理自动ROI。'
Assert-ContainsText $builderSource 'param.HasNewJudgeSubscription' '必须优先使用新布尔判定。'
Assert-ContainsText $builderSource 'OverlayGeometryAdapterRegistry.TryAppend' '自动ROI必须使用统一适配器。'
```

- [ ] **Step 2: 运行测试确认失败**

Expected: FAIL，提示构建器尚未接入。

- [ ] **Step 3: 实现判定状态对象**

增加内部只读运行状态，明确区分“存在判定并覆盖颜色”和“无判定并保留颜色”：

```csharp
internal sealed class ResultOverlayDrawColorState
{
    public bool IsOk { get; set; }
    public bool OverrideSourceColor { get; set; }
    public Color DisplayColor { get; set; }
    public string Diagnostics { get; set; }
}
```

新单判定存在时使用统一订阅读取并严格转换 `bool`；否则保留旧规则逻辑；两者都不存在时返回OK且 `OverrideSourceColor=false`。

- [ ] **Step 4: 接入自动ROI**

自动ROI分支只调用一次 `TryReadDrawItemValue`，从统一订阅目录找到当前输出类别，再把值、来源结果、类别和颜色上下文交给注册表。成功空AI结果不显示未找到；其他无法追加的结果调用现有未找到提示。

- [ ] **Step 5: 运行两组测试**

Expected: PASS。

### Task 4: 简化参数窗体与Designer布局

**Files:**
- Modify: `Node/7-ResultProcessing/ResultOverlayDraw2/NodeParamFormResultOverlayDraw2.cs`
- Modify: `Node/7-ResultProcessing/ResultOverlayDraw2/NodeParamFormResultOverlayDraw2.Designer.cs`
- Modify: `Node/7-ResultProcessing/ResultOverlayDraw2/NodeParamFormResultOverlayDraw2.resx`（仅由控件资源需要时调整）
- Test: `Tests/ResultOverlayDraw2Simplification.Tests.ps1`

**Interfaces:**
- Consumes: 图像、布尔、文本和ROI输入契约。
- Produces: 只包含“添加文本”“添加ROI”“删除”的操作界面。

- [ ] **Step 1: 扩展失败测试**

```powershell
Assert-ContainsText $designerSource 'this.buttonAddRoi.Text = "添加ROI";' '界面必须提供添加ROI。'
Assert-True (-not $designerSource.Contains('加线')) '界面不能继续提供加线。'
Assert-True (-not $designerSource.Contains('添加规则')) '界面不能继续提供颜色规则编辑。'
Assert-ContainsText $formSource 'SubscriptionDataCategory.MeasurementResult' 'ROI订阅必须接受测量结果。'
```

- [ ] **Step 2: 运行测试确认失败**

Expected: FAIL，提示旧按钮仍存在或新按钮缺失。

- [ ] **Step 3: 重建Designer布局**

基础区包含图像订阅、可选布尔订阅、OK/NG颜色和旧规则提示；绘制项区包含三按钮与表格；设置区按文本/ROI显示相应控件；高级设置默认收起；右侧保留预览。所有控件声明和事件绑定均放在Designer文件。

- [ ] **Step 4: 重写窗体映射逻辑**

新ROI使用 `Roi`；旧类型显示“ROI（旧配置）”。选择文本时给来源控件设置布尔、数值、文本、算法结果契约；选择ROI时设置点、点集、线、圆、椭圆、矩形、轮廓、区域、测量结果、算法结果契约。保存新判定时清除旧规则，未选择新判定时保留旧规则。

- [ ] **Step 5: 运行界面结构测试**

Expected: PASS。

### Task 5: 依赖范围、方案兼容与回归

**Files:**
- Modify: `Forms/ImageViewer/FrmSingleImage.cs`
- Modify: `Tests/SubscriptionConsumerContracts.Tests.ps1`
- Modify: `任务记录.md`

**Interfaces:**
- Consumes: `JudgeText1` 和旧 `ColorRules`。
- Produces: 手动调参过滤范围同时覆盖新单判定来源和旧规则来源。

- [ ] **Step 1: 写失败回归断言**

在简化测试中检查 `FrmSingleImage.AddResultOverlayDraw2Sources` 同时读取新判定来源和旧规则来源，并检查所有可见 `NodeSubscription` 均在初始化前声明契约。

- [ ] **Step 2: 运行测试确认失败**

Expected: FAIL，提示新判定来源尚未纳入过滤。

- [ ] **Step 3: 更新依赖范围与任务记录**

加入新判定源节点ID，保留旧规则循环。任务记录说明两类型界面、自动适配、单布尔判定、旧方案兼容、性能策略和验证结果。

- [ ] **Step 4: 运行专项与现有回归测试**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& ([scriptblock]::Create((Get-Content -Raw -Encoding UTF8 '.\Tests\ResultOverlayDraw2Simplification.Tests.ps1')))"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& ([scriptblock]::Create((Get-Content -Raw -Encoding UTF8 '.\Tests\ResultOverlayGeometryAdapter.Tests.ps1')))"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& ([scriptblock]::Create((Get-Content -Raw -Encoding UTF8 '.\Tests\SubscriptionConsumerContracts.Tests.ps1')))"
```

Expected: 全部PASS。

- [ ] **Step 5: 完整编译**

Run:

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "D:\MyCode\PublicWook\TDJS-Vision\TDJS-Vision.sln" /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /m
```

Expected: 0个错误；既有警告单独报告。
