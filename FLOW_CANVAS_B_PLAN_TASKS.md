# ProcessNew Free Flow Canvas B Plan Task List

## Current Plan

We are currently following **Plan B**:

> Upgrade `Forms/ProcessNew` from the existing ordered node list editor into a free flow-canvas editor with a graph model, and finally make process execution follow node connections.

This means the target is not only a visual style change. The work includes:

- Free node placement on a canvas.
- Node connections and start-node semantics.
- Graph data persistence and restore.
- Compatibility with existing `NodeBase` nodes and parameter forms.
- A later execution-engine migration from ordered list execution to connection-driven execution.

## Current Step

The current implementation step is **FlagRead Wait/No-Wait Option Feedback Pass**.

### Latest Progress

- 2026-07-08 旧方案数据升级：
  - [x] 新反馈已记录：桌面 `工位1方案带旋转.Sol` 是旧版 `1.0.0.0` 顺序流程方案，需要按当前方案格式重构数据。
  - [x] 已生成 `工位1方案带旋转_当前版本.Sol`，版本更新为 `2.4.0.0`，保留设备、节点参数、全局信号、检测项配置和 OK/NG 计数。
  - [x] 已为 4 个旧顺序流程补齐 `IsCameraCallbackTriggered`、`HasCanvasGraph`、`ConnectionInfos`、节点画布布局、起始节点和日志开关字段。
  - [x] 已按兼容顺序流程保留 `HasCanvasGraph=false` 并生成 `Legacy` 连线，避免改变 If/EndIf 顺序执行语义。
  - [x] 已完成 JSON 结构校验：5 个设备、4 个流程、49 个节点、45 条连线均有效。
- 2026-07-06 无监督 GPU 模板执行返回 -2 修复：
  - [x] 新反馈已记录：CPU 训练模板可正常加载，但选择 GPU 训练后模板显示训练成功，点击执行会出现 `-2` 返回码。
  - [x] 已定位到新增 `推理批次` 参数对旧节点默认成 1，可能覆盖 GPU 模板 manifest 中的训练批次，导致 GPU 加载/推理返回 `-2`。
  - [x] 已将无监督检测节点 `InferenceBatchSize` 默认值改为 0，表示未显式设置时跟随模板批次。
  - [x] 已在参数窗体回显旧参数时，若推理批次未设置则从模板清单恢复批次，避免旧方案打开后继续使用错误默认值。
- 2026-07-06 无监督检测节点运行参数补充：
  - [x] 新反馈已记录：无监督检测节点参数界面需要增加 `异常阈值`、`推理批次`、`最小缺陷面积` 三个运行参数。
  - [x] 已在无监督检测节点参数对象中新增阈值、推理批次、最小缺陷面积字段，并保留默认值和注释。
  - [x] 已在 `ParamFormUnsupervisedDetection.Designer.cs` 中新增设计器可见的三个数值控件，保存/回显时同步到节点参数。
  - [x] 已让运行适配器优先使用节点参数执行 `LoadModel` 与 `InferBgr`，参数变更会触发模型句柄按新批次/阈值/面积重新加载。
- 2026-07-06 无监督空异常框订阅提示修复：
  - [x] 新反馈已记录：`ROI结果绘制` 订阅无监督输出矩形时，没有异常框会显示“未查到”，但无监督成功空框应表示正常空结果；颜色识别等节点仍需要保留未查到提示。
  - [x] 已将 `ROI结果绘制` 与 `ROI结果绘制2` 的空几何白名单按节点类型扩展到 `UnsupervisedDetection`，仅在来源节点成功运行时隐藏缺失提示。
  - [x] 已扩展无监督检测节点回归测试，检查两个 ROI 绘制节点都覆盖该场景。
- 2026-07-06 无监督训练窗体设计器可见性修复：
  - [x] 新反馈已记录：`UnsupervisedTrainForm` 在设计器里看不到“训练”页，导致无法继续可视化设计训练界面。
  - [x] 已将原生 `TabControl` 页签隐藏从 `.Designer.cs` 移到运行时方法，设计器中保留可见 `检查/训练` 页签。
  - [x] 已增加设计器检测保护，构造期通过 `LicenseManager.UsageMode` 判断 WinForms 设计器环境，避免设计器加载时收起页签。
  - [x] 运行软件时仍会把原生页签收起为 `1x1`，保持自定义深色顶部导航效果不变。
  - [x] 已扩展 `Tests/UnsupervisedTrainForm.Tests.ps1`，回归检查设计器中不再隐藏原生页签，并确保运行时隐藏入口存在。
- 2026-07-06 无监督训练界面顶部背景与文本自适应：
  - [x] 新反馈已记录：顶部页签区域右侧仍是浅色背景，需要与界面保持同一深色背景；按钮文字需要按控件尺寸自适应，避免文字被裁切。
  - [x] 已新增设计器可见的根布局和深色顶部导航面板，原生 `TabControl` 页签头收起为 1 像素，仅作为内容页容器使用。
  - [x] 已增加 `检查/训练` 自定义导航按钮和选中态刷新逻辑，顶部空白区域与检查页背景统一为深色。
  - [x] 已增加按钮文字自适应字号方法，分类按钮、路径按钮、ROI 按钮和训练按钮会按可用宽高缩小文字。
  - [x] 已将右侧 ROI 工具条改为标题独占一行、ROI 按钮位于下一行右侧，长图片名不再和按钮抢同一行宽度。
  - [x] 已扩展 `Tests/UnsupervisedTrainForm.Tests.ps1`，回归检查深色导航、隐藏原生页签、按钮切页和文字自适应入口。
- 2026-07-06 无监督训练界面视觉优化：
  - [x] 新反馈已记录：检查/训练顶部页签太小，左侧分类按钮偏小，右侧预览标题与 ROI 按钮存在文本重叠，训练页背景需要与检查页一致。
  - [x] 已将无监督训练窗体顶部 `检查/训练` 页签改为大号深色导航样式，选中项使用青色文字和底部强调线。
  - [x] 已放大左侧 All/OK/NG 分类按钮和分类标题，提升最大化界面中的可读性。
  - [x] 已把右侧 ROI 工具条改为设计器可见的 `TableLayoutPanel` 伸缩布局，标题占剩余宽度并启用省略，避免长图像名压到按钮上。
  - [x] 已为训练页应用与检查页一致的深色背景、输入框、日志框和操作按钮样式。
  - [x] 已扩展 `Tests/UnsupervisedTrainForm.Tests.ps1`，回归检查大页签、分类按钮、ROI 工具条和训练页深色主题。
- 2026-07-06 无监督训练完成后 UI 卡死修复：
  - [x] 新反馈已记录：点击开始训练后日志显示训练完成，但界面按钮无响应，疑似 UI 线程被完成后的弹窗阻塞。
  - [x] 已定位到 `MessageBoxTD.Show()` 默认无 owner 调用 `ShowDialog()`，在无监督训练这种二级模态窗体内可能隐藏到后方，导致 UI 看似卡死。
  - [x] 已为 `MessageBoxTD` 增加带 owner 的显示重载，并在有 owner 时使用 `CenterParent` 和 `ShowDialog(owner)`。
  - [x] 已将 `UnsupervisedTrainForm` 内所有 `MessageBoxTD.Show` 调整为传入 `this`，确保提示框始终显示在训练窗体前方。
  - [x] 已新增 `Tests/UnsupervisedTrainingDialogOwnership.Tests.ps1`，回归检查无监督训练窗体中的弹窗 owner 约束。
- 2026-07-06 最大化弹窗恢复露底复修：
  - [x] 新反馈已记录：无监督训练窗口从最小化恢复到最大化后仍可能露出主界面底部，`FormNewProcessWizard` 也存在同类问题。
  - [x] 已将 `FormBase` 的最大化边界从一次性固定屏幕工作区改为可刷新逻辑，并处理 `WM_GETMINMAXINFO`，支持 owner 区域最大化。
  - [x] 已取消 `FormMain` 的 MDI 容器属性，并将根布局改为 DockPanel 行百分比填充，避免 DockPanel 工作区高度被 AutoSize 卡住。
  - [x] 已统一 `FormMain` 中最大化弹窗的打开入口，无监督训练和流程编辑窗口都按主窗体 owner 边界显示。
  - [x] 已新增 `Tests/FormWindowMaximizeBehavior.Tests.ps1` 并更新菜单入口测试，回归检查最大化边界与主窗体布局约束。
- 2026-07-06 无监督训练检查页界面配置：
  - [x] 新反馈已记录：检查页希望更接近参考标注工具，采用左侧分类、深色缩略图网格、右侧 ROI 预览的操作界面。
  - [x] 已扩大左侧分类栏与缩略图网格空间，缩略图卡片改为深色大卡片，并用 OK/NG/选中颜色绘制边框。
  - [x] 已为检查页应用深色运行时主题，路径栏、分类按钮、缩略图容器、ROI 工具栏和状态栏统一为标注工具风格。
  - [x] 已新增最大化边界修正逻辑，窗体显示和从最小化恢复到最大化时优先覆盖父窗口区域，避免底部露出主界面。
  - [x] 已扩展 `Tests/UnsupervisedTrainForm.Tests.ps1`，回归检查深色检查页主题和最大化边界修正入口。
- 2026-07-06 FormMain 无监督训练菜单接线：
  - [x] 新反馈已记录：`FormMain` 中的 `toolStripMenuItem2` 就是无监督训练窗口入口，需要接到已完成的无监督训练窗体。
  - [x] 已在设计器中为 `toolStripMenuItem2` 绑定点击事件，保证 WinForms 设计器可见。
  - [x] 已在 `FormMain` 中懒加载并显示 `UnsupervisedTrainForm`，避免主窗体启动时加载或检查无监督 native 依赖。
  - [x] 已新增 `Tests/FormMainUnsupervisedMenu.Tests.ps1` 回归检查，防止菜单入口再次断开。
- 2026-07-06 无监督运行环境落位：
  - [x] 新反馈已记录：训练时报错“无监督环境缺少 AnomalibLib.dll”，需要把参考工程运行环境放到当前软件运行目录。
  - [x] 已确认当前运行目录 `bin\Debug` 缺少 `UnsupervisedDll`，参考工程运行库目录为 `D:\MyCode\PublicWook\家有的无监督算法\App_Main_CPU\App_Main_CPU\AnomalibRuntime`。
  - [x] 已将整套 `AnomalibRuntime` 复制到 `D:\MyCode\PublicWook\TDJS-Vision\bin\Debug\UnsupervisedDll`，包含 `AnomalibLib.dll`、`device.license`、Python 环境、anomalib、预训练权重和推理依赖。
  - [x] 已验证 `AnomalibLib.dll`、`device.license`、`python_env\python.exe`、`python_env_gpu\Scripts\python.exe` 均存在。
  - [x] 已根据实际运行输出目录补充复制到 `D:\MyCode\PublicWook\TDJS-Vision\bin\x64\Debug\UnsupervisedDll`，该目录下同样已验证 `AnomalibLib.dll` 与 Python 环境存在。
- 2026-07-06 无监督训练图片数量前置校验：
  - [x] 新反馈已记录：`TrainModel` 返回 `-2` 时，当前实际原因可能是 NG 图片数量不足，需要在 `UnsupervisedTrainingService` 调用 native 前提前拦截并弹窗提示。
  - [x] 已在数据集准备完成后校验实际写入数量：OK 少于 2 张直接提示；NG 为 1 张直接提示“最少 2 张，如需纯 OK 训练请不要放入 NG 图像”。
  - [x] 已复用训练窗体已有异常弹框链路，服务层抛出的中文异常会直接显示到 `MessageBoxTD`。
  - [x] 已扩展 `Tests/UnsupervisedTemplatePackage.Tests.ps1`，防止图片数量前置校验被移除。
- 2026-07-03 诊断日志公共懒执行入口：
  - [x] 新反馈已记录：诊断信息很多时，不能在日志等级关闭后仍提前执行 `GetRuntimeText()`、图像摘要和相机分段诊断文本。
  - [x] `LogHelper` 已新增统一记录判定入口，集中处理系统设置日志等级和“仅记录异常日志”菜单过滤。
  - [x] `PerformanceSpikeDiagnostics` 已新增公共诊断判定入口，先检查缓存日志等级，再按需执行 `Func<string>` 生成诊断文本。
  - [x] `PerformanceSpikeDiagnostics` 已新增慢诊断懒执行入口，先检查日志等级和耗时阈值，再采集运行时现场。
  - [x] `CameraHik` 的回调 Mat 转换分段日志已改为 Debug 慢日志，正常帧不再每次生成分段详情。
  - [x] `NodeImageSource` 和 `Process.TryPrepareSkippedNodeRunResult` 的高频链路诊断已改为公共懒执行，避免 Debug 关闭时仍采集线程、内存、GDI、GC 和图像摘要。
  - [x] `ShowImageControl` 和 `FrmSingleImage` 的图像显示性能诊断已改为公共懒执行入口，Debug 关闭时不再提前生成图像摘要、显示结果摘要和运行时现场。
  - [x] `PerformanceSpikeDiagnostics.LogNodeMemoryIfNeeded` 已在 Debug 关闭时直接跳过节点内存水位采样，避免未开启诊断时仍查询进程内存、GDI 和句柄资源。
  - [x] 已新增 `Tests/DiagnosticLazyEvaluation.Tests.ps1` 回归检查，防止高频诊断再次在 `LogHelper.AddLog` 过滤前提前构建重诊断文本。
- 2026-07-03 相机回调图运行快速路径：
  - [x] 新反馈已记录：高频相机回调中，图像源回调结果准备后进入首个图像旋转节点前的通用图调度耗时仍偏高，需要减少直链场景固定开销。
  - [x] `Process.Run(NodeBase,bool)` 已增加相机回调快速路径：当跳过的图像源只有一个默认下游且该下游为 `ImageRotate`，直接运行首个图像旋转节点，再从其单一下游恢复通用图运行时。
  - [x] 复杂图、多个默认下游、非图像旋转首节点、非画布图或不满足并行图运行条件时，自动回退原有 `RunConnectedNodes(nodeImage, null, nodeImage)` 逻辑。
  - [x] 已新增 `Tests/CameraCallbackFastPath.Tests.ps1` 回归检查，防止快速路径绕过图像源结果准备或误用于多个下游的复杂场景。
- 2026-07-03 无监督训练界面第一阶段：
  - [x] 新反馈已记录：本阶段先完善 `Forms/AiTrainForm/UnsupervisedTrainForm` 训练界面，不做流程识别节点；无监督环境必须隔离在运行目录 `UnsupervisedDll`，缺失时不影响软件启动。
  - [x] 检查页已增加图像路径选择、递归加载、All/OK/NG 分类、OK 绿色边框和 NG 红色边框缩略图、右侧图像预览与单 ROI 绘制入口。
  - [x] ROI 已改为可选：不使用 ROI 时按整图训练；使用 ROI 时训练前校验只能有一个矩形 ROI，并把 ROI 参数写入模板清单。
  - [x] 训练页已增加模型类型、阈值、最小面积、输入尺寸、训练轮数、BatchSize、CPU/GPU/AUTO、模板名称、运行目录 `Model` 输出路径、进度条和训练日志。
  - [x] 已新增 `UnsupervisedRuntimeBootstrapper`、`UnsupervisedAnomalibDetector`、`UnsupervisedTrainingService` 和 `UnsupervisedTemplatePackage`，训练时才延迟加载 `UnsupervisedDll\AnomalibLib.dll`。
  - [x] 训练成功后将 `model.onnx` 与 `manifest.json` 打包为单个 `.tdunsup` 模板文件，输出限定在运行目录 `Model` 文件夹。
  - [x] 已新增无监督训练窗体布局、运行环境隔离和模板打包回归脚本。
- 2026-07-03 本地分段诊断日志降为 Debug：
  - [x] 新反馈已记录：相机回调、AI、颜色识别、ROI结果绘制、图像显示、保存图像中的本地分段诊断不应继续占用 Info，应受系统设置 Debug 开关控制。
  - [x] `NodeImageSource` 的相机回调入队、专用线程执行、链路诊断、性能诊断和慢诊断日志已改为 `Debug`，忙碌拒绝仍保留 `Warn`。
  - [x] `NodeTDAI` 的 AI 检测阶段、模型检查、通信读取、推理完成、结果运算和解析结果摘要日志已改为 `Debug`，节点运行结果和异常等级保持不变。
  - [x] `NodeColorDiscern`、`NodeParamFormColorDiscern`、`NodeResultOverlayDraw`、`NodeImageShow`、`NodeSaveImage`、`ParamFormSaveImage` 的分段/性能/内存诊断日志已改为 `Debug`。
  - [x] 已新增 `Tests/DiagnosticLogLevel.Tests.ps1` 回归检查，防止点名节点的诊断日志再次退回 `Info/Warn`。
  - [x] 验证通过：诊断日志等级回归、日志等级开关回归、既有两个源码回归、`git diff --check`、MSBuild `Debug|Any CPU` 构建均通过，构建 0 个错误。
- 2026-07-03 系统设置日志等级记录开关：
  - [x] 新反馈已记录：`FrmSystemSetting.tableLayoutPanel2` 已有 Debug/Info/Warn/Exception/Fatal 日志等级选项，但 `LogHelper.AddLog` 还没有按这些选项隔开判定。
  - [x] 已新增 `LogDebugEnabled`、`LogInfoEnabled`、`LogWarnEnabled`、`LogExceptionEnabled`、`LogFatalEnabled` 用户级设置，默认保持调试关闭、其它等级开启。
  - [x] 已新增 `LogLevelSettings` 统一读取和保存日志等级开关，并使用内存缓存降低相机回调、AI、性能诊断等高频日志入口开销。
  - [x] `FrmSystemSetting` 已加载并保存日志等级复选框，设计器中的复选框文本已改为中文显示并绑定变更事件。
  - [x] `LogHelper.AddLog` 已在入队写文件前执行等级开关过滤，关闭的等级不再落盘或刷新日志界面。
  - [x] 已新增 `Tests/LogLevelSettings.Tests.ps1` 回归检查，防止日志等级设置链路再次断开。
  - [x] 验证通过：日志等级回归检查、既有两个源码回归检查、MSBuild `Debug|Any CPU` 构建均通过，构建 0 个错误。
- 2026-07-02 端子右AI文本不显示排查与修复：
  - [x] 新反馈已记录：节点(56.ROI结果绘制)订阅节点(30.AI检测)输出时，日志显示检测框存在但显示结果文本为0，画面只显示矩形框。
  - [x] 已确认方案节点56文本项订阅 `30.AI检测 / AI输出结果`，不是手填空文本；根因为 `BiteGlueLength` 缺少依赖框时解析器提前 `return`，导致最终 `AlgorithmResult.Texts` 未赋值。
  - [x] `RL12Parse`、`RL12angondingParse` 与 `XMSGParse` 已改为缺少咬胶长度依赖框或比例分母为0时仅将当前检测项按0/NG继续解析，保留已生成矩形和后续文本汇总。
  - [x] 已新增 `Tests/TDAIParseNoEarlyReturn.Tests.ps1` 回归检查，防止咬胶长度缺依赖框再次提前退出整个AI解析。
- 2026-07-02 瑞玲6相机版本方案切换旧回调释放：
  - [x] 新反馈已记录：`D:\MyCode\PublicWook\TDJS-Vision` 仓库 `瑞玲6相机版本` 分支同样需要在删除流程、重置方案、打开新方案前释放旧流程节点资源，避免旧图像源节点相机回调继续触发旧流程。
  - [x] `Solution.RemoveProcess(Process)` 与 `Solution.RemoveProcess(string)` 已在移除流程和节点前释放流程内节点资源，并使用 `ToList()` 保证移除节点时遍历安全。
  - [x] `Solution.SolReset()` 已在清空流程和节点前释放全部流程节点资源，保留原有 2D/3D 相机、光源、PLC、Modbus、TCP、COM 设备释放逻辑。
  - [x] 已新增 `Tests/SolutionResourceRelease.Tests.ps1` 回归检查，防止后续再次漏掉流程资源释放顺序。
- 2026-07-02 主链路一眼定位诊断：
  - [x] 新反馈已记录：现场需要一次运行就能判断慢耗时或结果缺失到底卡在流程调度、图像源回调结果准备、节点内部还是线程收尾。
  - [x] `Process.TryPrepareSkippedNodeRunResult` 已增加 `【链路诊断-跳过节点准备开始/结束/异常/跳过】` 日志，输出流程批次、节点类型、Provider程序集、主程序路径、结果前后摘要和Provider耗时。
  - [x] `NodeImageSource.TryPrepareSkippedRunResult` 已增加 `【链路诊断-图像源回调结果入口/取回调Mat/构建输出/状态写入/出口】` 日志，确认回调Mat、OutputImage和Result写入是否真实发生。
  - [x] `NodeImageSource.RunPendingCallbackFrame` 已拆分专用线程收尾日志，记录清理待处理Mat、释放运行状态和线程总耗时，避免流程结束后黑盒等待。
  - [x] `Program.Main` 和 `NodeImageSource.RunPendingCallbackFrame` 已增加 `诊断版本=2026-07-02-链路诊断V2` 程序身份日志，直接输出主程序路径、入口程序集和当前目录，避免现场运行产物无法确认。
  - [x] 编译验证：MSBuild `Debug|Any CPU` 和 `Release|x64` 均通过，0 个错误；既有警告仍保留。
- 2026-07-02 流程图并行完成驱动调度：
  - [x] `Process.RunConnectedNodesParallel` 已从批次全部完成后推进，改为按节点完成顺序推进。
  - [x] 爱光测量方案中 `ROI结果绘制 -> 图像显示` 不再需要等待旁路 `延迟执行` 节点完成后才被调度。
  - [x] 已保留顺序单节点直接执行、条件分支跳过、失败下游禁用和并行信号等待预登记释放逻辑。
- 2026-07-02 单流程图像源黑盒耗时诊断：
  - [x] 新反馈已记录：单流程日志中“流程开始到图像旋转前”存在 19~106ms 黑盒耗时，需要拆开回调图像源跳过执行时的结果准备链路。
  - [x] `NodeImageSource.TryPrepareSkippedRunResult` 已增加 `【性能诊断-图像源回调结果】` 日志，拆分取回调Mat、结果对象、构建输出、状态写入、结果赋值和总耗时。
  - [x] 图像源回调结果准备超过通用阈值时追加 `【慢诊断-图像源回调结果】`，记录线程池、内存、句柄、GDI对象和GC现场。
- 2026-06-30 内存泄漏与保存图像诊断：
  - [x] 新反馈已记录：软件内存从约600MB上涨到约5000MB，需要在本方案节点链路增加内存泄漏诊断，重点怀疑保存图像。
  - [x] `PerformanceSpikeDiagnostics` 已新增进程内存快照，记录工作集、私有内存、托管堆、线程数、句柄数、GDI对象、USER对象和GC次数。
  - [x] `NodeBase.SetRunResult` 已增加低频节点水位日志，内存/GDI/句柄跨步增长或高水位时记录刚完成的流程和节点。
  - [x] `ParamFormImageSave.GetImage` 已增加保存图像取图内存诊断，拆分订阅读取、`ToBitmap`、可绘制位图转换和写入标注。
  - [x] `NodeImageSave` 已增加保存入队、跳过保存、原始临时Bitmap释放、队列启动/停止、队列消费和保存任务Bitmap释放诊断。
  - [x] `NodeImageSave` 已补充保存Bitmap所有权保护：未入队时释放临时Bitmap，多路径保存时避免同一Bitmap被多个保存线程同时保存和重复释放。
- 2026-06-30 ROI/图像显示/旋转耗时毛刺诊断：
  - [x] 新反馈已记录：排除首次软件打开后的冷启动后，现场仍希望把图像旋转、图像显示/ToBitmap、窗口UI排队和ROI结果绘制稳定压回5ms以内。
  - [x] 已新增慢耗时诊断工具，慢日志包含线程池、进程线程、内存、GC计数、图像尺寸和显示结果数量。
  - [x] `NodeImageRotate` 已增加旋转节点慢诊断，拆分订阅读取、取源图、旋转彩图、取灰度、灰度处理和输出构建。
  - [x] `NodeImageShow` 已增加图像显示慢诊断，补充 `ToBitmap` 毛刺时的输出图像、显示结果和运行时状态。
  - [x] `FrmSingleImage` 与 `ShowImageControl` 已增加窗口投递、UI排队、SetImage、旧图释放和静态ROI构建慢诊断。
  - [x] `ResultOverlayDraw` 已增加ROI结果绘制慢诊断，记录图像订阅源、输入输出图像摘要和运行时状态。
- 2026-06-29 数线芯远程ROI不绘制诊断：
  - [x] 新反馈已记录：本地测试可以绘制，远程日志显示线芯SEG推理有结果但 `ROI结果绘制` 输出矩形与文本均为0。
  - [x] `NodeTDAI` 在线芯截面SEG解析成功后新增解析结果摘要日志，记录检测结果、矩形、NG缓存矩形和文本数量。
  - [x] `NodeTDAI` 对SEG模型名称没有匹配解析器的情况改为明确异常，避免远程因模型名称选错而出现AI节点成功但ROI无数据的静默假成功。
