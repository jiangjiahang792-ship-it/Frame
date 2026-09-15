using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using OpenCvSharp;
using CvRect = OpenCvSharp.Rect;

namespace TDJS_Vision.Forms.AiTrainForm
{
    /// <summary>
    /// 定义训练服务可替换的运行环境解析边界。
    /// </summary>
    internal interface ILargeModelTrainingEnvironment
    {
        /// <summary>
        /// 验证大模型运行环境。
        /// </summary>
        /// <returns>运行环境状态。</returns>
        LargeModelRuntimeStatus ValidateRuntime();

        /// <summary>
        /// 确保并返回固定 Model 根目录。
        /// </summary>
        /// <returns>Model 根目录。</returns>
        string EnsureModelRoot();

        /// <summary>
        /// 解析实际 native 运行目录。
        /// </summary>
        /// <param name="runtimeRoot">运行环境检查返回的根目录。</param>
        /// <returns>实际 native 运行目录。</returns>
        string ResolveRuntimeRoot(string runtimeRoot);
    }

    /// <summary>
    /// 定义能够创建训练结果的完整训练阶段会话。
    /// </summary>
    internal interface ILargeModelTrainingSession : ILargeModelTrainingStages
    {
        /// <summary>
        /// 根据协调器返回的本次模型创建服务结果。
        /// </summary>
        /// <param name="modelPath">本次新模型路径。</param>
        /// <returns>训练结果。</returns>
        LargeModelTrainingResult CreateResult(string modelPath);
    }

    /// <summary>
    /// 定义服务入口可替换的训练阶段工厂。
    /// </summary>
    internal interface ILargeModelTrainingStageFactory
    {
        /// <summary>
        /// 创建一次完整训练使用的阶段会话。
        /// </summary>
        /// <param name="runtimeRoot">native 运行目录。</param>
        /// <param name="modelRoot">固定 Model 根目录。</param>
        /// <param name="request">训练请求。</param>
        /// <param name="progress">训练进度回调。</param>
        /// <param name="log">训练日志回调。</param>
        /// <returns>本次训练阶段会话。</returns>
        ILargeModelTrainingSession Create(
            string runtimeRoot,
            string modelRoot,
            LargeModelTrainingRequest request,
            IProgress<int> progress,
            IProgress<string> log);
    }

    /// <summary>
    /// 定义生产训练阶段使用的可替换模型重建边界。
    /// </summary>
    internal interface ILargeModelTrainingModelRebuilder
    {
        /// <summary>
        /// 为本次请求重新生成 ONNX。
        /// </summary>
        /// <param name="runtimeRoot">native 运行目录。</param>
        /// <param name="request">训练请求。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>本次 ONNX 正式路径。</returns>
        string RebuildOnnxModel(
            string runtimeRoot,
            LargeModelTrainingRequest request,
            IProgress<string> log,
            CancellationToken token);

        /// <summary>
        /// 使用本次 ONNX 重新生成 TensorRT Engine。
        /// </summary>
        /// <param name="runtimeRoot">native 运行目录。</param>
        /// <param name="onnxPath">本次 ONNX 正式路径。</param>
        /// <param name="request">训练请求。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>本次 Engine 正式路径。</returns>
        string RebuildTensorRtEngine(
            string runtimeRoot,
            string onnxPath,
            LargeModelTrainingRequest request,
            IProgress<string> log,
            CancellationToken token);
    }

    /// <summary>
    /// 定义生产阶段和模型验证器统一使用的检测器工厂。
    /// </summary>
    internal interface ILargeModelDinov2DetectorFactory
    {
        /// <summary>
        /// 创建绑定指定运行目录的真实检测器。
        /// </summary>
        /// <param name="runtimeRoot">native 运行目录。</param>
        /// <returns>DINOv2 检测器。</returns>
        LargeModelDinov2Detector Create(string runtimeRoot);
    }

    /// <summary>
    /// 大模型训练服务，负责数据集准备、DINOv2 bank 训练和模板打包。
    /// </summary>
    public sealed class LargeModelTrainingService
    {
        /// <summary>
        /// DINOv2 ViT 的 patch 尺寸，导出分辨率必须按该尺寸对齐。
        /// </summary>
        private const int Dinov2PatchSize = 14;

        /// <summary>
        /// ONNX 产物允许发布的最小字节数。
        /// </summary>
        private const long MinimumOnnxArtifactLength = 64;

        /// <summary>
        /// TensorRT Engine 产物允许发布的最小字节数。
        /// </summary>
        private const long MinimumEngineArtifactLength = 64;

        /// <summary>
        /// 取消外部进程后等待其退出的最长毫秒数。
        /// </summary>
        private const int ProcessTerminationWaitMilliseconds = 5000;

        /// <summary>
        /// 自动识别分数校准时最多读取的异常框数量。
        /// </summary>
        private const int CalibrationMaxBoxes = 128;

        /// <summary>
        /// 当前训练使用的 native 检测器。
        /// </summary>
        private LargeModelDinov2Detector _activeDetector;

        /// <summary>
        /// 当前训练检测器访问锁。
        /// </summary>
        private readonly object _activeDetectorLock = new object();

        /// <summary>
        /// 保护完整训练链顺序与跨进程串行的协调器。
        /// </summary>
        private readonly LargeModelTrainingCoordinator _trainingCoordinator;

        /// <summary>
        /// 负责模型临时生成、有效性验证和正式发布的组件。
        /// </summary>
        private readonly LargeModelArtifactPublisher _artifactPublisher;

        /// <summary>
        /// 负责验证并解析 runtime 与固定 Model 根目录的环境边界。
        /// </summary>
        private readonly ILargeModelTrainingEnvironment _trainingEnvironment;

        /// <summary>
        /// 可选的完整训练阶段工厂；为空时使用真实生产阶段适配器。
        /// </summary>
        private readonly ILargeModelTrainingStageFactory _trainingStageFactory;

        /// <summary>
        /// 生产阶段使用的模型重建边界。
        /// </summary>
        private readonly ILargeModelTrainingModelRebuilder _modelRebuilder;

        /// <summary>
        /// 生产阶段和模型验证统一使用的检测器工厂。
        /// </summary>
        private readonly ILargeModelDinov2DetectorFactory _detectorFactory;

        /// <summary>
        /// 生产阶段使用的模板发布器。
        /// </summary>
        private readonly ILargeModelTemplatePublisher _templatePublisher;

        /// <summary>
        /// 使用生产协调器和产物发布器初始化大模型训练服务。
        /// </summary>
        public LargeModelTrainingService()
        {
            _trainingCoordinator = new LargeModelTrainingCoordinator();
            _artifactPublisher = new LargeModelArtifactPublisher();
            _trainingEnvironment = new ProductionTrainingEnvironment();
            _trainingStageFactory = null;
            _detectorFactory = new ProductionDinov2DetectorFactory();
            _templatePublisher = new LargeModelTemplatePublisher();
            _modelRebuilder = new ProductionTrainingModelRebuilder(this);
        }

        /// <summary>
        /// 使用可替换组件初始化大模型训练服务。
        /// </summary>
        /// <param name="trainingCoordinator">完整训练链协调器。</param>
        /// <param name="artifactPublisher">模型产物发布器。</param>
        internal LargeModelTrainingService(
            LargeModelTrainingCoordinator trainingCoordinator,
            LargeModelArtifactPublisher artifactPublisher)
        {
            _trainingCoordinator = trainingCoordinator ?? throw new ArgumentNullException(nameof(trainingCoordinator));
            _artifactPublisher = artifactPublisher ?? throw new ArgumentNullException(nameof(artifactPublisher));
            _trainingEnvironment = new ProductionTrainingEnvironment();
            _trainingStageFactory = null;
            _detectorFactory = new ProductionDinov2DetectorFactory();
            _templatePublisher = new LargeModelTemplatePublisher();
            _modelRebuilder = new ProductionTrainingModelRebuilder(this);
        }

