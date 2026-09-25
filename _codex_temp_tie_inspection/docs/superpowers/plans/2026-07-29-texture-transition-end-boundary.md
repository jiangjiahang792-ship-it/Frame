# 扎带齿纹结束位置与端头边界定位实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 使用单根自适应纹理转折和同图稳健边界共识，替代固定白纹亮度差，使 NG 文件夹全部 45 张图中的每个有效扎带都能裁出从齿纹结束位置到最右端的端头区域。

**Architecture:** 新增只负责单根分析的 `IRightEndBoundaryLocator` 和 `TextureTransitionEndBoundaryLocator`，输出边界 X 与置信度。`OpenCvRightEndInspector` 保存一批旋正组件，先执行局部定位，再用高置信边界拟合跨行共识并补齐低置信区域，最后统一裁剪、逆映射和生成预览；WinForms 只展示最终起点和定位来源。

**Tech Stack:** C# 12、.NET 8 WinForms、OpenCvSharp 4.13、xUnit 2.4、x64。

## Global Constraints

- Demo 最终目录固定为 `D:\MyCode\PublicWook\扎带缺角检测`，不引用 TDJS-Vision 当前框架代码。
- NG 输入目录为 `C:\Users\34652\Desktop\20260728\20260728\NG`，共 45 张 3072×2048 JPG，其中 38 张内容不重复。
- 所有 WinForms 控件和表格列必须声明、初始化并加入 `MainForm.Designer.cs`。
- 所有界面文字使用简体中文；新增类型、属性、字段和方法均添加中文 XML 注释。
- 单根算法只扫描局部组件；跨行共识只处理区域级数值，不重复扫描整张原图。
- 不使用固定原图 X 坐标、不针对图片名或扎带序号分支、不简单降低原亮度差阈值。
- 生产代码修改前必须先运行能稳定复现问题的失败测试；每个任务完成后更新 `任务记录.md`。
- 工作副本位于 `D:\MyCode\PublicWook\TDJS-Vision\_codex_temp_tie_inspection`，最终再同步到目标目录。
- 最终目录不是独立 Git 仓库，因此不创建提交；以任务记录、测试输出、文件哈希和启动检查作为交付检查点。

---

## 文件结构

- 新建 `src/TieCornerInspection.WinForms/Models/EndBoundaryCandidate.cs`：单根边界位置与置信度值对象。
- 新建 `src/TieCornerInspection.WinForms/Models/EndBoundaryLocationSource.cs`：最终定位来源枚举。
- 新建 `src/TieCornerInspection.WinForms/Services/IRightEndBoundaryLocator.cs`：可替换单根结构边界接口。
- 新建 `src/TieCornerInspection.WinForms/Services/TextureTransitionEndBoundaryLocator.cs`：自适应齿纹到光滑段定位实现。
- 删除 `src/TieCornerInspection.WinForms/Services/IRightmostWhiteStripeLocator.cs` 和 `ColumnProjectionWhiteStripeLocator.cs`：移除固定白纹语义和固定亮度差实现。
- 修改 `src/TieCornerInspection.WinForms/Services/OpenCvRightEndInspector.cs`：两遍测量、稳健共识、补齐、裁剪和逆映射。
- 修改 `src/TieCornerInspection.WinForms/Models/InspectionRegionResult.cs`：端头边界属性和定位来源。
- 修改 `src/TieCornerInspection.WinForms/MainForm.Designer.cs`、`MainForm.cs`：中文列名、状态和预览提示。
- 新建 `tests/TieCornerInspection.Tests/TextureTransitionEndBoundaryLocatorTests.cs`：单根纹理定位测试。
- 新建 `tests/TieCornerInspection.Tests/EndBoundaryConsensusTests.cs`：跨行补齐和离群排除测试。
- 新建 `tests/TieCornerInspection.Tests/NgFolderBatchInspectionTests.cs`：45 张现场图批量回归。
- 修改现有白纹相关测试为端头结构边界语义。
- 修改 `README.md`、设计文档和 `任务记录.md`：记录新流程和验证证据。

---

### Task 1: 建立 NG 文件夹批量失败基线

