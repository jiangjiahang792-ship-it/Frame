# 无监督算法集成 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 `D:\MyCode\PublicWook\家有的无监督算法\App_Main_CPU\App_Main_CPU` 中的 Anomalib 无监督训练与推理能力，按 TDJS-Vision 节点框架方式集成到当前项目。

**Architecture:** 推荐新增独立的“无监督检测”节点，不直接改造现有 `AITD` 节点。核心算法封装为服务层，节点层只负责订阅图像、参数保存、运行状态、结果输出，训练界面作为辅助工具或参数窗体入口接入。

**Tech Stack:** C# / .NET Framework 4.8 / WinForms / SunnyUI / OpenCvSharp / P-Invoke `AnomalibLib.dll` / TDJS-Vision 节点框架。

---

## 难度结论

整体难度：中高。

推理节点本身是中等难度，因为目标工程已有 `AnomalibDetector`、ROI 裁剪、模型缓存、结果解析和并发推理逻辑，当前框架也已经有 `NodeBase + INodeParamForm + INodeResult + ToolTreeView.xml` 的成熟挂接方式。

真正拉高难度的是运行时依赖、训练流程和结果生态兼容。`AnomalibRuntime` 包含 `AnomalibLib.dll`、`device.license`、Python 环境、ONNX Runtime GPU、TensorRT、OpenVINO 等大目录，部署和授权需要单独设计；同时当前结果汇总、条件判断、发送AI结果等多处显式识别 `NodeType.AITD`，新增节点后要同步兼容。

建议先做“只推理、可画框、可输出OK/NG”的 MVP，再做训练界面和批量数据集工具。

## 已确认的目标算法入口

- `D:\MyCode\PublicWook\家有的无监督算法\App_Main_CPU\App_Main_CPU\AnomalibDetector.cs`
  封装 `InitForTraining`、`LoadModel`、`TrainModel`、`CancelTrain`、`InferBgr`、`Dispose`。
- `D:\MyCode\PublicWook\家有的无监督算法\App_Main_CPU\App_Main_CPU\ResultParsers\AnomalibResultParser.cs`
  将返回码、异常分数、缺陷框数量解析为最终 OK/NG。
- `D:\MyCode\PublicWook\家有的无监督算法\App_Main_CPU\App_Main_CPU\AnomalyRoiEditor.cs`
  支持矩形 ROI、四点多边形 ROI、裁剪、遮罩填充和 INI 存取。
- `D:\MyCode\PublicWook\家有的无监督算法\App_Main_CPU\App_Main_CPU\AnomalyCcdForm.cs`
  已包含多检测项组、ROI 并发推理、模型缓存、结果绘制、日志和保存图片。
- `D:\MyCode\PublicWook\家有的无监督算法\App_Main_CPU\App_Main_CPU\AnomalyTrainingForm.cs`
  已包含训练参数、ROI 数据集生成、训练调用和 ONNX 路径回写逻辑。
- `D:\MyCode\PublicWook\家有的无监督算法\App_Main_CPU\App_Main_CPU\AnomalibRuntime\AnomalibLib_API_Document.md`
  明确 DLL 只导出 `Init`、`TrainModel`、`CancelTrain`、`Infer`、`Release`。

## 当前框架挂点

- `D:\MyCode\PublicWook\TDJS-Vision\Node\INode.cs`
  新增 `NodeType.AnomalyDetection`。
- `D:\MyCode\PublicWook\TDJS-Vision\Node\NodeFactory.cs`
  注册 `NodeAnomalyDetection`。
- `D:\MyCode\PublicWook\TDJS-Vision\ToolTreeView.xml`
  在“检测识别”下添加 `Text="无监督检测"`，`Tag="AnomalyDetection"`。
- `D:\MyCode\PublicWook\TDJS-Vision\TDJS-Vision.csproj`
  添加新增 `.cs`、`.Designer.cs`、`.resx` 和需要复制的运行时配置项。
- `D:\MyCode\PublicWook\TDJS-Vision\Node\3-Detection\TDAI\NodeResultTDAI.cs`
  可复用 `AlgorithmResult`、`SingleDetectResult`、`ColorRotatedRect`、`ColorText`。