- 2026-06-29 海康相机硬触发 Mat 回调链路：
  - [x] 新反馈已记录：现场日志确认硬触发回调卡在 SDK `inputImage.ToBitmap()`，需要彻底绕开回调阶段的 Bitmap 转换。
  - [x] `ICamera` 与 `CameraHik` 已增加 `OnMatReceived` 事件，硬触发流程改用 `IImage -> Mat` 分发，旧 `OnImageReceived` 保留给 Bitmap 兼容路径。
  - [x] `CameraHik.GetImageCallBack` 已改为直接复制 SDK 转换后的像素内存到独立 `Mat`，并记录 `ConvertPixelType`、`CopyToMat`、`RGB转BGR` 和回调总耗时。
  - [x] `CameraHik.GetImageCallBack` 已增加旧 `OnImageReceived` 订阅告警，现场若仍有 Bitmap 回调订阅会直接暴露出来。
  - [x] `NodeImageSource` 已增加 `Mat` 回调入口，忙碌、未运行或未启用时立即释放帧，避免拒收帧前再做 Bitmap 转换。
  - [x] `ParamFormImageSource` 的相机硬触发绑定已切换到 `OnMatReceived`，方案运行会走新的 Mat 链路。
  - [x] Build-verified with MSBuild on 2026-06-29, 0 errors; existing warnings remain.
- 2026-06-29 相机回调忙碌拒绝与日志等级整理：
  - [x] 新反馈已记录：硬触发流程若当前帧正在处理，新帧直接丢弃并打印日志；普通阶段日志不要使用 `Fatal/Exception` 等错误等级。
  - [x] `NodeImageSource` 已将回调流程执行从 `Task.Run` 切换为节点专用后台线程，回调只负责入队并唤醒专用线程。
  - [x] `NodeImageSource` 已按忙碌拒绝策略统计接收帧、处理帧、忙碌丢帧，并在 `Process.IsRuning` 或专用线程忙碌时释放当前帧后输出 Warn 日志。
  - [x] `CameraHik`、`NodeImageSource`、`NodeTDAI`、`ColorDiscern` 的正常阶段/性能诊断日志已降为 `Info`，真正失败日志继续保留高等级。
  - [x] Build-verified with MSBuild on 2026-06-29, 0 errors; existing warnings remain.
- 2026-06-29 ROI结果绘制阶段日志增强：
  - [x] 新反馈已记录：需要在 ROI 结果绘制链路加入阶段性日志，判断耗时来自等待订阅、结果构建还是实际绘制。
  - [x] `ResultOverlayDraw` 已增加图像订阅读取、取图引用、绘制项构建、输出对象构建、文本/线/矩形/区域绘制项耗时和输出元素数量日志。
  - [x] `ResultOverlayDraw2` 已增加图像订阅读取、颜色规则判断、取图引用、绘制项构建、输出对象构建、分类型绘制项耗时和输出元素数量日志。
  - [x] `ShowImageControl` 已增加静态 ROI 构建明细日志，拆分 UI 排队等待、矩形、NG矩形、线、圆/圆弧/椭圆、轮廓、文本和实际 Bitmap 绘制耗时。
  - [x] `NodeImageDraw` 已增加取图、取AI/颜色结果、`ToBitmap`、实际绘制、`ToMat` 阶段耗时日志。
  - [x] 编译验证：MSBuild `TDJS-Vision.sln` 的 `Debug|Any CPU` 通过，184 个既有警告、0 个错误。
- 2026-06-28 线芯截面SEG矩形模式优化：
  - [x] 新反馈已记录：线芯截面SEG只需要矩形结果，不需要分割掩膜和不规则四角框后处理。
  - [x] `YoloOpenVinoCpuSession.DetectSeg` 已增加按需复制/解析掩膜参数，矩形模式下跳过 `CopyMasks` 与 `ExtractMaskBox`。
  - [x] `Yolo8Seg.Detect` 已透传是否需要掩膜四角框的参数，默认保留原完整SEG行为。
  - [x] `NodeTDAI` 在线芯截面模型下走矩形模式，其它SEG模型继续保留完整掩膜后处理。
- 2026-06-28 线芯截面解析恢复桌面版聚拢度算法：
  - [x] 新反馈已记录：线芯截面解析改回桌面版质心距离法，不再在解析文件中绘制图像。
  - [x] `LineCoreFront_C1_Seg.Parse` 已移除图像参数，解析过程不再调用 `Cv2.Circle` 修改输入图。
  - [x] `LineCoreFront_C1_Seg` 已用 `SeparateOutlierRects` 计算每个线芯中心到质心的距离，并按检测项上限区分OK/NG矩形。
  - [x] `NodeTDAI` 调用线芯截面解析时不再传入 `inputImage.Bitmaps[0]`，改为传入 `Process` 以累计OK/NG总数。
- 2026-06-21 海康 3D 相机设备结构搭建：
  - [x] 新反馈已记录：在 `Device` 下创建 3D 相机目录，用于放置海康 3D 相机对象、接口和数据结构，设备对象先继承 `IDevice` 体系。
  - [x] 已新增 `Device/3D/I3DCamera.cs`，定义 3D 相机设备抽象、帧事件、状态事件、打开关闭、采集、软触发、主动取帧和原生点云显示接口。
  - [x] 已新增 `Device/3D/Camera3DModels.cs`，定义 3D 设备信息、连接配置、采集配置、深度帧、点云点、原生点云缓存和事件参数。
  - [x] 已新增 `Device/3D/CameraHik3D.cs`，提供可序列化的海康 3D 相机设备骨架，SDK 连接与取图逻辑后续接入。
  - [x] `Solution` 已增加 `Camera3DDevices` 过滤属性，项目文件已加入 3D 相机新增源码编译项。
  - [x] Build-verified with MSBuild on 2026-06-21, 0 errors; existing warnings remain.
- 2026-06-21 3D 相机接入相机管理界面：
  - [x] 新反馈已记录：3D 相机可以放入 `Forms/CameraAdd` 相机管理界面中，完成后再做 3D 图像源节点。
  - [x] 已新增 `SingleCamera3D` 及设计器文件，用于在相机列表中显示、选择、连接和移除 3D 相机。
  - [x] 已新增 `Camera3DParamsShowControl` 及设计器文件，用于显示 3D 相机设备名、SN、IP、图像模式、触发方式、原生点云缓存和连接状态。
  - [x] `FrmCameraListView` 已支持反序列化、添加、选择和移除 `I3DCamera` 设备。
  - [x] `FrmCameraInfo` 已预留“海康3D”品牌入口和 3D 相机添加事件，SDK 枚举接入后可直接显示在线设备。
  - [x] Build-verified with MSBuild on 2026-06-21, 0 errors; existing warnings remain.
- 2026-06-12 流程画布细节体验优化：
  - [x] 新反馈已记录：复制粘贴后的节点显示文本不要追加“复制”字样；点击节点后需要清楚高亮输入、输出连线及关联节点；画布、节点、连线右键菜单内容需要区分；画布空间或节点与连线字体需要更紧凑。
  - [x] 粘贴节点命名已改为优先保留原节点名称，重名时仅追加 `_2/_3` 数字编号，不再追加“复制”文本。
  - [x] `ProcessFlowCanvas` 已增加单选节点关系高亮缓存，输入连线与输入节点使用蓝色、输出连线与输出节点使用橙色并加粗显示。
  - [x] 流程画布右键菜单已按空白画布、节点、连线三类目标动态显示不同命令，避免三类对象菜单内容混在一起。
  - [x] 画布逻辑空间已扩大到 `3200x2200`，节点标题、状态文本和分支标签字体已适当调小，便于大流程查看。
  - [x] Build-verified with MSBuild on 2026-06-12, 0 errors; existing warnings remain.
- 2026-06-09 中心端子侧面AI节点空引用诊断：
  - [x] 新反馈已记录：中心端子侧面流程运行时，133与142的AI检测节点会在图像获取和模型推理之间直接空引用失败，需要判断是否为141/134裁剪输出空图或AI模型未加载。
  - [x] `NodeTDAI` 已恢复运行前AI模型就绪等待与校验，避免 `Yolo8` 句柄为空时直接触发空引用。
  - [x] `NodeTDAI` 已增加输入图像摘要日志，记录订阅来源、原图、灰度图、`Bitmaps[0]`、ROI数量和首个ROI范围。
  - [x] `NodeTDAI` 失败日志已追加模型状态与输入图像摘要，下一次现场日志可直接区分图像为空、模型未加载、模型类型不匹配等原因。
- 2026-06-08 临时断线订阅保留与自动恢复：
  - [x] 新反馈已记录：流程中间断线、删除中间连接或插入新节点后重新连接时，下游节点应自动恢复到原来的订阅对象。
  - [x] 已撤回误方向的“拖拽落点替换节点”实现，避免流程编辑时出现额外替换弹窗。
  - [x] `NodeSubscription` 断线刷新时不再清空原订阅文本，只临时置空运行态订阅对象。
  - [x] 重新连线后 `ConnectionsChanged` 会按保留的 `ID.节点名 + 结果属性` 自动恢复订阅。
  - [x] 真正删除订阅源节点时仍会清空订阅，避免引用不存在节点。
  - [x] Build-verified with MSBuild on 2026-06-08, 0 errors; existing warnings remain.
- 2026-06-08 ROI绘制AI结果颜色与图像显示耗时诊断：
  - [x] 新反馈已记录：ROI结果绘制订阅AI输出时，整节点NG会导致所有检测项文本和矩形框都变红，无法区分单项OK/NG。
  - [x] `ResultOverlayDraw` 已改为AI/检测类 `AlgorithmResult` 保留每个检测项文本和矩形框自身颜色，不再被整节点 `IsAllOk` 颜色覆盖。
  - [x] `ResultOverlayDraw2` 已同步兼容AI/检测类 `AlgorithmResult` 的元素级颜色保留。
  - [x] 新反馈已记录：图像显示从约0ms升到约14ms，需要先分段日志定位。
  - [x] `NodeImageShow` 已增加订阅读取、图像尺寸、`ToBitmap`、事件发布和状态写入分段耗时日志。
  - [x] `FrmSingleImage` 与 `ShowImageControl` 已增加UI投递、排队、`SetImage`、静态ROI构建和刷新耗时日志。
  - [x] 现场日志确认：ROI构建约0ms，主要耗时来自4024x3036大图 `Mat.ToBitmap` 约19-20ms，以及图像窗口替换35MB位图时的 `SetImage` 约14-16ms。
  - [x] `ShowImageControl.ImageBitmap` 已移除换图时整图OpenCV灰度缓存，鼠标像素灰度改为按当前点从RGB计算，避免每帧UI线程处理整张大图。
  - [x] `ShowImageControl.ImageBitmap` 已将上一帧大位图释放移到后台任务，降低UI线程连续刷新两个图像窗口时的排队等待。
  - [x] Build-verified with MSBuild on 2026-06-08, 0 errors; existing warnings remain.
- 2026-06-08 图像窗口手动调参按ROI绘制来源过滤：
  - [x] 新反馈已记录：图像窗口1打开手动调参时不应显示全流程AI检测项，只显示当前图像显示订阅的ROI绘制项相关上下限。
  - [x] `FrmSingleImage.button2_Click` 已按当前图像显示节点解析 `ResultOverlayDraw/ResultOverlayDraw2` 的绘制项来源节点。
  - [x] `FormSolRunParam` 与 `SolRunParamControl` 已支持接收运行参数显示范围。
  - [x] 多条件运行参数表已按条件 `SourceNodeId` 过滤，只显示当前ROI绘制来源节点对应的开启项。
  - [x] Build-verified with MSBuild on 2026-06-08, 0 errors; existing warnings remain.
- 2026-06-08 手动调参卡死诊断：
  - [x] 新反馈已记录：现场方案“中心端子正面检测”运行一次后点击图像窗口“手动调参”可能卡死。
  - [x] 已检查现场方案，多条件运行参数开启项集中在多条件节点29，当前运行参数表不会产生大量行。
  - [x] `FrmSingleImage.button2_Click` 已增加手动调参诊断日志，并在流程仍在运行时阻止打开参数窗体。
  - [x] `FormSolRunParam.Shown` 与 `SolRunParamControl.Init/运行参数表刷新` 已增加阶段耗时日志，用于定位卡在建窗体、读取相机参数、加载AI检测项或刷新多条件运行参数。
  - [x] Build-verified with MSBuild on 2026-06-08, 0 errors; existing warnings remain.
- 2026-06-08 多条件运行参数表接入：
  - [x] 新反馈已记录：多条件项按 `DetectItemDic` 一样的“下限、当前值、上限、检测项”方式显示，检测项名称显示多条件模块注释。
  - [x] 多条件参数表新增“运行参数”勾选列，默认关闭；勾选后该条件会进入 `SolRunParam` 运行参数表。
  - [x] `SolRunParam` 已支持AI检测项和多条件项混合显示，流程运行后多条件当前值会同步刷新。
  - [x] 保存运行参数时，AI行继续写回 `DetectItemDic`，多条件行按操作符写回对应条件的值1/值2和运行参数开关。
  - [x] Build-verified with MSBuild on 2026-06-08, 0 errors; existing warnings remain.
- 2026-06-07 测量与四则运算结果三位小数输出：
  - [x] 新反馈已记录：测量工具模块与四则运算输出结果统一只保留小数点后三位。
  - [x] 新增测量结果统一舍入工具，测量节点对外发布的坐标、距离、角度、半径、耗时和点集合统一三位小数。
  - [x] 四则运算运行和预览固定使用三位小数输出，旧配置中的其它小数位会被运行时规则覆盖。
  - [x] 四则运算动态变量表和默认结果值均按三位小数发布，内部运算过程仍保持原始精度。
  - [x] Build-verified with MSBuild on 2026-06-07, 0 errors; existing warnings remain.
- 2026-06-07 四则运算判定颜色链路补齐：
  - [x] 新反馈已记录：多条件模块会判定四则运算结果数值，后续 `ResultOverlayDraw` 需要按该判定显示OK/NG颜色。
  - [x] `NodeResultArithmeticOperation` 已实现 `IJudgmentResult`，新增默认 `JudgeOk=true` 的“判定OK”属性。
  - [x] 四则运算成功结果默认 `JudgeOk=true`，失败结果默认 `JudgeOk=false`，多条件模块后续可继续按条件回写该值。
  - [x] Build-verified with MSBuild on 2026-06-07, 0 errors; existing warnings remain.
- 2026-06-07 测量判定回写与结果绘制按判定颜色改造：
  - [x] 新反馈已记录：测量结果默认带OK/NG布尔判定，多条件模块负责改写判定值，绘制工具按判定值自动选择OK/NG颜色。
  - [x] 已新增通用 `IJudgmentResult` 接口，并给 `Node\4-Measurement` 下现有测量结果补充默认 `JudgeOk=true` 的判定属性。
  - [x] 多条件模块已在每条条件评估后把匹配结果回写到订阅源结果，多个条件命中同一结果时按与逻辑累积。
  - [x] 多条件节点已支持默认分支作为公共路径，True/False 分支判定后默认分支也会继续执行。
  - [x] `ResultOverlayDraw` 参数和界面已新增“按判定”颜色模式，支持固定色、OK色、NG色，并将新增控件放入 `.Designer.cs`。
  - [x] `ResultOverlayDraw` 运行绘制已按源结果判定值选择OK/NG颜色，未找到判定值时回退固定颜色兼容旧配置。
  - [x] Build-verified with MSBuild on 2026-06-07, 0 errors; existing warnings remain.
- 2026-06-05 相机回调触发流程循环运行修复：
  - [x] 新反馈已记录：勾选“相机回调触发流程”后，主窗体循环运行没有看到相机进入回调。
  - [x] 已确认原逻辑只绑定相机帧事件，主窗体循环会跳过相机回调流程，但没有主动启动相机取流。
  - [x] `Solution.Run(true)` 已在运行态启动相机回调流程中的相机取流，并在退出时只停止本次启动的相机。
  - [x] 软触发相机回调流程已按循环间隔发出软触发，硬触发和连续取流保持由相机帧事件触发。
  - [x] 单流程面板循环运行在相机回调模式下不再普通采图，而是保持回调运行态并同步相机取流。
  - [x] Build-verified with MSBuild on 2026-06-05, 0 errors; existing warnings remain.
  - [x] 反馈修正：相机已由设备面板提前取流时，进入相机回调流程会先重启取流，保证 SDK 帧事件订阅发生在 `StartGrabbing()` 前。
  - [x] 反馈修正：图像源相机触发源反显兼容 `SOFT/LINE0` 等枚举名，避免旧方案重新保存后软触发源丢失。
  - [x] Build-verified after callback restart fix with MSBuild on 2026-06-05, 0 errors; existing warnings remain.
- 2026-06-05 ResultOverlayDraw2 独立节点新增：
  - [x] 新反馈已记录：保留原 `ResultOverlayDraw` 模块不变，新增 `ResultOverlayDraw2` 作为独立ROI结果绘制节点。
  - [x] `ResultOverlayDraw2` 已支持多个上游布尔结果订阅规则，并按“全部为真=OK”或“任一为真=OK”聚合后选择OK/NG绘制颜色。
  - [x] 颜色规则已支持选择多条件判定节点的整体结果或某一项条件明细。
  - [x] 参数界面已简化为输入图像、颜色判定规则、绘制项和预览四块，并将新增控件放入 `.Designer.cs`。
  - [x] Build-verified with MSBuild on 2026-06-05, 0 errors; existing warnings remain.
- 2026-06-05 ImageSource 共享变量图像源补齐：
  - [x] 新反馈已记录：`ImageSource` 的 `comboBoxImgSource` 已有“共享变量”选项，但缺少对应界面保存和运行逻辑。
  - [x] `ParamFormImageSource` 已新增设计器可见的共享变量选择控件，保存参数时记录共享变量名。
  - [x] `NodeImageSource` 已支持从共享变量读取 `OutputImage`、`Mat`、`Bitmap` 以及常见图像列表，并归一化为图像源输出。
  - [x] Build-verified with MSBuild on 2026-06-05, 0 errors; existing warnings remain.
- 2026-06-04 TDAI 动态检测项默认模板兜底：
  - [x] 新反馈已记录：动态通信解析出的检测项配置不存在时，需要从默认模板复制一份。
  - [x] `NodeTDAI` 已改为先解析通信值对应的检测项名称，再校验或复制 `DetectItemDic` 配置，并在默认模板缺失时给出明确异常。
  - [x] 反馈修正：`ParamFormTDAI` 打开通信检测项配置窗口前后会补齐并刷新检测项列表，`TDAICommuntionDetectionConfig` 加载和导入配置时也会把空检测项名称按“模板+触发值”创建出来。
  - [x] 反馈修正：`NodeTDAI` 通信切换当前检测项和运行完成后通知 `SolRunParam`，运行参数表会及时刷新到当前通信检测项的公差上下限。
- CaliperLine performance diagnostics pass:
  - [x] 2026-06-02 新反馈已记录：测量节点为了保护 `ImageSource` 原图而反复深拷贝，可能造成严重性能透支。
  - [x] 已给 `CaliperLine` 增加分段耗时日志，拆分订阅读取、深拷贝图、灰度准备、位置修正、算法、输出图准备、结果组装、结果发布和节点总耗时。
  - [x] 运行日志确认：4024x3036x3 图像下，卡尺算法约 0.21ms，但深拷贝、全图灰度转换和输出图准备合计约 50ms。
  - [x] 已将 `ImageSource` 输出扩展为原图、兼容旧节点的 `Bitmaps[0]` 和灰度图缓存 `GrayImg`，让只读测量节点复用源头灰度图。
  - [x] `CaliperLine` 已切换为默认读取 `OutputImage.GrayImg`，运行时不再深拷贝输入图、不再重复整图转灰度、不再输出完整图像，只输出结果字段和 `OutputImage.DisplayResult` 叠加层。
  - [x] Measurement folder GrayImg rollout: `CaliperCircle`、`CaliperEllipse`、`FindPoint`、`LineLineAngle`、`PointLineDistance`、`PointPointDistance`、`PointRegionDistance` 已统一改为算法读取 `GrayImg`、预览读取只读原图引用、运行结果只输出字段和 `DisplayResult`。
  - [x] 订阅型组合测量在运行时不再读取图像，只有绘制 ROI 并需要卡尺算法时才读取灰度图。
  - [x] 已删除 `CaliperLine` 临时性能诊断日志，保留已验证的 `GrayImg` 只读输入和 `DisplayResult` 输出模式。
  - [x] 已给 `FindPoint` 增加分段耗时日志，拆分订阅读取、灰度取图、临时灰度、位置修正/参数准备、算法、结果组装、结果发布和节点总耗时。
  - [x] 修复 `FindPointTimingInfo` 放在 WinForms 参数窗体类前导致 `.resx` manifest 资源名跑偏的问题，确保打开方案时能找到 `NodeParamFormFindPoint.resources`。
  - [x] `FindPoint` 算法从整图 `GaussianBlur/Canny/Mask/FindContours` 改为按 ROI 包围矩形局部处理，并在性能日志中追加 `ROI区域` 和 `ROI像素约`，用于确认算法实际处理面积。
  - [x] 2026-06-02 新反馈已记录：`1-Acquisition`、`2-ImagePreprocessing` 中也存在类似保护性深拷贝和 `Mat/Bitmap` 往返转换。
  - [x] `OutputImage` 增加统一 `FromSingleImage`、`HasValidImage`、`BuildGrayImage` 工具，单通道图像直接复用为 `GrayImg`，彩色图像只转换一次。
  - [x] `ImageSource` 相机回调去掉 `Bitmap.Clone -> ToMat -> Mat.Clone` 双重拷贝，单帧相机采集后及时释放临时 `Bitmap`。
  - [x] `ImageRotate` 运行时同步传播/旋转上游 `GrayImg`，零角度继续透传源图引用，避免后续测量重新整图灰度化。
  - [x] `ImageCrop` 运行时不再为了 ROI 裁剪把整图转成 `Bitmap` 塞入参数窗体，改为按保存 ROI 坐标直接从 `Mat` 裁剪，并同步裁剪第一张 `GrayImg` 缓存。
  - [x] `ImageSplit` 运行时从上游 `Mat` 直接分割，去掉 `Mat -> Bitmap -> Mat` 往返，记录分割矩形并同步第一张 `GrayImg` 缓存；成功路径恢复为 `ContinueRun`。
  - [x] Build-verified after acquisition/preprocessing deep-copy cleanup with MSBuild on 2026-06-02, 0 errors; existing warnings remain.
  - [ ] 后续按同一模式评估 `3-Detection` 和 `7-ResultProcessing` 节点。
- Completed through the first **Phase 10: Runtime State On Canvas** code pass.
- Fixed one graph runtime scheduling regression found during Phase 10 checks:
  - [x] For `A -> B` plus standalone `C`, running `A` now completes the `A -> B` component before moving to independent source node `C`.
- Fixed two additional runtime regressions found during Phase 10 checks:
  - [x] Restored/new `If` graph connections now interpret `Right` as True and `Bottom` as False even when older saved connection data still has `Branch = Default`.
  - [x] During a process run, subscriptions can no longer read stale results from an upstream node that has not successfully run in the current run.
- Addressed latest UI feedback with a second Phase 10 runtime pass:
  - [x] Graph-native `If = false` no longer falls back to legacy ordered-list continuation when no matching `Else/EndIf` exists.
  - [x] Runtime subscription freshness now uses the current process run id, so disabled or branch-skipped upstream nodes cannot provide prior-run results.
- Phase 10 UI regression status:
  - [x] Simple, branch, disabled, failed, stopped, and runtime-state-refresh checks passed in the UI.
  - [ ] Passive-triggered graph-flow verification is intentionally deferred.
- Phase 18 composite measurement planning:
  - [x] New request captured: integrate line-to-line angle, point-to-point distance, and point-to-region distance measurement nodes.
  - [x] Use `D:\MyCode\PrivateMyComputerCoder\Test\_OpenCv颜色2` as the manual-tool algorithm reference.
  - [x] Each new tool should support subscribing upstream measurement results and drawing local ROI geometry.
  - [x] Local drawn measurement should use existing caliper algorithms where applicable instead of only manual coordinate clicks.
  - [x] Each new tool should expose position-correction options for local drawn ROIs.
  - [x] Parameter UI should include Basic Parameters and Run Parameters only; no Result Display page.
- Phase 20 edge point finding planning:
  - [x] New request captured: add a `FindPoint` measurement node under the measurement toolbox category.
  - [x] The tool should subscribe one image input, let the user draw/confirm one or more ROI regions, and find all edge points inside those regions.
  - [x] Edge point extraction should follow/trace edge contours where possible, so the output is ordered edge point groups instead of only an unordered threshold point cloud.
  - [x] The first design should reuse the existing clean image plus `OutputImage.DisplayResult` overlay model; runtime output image pixels stay clean.
  - [x] The result should be reusable by downstream measurement, condition, ROI drawing, and point/region subscription logic.
