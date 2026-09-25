# 扎带最右白纹端头区域提取实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在大 ROI 内逐根定位最右侧内部白色竖纹，从白纹右边缘裁剪至扎带最右端，并在 WinForms 新页签集中显示全部彩色端头区域。

**Architecture:** 保留 `IRightEndInspector` 总检测接口，新增可替换的 `IRightmostWhiteStripeLocator` 列投影定位接口与默认实现。`OpenCvRightEndInspector` 负责二值连通域、二值和彩色图同步旋正、白纹定位、端头裁剪、预览拼接和坐标逆映射；`MainForm` 只负责参数读取、结果 Bitmap 生命周期和界面展示。

**Tech Stack:** C# 12、.NET 8 WinForms、OpenCvSharp 4.13、xUnit 2.4、x64。

## Global Constraints

- Demo 目录固定为 `D:\MyCode\PublicWook\扎带缺角检测`，不引用 TDJS-Vision 当前框架代码。
- 所有 WinForms 新控件必须声明和初始化在 `MainForm.Designer.cs`。
- 所有界面文字使用简体中文。
- 新增类、接口、字段、属性和方法均添加中文 XML 注释；关键算法添加中文思路注释。
- 算法按局部连通域线性扫描，避免整图重复遍历和逐像素托管对象分配。
- 使用 `apply_patch` 修改源文件；先在可写工作副本中测试，再同步到最终目录。
- 最终目录当前不是独立 Git 仓库，不初始化仓库、不创建提交；每个任务完成后更新 `任务记录.md` 作为检查点。
- 每个生产行为必须先有失败测试，确认失败原因正确后再写最小实现。

---

## 文件结构

- 新建 `src/TieCornerInspection.WinForms/Services/IRightmostWhiteStripeLocator.cs`：白纹定位策略接口。
- 新建 `src/TieCornerInspection.WinForms/Services/ColumnProjectionWhiteStripeLocator.cs`：列投影默认实现。
- 修改 `src/TieCornerInspection.WinForms/Services/OpenCvRightEndInspector.cs`：同步旋正、端头裁剪、预览拼接和原图坐标逆映射。
- 修改 `src/TieCornerInspection.WinForms/Models/InspectionRegionResult.cs`：白纹、裁剪区域和标注多边形数据。
- 修改 `src/TieCornerInspection.WinForms/Models/InspectionResult.cs`：新增可释放的端头预览图。
- 修改 `src/TieCornerInspection.WinForms/Models/InspectionParameters.cs`：移除斜切角判定参数。
- 修改 `src/TieCornerInspection.WinForms/MainForm.Designer.cs`：新增“端头提取图”页签和查看器，移除斜切角控件与表格列。
- 修改 `src/TieCornerInspection.WinForms/MainForm.cs`：端头预览 Bitmap 生命周期、页签选择和表格状态显示。
- 新建 `tests/TieCornerInspection.Tests/ColumnProjectionWhiteStripeLocatorTests.cs`：纯算法测试。
- 修改 `tests/TieCornerInspection.Tests/SlantedCutSampleInspectionTests.cs`：现场图测试改为白纹和端头提取验证。
- 修改 `README.md`、`任务记录.md`：说明新流程和验证证据。

---

### Task 1: 可替换的最右白纹定位器

**Files:**
- Create: `src/TieCornerInspection.WinForms/Services/IRightmostWhiteStripeLocator.cs`
- Create: `src/TieCornerInspection.WinForms/Services/ColumnProjectionWhiteStripeLocator.cs`
- Create: `tests/TieCornerInspection.Tests/ColumnProjectionWhiteStripeLocatorTests.cs`

**Interfaces:**
- Consumes: 前景为 255、背景为 0 的单根水平旋正二值 `Mat`，以及使用同一几何变换得到的彩色 `Mat`。
- Produces: `bool TryLocate(Mat alignedBinaryComponent, Mat alignedColorComponent, out Rect stripeBounds)`；成功时 `stripeBounds` 是旋正局部坐标系中的最右内部白纹矩形。

- [ ] **Step 1: 写“选择最右内部白纹”的失败测试**

