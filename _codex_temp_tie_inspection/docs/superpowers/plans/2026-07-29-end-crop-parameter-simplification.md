# 端头截取参数精简 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 删除圆头测量和缺陷判定参数与代码，只保留黑色区域提取，并使用“纹理灵敏度”和“起点修正(px)”把截取起点定位到最后一条可信纹路右侧。

**Architecture:** `InspectionParameters` 保存界面输入，独立 `EndBoundaryLocatorOptions` 把纹理灵敏度传入可替换定位器，检查器在局部定位或同图补齐后统一应用起点修正。定位器由“最高置信候选”改为“最靠右可信候选”，圆度与判定链路整体删除。

**Tech Stack:** C#、.NET 8、WinForms、OpenCvSharp、xUnit。

## Global Constraints

- 所有 WinForms 控件必须在 `MainForm.Designer.cs` 中定义并加入布局。
- 所有界面文字和代码注释使用简体中文。
- 新增属性、字段、方法、类和接口必须有 XML 注释。
- 保持接口化定位器设计，避免 WinForms 依赖进入算法实现。
- Demo 目录不是 Git 仓库，本计划不执行提交步骤；每次代码变更追加到 `任务记录.md`。

---

### Task 1: 最靠右可信纹理与灵敏度

**Files:**
- Create: `src/TieCornerInspection.WinForms/Models/EndBoundaryLocatorOptions.cs`
- Modify: `src/TieCornerInspection.WinForms/Services/IRightEndBoundaryLocator.cs`
- Modify: `src/TieCornerInspection.WinForms/Services/TextureTransitionEndBoundaryLocator.cs`
- Modify: `tests/TieCornerInspection.Tests/TextureTransitionEndBoundaryLocatorTests.cs`

**Interfaces:**
- Produces: `readonly record struct EndBoundaryLocatorOptions(int TextureSensitivity)`。
- Produces: `TryLocate(Mat binary, Mat color, EndBoundaryLocatorOptions options, out EndBoundaryCandidate candidate)`。
- Produces: `TryRefineNear(Mat binary, Mat color, int predictedBoundaryX, int searchRadius, EndBoundaryLocatorOptions options, out EndBoundaryCandidate candidate)`。

- [ ] **Step 1: 写最强转折右侧存在弱纹路的失败测试**

构造宽 240、高 24 的扎带：强纹理区结束于 X=120，随后保留三条较弱纹路直到 X=154，光滑端头从 X=155 开始。灵敏度 70 时断言 `BoundaryX` 位于 152～158；当前最高置信策略应错误落在约 120。

- [ ] **Step 2: 写灵敏度方向测试并确认 RED**

同一弱纹组件分别传入灵敏度 20 和 80；高灵敏度结果必须不小于低灵敏度结果，并落在最后弱纹右侧。运行：

```powershell
dotnet test tests\TieCornerInspection.Tests\TieCornerInspection.Tests.csproj -c Release --filter FullyQualifiedName~TextureTransitionEndBoundaryLocatorTests
```

Expected: 编译失败于 `EndBoundaryLocatorOptions` 或新接口不存在。

- [ ] **Step 3: 实现选项校验和最靠右候选选择**

灵敏度限制到 0～100，映射最低置信度：

```csharp
double minimumConfidence = 1.60 - options.TextureSensitivity / 100.0;
```

把最小光滑端头比例改为 0.25；扫描候选时保存最靠右且超过 `minimumConfidence` 的位置。右侧显著沿细化保持在合法范围，圆头高亮排除测试必须继续通过。

- [ ] **Step 4: 运行定位器测试完成 GREEN**

运行 Task 1 Step 2 命令，要求定位器全部测试通过。

---

### Task 2: 起点修正与圆头计算链路删除

**Files:**
- Modify: `src/TieCornerInspection.WinForms/Models/InspectionParameters.cs`
- Modify: `src/TieCornerInspection.WinForms/Models/InspectionRegionResult.cs`
- Modify: `src/TieCornerInspection.WinForms/Models/InspectionResult.cs`
- Modify: `src/TieCornerInspection.WinForms/Services/IRightEndInspector.cs`
- Modify: `src/TieCornerInspection.WinForms/Services/OpenCvRightEndInspector.cs`
- Modify: `tests/TieCornerInspection.Tests/OpenCvRightEndInspectorTests.cs`
- Modify: `tests/TieCornerInspection.Tests/EndBoundaryConsensusTests.cs`

**Interfaces:**
- Produces: `InspectionParameters.TextureSensitivity`，默认 60。
- Produces: `InspectionParameters.EndBoundaryOffsetPixels`，默认 0。
- Removes: `EndCapWidthHeightRatio`、`RoundCapScoreThreshold`、`MaximumNegativeDeviationPercent` 及全部圆头结果属性。

- [ ] **Step 1: 写正负起点修正失败测试**

对同一合成图分别使用偏移 0、+8、-6；断言最终 `ExtractedEndBounds.X` 分别变化 0、+8、-6。增加 +10000 越界输入，断言最终仍保留至少 3 像素端头。

- [ ] **Step 2: 运行检查器定向测试确认 RED**