**Files:**
- Create: `tests/TieCornerInspection.Tests/NgFolderBatchInspectionTests.cs`
- Copy: `samples/NG回归/20260728094801549.jpg`
- Copy: `samples/NG回归/20260728141640459.jpg`
- Copy: `samples/NG回归/20260728145614277.jpg`
- Copy: `samples/NG回归/20260728154905582.jpg`
- Copy: `samples/NG回归/20260728215757368.jpg`
- Modify: `tests/TieCornerInspection.Tests/TieCornerInspection.Tests.csproj`
- Modify: `任务记录.md`

**Interfaces:**
- Consumes: 当前 `OpenCvRightEndInspector.Inspect(Mat, InspectionParameters)` 和 `InspectionRegionResult.HasRightmostWhiteStripe`。
- Produces: 每张图的区域数、成功数、失败序号和裁剪宽度诊断；当前代码下明确失败的批量断言。

- [ ] **Step 1: 写遍历 45 张 JPG 的失败测试**

```csharp
[Fact]
public void Inspect_NgFolder_CurrentLocatorShowsEveryMiss()
{
    string folder = @"C:\Users\34652\Desktop\20260728\20260728\NG";
    string[] paths = Directory.GetFiles(folder, "*.jpg").OrderBy(path => path).ToArray();
    Assert.Equal(45, paths.Length);

    List<string> failures = new();
    foreach (string path in paths)
    {
        using Mat image = LoadColor(path);
        using InspectionResult result = new OpenCvRightEndInspector().Inspect(image, CreateFieldParameters());
        int[] missing = result.Regions
            .Where(region => !region.HasRightmostWhiteStripe)
            .Select(region => region.Index)
            .ToArray();
        _output.WriteLine($"{Path.GetFileName(path)} 区域={result.Regions.Count} 未找到={string.Join(',', missing)}");
        if (missing.Length > 0)
        {
            failures.Add($"{Path.GetFileName(path)}:[{string.Join(',', missing)}]");
        }
    }

    Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
}
```

`CreateFieldParameters()` 使用扩大后的界面默认比例得到 `Rect(1290, 82, 492, 1802)`，黑阈值 180、面积 120、宽度 30、高度 6～60、闭运算核 3。16% 宽 ROI 为末端齿纹保留足够上下文。

- [ ] **Step 2: 运行测试并确认 RED**

Run: `dotnet test tests\TieCornerInspection.Tests\TieCornerInspection.Tests.csproj -c Release --filter FullyQualifiedName~Inspect_NgFolder_CurrentLocatorShowsEveryMiss --logger "console;verbosity=detailed"`

Expected: 修复前测试执行 45 张图后因至少一个区域未找到旧白纹失败；输出必须包含具体图片名和失败序号，不能因目录、解码或 ROI 错误失败。

- [ ] **Step 3: 汇总稳定复现数据**

把每张图的区域数、成功数和失败数写入 `任务记录.md`。根据 RED 输出，把分别覆盖局部漏检、全部找到、25/25 全漏、多数漏检和稀疏布局的 `20260728094801549.jpg`、`20260728141640459.jpg`、`20260728145614277.jpg`、`20260728154905582.jpg`、`20260728215757368.jpg` 复制到 `samples/NG回归`。在测试项目中使用 `samples\NG回归\*.jpg` 链接并设置 `CopyToOutputDirectory="PreserveNewest"`；完整 45 张仍由外部批量测试执行，不复制另外 7 张内容相同的文件。

---

### Task 2: 单根自适应纹理转折定位器

**Files:**
- Create: `src/TieCornerInspection.WinForms/Models/EndBoundaryCandidate.cs`
- Create: `src/TieCornerInspection.WinForms/Services/IRightEndBoundaryLocator.cs`
- Create: `src/TieCornerInspection.WinForms/Services/TextureTransitionEndBoundaryLocator.cs`
- Create: `tests/TieCornerInspection.Tests/TextureTransitionEndBoundaryLocatorTests.cs`