- `D:\MyCode\PublicWook\TDJS-Vision\Node\7-ResultProcessing\ImageDraw\NodeParamFormImageDraw.cs`
  已能通过 `GetValue<AlgorithmResult>()` 读取结果，若订阅控件不过滤节点类型，则改动较小。
- `D:\MyCode\PublicWook\TDJS-Vision\Node\7-ResultProcessing\ResultSummarize\ParamFormSummarize.cs`
  当前只接受 `AITD` 和 `SharedVariable`，需要加入 `AnomalyDetection`。
- `D:\MyCode\PublicWook\TDJS-Vision\Node\6-LogicTool\If\NodeParamFormIf.cs`
  当前只对 `AITD` 读取 `NodeResultTDAI.AlgorithmResult`，需要兼容无监督节点结果。
- `D:\MyCode\PublicWook\TDJS-Vision\Node\6-LogicTool\ConditionRun\NodeParamFormConditionRun.cs`
  当前显式判断 `AITD`，需要兼容无监督节点。

## 推荐文件结构

- Create: `D:\MyCode\PublicWook\TDJS-Vision\Node\3-Detection\AnomalyDetection\AnomalibDetector.cs`
  从目标工程迁移 P/Invoke 封装，并改命名空间为 `TDJS_Vision.Node._3_Detection.AnomalyDetection`。
- Create: `D:\MyCode\PublicWook\TDJS-Vision\Node\3-Detection\AnomalyDetection\IAnomalyDetector.cs`
  定义 `LoadModel`、`InferBgr`、`InitForTraining`、`TrainModel`、`CancelTrain`、`Dispose`，便于替换 DLL 或模拟测试。
- Create: `D:\MyCode\PublicWook\TDJS-Vision\Node\3-Detection\AnomalyDetection\AnomalyRuntimeOptions.cs`
  保存运行根目录、设备、Python 路径、宽高、BatchSize、最大并发数。
- Create: `D:\MyCode\PublicWook\TDJS-Vision\Node\3-Detection\AnomalyDetection\AnomalyModelGroup.cs`
  保存检测项组、模型类型、ONNX 路径、阈值、最小面积、ROI 索引、启用状态。
- Create: `D:\MyCode\PublicWook\TDJS-Vision\Node\3-Detection\AnomalyDetection\AnomalyDetectorCache.cs`
  按模型路径、算法类型、设备、宽高、阈值、最小面积缓存并释放 detector。
- Create: `D:\MyCode\PublicWook\TDJS-Vision\Node\3-Detection\AnomalyDetection\AnomalyRoi.cs`
  保存矩形和多边形 ROI。
- Create: `D:\MyCode\PublicWook\TDJS-Vision\Node\3-Detection\AnomalyDetection\AnomalyRoiEditor.cs`
  迁移 ROI 裁剪与编辑逻辑。窗体控件必须拆到 `.Designer.cs`。
- Create: `D:\MyCode\PublicWook\TDJS-Vision\Node\3-Detection\AnomalyDetection\AnomalyResultParser.cs`
  输出 `SingleDetectResult`、异常分数文本和异常框。
- Create: `D:\MyCode\PublicWook\TDJS-Vision\Node\3-Detection\AnomalyDetection\NodeAnomalyDetection.cs`
  新节点运行入口。
- Create: `D:\MyCode\PublicWook\TDJS-Vision\Node\3-Detection\AnomalyDetection\NodeParamAnomalyDetection.cs`
  节点参数对象，必须可 JSON 序列化。
- Create: `D:\MyCode\PublicWook\TDJS-Vision\Node\3-Detection\AnomalyDetection\NodeResultAnomalyDetection.cs`
  暴露 `AlgorithmResult`、`OutputImage`、异常分数、缺陷框数量、最终 OK/NG。
- Create: `D:\MyCode\PublicWook\TDJS-Vision\Node\3-Detection\AnomalyDetection\NodeParamFormAnomalyDetection.cs`
  参数窗体业务逻辑。
- Create: `D:\MyCode\PublicWook\TDJS-Vision\Node\3-Detection\AnomalyDetection\NodeParamFormAnomalyDetection.Designer.cs`
  参数窗体控件定义，满足 WinForms 设计器可见要求。