- Phase 21 geometry creation planning:
  - [x] New request captured: add a new toolbox category named `图形创建` for tools that create reusable geometry results.
  - [x] Add a `LineMergeFit` / `线组合拟合` node that subscribes two upstream line results, such as two `CaliperLine` results, and outputs one new line segment.
  - [x] The new line segment should expose `StartX`, `StartY`, `EndX`, `EndY`, `CenterX`, `CenterY`, `Angle`, `Length`, `FitError`, and display overlay output.
  - [x] The result should be directly reusable by `PointLineDistance`, `LineLineAngle`, `ResultOverlayDraw`, `MultiCondition`, and future geometry tools.
  - [x] First implementation should support collinear merge fitting and parallel centerline fitting.
- Phase 21 geometry creation first implementation pass:
  - [x] Added `NodeType.LineMergeFit` and the `Node\8-GeometryCreation\LineMergeFit` node family.
  - [x] Added the new `图形创建` toolbox category and `线组合拟合` module entry.
  - [x] `LineMergeFit` supports collinear merge fitting and parallel centerline fitting from two upstream line results.
  - [x] `LineMergeFit` exposes standard line result properties for downstream subscription and draws clean overlay output through `OutputImage.DisplayResult`.
  - [x] `LineMergeFit` parameter form now includes a preview image panel that displays source lines, fit points, and the merged output line after local Run.
  - [x] `CaliperLine` now publishes `EdgePoints`, so line merge fitting can reuse actual caliper edge points instead of only four endpoints when available.
  - [x] Build-verified after implementation with MSBuild, 0 errors; existing warnings remain.
- Phase 22 arithmetic operation planning:
  - [x] New request captured: add a calculation tool module for addition, subtraction, multiplication, and division.
  - [x] User-confirmed toolbox direction: place this node under the existing `逻辑工具` category.
  - [x] First node name: `ArithmeticOperation` / `四则运算`.
  - [x] The tool should support constants and subscribed upstream numeric result properties.
  - [x] The tool should freely subscribe numeric values from other modules, such as line center coordinates, template match counts, scores, lengths, angles, and distances.
  - [x] The tool should support using its own generated intermediate variables/results in later calculation rows without creating graph self-subscription cycles.
  - [x] The tool should output a reusable numeric result for `MultiCondition`, message/logging tools, result drawing text, PLC/data send, and future calculation nodes.
- Phase 22 arithmetic operation first implementation pass:
  - [x] Added `NodeType.ArithmeticOperation` at the end of the enum to preserve existing serialized enum values.
  - [x] Added the `Node\6-LogicTool\ArithmeticOperation` node family with parameter, result, algorithm, node, and designer-visible parameter form files.
  - [x] Added row-based constant/subscription/internal-variable operands with add, subtract, multiply, and divide.
  - [x] Added an upstream numeric property picker and previous-row variable picker in the parameter UI.
  - [x] Registered `四则运算` under `逻辑工具`, language keys, toolbox icon mapping, canvas color, node factory, and project compile items.
  - [x] Build-verified after implementation with MSBuild, 0 errors; existing warnings remain.
- Phase 22 arithmetic operation row-output improvement pass:
  - [x] Reworked calculation rows to `值1 - 运算方式 - 值2 - 输出变量名 - 注释`, so each enabled row emits an independent named variable instead of only accumulating one final value.
  - [x] Added shared dynamic result variable interfaces and resolver support so named outputs can be advertised before runtime and read from completed results.
  - [x] Updated the arithmetic parameter UI with designer-visible value1/value2 operand editing, upstream dynamic-variable picking, and previous-row variable picking.
- ImageRotate runtime performance investigation and fix:
  - [x] Checked the provided 5-camera solution and found five ImageRotate nodes: four use `Angle = -270.0`, one uses `Angle = 0.0`.
  - [x] Removed runtime `Mat -> Bitmap -> Mat` conversion and PictureBox preview refresh from `NodeImageRotate.Run`.
  - [x] Added direct `Mat` input access, 0-degree pass-through, 90/180/270-degree `Cv2.Rotate` fast paths, and GDI preview image cleanup.
  - [x] Downstream standard subscriptions and `MultiCondition` now expose arithmetic outputs under `变量.xxx` / `输出变量`.
  - [x] Feedback fix: `ResultOverlayDraw` text items now read arithmetic named variables through the dynamic-variable resolver instead of only static result properties.
  - [x] Build-verified after the improvement pass with MSBuild, 0 errors; existing warnings remain.
- Phase 22.7 ModbusRead dynamic value subscription feedback pass:
  - [x] New feedback captured: `NodeParamFormMultiCondition` could see the `ModbusRead` result object but could not subscribe individual values read from address 1 with count 10.
  - [x] Root cause confirmed: `ModbusReadResult.Data` stored the values as an array object, while subscription pickers only expose fixed display properties and dynamic variables.
  - [x] Added dynamic variable type metadata so subscribers can know whether a dynamic value is bool, short, ushort, int, float, double, long, or ulong.
  - [x] `ModbusRead` now declares per-value dynamic outputs from its saved start address and count, such as `值01(地址1)` through `值10(地址10)`.
  - [x] `NodeResultModbusRead` now resolves those dynamic outputs to the actual array element produced by the last read.
  - [x] `MultiCondition` now uses dynamic variable type metadata instead of treating all dynamic outputs as `double`.
  - [x] Build-verified after implementation with MSBuild on 2026-05-27, 0 errors; existing warnings remain.
- Phase 22.8 MultiCondition mixed upstream subscription null-value investigation:
  - [x] New feedback captured: one `MultiCondition` subscribes point-to-point distance, line-to-line angle, and arithmetic named output; point-to-point distance compares normally, while line angle and arithmetic output hit `actualValue == null` and evaluate false.
  - [x] Initial code reading captured: `MultiConditionEvaluator` sets `actualValue = null` when the source result is null, the source node has not successfully produced a result for the current run id, or the configured property/dynamic-variable path reads null.
  - [ ] Suspected line-to-line angle cause: `NodeResultLineLineAngle.Angle` is nullable and is assigned only when the measurement result succeeds; runtime-level success with result-level NG can still leave `Angle = null`.
  - [ ] Suspected arithmetic cause: named outputs are read through `DynamicResultVariableResolver` using `$variable:变量名`; if the arithmetic node is branch-skipped/not successful in the current run, or the selected variable path/name is wrong, `MultiCondition` will not get a current numeric value.
  - [x] Added focused diagnostics showing per-condition source node, current-run success state, property path, actual value, and null reason before changing execution logic.
  - [x] `NodeResultMultiCondition.DiagnosticsText` now exposes a readable per-condition summary, and null failed conditions write a targeted warning log.
  - [x] Runtime feedback confirmed one null case was caused by `102.LineLineAngle` not running in the current batch: source run id was 1, current run id was 2, source status was `Unexecuted`.
  - [x] Added graph scheduling support for subscription dependencies so `MultiCondition` waits for configured source nodes before evaluating when those sources are active in the same graph component.
  - [x] Feedback log check on 2026-05-30: `中心端子工位` node `167.MultiCondition` read `215.ArithmeticOperation.V2` before node 215 had produced a current-run result, while the saved solution declares `167` subscribing `$variable:V2` from node 215.
  - [x] Widened hidden subscription dependency scheduling: a subscribed source in the same graph component is now treated as a wait dependency unless it is structurally downstream of the subscriber, so sibling branches such as `120/102 -> 215` and `171 -> 167` can still synchronize through the subscription.
  - [x] Build-verified after implementation with MSBuild on 2026-05-28, 0 errors; existing warnings remain.
- Phase 11.1 cross-process node copy/paste planning:
  - [x] New request captured on 2026-05-28: current node copy/paste is process-local; add support for copying selected nodes from one process tab and pasting them into another process tab.
  - [x] Copy buffer now survives active process/canvas switching through a shared clone snapshot and does not depend on the source process UI panel.
  - [x] Cross-process paste creates fresh target-process node instances, assigns new node IDs, makes node names unique with the `_复制` suffix, and restores parameters, notes, active state, size, and layout.
  - [x] Multi-node cross-process paste keeps copied relative positions and remaps internal graph connections from source node IDs to target node IDs.
  - [x] Parameter references that point to nodes copied in the same batch are remapped; references to source-process-only nodes outside the copied batch are cleared and reported as unresolved.
  - [x] Remapping covers both property-based parameters and older public-field/string-array subscription parameters.
  - [x] Paste into the target process registers as one grouped undo/redo edit and refreshes node count, selection, subscription pickers, and canvas overlays.
  - [x] Build-verified after implementation with MSBuild on 2026-05-28, 0 errors; existing warnings remain.
  - [ ] UI-verify single-node and multi-node cross-process copy/paste between two process tabs, including internal connections, parameter form restore, undo/redo, save/load, and unresolved external-reference handling.
- FlagRead wait/no-wait option feedback pass:
  - [x] New request captured on 2026-05-28: add a designer-visible `是否等待` checkbox to the `FlagRead` parameter form.
  - [x] Default checked state preserves the original behavior: wait in a loop until the selected listen signal becomes true, then continue downstream.
  - [x] When unchecked, `FlagRead` reads the flag once; true continues downstream, false records a normal successful read result and returns `StopBranch`, so graph execution stops only this node's downstream branch while legacy ordered execution still treats it as an early process end.
  - [x] Feedback fix: parallel graph execution no longer lets one unchecked `FlagRead=false` stop the whole ready batch/process component.
  - [x] Feedback fix: parallel waiting `FlagRead` nodes that read the same listen signal no longer race on `Reset`; reset is delayed until the current waiting readers have passed.
  - [x] Build-verified after implementation with MSBuild on 2026-05-28, 0 errors; existing warnings remain.
  - [x] Build-verified after the parallel `FlagRead` fix with MSBuild on 2026-05-28, 0 errors; existing warnings remain.
  - [ ] UI-verify checked wait mode, unchecked true mode, unchecked false branch-end mode, parallel checked-wait reset behavior, save/load restore, and reset-signal behavior.
- Graph runtime entry alignment feedback pass:
  - [x] Feedback captured on 2026-05-29: process-editor run buttons should use the same graph-parallel runtime as the main-window run buttons, so debugging reproduces production run behavior.
  - [x] `IsHandRun` now keeps only manual-run context for hard-trigger camera and process-trigger nodes; it no longer forces `RunConnectedNodes` into sequential graph mode.
  - [x] Partial graph debug parameters such as skipped node and stop-before node now also use the graph-parallel runtime when the graph is parallel-safe.
  - [x] Build-verified after entry-alignment change with MSBuild on 2026-05-29, 0 errors; existing warnings remain.
  - [ ] Runtime-verify process-editor single run and main-window single run produce comparable graph-parallel timing on `中心端子工位`.
- CompositeModule configurable port feedback pass:
  - [x] New request captured on 2026-05-30: improve `CompositeModule` so large canvas regions can be wrapped as reusable modules with configurable input and output ports.
  - [x] Add source-flow input port tooling so internal module nodes can subscribe to named input variables.
  - [x] Add source-flow output port tooling so internal results can be published as named module outputs.
  - [x] Let the outer `CompositeModule` bind each input port to a constant or upstream subscription.
  - [x] Publish configured output ports as dynamic variables so downstream external nodes can subscribe to them.
  - [x] Build-verify after the first configurable-port implementation pass.
    - Passed with MSBuild on 2026-05-30, 0 errors; existing warnings remain.
- Phase 11 undo/redo first implementation pass:
  - [x] `ProcessFlowCanvas` now owns a canvas edit history stack.
  - [x] Undo/redo supports node creation, node deletion, node movement, connection creation, connection deletion, start-node changes, and node enable/disable changes.
  - [x] `Ctrl+Z`, `Ctrl+Y`, and `Ctrl+Shift+Z` were temporarily removed after UI feedback; undo/redo is currently available from the canvas context menu.
  - [x] Canvas context menu now includes undo and redo entries.
  - [x] Node deletion can preserve the node object for undo without releasing node resources.
  - [x] Fixed node-move undo redraw leaving a stale visual residue at the pre-undo location.
  - [x] UI verification for the undo/redo pass passed after removing shortcuts and fixing node-move redraw residue.
- Phase 11 copy/paste first implementation pass:
  - [x] Canvas context menu now includes copy node and paste node entries.
  - [x] Copy/paste supports selected single node and selected multiple nodes.
  - [x] Pasted nodes receive a new node ID and a unique copied node name.
  - [x] Pasted nodes clone parameters, notes, active state, size, and canvas placement.
  - [x] Paste is registered as a node creation edit, so existing undo/redo can remove and restore the pasted node.
  - [x] UI verification for the single-node copy/paste pass passed.
- Phase 11 multi-select and multi-node edit first implementation pass:
  - [x] Canvas supports rubber-band selection with a dashed selection rectangle.
  - [x] Nodes intersecting the selection rectangle become selected as a group.
  - [x] Dragging any selected node moves the selected group together.
  - [x] Deleting selected nodes removes them as a grouped undo/redo edit and restores attached connections on undo.
  - [x] Enabling or disabling selected nodes is grouped in undo/redo.
  - [x] Multi-node copy/paste copies internal connections among the selected nodes.
  - [x] Pasted internal connections are remapped from old node IDs to new node IDs.
  - [x] Pasted parameter text references such as `ID.NodeName` are remapped when they refer to nodes copied in the same batch.
  - [x] UI verification for multi-select, group move, group delete, and multi-node copy/paste passed.
- Phase 11 zoom first implementation pass:
  - [x] Canvas supports zoom from 50% to 200%.
  - [x] Right-click canvas menu includes zoom in, zoom out, and reset zoom commands.
  - [x] `Ctrl + mouse wheel` zooms around the mouse pointer.
  - [x] Drawing, hit-testing, rubber-band selection, dragging, connection interaction, and paste location use zoom-aware coordinate conversion.
  - [x] Canvas shows the current zoom percentage.
  - [x] Scrolling after zoom forces a full repaint so the zoom percentage overlay does not leave duplicate residues.
  - [x] UI verification for zoom interactions passed.
- Phase 11 snap-to-grid first implementation pass:
  - [x] Canvas has a right-click menu toggle for snap-to-grid.
  - [x] Dragged nodes snap to the nearest 24px grid point when the toggle is enabled.
  - [x] Toolbox-created nodes use the snap rule when the toggle is enabled.
  - [x] Multi-node paste snaps the pasted group target while preserving relative node placement.
- Phase 13 UI layout polish planning:
  - [x] Use `D:\MyCode\PublicWook\TDVision\TDVision\TDVision\UI\FlowCanvasPanel.cs` as the visual and connection-routing reference.
  - [x] Split the UI polish work into staged deliverables so the current graph editor remains usable after each step.
  - [x] Phase 13.1: Dark canvas surface, compact node styling, and routed arrow connections first code pass.
  - [x] Phase 13.2: Toolbox layout cleanup and left-side navigation density improvements.
  - [x] Phase 13.3: Process tab/header command bar cleanup.
  - [x] Phase 13.4: Optional right-side image/result preview panel.
  - [x] Phase 13.5: Minimap and viewport polish.
- Phase 13.1 canvas visual and connection polish:
  - [x] Canvas background now uses a dark workbench surface.
  - [x] Grid lines use subtle dark minor/major grid styling.
  - [x] Nodes have a cleaner light-card style with shadow, status color, and selected orange accent.
  - [x] `ProcessFlowCanvas` now uses a routed arrow path based on the `TDVision` `PathRouter` approach.
  - [x] Connection hit-testing now uses the same routed path as drawing.
  - [x] `If` branch labels remain visible on routed true/false connections.
  - [x] UI verification accepted before starting Phase 13.2.
- Phase 13.2 toolbox layout first implementation pass:
  - [x] Toolbox controls are now grouped inside a designer-visible `panelToolbox`.
  - [x] Left toolbox width was reduced from the old fixed 394px layout.
  - [x] Toolbox can collapse to a narrow left rail and expand again.
  - [x] Toolbox tree uses a darker, denser visual style.
  - [x] Toolbox icons are generated from category/node type names at runtime.
  - [x] Drag cursor preview uses the generated toolbox icons.
- Phase 13.2 toolbox feedback pass:
  - [x] Replaced the visible expanded toolbox tree with a left category rail plus a selected-category module grid.
  - [x] Toolbox category clicks rebuild the visible module cards for that category only.
  - [x] Module cards keep the existing `DragData` drag source behavior for canvas node creation.
  - [x] The process editor window now opens maximized and exposes minimize, maximize/restore, close, and border resize behavior.
- Phase 13.2 floating toolbox feedback pass:
  - [x] The left toolbox now defaults to a compact category rail only.
  - [x] Clicking a category opens the module grid as a floating flyout over the editor surface.
  - [x] Starting a module drag closes the floating flyout after the drag operation.
- Phase 13.3 process header first implementation pass:
  - [x] Moved add/delete process actions into a compact top action strip instead of the old right-side empty column.
  - [x] Restyled process tabs with a compact dark owner-drawn tab header.
  - [x] Restyled the per-process run/stop/clean/enable row as a compact dark command bar.
- Phase 13.3 process header feedback pass:
  - [x] The add-process button is now a compact blue `+` positioned beside the process tabs.
  - [x] The tab-adjacent action strip now auto-sizes for add/delete process commands so neither action is clipped.
  - [x] The tab-adjacent `+` placement now uses the actual last tab rectangle to avoid header/action misalignment.
  - [x] Per-process command bars now show only single run, loop run, and stop on the active process page.
  - [x] Added first-pass loop run behavior that repeatedly runs the active process until stop/cancellation.
  - [x] Restyled the process tab strip to match the dark reference header with red status dots, stronger selected-tab text, and darker inactive tabs.
  - [x] Removed the visible numeric prefix from owner-drawn process tab text while keeping the underlying tab text unchanged for existing process lookup.
  - [x] Extended the dark tab-adjacent action surface across the empty header area so the default light tab background no longer shows.
  - [x] Replaced low-resolution run/loop/stop resource images with GDI+ vector-drawn command glyphs.
- Phase 13.4 image and viewport preview first pass:
  - [x] `ProcessFlowCanvas` draws a floating image preview panel at the top-right of the canvas.
  - [x] The preview panel now binds to selected-node `OutputImage` data when available, with a latest image-output fallback.
  - [x] The preview header selector now opens a list of available process image outputs for manual selection.
  - [x] The preview header shows the image node title and live RGB values while hovering over the preview image.
  - [x] `ProcessFlowCanvas` draws a minimap at the bottom-right of the canvas.
  - [x] The minimap supports click/drag viewport navigation.
- Phase 13 performance feedback pass:
  - [x] The preview overlay now caches the converted bitmap instead of converting `Mat` to `Bitmap` on every paint/zoom redraw.
  - [x] AI result drawing no longer clones the entire bitmap before drawing, and reuses the text font within each draw pass.
  - [x] Image preview and minimap now render as designer-visible overlay controls above the canvas instead of being painted into the scrollable node layer.
  - [x] Fixed preview/minimap flicker while scrolling by keeping both overlays out of the `AutoScroll` paint surface.
  - [x] Removed high-frequency overlay invalidation during node dragging and rubber-band selection so canvas interactions stay responsive.
- Phase 13.6 canvas drag alignment and stable routing feedback pass:
  - [x] New feedback captured: dragging a node should show dashed alignment guides like the reference images.
  - [x] Dragged nodes now snap to nearby peer node top/center/bottom and left/center/right alignment coordinates.
  - [x] Green dashed guide lines now span the dragged node and matching peer nodes so same-level nodes are easier to align visually.
  - [x] Existing connection routes are cached by their own endpoints, so moving an unrelated node no longer makes other lines jump or reroute.
  - [x] Connections attached to the moved node still reroute live because their own endpoints change.
  - [x] Feedback fix: mouse capture and release-time repaint now clear alignment guides immediately after the user releases the mouse.
  - [x] Feedback fix: equal-distance alignment now prefers node centerlines over edges, so same-size nodes align on anchor/connection centers.
  - [x] Feedback fix: endpoint-aligned connections use a straight route first when no node blocks the direct segment.
  - [x] Build-verified after implementation with MSBuild on 2026-05-27, 0 errors; existing warnings remain.
  - [x] Build-verified after feedback fix with MSBuild on 2026-05-27, 0 errors; existing warnings remain.
- Phase 14 multi-condition logic node planning:
  - [x] New request captured: create a node and parameter UI based on `Node/6-LogicTool/If` that supports freely adding multiple conditions.
  - [x] Condition source selection should choose a property from an upstream node result; in normal cases this means public `INodeResult` output properties.
  - [x] The circled UI area in the provided reference image is planned as an upstream-node/result-property picker.
  - [x] WinForms controls added for this feature must be declared and laid out in the corresponding `.Designer.cs` files so the designer can display them.
- Phase 14 multi-condition logic node first implementation pass:
  - [x] Added `NodeType.MultiCondition` and a new `Node/6-LogicTool/MultiCondition` node family.
  - [x] Added serializable condition rows with match mode, source node id/text, property path, operator, values, and note.
  - [x] Added a designer-visible parameter form with condition rows plus an upstream node/result-property tree.
  - [x] Added reflection-based evaluation for scalar values, nested result properties, and common OK/NG boolean patterns.
  - [x] Connected the new node to graph-native True/False branch routing.
  - [x] Added toolbox/language entries and copy/paste remapping for internal `SourceNodeId` references.
- Phase 14 multi-condition UI feedback fixes:
  - [x] Added a runtime/designer fallback so `NodeParamFormMultiCondition.InitializeOperatorColumn` can recreate `ColumnOperator` when the designer file is regenerated without the column instance.
  - [x] Adjusted the condition grid columns to use fill layout, larger row/header height, and minimum widths so table content is no longer clipped by narrow cells.
- Phase 15 OpenCV caliper/circle/ellipse integration planning:
  - [x] Read and compared the reference algorithms from `D:\MyCode\PrivateMyComputerCoder\Test\_OpenCv颜色2`.
  - [x] Confirmed all three tools share the same core pipeline: grayscale input, caliper sampling, 1D gradient edge search, edge polarity/mode selection, parabolic sub-pixel correction, then geometric fitting.
  - [x] Confirmed `CaliperLineTool` fits line results with `Cv2.FitLine`, `CaliperCircleTool` solves circle least squares with `Cv2.Solve(..., SVD)`, and `FindEllipseTool` fits ellipse results with `Cv2.FitEllipse`.
  - [x] Confirmed the current `ShowImageControl` already supports basic line/circle ROI drawing, but does not yet expose the reference solution's circle-caliper and ellipse-caliper ROI types needed for the new tools.
- Phase 15 OpenCV measurement integration first implementation pass:
  - [x] Added shared caliper measurement algorithm code under `Node\4-Measurement\Common`.
  - [x] Added three new measurement nodes under `Node\4-Measurement`: `CaliperLine`, `CaliperCircle`, and `CaliperEllipse`.
  - [x] Added designer-visible WinForms parameter forms for the three nodes, with image subscription, numeric geometry/caliper settings, run preview, and save support.
  - [x] Registered the three node types in `NodeType`, process node creation, toolbox XML, language JSON files, and project compile items.
  - [x] Exposed scalar result properties such as OK flag, edge point count, fitted geometry, and algorithm time so downstream `INodeResult` property pickers can read them.
- Phase 15 measurement ROI interaction feedback pass:
  - [x] Merged the reference `ShowImageControl` circle-caliper and ellipse-caliper ROI APIs/classes into the current project control while keeping existing display compatibility APIs.
  - [x] Replaced the three measurement parameter-form preview `PictureBox` controls with designer-visible `ShowImageControl` controls.
  - [x] The line, circle, and ellipse measurement forms now create draggable caliper ROI overlays and read the latest ROI geometry back before save/run.
  - [x] Left-side line/circle/ellipse ROI parameter edits now immediately refresh the right-side caliper ROI overlay, including caliper count, width, height, position, radius/angle, and direction changes.
  - [x] Changing caliper count/width/height now preserves the current dragged ROI position instead of rebuilding from stale left-side coordinate text.
  - [x] `ShowImageControl.ImageBitmap` no longer calls `ShowFit()` on every image assignment, so run-preview updates keep the current zoom and pan.
- Phase 15.7 fast rotated template matching feedback pass:
  - [x] Reworked `NodeParamFormMatchTemplate` into a three-tab contour-matching layout: Basic Parameters, Feature Template, and Run Parameters, with bottom Continuous Run / Run / OK commands.
  - [x] Removed the Result Display tab; compact run status now stays on the Basic Parameters page with the image preview.
  - [x] Basic Parameters now focuses on image input, search-region setup, display toggles, and run preview. Search-region geometry is saved with the solution and used to crop matching input for faster search.
  - [x] Template creation/editing now opens a dedicated `TemplateCreateForm` dialog again; the dialog owns template ROI generation, contour preview, and brush-based contour/detail erasing.
  - [x] Template images now save into the solution through `NodeParamMatchTemplate.TemplateImageBytes`; external paths are only a legacy/import fallback.
  - [x] The main Feature Template tab now acts as template management and contour preview, while detailed editing happens in the template dialog.
  - [x] Removed the old standalone `TemplateCreate` form from the project file and added the new designer-visible `TemplateCreateForm`.
  - [x] Added explicit confirm-ROI actions for both the main search region and the template creation ROI. Run/generate now requires confirmation and rejects changed ROI geometry until confirmed again.
  - [x] Reworked template brush erasing to use an accumulated erase mask plus OpenCV inpaint, so erased areas are filtered out of template contours and brush patch borders are not counted as new contours.
  - [x] Adjusted ROI interaction so dynamic ROIs are not shown by default; template creation uses Draw ROI -> Create Template, search regions use Draw ROI -> Confirm ROI, and confirmed ROIs are cleared from the image control.
  - [x] Added source-image refresh/fit before opening template creation so the template dialog does not open with an empty source display.
  - [x] Fixed saved search-region reuse after reopening the template-matching parameter window when whole-image search is disabled.
  - [x] Applied the same no-default-ROI behavior to caliper line, circle, and ellipse parameter forms, with designer-visible Draw ROI and Confirm ROI buttons.
  - [x] Hardened embedded-template restore after reopening a solution: PNG byte decode now has an OpenCV fallback, template bitmap replacement is atomic, and contour-preview failures no longer block template restore.
  - [x] Fixed the specific reopened-template restore failure in `FastTemplateMatcher.To24Bpp(...)` by replacing the fragile 24bpp `Bitmap.Clone()` path with a `LockBits` row-copy path plus OpenCV fallback.