**Interfaces:**
- Produces: `bool TryLocate(Mat alignedBinaryComponent, Mat alignedColorComponent, out EndBoundaryCandidate candidate)`。
- Produces: `bool TryRefineNear(Mat alignedBinaryComponent, Mat alignedColorComponent, int predictedBoundaryX, int searchRadius, out EndBoundaryCandidate candidate)`。
- Produces: `EndBoundaryCandidate.BoundaryX` 为旋正组件局部坐标中的裁剪起点，`Confidence` 为大于等于 0 的结构置信度。

- [ ] **Step 1: 写亮度变化不改变边界的失败测试**

```csharp
[Theory]
[InlineData(45)]
[InlineData(90)]
[InlineData(145)]
public void TryLocate_OverallBrightnessChanges_ReturnsSameTextureBoundary(byte smoothBrightness)
{
    const int expectedBoundaryX = 82;
    using Mat binary = CreateSolidTieMask(180, 30);
    using Mat color = CreateTexturedTie(180, 30, expectedBoundaryX, smoothBrightness);
    IRightEndBoundaryLocator locator = new TextureTransitionEndBoundaryLocator();

    bool found = locator.TryLocate(binary, color, out EndBoundaryCandidate candidate);

    Assert.True(found);
    Assert.InRange(candidate.BoundaryX, expectedBoundaryX - 2, expectedBoundaryX + 2);
    Assert.True(candidate.Confidence > 0);
}
```

- [ ] **Step 2: 运行单测并确认 RED**

Run: `dotnet test tests\TieCornerInspection.Tests\TieCornerInspection.Tests.csproj -c Release --filter FullyQualifiedName~TextureTransitionEndBoundaryLocatorTests`

Expected: 编译失败，提示 `IRightEndBoundaryLocator`、`TextureTransitionEndBoundaryLocator` 或 `EndBoundaryCandidate` 尚不存在。

- [ ] **Step 3: 创建值对象和接口**

```csharp
public readonly record struct EndBoundaryCandidate(int BoundaryX, double Confidence);

public interface IRightEndBoundaryLocator
{
    bool TryLocate(
        Mat alignedBinaryComponent,
        Mat alignedColorComponent,
        out EndBoundaryCandidate candidate);

    bool TryRefineNear(
        Mat alignedBinaryComponent,
        Mat alignedColorComponent,
        int predictedBoundaryX,
        int searchRadius,
        out EndBoundaryCandidate candidate);
}
```

全部类型和成员添加中文 XML 注释，接口校验空图、尺寸不一致和通道类型。

- [ ] **Step 4: 实现归一化纹理能量**

`TextureTransitionEndBoundaryLocator` 按以下固定几何规则实现：

- 投影带为高度的 25%～75%。
- 每列取投影带内灰度中位数。
- 差分能量 `energy[x] = Abs(profile[x] - profile[x - 1])`。
- 滑动窗口宽度为 `Max(5, Round(height * 0.30))`，使用前缀和在线性时间内计算平均能量。
- 候选转折要求左侧两个窗口的中位能量高于右侧两个窗口；使用整条曲线的中位绝对偏差归一化，置信度为 `(leftEnergy - rightEnergy) / Max(1, mad)`。
- 右侧端头长度至少为 `Max(height * 2, Round(width * 0.35))`。
- 在候选附近 `±Round(height * 0.35)` 内选择最后一个显著下降沿，并返回其右侧第一列。
- `TryRefineNear` 复用同一列灰度、差分和归一化能量，只在 `[predictedBoundaryX - searchRadius, predictedBoundaryX + searchRadius]` 内选择置信度最高的下降沿；没有高于局部 MAD 的边缘时返回 `false`。

- [ ] **Step 5: 增加无纹理和圆头反光测试**

```csharp
[Fact]
public void TryLocate_CompletelySmoothTie_ReturnsFalse()
{
    using Mat binary = CreateSolidTieMask(180, 30);
    using Mat color = CreateSmoothTie(180, 30, 70);
    Assert.False(new TextureTransitionEndBoundaryLocator().TryLocate(binary, color, out _));
}

[Fact]
public void TryLocate_HighlightOnlyAtRoundedTip_DoesNotMoveBoundaryToTip()
{
    const int expectedBoundaryX = 80;
    using Mat binary = CreateRoundedTieMask(180, 30);
    using Mat color = CreateTexturedTieWithTipHighlight(180, 30, expectedBoundaryX);
    Assert.True(new TextureTransitionEndBoundaryLocator().TryLocate(binary, color, out var candidate));
    Assert.InRange(candidate.BoundaryX, 78, 82);
}
```

