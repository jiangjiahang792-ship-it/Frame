# 扎带右端缺角检测实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建独立的 OpenCvSharp C# WinForms Demo，分割扎带右端并输出逐区域端帽圆度。

**Architecture:** 界面依赖 `IRightEndInspector` 接口，OpenCV 检测实现与 WinForms 展示隔离。检测限定在 ROI 内，以通道最大值阈值排除彩色标注，连通域按纵向排序后截取右端端帽计算圆度。

**Tech Stack:** .NET 8、C#、WinForms、OpenCvSharp 4.13、xUnit 2.4。

## Global Constraints

- Demo 位于 `D:\MyCode\PublicWook\扎带缺角检测`，不得引用当前 TDJS-Vision 框架代码。
- 所有 WinForms 控件必须放在 `MainForm.Designer.cs`。
- 所有界面文字与任务文档使用简体中文。
- 类、接口、属性、字段和方法添加 XML 或行内中文注释。
- 算法接口化、可替换，并避免全图重复处理。

---

### Task 1: 算法契约与失败测试

**Files:**
- Create: `src/TieCornerInspection.WinForms/Models/InspectionParameters.cs`
- Create: `src/TieCornerInspection.WinForms/Models/InspectionRegionResult.cs`
- Create: `src/TieCornerInspection.WinForms/Models/InspectionResult.cs`
- Create: `src/TieCornerInspection.WinForms/Services/IRightEndInspector.cs`
- Create: `tests/TieCornerInspection.Tests/OpenCvRightEndInspectorTests.cs`

**Interfaces:**
- Produces: `InspectionResult Inspect(Mat source, InspectionParameters parameters)`。

- [ ] **Step 1: 编写失败测试**：构造完整圆头和缺角圆头，断言完整圆头圆度更高；构造三行目标，断言结果从上到下排序且缺角行为最低圆度。
- [ ] **Step 2: 运行测试确认失败**：运行 `dotnet test --no-restore`，预期因 `OpenCvRightEndInspector` 尚不存在而编译失败。
- [ ] **Step 3: 定义最小模型与接口**：属性包含 ROI、黑色阈值、面积、尺寸、端帽比例、闭运算核和判定阈值。

### Task 2: OpenCV 检测实现

**Files:**
- Create: `src/TieCornerInspection.WinForms/Services/OpenCvRightEndInspector.cs`

**Interfaces:**
- Consumes: `InspectionParameters`。
- Produces: `InspectionResult Inspect(Mat source, InspectionParameters parameters)`。

- [ ] **Step 1: 实现通道最大值黑色二值化**：使用 `Cv2.Max` 合并通道并执行反向阈值。
- [ ] **Step 2: 实现连通域过滤和排序**：按面积、宽高过滤，按中心 Y 排序。
- [ ] **Step 3: 实现端帽轮廓圆度**：截取右侧端帽，查找最大外轮廓，计算 `4 * Math.PI * area / (perimeter * perimeter)`。
- [ ] **Step 4: 运行测试确认转绿**：运行 `dotnet test --no-restore`，预期全部通过。
- [ ] **Step 5: 重构并保持测试通过**：拆分参数校验、端帽分析和标注绘制私有方法。

### Task 3: WinForms 界面与 ROI 交互

**Files:**
- Create: `src/TieCornerInspection.WinForms/Program.cs`
- Create: `src/TieCornerInspection.WinForms/MainForm.cs`
- Create: `src/TieCornerInspection.WinForms/MainForm.Designer.cs`

**Interfaces:**
- Consumes: `IRightEndInspector.Inspect`。
- Produces: 可加载图片、拖动 ROI、调参、检测、查看逐区域圆度的中文界面。

- [ ] **Step 1: 在设计器文件创建全部控件**：工具栏、两个 PictureBox、参数 NumericUpDown、DataGridView 和状态栏。
- [ ] **Step 2: 实现图片加载与坐标换算**：拖动框选坐标从 PictureBox 映射到原图，并同步 ROI 数值框。
- [ ] **Step 3: 实现异步检测与结果绑定**：后台执行检测，刷新标注图、二值图和表格，最低圆度行标红。
- [ ] **Step 4: 实现资源释放和中文异常提示**。

### Task 4: 样图、说明与验收

**Files:**
- Create: `samples/扎带右端样图.png`
- Create: `README.md`
- Create: `任务记录.md`

**Interfaces:**
- Produces: 可直接运行和复现实测结果的独立 Demo。

- [ ] **Step 1: 复制用户样图并设置默认 ROI**。
- [ ] **Step 2: 在 README 记录算法、操作方法、参数含义与圆度解释**。
- [ ] **Step 3: 运行完整测试和 Release 构建**：`dotnet test -c Release` 与 `dotnet build -c Release` 均预期退出码 0。
- [ ] **Step 4: 运行样图检测命令并记录逐区域圆度**：确认结果数量、最低圆度区域及标注输出。
- [ ] **Step 5: 将完成项、验证命令和结果写入 `任务记录.md`。