- Phase 16 position correction and measurement-following planning:
  - [x] New request captured: add a position-correction/following tool so measurement ROIs can follow a matched product location.
  - [x] First-pass correction data should use X, Y, and angle only.
  - [x] Position correction should subscribe to upstream template-matching location data, store a user-created baseline, and output current offset/angle correction information.
  - [x] Measurement tools should expose designer-visible controls for enabling position correction and subscribing to correction information.
  - [x] When enabled, measurement runtime should transform saved measurement geometry by the subscribed correction before measuring.
  - [x] Implement a new position-correction node/result/parameter form and toolbox/language/project registration.
  - [x] Add correction subscription controls and runtime geometry transform to caliper line, circle, and ellipse tools.
  - [x] Build-verify after first implementation pass.
  - [x] Feedback fix: template-match and caliper parameter-form Refresh Image actions now only reload the current subscribed image result and no longer run upstream flow nodes.
  - [x] Feedback fix: local template-match Run/Continuous Run now publishes MatchX, MatchY, Angle, Score, and display output to the node result so position-correction baseline creation reads the just-previewed match result.
  - [x] Feedback fix: position-correction Create Baseline and local Run now publish the current correction info to the node result so same-image baseline correction remains an identity transform for downstream calipers.
  - [x] Build-verify after the refresh-image/no-upstream-run and baseline-result publishing fixes.
  - [x] Feedback fix: when position correction is enabled, line/circle/ellipse caliper parameter forms now display saved baseline ROI through the current correction for editing, and inverse-transform edited/drawn ROI back to baseline coordinates on confirm/save.
  - [x] This allows adding or editing measurement ROIs on a later corrected image without saving an already-followed ROI and applying the correction a second time.
  - [x] Build-verify after the corrected-image ROI baseline-normalization fix.
  - [ ] Runtime-verify template match -> position correction -> measurement follow on real images.
- Phase 17 parallel branch runtime scheduling design:
  - [x] New request captured: graph branches such as `A -> B` and `A -> C` should be able to run `B` and `C` in parallel instead of sequentially when dependencies allow it.
  - [x] Current behavior confirmed: the graph runtime uses a pending queue and awaits each node one by one, so sibling branches both run but do not run concurrently.
  - [x] Chosen strategy: dependency-aware parallel scheduling, keeping graph order deterministic and batching all nodes whose graph dependencies are ready.
  - [x] First implementation pass added a parallel graph scheduler while preserving the old sequential graph runtime as fallback for preview/skip runs and legacy control-flow graphs.
  - [x] Removed the first-pass safe-node allowlist; all node types can run in parallel when their incoming graph dependencies are satisfied.
  - [x] Conditional branch skips now mark non-selected edges inactive so joins wait only for active upstream paths.
  - [x] Join nodes wait for all active incoming branches to complete; if any node in a parallel batch fails, downstream nodes are not started and the process fails after the already-started batch finishes.
  - [x] Parallel graph process runtime now reports dependency-graph critical-path time, so forked branches contribute the slowest branch instead of branch sums or UI/thread scheduling overhead.
  - [x] Parallel branch failures are isolated: independent ready branches continue running, while descendants of the failed branch are skipped and the first real exception is reported after runnable work finishes.
  - [x] Template-match no-hit and caliper no-geometry results are normal NG outcomes with nullable scalar outputs instead of hard process failures.
  - [x] Multi-condition evaluation treats missing/current-run-null source values as null; every normal comparison returns false on null except explicit IsNull/IsNotNull.
  - [x] Parallel scheduler now forces a failed node runtime status when catching branch exceptions, so the canvas keeps the failed node visibly red while other runnable branches continue.
  - [x] Multi-condition evaluation treats a disabled source node as a satisfied condition before null/property comparison.
  - [x] Feedback change: node result-level NG is now tracked separately from runtime failure; `IsOk = false`, `IsAllOk = false`, or an NG `Result`/`AlgorithmResult`/`SummaryResult` shows as NG on the canvas but keeps `RuntimeStatus = Successful` so downstream IF, ROI drawing, saving, and communication nodes can still run.
  - [x] Feedback fix: failed canvas nodes now keep a red border, red title band, red anchor borders, and red status text even when selected.
  - [x] Feedback fix: QR no-code results and FindCircle radius NG results now publish `AlgorithmResult.IsAllOk = false` so the generic result-status pass can mark those nodes failed on the canvas.
  - [x] Feedback fix: scheduler now treats a node's final `RuntimeStatus = Failed` as a dependency failure, so downstream nodes and joins do not run through failed upstream paths even when another incoming branch is disabled or completed.
  - [x] Feedback fix: `MultiCondition` is now a failure-tolerant condition node; failed upstream paths do not block the multi-condition node itself, while ordinary downstream nodes still keep the existing failed-dependency blocking behavior.
  - [x] Build-verify after the first dependency-aware parallel scheduler implementation pass.
  - [x] Build-verify after branch failure isolation and nullable visual NG result handling.
  - [x] Build-verify after failed-node visual status and disabled-source condition handling.
  - [x] Build-verify after the earlier node-result NG canvas-status feedback pass.
  - [x] Build-verify after failed-runtime-status downstream blocking fixes.
  - [x] Build-verify after separating result-level NG from runtime failure.
  - [ ] Runtime-verify with simple fork, join, condition branch, disabled node, failed node, stop/cancel, image subscription, and device-node cases.
- TDAI stability fix:
  - [x] Added a model-readiness guard before `NodeTDAI` reads `param.Yolo8.ModelType`, so a failed startup model load now reports a clear model/config error instead of throwing a null reference.
  - [x] Made `ParamFormTDAI.LoadModel(...)` clear `param.Yolo8` on load failure and log failures without dereferencing missing AI config data.
- Build verification:
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 17 dependency-aware parallel scheduler first implementation pass.
  - [x] `TDJS-Vision.sln` builds successfully after branch failure isolation and nullable visual NG result handling.
  - [x] `TDJS-Vision.sln` builds successfully after failed-node visual status and disabled-source condition handling.
  - [x] `TDJS-Vision.sln` builds successfully after the scheduling, branch, and subscription-stale-result fixes.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 11 undo/redo first implementation pass.
  - [x] `TDJS-Vision.sln` builds successfully after fixing node-move undo redraw residue and removing undo/redo shortcuts.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 11 copy/paste first implementation pass.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 11 multi-select and multi-node copy/paste first implementation pass.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 11 zoom first implementation pass.
  - [x] `TDJS-Vision.sln` builds successfully after fixing zoom percentage overlay residue during scroll.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 11 snap-to-grid first implementation pass.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 13.1 canvas visual and routed-connection pass.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 13.2 toolbox layout first implementation pass.
  - [x] `TDJS-Vision.sln` builds successfully after the toolbox category-rail feedback pass and the Phase 13.3 command-bar first pass.
  - [x] `TDJS-Vision.sln` builds successfully after the floating toolbox, tab-adjacent add button, image preview, and minimap pass.
  - [x] `TDJS-Vision.sln` builds successfully after the per-process run/loop/stop command pass and preview data-binding pass.
  - [x] `TDJS-Vision.sln` builds successfully after the auto-size process action strip, preview selector, preview cache, and AI draw optimization pass.
  - [x] `TDJS-Vision.sln` builds successfully after moving preview/minimap to independent overlay controls and fixing drag/selection overlay refresh cost.
  - [x] `TDJS-Vision.sln` builds successfully after the process-header reference-style pass and vector command button pass.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 14 multi-condition node first implementation pass.
  - [x] `TDJS-Vision.sln` builds successfully after the TDAI startup model-load guard.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 15 measurement caliper/circle/ellipse first implementation pass.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 15 measurement ROI interaction feedback pass.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 15 left-parameter-to-ROI realtime sync fix.
  - [x] `TDJS-Vision.sln` builds successfully after preserving dragged ROI position during caliper parameter edits and removing automatic image recentering.
  - [x] `TDJS-Vision.sln` builds successfully after the integrated template-matching UI, embedded-template persistence, and contour-erasing feedback pass.
  - [x] `TDJS-Vision.sln` builds successfully after removing the Result Display tab, restoring template creation/editing to a dialog, and adding saved search-region matching.
  - [x] `TDJS-Vision.sln` builds successfully after adding ROI confirmation and fixing template brush erasing contour filtering.
  - [x] `TDJS-Vision.sln` builds successfully after the ROI default-visibility and template/source refresh feedback pass.
  - [x] `TDJS-Vision.sln` builds successfully after the embedded-template restore hardening pass.
  - [x] `TDJS-Vision.sln` builds successfully after replacing the reopened-template 24bpp clone path.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 16 refresh-image/no-upstream-run and position-correction result publishing fixes.
  - [x] `TDJS-Vision.sln` builds successfully after the corrected-image caliper ROI baseline-normalization fix.
  - [x] `TDJS-Vision.sln` builds successfully after the node-result NG to failed-canvas-status feedback fixes.
  - [x] `TDJS-Vision.sln` builds successfully after failed-runtime-status downstream blocking fixes.
- Verified subscription regression points:
  - [x] A node with `A -> C` and `B -> C` shows both `A` and `B` as subscription candidates.
  - [x] Deleting a connection removes the disconnected node from subscription candidates.
  - [x] Saving and reopening restores the subscription relationship correctly.
- Current code point:
  - [x] Subscription candidate lookup now walks graph upstream connections.
  - [x] Connection add/delete and node-connection cleanup notify subscription controls to refresh.
  - [x] Multiple incoming paths contribute their upstream nodes to the same subscription candidate list.
  - [x] A disconnected subscription target is cleared when it is no longer upstream.
  - [x] `NodeBase` now exposes runtime status and elapsed-time text for canvas rendering.
  - [x] `ProcessFlowCanvas` refreshes affected node regions when node status changes.
  - [x] Running, success, failed, disabled, selected, and unexecuted states now have distinct canvas styling.
- Next task:
  - [x] Recheck `If = false` graph branch behavior in the UI.
  - [x] Recheck disabled upstream node behavior in the UI: `A -> B -> C`, disable `B`, then `C` should fail instead of reading `B`'s last result.
  - [x] Continue Phase 10 UI regression checks for simple, branch, failed, and stopped graphs.
  - [ ] Defer passive-triggered graph-flow regression check.
  - [x] Begin Phase 11 editor quality features.
  - [x] UI-verify the first undo/redo implementation pass.
  - [x] Continue with copy/paste node support.
  - [x] UI-verify the first copy/paste implementation pass.
  - [x] Continue with multi-node copy/paste and internal connection remapping.
  - [x] UI-verify multi-select, group move/delete, and multi-node copy/paste.
  - [x] Implement cross-process node copy/paste between process tabs.
  - [x] Continue with zoom support.
  - [x] UI-verify zoom interactions.
  - [x] Continue with snap-to-grid support.
  - [ ] UI-verify snap-to-grid interactions.
  - [x] Begin Phase 13 flow editor UI layout polish.
  - [x] Implement Phase 13.1 canvas visual and connection polish.
  - [x] UI-verify Phase 13.1 canvas visual and routed-connection interactions.
  - [x] Implement Phase 13.2 toolbox layout first pass.
  - [ ] UI-verify Phase 13.2 toolbox collapse/expand and drag creation.
  - [x] Implement toolbox category rail and selected-category module grid per latest UI feedback.
  - [x] Implement maximized/resizable process editor window behavior.
  - [x] Implement compact process tab/action header and run command bar first pass.
  - [ ] UI-verify category rail switching, module drag creation, window resize, and compact command bar behavior.
  - [ ] UI-verify reference-style process header visuals and vector run/loop/stop command buttons.
  - [x] Convert selected-category modules to a floating flyout.
  - [x] Move the add-process `+` beside the process tabs.
  - [x] Add first-pass right-side image preview and bottom-right minimap overlays.
  - [x] Auto-size the compact process action strip so add/delete commands are not clipped.
  - [x] Bind the right-side preview overlay to selected-node image output data.
  - [x] Add a preview selector menu for process image outputs.
  - [x] UI-verify preview/minimap stay fixed without flicker during canvas scroll and zoom.
  - [x] UI-verify node dragging, multi-node dragging, and rubber-band selection remain responsive after overlay separation.
  - [ ] UI-verify floating module flyout, tab-adjacent process actions, preview overlay, and minimap navigation.
  - [x] Add Phase 14 multi-condition logic node plan to this task file.
  - [ ] Inspect existing `Node/6-LogicTool/If` node, params, result, save/restore, and graph branch behavior before implementation.
  - [ ] Design the new multi-condition node type, parameter model, result model, and toolbox creation entry.
  - [ ] Implement the multi-condition parameter UI with designer-visible controls.
  - [ ] Implement upstream `INodeResult` property discovery and condition source selection.
  - [ ] Implement runtime evaluation and True/False graph branch output.
  - [ ] Build-verify and UI-verify multi-condition node editing, save/load, and graph execution.

- [x] The flow editor has entered graph-aware subscription migration.
- [x] Subscription candidates should come from graph upstream nodes, not from ordered node list position.
- [x] Recalculate whether a subscribed node is still upstream after a connection is deleted or changed.
- [x] Show upstream subscription candidates from every incoming graph path when a node is reached by multiple connected nodes.
- [x] Keep old solution subscription text pending until restored graph connections can validate it.
- [x] Verify old solution subscriptions after opening a migrated flow and after save/load.

## Current Understanding

### Existing `TDJS-Vision` ProcessNew editor

- `ProcessEditPanel` creates and owns a `Process`.
- Nodes are created by dragging toolbox entries from `FormNewProcessWizard`.
- `CreateNode(NodeType, nodeName)` builds concrete `NodeBase` controls with a `switch`.
- Nodes are displayed as `NodeBase : UserControl` rows with `DockStyle.Top`.
- `Process.Run(...)` executes `Process.Nodes` in list order.
- Existing logic nodes use `NodeReturn.NextIndex` for ordered-list jumps.
- Parameters are edited in each node's own `ParamForm`.
- Save/restore uses `ProcessConfig`, `NodeConfig`, and `INodeParam`.
- `NodeSubscription` now builds its subscription candidate list from graph upstream nodes.

### Reference `TDVision` editor

- `FlowCanvasPanel` is a self-drawn graph canvas.
- A `Flow` contains `Nodes` and `Connections`.
- Nodes carry `Location`, `Size`, selection state, start-node state, parameters, and results.
- Dragging, selection, anchoring, connecting, undo/redo, and canvas drawing live in the canvas layer.
- Graph persistence saves the graph model instead of rebuilding a vertical UI list.
- Runtime execution traverses graph connections from explicit or implicit start nodes.

## Main Design Decisions

- [x] Use **Plan B** as the target direction.
- [x] Keep existing node parameter forms working during the canvas migration.
- [ ] Separate canvas display concerns from existing `NodeBase` runtime behavior as much as practical.
- [x] Persist graph layout and connections in the current solution format.
- [x] Keep old `.Sol` files loadable and give old ordered nodes a default canvas layout.
- [x] Use graph-native `If` branches for new flow-canvas condition flows.
- [x] Do not expose `Else` or `EndIf` as new flow-canvas toolbox nodes.
- [x] Keep old ordered `Else` and `EndIf` nodes loadable for existing solution compatibility.

## Phase 1: Graph Models And Compatibility

### Flow model changes

- [x] Extend process configuration to store graph data.
- [x] Add node canvas layout fields:
  - [x] `X`
  - [x] `Y`
  - [x] `Width`
  - [x] `Height`
  - [x] `IsStartNode`
- [x] Add a process connection model:
  - [x] `ConnectionId`
  - [x] `FromNodeId`
  - [x] `ToNodeId`
  - [x] `FromAnchor`
  - [x] `ToAnchor`
  - [x] `Branch`
- [x] Decide whether connection data lives only in `ProcessConfig` or is also kept in runtime `Process`.
- [ ] Define one node ID rule for graph operations inside a process.
- [ ] Define whether graph cycles are forbidden in the first implementation.

### Backward compatibility

- [x] Load old process configs that do not contain layout data.
- [x] Generate default positions for old nodes.
- [x] Decide whether old ordered nodes should be auto-connected in their old order.
- [ ] Preserve existing node parameters during old-solution migration.
- [ ] Preserve existing node subscriptions during old-solution migration.

## Phase 2: Canvas Foundation

### New canvas components

- [x] Add a new canvas control for `ProcessNew`.
- [ ] Add a canvas node display model or wrapper around the current runtime node.
- [ ] Add a canvas connection display model.
- [ ] Add a path routing helper for connection drawing.
- [x] Add hit-testing helpers for nodes, anchors, and connections.

### Basic rendering

- [x] Enable double-buffered drawing.
- [x] Draw a grid background.
- [x] Draw node bodies.
- [x] Draw node titles with node ID and node name.
- [x] Draw active, disabled, selected, running, success, and failed states.
- [x] Draw input/output anchor points.
- [x] Draw directed connection lines and arrow heads.
- [x] Support logical canvas coordinates separate from screen coordinates.
- [x] Support scrolling or panning for a canvas larger than the visible panel.

## Phase 3: Replace The ProcessNew List Editor UI

- [x] Add the new canvas control to `ProcessEditPanel.Designer.cs`.
- [x] Keep WinForms controls that must appear in the designer inside `.Designer.cs`.
- [x] Keep existing process-level buttons and status controls:
  - [x] Run
  - [x] Stop
  - [x] Clean status
  - [x] Enable switch
  - [x] Process priority
  - [x] Process group
  - [x] Process rename
  - [x] Log output switch
  - [x] Passive trigger switch
- [x] Replace the vertical node display panel with the canvas.
- [x] Remove or isolate `_stack` from UI layout responsibilities.
- [ ] Keep `Process.Nodes` available for runtime node ownership during migration.

## Phase 4: Toolbox Drag Creation

- [x] Keep the existing toolbox drag source in `FormNewProcessWizard`.
- [x] Make the canvas accept the existing `DragData`.
- [x] Create a node at the mouse drop position.
- [x] Reuse existing concrete node creation logic at first.
- [x] Synchronize new nodes into:
  - [x] `Solution.Instance.Nodes`
  - [x] `Process.Nodes`
  - [x] Canvas node collection
- [x] Assign default canvas size for newly created nodes.
- [x] Mark the newly created node as selected.
- [x] Refresh the process node count after creation.

## Phase 5: Core Canvas Editing

### Node interactions

- [x] Single-click select a node.
- [x] Drag one node to a new location.
- [x] Drag multiple selected nodes.
- [x] Rubber-band select nodes.
- [x] Delete selected nodes.
- [x] Remove connections attached to deleted nodes.
- [x] Rename a node from canvas interaction.
- [x] Enable or disable a node from canvas interaction.
- [x] Add or edit node remarks.
- [x] Open existing node parameter forms by double-clicking canvas nodes.

### Connection interactions

- [x] Start a connection drag from a node anchor.
- [x] Finish a connection on a target node anchor.
- [x] Draw the temporary connection during drag.
- [x] Select a connection.
- [x] Delete a connection.
- [x] Reject self-connections.
- [x] Reject duplicate connections.
- [ ] Define whether multiple outgoing connections are allowed for all node types.
- [ ] Define whether multiple incoming connections are allowed for all node types.
- [x] Add start-node selection behavior.

## Phase 6: Existing Parameter Form Compatibility

- [x] Keep double-click behavior connected to each node's current `ParamForm`.
- [x] Keep parameter save behavior based on `ParamForm.Params`.
- [x] Keep restore behavior:
  - [x] Assign saved `NodeParam` to `ParamForm.Params`.
  - [x] Call `SetParam2Form()`.
- [ ] Verify parameter forms for device-backed nodes still restore device choices.
- [ ] Verify image source parameter restore.
- [ ] Verify ROI parameter restore.
- [ ] Verify AI parameter restore.
- [ ] Verify script and logic parameter restore.
- [ ] Verify node removal still releases special resources such as AI handles.

## Phase 7: Subscription Migration

### First compatibility pass

- [x] Replace ordered `NodeSubscription` candidate lookup with graph upstream lookup.
- [x] Make renamed upstream canvas nodes refresh subscription display text.
- [x] Make deleted canvas nodes invalidate removed subscription targets.
- [x] Verify subscriptions restore after save/load.

### Graph-aware subscription pass

- [x] Decide the target subscription rule:
  - [x] Upstream graph nodes only.
  - [ ] Any node in the same process.
  - [ ] Node-type-specific rules.
- [x] Add graph upstream lookup for subscription candidates.
- [x] Include upstream nodes from all incoming graph paths when a node has multiple connected predecessors.
- [x] Refresh and validate subscription sources when connections are added, deleted, or changed.
- [x] Prevent invalid subscriptions from silently surviving graph edits.
- [x] Define how subscription data should be stored if visible node names change.
  - [x] Current pass keeps existing `ID.NodeName` text storage for compatibility and updates visible text on upstream node rename.

## Phase 8: Save And Restore

### Save

- [x] Save node canvas locations.
- [x] Save node sizes if sizes become editable.
- [x] Save start-node state.
- [x] Save graph connections.
- [x] Save existing node params without losing concrete parameter types.
- [ ] Keep current devices, global signals, and detect item persistence intact.

### Restore

- [x] Restore process tabs.
- [x] Create all runtime nodes first.
- [x] Restore node active and selected state.
- [x] Restore node parameter objects.
- [x] Push restored params back into parameter forms with `SetParam2Form()`.
- [x] Restore canvas positions.
- [x] Restore graph connections after all nodes exist.
- [x] Restore start-node state.
- [x] Validate broken connection references during load.

## Phase 9: Runtime Execution Migration

### Graph execution rules

- [x] Add runtime connection ownership to `Process`.
- [x] Define start-node execution rules.
- [x] Decide whether a process can have multiple start nodes.
- [ ] Decide how branches run:
  - [x] Sequentially.
  - [ ] In parallel.
- [x] Decide what a failed node does to downstream nodes.
- [x] Decide cancellation behavior across graph branches.
- [x] Decide whether disabled nodes are skipped or block downstream execution.
- [x] Prevent infinite execution when a graph cycle exists.

### Current transition runtime rules

- [x] Prefer explicit `IsStartNode` nodes.
- [x] Also run implicit source nodes outside explicit start-node components.
- [x] Traverse outgoing connections sequentially in connection creation order.
- [x] Complete each start-node component before moving to the next independent source component.
- [x] Visit each node at most once in one run to avoid graph cycles looping forever.
- [x] Skip disabled nodes and continue through their outgoing connections.
- [x] Stop the current graph run when a node throws or cancellation is requested.
- [x] Accept multiple persisted start nodes at runtime even though the current canvas menu keeps one explicit start node selected.
- [x] Treat new `If` connections from the `Right` anchor as True branches.
- [x] Treat new `If` connections from the `Bottom` anchor as False branches.
- [x] Infer canvas `If` branch semantics from restored `Right` and `Bottom` anchors when older saved data still has `Branch = Default`.
- [x] Persist and restore branch type with each connection.
- [x] Prevent disabled or branch-skipped upstream nodes from serving stale subscription results during the current run.
- [x] Track per-process run ids so subscription reads can distinguish current-run results from prior-run cached results.

### Existing logic compatibility

- [x] Review `If`, `Else`, and `EndIf` nodes.
- [x] Review nodes that return `NodeReturn.NextIndex`.
- [x] Define graph-native `If` branching for True/False connections.
- [x] Retire `Else` and `EndIf` from new graph-mode creation; keep old ordered nodes only for compatibility.
- [x] Define migration behavior for old ordered-list conditional flows.
- [x] Keep manual process run working during migration.
- [x] Keep passive-triggered process behavior working during migration.
- [x] Keep `RunForUpdateImages(...)` behavior available for parameter forms that need upstream images.
- [x] Use legacy `NodeReturn.NextIndex` jumps during the transition when an old logic node asks for an ordered-list jump.
- [x] Use `NodeReturn.NextBranch` when a graph-native `If` node selects its True or False connection set.

## Phase 10: Runtime State On Canvas

- [x] Show node execution state on canvas.
- [x] Show per-node execution time on canvas.
- [x] Refresh only affected node regions where practical.
- [x] Keep process run-time text updated.
- [x] Keep process run/stop button state updated.
- [x] Keep clean-status behavior working for all canvas nodes.