- [ ] **Step 6: 运行定位器测试完成 GREEN**

Run: Task 2 测试命令。

Expected: 全部亮度、无纹理和圆头反光测试通过，0 个失败。

- [ ] **Step 7: 更新任务记录检查点**

记录归一化方式、固定几何比例、RED/GREEN 输出和测试数量。

---

### Task 3: 同图稳健边界共识与补齐

**Files:**
- Create: `src/TieCornerInspection.WinForms/Models/EndBoundaryLocationSource.cs`
- Modify: `src/TieCornerInspection.WinForms/Models/InspectionRegionResult.cs`
- Modify: `src/TieCornerInspection.WinForms/Services/OpenCvRightEndInspector.cs`
- Delete: `src/TieCornerInspection.WinForms/Services/IRightmostWhiteStripeLocator.cs`
- Delete: `src/TieCornerInspection.WinForms/Services/ColumnProjectionWhiteStripeLocator.cs`
- Create: `tests/TieCornerInspection.Tests/EndBoundaryConsensusTests.cs`

**Interfaces:**
- Consumes: `IRightEndBoundaryLocator.TryLocate(...)` 和 `EndBoundaryCandidate`。
- Produces: `InspectionRegionResult.HasEndBoundary`、`EndBoundaryBounds`、`ExtractedEndBounds`、`EndBoundaryPolygon`、`BoundaryLocationSource`。
- Produces: `EndBoundaryLocationSource.None`、`LocalTexture`、`ImageConsensus`。

- [ ] **Step 1: 写部分无纹理行由共识补齐的失败测试**

```csharp
[Fact]
public void Inspect_OneSmoothRow_UsesOtherRowsToCompleteBoundary()
{
    using Mat image = CreateFiveRowImage(smoothRowIndex: 3, out int expectedBoundaryX);
    using InspectionResult result = new OpenCvRightEndInspector().Inspect(image, CreateSyntheticParameters(image));

    Assert.Equal(5, result.Regions.Count);
    InspectionRegionResult completed = result.Regions[2];
    Assert.True(completed.HasEndBoundary);
    Assert.Equal(EndBoundaryLocationSource.ImageConsensus, completed.BoundaryLocationSource);
    Assert.InRange(completed.EndBoundaryBounds.Right, expectedBoundaryX - 3, expectedBoundaryX + 3);
}
```

- [ ] **Step 2: 写离群点不得拉偏共识的失败测试**

构造 7 行组件，其中 6 行真实边界形成轻微斜线，第 4 行定位器返回偏移 45 像素的高值；断言待补齐行预测位置与真实线误差不超过 3 像素。

Run: `dotnet test tests\TieCornerInspection.Tests\TieCornerInspection.Tests.csproj -c Release --filter FullyQualifiedName~EndBoundaryConsensusTests`

Expected: 编译失败于新结果属性或测试断言失败于当前单遍实现。

- [ ] **Step 3: 增加定位来源和结果属性**

```csharp
public enum EndBoundaryLocationSource
{
    None,
    LocalTexture,
    ImageConsensus
}

public bool HasEndBoundary { get; set; }
public Rect EndBoundaryBounds { get; set; }
public Point2f[] EndBoundaryPolygon { get; set; } = Array.Empty<Point2f>();
public EndBoundaryLocationSource BoundaryLocationSource { get; set; }
```

移除 `HasRightmostWhiteStripe`、`WhiteStripeBounds` 和 `WhiteStripePolygon`，让模型语义与新算法一致。

- [ ] **Step 4: 把测量改为两遍流程**

新增私有可释放 `PendingRegionMeasurement`，保存候选框、旋正组件、局部候选、端帽指标和最终边界。第一遍只旋正并调用局部定位器；第二遍完成共识后统一创建 `InspectionRegionResult` 和预览 tile。所有保存的 `AlignedComponent` 在批次 `finally` 中释放。

- [ ] **Step 5: 实现稳健直线共识**