测试构造 181×31 的白底 BGR 图，画一根黑色水平扎带，在局部 X=50～54 和 X=85～90 画两条不切断上下黑边的白纹；转成前景二值图后直接调用真实定位器。手工期望最右白纹为 `X=85, Width=6`。

```csharp
[Fact]
public void TryLocate_MultipleInternalWhiteStripes_ReturnsRightmostStripe()
{
    using Mat component = CreateComponentWithStripes((50, 5), (85, 6));
    IRightmostWhiteStripeLocator locator = new ColumnProjectionWhiteStripeLocator();

    bool found = locator.TryLocate(component, out Rect stripe);

    Assert.True(found);
    Assert.Equal(85, stripe.X);
    Assert.Equal(6, stripe.Width);
}
```

- [ ] **Step 2: 运行单测并确认 RED**

Run: `dotnet test tests\TieCornerInspection.Tests\TieCornerInspection.Tests.csproj -c Release --filter FullyQualifiedName~ColumnProjectionWhiteStripeLocatorTests`

Expected: 编译失败，提示 `IRightmostWhiteStripeLocator` 或 `ColumnProjectionWhiteStripeLocator` 不存在；失败原因只能是功能尚未实现。

- [ ] **Step 3: 写最小接口与列投影实现**

接口：

```csharp
public interface IRightmostWhiteStripeLocator
{
    bool TryLocate(Mat alignedBinaryComponent, Mat alignedColorComponent, out Rect stripeBounds);
}
```

默认实现固定使用以下归一化规则：中间 50% 高度为投影带；前景占比 `<= 0.35` 为二值白纹列；候选宽度为高度的 `4%～35%`；候选左右各至少有高度 `12%` 的连续黑色支撑列，支撑列平均前景占比 `>= 0.60`。现场图中亮纹低于全局黑色阈值时，再使用彩色图局部最大通道均值，要求相对暗部亮度差至少为 `40`，同时要求右侧端头长度至少为总宽度的 `35%` 且不少于高度的 2 倍。从右向左返回第一个有效候选。

- [ ] **Step 4: 增加边界和无白纹测试并完成 GREEN**

```csharp
[Fact]
public void TryLocate_WhiteBackgroundAtRoundedTip_DoesNotReturnOuterBackground()
{
    using Mat component = CreateSolidRoundedComponent();
    var locator = new ColumnProjectionWhiteStripeLocator();
    Assert.False(locator.TryLocate(component, out _));
}

[Fact]
public void TryLocate_SinglePixelNoise_DoesNotReturnNoiseAsStripe()
{
    using Mat component = CreateComponentWithSingleWhiteNoiseColumn();
    var locator = new ColumnProjectionWhiteStripeLocator();
    Assert.False(locator.TryLocate(component, out _));
}
```

Run: 同 Step 2。

Expected: 3 个定位器测试通过，0 警告、0 错误。

- [ ] **Step 5: 更新任务记录检查点**

在 `任务记录.md` 新增“最右白纹定位器”小节，记录 3 个测试及固定阈值含义；不创建 Git 提交。

---

### Task 2: 检测结果模型与端头裁剪集成

**Files:**
- Modify: `src/TieCornerInspection.WinForms/Models/InspectionRegionResult.cs`
- Modify: `src/TieCornerInspection.WinForms/Models/InspectionResult.cs`
- Modify: `src/TieCornerInspection.WinForms/Services/OpenCvRightEndInspector.cs`
- Modify: `tests/TieCornerInspection.Tests/OpenCvRightEndInspectorTests.cs`

**Interfaces:**
- Consumes: `IRightmostWhiteStripeLocator.TryLocate`。
- Produces: `InspectionRegionResult.HasRightmostWhiteStripe`、`WhiteStripeBounds`、`ExtractedEndBounds`、`WhiteStripePolygon`、`ExtractedEndPolygon`；`InspectionResult.EndRegionPreview`。

- [ ] **Step 1: 写端头裁剪边界失败测试**

使用真实 `OpenCvRightEndInspector` 检测一根包含两条白纹的合成扎带。手工断言：选择最右白纹；最终裁剪 X 等于白纹 `Right`；裁剪 `Right` 等于旋正组件宽度；裁剪高度等于旋正组件高度；预览图非空。