## Phase 11: Editor Quality Features

- [ ] Add undo and redo for graph edits.
  - [x] First code pass supports undo/redo for node creation, node deletion, node movement, connection creation/deletion, start-node changes, and node enable/disable changes.
  - [x] Added canvas context-menu entries for undo/redo.
  - [x] Temporarily removed Ctrl+Z/Ctrl+Y/Ctrl+Shift+Z shortcuts after UI feedback.
  - [x] Added context-menu undo/redo commands in `ProcessEditPanel.Designer.cs`.
  - [x] Node-count display refreshes after undo/redo changes that add or remove nodes.
  - [x] Deleted nodes are detached without releasing resources when the action is undoable.
  - [x] Fixed node-move undo redraw residue by invalidating the canvas after applying the restored location.
  - [x] UI-verify undo/redo edit scenarios.
- [ ] Add copy and paste for nodes.
  - [x] First code pass supports copying and pasting one selected node from the canvas context menu.
  - [x] First multi-node pass supports copying and pasting all selected nodes from the canvas context menu.
  - [x] Pasted nodes copy parameter objects through JSON cloning and then restore their parameter forms with `SetParam2Form()`.
  - [x] Pasted nodes copy node notes, active state, display size, and canvas placement offset from the last right-click location.
  - [x] Paste participates in the existing undo/redo node-create flow.
  - [x] UI-verify single-node copy/paste edit scenarios.
  - [x] UI-verify multi-node copy/paste edit scenarios.
- [ ] Remap node IDs and internal graph references during paste.
  - [x] Single-node paste assigns a fresh node ID through the normal `CreateNode` path.
  - [x] Pasted node names are made unique with a `_复制` suffix.
  - [x] Existing subscription text inside pasted parameters is preserved; it can resolve again after valid upstream graph connections are created.
  - [x] Multi-node paste remaps copied internal connections to the new node IDs.
  - [x] Multi-node paste remaps copied parameter string references when they point to nodes in the copied batch.
  - [x] UI-verify internal connection and copied subscription remapping.
- [ ] Add cross-process copy and paste for nodes.
  - [x] Requirement captured: copy selected nodes in one process and paste them into another process.
  - [x] Move or mirror the copy buffer to an editor-level service/state that survives switching process tabs.
  - [x] Store cloneable node payloads, relative placement, copied internal connections, and source-to-target remap metadata without holding target-process runtime objects.
  - [x] On paste, create nodes through the normal target-process factory path so IDs, names, canvas state, and parameter forms are initialized consistently.
  - [x] Remap copied internal connections and copied-batch parameter references to the new target-process node IDs.
  - [x] Include older public-field and string-array subscription parameters in the remapping pass.
  - [x] Detect external references that still point back to the source process and leave a clear unresolved state or warning for the user.
  - [x] Register the cross-process paste as one grouped undo/redo edit on the target canvas.
  - [x] Refresh process node count, canvas selection, subscription controls, and preview/minimap overlays after paste.
  - [ ] UI-verify cross-process paste with one node, multiple connected nodes, nodes with subscriptions, and pasted undo/redo.
- [ ] Add zoom if needed.
  - [x] First code pass supports right-click zoom in, zoom out, reset zoom, and `Ctrl + mouse wheel` zoom.
  - [x] Zoom keeps node canvas coordinates unchanged and scales only rendering, hit-testing, and scrollable canvas size.
  - [x] Current zoom percentage is displayed on the canvas.
  - [x] UI-verify zoom edit scenarios.
- [ ] Add minimap if needed.
- [ ] Add snap-to-grid if needed.
  - [x] First code pass adds a right-click menu toggle for snap-to-grid.
  - [x] Node drag and toolbox-created nodes snap to the canvas grid when enabled.
  - [x] Multi-node paste snaps the pasted group target while keeping copied node spacing intact.
  - [ ] UI-verify snap-to-grid edit scenarios.
- [ ] Improve connection routing around nodes.
- [x] Add context menus for canvas, nodes, and connections.

## Phase 12: Test And Verification Checklist

### Editing

- [ ] Create an empty process.
- [ ] Drag one node onto the canvas.
- [ ] Drag multiple nodes onto the canvas.
- [ ] Move nodes and save/load them.
- [ ] Connect nodes and save/load connections.
- [ ] Delete a node and confirm attached connections are removed.
- [ ] Delete a connection without deleting its nodes.
- [ ] Select and deselect nodes and connections.
- [ ] Double-click nodes and open current parameter forms.
- [ ] Undo/redo node creation.
- [ ] Undo/redo node movement.
- [ ] Undo/redo node deletion and attached connection restore.
- [ ] Undo/redo connection creation and deletion.
- [ ] Undo/redo start-node changes.
- [ ] Undo/redo node enable/disable changes.
- [x] Copy and paste one node.
- [x] Paste repeatedly and confirm unique IDs/names and offset locations.
- [x] Undo/redo pasted node creation.
- [x] Rubber-band select multiple nodes.
- [x] Drag selected nodes as a group.
- [x] Delete selected nodes as a group and undo/redo the deletion.
- [x] Copy and paste multiple connected nodes.
- [x] Confirm pasted internal connections use the new node IDs.
- [x] Confirm pasted internal subscription references use the new node text.
- [ ] Copy one node from one process tab and paste it into another process tab.
- [ ] Copy multiple connected nodes from one process tab and paste them into another process tab.
- [ ] Confirm cross-process paste remaps copied-batch subscriptions and flags source-process-only references.
- [x] Zoom in and zoom out from the context menu.
- [x] Zoom in and zoom out with `Ctrl + mouse wheel`.
- [x] At non-100% zoom, verify node click, drag, rubber-band selection, connection creation, connection selection, and paste location.
- [ ] Enable snap-to-grid and drag one node.
- [ ] Enable snap-to-grid and drag selected nodes as a group.
- [ ] Enable snap-to-grid and paste copied nodes.
- [ ] Enable snap-to-grid at non-100% zoom and verify drag/drop placement.

### Parameters

- [ ] Change a simple numeric/string parameter and save/load it.
- [ ] Change image source parameters and save/load them.
- [ ] Change camera/device-backed parameters and save/load them.
- [ ] Change ROI parameters and save/load them.
- [x] Restore node subscriptions after save/load.

### Compatibility

- [ ] Open an old `.Sol` file without graph layout fields.
- [ ] Verify old nodes receive default canvas positions.
- [ ] Verify old node params restore correctly.
- [ ] Verify old devices restore before process nodes that depend on them.
- [ ] Save the migrated solution and reopen it.

### Runtime

- [x] Run a simple graph with one start node.
- [x] Run a graph with a branch.
- [x] Recheck `If = false` only follows False connections and does not continue through True/default downstream nodes.
- [x] Run a graph with disabled nodes.
- [x] Recheck disabled upstream node does not provide stale subscription data to downstream nodes.
- [x] Run a graph with a failed node.
- [x] Verify `A -> B` is not blocked by a failing standalone `C` because the `A -> B` component runs first.
- [x] Stop a running graph.
- [ ] Run passive-triggered graph flows. Deferred for now.
- [x] Verify runtime state refresh on canvas after the Phase 10 code pass.

## Phase 13: Flow Editor UI Layout Polish

Reference direction:

- Use the `TDVision` flow canvas as the near-term visual reference.
- Prefer a darker, denser, workbench-style canvas over the current bright engineering-grid look.
- Reuse the `TDVision` routed arrow connection idea instead of continuing with simple direct Bezier links.
- Keep current `ProcessNew` graph behavior, persistence, undo/redo, copy/paste, zoom, and snap-to-grid working while changing the presentation layer.

### Phase 13.1: Canvas Visual And Connection Baseline

- [x] Change the canvas background to a dark workbench surface.
- [x] Draw a subtle dark grid with enough contrast for node alignment.
- [x] Restyle nodes to be cleaner and less bulky while keeping runtime status text visible.
- [x] Port the `TDVision` routed arrow path approach into `ProcessFlowCanvas`.
- [x] Update connection hit-testing to match routed paths.
- [x] Keep branch labels visible on `If` true/false connections.
- [x] Build-verify after the first visual pass.
- [x] UI-verify simple connections, branch connections, selected connection highlighting, and temporary connection dragging.

### Phase 13.2: Toolbox Density And Navigation

- [x] Reduce the current left tree's visual weight.
- [x] Evaluate whether to keep tree categories or switch to a compact icon/category rail similar to the reference.
- [x] Replace the visible tree with a compact category rail and selected-category module grid.
- [x] Convert the selected-category module grid to a floating flyout instead of a fixed layout column.
- [x] Keep existing drag source behavior and `DragData` compatibility.
- [x] Ensure icons and labels remain readable in Chinese.
- [x] Add a left-side hide/show interaction for the toolbox.
- [x] Generate toolbox icons from category/node type names.
- [x] Build-verify after the first toolbox pass.
- [ ] UI-verify toolbox drag creation after layout changes.
- [ ] UI-verify toolbox collapse and expand.

### Phase 13.3: Process Header And Command Bar

- [x] Make process tabs and run/stop/clean/enable commands feel like one editor header.
- [x] Reduce empty space around the command buttons.
- [x] Place the add-process `+` beside the process tab strip.
- [x] Keep existing run status, elapsed time, node count, and enable switch behavior.
- [x] Embed the active process controls as single run, loop run, and stop in the per-process top-right command bar.
- [x] Restyle process tabs with reference-style red status dots and dark selected/inactive tab states.
- [x] Replace low-resolution run/loop/stop images with vector-drawn command glyphs.
- [ ] UI-verify process switching, add button alignment, single run, loop run, and stop.
- [ ] UI-verify top header matches the provided reference closely enough.

### Phase 13.4: Image And Result Preview Area

- [x] Decide whether the right-side preview belongs in `ProcessNew` now or should remain a later integration.
- [x] Add a first-pass floating right-side preview panel inside the canvas.
- [x] Bind the preview panel to selected-node image/result data.
- [x] Add a selector for manually choosing available process image outputs.
- [ ] Keep preview panel optional or collapsible so small screens still prioritize the flow canvas.
- [ ] UI-verify selected-node result display and fallback empty states.

### Phase 13.5: Minimap And Viewport Polish

- [x] Add a minimap inspired by `TDVision.UI.MinimapPanel`.
- [ ] Keep minimap optional from the canvas context menu.
- [x] Ensure minimap works with zoom and scroll viewport navigation in the first pass.
- [ ] Ensure minimap works with selection and snap-to-grid.
- [ ] UI-verify large graphs with many nodes and connections.

## Phase 14: Multi-Condition Logic Node

Reference direction:

- Use the existing `Node/6-LogicTool/If` node as the behavior and branch-output reference.
- Add a new logic node that can evaluate multiple user-defined conditions instead of a single fixed `If` expression.
- The condition editor should let the user pick an upstream node and one of that node result object's public properties, usually from `INodeResult`.
- The UI reference is the provided condition-checking panel: the condition cell should support opening a picker tree that lists upstream nodes and selectable result properties.
- Keep graph-native branch behavior aligned with the current flow-canvas `If`: True should continue through the True branch, False through the False branch.

### Phase 14.1: Requirement And Model Design

- [x] Inspect current `NodeIf`, `NodeParamIf`, `NodeResultIf`, and parameter form implementation.
- [x] Decide the new node name and `NodeType` entry, for example `MultiCondition`, `ConditionCheck`, or another project-appropriate Chinese display name.
- [x] Define a serializable condition parameter model:
  - [x] Condition name.
  - [x] Optional note/comment.
  - [x] Source node ID and display text.
  - [x] Source result property path.
  - [x] Comparison operator.
  - [x] Expected value or expected range.
  - [x] Value type metadata for restore and UI editing.
- [x] Define the whole-node match mode:
  - [x] All conditions must pass.
  - [x] Any condition may pass.
  - [x] Reserve custom expression/grouping for a later pass if needed.
- [x] Define the result model so downstream nodes can read:
  - [x] Overall boolean result.
  - [x] Per-condition pass/fail details.
  - [ ] Optional display text for logs/result tab.

### Phase 14.2: Upstream Result Property Picker

- [x] Reuse graph upstream lookup rules so the picker only lists valid upstream nodes.
- [x] List upstream nodes by `ID.NodeName`, with a `None` option.
- [x] Reflect over each upstream node's `Result` object and list public readable properties from `INodeResult` implementations.
- [x] Support common scalar property types first: `bool`, numeric types, `string`, enum, and nullable versions.
- [x] Decide first-pass behavior for complex properties such as images, collections, and custom result objects.
- [x] Persist source references by stable node ID plus property path, not only by visible text.
- [x] Refresh or invalidate source selections when graph connections change, upstream nodes are deleted, or upstream nodes are renamed.

### Phase 14.3: Parameter UI

- [x] Build the parameter form based on the existing `If` parameter UI style.
- [x] Add designer-visible controls in `.Designer.cs` files, following the project WinForms rule.
- [x] Support add, delete, and edit condition rows.
- [x] Provide a condition source picker opened from the condition cell/link button.
- [x] Provide operator and value editors appropriate to the selected source property type.
- [x] Keep Chinese labels readable and compact.
- [ ] Add a result display tab or area for overall result and per-condition evaluation details.

### Phase 14.4: Runtime Evaluation And Flow Branching

- [x] On run, validate all selected source nodes are upstream and have successful results for the current process run.
- [x] Read the selected `INodeResult` property value by reflection or a small typed accessor helper.
- [x] Evaluate each condition according to its operator and value type.
- [x] Apply the selected match mode to produce the overall boolean result.
- [x] Return `NodeReturn.NextBranch` as True or False so existing graph-native branch routing can execute the correct outgoing connection.
- [x] Log clear failure messages when a source node/result/property is missing or has not run in the current process run.
- [x] Avoid falling back to legacy ordered-list continuation for graph-native False paths.

### Phase 14.5: Save/Restore And Compatibility

- [x] Save and restore all condition rows through the existing node parameter persistence path.
- [x] Keep old `.Sol` files loadable when they do not contain the new node type.
- [x] Ensure renamed upstream nodes keep stable node-ID based references and update visible display text.
- [x] Ensure copied/pasted multi-condition nodes remap internal upstream references when the source nodes are copied in the same batch.
- [x] Ensure deleting graph connections clears or marks now-invalid condition sources.

### Phase 14.6: Verification

- [x] Build-verify after adding the node type and UI.
- [ ] UI-verify adding, deleting, and editing multiple condition rows.
- [ ] UI-verify upstream result property picker with one upstream node and multiple upstream nodes.
- [ ] UI-verify save/load restores condition rows and selected properties.
- [ ] Runtime-verify All mode True and False outcomes.
- [ ] Runtime-verify Any mode True and False outcomes.
  - [ ] Runtime-verify missing/stale upstream results fail clearly instead of reading old results.
  - [ ] Runtime-verify True and False outgoing graph branches from the new node.

## Phase 15: OpenCV Caliper, Circle, And Ellipse Tool Integration

Reference source:

- `D:\MyCode\PrivateMyComputerCoder\Test\_OpenCv颜色2\卡尺工具\CaliperLineTool.cs`
- `D:\MyCode\PrivateMyComputerCoder\Test\_OpenCv颜色2\找圆工具\CaliperCircleTool.cs`
- `D:\MyCode\PrivateMyComputerCoder\Test\_OpenCv颜色2\找椭圆工具\FindEllipseTool.cs`
- UI/ROI reference: `D:\MyCode\PrivateMyComputerCoder\Test\_OpenCv颜色2\ShowImageControl.cs`

### Phase 15.1: Algorithm Understanding

- [x] `CaliperLineTool` samples multiple rectangular calipers along a base line, searches each caliper profile for an edge point, and fits the final line from valid edge points.
- [x] `CaliperCircleTool` samples radial calipers around a base circle or arc, searches edges along each radial profile, and fits the final circle from at least three edge points.
- [x] `FindEllipseTool` samples normal-direction calipers around a rotated base ellipse, searches edges along each profile, and fits the final ellipse from at least five edge points.
- [x] Shared parameters include caliper count, caliper width/height, edge strength threshold, edge polarity, and edge find mode.
- [x] Shared edge logic supports `DarkToLight`, `LightToDark`, and `Both`, plus `First`, `Last`, and `Best` candidate selection.
- [x] Shared precision path uses local gradient peak interpolation for sub-pixel edge coordinates.

### Phase 15.2: Integration Design

- [x] Decide to add three new measurement nodes instead of changing existing `FindLine` and `FindCircle` detection nodes.
- [x] Extract the reusable caliper edge-search code into a project namespace shared by line, circle, and ellipse tools.
- [x] Normalize common enums and result types so all three tools expose consistent output to downstream `INodeResult` consumers.
- [x] Keep algorithm input based on current project `OutputImage`/`Mat` conventions instead of repeatedly converting through `Bitmap` when a `Mat` is already available.
- [x] Define result display objects: fitted geometry, edge points, point count, success flag, message, and annotated output image.

### Phase 15.3: ROI And Parameter UI

- [x] Add missing circle-caliper and ellipse-caliper ROI support to the current `Forms\DispShowImage\ShowImageControl.cs`, using the reference control as the behavior guide.
- [x] Add all WinForms controls for new/changed parameter forms in `.Designer.cs` files so the Visual Studio designer can display them.
- [x] Provide line, circle, and ellipse ROI editing with caliper count, width, height, edge strength, polarity, and find mode controls.
- [x] Preserve save/restore compatibility for algorithm parameters through the existing node parameter persistence path.
- [x] Ensure parameter forms can preview edge points and fitted results on the preview image.

### Phase 15.4: Node Runtime

- [x] Implement or upgrade the line caliper node runtime.
- [x] Implement or upgrade the circle caliper node runtime.
- [x] Implement the ellipse caliper node runtime.
- [x] Register any new node types in toolbox XML, language JSON files, `NodeType`, node creation switches, and project file entries.
- [x] Ensure result properties are discoverable by the new multi-condition node's upstream `INodeResult` picker.
- [x] Add clear failure messages for missing images, insufficient edge points, and failed fitting.

### Phase 15.5: Verification

- [x] Build-verify after adding the shared algorithm layer.
- [x] Build-verify after adding ROI/control support.
- [x] Build-verify after adding each node family.
- [ ] Runtime-verify line, circle, and ellipse tools on synthetic images with known geometry.
- [ ] UI-verify ROI editing, preview rendering, save/load, and graph execution.

### Phase 15.6: Image/Overlay Layer Separation

- [x] Add `OutputImage.DisplayResult` so node output images can remain clean while result geometry/text is rendered by `ShowImageControl`.
- [x] Add `ShowImageControl.SetImage(Bitmap, AlgorithmResult)` and `SetDisplayResult(...)` to draw result rectangles, lines, circles, ellipses, and text as static overlay ROI objects.
- [x] Route `ImageShow` and `FrmSingleImage` through the new image-plus-overlay display path.
- [x] Change caliper line, caliper circle, and caliper ellipse nodes so runtime output keeps the original image clean and sends edge points/fitted geometry/text through `DisplayResult`.
- [x] Change template matching, color discern, binary analysis, find line, find circle, and battery-ear runtime outputs toward clean image plus display overlay.
- [x] Keep parameter-form preview drawing behavior where it is only used for local UI tuning and does not become the node's runtime output image.
- [x] Build-verify after display-layer migration.
- [ ] Runtime-verify full-process display on real solution images after zooming/panning, confirming result shapes/text no longer pollute source pixels.
- [x] Review explicit result-image tools such as `ImageDraw`; keep them as intentional bitmap writers or split them into display overlay mode if needed.
  - [x] 2026-06-06 反馈变更：`ImageDraw` 保留写入结果图像像素的定位，但运行时改为复用 `ShowImageControl` 的 ROI 绘制方法，并且不在该运行绘制入口新增锁。

### Phase 15.7: Fast Rotated Template Matching Tool

- [x] Trace old template creation flow from `WookTwoParamsWindow.button13_Click`: rotated ROI crop, 24bpp template normalization, and template model persistence intent.
- [x] Trace old runtime matching flow through `OpenCvVisionAdapter.MatchTemplate(...)` and the `Fastest_Image_Pattern_Matching` `MatchTool.dll` P/Invoke API.
- [x] Add `FastTemplateMatcher` wrapper for `CreateMatchEngine`, `LearnPatternMem`, and `MatchMem`, including 24bpp conversion, rotated template crop, match box conversion, score filtering, and optional outline extraction.
- [x] Update the current `MatchTemplate` parameter form to expose fast-match parameters: score, max result count, angle tolerance, angle step, max overlap, coarse match, outline display, and match-box display.
- [x] Update `TemplateCreate` to use `ShowImageControl` dynamic rotated rectangles so template ROI creation is visible in the WinForms designer/control layout path and supports rotation.
- [x] Route runtime template-match output through clean `OutputImage.Bitmaps` plus `OutputImage.DisplayResult`, including rotated boxes, outline contours, text, score, angle, and elapsed time.
- [x] Add native runtime dependencies under `Native/`: `MatchTool.dll` and `opencv_world4100.dll`, with project copy-to-output configuration.
- [x] Build-verify after fast template matching migration (`MSBuild`, 0 errors; existing warnings remain).
- [x] Feedback pass: merged template creation into `NodeParamFormMatchTemplate` so template import, rotated-ROI creation, matching preview, and template editing live in one designer-visible form.
- [x] Feedback pass: changed `NodeParamFormMatchTemplate` to a tabbed layout matching the requested contour-match UI structure: Basic Parameters, Feature Template, Run Parameters, and Result Display.
- [x] Feedback pass: template images are now saved into `NodeParamMatchTemplate.TemplateImageBytes` with the solution file; external template paths remain only as legacy/import fallback.
- [x] Feedback pass: template contour preview is shown directly in the parameter form, and brush-based detail erasing edits the template image so simplified templates can reduce learned contour/detail cost.
- [x] Removed the old standalone `TemplateCreate` form from the project after integrating creation into the main template-matching UI.
- [x] Build-verify after integrated template UI and embedded-template persistence (`MSBuild`, 0 errors; existing warnings remain).
- [x] Feedback pass: dynamic ROI overlays are now hidden by default, template creation opens with a refreshed fitted source image, template ROI creation no longer requires a separate confirm step, confirmed search ROIs are cleared, and saved search regions run after reopening without needing a visible ROI.
- [x] Feedback pass: hardened solution reload for embedded templates so GDI+ decode/preview failures do not surface as template restore failure when the template bytes are still recoverable.
- [ ] Runtime-verify on real product images with rotated targets, confirming match center, angle, score, overlap filtering, and outline display are correct.
- [ ] Runtime-verify deployed/release output folder contains native dependencies and can load `MatchTool.dll` without `DllNotFoundException` or OpenCV native dependency errors.

## Recommended Implementation Order

## Phase 18: Composite Geometry Measurement Tools

Reference source:

- `D:\MyCode\PrivateMyComputerCoder\Test\_OpenCv颜色2\线到线夹角工具`
- `D:\MyCode\PrivateMyComputerCoder\Test\_OpenCv颜色2\点到点距离工具`
- `D:\MyCode\PrivateMyComputerCoder\Test\_OpenCv颜色2\点到区域距离工具`

### Phase 18.1: Algorithm And Data Source Design

- [x] Add shared geometry measurement helpers for line-line angle, point-point distance, and point-region min/max distance.
- [x] Add reflection-based result readers so composite tools can subscribe upstream line, point, and region/contour outputs.
- [x] Keep pixel/sub-pixel precision modes from the reference tools.
- [x] Use existing caliper algorithms for local drawn line and point measurement: line calipers for line-line angle, circle calipers for point extraction.

### Phase 18.2: Node And UI Integration

- [x] Add `LineLineAngle`, `PointPointDistance`, and `PointRegionDistance` node families under `Node\4-Measurement`.
- [x] Add designer-visible WinForms parameter forms with Basic Parameters and Run Parameters tabs only.
- [x] Add source mode selection between subscribe and draw.
- [x] Add position-correction controls for draw-mode baseline ROI geometry.
- [x] Register the new nodes in `NodeType`, process node creation, toolbox XML, language JSON files, and project compile items.

### Phase 18.3: Verification

- [x] UTF-8 parse-verify language JSON and toolbox XML after adding the new node entries.
- [x] Static registration sweep confirmed project compile items, toolbox tags, language keys, node icons, and node factory cases.
- [x] Feedback pass: subscribe mode hides Run Parameters for line-line angle, point-point distance, and point-region distance tools.
- [x] Feedback pass: draw-mode line-line and point-point tools now keep independent per-ROI caliper run parameters, with an ROI selector for tuning the active ROI.
- [x] Feedback fix: Confirm ROI now preserves edge threshold, blur size, edge polarity, edge pick mode, and search direction instead of reverting non-geometry run parameters to defaults.
- [x] Build-verify after registration.
  - Passed on 2026-05-25 with MSBuild, 0 errors; existing warnings remain.
- [ ] UI-verify parameter forms can refresh images, draw/confirm ROIs, and run local previews.
- [ ] Runtime-verify subscribed upstream results and draw-mode caliper measurement on real images.

## Phase 19: Configurable ROI And Result Drawing Tool

Reference source:

- `D:\MyCode\PublicWook\TDJS-Vision\Node\7-ResultProcessing\ImageDraw`

### Phase 19.1: Requirement Capture

- [x] New request captured: add a drawing tool that can subscribe to a selected module's image, text, fitted line, or region result and draw it onto/over an output image.
- [x] The tool should support choosing the source module/result item instead of being limited to AI/color detection results.
- [x] Text drawing should support font size and placement: top-left, top-right, bottom-left, and bottom-right.
- [x] Geometry drawing should support at least rectangle line width, and should be extendable to lines and regions/contours.
- [x] Draw items should support user-selectable colors, reusing the current `ColorText`, `ColorLine`, `ColorContour`, and rectangle color conventions where possible.
- [x] Use the existing `ImageDraw` module's subscription and bitmap-drawing behavior as the first reference.
- [x] Preferred first direction: keep the input image pixels clean and emit non-destructive `OutputImage.DisplayResult` overlays for display.
- [ ] Decide whether a later optional mode should also write drawings into image pixels for image export/archiving.

### Phase 19.2: Framework Fit Discussion

- [x] Review current result data surfaces: `OutputImage`, `AlgorithmResult.Texts`, `AlgorithmResult.Lines`, rectangles, circles, ellipses, contours, and measurement result objects.
- [x] Decide the source picker design: one image subscription plus per-item result subscriptions stored on each draw item.
- [x] Decide how to represent manually configured draw items: text item, line item, rectangle item, and region/contour item in the first version.
- [x] Decide the style model for draw items: color, line width, text font size, text corner placement, and margins.
- [x] Decide save/restore model for multiple draw items and per-item style settings.
- [x] Decide the node belongs under `7-ResultProcessing` as a new overlay-only successor beside the old `ImageDraw`.

### Phase 19.3: Planned Implementation Items

- [x] Add a new `ResultOverlayDraw` node family for configurable ROI/result drawing.
- [x] Add designer-visible WinForms controls in `.Designer.cs` for image source, result source, draw-item list, style settings, and preview.
- [x] Support text placement in all four corners with safe margins.
- [x] Support configurable color and line width for rectangles, lines, and regions/contours, and extend the shared display-result style model for other geometry.
- [x] Support drawing subscribed fitted lines and subscribed regions/contours.
- [x] Preserve current `ImageDraw` compatibility unless explicitly replacing it.
- [x] Feedback fix: `ResultOverlayDraw` is failure-tolerant in graph runtime; failed per-item ROI/text sources are skipped, while the input image subscription remains strict.
- [x] Feedback fix: sequential graph runtime now records upstream node exceptions and continues scheduling failure-tolerant nodes such as `ResultOverlayDraw` before surfacing the original failure.
- [x] Feedback fix: `ResultOverlayDraw` draw items now skip missing/invalid subscribed geometry or throwing result-property getters instead of failing the ROI drawing node.
- [x] Feedback pass: `ResultOverlayDraw` supports applying current color, line width, and text size to all draw items at once.
- [x] Feedback pass: subscribed text draw items support a text prefix so scalar values such as caliper length can be displayed with a label.
- [x] Feedback fix: subscribed text draw items can read dynamic named variables such as arithmetic operation outputs shown as `变量.xxx`.
- [x] Build-verify after implementation.
- [x] Build-verify after the `ResultOverlayDraw` failure-tolerant runtime fix.
- [ ] UI-verify the new form inside the running application.

## Phase 20: Edge Point Finding Measurement Tool

### Phase 20.1: Requirement And Scope

- [x] Add a new measurement toolbox node named `FindPoint` / `找点`.
- [x] Keep it as a new node under `Node\4-Measurement` instead of changing existing `LineFind`, `CircleFind`, or caliper tools.
- [x] The node input is one subscribed image.
- [x] The user draws and confirms one or more ROI regions on the image.
- [x] Runtime finds edge points inside the confirmed ROI regions by tracing edge contours along the material edge.
- [x] Runtime output should keep image pixels clean and publish overlay geometry through `OutputImage.DisplayResult`.
- [x] The tool should expose point/contour results so downstream `PointPointDistance`, `PointRegionDistance`, `ResultOverlayDraw`, `MultiCondition`, and future tools can subscribe to them.

### Phase 20.2: Algorithm Design

- [ ] Reuse OpenCV primitives already available in the project: grayscale conversion, optional smoothing, gradient/Canny-style edge extraction, ROI masking, and contour tracing.
- [ ] Treat the ROI as a mask. Edge extraction should ignore pixels outside the confirmed ROI.
- [ ] Provide first-pass run parameters: smoothing kernel, edge threshold or low/high threshold, minimum contour length, maximum point count or sampling step, and contour selection mode.
- [ ] Support edge polarity where practical: dark-to-light, light-to-dark, and both. If the contour-tracing path cannot directly encode polarity, use gradient sign filtering before contour extraction.
- [ ] Return ordered contour point groups from `Cv2.FindContours`/edge-following, plus a flattened point list for simple point consumers.
- [ ] Add optional point simplification/sampling to avoid producing huge point lists on noisy edges.
- [ ] Reject empty ROI, missing image, no edge found, or too-few-point cases with clear messages.

### Phase 20.3: Parameter UI

- [ ] Add designer-visible WinForms controls in `.Designer.cs`.
- [ ] Use the same general structure as recent measurement tools: Basic Parameters and Run Parameters.
- [ ] Basic Parameters: image subscription, Refresh Image, Draw ROI, Confirm ROI, Clear ROI, Run, and preview image.
- [ ] Run Parameters: smoothing, threshold, polarity, contour mode, minimum contour length, point sampling/simplification, and display toggles.
- [ ] If multiple ROIs are supported in the first pass, add an ROI selector so each ROI can keep independent run parameters when needed.
- [ ] Add position-correction controls only if the first implementation uses saved baseline ROI geometry; otherwise reserve this for a follow-up pass after the core tool is stable.

### Phase 20.4: Result Model

- [ ] Add `NodeResultFindPoint` with `IsOk`, `RunTime`, `PointCount`, `ContourCount`, `Points`, `Contours`, selected/best contour, message, and `OutputImage`.
- [ ] Add small result value types when useful, for example `MeasuredPointSet` or `MeasuredContour`, under `Node\4-Measurement\Common`.
- [ ] Extend `MeasurementResultReader` so downstream measurement/overlay tools can read this node as a point set and as a region/contour source.
- [ ] Display found points/contours in `OutputImage.DisplayResult` using existing `ColorContour`/point overlay conventions where possible.

### Phase 20.5: Registration And Integration

- [ ] Add `NodeType.FindPoint`.
- [ ] Add the `Node\4-Measurement\FindPoint` node family: node, parameter, parameter form, result, and any helper classes.
- [ ] Register the node in `ProcessEditPanel.CreateNode`.
- [ ] Register the node in `ToolTreeView.xml` under `测量工具`.
- [ ] Add language keys in `Languages\zh-CN.json` and `Languages\en-US.json`.
- [ ] Add project compile items and icon/color mapping if required by the current toolbox generation path.

### Phase 20.6: Verification

- [ ] Build-verify after first implementation.
- [ ] UI-verify image refresh, draw ROI, confirm ROI, clear ROI, save/load, and preview run.
- [ ] Runtime-verify on a simple synthetic image with known straight/curved edges.
- [ ] Runtime-verify on real product images that edge points are found along the material edge and do not include unrelated texture/noise.
- [ ] Runtime-verify downstream subscription from `PointRegionDistance`, `ResultOverlayDraw`, and `MultiCondition`.

## Phase 21: Geometry Creation And Line Merge Fit Tool

### Phase 21.1: Requirement And Scope

- [x] Add a new toolbox category named `图形创建`.
- [x] Add a new geometry creation node named `LineMergeFit` / `线组合拟合`.
- [x] The node subscribes two upstream line results, especially two `CaliperLine` outputs.
- [x] The node outputs one reusable line segment instead of only calculating a one-off measurement.
- [x] The output line should be consumable by `PointLineDistance`, `LineLineAngle`, `ResultOverlayDraw`, `MultiCondition`, and future graph tools.

### Phase 21.2: Algorithm Design

- [x] Support collinear merge fitting from the two source lines and their available edge points.
- [x] Support parallel centerline fitting for two product edges that should produce one middle reference line.
- [x] Output fit quality values such as source-line angle difference, line spacing, and fit error.
- [x] Keep runtime output image pixels clean and publish line overlays through `OutputImage.DisplayResult`.

### Phase 21.3: Parameter UI

- [x] Add designer-visible WinForms controls in `.Designer.cs`.
- [x] Provide two upstream line subscription selectors.
- [x] Provide fit mode selection between collinear merge and parallel centerline.
- [x] Provide run and save actions with a compact runtime status display.
- [x] Provide a preview image panel for local run verification.

### Phase 21.4: Result Model

- [x] Add `NodeResultLineMergeFit` with standard line properties: `StartX`, `StartY`, `EndX`, `EndY`, `CenterX`, `CenterY`, `Angle`, `Length`, `LineDistance`, `AngleDifference`, `FitError`, `IsOk`, `Result`, and `OutputImage`.
- [x] Ensure `MeasurementResultReader.TryReadLine(...)` can consume the new result through existing `StartX/StartY/EndX/EndY` property names.

### Phase 21.5: Registration And Integration

- [x] Add `NodeType.LineMergeFit`.
- [x] Add the `Node\8-GeometryCreation\LineMergeFit` node family.
- [x] Register the node in `ProcessEditPanel.CreateNode`.
- [x] Register `图形创建` and `线组合拟合` in `ToolTreeView.xml`.
- [x] Add language keys in `Languages\zh-CN.json` and `Languages\en-US.json`.
- [x] Add project compile items and toolbox icon mapping.

### Phase 21.6: Verification

- [x] Build-verify after first implementation.
  - Passed on 2026-05-26 with MSBuild, 0 errors; existing warnings remain.
- [x] Static-verify toolbox registration, language keys, node factory switch, project compile items, and standard result property names.
- [ ] Runtime-verify two `CaliperLine` results merge into one line and downstream tools can subscribe to it.

## Phase 22: Logic Tool And Arithmetic Operation Node

### Phase 22.1: Requirement And Scope

- [x] Add a calculation tool module for addition, subtraction, multiplication, and division.
- [x] Place the new arithmetic node under the existing `逻辑工具` toolbox category.
- [x] Add a new node named `ArithmeticOperation` / `四则运算`.
- [x] Support both constant operands and subscribed upstream numeric result operands.
- [x] Support free numeric subscription from any upstream module property exposed through `[DisplayName]`, including measurement, template matching, detection, and calculation results.
- [x] Support internal generated variables so later rows can reuse earlier row results inside the same node.
- [x] Produce a reusable numeric result that downstream nodes can subscribe to.
- [x] Keep the first implementation deterministic and lightweight for fast runtime execution.

### Phase 22.2: Data And Operation Design

- [x] Use a row-based calculation model instead of a free-form expression parser in the first pass.
- [x] Each row should store `值1`, `运算方式`, `值2`, `输出变量名`, `注释`, and enabled state.
- [x] Evaluation rule: each enabled row independently calculates `值1 运算方式 值2` and writes the result to its configured output variable.
- [x] Supported operators: add, subtract, multiply, divide.
- [x] Division by zero should fail clearly and not return stale previous results.
- [x] Numeric conversion should accept common numeric result types and numeric strings where safe.
- [x] Row output variables should be evaluated in row order and only reference previous rows, not future rows.
- [x] Avoid graph self-subscription for the same calculation node; self-generated values should be handled by the node's internal variable table.
- [x] Add optional result formatting fields such as decimal places if they are low-risk in the first pass.
- [x] Publish named row outputs through dynamic result-variable paths so downstream tools can subscribe to any configured output variable.

### Phase 22.3: Parameter UI

- [x] Add designer-visible WinForms controls in `.Designer.cs`.
- [x] Provide a calculation grid with add, delete, move up, move down, enable/disable, `值1`, `运算`, `值2`, `输出变量名`, and `注释`.
- [x] Provide a selected-row upstream numeric property picker for free subscription.
- [x] Provide a previous-output-variable picker for reusing values generated by earlier rows in the same calculation node.
- [x] Provide local Run and OK buttons, plus a compact preview/result label showing the expression summary and all named output values.
- [x] Keep the UI compact and operational, similar to measurement/config tools rather than a marketing-style page.

### Phase 22.4: Result Model

- [x] Add `NodeResultArithmeticOperation` with `IsOk`, `Value`, `ValueText`, `ExpressionText`, `OperandCount`, `OperationCount`, `DefaultVariableName`, `Message`, `Variables`, and `RunTime`.
- [x] Add `[DisplayName]` attributes so `MultiCondition`, message/logging tools, and other subscribers can pick the result fields.
- [x] Implement `IDynamicResultVariables` so each named row output can be subscribed as `变量.xxx`.
- [x] Preserve enough expression detail for troubleshooting without storing heavy UI-only state in the result.

### Phase 22.5: Registration And Integration

- [x] Add `NodeType.ArithmeticOperation`.
- [x] Add the `Node\6-LogicTool\ArithmeticOperation` node family.
- [x] Register the node in `ProcessEditPanel.CreateNode`.
- [x] Register `四则运算` under `逻辑工具` in `ToolTreeView.xml`.
- [x] Add language keys in `Languages\zh-CN.json` and `Languages\en-US.json`.
- [x] Add project compile items and toolbox icon mapping.

### Phase 22.6: Verification

- [x] Build-verify after first implementation.
- [x] Static-verify toolbox registration, language keys, node factory switch, project compile items, and result display names.
- [x] Build-verify after row-output improvement.
  - Passed on 2026-05-27 with MSBuild, 0 errors; existing warnings remain.
- [x] Static-verify dynamic variable integration in arithmetic output, standard `NodeSubscription`, and `MultiCondition`.
- [ ] Runtime-verify constant-only add/subtract/multiply/divide cases.
- [ ] Runtime-verify subscribed numeric values from measurement nodes and calculation chaining.
- [ ] Runtime-verify downstream subscription to arbitrary named row outputs through `变量.xxx` / `输出变量`.
- [ ] Runtime-verify divide-by-zero and missing/stale upstream result failures.

### Phase 22.8: MultiCondition Mixed Upstream Subscription Null-Value Investigation

- [ ] Reproduce with one `MultiCondition` subscribing:
  - point-to-point distance `Distance`;
  - line-to-line angle `Angle`;
  - arithmetic operation named output variable.
- [ ] Confirm whether the line-to-line angle source node has `HasSuccessfulResultForRun(CurrentRunId) == true` when `MultiCondition` evaluates.
- [ ] Confirm whether `NodeResultLineLineAngle.IsOk == true` and `NodeResultLineLineAngle.Angle.HasValue == true`; if `IsOk == false`, `Angle = null` is expected by the current result model.
- [ ] Confirm whether the arithmetic source node has `HasSuccessfulResultForRun(CurrentRunId) == true` when `MultiCondition` evaluates.
- [ ] Confirm whether the arithmetic condition `PropertyPath` is a dynamic-variable path such as `$variable:变量名`, and whether the variable exists in `NodeResultArithmeticOperation.Variables`.
- [ ] Check whether graph branch isolation or failed/NG upstream handling makes the arithmetic or angle node not produce a current-run successful result before the multi-condition node runs.
- [x] Add targeted runtime diagnostics if needed so each condition can show why the actual value is null: source not run this batch, result object null, dynamic variable missing, or nullable result property empty.
- [x] Add subscription-dependency scheduling so `MultiCondition` waits for configured source nodes that are active in the same graph component.
- [x] Apply the same subscription-dependency scheduling contract to `ResultOverlayDraw`, so runtime overlay text/ROI sources are not skipped because the draw node ran before hidden subscribed measurements.
- [x] Root-cause note: this class of bug is a graph-scheduler design gap, not a display-control issue. Parameter-form preview may read previous successful upstream results, while graph runtime requires current-run successful results; if hidden `NodeSubscription` sources are not declared to the scheduler, runtime views can show fewer text/ROI items than preview.
- [x] Mechanism note: nodes that read subscribed results during `Run(...)` should implement `INodeSubscriptionDependencyProvider`. `Process` then treats returned source node IDs as hidden upstream dependencies when they are active, in the same graph component, and structurally upstream, so the node waits/requeues until those sources finish in the current run.
- [x] Harden `NodeResultArithmeticOperation.TryGetDynamicVariable(...)` with a case-insensitive fallback for old/deserialized variable dictionaries.
- [x] Clarify `MultiCondition` operator semantics: `Contains` is text contains; numeric `Value1`/`Value2` ranges should use `Between`. Add compatibility handling so numeric `Contains`/`NotContains` rows with `Value2` filled are treated as `Between`/`NotBetween`.
- [ ] Audit remaining runtime subscription readers and connect them to `INodeSubscriptionDependencyProvider` where they depend on current-run upstream results.
- [x] Build-verified the diagnostics and subscription-dependency scheduling pass with MSBuild on 2026-05-28, 0 errors; existing warnings remain.

1. [x] Add graph layout and connection models with backward-compatible save/load defaults.
2. [x] Add the `ProcessNew` canvas control and draw nodes from current runtime nodes.
3. [x] Replace the vertical node list in `ProcessEditPanel` with the canvas.
4. [x] Support toolbox drag creation and node dragging.
5. [x] Preserve parameter form open/save/restore behavior.
6. [ ] Add connection editing and start-node state.
7. [x] Persist and restore canvas nodes plus connections.
8. [ ] Migrate subscription rules from ordered nodes toward graph-aware upstream nodes.
9. [ ] Migrate `Process.Run(...)` from ordered execution to connection-driven execution.
10. [ ] Add editor productivity features and broad regression verification.

## First Deliverable

The first usable milestone should include:

- [x] `ProcessEditPanel` shows a free canvas instead of a vertical node list.
- [x] Toolbox drag creates nodes at the drop position.
- [x] Nodes can be selected and moved.
- [x] Existing parameter forms still open from canvas nodes.
- [x] Nodes can be connected and disconnected.
- [x] Node positions and connections survive save/load.
- [x] Old solutions load into an auto-laid-out canvas.

After this milestone, the editor will visibly become a flow-canvas editor while the deeper graph execution migration continues under Plan B.

## 2026-05-30: Camera Callback Triggered Process

- [x] Add a process context-menu switch named `是否为相机回调触发流程` in `ProcessEditPanel` and persist it in `ProcessConfig`.
- [x] Register camera frame callbacks directly to the owning `NodeImageSource.HandleCameraCallbackFrame` method, so callback frames do not need to search for the image source node.
- [x] Keep the current run mode unchanged when `是否为相机回调触发流程` is not checked; callback frames return before image conversion or process execution.
- [x] 2026-06-02 修正：`是否为相机回调触发流程` 菜单成为启用相机回调订阅的唯一入口；未勾选时图像源保存、反序列化或切换相机都会保持节点回调和海康 `GetImageCallBack` 注销，避免影响 `GetOneFrameImage()` 顺序取图。
- [x] For checked callback-triggered processes, cache the callback frame as the current `ImageSource` result, skip executing the image source node itself, and run only from that node toward downstream connections.
- [x] Exclude camera-callback-triggered processes from `FormMain` solution loop active/passive scheduling; the loop only keeps the solution in running state so camera callbacks can trigger those processes.
- [x] Make Hik camera SDK frame callback registration idempotent by unsubscribing before subscribing on camera open.
- [x] Build-verified on 2026-05-30 and again on 2026-05-31 with MSBuild, 0 errors; existing warnings remain.
- [x] 2026-06-06 修正：`NodeImageSource.HandleCameraCallbackFrame` 移除 `SemaphoreSlim` 和回调帧缓存 `lock`，改用 `Interlocked` 原子状态位和原子引用交换，避免相机回调线程等待锁。
- [x] Build-verified the callback no-lock change on 2026-06-06 with MSBuild, 0 errors; existing warnings remain.

## 2026-06-06: ColorDiscern DisplayResult Output Mode

- [x] Change `ColorDiscern` runtime output to the measurement-node style: keep output image pixels clean, publish ROI/text through `OutputImage.DisplayResult`, and keep `OutputImage.Rectangles` only for legacy rectangle consumers.
- [x] Split color matching from preview drawing with `MatchColorResultsAsync(...)`; the node runtime no longer calls the Bitmap/GDI+ preview branch.
- [x] Build `NodetColorResult` as a standard `AlgorithmResult`, remove the shadowing `IsAllOk` field, and merge same-name color detections into one `DetectResults` list.
- [x] Align ColorDiscern input image subscription with measurement nodes by letting `GetInputOutputImage()` surface subscription errors directly.
- [x] Await the ColorDiscern cancellation check in the touched runtime path to avoid leaving a compiler warning in the modified node.
- [x] Build-verified on 2026-06-06 with MSBuild, 0 errors; existing warnings remain.

## 2026-06-06: Parallel Branch Scheduling Stabilization

- [x] Investigated right-peel callback log around `流程4` and confirmed `79.如果` and `85.AI结果绘制` were ready in the same parallel batch after `75.颜色识别` and `76.AI检测`.
- [x] Changed graph parallel scheduling so ready `If` / `MultiCondition` branch-control nodes run as a single-node batch before ordinary ready nodes, allowing branch skips to be applied before display/draw/communication nodes continue.
- [x] Build-verified the branch-control batch scheduling change with MSBuild on 2026-06-06, 0 errors; existing warnings remain.

## 2026-06-06: ColorDiscern Runtime Delay Diagnostics

- [x] Add temporary segmented runtime logs around `NodeColorDiscern.Run(...)` to distinguish scheduler start delay from input acquisition, Mat access, matching, and result construction.
- [x] Add temporary segmented runtime logs inside `MatchColorResultsAsync(...)` to distinguish Modbus color-code reading, current-color detection, all-color detection, and sort checking.
- [x] Build-verified the ColorDiscern diagnostic logging change with MSBuild on 2026-06-06, 0 errors; existing warnings remain.

## 2026-06-06: Parallel Branch Batch Refinement

- [x] Investigated `流程4` logs and solution connections, confirming `85.AI结果绘制` is a sibling branch from `75/76` rather than downstream of `79/101.如果`.
- [x] Refined ready-batch construction so `If` / `MultiCondition` only hold their structural downstream nodes; independent sibling branches remain in the same parallel batch.
- [x] Build-verified the refined branch batching change with MSBuild on 2026-06-06, 0 errors; existing warnings remain.

## 2026-06-07: TDAI DET GPU Inference Serialization

- [x] Add a static DET GPU inference lock in `Yolo8Det` so multiple process flows cannot enter the native GPU DET inference DLL at the same time.
- [x] Keep CPU DET inference unlocked, and apply the GPU lock to GPU model handle destruction to avoid inference/release collisions.

## 2026-06-08: LineLineAngle Point-Set Stability Trial

- [x] Let `LineMergeFit` publish the source point set used for fitting, so downstream line-line measurement can reuse upstream measured points.
- [x] Let `LineLineAngle` read subscribed line point sets when available, fit each line from those points before calculating the included angle, and fall back to the original endpoint line when no point set exists.
- [x] Add average/min/max line distance and distance point count to the `LineLineAngle` result for checking whether the measured point set itself is stable.
- [x] Correct the point-set reader so geometry-generation helper points such as `LineMergeFit` parallel-center source points are not treated as output-line points by downstream line-line measurement.
- [x] Build-verified the point-set stability trial with MSBuild on 2026-06-08, 0 errors; existing warnings remain.

## 2026-06-08: Image Preprocess Node

- [x] Add `ImagePreprocess` under `Node/2-ImagePreprocessing` with `emphasize` edge enhancement, `texture_laws` texture filtering, and median filtering modes.
- [x] Add a WinForms parameter form with image subscription, preview, mode selection, and per-mode parameters; the preview uses the same image display control family as measurement tools.
- [x] Register the node in `NodeType`, node factories/editors, toolbox XML, language files, canvas coloring, and project compile items.
- [x] Build-verified the image preprocess node with MSBuild on 2026-06-08, 0 errors; existing warnings remain.
- [x] 2026-06-09 修正：流程编辑画布增加 `DragOver` 接收状态保持，并放宽工具箱拖拽启动判断，避免新预处理工具卡片拖不到流程编辑区。
- [x] Build-verified the preprocess toolbox drag fix with MSBuild on 2026-06-09, 0 errors; existing warnings remain.

## 2026-06-09: LineLineAngle ROI Display Extension

- [x] Extend `LineLineAngle` display ROI lines to the input image boundary, so the two measured lines visibly meet at the intersection point instead of only showing short fitted segments.
- [x] Add a larger orange intersection marker with crosshair overlay for execution preview and downstream display results.

## 2026-06-10: LineLineAngle Angle Annotation and LineMergeFit Preview

- [x] Add generic arc ROI support to `AlgorithmResult` and `ShowImageControl`, so angle-style overlays can render in both preview and output display paths.
- [x] Let `LineLineAngle` draw a red included-angle arc and `α=角度°` label near the intersection point when the measurement succeeds.
- [x] Fix `LineMergeFit` preview image lookup by falling back from subscribed line-node output images to their nearest upstream image source or preprocessing output.
- [x] Performance correction: keep `LineMergeFit` runtime geometry-only and skip preview image cloning during flow execution; only the parameter-form run preview prepares a preview base image.