新增私有静态方法：

```csharp
private static bool TryFitBoundaryConsensus(
    IReadOnlyList<PendingRegionMeasurement> measurements,
    out BoundaryConsensus consensus);
```

至少需要 3 个 `LocalTexture` 候选。先计算所有点对斜率的中位数，再计算 `x - slope * y` 的中位数作为截距；计算残差中位数和 MAD，剔除大于 `Max(3, 3 * MAD)` 的点后重复一次。`BoundaryConsensus.PredictX(double y)` 返回原图 X。

- [ ] **Step 6: 映射预测位置并局部细化**

`AlignedComponent` 保存正向旋转矩阵并新增：

```csharp
public int MapImageXToAligned(
    double imageX,
    double imageY,
    Rect componentBounds,
    Rect regionOfInterest);
```

把共识预测点映射到旋正局部 X，调用 `_boundaryLocator.TryRefineNear(aligned.Binary, aligned.Color, predictedX, alignedHeight, out candidate)`；无法细化时使用预测 X。限制到 `[2, alignedWidth - 3]`。`EndBoundaryBounds` 使用 `new Rect(finalBoundaryX - 1, projectionTop, 1, projectionHeight)`，保证 `EndBoundaryBounds.Right` 等于端头裁剪起点。

- [ ] **Step 7: 运行共识测试完成 GREEN**

Run: Task 3 测试命令。

Expected: 无纹理行来源为 `ImageConsensus`，离群点测试误差不超过 3 像素，0 个失败。

- [ ] **Step 8: 更新任务记录检查点**

记录两遍资源生命周期、Theil–Sen 中位斜率、MAD 离群排除和 RED/GREEN 结果。

---

### Task 4: 现有测试迁移与 NG 批量 GREEN

**Files:**
- Modify: `tests/TieCornerInspection.Tests/OpenCvRightEndInspectorTests.cs`
- Modify: `tests/TieCornerInspection.Tests/SlantedCutSampleInspectionTests.cs`
- Modify: `tests/TieCornerInspection.Tests/NgFolderBatchInspectionTests.cs`
- Delete: `tests/TieCornerInspection.Tests/ColumnProjectionWhiteStripeLocatorTests.cs`：其行为迁移到新定位器测试后删除。
- Modify: `src/TieCornerInspection.WinForms/Services/OpenCvRightEndInspector.cs`

**Interfaces:**
- Consumes: 新端头边界结果模型和定位器。
- Produces: 旧样图、先前现场图和 45 张 NG 图全部通过的新语义回归。

- [ ] **Step 1: 把现有集成断言改为端头边界语义**

所有 `HasRightmostWhiteStripe` 改为 `HasEndBoundary`；`WhiteStripeBounds.Right` 改为 `EndBoundaryBounds.Right`；`WhiteStripePolygon` 改为 `EndBoundaryPolygon`。端头裁剪继续断言：

```csharp
Assert.Equal(region.EndBoundaryBounds.Right, region.ExtractedEndBounds.X);
Assert.Equal(region.AlignedComponentSize.Width, region.ExtractedEndBounds.Right);
Assert.Equal(region.AlignedComponentSize.Height, region.ExtractedEndBounds.Height);
```

- [ ] **Step 2: 把 NG 批量测试改为最终契约**

对 45 张图片逐张断言：区域数大于 0；每个区域 `HasEndBoundary` 为真；裁剪宽度至少 3；边界和裁剪矩形合法；定位来源不能为 `None`。输出每张图的局部数、共识补齐数和失败数。

- [ ] **Step 3: 运行 NG 批量测试并逐项修正算法参数**

Run: `dotnet test tests\TieCornerInspection.Tests\TieCornerInspection.Tests.csproj -c Release --filter FullyQualifiedName~NgFolderBatchInspectionTests --logger "console;verbosity=detailed"`

Expected: 45 张图全部完成；最终失败图片 0 张、失败区域 0 个。参数只允许使用扎带高度、组件宽度、纹理统计量和同图共识残差归一化，不允许加入文件名或绝对 X。

- [ ] **Step 4: 运行全部算法和旧样图测试**