```csharp
[Fact]
public void Inspect_StripedTie_ExtractsFromRightEdgeOfRightmostStripeToTip()
{
    using Mat image = CreateStripedTieImage();
    using InspectionResult result = new OpenCvRightEndInspector().Inspect(image, CreateSyntheticParameters(image));
    InspectionRegionResult region = Assert.Single(result.Regions);

    Assert.True(region.HasRightmostWhiteStripe);
    Assert.Equal(region.WhiteStripeBounds.Right, region.ExtractedEndBounds.X);
    Assert.Equal(region.AlignedComponentSize.Width, region.ExtractedEndBounds.Right);
    Assert.Equal(region.AlignedComponentSize.Height, region.ExtractedEndBounds.Height);
    Assert.False(result.EndRegionPreview.Empty());
}
```

- [ ] **Step 2: 运行单测并确认 RED**

Run: `dotnet test tests\TieCornerInspection.Tests\TieCornerInspection.Tests.csproj -c Release --filter FullyQualifiedName~Inspect_StripedTie_ExtractsFromRightEdgeOfRightmostStripeToTip`

Expected: 编译失败，提示新增结果属性不存在。

- [ ] **Step 3: 扩展结果模型和资源释放**

`InspectionRegionResult` 新增带 XML 注释的属性：

```csharp
public bool HasRightmostWhiteStripe { get; set; }
public Rect WhiteStripeBounds { get; set; }
public Rect ExtractedEndBounds { get; set; }
public OpenCvSharp.Size AlignedComponentSize { get; set; }
public Point2f[] WhiteStripePolygon { get; set; } = Array.Empty<Point2f>();
public Point2f[] ExtractedEndPolygon { get; set; } = Array.Empty<Point2f>();
```

`InspectionResult` 新增必填 `Mat EndRegionPreview`，并在 `Dispose()` 中释放。

- [ ] **Step 4: 同步旋正二值图和彩色图**

把只返回二值 `Mat` 的 `AlignHorizontally` 重构为私有可释放 `AlignedComponent`：缓存旋转矩阵、逆矩阵、前景紧密矩形、旋正二值图和彩色图。二值图使用 `Nearest`，彩色图使用 `Linear`，两者使用同一几何变换。通过逆仿射矩阵把局部白纹矩形和裁剪矩形四角映射回原图坐标。

- [ ] **Step 5: 集成定位器并裁剪彩色端头**

`OpenCvRightEndInspector` 增加默认构造函数和策略注入构造函数：

```csharp
public OpenCvRightEndInspector()
    : this(new ColumnProjectionWhiteStripeLocator()) { }

public OpenCvRightEndInspector(IRightmostWhiteStripeLocator stripeLocator)
{
    _stripeLocator = stripeLocator ?? throw new ArgumentNullException(nameof(stripeLocator));
}
```

每个连通域成功定位后使用：

```csharp
Rect endBounds = new(
    stripeBounds.Right,
    0,
    aligned.Binary.Width - stripeBounds.Right,
    aligned.Binary.Height);
```

宽度小于 3 时按未找到处理。临时彩色 crop 在预览拼接完成后释放，不保存到行模型。

- [ ] **Step 6: 实现预览拼接并完成 GREEN**

`BuildEndRegionPreview` 使用深灰背景；每行左侧 56 像素写 `#01` 等序号，端头原尺寸粘贴到右侧，裁剪左边界画红线。未找到白纹时显示完整旋正组件并写“未找到白纹”。行间距固定 8 像素。

Run: Task 2 单测过滤命令。

Expected: 端头边界测试通过，`EndRegionPreview` 非空，原有合成检测测试继续通过。

- [ ] **Step 7: 更新任务记录检查点**

记录结果模型、同步旋正、逆映射、预览资源释放及通过的测试；不创建 Git 提交。

---

### Task 3: 标注图与现场样图回归

**Files:**
- Modify: `src/TieCornerInspection.WinForms/Services/OpenCvRightEndInspector.cs`
- Modify: `tests/TieCornerInspection.Tests/SlantedCutSampleInspectionTests.cs`