        /// <summary>
        /// 使用可替换生产边界初始化可执行训练服务。
        /// </summary>
        /// <param name="trainingCoordinator">完整训练链协调器。</param>
        /// <param name="artifactPublisher">模型产物发布器。</param>
        /// <param name="trainingEnvironment">运行环境解析边界。</param>
        /// <param name="trainingStageFactory">可选训练阶段工厂；为空时使用真实生产适配器。</param>
        /// <param name="modelRebuilder">模型重建边界。</param>
        /// <param name="detectorFactory">检测器工厂。</param>
        /// <param name="templatePublisher">模板发布器。</param>
        internal LargeModelTrainingService(
            LargeModelTrainingCoordinator trainingCoordinator,
            LargeModelArtifactPublisher artifactPublisher,
            ILargeModelTrainingEnvironment trainingEnvironment,
            ILargeModelTrainingStageFactory trainingStageFactory,
            ILargeModelTrainingModelRebuilder modelRebuilder,
            ILargeModelDinov2DetectorFactory detectorFactory,
            ILargeModelTemplatePublisher templatePublisher)
        {
            _trainingCoordinator = trainingCoordinator ?? throw new ArgumentNullException(nameof(trainingCoordinator));
            _artifactPublisher = artifactPublisher ?? throw new ArgumentNullException(nameof(artifactPublisher));
            _trainingEnvironment = trainingEnvironment ?? throw new ArgumentNullException(nameof(trainingEnvironment));
            _trainingStageFactory = trainingStageFactory;
            _modelRebuilder = modelRebuilder ?? throw new ArgumentNullException(nameof(modelRebuilder));
            _detectorFactory = detectorFactory ?? throw new ArgumentNullException(nameof(detectorFactory));
            _templatePublisher = templatePublisher ?? throw new ArgumentNullException(nameof(templatePublisher));
        }

        /// <summary>
        /// 执行一次大模型训练。
        /// </summary>
        /// <param name="request">训练请求。</param>
        /// <param name="progress">训练进度回调。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>训练结果。</returns>
        public LargeModelTrainingResult Train(LargeModelTrainingRequest request, IProgress<int> progress, IProgress<string> log, CancellationToken token)
        {
            ValidateRequest(request);
            LargeModelRuntimeStatus runtime = _trainingEnvironment.ValidateRuntime();
            if (!runtime.IsReady)
                throw new InvalidOperationException(runtime.Message);

            string modelRoot = _trainingEnvironment.EnsureModelRoot();
            EnsureTemplatePathInModelRoot(request.OutputTemplatePath, modelRoot);
            string runtimeRoot = _trainingEnvironment.ResolveRuntimeRoot(runtime.RootPath);
            bool useGpu = !string.Equals(
                LargeModelRuntimeBootstrapper.NormalizeDevice(request.Device),
                "CPU",
                StringComparison.OrdinalIgnoreCase);
            ILargeModelTrainingSession stages = _trainingStageFactory != null
                ? _trainingStageFactory.Create(runtimeRoot, modelRoot, request, progress, log)
                : new ProductionTrainingStages(
                    this,
                    _modelRebuilder,
                    _detectorFactory,
                    _templatePublisher,
                    runtimeRoot,
                    modelRoot,
                    request,
                    progress,
                    log);
            string modelPath = _trainingCoordinator.Execute(runtimeRoot, useGpu, stages, log, token);
            return stages.CreateResult(modelPath);
        }

        /// <summary>
        /// 请求取消当前 native 训练。
        /// </summary>
        public void CancelActiveTraining()
        {
            lock (_activeDetectorLock)
            {
                if (_activeDetector != null)
                {
                    _activeDetector.Dispose();
                    _activeDetector = null;
                }
            }
        }

        /// <summary>
        /// 设置当前训练检测器。
        /// </summary>
        /// <param name="detector">检测器实例。</param>
        private void SetActiveDetector(LargeModelDinov2Detector detector)
        {
            lock (_activeDetectorLock)
            {
                _activeDetector = detector;
            }
        }