## 2026-06-10: Node-Level Runtime Log Switch

- [x] Add per-node `OutputLog` switch, default enabled and compatible with old solution files.
- [x] Add node right-click menu item `是否输出日志` for batch toggling selected nodes.
- [x] Route node execution through `Process.ShowLog && node.OutputLog`, while keeping process start/end logs controlled by the process-level switch.

## 2026-06-10: ROI Missing Result NG Prompt

- [x] Let `ResultOverlayDraw` and `ResultOverlayDraw2` add red `未查到` text when an enabled item cannot read its subscription or has no drawable geometry.
- [x] Keep failure-tolerant ROI drawing visible so downstream image display can still show partial overlays plus explicit NG missing-result prompts.
- [x] Set `CaliperLine` `JudgeOk` from the actual measure success state, so failed line searches feed NG color decisions correctly.

## 2026-06-10: ROI Text Coordinate Mode

- [x] Add a text coordinate mode to `ColorText` and `RoiTextBlock`, supporting image-area corner positioning and display-control corner positioning.
- [x] Add `坐标系` selectors to `ROI结果绘制` and `ROI结果绘制2` text item settings, defaulting old and new items to `图像坐标系`.
- [x] Route normal text, upstream algorithm text, and red `未查到` prompts through the selected coordinate mode.
- [x] Allow line, rectangle, and region items to edit the same prompt position/coordinate/margin settings, so geometry lookup failures can also draw `未查到` in control coordinates.
- [x] Deduplicate identical `未查到/未找到` prompts when upstream AI text and ROI fallback text both report the same missing result, preferring the control-coordinate prompt.

## 2026-06-11: Process Edit Panel Manual Run Canvas Refresh

- [x] Add an immediate runtime-state refresh method to `ProcessFlowCanvas`, flushing pending node status invalidations and repainting overlays.
- [x] Call the canvas runtime refresh from `ProcessEditPanel` manual run status changes and run completion, so `buttonRun` updates node colors without waiting for a click or drag.

## 2026-06-11: TDAI CPU YOLO新版接口替换

- [x] 新增 `YoloOpenVinoCpuSession`，统一封装新版 `td_det.dll` 的 `yolo_init`、`yolo_infer`、`yolo_release` 三函数接口。
- [x] 将 `Yolo8Det`、`Yolo8Seg`、`Yolo8Obb`、`Yolo8Pose` 的 `DeviceType.CPU` 分支切换到新版CPU YOLO接口，GPU分支暂时保持旧实现。
- [x] 保持外层 `IYolo8.Init(...)`、`DetResult`、`SegResult`、`ObbResult`、`PoseResult` 形状不变，减少对 `NodeTDAI` 和业务解析的影响。
- [x] 同步现有输出目录中的 `td_det.dll`、OpenVINO/TBB运行库和许可证文件，确保 `bin\Debug`、`bin\x64\Debug`、`bin\x64\Release` 均可加载新版CPU YOLO运行时。
- [x] 编译验证：MSBuild `Debug|Any CPU` 和 `Debug|x64` 均为0错误，项目既有188个警告仍存在。

## 2026-06-11: 流程图连线逻辑与执行架构说明

- [x] 新增 `流程图连线逻辑与执行架构说明.md`，整理 `ProcessEditPanel`、`ProcessFlowCanvas`、`Process`、`NodeReturn` 之间的连线创建、保存加载、顺序执行、并行执行和 True/False 分支调度关系。
- [x] 在说明文件中加入红色高亮 Mermaid 架构图，方便后续查看流程图连线逻辑与执行架构。

## 2026-06-11: 流程图连线功能详细说明

- [x] 新增 `流程图连线功能详细说明.txt`，单独说明流程中连线功能的设计思路、鼠标触发事件、创建实现、绘制实现、删除撤销、保存加载和执行调度关联。
- [x] 按“只讲连线功能，不牵扯项目其它内容”的要求，重写 `流程图连线功能详细说明.txt` 为独立连线控件说明，去掉具体项目执行层和业务节点内容。

## 2026-06-11: 保存图像节点写入ROI叠加结果

- [x] 修正 `ImageSave` 节点订阅带 `DisplayResult` 的 `OutputImage` 时只保存底图的问题，保存前把线框、轮廓和文本叠加层绘制到后台保存图。
- [x] 复用 `ShowImageControl.DrawDisplayResultToBitmap` 的绘制逻辑，并兼容索引色灰度图转普通 RGB 图后再绘制，保证后台保存和界面显示一致。

## 2026-06-11: 卡尺找线与线组合拟合稳定性增强

- [x] 让卡尺算法在采样剖面时沿卡尺宽度方向做灰度平均，并使用双线性采样，减少金属反光、毛刺和整数取点对边缘位置的影响。
- [x] 给卡尺找线加入基于中位数残差的离群点剔除，避免少量错误边缘点拉偏拟合角度。
- [x] 给线组合拟合加入鲁棒点过滤，共线合并和平行中线模式都会过滤异常边缘点，降低流程17中 `213/214` 对坏点的敏感度。
- [x] 编译验证：MSBuild `TDJS-Vision.sln` 通过，项目既有 warning 仍存在。

## 2026-06-12: AI运行参数阈值同步与ROI空结果提示修正

- [x] 修正 `SolRunParam` 修改 AI 置信度和 NMS 阈值后只写入 `AIInputInfo`，未同步当前 `Yolo8` 模型句柄的问题。
- [x] 让 `textBoxScoreThreshold` 与 `textBoxScoreNMS` 离开焦点时即时回写当前 AI 节点运行阈值，保存时同步写入配置文件和方案。
- [x] 修正 `ROI结果绘制` 与 `ROI结果绘制2` 对 TDAI 节点成功运行但空检出的特殊处理，不再显示 `AI检测：未查到`；TDAI 节点运行失败时仍保留红色缺失提示。

## 2026-06-12: 线组合拟合平行中线端点中点法

- [x] 将 `LineMergeFit` 的平行中线模式改为两条线对应端点取中点，再连接两个中点生成输出线。
- [x] 在取中点前对齐第二条线的方向，避免上游线段起终点反向导致中线交叉。
- [x] 保留平行中线模式的线间距、拟合误差和显示点输出，用于继续对比运行效果。

## 2026-06-13: 循环运行流程日志无效空跑过滤

- [x] 将方案循环运行和流程编辑界面循环按钮的 `isCyclical` 标记传入流程执行链，区分单次运行与循环运行日志策略。
- [x] 流程级开始日志在循环运行中延迟写入，只有本轮需要保留结束日志时再补写开始日志。
- [x] 对循环成功、耗时为 0ms、没有允许输出日志节点成功执行、OK/NG计数无变化、没有业务 NG 的空跑流程过滤开始/结束日志，避免刷爆日志文件。
- [x] 循环成功日志增加启用节点完整运行判定，本轮成功运行节点数未达到当前流程启用节点数时不写流程开始/结束日志。
- [x] 失败、异常、取消和提前结束仍强制写入流程开始/结束日志，不吞关键问题日志。

## 2026-06-13: 卡尺找线角度输出绝对值

- [x] 将 `CaliperLine` 的 `Angle` 结果字段改为输出 `Atan2` 角度的绝对值，避免订阅结果出现正负号。
- [x] 同步修改卡尺找线图像叠加文本中的角度显示，保证界面显示和节点输出一致。

## 2026-06-16: CUDA 12.2 YOLO Demo运行验证

- [x] 识别 `csharp_mini_demo` Debug 输出为依赖本机 .NET 8 的框架依赖运行方式，直接点击会提示安装 `.NET 8.0.0`。
- [x] 将 Release 自包含运行包复制到纯英文目录 `D:\MyCode\PublicWook\YoloCuda122Run`，避开 native DLL 在中文路径下的编码转换失败。
- [x] 验证 `Terminal.engine.encrypted` 与 `Terminal.engine` 当前不可用，改用 `Terminal.onnx` 完成 GPU 初始化和一次推理，推理结果为 `objects=5`。
- [x] 新增 `D:\MyCode\PublicWook\YoloCuda122Run\RunYoloCuda122.cmd`，双击后默认设置 ONNX 模型路径并启动 CUDA 12.2 demo。

## 2026-06-16: CUDA 12.2 YOLO .NET Framework 4.8演示程序

- [x] 在 `D:\MyCode\PublicWook\Yolo算法\GPU版本-cuda12.2（1050TI以上的显卡）\csharp_net48_demo` 新建 `YoloOpenVinoTensorRtNet48Demo` WinForms 工程，目标框架为 .NET Framework 4.8，平台固定 x64。
- [x] 新增 `IYoloInferenceSession` 与 `YoloOpenVinoTensorRtGpuSession`，用 .NET Framework 4.8 兼容写法封装 `yolo_init`、`yolo_infer`、`yolo_release`。
- [x] 将输出目录固定到纯英文路径 `D:\MyCode\PublicWook\YoloCuda122Net48Run`，并在构建后复制 CUDA 12.2 native 运行库、`Terminal.onnx`、`Terminal.jpg` 和许可证文件。
- [x] 新增 WinForms 可视化界面，控件写入 `MainForm.Designer.cs`，支持选择模型、选择图片、设置置信度/NMS、运行一次和连续运行。
- [x] 编译验证：MSBuild `Debug|x64` 通过，0 个警告、0 个错误；冒烟验证使用 `Terminal.onnx` 成功推理，目标数量 5，native 总耗时约 10.44ms。
- [x] 按 `D:\MyCode\PublicWook\Yolo\GPU-1050ti\text\csharp_mini_demo\csharp_mini_demo\Program.cs` 的 `Infer(Mat image)` 逻辑修正 .NET Framework 4.8 版封装，去掉 `Bitmap.LockBits` 与 `EnsureBgr24Bitmap` 路径，改为直接传 `image.Data`、`image.Width`、`image.Height`、`checked((int)image.Step())`。
- [x] 将托管 `OpenCvSharp.dll` 引用切换为 net48 版本，保留原 demo 输出目录中的 native OpenCV/CUDA/TensorRT 运行库；重新编译 `Debug|x64` 通过，Mat 版冒烟验证目标数量 5。
- [x] 清理 `D:\MyCode\PublicWook\Yolo\GPU-1050ti\csharp_net48_demo` 项目显式托管引用，删除未被源码直接使用的 `System.Core` 引用；重新编译 `Debug|x64` 通过，冒烟验证目标数量 5。
- [x] 将 CUDA 12.2 YOLO .NET Framework 4.8 demo 的运行依赖统一复制到 `D:\MyCode\PublicWook\Yolo\GPU-1050ti\csharp_net48_demo\obj\x64\Debug`，并将 `YoloNativeSource` 与 `OpenCvSharpNet48Dll` 改为从该本地目录取文件，不再引用原 `net8.0-windows` 输出目录或 NuGet 目录；重新编译与冒烟推理均通过，目标数量 5。
- [x] 精简 CUDA 12.2 YOLO .NET Framework 4.8 demo 的本地运行 DLL，移出 `api-ms-*.dll`、FFmpeg 视频输入、非 ONNX OpenVINO 前端、NPU 插件、TensorRT lean/dispatch、legacy parser 与 `vccorlib140.dll`；`obj\x64\Debug` 与实际运行目录均保留 37 个 DLL，重新编译和 `Start-Process -Wait --smoke` 验证通过，目标数量 5。
- [x] 在 `obj\x64\Debug` 下建立 `1050tidll` 隔离目录保存 1050Ti/CUDA 12.2 的 37 个 DLL，删除 `obj\x64\Debug` 根目录 DLL，并新增启动器读取显卡信息；非 `NVIDIA GeForce GTX 750` 时自动把托管程序集解析、DLL 搜索路径和 `yolo_openvino_tensorrt.dll` 预加载指向 `1050tidll`。重新编译通过，`obj\x64\Debug` 与实际输出目录均验证根目录 DLL 数量为 0、`1050tidll` 数量为 37，冒烟推理目标数量 5。
- [x] 按 `1050tidll` 模板从 CUDA 11.8/GTX 750 demo 输出目录复制对应 native DLL 到 `obj\x64\Debug\750dll`，CUDA 主库映射为 `cublas64_11.dll`、`cublasLt64_11.dll`、`cudart64_110.dll`，并补入 750 运行链特有的 `nvparsers.dll`、`zlibwapi.dll`；`OpenCvSharp.dll` 保持使用 net48 版本。更新项目构建复制 `750dll` 到实际输出目录，验证输出根目录 DLL 数量为 0、`1050tidll` 为 37、`750dll` 为 31，当前非 GTX 750 机器仍自动选择 `1050tidll` 并推理成功。
- [x] 将 1050Ti 与 750 两套环境完全相同的公共 DLL 移到 `obj\x64\Debug` 和实际输出根目录，公共 DLL 包含 `OpenCvSharp.dll`、`OpenCvSharpExtern.dll`、OpenVINO ONNX 相关 DLL 与 `tbb12.dll`，并从 `1050tidll`、`750dll` 子目录移除重复副本；启动器改为“根目录公共 DLL + 显卡专用子目录”搜索逻辑。重新编译通过，根目录公共 DLL 数量 12，`1050tidll` 数量 25，`750dll` 数量 19，冒烟推理目标数量 5。
- [x] 继续按哈希核对 1050Ti 与 750 两套隔离目录的同名 DLL，将完全一致的 `onnxruntime.dll`、`opencv_world4100.dll` 也提升到 `obj\x64\Debug` 公共根目录；CUDA、cuDNN、TensorRT、VC 运行库与 `yolo_openvino_tensorrt.dll` 因版本不同继续保留在 `1050tidll`、`750dll` 中隔离。
- [x] 为公共根目录 native DLL 增加完整路径预加载逻辑，优先固定加载 `tbb12.dll`、`opencv_world4100.dll`、`onnxruntime.dll`、`openvino.dll`、`openvino_c.dll`，避免外部 PATH 或子目录加载顺序影响公共依赖；重新编译通过，等待式 `--smoke` 验证成功，当前非 GTX 750 机器自动选择 `1050tidll`，目标数量 5。
- [x] 按实验将 `TDJS-Vision\bin\x64\Debug` 中与 `YoloCuda122Net48Run` 根目录同名的 13 个 DLL 覆盖到 YOLO 运行目录并备份原文件；全量替换后 `--smoke` 失败，`yolo_openvino_tensorrt.dll` 加载返回 Win32 错误码 127。随后恢复 YOLO 原版 OpenVINO/tbb 组，保留 TDJS 的 `OpenCvSharp.dll`、`OpenCvSharpExtern.dll` 继续验证，`--smoke` 成功，目标数量 5。
- [x] 对 `TDJS-Vision\bin\x64\Debug` 做第一轮运行目录瘦身：删除旧程序输出 `机器视觉检测软件V2.0.*`、调试符号与文档 XML、Roslyn 多语言资源目录、日志目录、`protected` 临时保护输出、x86 OpenCvSharp native 副本、OpenCV 视频插件、OpenVINO 非 ONNX 前端/NPU 插件，以及未被当前源码引用的 `TensorRtSharp.dll`、`OpenCvSharp.WpfExtensions.dll`、`td_seg.dll`；保留模型、方案、语言 JSON、DockPanel 配置、核心 OpenVINO/ONNX/CPU/GPU 运行库、相机/PLC/脚本依赖和模板匹配 `Native` 目录。目录体积约从 602.18 MB 降到 423.85 MB，节省约 178.32 MB。
- [x] 修复瘦身后 CPU YOLO 加载 `td_det.dll` 出现 `DllNotFoundException`/`0x8007007F` 的运行时问题：确认 `bin\x64\Debug` 中新版 `td_det.dll` 被旧版 OpenVINO/tbb 依赖组混用，已从 `bin\Debug` 同步新版 `td_det.dll`、`onnxruntime.dll`、OpenVINO ONNX/CPU/GPU 运行组、`tbb12.dll`、`vcruntime140.dll`、`vcruntime140_1.dll` 到 `bin\x64\Debug`；使用 `LoadLibrary` 验证 `td_det.dll` 可正常加载。
- [x] 对 `TDJS-Vision\bin\x64\Release` 做同规则运行目录瘦身：删除旧程序输出 `机器视觉检测软件V2.0.*`、调试符号与文档 XML、Roslyn 多语言资源目录、日志目录、`protected` 临时保护输出、`temp_model_*.temp` 临时模型、x86 OpenCvSharp native 副本、OpenCV 视频插件、OpenVINO 非 ONNX 前端/NPU 插件，以及未被当前源码引用的 `TensorRtSharp.dll`、`OpenCvSharp.WpfExtensions.dll`、`td_seg.dll`；随后从 `bin\Debug` 同步新版 CPU YOLO 依赖组到 `bin\x64\Release`，包含 `td_det.dll`、`onnxruntime.dll`、OpenVINO ONNX/CPU/GPU 运行组、`tbb12.dll`、`vcruntime140.dll`、`vcruntime140_1.dll`。Release 目录体积约从 708.61 MB 降到 464.30 MB，节省约 244.31 MB，并用 `LoadLibrary` 验证 `td_det.dll` 可正常加载。
- [x] 将 `D:\MyCode\PublicWook\Yolo\GPU-1050ti\csharp_net48_demo\obj\x64\Debug` 中的 YOLO GPU .NET Framework 4.8 运行 DLL 环境复制到 `TDJS-Vision\bin\x64\Debug\YoloGPUDll`：公共根目录 14 个 DLL，`1050tidll` 子目录 23 个 CUDA 12.2/1050Ti 运行 DLL，`750dll` 子目录 17 个 CUDA 11.8/GTX750 运行 DLL；仅做文件环境准备，未改业务代码，并逐个 SHA256 校验源文件与目标文件一致。

## 2026-06-17: TDJS-Vision YOLO GPU环境切换集成

- [x] 新增 `YoloGpuRuntimeBootstrapper`，在GPU模型初始化前读取 `Win32_VideoController` 显卡名称；精确匹配 `NVIDIA GeForce GTX 750` 时选择 `YoloGPUDll\750dll`，其它显卡默认选择 `YoloGPUDll\1050tidll`。
- [x] 在 `ParamFormTDAI.CreateModelHandle` 创建GPU模型前初始化GPU运行环境，并输出显卡信息、匹配环境和最终DLL目录日志。
- [x] 将新版 `YoloOpenVinoCpuSession` 抽成CPU/GPU双native适配逻辑，CPU继续使用 `td_det.dll`，GPU使用 `yolo_openvino_tensorrt.dll`，推理仍直接传 `Mat.Data`、`Width`、`Height`、`Step()`。
- [x] 将 `Yolo8Det`、`Yolo8Seg`、`Yolo8Obb`、`Yolo8Pose` 的GPU分支从旧 `td_gpu_detect.dll`、`td_obb.dll`、`td_pose.dll` 切换为新版TensorRT GPU session，并保留CPU分支现有逻辑。
- [x] 更新 `TDJS-Vision.csproj`，加入 `System.Management` 引用和 `YoloGpuRuntimeBootstrapper.cs` 编译项；MSBuild `Debug|x64` 编译通过，结果为 187 个既有警告、0 个错误。
- [x] 将 `bin\x64\Debug\YoloGPUDll` 同步到 `bin\x64\Release\YoloGPUDll`，Release 下公共根目录 14 个 DLL，`1050tidll` 23 个 DLL，`750dll` 17 个 DLL。
- [x] MSBuild `Release|x64` 编译通过，结果为 188 个既有警告、0 个错误。

## 2026-06-18: 取消YOLO GPU推理锁

- [x] 按当前GPU DLL推理需求，移除 `Yolo8Det`、`Yolo8Seg`、`Yolo8Obb`、`Yolo8Pose` 中GPU推理和GPU会话释放处的 `lock`，GPU推理不再被模型类型静态锁串行化。
- [x] MSBuild `Debug|x64` 编译通过，结果为 187 个既有警告、0 个错误。

## 2026-06-21: 海康3D相机真实取帧接入

- [x] 将 `Mv3dLpNet.dll` 纳入项目 `dll` 目录并新增工程引用，避免海康3D相机接入依赖开发机绝对安装路径。
- [x] 补实 `CameraHik3D` 的 SDK 初始化、设备枚举、按序列号/IP打开、开始/停止采集、软触发、主动取帧、深度图解析、深度转点云和 SDK 原生点云缓存。
- [x] 保留 `I3DCamera` 对外抽象和 3D 添加窗体既有调用方式，后台采集线程通过 `FrameArrived` 输出 `Camera3DFrameData`，主动取帧优先复用最近有效帧。
- [x] 编译验证：MSBuild `TDJS-Vision.sln` 的 `Debug|Any CPU` 通过，结果为 187 个既有警告、0 个错误；`bin\Debug` 已生成 `Mv3dLpNet.dll`。
- [x] 运行时验证：`Mv3dLpNet` 初始化、设备数量枚举和 `CameraHik3D.FindCamera()` 均通过，当前发现设备 `MV-DP3060-01P / 00DA8028164 / 192.168.123.88`；打开设备与启动测量成功，但当前设备未输出完整帧，封装层与原始 SDK 轮询均返回 `MV3D_LP_E_NODATA`。

## 2026-06-21: 3D图像源节点接入

- [x] 新增 `ImageSource3D` 节点类型、节点工厂注册和工具树入口，工具箱“图像采集”下可添加“3D图像源”节点。
- [x] 新增 `NodeImageSource3D`、`NodeParamImageSource3D`、`NodeResultImageSource3D` 和 `ParamFormImageSource3D`，参数窗体控件全部写入 `.Designer.cs`。
- [x] 3D 图像源节点支持选择方案内 `I3DCamera`、设置图像模式、超时时间、点云缓存、托管点云抽样和自动打开/取流；运行结果输出完整 `Camera3DFrameData` 与深度预览 `OutputImage`。
- [x] 编译验证：MSBuild `TDJS-Vision.sln` 的 `Debug|Any CPU` 通过，0 个错误；当前警告为项目既有异步、未使用变量和接口隐藏类警告，未发现 `ImageSource3D` 相关新增警告。
- [x] 修复拖入“3D图像源”时报“拖入节点失败”的问题：补齐流程画布 `ProcessEditPanel.CreateNode` 中 `ImageSource3D` 的实例化分支，并同步工具箱图标和画布颜色映射。
- [x] 修复后编译验证：MSBuild `TDJS-Vision.sln` 的 `Debug|Any CPU` 通过，结果为 189 个既有警告、0 个错误。
- [x] 修复 `ParamFormImageSource3D` 内容区被 `FormBase` 自定义标题栏遮挡的问题：移除窗体级内边距，将间距放入主布局面板，并恢复第一行“选择3D相机”控件可见。
- [x] 布局修复后编译验证：MSBuild `TDJS-Vision.sln` 的 `Debug|Any CPU` 通过，结果为 189 个既有警告、0 个错误。
- [x] 增加 3D 相机随方案加载自动重连配置，加载相机列表时按配置自动打开并开始采集；手动关闭连接后会保存为不自动重连。
- [x] 方案重置时同步释放 3D 相机资源并清理 3D 相机列表控件缓存，避免重复加载方案后残留旧 3D 设备状态。
- [x] 自动重连修复后编译验证：MSBuild `TDJS-Vision.sln` 的 `Debug|Any CPU` 通过，结果为 189 个既有警告、0 个错误。

## 2026-06-22: 3D图像显示节点接入

- [x] 保留现有 `FrmSingleImage` 作为 2D 图像显示窗口，新增 `FrmSingle3DImage` 和 `PointCloudPreviewControl` 用于显示 3D 帧、点云和深度投影。
- [x] 新增 `ImageShow3D` 节点、参数窗体和运行结果，参数窗体控件全部写入 `.Designer.cs`，节点订阅上游“3D帧数据”并推送到 3D 图像窗口。
- [x] `FrmImageViewer` 增加 4 个可复用的 3D 图像窗口，节点运行时按窗口名自动打开到同一个 DockPanel。
- [x] 工具树、节点类型、节点工厂、流程画布拖拽创建入口、工具箱图标和画布颜色映射已注册 `3D图像显示`。
- [x] 编译验证：MSBuild `TDJS-Vision.sln` 的 `Debug|Any CPU` 通过，结果为 189 个既有警告、0 个错误。

## 2026-06-22: 3D图像显示节点复用普通图像窗口

- [x] 按新反馈调整 `ImageShow3D`：3D 显示只展示深度预览图，不再自动打开独立 `FrmSingle3DImage` 点云窗口。
- [x] `ParamFormImageShow3D` 的窗口选择改为复用现有 `FrmSingleImage` 图像窗口，设计器文本改为“订阅深度图”和“图像窗口”。
- [x] `NodeImageShow` 增加图像刷新事件发布方法，`ImageShow3D` 复用同一套图像窗口刷新逻辑显示 `DepthImage`。
- [x] `FrmImageViewer` 不再预创建旧版 3D 图像窗口实例，旧接口和旧窗口键名仅保留兼容。
- [x] 编译验证：MSBuild `TDJS-Vision.sln` 的 `Debug|Any CPU` 通过，结果为 189 个既有警告、0 个错误。

## 2026-06-22: 3D相机最新帧消费后清空