**Interfaces:**
- Consumes: Task 2 生成的原图坐标多边形。
- Produces: 青色白纹四边形、红色端头四边形；3072×2048 现场图端头提取回归结果。

- [ ] **Step 1: 把现场图测试改写为失败的白纹提取测试**

使用界面默认 ROI `Rect(1290, 82, 246, 1802)` 和最大高度 `60`，不再断言第 25 根斜切角或 OK/NG。断言 25 个区域、25 个全部成功定位、每个区域的裁剪 X 等于白纹 Right、裁剪宽高有效、预览非空。

```csharp
Assert.Equal(25, result.Regions.Count);
Assert.All(result.Regions, region => Assert.True(region.HasRightmostWhiteStripe));
Assert.All(
    result.Regions.Where(region => region.HasRightmostWhiteStripe),
    region =>
    {
        Assert.Equal(region.WhiteStripeBounds.Right, region.ExtractedEndBounds.X);
        Assert.True(region.ExtractedEndBounds.Width >= 3);
        Assert.Equal(region.AlignedComponentSize.Height, region.ExtractedEndBounds.Height);
    });
```

- [ ] **Step 2: 运行现场图测试并确认 RED**

Run: `dotnet test tests\TieCornerInspection.Tests\TieCornerInspection.Tests.csproj -c Release --filter FullyQualifiedName~SlantedCutSampleInspectionTests --logger "console;verbosity=detailed"`

Expected: 断言失败并输出实际白纹数量或裁剪边界；失败原因是现场图尚未满足白纹提取契约，而不是图片缺失或解码错误。

- [ ] **Step 3: 调整列投影的实现细节而不改变归一化契约**

现场诊断确认亮纹仍低于全局黑色阈值，因此允许引入彩色局部对比作为第二检测通道；不得加入固定像素坐标或针对第 25 根的分支，并继续保留“内部左右均有黑色支撑”和“足够长右侧端头”的约束。现场测试增加前 24 根裁剪宽度 `195～215` 像素的回归范围，排除端头弱亮度缓变。

- [ ] **Step 4: 绘制准确多边形标注**

`DrawAnnotations` 对成功区域绘制：白纹四边形使用青色、端头四边形使用红色；未找到白纹只保留橙色连通域框。所有点先限制到图像边界再转成 `Point`，避免 OpenCV 越界。

- [ ] **Step 5: 运行现场图和全部算法测试完成 GREEN**

Run: `dotnet test tests\TieCornerInspection.Tests\TieCornerInspection.Tests.csproj -c Release --logger "console;verbosity=normal"`

Expected: 白纹、现场样图、旧样图、连通域和缩放测试全部通过。

- [ ] **Step 6: 更新任务记录检查点**

记录现场图成功定位数量、各区域裁剪宽度范围和未找到序号；不创建 Git 提交。

---

### Task 4: 移除斜切角判定路径

**Files:**
- Modify: `src/TieCornerInspection.WinForms/Models/InspectionParameters.cs`
- Modify: `src/TieCornerInspection.WinForms/Models/InspectionRegionResult.cs`
- Modify: `src/TieCornerInspection.WinForms/Services/OpenCvRightEndInspector.cs`
- Modify: `tests/TieCornerInspection.Tests/SlantedCutSampleInspectionTests.cs`

**Interfaces:**
- Consumes: 白纹提取现场回归测试。
- Produces: 缺陷判定只保留圆头匹配度和相对偏差；不再公开或显示斜切角。

- [ ] **Step 1: 先修改现场测试，使斜切角不再决定结果**

删除 `EndCutAngleDegrees` 和 `MaximumNormalEndCutAngleDegrees` 相关断言，保留白纹和裁剪区域断言。运行该测试，确认它在生产代码尚未移除时仍验证新行为；这一任务属于删除旧行为，不为“符号不存在”写源文本测试。

- [ ] **Step 2: 删除斜切角生产路径**

删除参数、区域结果属性、`MeasureEndCutAngleDegrees`、判定条件和标注 `A=` 文字；保留最大连通域高度默认值 `60`。

- [ ] **Step 3: 运行全部测试**

Run: `dotnet test TieCornerInspection.sln --no-restore -c Release`