- Create: `D:\MyCode\PublicWook\TDJS-Vision\Node\3-Detection\AnomalyDetection\NodeParamFormAnomalyDetection.resx`
  参数窗体资源文件。

## 阶段计划

### Task 1: 运行时与服务层迁移

**Files:**
- Create: `Node\3-Detection\AnomalyDetection\IAnomalyDetector.cs`
- Create: `Node\3-Detection\AnomalyDetection\AnomalibDetector.cs`
- Create: `Node\3-Detection\AnomalyDetection\AnomalyRuntimeOptions.cs`
- Create: `Node\3-Detection\AnomalyDetection\AnomalyDetectorCache.cs`
- Modify: `TDJS-Vision.csproj`

- [ ] 抽出 `IAnomalyDetector`，避免节点直接依赖具体 DLL 封装。
- [ ] 迁移 `AnomalibDetector`，保留 UTF-8 字符串封送、`SetDllDirectory`、`LoadLibraryEx`、PATH 注入和 `PYTHONHOME` 处理。
- [ ] 把默认运行目录定为 `AppDomain.CurrentDomain.BaseDirectory\AnomalibRuntime`，同时允许参数窗体覆盖。
- [ ] 对 `device.license` 做部署检查，日志只输出存在性和路径，不输出授权内容。
- [ ] 建立缓存释放机制，节点删除、方案关闭、参数变化时释放旧 detector。

### Task 2: 无监督检测节点 MVP

**Files:**
- Create: `Node\3-Detection\AnomalyDetection\NodeAnomalyDetection.cs`
- Create: `Node\3-Detection\AnomalyDetection\NodeParamAnomalyDetection.cs`
- Create: `Node\3-Detection\AnomalyDetection\NodeResultAnomalyDetection.cs`
- Create: `Node\3-Detection\AnomalyDetection\AnomalyResultParser.cs`
- Modify: `Node\INode.cs`
- Modify: `Node\NodeFactory.cs`
- Modify: `ToolTreeView.xml`
- Modify: `TDJS-Vision.csproj`

- [ ] 新增 `NodeType.AnomalyDetection`，显示文本用中文“无监督检测”。
- [ ] 节点订阅上游 `OutputImage`，优先使用 `Bitmaps[0]`，无 ROI 时直接报参数错误或提示配置 ROI。
- [ ] 运行时调用 `LoadModel` 和 `InferBgr`，输出 `AlgorithmResult`。
- [ ] `AlgorithmResult.DetectResults` 至少包含检测项名、异常分数、缺陷框数量、OK/NG。
- [ ] `AlgorithmResult.Rects` 输出缺陷框，坐标映射回原图 ROI 位置。
- [ ] `AlgorithmResult.Texts` 输出最终 OK/NG、score、box count、耗时。
- [ ] `NodeResultAnomalyDetection` 同时暴露 `AlgorithmResult` 和 `OutputImage.DisplayResult`，方便绘制节点和叠加绘制节点复用。

### Task 3: 参数窗体与 ROI 配置

**Files:**
- Create: `Node\3-Detection\AnomalyDetection\NodeParamFormAnomalyDetection.cs`
- Create: `Node\3-Detection\AnomalyDetection\NodeParamFormAnomalyDetection.Designer.cs`
- Create: `Node\3-Detection\AnomalyDetection\NodeParamFormAnomalyDetection.resx`
- Create: `Node\3-Detection\AnomalyDetection\AnomalyRoi.cs`
- Create: `Node\3-Detection\AnomalyDetection\AnomalyRoiEditor.cs`
- Modify: `TDJS-Vision.csproj`

- [ ] 参数窗体使用 `NodeSubscription` 订阅图像节点。
- [ ] UI 控件全部放在 `.Designer.cs`，控件 `Text` 使用中文简体。
- [ ] 支持模型路径、算法类型、阈值、最小面积、输入宽高、BatchSize、设备、运行根目录、ROI 索引。
- [ ] 支持“编辑ROI”“加载模型”“测试推理”“保存”按钮。
- [ ] 参数保存到 `NodeParamAnomalyDetection`，跟随方案 JSON 序列化。
- [ ] ROI 信息优先保存在节点参数中，避免依赖旧工程的 INI 路径。

