# 图像缩放与平移 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让原图、检测标注图和黑色二值图支持鼠标缩放、右键平移和一键适应窗口，同时保留原图左键 ROI 框选。

**Architecture:** `ImageViewport` 只维护适应比例、用户缩放倍数和平移量，负责可测试的坐标变换。`ZoomableImageBox` 负责 WinForms 绘制和鼠标事件，三个实例统一在 `MainForm.Designer.cs` 创建；主窗体只负责 ROI 叠加和顶部按钮。

**Tech Stack:** .NET 8、C#、WinForms、xUnit 2.4。

## Global Constraints

- Demo 位于 `D:\MyCode\PublicWook\扎带缺角检测`，不得引用 TDJS-Vision 当前框架代码。
- 所有加入窗体的控件必须在 `MainForm.Designer.cs` 中声明和初始化。
- 所有界面文字使用简体中文。
- 类、属性、字段和方法添加中文注释。
- 原图左键框选 ROI 的行为不得改变。

---

### Task 1: 可测试视口变换

**Files:**
- Create: `src/TieCornerInspection.WinForms/Controls/ImageViewport.cs`
- Create: `tests/TieCornerInspection.Tests/ImageViewportTests.cs`

**Interfaces:**
- Produces: `SetViewport(Size clientSize, Size imageSize)`、`ZoomAt(PointF clientPoint, double factor)`、`PanBy(float dx, float dy)`、`Reset()`、`ClientToImage(PointF)`、`ImageToClient(PointF)`。

- [ ] **Step 1: 编写失败测试**：用 `1000×500` 图像和 `500×500` 视口断言适应比例为 `0.5`；在客户端点 `(250,250)` 放大两倍前后，该点映射的图像坐标保持不变；缩放倍数限制在 `0.1～20.0`。
- [ ] **Step 2: 运行测试确认失败**：执行 `dotnet test --filter FullyQualifiedName~ImageViewportTests`，预期因 `ImageViewport` 不存在而编译失败。
- [ ] **Step 3: 实现最小视口类**：总比例为 `适应比例 × 用户倍数`，缩放时根据锚点反算新平移量。
- [ ] **Step 4: 再次运行测试**：预期全部 `ImageViewportTests` 通过。

### Task 2: WinForms 可缩放查看器

**Files:**
- Create: `src/TieCornerInspection.WinForms/Controls/ZoomableImageBox.cs`
- Modify: `src/TieCornerInspection.WinForms/MainForm.Designer.cs`
- Modify: `src/TieCornerInspection.WinForms/MainForm.cs`

**Interfaces:**
- Consumes: `ImageViewport`。
- Produces: `ZoomIn()`、`ZoomOut()`、`ResetView()`、`TryClientToImage(Point, out Point)`、`ImageToClientRectangle(RectangleF)`。

- [ ] **Step 1: 实现查看器绘制**：双缓冲绘制 Bitmap，使用视口矩阵确定目标矩形。
- [ ] **Step 2: 实现鼠标交互**：滚轮按 `1.2` 倍缩放，右键拖动平移，双击恢复适应窗口。
- [ ] **Step 3: 替换三处 PictureBox**：设计器文件中创建三个 `ZoomableImageBox`，可见提示文字标明缩放操作。
- [ ] **Step 4: 保持 ROI 坐标准确**：主窗体通过查看器坐标变换完成左键框选和红框绘制。
- [ ] **Step 5: 增加顶部按钮**：全部放大、全部缩小、全部适应，按钮事件同时调用三个查看器。

### Task 3: 文档与验收

**Files:**
- Modify: `README.md`
- Modify: `任务记录.md`

**Interfaces:**
- Produces: 缩放操作说明和可复现验证记录。

- [ ] **Step 1: 更新操作说明**：记录滚轮、右键拖动、双击和顶部按钮。
- [ ] **Step 2: 运行 `dotnet test TieCornerInspection.sln -c Release`**：预期全部测试通过。
- [ ] **Step 3: 运行 `dotnet build TieCornerInspection.sln -c Release`**：预期 0 警告、0 错误。
- [ ] **Step 4: 启动 Release 程序做冒烟测试**：程序应保持正常运行，测试后关闭。