- [x] `CameraHik3D.GetOneFrameData()` 读取后台缓存帧时改为取出即清空，避免流程重复消费同一帧。
- [x] 主动取帧返回后同步清理相机对象内部原生点云缓存，开始和停止取流时也清空最近帧缓存，避免跨采集周期误用旧帧。
- [x] 编译验证：MSBuild `TDJS-Vision.sln` 的 `Debug|Any CPU` 通过，结果为 189 个既有警告、0 个错误。

## 2026-06-22: 3D深度图伪彩色显示与灰度缓存分离

- [x] `NodeImageSource3D.BuildDepthPreviewImage()` 输出伪彩色深度预览图作为 `SrcImg` 和 `Bitmaps[0]`，保持 3D 图像显示效果接近海康 demo。
- [x] 同时保留归一化后的 8 位灰度深度图作为 `GrayImg`，后续测量模块和 2D 算子可按普通 `ImageSource` 的灰度缓存逻辑直接复用。
- [x] 无效深度像素在伪彩色显示中置黑，避免伪彩色映射把无效深度误显示成有效颜色。

## 2026-06-22: 隐藏3D图像显示节点入口

- [x] 从工具树中隐藏 `3D图像显示` 新建入口，新流程统一使用普通 `图像显示` 订阅 `3D图像源` 的 `深度预览图`。
- [x] 保留 `ImageShow3D` 节点类型、工厂创建和运行逻辑，兼容已经保存了旧 3D 图像显示节点的历史方案。

## 2026-06-24: 延迟执行节点50ms稳定性优化

- [x] `NodeBase` 新增 `Stopwatch` 版 `SetRunResult` 入口，短耗时节点可用高精度计时器写入运行耗时并保持流程批次状态更新。
- [x] `NodeSleepTool` 从 `Task.Delay` 改为 `Stopwatch` 目标时间等待，长等待分段 `Thread.Sleep`，末段短自旋，减少50ms延迟被线程池恢复放大到59ms左右的抖动。
- [x] 验证记录：旧 `Task.Delay(50)` 本机80次测量平均约61.55ms、最大68ms；编译产物中 `NodeSleepTool.ExecutePreciseSleep(50)` 80次测量79次为50ms、最大54ms；MSBuild `TDJS-Vision.sln` 通过，187个既有警告、0个错误。

## 2026-06-27: 回退线程池诊断和保存图片队列改造

- [x] 按指定基线 `f41ad347d9e7aadbc68a2ee11007de53b1ad894a` 恢复流程回调、并行节点调度和保存图片节点相关代码。
- [x] 保留 `Program.cs` 中线程池最小线程数提升到64的启动配置。
- [x] 保留 `LineCoreFront_C1_Seg.cs` 线芯SEG解析文件、项目编译引用以及 `NodeTDAI` 中对应解析调用。
- [x] 编译验证：MSBuild `TDJS-Vision.sln` 的 `Debug|Any CPU` 通过，184个既有警告、0个错误。

## 2026-07-02: 流程图并行分支完成驱动调度

- [x] 将流程图并行调度从整批 `Task.WhenAll` 等待改为完成驱动推进，某个分支节点完成后立即重新计算可运行下游，避免 `ROI结果绘制 -> 图像显示` 被旁路 `延迟执行` 节点阻塞。
- [x] 顺序单节点且没有其它并行任务时仍保持直接执行，减少普通串行流程的额外线程池调度开销。
- [x] 保留原有分支跳过、失败下游禁用、`StopRun`、`StopBranch` 和并行信号等待预登记释放逻辑。
- [x] 编译验证：MSBuild `TDJS-Vision.sln` 的 `Debug|Any CPU` 通过，184个既有警告、0个错误。

## 2026-07-02: 保存图片节点快速入队

- [x] `NodeImageSave` 主流程不再同步执行 `Mat.ToBitmap`、保存前 ROI 标注绘制、目录创建、文件名去重和磁盘保存，只读取订阅引用并投递轻量保存任务到后台队列。
- [x] 保存队列后台线程负责 `Mat.ToBitmap`、`DrawDisplayResultToBitmap`、创建目录、生成最终文件名、压缩保存和释放后台生成的 `Bitmap`，主流程只保留保存规则判断和队列投递。
- [x] 新增 `【性能诊断-保存图像快速入队】`、`【内存诊断-保存图像后台取图】` 分段日志，用于区分主流程入队耗时与后台转换/标注/写盘耗时。
- [x] `ParamFormImageSave` 新增 `GetOutputImageForSave()`，保存节点快速获取原始 `OutputImage` 引用；保留旧 `GetImage()` 兼容其它可能调用。
- [x] 编译验证：MSBuild `TDJS-Vision.sln` 的 `Release|x64` 和 `Debug|x64` 均通过，0个错误；警告为项目既有警告。

## 2026-07-06: 无监督检测节点接入

- [x] 新增 `UnsupervisedDetection` 节点类型、工具箱“检测识别/无监督检测”入口、节点工厂和流程编辑创建分支。
- [x] 新增无监督检测节点、参数窗体、节点参数、节点结果和运行适配器；参数窗体控件全部写入 `.Designer.cs`，只允许选择训练窗口生成到运行目录 `Model` 文件夹下的 `.tdunsup` 模板。
- [x] 扩展无监督模板包解包能力，识别时从模板包读取 `manifest.json` 和 `model.onnx`，按模板 ROI 执行整图或 ROI 推理，并将异常框偏移回原图坐标。
- [x] 扩展 `UnsupervisedAnomalibDetector` 的 `LoadModel` 和 `InferBgr`，复用运行目录 `UnsupervisedDll` 环境，节点运行时缓存模型句柄，删除节点时释放 native 资源。
- [x] 输出 `AlgorithmResult`，OK/NG 判定、异常分数、异常框数量和 NG 矩形写入 `Rects`/`RectsNgMap`，供 ROI 结果绘制节点订阅；同步支持 `If`、`ConditionRun`、`MessageBox`、`ProcessTrigger` 和结果汇总读取无监督 OK/NG。
- [x] 修复 GPU 训练模板在检测节点执行时 `LoadModel` 返回 `-2` 的隔离问题：模板中的训练设备只作为首选推理设备，`GPU/AUTO` 初始化失败时自动执行 CPU 回退加载。
- [x] 无监督检测节点加载模型失败时输出设备、批次、阈值、最小缺陷面积、模型路径、Python 路径和 CPU 回退返回码，便于继续排查 GPU 环境问题。
- [x] 修复无监督检测节点异常框坐标映射：DLL 返回框为模型输入坐标，节点现在按模板输入尺寸缩放到实际原图/ROI 尺寸后再叠加 ROI 偏移，避免 512×256 坐标直接绘制到 1346×1226 原图左上角。
- [x] 修复无监督训练窗体关闭后再次打开可能出现“参数无效”的资源生命周期问题：主窗体每次打开都创建新的训练窗体实例，关闭/Dispose 时统一释放缩略图、预览图和后台任务。
- [x] 优化无监督训练检查页缩略图滚动性能：缩略图卡片改为单控件自绘、启用滚动容器双缓冲、批量添加卡片，减少大量图片时的控件数量和布局抖动。
- [x] 增加无监督训练检查页手动分类操作：选中图片使用蓝色边框显示，可通过“选中设OK”“选中设NG”按钮在 OK/NG 分类之间切换并实时刷新数量和筛选结果。
- [x] 修复无监督训练检查页缩略图只显示一张的问题：批量添加自绘缩略图后立即恢复并触发 `FlowLayoutPanel` 布局，避免 54 张图像控件重叠在同一位置。
- [x] 无监督训练窗体关闭后再次打开时恢复本次软件运行期间最近一次图像路径，并在路径仍存在时自动重新加载，避免重复手动选择路径。

## 2026-07-08: 大模型算法集成

- [x] 新增大模型训练界面入口，`FormMain` 的“大模型训练”菜单每次打开新的 `LargeModelTrainForm`，避免复用已释放的 native/图像资源。
- [x] 参考无监督训练界面复制并改造大模型训练窗体，模板扩展名独立为 `.tdlarge`，训练输出写入运行目录 `Model` 文件夹。
- [x] 新增 `LargeModelDll` 隔离运行环境检查，DINOv2 native 包装延迟加载 `Dinov2AD.dll` 和 `OpenCvBridge.dll`，不在工程中硬引用算法 DLL。
- [x] 新增 DINOv2 `init/train/infer/release` 托管封装，推理阶段直接传递 OpenCvSharp `Mat.CvPtr`，避免通过临时图片文件传递图像。
- [x] 根据 DINOv2 demo、ONNX/engine 导出文档与 DLL `train` 签名收敛大模型训练参数面板：保留模型精度、运行设备、图像阈值、面积阈值和输入宽高；输入宽高作为 ONNX/engine 分辨率参数，必须为 14 的倍数；训练轮数、迭代、BatchSize 不放入界面，避免误导为 native 可调训练超参。
- [x] 新增大模型模板包，按 demo 的模型/特征对应关系把当前使用的 engine/ONNX 与训练生成的 memory bank 一起写入 `.tdlarge`，模板清单使用 `manifest.json` 保存模型、阈值、面积阈值和 ROI 信息。
- [x] 大模型训练服务已支持按所选分辨率选择模型：CPU 使用 `dinov2_<宽>x<高>.onnx`，GPU 优先使用 `dinov2_<宽>x<高>_<fp16/fp32>.engine`，缺少 engine 但存在同尺寸 ONNX 时调用 `LargeModelDll\trtexec.exe` 自动生成 engine 后再训练 bank。
- [x] 修正大模型图像阈值控件范围：DINOv2 `score/image_thresh` 是特征距离阈值，不是 0~1 概率；训练界面和大模型调用节点均改为 4 位小数、最大 1000000，保留 0 表示使用 bank 自动阈值。
- [x] 自由画布节点卡片新增模型运行参数摘要：大模型调用显示阈值、面积和最大框数，无监督检测显示阈值、推理批次、最小面积和最大框数，保存参数后刷新节点显示。
- [x] 修复大模型训练选择 `840x840` 分辨率时缺少 ONNX 的问题：训练服务缺少同尺寸 ONNX 时会优先调用 `LargeModelDll\export_dinov2.py`、`LargeModelDll\dinov2` 和隔离 `third_party\python_native_env` 自动导出固定位置编码 ONNX，再按原逻辑生成 TensorRT engine。
- [x] 已在当前 `bin\x64\Debug\LargeModelDll` 补齐隔离 ONNX 导出环境、DINOv2 权重缓存、`dinov2_840x840.onnx` 和 `dinov2_840x840_fp16.engine`；trtexec 验证输入 `1x3x840x840`、输出 `1x3600x384` 可正常构建并推理。
- [x] 同步 `bin\x64\Release\LargeModelDll` 与 Debug 大模型运行环境，包含 native DLL、TensorRT、DINOv2 自动导出环境、权重缓存及已生成的 448x224、448x448、840x840、1120x1120 ONNX/engine；Release|x64 编译通过。
- [x] 新增“大模型调用”流程节点、参数窗体、节点参数、节点结果和运行适配器，输出 `AlgorithmResult`、OK/NG、异常分数和异常框，供条件判断、结果汇总和 ROI 绘制订阅。
- [x] 排查大模型节点选择新训练模板运行失败：已用最新 `.tdlarge` 执行 engine+bank 解包、native 加载和单图推理冒烟验证；运行参数界面移除 demo 未提供的“推理批次”，失败日志增加模板路径与输入图像摘要，便于定位是否未保存参数、订阅为空或上游图像为空。

## 2026-07-13: 保存图片节点兼容多条件判定结果

- [x] 新增保存图片统一 OK/NG 判定对象、判定解析接口和可插拔结果适配器，默认兼容 `AlgorithmResult` 与多条件节点输出的 `bool` 条件结果。
- [x] `ParamFormImageSave` 的 OK/NG 订阅改为先按 `object` 读取，再由统一解析器转换，避免多条件布尔结果被强制转换成 `AlgorithmResult` 时抛出类型不匹配异常。
- [x] 保存图片参数界面标签在 `.Designer.cs` 中由“订阅AI检测结果”调整为“订阅OK/NG判定结果”，明确支持 AI 与多条件结果。
- [x] `NodeImageSave` 改为消费统一判定结果；AI结果继续按具体 NG 检测项建立子目录，多条件 NG 结果没有检测项分类时保存到通用 `NG` 目录。
- [x] 多条件 True/False 适配复用不可变静态判定对象和只读空分类集合，避免保存节点高频运行时重复创建临时小对象。
- [x] 新增 `ImageSaveMultiConditionSubscription.Tests.ps1` 回归测试，覆盖多条件 True/False、AI检测项分类、不支持类型错误以及保存节点接线约束。
- [x] 验证记录：`ImageSaveMultiConditionSubscription.Tests.ps1`、`DiagnosticLogLevel.Tests.ps1`、`LargeModelIntegration.Tests.ps1`、`UnsupervisedDetectionNode.Tests.ps1` 均通过；MSBuild `Debug|Any CPU` 与 `Debug|x64` 均为 183 个既有警告、0 个错误。
- [x] 全量脚本测试额外发现两个与本次修改文件无关的既有失败：`FormWindowMaximizeBehavior.Tests.ps1` 的主窗体 `AutoSize` 断言、`UnsupervisedTrainForm.Tests.ps1` 的无监督按钮文本断言，本次未扩大范围修改。

## 2026-07-13: 主程序防止重复启动

- [x] 新反馈已记录：客户可能多次点击软件 exe，需要避免同一桌面会话内启动多个独立主程序进程。
- [x] `Program.Main` 新增本地命名 `Mutex` 单实例保护，抢到运行权的首个进程持有互斥锁直到 `Application.Run` 结束。
- [x] 重复启动的第二个进程会在进入线程池配置、启动诊断日志、通信库授权和主窗体创建前拦截，仅弹出“软件已经运行，请勿重复启动！”中文提示后退出。
- [x] 单实例抢锁逻辑抽成 `TryEnterSingleInstance`，兼容上次异常退出导致的废弃互斥锁，下一次启动可自动接管继续运行。
- [x] 正常启动流程抽成 `RunApplication(args)`，保留 `.Sol` 参数打开方案的既有行为。
- [x] 新增 `ProgramSingleInstance.Tests.ps1` 回归检查，覆盖互斥锁名称、抢锁顺序、重复启动提示、废弃互斥锁接管和释放约束。
- [x] 验证记录：`ProgramSingleInstance.Tests.ps1`、`ImageSaveMultiConditionSubscription.Tests.ps1`、`git diff --check` 均通过；MSBuild `Debug|x64` 通过，183 个既有警告、0 个错误。

## 2026-07-13: 主窗体授权程序启动

- [x] `FormMain` 的“授权”菜单点击后从软件运行目录启动 `activate.exe`，不依赖当前工作目录且不等待子进程退出。
- [x] 新增可插拔 `IExternalProgramLauncher` 接口及默认 `ExternalProgramLauncher` 实现，窗体只负责路径、存在性检查和界面反馈。
- [x] `activate.exe` 不存在或启动失败时显示简体中文错误提示，避免异常终止主程序。
- [x] “授权”菜单点击事件在 `FormMain.Designer.cs` 中绑定，控件继续可在 WinForm 设计器中查看。
- [x] 新增 `FormMainAuthorizationMenu.Tests.ps1` 回归测试，覆盖事件绑定、运行目录路径、中文错误提示和启动器实现约束。
- [x] 验证记录：`FormMainAuthorizationMenu.Tests.ps1`、`FormMainUnsupervisedMenu.Tests.ps1` 和 `git diff --check` 均通过；MSBuild `Debug|x64` 通过，183 个既有警告、0 个错误。
## 2026-07-14: 框架代码分布与新增节点开发交接文档

- [x] 基于当前源码梳理 `Program → FormMain → Solution → Process → NodeBase` 的启动、方案、流程和节点运行链路。
- [x] 梳理根目录、`Node`、`Forms`、`Device`、语言资源、第三方依赖、测试和交付文件的职责边界。
- [x] 核实工具箱拖拽、普通流程节点创建、组合模块节点工厂、画布、订阅、方案保存加载、复制粘贴和资源释放链路。
- [x] 对比 `NodeType`、`ToolTreeView.xml`、`NodeFactory` 和 `ProcessEditPanel`：当前共 73 个枚举值、64 个工具箱叶节点、两套各 67 个创建映射，两套映射一致。
- [x] 输出 `docs/TDJS-Vision框架代码分布与新增节点开发交接文档.md`，包含新增节点的文件骨架、两套注册点、Designer 约束、`.csproj`、多语言、序列化兼容、性能、测试门禁和常见故障定位。
- [x] 本任务未修改业务源代码、窗体设计器和测试代码，只新增交接文档、实施计划并追加本任务记录。
- [x] 验证结果：主文档共 821 行、14 个一级章节和 23 项交付检查项；UTF-8 读取无替换字符，16 个关键源码路径全部存在，必需注册点均有覆盖，无占位词、无尾随空白，Markdown 代码围栏成对，`git diff --check` 返回 0。

## 2026-07-14: 输出 Word 版开发交接文档

- [x] 将框架代码分布与新增节点开发交接内容排版为 Word 文档，输出到 `outputs/TDJS-Vision框架代码分布与新增节点开发交接文档.docx`。
- [x] Word 文档包含封面、自动目录、14 个一级章节、核心架构与数据流图、代码分布表、新增节点 14 步流程、故障定位表、开发提交检查表和推荐阅读顺序。
- [x] 文档采用 Letter 页面、1 英寸页边距、统一蓝灰色技术手册样式、中文字体、页码、页眉分隔线、固定表格列宽、重复表头、代码块和检查框。
- [x] 修正 Word 更新域后独立编号列表串号的问题，各开发步骤列表按源文档从 1 开始；项目符号列表保持项目符号语义。
- [x] 为核心架构图补充“TDJS-Vision 核心架构与数据流”替代文本；所有数据表首行均标记为重复表头，封面键值信息表保留非表头语义。
- [x] 使用 Microsoft Word 导出并逐页检查 28 页最终版，确认封面、目录、架构图、表格分页、代码块、编号、检查表和页码无截断、重叠或乱码。
- [x] 结构审计结果：1 个纵向章节，页面与页边距正确；1 张内嵌图片；12 张表格的总宽、缩进、网格列和单元格宽度一致；标题样式为 15 个一级标题和 40 个二级标题；可访问性高风险为 0。
- [x] 本任务未修改业务源代码、窗体设计器和测试代码，只新增 Word 交付文档并追加任务记录。

## 2026-07-15: 统一检测节点画布高度

- [x] 删除无监督检测节点和大模型调用节点底部的阈值、批次、面积及最大框数参数摘要。
- [x] 两类节点继承画布参数摘要的空默认实现，不再触发节点高度提高到 84，恢复为与其他普通节点一致的默认高度。
- [x] 保留画布通用参数摘要绘制与高度计算能力，避免影响其他节点及后续可插拔扩展。
- [x] 更新无监督检测与大模型集成专项检查，覆盖两个节点不再重写画布参数摘要的行为。
- [x] 验证记录：`UnsupervisedDetectionNode.Tests.ps1` 和 `LargeModelIntegration.Tests.ps1` 均通过；MSBuild `Debug|x64` 编译通过，182 个既有警告、0 个错误。

## 2026-07-15: 相机图像源统一回调取图

- [x] 已确认相机图像源统一通过相机帧回调取得图像，不再使用 `GetOneFrameImage()`。
- [x] 已确认软触发先运行图像源上游节点，到达图像源后调用一次 `GrabOne()`，回调唤醒当前节点并由同一 `RunId` 继续下游。
- [x] 已确认硬触发图像源存在上游节点时属于非法配置，流程编辑页与主窗体的单次、循环运行入口均需弹出中文提示并拒绝启动。
- [x] 已确认点击停止后取消回调等待、停止本次运行启动的相机取流并丢弃迟到帧。
- [x] 已确认隐藏 `Off/On` 触发模式界面，运行固定为 `TriggerModel.On`；旧方案中的 `Off` 自动迁移为 `On` 并保留原触发源。
- [x] 已完成设计说明：`docs/superpowers/specs/2026-07-15-camera-image-source-callback-acquisition-design.md`。
- [x] 已完成详细实施计划：`docs/superpowers/plans/2026-07-15-camera-image-source-callback-acquisition.md`。
- [ ] 按测试驱动方式完成代码改造。
  - [x] 已建立 `Tests/CameraImageSourceUnifiedCallback.Tests.ps1` 专项回归契约，覆盖同轮回调等待、软触发顺序、禁用同步取图、旧参数迁移、硬触发校验、菜单清理和重复软触发清理。
  - [x] 已建立 `Tests/CameraFrameAwaiter.Tests.ps1` 行为测试，覆盖单帧交付、迟到帧拒绝和停止令牌取消等待。
  - [x] 已新增 `ICameraFrameAwaiter` 与 `CameraFrameAwaiter`，以一次性任务完成源实现线程安全的单帧交付、流程取消和释放保护；行为测试已通过。
  - [x] `NodeImageSource` 已删除旧回调工作线程、待处理帧和回调二次启动 `Process.Run` 的入口；相机路径改为先登记等待，软触发调用一次 `GrabOne()`，收到回调后在同一轮流程继续下游。
  - [x] 已同步清理只属于旧专用线程架构的诊断测试标记；`CameraFrameAwaiter.Tests.ps1`、`DiagnosticLazyEvaluation.Tests.ps1`、`DiagnosticLogLevel.Tests.ps1` 均通过，阶段性 `Debug|x64` 编译为 180 个既有警告、0 个错误。
  - [x] 图像源参数界面已在 `.Designer.cs` 中删除 `Off/On` 触发模式控件并回收布局行；保存和应用相机参数固定为 `TriggerModel.On`，旧 `Off` 参数加载时自动迁移并保留原触发源。
  - [x] 图像源相机回调订阅已改为只依赖有效相机参数，不再依赖流程菜单开关；专项测试已推进到缺少硬触发配置校验器的下一预期失败点，阶段性编译为 180 个既有警告、0 个错误。
  - [x] 已新增可替换的流程相机配置校验器；新画布按实际上游连线校验，旧流程按节点顺序兼容校验，硬触发图像源存在有效上游节点时生成包含流程名和节点信息的中文错误。
  - [x] 流程编辑页和主窗体的单次、循环入口均在发送启动信号及切换运行状态前校验；底层 `Solution.Run` 同时保留日志型安全校验，避免其他调用入口绕过规则。
  - [x] 单次与循环运行均统一启动相机图像源回调取流并在退出时停止本轮启动的相机；软触发只由运行到 `NodeImageSource` 时调用一次 `GrabOne()`，删除方案层重复软触发逻辑。
  - [x] 已从 `ProcessEditPanel.Designer.cs` 删除“是否为相机回调触发流程”菜单及代码绑定；运行时不再依赖旧兼容标志，旧方案字段仅保留反序列化兼容。
  - [x] 点击停止通过运行取消令牌解除图像源回调等待；运行方法的 `finally` 停止本轮启动的相机，等待器拒绝并释放停止后的迟到帧。
- [x] 完成专项测试、相关回归测试与 `Debug|x64` 编译验证。
  - [x] `CameraFrameAwaiter.Tests.ps1`、`CameraImageSourceUnifiedCallback.Tests.ps1`、`CameraCallbackFastPath.Tests.ps1`、`DiagnosticLazyEvaluation.Tests.ps1`、`DiagnosticLogLevel.Tests.ps1`、`SolutionResourceRelease.Tests.ps1` 均通过。
  - [x] `git diff --check` 通过；MSBuild `Debug|x64` 编译通过，180 个既有警告、0 个错误。

## 2026-07-16: 日志界面高频刷新防卡死

- [x] 定位根因：非运行状态下每条日志分别 `BeginInvoke` 到 UI，高频更新两个列表并重复滚动；运行结束还会一次性排空积压日志，导致 UI 消息队列被日志刷新占满。
- [x] 新增可替换的 `ILogUiBuffer` 和 2000 条默认有界实现，使用线程安全队列保存最新待显示日志，超限仅淘汰最旧的界面记录，不影响日志文件完整写入。
- [x] 在 `LogHelper.Designer.cs` 中新增 100ms WinForms 定时器，所有运行状态统一入队，每个 UI 节拍最多批量处理 100 条。
- [x] 六个日志列表按批次统一暂停和恢复绘制，每批只滚动一次当前列表；“全部”保留 1000 条、分类保留 300 条，超限删除最旧记录而不再整表清空。
- [x] 日志窗口隐藏时不更新列表控件；主动清空显示会同步清空待显示缓冲区，控件释放时解除静态日志事件。
- [x] 完成专项测试、相关日志回归、资源释放测试和 `Debug|x64` 编译验证。
  - [x] `LogUiThrottledRefresh.Tests.ps1` 覆盖容量、顺序、分批、清空、10000 条并发写入、无逐条 UI 投递、设计器定时器和释放解绑约束。
  - [x] `LogLevelSettings.Tests.ps1`、`DiagnosticLogLevel.Tests.ps1`、`DiagnosticLazyEvaluation.Tests.ps1`、`SolutionResourceRelease.Tests.ps1` 均通过。
  - [x] `git diff --check` 通过；MSBuild `Debug|x64` 编译通过，180 个既有警告、0 个错误。
