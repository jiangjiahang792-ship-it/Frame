# TDJS-Vision 独占工作区与方案加载动画设计

## 目标

流程编辑、无监督训练和大模型训练打开后，主窗体立即隐藏，任务栏只保留当前工作窗口；当前工作窗口关闭后，主窗体重新显示、激活并置前。用户手动打开方案时，隐藏主窗体并复用启动机器人动画，显示真实方案、设备、流程和节点加载进度，完成或失败后恢复主窗体。

## 现状与根因

- `FormNewProcessWizard`、`UnsupervisedTrainForm`、`LargeModelTrainForm` 都通过 `FormMain.ShowOwnedMaximizedDialog` 最大化模态显示。
- 模态显示只禁用了主窗体，没有隐藏主窗体，所以 Windows 任务栏同时显示主页面和当前工作窗口。
- 手动打开方案在主 UI 线程直接调用 `Solution.Instance.Load`。同步反序列化链会恢复多个 WinForms 控件，不能整体移动到 `Task.Run`；否则会违反控件线程亲和规则。
- 现有 `StartupSplashController` 已在独立 STA UI 线程运行，适合在主 UI 线程同步加载方案时保持动画响应。

## 方案比较

### 方案 A：统一协调器（采用）

新增 `IExclusiveWorkspacePresenter` 和 `ISolutionLoadingCoordinator`。前者统一管理主窗体隐藏、最大化工作窗口和关闭后的恢复；后者在独立启动页线程显示动画，同时在主 UI 线程执行真实方案反序列化。优点是入口集中、异常路径有 `finally` 兜底、便于后续增加新的全屏工具窗口。

### 方案 B：每个菜单入口直接写 `Hide/Show`

改动少，但流程编辑和两个训练入口会重复代码，后续新增入口容易漏掉异常恢复或前台激活，不符合可插拔要求。

### 方案 C：把工具窗口嵌入主窗体

可以彻底只保留一个顶层窗口，但需要重构现有最大化窗体、资源释放和设计器布局，风险和范围明显超过本次需求。

## 独占工作区设计

`IExclusiveWorkspacePresenter.ShowDialog(Form mainForm, Form workspaceForm)` 接收主窗体和工作窗体。实现按以下顺序执行：

1. 将工作窗体预定位到主窗体区域，并保持最大化。
2. 隐藏主窗体，使其任务栏缩略图消失。
3. 以主窗体为 Owner 模态显示工作窗体。
4. 无论正常关闭还是显示异常，都在 `finally` 中重新显示主窗体并执行 `Activate/BringToFront`。

流程编辑窗体继续复用既有缓存实例；两个 AI 训练窗体继续保持“每次创建、关闭后释放”的生命周期。本次不把 AI 配置工具误归类为 AI 训练窗口。

## 手动打开方案动画设计

`ISolutionLoadingCoordinator.Load(Form mainForm, string solutionPath, bool showInfo)` 负责：

1. 启动 `StartupSplashController`，再隐藏主窗体。
2. 建立 `StartupProgressContext`，在主 UI 线程直接调用 `ConfigHelper.SolLoad`。
3. 等待设备和流程事件链结束，调用 `ThrowIfFailures` 汇总关键错误。
4. 与软件启动保持一致，动画最少显示 2 秒。
5. 在 `finally` 中先重新显示主窗体，再关闭启动页，最后激活并置前主窗体。

不使用 `Solution.Instance.Load`，因为它会捕获并吞掉异常，无法由动画加载协调器统一处理。加载失败时写完整日志，恢复主窗体后显示简体中文错误提示，不让用户停留在无响应动画中。

## 测试与约束

- 先新增回归测试并确认因接口和调用缺失而失败。
- 验证流程编辑、无监督训练和大模型训练全部使用独占工作区接口。
- 验证隐藏主窗体后才显示工作窗体，且所有路径都在 `finally` 中恢复和激活主窗体。
- 验证手动打开方案使用动画协调器，不再调用会吞异常的 `Solution.Instance.Load`。
- 验证方案加载仍在调用线程执行，禁止 `Task.Run` 包裹 `ConfigHelper.SolLoad`。
- 新增类、接口、属性、字段和方法均使用简体中文注释，不新增第三方依赖。
- 当前工作区包含用户已有未提交改动，本任务不自动提交 Git。