Expected: 所有行为测试通过，删除旧角度路径没有破坏白纹提取。

- [ ] **Step 4: 更新任务记录检查点**

记录斜切角已退出判定和界面；不创建 Git 提交。

---

### Task 5: WinForms 端头提取页签与结果表格

**Files:**
- Modify: `src/TieCornerInspection.WinForms/MainForm.Designer.cs`
- Modify: `src/TieCornerInspection.WinForms/MainForm.cs`

**Interfaces:**
- Consumes: `InspectionResult.EndRegionPreview` 和区域白纹属性。
- Produces: “端头提取图”页签、可缩放查看器、白纹状态表格和 Bitmap 生命周期管理。

- [ ] **Step 1: 在 Designer 文件中声明并创建控件**

新增带中文 XML 注释的字段：

```csharp
private TabPage endRegionTabPage = null!;
private ZoomableImageBox pictureEndRegions = null!;
```

在 `InitializeComponent()` 中创建、加入 `resultTabControl`、设置 `Text = "端头提取图"`、深灰背景、中文 `EmptyText` 和缩放提示。移除 `labelEndCutAngleThreshold`、`numericEndCutAngleThreshold` 及“斜切角”表格列。表格新增“白纹 X”“裁剪宽度”“白纹状态”。

- [ ] **Step 2: 完成 Bitmap 生命周期和交互**

`MainForm` 新增 `_endRegionPreviewBitmap`；加载新图和关闭窗体时释放；检测后从 `result.EndRegionPreview` 转换并替换；默认选中 `endRegionTabPage`。三个“全部”缩放按钮改为同时操作四个查看器。

- [ ] **Step 3: 更新表格和状态栏**

每行写入白纹 X、裁剪宽度和“已找到/未找到”；状态栏显示总数、成功数和失败数。删除斜切角参数读取、表格值、最大角度状态文字。

- [ ] **Step 4: 构建验证 Designer 与代码一致**

Run: `dotnet build TieCornerInspection.sln --no-restore -c Release`

Expected: 0 警告、0 错误；Designer 中声明的所有控件均已初始化和加入容器。

- [ ] **Step 5: 更新任务记录检查点**

记录新增页签、四图同步缩放、表格列和资源释放；不创建 Git 提交。

---

### Task 6: 文档、同步与最终验证

**Files:**
- Modify: `README.md`
- Modify: `任务记录.md`
- Sync: 工作副本到 `D:\MyCode\PublicWook\扎带缺角检测`

**Interfaces:**
- Consumes: 完成的算法、模型、测试和界面。
- Produces: 可直接打开运行的最终 Demo 与验证记录。

- [ ] **Step 1: 更新用户文档**

README 写明：大 ROI、最右白纹列投影、从白纹右边缘到最右端、端头提取页签操作、未找到白纹提示，以及本阶段不使用斜切角判定。

- [ ] **Step 2: 完整测试**

Run: `dotnet test TieCornerInspection.sln --no-restore -c Release --logger "console;verbosity=normal"`

Expected: 全部测试通过，失败 0 个，跳过 0 个。

- [ ] **Step 3: Release 构建**

Run: `dotnet build TieCornerInspection.sln --no-restore -c Release`

Expected: 0 警告、0 错误。

- [ ] **Step 4: 同步最终目录并校验文件哈希**

使用 `robocopy` 排除 `bin`、`obj`、`.vs` 和 `*.user`，同步源文件、样图、测试和文档。逐文件计算 SHA256，确认工作副本与最终目录全部一致。

- [ ] **Step 5: 在最终目录重复测试和构建**

Run: `dotnet test TieCornerInspection.sln --no-restore -c Release`

Run: `dotnet build TieCornerInspection.sln --no-restore -c Release`

Expected: 测试失败 0 个；构建 0 警告、0 错误。

- [ ] **Step 6: WinForms 启动冒烟测试**

隐藏启动 Release EXE，等待 3 秒；若进程保持运行则记录通过并关闭测试进程。若提前退出，记录退出码并停止交付声明。

- [ ] **Step 7: 写入最终任务记录**

在 `任务记录.md` 记录测试总数、现场图白纹成功数、构建警告/错误数和启动检查结果。