### Task 4: 结果生态兼容

**Files:**
- Modify: `Node\7-ResultProcessing\ResultSummarize\ParamFormSummarize.cs`
- Modify: `Node\6-LogicTool\If\NodeParamFormIf.cs`
- Modify: `Node\6-LogicTool\ConditionRun\NodeParamFormConditionRun.cs`
- Inspect: `Node\5-EquipmentCommunication\AIResultSend\ParamFormSignalSend.cs`
- Inspect: `Node\7-ResultProcessing\ImageDraw\NodeParamFormImageDraw.cs`
- Inspect: `Node\7-ResultProcessing\ResultOverlayDraw\NodeResultOverlayDraw.cs`

- [ ] 对只判断 `NodeType.AITD` 的地方增加 `NodeType.AnomalyDetection`。
- [ ] 优先通过 `GetValue<AlgorithmResult>()` 取值，减少对具体 `NodeResultTDAI` 类型的绑定。
- [ ] 确认 `发送AI结果` 能读取无监督节点输出的检测项名。
- [ ] 确认 `AI结果绘制`、`ROI结果绘制` 能画出无监督缺陷框和文本。
- [ ] 确认 `结果汇总`、`如果`、`条件运行` 能用 `IsAllOk` 判定。

### Task 5: 训练工具集成

**Files:**
- Create: `Forms\AnomalyTraining\AnomalyTrainingForm.cs`
- Create: `Forms\AnomalyTraining\AnomalyTrainingForm.Designer.cs`
- Create: `Forms\AnomalyTraining\AnomalyTrainingForm.resx`
- Create: `Node\3-Detection\AnomalyDetection\AnomalyTrainingService.cs`
- Modify: `TDJS-Vision.csproj`

- [ ] 先把训练能力做成独立工具窗体，不阻塞推理节点 MVP。
- [ ] 迁移 `AnomalyTrainingForm` 时把手工创建控件拆到 `.Designer.cs`。
- [ ] 训练服务复用 `IAnomalyDetector.InitForTraining`、`TrainModel`、`CancelTrain`。
- [ ] 支持 ROI 数据集生成、训练进度、取消训练、训练完成后回写 ONNX 路径。
- [ ] 训练目录、运行目录和授权文件只在本机路径引用，不把 `device.license` 内容写入日志或方案。

### Task 6: 验证与性能

**Files:**
- Create: `Tests\Test-AnomalyDetectionNode.ps1`
- Create: `Tests\Test-AnomalyRuntimeFiles.ps1`
- Modify: `FLOW_CANVAS_B_PLAN_TASKS.md`

- [ ] 增加运行时文件检查脚本，验证 `AnomalibRuntime\AnomalibLib.dll` 和 `device.license` 存在。
- [ ] 使用一张本地图像和一个 ONNX 模型做手动推理验证。
- [ ] 验证模型缓存命中、参数变化释放旧模型、节点删除释放资源。
- [ ] 验证 ROI 数量较多时最大并发不超过 4，避免 CPU/GPU 资源打满。
- [ ] 验证结果绘制、结果汇总、条件判断、发送AI结果全链路。
- [ ] 记录本次代码变更到任务文件，符合项目要求。

## 主要风险

- `AnomalibRuntime` 体积大，且含授权文件，不能简单提交到源码仓库。
- 当前 TDJS-Vision 使用 OpenCvSharp 4.10，目标工程使用 4.11；直接升级包会扩大风险，MVP 建议先沿用当前项目 4.10。
- `AnomalibLib.dll` 的 `Infer` 要求 BGR 三通道图像，节点需要对灰度或 BGRA 输入做转换。
- 原训练窗体是代码动态创建 UI，不符合当前“控件进入 Designer.cs”的要求，迁移训练界面时需要重拆。
- 当前部分结果处理节点显式绑定 `AITD`，需要统一结果获取方式，避免新增节点跑通但下游无法订阅。

## 建议工期

- 只做推理 MVP：1 到 2 天。
- 推理节点 + 结果处理全链路 + 基础测试：2 到 4 天。
- 再加训练工具、ROI 数据集、部署检查：4 到 7 天。
- 若要做完整发布包、授权校验、GPU/CPU 多机测试：7 到 10 天。
