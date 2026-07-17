# 大模型节点保存时预加载实施计划

> 本计划执行已由用户在确认设计后授权，直接在当前工作区完成。

**目标：** 把大模型模板解包、native 初始化和首次预热从流程第一次运行移到参数保存阶段。

**结构：** 模板包负责轻量读取清单；运行适配器负责模型缓存、原子加载和预热；节点暴露异步预加载接口；参数窗体负责保存期间的界面状态和错误提示。

**技术栈：** C#、WinForms、OpenCvSharp、PowerShell 集成测试、MSBuild。

## 任务一：锁定目标行为

- 修改 `Tests/LargeModelIntegration.Tests.ps1`。
- 添加保存时等待预加载、预览不完整解包、阈值不触发重载、预热后原子替换的检查。
- 先运行测试并确认新增检查失败。

## 任务二：轻量读取模板清单

- 修改 `Forms/AiTrainForm/LargeModelTemplatePackage.cs`，新增只读 `manifest.json` 的方法。
- 修改 `Node/3-Detection/LargeModel/ParamFormLargeModelDetection.cs`，预览改用轻量清单读取。

## 任务三：保存时加载并预热

- 修改 `Node/3-Detection/LargeModel/LargeModelDetectionRuntime.cs`，增加预加载、空图预热和成功后原子替换。
- 移除阈值参与加载缓存匹配的逻辑。
- 修改 `Node/3-Detection/LargeModel/NodeLargeModelDetection.cs`，提供异步预加载方法。
- 用同一个配置门协调预加载后的参数提交和流程运行参数读取，避免连续运行竞态。
- 修改参数窗体保存事件，在后台等待预加载，正确恢复界面状态与错误提示。

## 任务四：验证与记录

- 运行大模型集成测试。
- 运行保存预加载并发行为测试。
- 编译 `TDJS-Vision.sln`。
- 检查只改动本任务相关文件，不覆盖用户当前相机开发内容。
- 将结果追加到 `任务记录.md` 并提交本任务文件。
