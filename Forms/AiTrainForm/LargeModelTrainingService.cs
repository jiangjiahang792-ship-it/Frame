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
    /// 大模型训练服务，负责数据集准备、DINOv2 bank 训练和模板打包。
    /// </summary>
    public sealed class LargeModelTrainingService
    {
        /// <summary>
        /// DINOv2 ViT 的 patch 尺寸，导出分辨率必须按该尺寸对齐。
        /// </summary>
        private const int Dinov2PatchSize = 14;

        /// <summary>
        /// 当前训练使用的 native 检测器。
        /// </summary>
        private LargeModelDinov2Detector _activeDetector;

        /// <summary>
        /// 当前训练检测器访问锁。
        /// </summary>
        private readonly object _activeDetectorLock = new object();

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
            LargeModelRuntimeStatus runtime = LargeModelRuntimeBootstrapper.ValidateRuntime();
            if (!runtime.IsReady)
                throw new InvalidOperationException(runtime.Message);

            string modelRoot = LargeModelRuntimeBootstrapper.EnsureModelRoot();
            EnsureTemplatePathInModelRoot(request.OutputTemplatePath, modelRoot);

            string workRoot = Path.Combine(modelRoot, "_LargeModelTraining", SafeFileName(request.TemplateName));
            string datasetRoot = Path.Combine(workRoot, "dataset");
            string modelOutputRoot = Path.Combine(workRoot, "model");
            PrepareCleanDirectory(datasetRoot, workRoot);
            PrepareCleanDirectory(modelOutputRoot, workRoot);

            log?.Report("正在准备大模型训练数据集。");
            DatasetBuildResult dataset = BuildDataset(request, datasetRoot, token);
            log?.Report("数据集准备完成，OK=" + dataset.OkCount.ToString(CultureInfo.InvariantCulture) + "，NG=" + dataset.NgCount.ToString(CultureInfo.InvariantCulture));
            ValidateDatasetImageCount(dataset);

            token.ThrowIfCancellationRequested();
            progress?.Report(5);

            string modelPath = RebuildTrainingModelPath(runtime.RootPath, request, log, token);
            string bankPath = Path.Combine(modelOutputRoot, "memory.bank");
            int deviceMode = LargeModelRuntimeBootstrapper.ResolveDeviceMode(request.Device);
            using (var detector = new LargeModelDinov2Detector(runtime.RootPath))
            {
                SetActiveDetector(detector);
                try
                {
                    log?.Report("正在训练 DINOv2 memory bank。");
                    int trained = detector.TrainMemoryBank(modelPath, bankPath, deviceMode, dataset.OkPath, dataset.NgCount > 0 ? dataset.NgPath : string.Empty);
                    if (trained < 0)
                        throw new InvalidOperationException("大模型训练失败，native 返回码：" + trained.ToString(CultureInfo.InvariantCulture));
                    if (!File.Exists(bankPath))
                        throw new FileNotFoundException("大模型训练未生成 memory bank。", bankPath);

                    log?.Report("memory bank 训练完成，处理图片数：" + trained.ToString(CultureInfo.InvariantCulture));
                }
                finally
                {
                    SetActiveDetector(null);
                }
            }

            token.ThrowIfCancellationRequested();
            progress?.Report(90);

            log?.Report("正在打包大模型模板文件。");
            LargeModelTemplateManifest manifest = BuildManifest(request, dataset, modelPath, deviceMode);
            LargeModelTemplatePackage.Create(request.OutputTemplatePath, manifest, modelPath, bankPath);
            progress?.Report(100);

            return new LargeModelTrainingResult
            {
                TemplatePath = request.OutputTemplatePath,
                BankPath = bankPath,
                OkCount = dataset.OkCount,
                NgCount = dataset.NgCount
            };
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
            if (string.IsNullOrWhiteSpace(request.OutputTemplatePath))
                throw new InvalidOperationException("模板输出路径不能为空。");
            if (request.Images == null || request.Images.Count == 0)
                throw new InvalidOperationException("请先加载训练图片。");
            if (!request.Images.Any(item => item.Category == LargeModelImageCategory.OK))
                throw new InvalidOperationException("大模型训练至少需要 OK 图片。");
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
        /// 为本次训练重新生成与所选设备、分辨率和精度匹配的模型。
        /// </summary>
        /// <param name="runtimeRoot">大模型运行环境目录。</param>
        /// <param name="request">训练请求。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>本次训练实际使用的新 ONNX 或 TensorRT Engine 路径。</returns>
        private static string RebuildTrainingModelPath(string runtimeRoot, LargeModelTrainingRequest request, IProgress<string> log, CancellationToken token)
        {
            string root = LargeModelDinov2Native.ResolveRuntimeRoot(runtimeRoot);
            string sizeText = BuildSizeText(request.InputWidth, request.InputHeight);
            string normalizedDevice = LargeModelRuntimeBootstrapper.NormalizeDevice(request.Device);
            string normalizedPrecision = LargeModelRuntimeBootstrapper.NormalizePrecision(request.Precision);
            string onnxPath = RebuildOnnxModelPath(root, request.InputWidth, request.InputHeight, sizeText, log, token);

            if (string.Equals(normalizedDevice, "CPU", StringComparison.OrdinalIgnoreCase))
            {
                return onnxPath;
            }

            string enginePath = Path.Combine(root, "dinov2_" + sizeText + "_" + normalizedPrecision + ".engine");
            string trtexecPath = Path.Combine(root, "trtexec.exe");
            if (!File.Exists(trtexecPath))
                throw new FileNotFoundException("LargeModelDll 缺少 trtexec.exe，无法从 ONNX 生成 TensorRT Engine。", trtexecPath);

            log?.Report("开始使用当前电脑显卡重新生成 TensorRT Engine：" + Path.GetFileName(enginePath));
            BuildTensorRtEngine(root, trtexecPath, onnxPath, enginePath, normalizedPrecision, log, token);
            return enginePath;
        }

        /// <summary>
        /// 为本次训练重新导出所选分辨率的 ONNX，并在成功后替换正式模型。
        /// </summary>
        /// <param name="root">大模型运行环境目录。</param>
        /// <param name="width">输入宽度。</param>
        /// <param name="height">输入高度。</param>
        /// <param name="sizeText">分辨率文本。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>本次训练新生成的 ONNX 完整路径。</returns>
        private static string RebuildOnnxModelPath(string root, int width, int height, string sizeText, IProgress<string> log, CancellationToken token)
        {
            string onnxPath = Path.Combine(root, "dinov2_" + sizeText + ".onnx");
            string tempOnnxPath = Path.Combine(root, Path.GetFileNameWithoutExtension(onnxPath) + ".building.onnx");
            TryDeleteGeneratedFile(tempOnnxPath);

            try
            {
                log?.Report("开始为本次训练重新导出 DINOv2 ONNX：" + Path.GetFileName(onnxPath));
                ExportOnnxModel(root, tempOnnxPath, width, height, sizeText, log, token);
                if (!File.Exists(tempOnnxPath))
                    throw new FileNotFoundException("DINOv2 ONNX 导出完成后仍未找到临时模型文件：" + tempOnnxPath, tempOnnxPath);

                ReplaceGeneratedFile(tempOnnxPath, onnxPath);
                log?.Report("本次训练 ONNX 已更新：" + onnxPath);
                return onnxPath;
            }
            catch
            {
                TryDeleteGeneratedFile(tempOnnxPath);
                throw;
            }
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
                    "LargeModelDll 缺少 ONNX 导出环境，无法生成 dinov2_" + sizeText + ".onnx。请将 export_dinov2.py、dinov2 源码目录和 third_party\\python_native_env 放入 LargeModelDll，或手动放入 dinov2_" + sizeText + ".onnx。",
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
            StringBuilder processOutput = new StringBuilder();

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
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                while (!process.WaitForExit(500))
                {
                    if (token.IsCancellationRequested)
                    {
                        TryKillProcess(process);
                        token.ThrowIfCancellationRequested();
                    }
                }

                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("DINOv2 ONNX 导出失败，python 返回码：" + process.ExitCode.ToString(CultureInfo.InvariantCulture) + "\r\n" + processOutput);
            }

            log?.Report("DINOv2 ONNX 导出完成：" + onnxPath);
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
        /// 使用同目录生成完成的临时文件替换正式模型文件。
        /// </summary>
        /// <param name="temporaryPath">已经完整生成的临时文件路径。</param>
        /// <param name="targetPath">正式文件路径。</param>
        private static void ReplaceGeneratedFile(string temporaryPath, string targetPath)
        {
            if (File.Exists(targetPath))
            {
                // 同目录替换由文件系统原子完成；替换失败时保留原正式文件。
                File.Replace(temporaryPath, targetPath, null, true);
                return;
            }

            File.Move(temporaryPath, targetPath);
        }

        /// <summary>
        /// 尽力清理模型生成过程中遗留的临时文件。
        /// </summary>
        /// <param name="path">待清理的受控临时文件路径。</param>
        private static void TryDeleteGeneratedFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // 临时文件清理失败不能覆盖真正的 ONNX 或 Engine 生成异常。
            }
        }

        /// <summary>
        /// 调用 trtexec 从 ONNX 生成目标分辨率 TensorRT engine。
        /// </summary>
        /// <param name="runtimeRoot">大模型运行环境目录。</param>
        /// <param name="trtexecPath">trtexec 路径。</param>
        /// <param name="onnxPath">ONNX 模型路径。</param>
        /// <param name="enginePath">engine 输出路径。</param>
        /// <param name="precision">fp16 或 fp32。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        private static void BuildTensorRtEngine(string runtimeRoot, string trtexecPath, string onnxPath, string enginePath, string precision, IProgress<string> log, CancellationToken token)
        {
            string tempEnginePath = Path.Combine(Path.GetDirectoryName(enginePath), Path.GetFileNameWithoutExtension(enginePath) + ".building.engine");
            TryDeleteGeneratedFile(tempEnginePath);

            try
            {
                string precisionArgs = string.Equals(precision, "fp16", StringComparison.OrdinalIgnoreCase) ? " --fp16" : " --noTF32";
                string arguments = "--onnx=\"" + onnxPath + "\" --saveEngine=\"" + tempEnginePath + "\"" + precisionArgs + " --workspace=4096";
                StringBuilder processOutput = new StringBuilder();

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

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    while (!process.WaitForExit(500))
                    {
                        if (token.IsCancellationRequested)
                        {
                            TryKillProcess(process);
                            token.ThrowIfCancellationRequested();
                        }
                    }

                    process.WaitForExit();
                    if (process.ExitCode != 0)
                        throw new InvalidOperationException("TensorRT Engine 生成失败，trtexec 返回码：" + process.ExitCode.ToString(CultureInfo.InvariantCulture) + "\r\n" + processOutput);
                }

                if (!File.Exists(tempEnginePath))
                    throw new FileNotFoundException("TensorRT 未生成临时 Engine 文件。", tempEnginePath);

                ReplaceGeneratedFile(tempEnginePath, enginePath);
                log?.Report("TensorRT Engine 重新生成完成：" + enginePath);
            }
            catch
            {
                TryDeleteGeneratedFile(tempEnginePath);
                throw;
            }
        }

        /// <summary>
        /// 收集 trtexec 输出并写入训练日志。
        /// </summary>
        /// <param name="builder">输出缓存。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="line">输出行。</param>
        private static void AppendProcessLog(StringBuilder builder, IProgress<string> log, string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            builder.AppendLine(line);
            log?.Report(line);
        }

        /// <summary>
        /// 尝试终止 trtexec 进程。
        /// </summary>
        /// <param name="process">进程实例。</param>
        private static void TryKillProcess(System.Diagnostics.Process process)
        {
            try
            {
                if (process != null && !process.HasExited)
                    process.Kill();
            }
            catch
            {
                // 取消时尽力终止 trtexec；失败时交给外层取消流程继续退出。
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
            if (!fullTarget.StartsWith(fullAllowed, StringComparison.OrdinalIgnoreCase))
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
            Directory.CreateDirectory(okPath);
            Directory.CreateDirectory(ngPath);

            int okCount = 0;
            int ngCount = 0;
            int index = 0;
            foreach (LargeModelImageItem item in request.Images)
            {
                token.ThrowIfCancellationRequested();
                string targetFolder = item.Category == LargeModelImageCategory.NG ? ngPath : okPath;
                bool saved = request.RoiRect.HasValue
                    ? CropImageToRoi(item.FilePath, targetFolder, request.RoiRect.Value, index)
                    : CopyImageToDataset(item.FilePath, targetFolder, index);

                if (saved)
                {
                    if (item.Category == LargeModelImageCategory.NG) ngCount++;
                    else okCount++;
                }

                index++;
            }

            return new DatasetBuildResult(okPath, ngPath, okCount, ngCount);
        }

        /// <summary>
        /// 复制整图到训练数据集；无 ROI 时使用该路径。
        /// </summary>
        /// <param name="sourceFile">源图路径。</param>
        /// <param name="targetFolder">目标目录。</param>
        /// <param name="index">图片序号。</param>
        /// <returns>保存成功返回 true。</returns>
        private static bool CopyImageToDataset(string sourceFile, string targetFolder, int index)
        {
            if (!File.Exists(sourceFile)) return false;

            string extension = Path.GetExtension(sourceFile);
            if (string.IsNullOrWhiteSpace(extension)) extension = ".png";
            string targetFile = Path.Combine(targetFolder, BuildDatasetFileName(sourceFile, index, extension));
            File.Copy(sourceFile, targetFile, true);
            return true;
        }

        /// <summary>
        /// 裁剪 ROI 到训练数据集。
        /// </summary>
        /// <param name="sourceFile">源图路径。</param>
        /// <param name="targetFolder">目标目录。</param>
        /// <param name="roi">图像坐标系 ROI。</param>
        /// <param name="index">图片序号。</param>
        /// <returns>保存成功返回 true。</returns>
        private static bool CropImageToRoi(string sourceFile, string targetFolder, CvRect roi, int index)
        {
            if (!File.Exists(sourceFile)) return false;

            using (Mat image = Cv2.ImRead(sourceFile))
            {
                if (image.Empty()) return false;

                CvRect rect = ClampRect(roi, image.Width, image.Height);
                if (rect.Width <= 0 || rect.Height <= 0) return false;

                using (Mat crop = new Mat(image, rect).Clone())
                {
                    string targetFile = Path.Combine(targetFolder, BuildDatasetFileName(sourceFile, index, ".png"));
                    return Cv2.ImWrite(targetFile, crop);
                }
            }
        }

        /// <summary>
        /// 构建模板清单。
        /// </summary>
        /// <param name="request">训练请求。</param>
        /// <param name="dataset">数据集结果。</param>
        /// <param name="modelPath">实际使用的 native 模型路径。</param>
        /// <param name="deviceMode">native 设备模式。</param>
        /// <returns>模板清单。</returns>
        private static LargeModelTemplateManifest BuildManifest(LargeModelTrainingRequest request, DatasetBuildResult dataset, string modelPath, int deviceMode)
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
                ImageThreshold = request.ImageThreshold,
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
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(c, '_');
            }

            return value;
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
            public DatasetBuildResult(string okPath, string ngPath, int okCount, int ngCount)
            {
                OkPath = okPath;
                NgPath = ngPath;
                OkCount = okCount;
                NgCount = ngCount;
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
        }
    }
}