Run: `dotnet test TieCornerInspection.sln --no-restore -c Release --logger "console;verbosity=normal"`

Expected: 所有测试通过；原先 13 个行为回归没有丢失。

- [ ] **Step 5: 更新任务记录检查点**

记录 45 张图片总区域数、局部定位数、共识补齐数、最终失败数和全部测试数量。

---

### Task 5: WinForms 端头边界界面语义

**Files:**
- Modify: `src/TieCornerInspection.WinForms/MainForm.Designer.cs`
- Modify: `src/TieCornerInspection.WinForms/MainForm.cs`
- Modify: `src/TieCornerInspection.WinForms/Services/OpenCvRightEndInspector.cs`

**Interfaces:**
- Consumes: `HasEndBoundary`、`EndBoundaryBounds`、`BoundaryLocationSource`。
- Produces: “端头起点 X”“端头裁剪宽度”“定位来源”表格列和批量定位状态摘要。

- [ ] **Step 1: 在 Designer 文件替换表格列**

把“最右白纹 X”改为“端头起点 X”，把“白纹状态”改为“定位来源”。不新增运行时动态列；所有列仍在 `MainForm.Designer.cs` 中创建并加入 `resultGrid.Columns`。

- [ ] **Step 2: 更新结果行和状态栏**

`MainForm.ShowResults` 映射为中文：`LocalTexture => "局部纹理"`、`ImageConsensus => "整体补齐"`、`None => "未找到"`。状态栏显示：

```text
检测到 N 根扎带；局部定位 L 根；整体补齐 C 根；未找到 F 根。
```

- [ ] **Step 3: 更新预览和标注颜色语义**

红线表示最终端头起点；青色一列宽四边形表示边界；只有 `None` 使用橙线并显示“未找到端头边界”。删除代码和注释中“最右白纹”的当前行为描述。

- [ ] **Step 4: 构建验证 Designer 与代码一致**

Run: `dotnet build TieCornerInspection.sln --no-restore -c Release`

Expected: 警告 0 个，错误 0 个。

- [ ] **Step 5: 更新任务记录检查点**

记录表格、状态栏、预览颜色和 Designer 控件约束。

---

### Task 6: 文档、同步与最终验证

**Files:**
- Modify: `README.md`
- Modify: `任务记录.md`
- Sync: 工作副本到 `D:\MyCode\PublicWook\扎带缺角检测`

**Interfaces:**
- Consumes: 完成的算法、模型、测试和界面。
- Produces: 可直接运行的最终 Demo、批量验证证据和更新后的使用说明。

- [ ] **Step 1: 更新 README**

说明“齿纹结束→光滑端头”、自适应纹理统计、同图共识补齐、定位来源列、红/橙预览线含义，以及 NG 文件夹批量验证结果。删除固定亮度差 40 和“最右白纹”作为当前主算法的描述。

- [ ] **Step 2: 完整测试**

Run: `dotnet test TieCornerInspection.sln --no-restore -c Release --logger "console;verbosity=normal"`

Expected: 全部测试通过，失败 0 个，跳过 0 个。

- [ ] **Step 3: Release 构建**

Run: `dotnet build TieCornerInspection.sln --no-restore -c Release`

Expected: 警告 0 个，错误 0 个。

- [ ] **Step 4: 同步并校验哈希**

使用 `robocopy /E` 排除 `bin`、`obj`、`.vs` 和 `*.user`。对工作副本所有交付文件逐一计算 SHA-256，目标目录缺失或哈希不同数量必须为 0。

- [ ] **Step 5: 最终目录重复验证**

Run: `dotnet test TieCornerInspection.sln --no-restore -c Release`

Run: `dotnet build TieCornerInspection.sln --no-restore -c Release`

Expected: 测试失败 0 个；构建警告 0 个、错误 0 个。

- [ ] **Step 6: WinForms 启动检查**

隐藏启动最终 Release EXE 3 秒。进程保持运行则记录通过并关闭；若提前退出，记录退出码并停止交付声明。

- [ ] **Step 7: 写入最终任务记录**

记录测试总数、45 张 NG 图片的总区域数、局部/补齐/失败数量、构建警告和错误数、文件哈希结果及启动检查结果。