```powershell
dotnet test tests\TieCornerInspection.Tests\TieCornerInspection.Tests.csproj -c Release --filter "FullyQualifiedName~OpenCvRightEndInspectorTests|FullyQualifiedName~EndBoundaryConsensusTests"
```

Expected: 编译失败于新参数不存在。

- [ ] **Step 3: 把灵敏度传入局部定位和共识细化**

检查器创建一次 `EndBoundaryLocatorOptions`，局部 `TryLocate` 和共识 `TryRefineNear` 使用同一选项，保证两种来源参数含义一致。

- [ ] **Step 4: 统一应用并限制起点修正**

在最终候选确定后执行：

```csharp
int adjustedX = Math.Clamp(
    candidate.BoundaryX + parameters.EndBoundaryOffsetPixels,
    2,
    aligned.Binary.Width - 3);
```

边界矩形、端头裁剪和原图多边形全部使用 `adjustedX`。

- [ ] **Step 5: 删除圆头计算与判定链路**

删除端帽截取、最大轮廓圆度、圆头匹配度、相对中位数偏差和缺陷判定方法；精简 `PendingRegionMeasurement`、`InspectionRegionResult` 和标注文字。标注图只画序号、端头边界和端头区域。

- [ ] **Step 6: 运行定向测试完成 GREEN**

运行 Task 2 Step 2 命令，要求全部通过且无旧圆头属性引用。

---

### Task 3: Designer 参数控件与结果表格精简

**Files:**
- Modify: `src/TieCornerInspection.WinForms/MainForm.Designer.cs`
- Modify: `src/TieCornerInspection.WinForms/MainForm.cs`

**Interfaces:**
- Consumes: `TextureSensitivity`、`EndBoundaryOffsetPixels`。
- Produces: 中文控件“纹理灵敏度”“起点修正(px)”和精简结果表格。

- [ ] **Step 1: 从 Designer 删除旧控件**

删除 `labelCapRatio`、`numericCapRatio`、`labelCircularityThreshold`、`numericCircularityThreshold`、`labelRelativeThreshold`、`numericRelativeThreshold` 的字段、实例化、布局加入、配置和初始化生命周期调用。

- [ ] **Step 2: 在 Designer 加入两个新控件**

新增并完整加入 `parameterPanel`：

- `numericTextureSensitivity`：整数 0～100，默认 60，步长 5。
- `numericEndBoundaryOffset`：整数 -100～100，默认 0，步长 1。

控件文字分别为“纹理灵敏度”“起点修正(px)”。

- [ ] **Step 3: 精简表格和结果显示**

删除端帽面积、端帽周长、圆度、圆头匹配度、相对偏差和判定列。`ShowResults` 只填序号、X、Y、宽度、高度、区域面积、端头起点 X、裁剪宽度和定位来源，不再按缺陷或最低圆度着色。

- [ ] **Step 4: 把新控件值写入检测参数**

`CreateParametersFromControls()` 读取两个新数值框；状态栏继续显示局部纹理、整体补齐和未定位数量。

- [ ] **Step 5: 编译检查 Designer 完整性**

```powershell
dotnet build TieCornerInspection.sln --no-restore -c Release
```

Expected: 0 警告、0 错误，所有控件均由 Designer 字段引用。

---

### Task 4: 回归测试、文档、同步和交付

**Files:**
- Modify: `tests/TieCornerInspection.Tests/SampleImageInspectionTests.cs`
- Modify: `tests/TieCornerInspection.Tests/SlantedCutSampleInspectionTests.cs`
- Modify: `tests/TieCornerInspection.Tests/NgFolderBatchInspectionTests.cs`
- Modify: `README.md`
- Modify: `任务记录.md`
- Sync: `D:\MyCode\PublicWook\扎带缺角检测`

**Interfaces:**
- Consumes: 精简结果模型和新参数。
- Produces: 现场样图与 45 张 NG 图的端头截取回归证据。

- [ ] **Step 1: 迁移现场测试**

删除所有圆度、圆头匹配度、相对偏差和判定断言；保留区域数量、端头边界、裁剪几何、定位来源和预览图断言。所有参数工厂补充灵敏度 60、起点修正 0。

- [ ] **Step 2: 运行完整 Release 测试**

```powershell
dotnet test TieCornerInspection.sln --no-restore -c Release --logger "console;verbosity=minimal"
```

Expected: 0 个失败；45 张 NG 图片最终未定位数量为 0。

- [ ] **Step 3: 更新 README 和任务记录**

README 删除圆度和判定说明，说明两个新参数的方向及建议调法。任务记录追加 RED/GREEN、批量数量、构建和启动结果。

- [ ] **Step 4: 扫描旧语义并构建**

```powershell
rg -n "EndCap|Circularity|RoundCap|RelativeDeviation|IsDefective|端帽比例|圆头匹配阈值|相对下降阈值" src tests README.md
dotnet build TieCornerInspection.sln --no-restore -c Release
```

Expected: 当前源代码和界面无旧属性或旧控件；Release 构建 0 警告、0 错误。

- [ ] **Step 5: 同步正式目录并复验**

排除 `bin`、`obj`、`.vs` 和 `*.user` 同步到正式 Demo；计算全部交付文件 SHA-256，差异必须为 0。在正式目录再次执行完整测试、Release 构建和隐藏启动 3 秒检查。