        /// <summary>
        /// 校验训练请求。
        /// </summary>
        /// <param name="request">训练请求。</param>
        private static void ValidateRequest(LargeModelTrainingRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.TemplateName))
                throw new InvalidOperationException("模板名称不能为空。");
            ValidateTemplateName(request.TemplateName);
            if (string.IsNullOrWhiteSpace(request.OutputTemplatePath))
                throw new InvalidOperationException("模板输出路径不能为空。");
            if (request.Images == null || request.Images.Count == 0)
                throw new InvalidOperationException("请先加载训练图片。");
            int okImageCount = request.Images.Count(item => item.Category == LargeModelImageCategory.OK);
            if (okImageCount < 1)
                throw new InvalidOperationException("大模型训练至少需要 1 张 OK 图片。");
            if (request.InputWidth <= 0 || request.InputHeight <= 0)
                throw new InvalidOperationException("训练输入尺寸必须大于 0。");
            ValidatePatchSizeMultiple(request.InputWidth, "输入宽度");
            ValidatePatchSizeMultiple(request.InputHeight, "输入高度");
            if (request.AreaThreshold < 0)
                throw new InvalidOperationException("面积阈值不能小于 0。");
        }

        /// <summary>
        /// 校验 DINOv2 输入尺寸必须按 patch 网格对齐。
        /// </summary>
        /// <param name="value">尺寸值。</param>
        /// <param name="displayName">显示名称。</param>
        private static void ValidatePatchSizeMultiple(int value, string displayName)
        {
            if (value % Dinov2PatchSize != 0)
                throw new InvalidOperationException(displayName + "必须是 14 的倍数。");
        }

        /// <summary>
        /// 为本次训练重新导出并发布所选分辨率的 ONNX。
        /// </summary>
        /// <param name="root">大模型运行环境目录。</param>
        /// <param name="width">输入宽度。</param>
        /// <param name="height">输入高度。</param>
        /// <param name="sizeText">分辨率文本。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>通过有效性与 native 加载验证的新 ONNX 正式路径。</returns>
        private string RebuildOnnxModelPath(
            string root,
            int width,
            int height,
            string sizeText,
            IProgress<string> log,
            CancellationToken token)
        {
            string onnxPath = Path.Combine(root, "dinov2_" + sizeText + ".onnx");
            log?.Report("开始为本次训练重新导出 DINOv2 ONNX：" + Path.GetFileName(onnxPath));
            string publishedPath = _artifactPublisher.Publish(
                onnxPath,
                "DINOv2 ONNX",
                MinimumOnnxArtifactLength,
                new CallbackArtifactGenerator(
                    (temporaryPath, generationToken) => ExportOnnxModel(
                        root,
                        temporaryPath,
                        width,
                        height,
                        sizeText,
                        log,
                        generationToken)),
                new CallbackArtifactValidator(
                    temporaryPath => ValidateOnnxArtifactInSubprocess(
                        root,
                        temporaryPath,
                        log,
                        token)),
                log,
                token);
            log?.Report("本次训练 ONNX 已更新：" + publishedPath);
            return publishedPath;
        }

        /// <summary>
        /// 拒绝可被文件系统解释为当前目录或父目录的模板名称。
        /// </summary>
        /// <param name="templateName">原始模板名称。</param>
        private static void ValidateTemplateName(string templateName)
        {
            string normalizedName = templateName == null ? string.Empty : templateName.Trim();
            if (string.Equals(normalizedName, ".", StringComparison.Ordinal) ||
                string.Equals(normalizedName, "..", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("模板名称不能使用当前目录或父目录标记。");
            }
        }

        /// <summary>
        /// 使用本次 ONNX 重新生成并发布 TensorRT Engine。
        /// </summary>
        /// <param name="root">大模型运行环境目录。</param>
        /// <param name="onnxPath">本次新生成的 ONNX 正式路径。</param>
        /// <param name="sizeText">分辨率文本。</param>
        /// <param name="precision">fp16 或 fp32。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>通过有效性与 native 加载验证的新 TensorRT Engine 正式路径。</returns>
        private string RebuildTensorRtEnginePath(
            string root,
            string onnxPath,
            string sizeText,
            string precision,
            IProgress<string> log,
            CancellationToken token)
        {
            string normalizedPrecision = LargeModelRuntimeBootstrapper.NormalizePrecision(precision);
            string enginePath = Path.Combine(root, "dinov2_" + sizeText + "_" + normalizedPrecision + ".engine");
            string trtexecPath = Path.Combine(root, "trtexec.exe");
            if (!File.Exists(trtexecPath))
                throw new FileNotFoundException("LargeModelDll 缺少 trtexec.exe，无法从 ONNX 生成 TensorRT Engine。", trtexecPath);

            log?.Report("开始使用当前电脑显卡重新生成 TensorRT Engine：" + Path.GetFileName(enginePath));
            string publishedPath = _artifactPublisher.Publish(
                enginePath,
                "TensorRT Engine",
                MinimumEngineArtifactLength,
                new CallbackArtifactGenerator(
                    (temporaryPath, generationToken) => GenerateTensorRtEngine(
                        root,
                        trtexecPath,
                        onnxPath,
                        temporaryPath,
                        normalizedPrecision,
                        log,
                        generationToken)),
                new CallbackArtifactValidator(
                    temporaryPath => ValidateTensorRtEngineInSubprocess(
                        root,
                        trtexecPath,
                        temporaryPath,
                        log,
                        token)),
                log,
                token);
            log?.Report("TensorRT Engine 重新生成完成：" + publishedPath);
            return publishedPath;
        }

        /// <summary>
        /// 调用 demo 导出脚本生成固定位置编码的 DINOv2 ONNX。
        /// </summary>
        /// <param name="root">大模型运行环境目录。</param>
        /// <param name="onnxPath">目标 ONNX 路径。</param>
        /// <param name="width">输入宽度。</param>
        /// <param name="height">输入高度。</param>
        /// <param name="sizeText">分辨率文本。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        private static void ExportOnnxModel(string root, string onnxPath, int width, int height, string sizeText, IProgress<string> log, CancellationToken token)
        {
            string pythonPath = Path.Combine(root, "third_party", "python_native_env", "python.exe");
            string scriptPath = Path.Combine(root, "export_dinov2.py");
            string repoPath = Path.Combine(root, "dinov2");
            string missingPath = null;

            if (!File.Exists(pythonPath))
                missingPath = pythonPath;
            else if (!File.Exists(scriptPath))
                missingPath = scriptPath;
            else if (!Directory.Exists(repoPath))
                missingPath = repoPath;

            if (!string.IsNullOrWhiteSpace(missingPath))
            {
                throw new FileNotFoundException(
                    "LargeModelDll 缺少 ONNX 导出环境，无法为本次训练重新生成 dinov2_" + sizeText + ".onnx。请补齐 LargeModelDll 下的 export_dinov2.py、dinov2 源码目录和 third_party\\python_native_env。",
                    missingPath);
            }

            string torchCacheRoot = Path.Combine(root, "_torch_cache");
            Directory.CreateDirectory(torchCacheRoot);

            string arguments = QuoteProcessArgument(scriptPath)
                + " --width " + width.ToString(CultureInfo.InvariantCulture)
                + " --height " + height.ToString(CultureInfo.InvariantCulture)
                + " --pos-mode fixed"
                + " --output " + QuoteProcessArgument(onnxPath)
                + " --repo " + QuoteProcessArgument(repoPath);
            LargeModelProcessOutputBuffer processOutput = new LargeModelProcessOutputBuffer();

            using (System.Diagnostics.Process process = new System.Diagnostics.Process())
            {
                process.StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = arguments,
                    WorkingDirectory = root,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                ConfigureOnnxExportEnvironment(process.StartInfo, root, torchCacheRoot);

                process.OutputDataReceived += (sender, args) => AppendProcessLog(processOutput, log, args.Data);
                process.ErrorDataReceived += (sender, args) => AppendProcessLog(processOutput, log, args.Data);

                log?.Report("正在导出 DINOv2 ONNX：" + sizeText);
                token.ThrowIfCancellationRequested();
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                while (!process.WaitForExit(500))
                {
                    if (token.IsCancellationRequested)
                    {
                        TryKillProcessAndWait(process, log);
                        token.ThrowIfCancellationRequested();
                    }
                }

                process.WaitForExit();
                token.ThrowIfCancellationRequested();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("DINOv2 ONNX 导出失败，python 返回码：" + process.ExitCode.ToString(CultureInfo.InvariantCulture) + "\r\n" + processOutput.GetText());
            }

            log?.Report("DINOv2 ONNX 导出完成：" + onnxPath);
        }

        /// <summary>
        /// 在隔离 Python 子进程中检查 ONNX 结构，避免不安全的 native 初始化破坏主程序内存。
        /// </summary>
        /// <param name="runtimeRoot">大模型运行环境目录。</param>
        /// <param name="onnxPath">待验证的 ONNX 临时文件。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        private static void ValidateOnnxArtifactInSubprocess(
            string runtimeRoot,
            string onnxPath,
            IProgress<string> log,
            CancellationToken token)
        {
            string pythonPath = Path.Combine(runtimeRoot, "third_party", "python_native_env", "python.exe");
            string torchCacheRoot = Path.Combine(runtimeRoot, "_torch_cache");
            const string validationScript = "import onnx,sys; onnx.checker.check_model(sys.argv[1]); print('ONNX validation passed')";
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = "-c " + QuoteProcessArgument(validationScript) + " " + QuoteProcessArgument(onnxPath),
                WorkingDirectory = runtimeRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            ConfigureOnnxExportEnvironment(startInfo, runtimeRoot, torchCacheRoot);
            log?.Report("正在隔离子进程中校验 DINOv2 ONNX。");
            RunArtifactValidationProcess(startInfo, "DINOv2 ONNX", log, token);
        }

        /// <summary>
        /// 配置 ONNX 导出进程的隔离环境变量。
        /// </summary>
        /// <param name="startInfo">进程启动信息。</param>
        /// <param name="runtimeRoot">大模型运行环境目录。</param>
        /// <param name="torchCacheRoot">Torch 权重缓存目录。</param>
        private static void ConfigureOnnxExportEnvironment(System.Diagnostics.ProcessStartInfo startInfo, string runtimeRoot, string torchCacheRoot)
        {
            string pythonRoot = Path.Combine(runtimeRoot, "third_party", "python_native_env");
            PrependProcessPath(startInfo, runtimeRoot, pythonRoot, Path.Combine(pythonRoot, "Library", "bin"), Path.Combine(pythonRoot, "Scripts"));
            // 无监督 native 初始化会修改进程级 PYTHONHOME；子进程必须显式恢复到大模型独立环境，避免标准库与解释器混用。
            SetProcessEnvironment(startInfo, "PYTHONHOME", pythonRoot);
            SetProcessEnvironment(startInfo, "PYTHONPATH", string.Empty);
            SetProcessEnvironment(startInfo, "PYTHONDONTWRITEBYTECODE", "1");
            SetProcessEnvironment(startInfo, "PYTHONNOUSERSITE", "1");
            SetProcessEnvironment(startInfo, "TORCH_HOME", torchCacheRoot);
        }

        /// <summary>
        /// 给进程 PATH 前置隔离运行目录，避免命中主机上其它版本的依赖库。
        /// </summary>
        /// <param name="startInfo">进程启动信息。</param>
        /// <param name="paths">需要前置的目录列表。</param>
        private static void PrependProcessPath(System.Diagnostics.ProcessStartInfo startInfo, params string[] paths)
        {
            string oldPath = startInfo.EnvironmentVariables["PATH"];
            StringBuilder builder = new StringBuilder();
            foreach (string path in paths)
            {
                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                    continue;

                if (builder.Length > 0)
                    builder.Append(';');
                builder.Append(path);
            }

            if (!string.IsNullOrWhiteSpace(oldPath))
            {
                if (builder.Length > 0)
                    builder.Append(';');
                builder.Append(oldPath);
            }

            startInfo.EnvironmentVariables["PATH"] = builder.ToString();
        }

        /// <summary>
        /// 设置子进程环境变量。
        /// </summary>
        /// <param name="startInfo">进程启动信息。</param>
        /// <param name="name">变量名。</param>
        /// <param name="value">变量值。</param>
        private static void SetProcessEnvironment(System.Diagnostics.ProcessStartInfo startInfo, string name, string value)
        {
            startInfo.EnvironmentVariables[name] = value;
        }

        /// <summary>
        /// 按 Windows 命令行规则包装进程参数。
        /// </summary>
        /// <param name="value">原始参数值。</param>
        /// <returns>带引号的参数值。</returns>
        private static string QuoteProcessArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        /// <summary>
        /// 调用 trtexec 从本次 ONNX 生成指定临时路径的 TensorRT Engine。
        /// </summary>
        /// <param name="runtimeRoot">大模型运行环境目录。</param>
        /// <param name="trtexecPath">trtexec 路径。</param>
        /// <param name="onnxPath">ONNX 模型路径。</param>
        /// <param name="engineOutputPath">本次唯一 Engine 临时输出路径。</param>
        /// <param name="precision">fp16 或 fp32。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        private static void GenerateTensorRtEngine(
            string runtimeRoot,
            string trtexecPath,
            string onnxPath,
            string engineOutputPath,
            string precision,
            IProgress<string> log,
            CancellationToken token)
        {
            string precisionArgs = string.Equals(precision, "fp16", StringComparison.OrdinalIgnoreCase) ? " --fp16" : " --noTF32";
            string arguments = "--onnx=" + QuoteProcessArgument(onnxPath)
                + " --saveEngine=" + QuoteProcessArgument(engineOutputPath)
                + precisionArgs + " --workspace=4096";
            LargeModelProcessOutputBuffer processOutput = new LargeModelProcessOutputBuffer();

            using (System.Diagnostics.Process process = new System.Diagnostics.Process())
            {
                process.StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = trtexecPath,
                    Arguments = arguments,
                    WorkingDirectory = runtimeRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                process.OutputDataReceived += (sender, args) => AppendProcessLog(processOutput, log, args.Data);
                process.ErrorDataReceived += (sender, args) => AppendProcessLog(processOutput, log, args.Data);

                token.ThrowIfCancellationRequested();
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                while (!process.WaitForExit(500))
                {
                    if (token.IsCancellationRequested)
                    {
                        TryKillProcessAndWait(process, log);
                        token.ThrowIfCancellationRequested();
                    }
                }

                process.WaitForExit();
                token.ThrowIfCancellationRequested();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("TensorRT Engine 生成失败，trtexec 返回码：" + process.ExitCode.ToString(CultureInfo.InvariantCulture) + "\r\n" + processOutput.GetText());
            }
        }

        /// <summary>
        /// 在独立 trtexec 子进程中反序列化 Engine，native 崩溃时不会终止主程序。
        /// </summary>
        /// <param name="runtimeRoot">大模型运行环境目录。</param>
        /// <param name="trtexecPath">trtexec 程序路径。</param>
        /// <param name="enginePath">待验证的 Engine 临时文件。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        private static void ValidateTensorRtEngineInSubprocess(
            string runtimeRoot,
            string trtexecPath,
            string enginePath,
            IProgress<string> log,
            CancellationToken token)
        {
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = trtexecPath,
                Arguments = "--loadEngine=" + QuoteProcessArgument(enginePath) + " --skipInference",
                WorkingDirectory = runtimeRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            log?.Report("正在隔离子进程中校验 TensorRT Engine。");
            RunArtifactValidationProcess(startInfo, "TensorRT Engine", log, token);
        }

        /// <summary>
        /// 执行可取消的模型产物子进程校验并收集失败诊断。
        /// </summary>
        /// <param name="startInfo">校验进程启动信息。</param>
        /// <param name="stageName">模型产物阶段名称。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        private static void RunArtifactValidationProcess(
            System.Diagnostics.ProcessStartInfo startInfo,
            string stageName,
            IProgress<string> log,
            CancellationToken token)
        {
            LargeModelProcessOutputBuffer processOutput = new LargeModelProcessOutputBuffer();
            using (System.Diagnostics.Process process = new System.Diagnostics.Process())
            {
                process.StartInfo = startInfo;
                process.OutputDataReceived += (sender, args) => AppendProcessLog(processOutput, null, args.Data);
                process.ErrorDataReceived += (sender, args) => AppendProcessLog(processOutput, null, args.Data);

                token.ThrowIfCancellationRequested();
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                while (!process.WaitForExit(500))
                {
                    if (token.IsCancellationRequested)
                    {
                        TryKillProcessAndWait(process, log);
                        token.ThrowIfCancellationRequested();
                    }
                }

                process.WaitForExit();
                token.ThrowIfCancellationRequested();
                if (process.ExitCode != 0)
                {
                    throw new InvalidDataException(
                        stageName + " 子进程校验失败，返回码："
                        + process.ExitCode.ToString(CultureInfo.InvariantCulture)
                        + "\r\n" + processOutput.GetText());
                }
            }

            log?.Report(stageName + " 子进程校验通过。");
        }

        /// <summary>
        /// 收集外部进程输出并写入训练日志。
        /// </summary>
        /// <param name="buffer">线程安全输出缓存。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="line">输出行。</param>
        private static void AppendProcessLog(LargeModelProcessOutputBuffer buffer, IProgress<string> log, string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            buffer.AppendLine(line);
            log?.Report(line);
        }

        /// <summary>
        /// 取消时尽力终止外部进程，并在清理临时文件前有界等待进程退出。
        /// </summary>
        /// <param name="process">进程实例。</param>
        /// <param name="log">训练日志回调。</param>
        private static void TryKillProcessAndWait(System.Diagnostics.Process process, IProgress<string> log)
        {
            try
            {
                if (process != null && !process.HasExited)
                    process.Kill();
            }
            catch (Exception ex)
            {
                TryReportProcessMessage(log, "取消训练时终止外部进程失败：" + ex.Message);
            }

            try
            {
                if (process != null && !process.HasExited && !process.WaitForExit(ProcessTerminationWaitMilliseconds))
                    TryReportProcessMessage(log, "取消训练后外部进程未在限定时间内退出，临时文件将继续尽力清理。");
            }
            catch (Exception ex)
            {
                TryReportProcessMessage(log, "取消训练时等待外部进程退出失败：" + ex.Message);
            }
        }

        /// <summary>
        /// 尽力写入外部进程取消日志，日志回调异常不得覆盖取消异常。
        /// </summary>
        /// <param name="log">训练日志回调。</param>
        /// <param name="message">简体中文日志内容。</param>
        private static void TryReportProcessMessage(IProgress<string> log, string message)
        {
            try
            {
                log?.Report(message);
            }
            catch
            {
                // 保留当前取消异常，不让日志回调失败改变异常语义。
            }
        }

        /// <summary>
        /// 生成模型尺寸文本。
        /// </summary>
        /// <param name="width">输入宽度。</param>
        /// <param name="height">输入高度。</param>
        /// <returns>例如 448x224。</returns>
        private static string BuildSizeText(int width, int height)
        {
            return width.ToString(CultureInfo.InvariantCulture) + "x" + height.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 校验实际写入训练数据集的图片数量，避免 native 返回笼统错误码。
        /// </summary>
        /// <param name="dataset">数据集构建结果。</param>
        private static void ValidateDatasetImageCount(DatasetBuildResult dataset)
        {
            if (dataset.OkCount <= 0)
                throw new InvalidOperationException("没有可用于训练的 OK 图片。");
        }

        /// <summary>
        /// 确保模板路径位于运行目录 Model 文件夹内。
        /// </summary>
        /// <param name="templatePath">模板路径。</param>
        /// <param name="modelRoot">Model 根目录。</param>
        private static void EnsureTemplatePathInModelRoot(string templatePath, string modelRoot)
        {
            string fullTemplatePath = Path.GetFullPath(templatePath);
            string fullModelRoot = EnsureTrailingSeparator(Path.GetFullPath(modelRoot));
            if (!fullTemplatePath.StartsWith(fullModelRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("模板文件必须保存到运行目录 Model 文件夹。");
        }

        /// <summary>
        /// 准备受控工作目录，避免误删其它路径。
        /// </summary>
        /// <param name="targetPath">待清理目录。</param>
        /// <param name="allowedRoot">允许清理的根目录。</param>
        private static void PrepareCleanDirectory(string targetPath, string allowedRoot)
        {
            string fullTarget = Path.GetFullPath(targetPath);
            string fullAllowed = EnsureTrailingSeparator(Path.GetFullPath(allowedRoot));
            if (!fullTarget.StartsWith(fullAllowed, StringComparison.OrdinalIgnoreCase) ||
                fullTarget.Length <= fullAllowed.Length)
                throw new InvalidOperationException("训练临时目录不在允许范围内。");

            if (Directory.Exists(fullTarget))
            {
                Directory.Delete(fullTarget, true);
            }

            Directory.CreateDirectory(fullTarget);
        }

        /// <summary>
        /// 构建训练数据集。
        /// </summary>
        /// <param name="request">训练请求。</param>
        /// <param name="datasetRoot">数据集根目录。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>数据集构建结果。</returns>
        private static DatasetBuildResult BuildDataset(LargeModelTrainingRequest request, string datasetRoot, CancellationToken token)
        {
            string okPath = Path.Combine(datasetRoot, "OK");
            string ngPath = Path.Combine(datasetRoot, "NG");
            string calibrationOkPath = Path.Combine(datasetRoot, "Calibration", "OK");
            string calibrationNgPath = Path.Combine(datasetRoot, "Calibration", "NG");
            Directory.CreateDirectory(okPath);
            Directory.CreateDirectory(ngPath);
            Directory.CreateDirectory(calibrationOkPath);
            Directory.CreateDirectory(calibrationNgPath);

            int okCount = 0;
            int ngCount = 0;
            int index = 0;
            int sourceOkCount = request.Images.Count(item => item.Category == LargeModelImageCategory.OK);
            int sourceNgCount = request.Images.Count(item => item.Category == LargeModelImageCategory.NG);
            bool canReserveCalibrationImages = sourceOkCount > 1 && sourceNgCount > 0;
            string calibrationOkImagePath = null;
            string calibrationNgImagePath = null;
            foreach (LargeModelImageItem item in request.Images)
            {
                token.ThrowIfCancellationRequested();
                bool isNg = item.Category == LargeModelImageCategory.NG;
                bool reserveForCalibration = canReserveCalibrationImages &&
                    (isNg
                        ? string.IsNullOrWhiteSpace(calibrationNgImagePath)
                        : string.IsNullOrWhiteSpace(calibrationOkImagePath));
                string targetFolder = reserveForCalibration
                    ? (isNg ? calibrationNgPath : calibrationOkPath)
                    : (isNg ? ngPath : okPath);
                string savedPath = request.RoiRect.HasValue
                    ? CropImageToRoi(item.FilePath, targetFolder, request.RoiRect.Value, index)
                    : CopyImageToDataset(item.FilePath, targetFolder, index);

                if (!string.IsNullOrWhiteSpace(savedPath))
                {
                    if (reserveForCalibration)
                    {
                        if (isNg) calibrationNgImagePath = savedPath;
                        else calibrationOkImagePath = savedPath;
                    }
                    else if (isNg)
                    {
                        ngCount++;
                    }
                    else
                    {
                        okCount++;
                    }
                }

                index++;
            }

            return new DatasetBuildResult(okPath, ngPath, okCount, ngCount, calibrationOkImagePath, calibrationNgImagePath);
        }

        /// <summary>
        /// 复制整图到训练数据集；无 ROI 时使用该路径。
        /// </summary>
        /// <param name="sourceFile">源图路径。</param>
        /// <param name="targetFolder">目标目录。</param>
        /// <param name="index">图片序号。</param>
        /// <returns>保存成功返回 true。</returns>
        private static string CopyImageToDataset(string sourceFile, string targetFolder, int index)
        {
            if (!File.Exists(sourceFile)) return null;

            string extension = Path.GetExtension(sourceFile);
            if (string.IsNullOrWhiteSpace(extension)) extension = ".png";
            string targetFile = Path.Combine(targetFolder, BuildDatasetFileName(sourceFile, index, extension));
            File.Copy(sourceFile, targetFile, true);
            return targetFile;
        }

        /// <summary>
        /// 裁剪 ROI 到训练数据集。
        /// </summary>
        /// <param name="sourceFile">源图路径。</param>
        /// <param name="targetFolder">目标目录。</param>
        /// <param name="roi">图像坐标系 ROI。</param>
        /// <param name="index">图片序号。</param>
        /// <returns>保存成功返回 true。</returns>
        private static string CropImageToRoi(string sourceFile, string targetFolder, CvRect roi, int index)
        {
            if (!File.Exists(sourceFile)) return null;

            using (Mat image = Cv2.ImRead(sourceFile))
            {
                if (image.Empty()) return null;

                CvRect rect = ClampRect(roi, image.Width, image.Height);
                if (rect.Width <= 0 || rect.Height <= 0) return null;

                using (Mat crop = new Mat(image, rect).Clone())
                {
                    string targetFile = Path.Combine(targetFolder, BuildDatasetFileName(sourceFile, index, ".png"));
                    return Cv2.ImWrite(targetFile, crop) ? targetFile : null;
                }
            }
        }

        /// <summary>
        /// 使用预留 OK/NG 图像自动校准图像级识别分数。
        /// </summary>
        /// <param name="detector">训练完成并已加载 bank 的检测器。</param>
        /// <param name="dataset">包含预留校准图的数据集结果。</param>
        /// <param name="request">训练请求。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>可正确区分预留 OK/NG 的图像阈值。</returns>
        private static float CalibrateImageThreshold(
            LargeModelDinov2Detector detector,
            DatasetBuildResult dataset,
            LargeModelTrainingRequest request,
            IProgress<string> log,
            CancellationToken token)
        {
            if (detector == null)
                throw new ArgumentNullException(nameof(detector));

            token.ThrowIfCancellationRequested();
            if (!HasCalibrationImages(dataset))
            {
                log?.Report(
                    "大模型自动校准识别分数样本不足，使用默认图像阈值："
                    + request.ImageThreshold.ToString("0.######", CultureInfo.InvariantCulture));
                return request.ImageThreshold;
            }

            log?.Report("正在使用预留 OK/NG 图像自动校准大模型识别分数。");
            try
            {
                using (Mat okImage = LoadCalibrationImage(dataset.CalibrationOkImagePath, "OK"))
                using (Mat ngImage = LoadCalibrationImage(dataset.CalibrationNgImagePath, "NG"))
                {
                    LargeModelDinov2Result okProbe = detector.InferBgr(okImage, request.ImageThreshold, request.AreaThreshold, CalibrationMaxBoxes);
                    LargeModelDinov2Result ngProbe = detector.InferBgr(ngImage, request.ImageThreshold, request.AreaThreshold, CalibrationMaxBoxes);
                    if (okProbe.ReturnCode < 0 || ngProbe.ReturnCode < 0)
                        throw new InvalidOperationException("大模型自动校准探测推理失败。");

                    float threshold = SelectThresholdBetweenScores(okProbe.Score, ngProbe.Score, "大模型");
                    LargeModelDinov2Result okVerify = detector.InferBgr(okImage, threshold, request.AreaThreshold, CalibrationMaxBoxes);
                    LargeModelDinov2Result ngVerify = detector.InferBgr(ngImage, threshold, request.AreaThreshold, CalibrationMaxBoxes);
                    if (!okVerify.IsOk || !ngVerify.IsNg)
                    {
                        throw new InvalidOperationException(
                            "大模型自动校准识别分数失败，预留 OK/NG 样本无法同时判对；OK分数="
                            + okProbe.Score.ToString("0.######", CultureInfo.InvariantCulture)
                            + "，NG分数="
                            + ngProbe.Score.ToString("0.######", CultureInfo.InvariantCulture)
                            + "，候选阈值="
                            + threshold.ToString("0.######", CultureInfo.InvariantCulture)
                            + "。请更换更有代表性的 OK/NG 样本或调整最小面积。");
                    }

                    log?.Report(
                        "大模型识别分数自动校准完成，OK分数="
                        + okProbe.Score.ToString("0.######", CultureInfo.InvariantCulture)
                        + "，NG分数="
                        + ngProbe.Score.ToString("0.######", CultureInfo.InvariantCulture)
                        + "，阈值="
                        + threshold.ToString("0.######", CultureInfo.InvariantCulture));
                    return threshold;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                log?.Report(
                    "大模型自动校准识别分数未成功，已使用默认图像阈值 "
                    + request.ImageThreshold.ToString("0.######", CultureInfo.InvariantCulture)
                    + "，不影响模型生成。原因："
                    + ex.Message);
                return request.ImageThreshold;
            }
        }

        /// <summary>
        /// 判断本次数据集是否具备自动校准识别分数所需的预留样本。
        /// </summary>
        /// <param name="dataset">数据集构建结果。</param>
        /// <returns>具备 OK 和 NG 预留校准图时返回 true。</returns>
        private static bool HasCalibrationImages(DatasetBuildResult dataset)
        {
            return dataset != null &&
                !string.IsNullOrWhiteSpace(dataset.CalibrationOkImagePath) &&
                !string.IsNullOrWhiteSpace(dataset.CalibrationNgImagePath);
        }

        /// <summary>
        /// 加载已经按 ROI 规则保存的校准图片。
        /// </summary>
        /// <param name="imagePath">校准图片路径。</param>
        /// <param name="categoryText">分类文本。</param>
        /// <returns>OpenCV 图像。</returns>
        private static Mat LoadCalibrationImage(string imagePath, string categoryText)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                throw new FileNotFoundException("未找到大模型自动校准使用的 " + categoryText + " 图片。", imagePath);

            Mat image = Cv2.ImRead(imagePath);
            if (image.Empty())
            {
                image.Dispose();
                throw new InvalidDataException("大模型自动校准使用的 " + categoryText + " 图片为空或无法读取：" + imagePath);
            }

            return image;
        }

        /// <summary>
        /// 根据 OK 和 NG 的异常分数选择中间阈值。
        /// </summary>
        /// <param name="okScore">OK 校准图异常分数。</param>
        /// <param name="ngScore">NG 校准图异常分数。</param>
        /// <param name="moduleName">模块名称。</param>
        /// <returns>候选阈值。</returns>
        private static float SelectThresholdBetweenScores(float okScore, float ngScore, string moduleName)
        {
            if (float.IsNaN(okScore) || float.IsNaN(ngScore) || float.IsInfinity(okScore) || float.IsInfinity(ngScore))
                throw new InvalidOperationException(moduleName + "自动校准识别分数失败，native 返回了无效分数。");
            if (ngScore <= okScore)
                throw new InvalidOperationException(
                    moduleName + "自动校准识别分数失败，预留 NG 分数必须高于 OK 分数；OK分数="
                    + okScore.ToString("0.######", CultureInfo.InvariantCulture)
                    + "，NG分数="
                    + ngScore.ToString("0.######", CultureInfo.InvariantCulture)
                    + "。");

            double threshold = (okScore + ngScore) / 2.0;
            if (threshold <= 0.0)
                threshold = Math.Max(0.000001, ngScore / 2.0);
            return (float)threshold;
        }

        /// <summary>
        /// 构建模板清单。
        /// </summary>
        /// <param name="request">训练请求。</param>
        /// <param name="dataset">数据集结果。</param>
        /// <param name="modelPath">实际使用的 native 模型路径。</param>
        /// <param name="deviceMode">native 设备模式。</param>
        /// <returns>模板清单。</returns>
        private static LargeModelTemplateManifest BuildManifest(LargeModelTrainingRequest request, DatasetBuildResult dataset, string modelPath, int deviceMode, float imageThreshold)
        {
            CvRect roi = request.RoiRect.GetValueOrDefault();
            return new LargeModelTemplateManifest
            {
                Version = 1,
                TemplateName = request.TemplateName,
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                ModelType = string.IsNullOrWhiteSpace(request.ModelType) ? "DINOv2" : request.ModelType,
                Precision = LargeModelRuntimeBootstrapper.NormalizePrecision(request.Precision),
                DeviceMode = deviceMode,
                Device = LargeModelRuntimeBootstrapper.NormalizeDevice(request.Device),
                ModelFileName = Path.GetFileName(modelPath),
                BankFileName = "bank.bin",
                ImageThreshold = imageThreshold,
                AreaThreshold = request.AreaThreshold,
                InputWidth = request.InputWidth,
                InputHeight = request.InputHeight,
                RoiEnabled = request.RoiRect.HasValue,
                RoiX = request.RoiRect.HasValue ? roi.X : 0,
                RoiY = request.RoiRect.HasValue ? roi.Y : 0,
                RoiWidth = request.RoiRect.HasValue ? roi.Width : 0,
                RoiHeight = request.RoiRect.HasValue ? roi.Height : 0,
                SourceImageCount = request.Images.Count,
                OkCount = dataset.OkCount,
                NgCount = dataset.NgCount
            };
        }

        /// <summary>
        /// 限制 ROI 在图像范围内。
        /// </summary>
        /// <param name="rect">原始 ROI。</param>
        /// <param name="imageWidth">图像宽度。</param>
        /// <param name="imageHeight">图像高度。</param>
        /// <returns>裁剪后的 ROI。</returns>
        private static CvRect ClampRect(CvRect rect, int imageWidth, int imageHeight)
        {
            int x = Math.Max(0, Math.Min(rect.X, imageWidth - 1));
            int y = Math.Max(0, Math.Min(rect.Y, imageHeight - 1));
            int width = Math.Max(0, Math.Min(rect.Width, imageWidth - x));
            int height = Math.Max(0, Math.Min(rect.Height, imageHeight - y));
            return new CvRect(x, y, width, height);
        }

        /// <summary>
        /// 构建数据集文件名。
        /// </summary>
        /// <param name="sourceFile">源图路径。</param>
        /// <param name="index">图片序号。</param>
        /// <param name="extension">目标扩展名。</param>
        /// <returns>数据集文件名。</returns>
        private static string BuildDatasetFileName(string sourceFile, int index, string extension)
        {
            string name = Path.GetFileNameWithoutExtension(sourceFile);
            return index.ToString("D6", CultureInfo.InvariantCulture) + "_" + SafeFileName(name) + extension;
        }

        /// <summary>
        /// 安全化文件名。
        /// </summary>
        /// <param name="name">原始名称。</param>
        /// <returns>安全文件名。</returns>
        private static string SafeFileName(string name)
        {
            string value = string.IsNullOrWhiteSpace(name) ? "template" : name.Trim();
            ValidateTemplateName(value);
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(c, '_');
            }

            return value;
        }

        /// <summary>
        /// 在固定可信训练根下解析模板工作目录，并阻止规范化后的目录逃逸或等于可信根。
        /// </summary>
        /// <param name="modelRoot">固定 Model 根目录。</param>
        /// <param name="templateName">模板名称。</param>
        /// <param name="trustedTrainingRoot">返回的固定可信训练根。</param>
        /// <returns>严格位于可信训练根下的模板工作目录。</returns>
        private static string ResolveTrainingWorkRoot(
            string modelRoot,
            string templateName,
            out string trustedTrainingRoot)
        {
            trustedTrainingRoot = Path.GetFullPath(Path.Combine(modelRoot, "_LargeModelTraining"));
            string safeTemplateName = SafeFileName(templateName);
            string fullWorkRoot = Path.GetFullPath(Path.Combine(trustedTrainingRoot, safeTemplateName));
            string allowedPrefix = EnsureTrailingSeparator(trustedTrainingRoot);
            if (!fullWorkRoot.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase) ||
                fullWorkRoot.Length <= allowedPrefix.Length)
            {
                throw new InvalidOperationException("模板训练工作目录不在固定可信根目录内。");
            }

            return fullWorkRoot;
        }

        /// <summary>
        /// 确保目录路径以分隔符结尾。
        /// </summary>
        /// <param name="path">目录路径。</param>
        /// <returns>带分隔符的目录路径。</returns>
        private static string EnsureTrailingSeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        /// <summary>
        /// 使用现有运行时引导器提供生产训练环境。
        /// </summary>
        private sealed class ProductionTrainingEnvironment : ILargeModelTrainingEnvironment
        {
            /// <summary>
            /// 验证生产大模型运行环境。
            /// </summary>
            /// <returns>运行环境状态。</returns>
            public LargeModelRuntimeStatus ValidateRuntime()
            {
                return LargeModelRuntimeBootstrapper.ValidateRuntime();
            }

            /// <summary>
            /// 确保并返回生产 Model 根目录。
            /// </summary>
            /// <returns>Model 根目录。</returns>
            public string EnsureModelRoot()
            {
                return LargeModelRuntimeBootstrapper.EnsureModelRoot();
            }

            /// <summary>
            /// 解析生产 native 运行目录。
            /// </summary>
            /// <param name="runtimeRoot">环境检查返回的根目录。</param>
            /// <returns>实际 native 运行目录。</returns>
            public string ResolveRuntimeRoot(string runtimeRoot)
            {
                return LargeModelDinov2Native.ResolveRuntimeRoot(runtimeRoot);
            }
        }

        /// <summary>
        /// 使用训练服务现有外部进程和产物发布逻辑重新生成模型。
        /// </summary>
        private sealed class ProductionTrainingModelRebuilder : ILargeModelTrainingModelRebuilder
        {
            /// <summary>
            /// 提供实际 ONNX 与 Engine 重建方法的训练服务。
            /// </summary>
            private readonly LargeModelTrainingService _owner;

            /// <summary>
            /// 初始化生产模型重建器。
            /// </summary>
            /// <param name="owner">训练服务。</param>
            public ProductionTrainingModelRebuilder(LargeModelTrainingService owner)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            }

            /// <summary>
            /// 为本次请求重新生成并发布 ONNX。
            /// </summary>
            /// <param name="runtimeRoot">native 运行目录。</param>
            /// <param name="request">训练请求。</param>
            /// <param name="log">训练日志回调。</param>
            /// <param name="token">取消令牌。</param>
            /// <returns>本次 ONNX 正式路径。</returns>
            public string RebuildOnnxModel(
                string runtimeRoot,
                LargeModelTrainingRequest request,
                IProgress<string> log,
                CancellationToken token)
            {
                string sizeText = BuildSizeText(request.InputWidth, request.InputHeight);
                return _owner.RebuildOnnxModelPath(
                    runtimeRoot,
                    request.InputWidth,
                    request.InputHeight,
                    sizeText,
                    log,
                    token);
            }

            /// <summary>
            /// 使用本次 ONNX 重新生成并发布 TensorRT Engine。
            /// </summary>
            /// <param name="runtimeRoot">native 运行目录。</param>
            /// <param name="onnxPath">本次 ONNX 正式路径。</param>
            /// <param name="request">训练请求。</param>
            /// <param name="log">训练日志回调。</param>
            /// <param name="token">取消令牌。</param>
            /// <returns>本次 Engine 正式路径。</returns>
            public string RebuildTensorRtEngine(
                string runtimeRoot,
                string onnxPath,
                LargeModelTrainingRequest request,
                IProgress<string> log,
                CancellationToken token)
            {
                string sizeText = BuildSizeText(request.InputWidth, request.InputHeight);
                return _owner.RebuildTensorRtEnginePath(
                    runtimeRoot,
                    onnxPath,
                    sizeText,
                    request.Precision,
                    log,
                    token);
            }
        }

        /// <summary>
        /// 创建使用生产 native API 的 DINOv2 检测器。
        /// </summary>
        private sealed class ProductionDinov2DetectorFactory : ILargeModelDinov2DetectorFactory
        {
            /// <summary>
            /// 创建绑定指定运行目录的生产检测器。
            /// </summary>
            /// <param name="runtimeRoot">native 运行目录。</param>
            /// <returns>DINOv2 检测器。</returns>
            public LargeModelDinov2Detector Create(string runtimeRoot)
            {
                return new LargeModelDinov2Detector(runtimeRoot);
            }
        }

        /// <summary>
        /// 把生产模型导出或构建回调适配为可注入的产物生成器接口。
        /// </summary>
        private sealed class CallbackArtifactGenerator : ILargeModelArtifactGenerator
        {
            /// <summary>
            /// 实际执行模型生成的回调。
            /// </summary>
            private readonly Action<string, CancellationToken> _callback;

            /// <summary>
            /// 使用指定生成回调初始化适配器。
            /// </summary>
            /// <param name="callback">接收唯一临时路径和取消令牌的生成回调。</param>
            public CallbackArtifactGenerator(Action<string, CancellationToken> callback)
            {
                _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            }

            /// <summary>
            /// 执行实际模型导出或构建回调。
            /// </summary>
            /// <param name="temporaryPath">本次唯一临时模型路径。</param>
            /// <param name="token">取消令牌。</param>
            public void Generate(string temporaryPath, CancellationToken token)
            {
                _callback(temporaryPath, token);
            }
        }

        /// <summary>
        /// 把模型产物校验回调适配为可注入的验证器接口。
        /// </summary>
        private sealed class CallbackArtifactValidator : ILargeModelArtifactValidator
        {
            /// <summary>
            /// 实际执行模型产物校验的回调。
            /// </summary>
            private readonly Action<string> _callback;

            /// <summary>
            /// 初始化模型产物校验适配器。
            /// </summary>
            /// <param name="callback">接收待验证模型路径的校验回调。</param>
            public CallbackArtifactValidator(Action<string> callback)
            {
                _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            }

            /// <summary>
            /// 执行隔离的模型产物校验。
            /// </summary>
            /// <param name="modelPath">待验证的临时 ONNX 或 Engine 路径。</param>
            public void Validate(string modelPath)
            {
                _callback(modelPath);
            }
        }

        /// <summary>
        /// 把现有数据集、模型、Bank 和模板逻辑接入可测试的训练协调接口。
        /// </summary>
        internal sealed class ProductionTrainingStages : ILargeModelTrainingSession
        {
            /// <summary>
            /// 拥有检测器取消状态和模型发布器的训练服务。
            /// </summary>
            private readonly LargeModelTrainingService _owner;

            /// <summary>
            /// 当前阶段使用的可替换模型重建器。
            /// </summary>
            private readonly ILargeModelTrainingModelRebuilder _modelRebuilder;

            /// <summary>
            /// 当前阶段使用的检测器工厂。
            /// </summary>
            private readonly ILargeModelDinov2DetectorFactory _detectorFactory;

            /// <summary>
            /// 当前阶段使用的模板发布器。
            /// </summary>
            private readonly ILargeModelTemplatePublisher _templatePublisher;

            /// <summary>
            /// 大模型运行环境目录。
            /// </summary>
            private readonly string _runtimeRoot;

            /// <summary>
            /// 当前训练请求。
            /// </summary>
            private readonly LargeModelTrainingRequest _request;

            /// <summary>
            /// 训练进度回调。
            /// </summary>
            private readonly IProgress<int> _progress;

            /// <summary>
            /// 训练日志回调。
            /// </summary>
            private readonly IProgress<string> _log;

            /// <summary>
            /// 当前模板受控训练工作目录。
            /// </summary>
            private readonly string _workRoot;

            /// <summary>
            /// 固定可信训练根目录，所有递归清理目标必须严格位于其下。
            /// </summary>
            private readonly string _trustedTrainingRoot;

            /// <summary>
            /// 当前训练数据集目录。
            /// </summary>
            private readonly string _datasetRoot;

            /// <summary>
            /// 当前 memory bank 输出目录。
            /// </summary>
            private readonly string _modelOutputRoot;

            /// <summary>
            /// 当前 memory bank 输出路径。
            /// </summary>
            private readonly string _bankPath;

            /// <summary>
            /// 当前训练使用的 native 设备模式。
            /// </summary>
            private readonly int _deviceMode;

            /// <summary>
            /// 已完成的数据集构建结果。
            /// </summary>
            private DatasetBuildResult _dataset;

            /// <summary>
            /// 自动校准后的图像级识别分数。
            /// </summary>
            private float _calibratedImageThreshold;

            /// <summary>
            /// 初始化生产训练阶段适配器，不在构造期间修改共享目录。
            /// </summary>
            /// <param name="owner">训练服务。</param>
            /// <param name="modelRebuilder">模型重建器。</param>
            /// <param name="detectorFactory">检测器工厂。</param>
            /// <param name="templatePublisher">模板发布器。</param>
            /// <param name="runtimeRoot">大模型运行环境目录。</param>
            /// <param name="modelRoot">模板输出根目录。</param>
            /// <param name="request">训练请求。</param>
            /// <param name="progress">训练进度回调。</param>
            /// <param name="log">训练日志回调。</param>
            public ProductionTrainingStages(
                LargeModelTrainingService owner,
                ILargeModelTrainingModelRebuilder modelRebuilder,
                ILargeModelDinov2DetectorFactory detectorFactory,
                ILargeModelTemplatePublisher templatePublisher,
                string runtimeRoot,
                string modelRoot,
                LargeModelTrainingRequest request,
                IProgress<int> progress,
                IProgress<string> log)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                _modelRebuilder = modelRebuilder ?? throw new ArgumentNullException(nameof(modelRebuilder));
                _detectorFactory = detectorFactory ?? throw new ArgumentNullException(nameof(detectorFactory));
                _templatePublisher = templatePublisher ?? throw new ArgumentNullException(nameof(templatePublisher));
                _runtimeRoot = runtimeRoot;
                _request = request;
                _progress = progress;
                _log = log;
                _workRoot = ResolveTrainingWorkRoot(modelRoot, request.TemplateName, out _trustedTrainingRoot);
                _datasetRoot = Path.Combine(_workRoot, "dataset");
                _modelOutputRoot = Path.Combine(_workRoot, "model");
                _bankPath = Path.Combine(_modelOutputRoot, "memory.bank");
                _deviceMode = LargeModelRuntimeBootstrapper.ResolveDeviceMode(request.Device);
            }

            /// <summary>
            /// 在独占训练锁内清理并构建本次训练数据集。
            /// </summary>
            /// <param name="token">取消令牌。</param>
            public void PrepareDataset(CancellationToken token)
            {
                PrepareCleanDirectory(_datasetRoot, _trustedTrainingRoot);
                PrepareCleanDirectory(_modelOutputRoot, _trustedTrainingRoot);
                _log?.Report("正在准备大模型训练数据集。");
                _dataset = BuildDataset(_request, _datasetRoot, token);
                _log?.Report(
                    "数据集准备完成，OK=" + _dataset.OkCount.ToString(CultureInfo.InvariantCulture)
                    + "，NG=" + _dataset.NgCount.ToString(CultureInfo.InvariantCulture));
                ValidateDatasetImageCount(_dataset);
                token.ThrowIfCancellationRequested();
                _progress?.Report(5);
            }

            /// <summary>
            /// 为本次训练重新生成并发布 ONNX。
            /// </summary>
            /// <param name="token">取消令牌。</param>
            /// <returns>本次新 ONNX 正式路径。</returns>
            public string RebuildOnnxModel(CancellationToken token)
            {
                return _modelRebuilder.RebuildOnnxModel(
                    _runtimeRoot,
                    _request,
                    _log,
                    token);
            }

            /// <summary>
            /// 使用本次 ONNX 重新生成并发布 TensorRT Engine。
            /// </summary>
            /// <param name="onnxPath">本次新 ONNX 正式路径。</param>
            /// <param name="token">取消令牌。</param>
            /// <returns>本次新 Engine 正式路径。</returns>
            public string RebuildTensorRtEngine(string onnxPath, CancellationToken token)
            {
                return _modelRebuilder.RebuildTensorRtEngine(
                    _runtimeRoot,
                    onnxPath,
                    _request,
                    _log,
                    token);
            }

            /// <summary>
            /// 使用本次已验证模型重新训练 memory bank。
            /// </summary>
            /// <param name="modelPath">本次新 ONNX 或 Engine 正式路径。</param>
            /// <param name="token">取消令牌。</param>
            public void TrainMemoryBank(string modelPath, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                using (LargeModelDinov2Detector detector = _detectorFactory.Create(_runtimeRoot))
                {
                    _owner.SetActiveDetector(detector);
                    try
                    {
                        using (token.Register(detector.Dispose))
                        {
                            token.ThrowIfCancellationRequested();
                            _log?.Report("正在训练 DINOv2 memory bank。");
                            int trained = detector.TrainMemoryBank(
                                modelPath,
                                _bankPath,
                                _deviceMode,
                                _dataset.OkPath,
                                _dataset.NgCount > 0 ? _dataset.NgPath : string.Empty,
                                token);
                            if (trained < 0)
                                throw new InvalidOperationException("大模型训练失败，native 返回码：" + trained.ToString(CultureInfo.InvariantCulture));
                            if (!File.Exists(_bankPath))
                                throw new FileNotFoundException("大模型训练未生成 memory bank。", _bankPath);

                            _log?.Report("memory bank 训练完成，处理图片数：" + trained.ToString(CultureInfo.InvariantCulture));
                            token.ThrowIfCancellationRequested();
                            if (HasCalibrationImages(_dataset))
                            {
                                try
                                {
                                    int loadRet = detector.LoadModel(modelPath, _bankPath, _deviceMode);
                                    if (loadRet != 0)
                                    {
                                        throw new InvalidOperationException(
                                            "native 返回码：" + loadRet.ToString(CultureInfo.InvariantCulture));
                                    }

                                    _calibratedImageThreshold = CalibrateImageThreshold(detector, _dataset, _request, _log, token);
                                }
                                catch (OperationCanceledException)
                                {
                                    throw;
                                }
                                catch (Exception ex)
                                {
                                    _log?.Report(
                                        "大模型自动校准前加载 memory bank 失败，已使用默认图像阈值 "
                                        + _request.ImageThreshold.ToString("0.######", CultureInfo.InvariantCulture)
                                        + "，不影响模型生成。原因："
                                        + ex.Message);
                                    _calibratedImageThreshold = _request.ImageThreshold;
                                }
                            }
                            else
                            {
                                _calibratedImageThreshold = CalibrateImageThreshold(detector, _dataset, _request, _log, token);
                            }
                        }
                    }
                    finally
                    {
                        _owner.SetActiveDetector(null);
                    }
                }
            }

            /// <summary>
            /// 把本次模型、Bank 和参数清单打包为模板。
            /// </summary>
            /// <param name="modelPath">本次新 ONNX 或 Engine 正式路径。</param>
            /// <param name="token">取消令牌。</param>
            public void PackageTemplate(string modelPath, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                _progress?.Report(90);
                _log?.Report("正在打包大模型模板文件。");
                LargeModelTemplateManifest manifest = BuildManifest(_request, _dataset, modelPath, _deviceMode, _calibratedImageThreshold);
                _templatePublisher.Publish(
                    _request.OutputTemplatePath,
                    manifest,
                    modelPath,
                    _bankPath,
                    _log,
                    token);
                _progress?.Report(100);
            }

            /// <summary>
            /// 根据已经完成的受保护训练链创建服务返回结果。
            /// </summary>
            /// <param name="modelPath">协调器返回的本次新模型路径。</param>
            /// <returns>大模型训练结果。</returns>
            public LargeModelTrainingResult CreateResult(string modelPath)
            {
                if (string.IsNullOrWhiteSpace(modelPath) || _dataset == null)
                    throw new InvalidOperationException("大模型训练链未完整结束，无法创建训练结果。");

                return new LargeModelTrainingResult
                {
                    TemplatePath = _request.OutputTemplatePath,
                    BankPath = _bankPath,
                    OkCount = _dataset.OkCount,
                    NgCount = _dataset.NgCount
                };
            }
        }

        /// <summary>
        /// 数据集构建结果。
        /// </summary>
        private sealed class DatasetBuildResult
        {
            /// <summary>
            /// 初始化数据集构建结果。
            /// </summary>
            /// <param name="okPath">OK 数据集路径。</param>
            /// <param name="ngPath">NG 数据集路径。</param>
            /// <param name="okCount">OK 数量。</param>
            /// <param name="ngCount">NG 数量。</param>
            public DatasetBuildResult(string okPath, string ngPath, int okCount, int ngCount, string calibrationOkImagePath, string calibrationNgImagePath)
            {
                OkPath = okPath;
                NgPath = ngPath;
                OkCount = okCount;
                NgCount = ngCount;
                CalibrationOkImagePath = calibrationOkImagePath;
                CalibrationNgImagePath = calibrationNgImagePath;
            }

            /// <summary>
            /// OK 数据集路径。
            /// </summary>
            public string OkPath { get; private set; }

            /// <summary>
            /// NG 数据集路径。
            /// </summary>
            public string NgPath { get; private set; }

            /// <summary>
            /// OK 数量。
            /// </summary>
            public int OkCount { get; private set; }

            /// <summary>
            /// NG 数量。
            /// </summary>
            public int NgCount { get; private set; }

            /// <summary>
            /// 预留且不参与训练的 OK 校准图路径。
            /// </summary>
            public string CalibrationOkImagePath { get; private set; }

            /// <summary>
            /// 预留且不参与训练的 NG 校准图路径。
            /// </summary>
            public string CalibrationNgImagePath { get; private set; }
        }
    }
}
