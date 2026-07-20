using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using Logger;
using OpenCvSharp;
using TDJS_Vision.Forms.AiTrainForm;
using CvRect = OpenCvSharp.Rect;
using DrawingRect = System.Drawing.Rectangle;

namespace TDJS_Vision.Node._3_Detection.LargeModel
{
    /// <summary>
    /// 大模型调用节点的模板加载与推理运行适配器。
    /// </summary>
    internal sealed class LargeModelDetectionRuntime : IDisposable
    {
        /// <summary>
        /// native 检测器访问锁，避免后台流程并发重入导致句柄状态错乱。
        /// </summary>
        private readonly object _syncRoot = new object();

        /// <summary>
        /// 当前已加载模板路径。
        /// </summary>
        private string _loadedTemplatePath;

        /// <summary>
        /// 当前已加载模板最后写入时间。
        /// </summary>
        private long _loadedTemplateTicks;

        /// <summary>
        /// 当前模型实际使用的推理设备。
        /// </summary>
        private string _loadedDevice;

        /// <summary>
        /// 当前实际加载的模型文件路径。
        /// </summary>
        private string _loadedModelPath;

        /// <summary>
        /// 当前模板解包结果。
        /// </summary>
        private LargeModelTemplateExtractResult _extractResult;

        /// <summary>
        /// 当前 native 检测器。
        /// </summary>
        private LargeModelDinov2Detector _detector;

        /// <summary>
        /// 执行一次大模型调用。
        /// </summary>
        /// <param name="sourceImage">流程输入图像。</param>
        /// <param name="param">节点参数。</param>
        /// <returns>大模型调用结果。</returns>
        public LargeModelDetectionRunResult Infer(Mat sourceImage, NodeParamLargeModelDetection param)
        {
            if (sourceImage == null || sourceImage.Empty())
                throw new InvalidOperationException("大模型调用输入图像为空。");
            if (param == null)
                throw new ArgumentNullException(nameof(param));

            lock (_syncRoot)
            {
                EnsureModelLoaded(param);
                LargeModelTemplateManifest manifest = _extractResult.Manifest;
                CvRect? roiRect = null;
                Stopwatch imageTimer = Stopwatch.StartNew();
                Mat inferImage = BuildInferenceImage(sourceImage, manifest, out roiRect);
                imageTimer.Stop();
                using (inferImage)
                {
                    int maxBoxes = param.MaxBoxes <= 0 ? NodeParamLargeModelDetection.DefaultMaxBoxes : param.MaxBoxes;
                    float imageThreshold = ResolveImageThreshold(param, manifest);
                    int areaThreshold = ResolveAreaThreshold(param, manifest);
                    long detectorPreparationMilliseconds;
                    long nativeInferenceMilliseconds;
                    Stopwatch inferenceCallTimer = Stopwatch.StartNew();
                    LargeModelDinov2Result nativeResult = _detector.InferBgr(
                        inferImage,
                        imageThreshold,
                        areaThreshold,
                        maxBoxes,
                        out detectorPreparationMilliseconds,
                        out nativeInferenceMilliseconds);
                    inferenceCallTimer.Stop();
                    if (nativeResult.ReturnCode < 0)
                        throw new InvalidOperationException("大模型推理失败，native 返回码：" + nativeResult.ReturnCode.ToString(CultureInfo.InvariantCulture));

                    Stopwatch postprocessTimer = Stopwatch.StartNew();
                    List<DrawingRect> boxes = OffsetBoxesToSourceImage(nativeResult.Boxes, roiRect, sourceImage.Width, sourceImage.Height);
                    bool isOk = nativeResult.IsOk;
                    string message = BuildResultMessage(nativeResult, boxes.Count);
                    postprocessTimer.Stop();

                    long wrapperPostprocessMilliseconds = Math.Max(
                        0L,
                        inferenceCallTimer.ElapsedMilliseconds - detectorPreparationMilliseconds - nativeInferenceMilliseconds);
                    return new LargeModelDetectionRunResult(
                        nativeResult,
                        manifest,
                        roiRect,
                        boxes,
                        isOk,
                        message,
                        _loadedDevice,
                        _loadedModelPath,
                        sourceImage.Width,
                        sourceImage.Height,
                        imageTimer.ElapsedMilliseconds + detectorPreparationMilliseconds,
                        nativeInferenceMilliseconds,
                        postprocessTimer.ElapsedMilliseconds + wrapperPostprocessMilliseconds);
                }
            }
        }

        /// <summary>
        /// 预先加载并预热指定模板，使流程运行阶段直接进入推理。
        /// </summary>
        /// <param name="param">待预加载的节点参数。</param>
        public void Preload(NodeParamLargeModelDetection param)
        {
            if (param == null)
                throw new ArgumentNullException(nameof(param));

            lock (_syncRoot)
            {
                EnsureModelLoaded(param, true);
            }
        }

        /// <summary>
        /// 释放 native 模型句柄。
        /// </summary>
        public void Dispose()
        {
            lock (_syncRoot)
            {
                ReleaseDetector();
            }
        }

        /// <summary>
        /// 确保指定模板对应模型已经加载。
        /// </summary>
        /// <param name="param">节点参数。</param>
        private void EnsureModelLoaded(NodeParamLargeModelDetection param)
        {
            // 保存成功后的正常运行走纯内存快速路径，不再每帧访问模板文件时间戳。
            if (IsLoadedTemplateMatchedFast(param))
                return;

            EnsureModelLoaded(param, false);
        }

        /// <summary>
        /// 仅使用内存状态判断流程当前参数是否已经对应已加载模型。
        /// </summary>
        /// <param name="param">节点参数。</param>
        /// <returns>当前检测器与模板路径一致时返回 true。</returns>
        private bool IsLoadedTemplateMatchedFast(NodeParamLargeModelDetection param)
        {
            return param != null &&
                _detector != null &&
                _extractResult != null &&
                !string.IsNullOrWhiteSpace(param.TemplatePath) &&
                string.Equals(_loadedTemplatePath, param.TemplatePath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 确保指定模板对应模型已经加载，并按需执行首次推理预热。
        /// </summary>
        /// <param name="param">节点参数。</param>
        /// <param name="warmup">加载新模型后是否执行一次预热推理。</param>
        private void EnsureModelLoaded(NodeParamLargeModelDetection param, bool warmup)
        {
            string templatePath = ResolveTemplatePath(param.TemplatePath);
            long templateTicks = File.GetLastWriteTimeUtc(templatePath).Ticks;
            if (_detector != null &&
                string.Equals(_loadedTemplatePath, templatePath, StringComparison.OrdinalIgnoreCase) &&
                _loadedTemplateTicks == templateTicks &&
                _extractResult != null)
            {
                return;
            }

            LargeModelRuntimeStatus runtime = LargeModelRuntimeBootstrapper.ValidateRuntime();
            if (!runtime.IsReady)
                throw new InvalidOperationException(runtime.Message);

            string extractRoot = Path.Combine(LargeModelRuntimeBootstrapper.EnsureModelRoot(), "_LargeModelInference");
            Directory.CreateDirectory(extractRoot);
            LargeModelTemplateExtractResult extractResult = LargeModelTemplatePackage.Extract(templatePath, extractRoot);

            LoadModelResult loadResult = null;
            bool published = false;
            try
            {
                loadResult = LoadModelWithFallback(runtime.RootPath, extractResult);
                if (warmup)
                    WarmupDetector(loadResult.Detector, param, extractResult.Manifest);

                SwapLoadedDetector(
                    loadResult.Detector,
                    extractResult,
                    templatePath,
                    templateTicks,
                    FormatDeviceMode(loadResult.DeviceMode, loadResult.ModelPath),
                    loadResult.ModelPath);
                published = true;
                LogLoadedRuntime();
            }
            finally
            {
                if (!published && loadResult != null && loadResult.Detector != null)
                    loadResult.Detector.Dispose();
            }
        }

        /// <summary>
        /// 使用模板输入尺寸执行一次空图推理，提前完成 CUDA/TensorRT 首次初始化。
        /// </summary>
        /// <param name="detector">已经完成 native 初始化的新检测器。</param>
        /// <param name="param">节点推理参数。</param>
        /// <param name="manifest">模板清单。</param>
        private static void WarmupDetector(
            LargeModelDinov2Detector detector,
            NodeParamLargeModelDetection param,
            LargeModelTemplateManifest manifest)
        {
            if (detector == null)
                throw new ArgumentNullException(nameof(detector));
            if (manifest == null)
                throw new InvalidDataException("大模型模板清单为空，无法预热模型。");
            if (manifest.InputWidth <= 0 || manifest.InputHeight <= 0)
                throw new InvalidDataException("大模型模板输入尺寸无效，无法预热模型。");

            using (var warmupImage = new Mat(manifest.InputHeight, manifest.InputWidth, MatType.CV_8UC3, Scalar.Black))
            {
                // 预热只用于触发 GPU/CUDA/TensorRT 初始化，限制一个框可避免按用户配置分配不必要的大数组。
                LargeModelDinov2Result result = detector.InferBgr(
                    warmupImage,
                    ResolveImageThreshold(param, manifest),
                    ResolveAreaThreshold(param, manifest),
                    1);
                if (result.ReturnCode < 0)
                {
                    throw new InvalidOperationException(
                        "大模型预热失败，native 返回码：" + result.ReturnCode.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        /// <summary>
        /// 发布已经加载并预热成功的新模型，然后释放旧模型。
        /// </summary>
        /// <param name="detector">新检测器。</param>
        /// <param name="extractResult">新模板解包结果。</param>
        /// <param name="templatePath">新模板完整路径。</param>
        /// <param name="templateTicks">新模板最后写入时间。</param>
        /// <param name="device">实际加载成功的推理设备。</param>
        /// <param name="modelPath">实际加载的模型文件路径。</param>
        private void SwapLoadedDetector(
            LargeModelDinov2Detector detector,
            LargeModelTemplateExtractResult extractResult,
            string templatePath,
            long templateTicks,
            string device,
            string modelPath)
        {
            LargeModelDinov2Detector oldDetector = _detector;
            _detector = detector;
            _extractResult = extractResult;
            _loadedTemplatePath = templatePath;
            _loadedTemplateTicks = templateTicks;
            _loadedDevice = device;
            _loadedModelPath = modelPath;

            if (oldDetector == null)
                return;

            try
            {
                oldDetector.Dispose();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("释放旧大模型检测器失败：" + ex.Message);
            }
        }

        /// <summary>
        /// 将 native 设备模式转换为用户可读文本。
        /// </summary>
        /// <param name="deviceMode">0 自动、1 GPU、2 CPU。</param>
        /// <param name="modelPath">实际加载的模型路径。</param>
        /// <returns>实际设备文本。</returns>
        private static string FormatDeviceMode(int deviceMode, string modelPath)
        {
            if (deviceMode == 1)
            {
                return string.Equals(Path.GetExtension(modelPath), ".engine", StringComparison.OrdinalIgnoreCase)
                    ? "GPU_TENSORRT"
                    : "GPU";
            }
            if (deviceMode == 2)
                return "CPU_ONNX";

            return "AUTO";
        }

        /// <summary>
        /// 将模型加载后的真实设备和模型路径写入统一运行日志。
        /// </summary>
        private void LogLoadedRuntime()
        {
            LogHelper.AddLog(
                MsgLevel.Info,
                "大模型运行模型加载成功，实际设备=" + (_loadedDevice ?? "未知") +
                "，实际模型=" + (_loadedModelPath ?? "未知"),
                true);
        }

        /// <summary>
        /// 解析本次推理使用的图像阈值。
        /// </summary>
        /// <param name="param">节点参数。</param>
        /// <param name="manifest">模板清单。</param>
        /// <returns>有效图像阈值。</returns>
        private static float ResolveImageThreshold(NodeParamLargeModelDetection param, LargeModelTemplateManifest manifest)
        {
            if (param != null && param.ImageThreshold > 0F)
                return param.ImageThreshold;
            if (manifest != null && manifest.ImageThreshold > 0F)
                return manifest.ImageThreshold;

            return NodeParamLargeModelDetection.DefaultImageThreshold;
        }

        /// <summary>
        /// 解析本次推理使用的面积阈值。
        /// </summary>
        /// <param name="param">节点参数。</param>
        /// <param name="manifest">模板清单。</param>
        /// <returns>有效面积阈值。</returns>
        private static int ResolveAreaThreshold(NodeParamLargeModelDetection param, LargeModelTemplateManifest manifest)
        {
            if (param != null && param.AreaThreshold >= 0)
                return param.AreaThreshold;
            if (manifest != null && manifest.AreaThreshold >= 0)
                return manifest.AreaThreshold;

            return NodeParamLargeModelDetection.DefaultAreaThreshold;
        }

        /// <summary>
        /// 加载大模型模板模型；只有 AUTO 初始化失败时允许按模板尺寸尝试 CPU ONNX 回退。
        /// </summary>
        /// <param name="runtimeRoot">大模型运行环境目录。</param>
        /// <param name="extractResult">模板解包结果。</param>
        /// <returns>加载成功的检测器结果。</returns>
        private static LoadModelResult LoadModelWithFallback(string runtimeRoot, LargeModelTemplateExtractResult extractResult)
        {
            int firstRet;
            string firstModelPath = ResolvePrimaryModelPath(runtimeRoot, extractResult);
            int firstDeviceMode = ResolveManifestDeviceMode(extractResult.Manifest);
            LargeModelDinov2Detector detector = LoadModelOnce(runtimeRoot, firstModelPath, extractResult.BankPath, firstDeviceMode, out firstRet);
            if (firstRet == 0)
                return new LoadModelResult(detector, firstDeviceMode, firstModelPath);

            detector.Dispose();
            if (!ShouldRetryLoadModelOnCpu(firstDeviceMode))
                throw new InvalidOperationException(BuildLoadModelFailureMessage(firstModelPath, firstDeviceMode, firstRet, null));

            int cpuRet;
            string cpuModelPath = LargeModelRuntimeBootstrapper.ResolveModelPath(runtimeRoot, "CPU", extractResult.Manifest.Precision, extractResult.Manifest.InputWidth, extractResult.Manifest.InputHeight);
            LargeModelDinov2Detector cpuDetector = LoadModelOnce(runtimeRoot, cpuModelPath, extractResult.BankPath, 2, out cpuRet);
            if (cpuRet == 0)
            {
                Trace.TraceWarning("大模型首选设备加载失败，返回码=" + firstRet.ToString(CultureInfo.InvariantCulture) + "，已执行 CPU 回退。");
                return new LoadModelResult(cpuDetector, 2, cpuModelPath);
            }

            cpuDetector.Dispose();
            throw new InvalidOperationException(BuildLoadModelFailureMessage(firstModelPath, firstDeviceMode, firstRet, cpuRet));
        }

        /// <summary>
        /// 解析模板首选模型路径；新模板优先使用内置 engine/ONNX，旧模板回退到 LargeModelDll。
        /// </summary>
        /// <param name="runtimeRoot">大模型运行环境目录。</param>
        /// <param name="extractResult">模板解包结果。</param>
        /// <returns>本次优先加载的模型路径。</returns>
        private static string ResolvePrimaryModelPath(string runtimeRoot, LargeModelTemplateExtractResult extractResult)
        {
            if (extractResult != null &&
                !string.IsNullOrWhiteSpace(extractResult.ModelPath) &&
                File.Exists(extractResult.ModelPath))
                return extractResult.ModelPath;

            return LargeModelRuntimeBootstrapper.ResolveModelPath(runtimeRoot, extractResult.Manifest);
        }

        /// <summary>
        /// 单次调用 native init 初始化模型。
        /// </summary>
        /// <param name="runtimeRoot">大模型运行环境目录。</param>
        /// <param name="modelPath">模型路径。</param>
        /// <param name="bankPath">bank 路径。</param>
        /// <param name="deviceMode">native 设备模式。</param>
        /// <param name="returnCode">native 返回码。</param>
        /// <returns>本次初始化使用的检测器实例。</returns>
        private static LargeModelDinov2Detector LoadModelOnce(string runtimeRoot, string modelPath, string bankPath, int deviceMode, out int returnCode)
        {
            LargeModelDinov2Detector detector = new LargeModelDinov2Detector(runtimeRoot);
            returnCode = detector.LoadModel(modelPath, bankPath, deviceMode);
            return detector;
        }

        /// <summary>
        /// 从模板清单解析 native 设备模式。
        /// </summary>
        /// <param name="manifest">模板清单。</param>
        /// <returns>0 自动、1 GPU、2 CPU。</returns>
        private static int ResolveManifestDeviceMode(LargeModelTemplateManifest manifest)
        {
            if (manifest != null && manifest.DeviceMode >= 0 && manifest.DeviceMode <= 2)
                return manifest.DeviceMode;

            return LargeModelRuntimeBootstrapper.ResolveDeviceMode(manifest == null ? null : manifest.Device);
        }

        /// <summary>
        /// 判断当前失败是否允许用 CPU 重新初始化。
        /// </summary>
        /// <param name="deviceMode">首选设备模式。</param>
        /// <returns>AUTO 模式失败时返回 true。</returns>
        private static bool ShouldRetryLoadModelOnCpu(int deviceMode)
        {
            return deviceMode == 0;
        }

        /// <summary>
        /// 生成 init 全部尝试失败后的诊断信息。
        /// </summary>
        /// <param name="modelPath">首选模型路径。</param>
        /// <param name="deviceMode">首选设备模式。</param>
        /// <param name="firstRet">首选设备返回码。</param>
        /// <param name="cpuRet">CPU 回退返回码，未回退时为空。</param>
        /// <returns>可直接展示给用户的诊断文本。</returns>
        private static string BuildLoadModelFailureMessage(string modelPath, int deviceMode, int firstRet, int? cpuRet)
        {
            string message = "加载大模型模板失败，返回码：" + firstRet.ToString(CultureInfo.InvariantCulture) +
                "，设备模式=" + deviceMode.ToString(CultureInfo.InvariantCulture) +
                "，模型=" + modelPath;

            if (cpuRet.HasValue)
                message += "，CPU回退返回码：" + cpuRet.Value.ToString(CultureInfo.InvariantCulture);

            return message;
        }

        /// <summary>
        /// 解析并校验模板路径。
        /// </summary>
        /// <param name="templatePath">原始模板路径。</param>
        /// <returns>模板完整路径。</returns>
        private static string ResolveTemplatePath(string templatePath)
        {
            if (string.IsNullOrWhiteSpace(templatePath))
                throw new InvalidOperationException("请选择大模型模板文件。");

            string fullTemplatePath = Path.GetFullPath(templatePath);
            if (!File.Exists(fullTemplatePath))
                throw new FileNotFoundException("未找到大模型模板文件。", fullTemplatePath);

            string modelRoot = EnsureTrailingSeparator(Path.GetFullPath(LargeModelRuntimeBootstrapper.GetModelRoot()));
            if (!fullTemplatePath.StartsWith(modelRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("大模型调用节点只能使用训练窗口生成到 Model 文件夹中的模板。");

            return fullTemplatePath;
        }

        /// <summary>
        /// 根据模板 ROI 设置构建 native 推理图像。
        /// </summary>
        /// <param name="sourceImage">源图。</param>
        /// <param name="manifest">模板清单。</param>
        /// <param name="roiRect">实际使用的 ROI。</param>
        /// <returns>待推理图像。</returns>
        private static Mat BuildInferenceImage(Mat sourceImage, LargeModelTemplateManifest manifest, out CvRect? roiRect)
        {
            roiRect = null;
            if (manifest == null || !manifest.RoiEnabled)
                return sourceImage.Clone();

            CvRect rect = ClampRect(new CvRect(manifest.RoiX, manifest.RoiY, manifest.RoiWidth, manifest.RoiHeight), sourceImage.Width, sourceImage.Height);
            if (rect.Width <= 0 || rect.Height <= 0)
                throw new InvalidOperationException("大模型模板 ROI 超出当前图像范围。");

            roiRect = rect;
            return new Mat(sourceImage, rect).Clone();
        }

        /// <summary>
        /// 将 ROI 内异常框偏移回源图坐标。
        /// </summary>
        /// <param name="boxes">native 返回的推理图像坐标矩形。</param>
        /// <param name="roiRect">推理 ROI。</param>
        /// <param name="imageWidth">源图宽度。</param>
        /// <param name="imageHeight">源图高度。</param>
        /// <returns>源图坐标系异常矩形。</returns>
        internal static List<DrawingRect> OffsetBoxesToSourceImage(DrawingRect[] boxes, CvRect? roiRect, int imageWidth, int imageHeight)
        {
            List<DrawingRect> result = new List<DrawingRect>();
            if (boxes == null || imageWidth <= 0 || imageHeight <= 0)
                return result;

            int offsetX = roiRect.HasValue ? roiRect.Value.X : 0;
            int offsetY = roiRect.HasValue ? roiRect.Value.Y : 0;
            foreach (DrawingRect box in boxes)
            {
                DrawingRect shifted = new DrawingRect(box.X + offsetX, box.Y + offsetY, box.Width, box.Height);
                DrawingRect clamped = ClampDrawingRect(shifted, imageWidth, imageHeight);
                if (clamped.Width > 0 && clamped.Height > 0)
                    result.Add(clamped);
            }

            return result;
        }

        /// <summary>
        /// 限制 OpenCV ROI 在图像范围内。
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
        /// 限制绘制矩形在图像范围内。
        /// </summary>
        /// <param name="rect">原始绘制矩形。</param>
        /// <param name="imageWidth">图像宽度。</param>
        /// <param name="imageHeight">图像高度。</param>
        /// <returns>裁剪后的绘制矩形。</returns>
        private static DrawingRect ClampDrawingRect(DrawingRect rect, int imageWidth, int imageHeight)
        {
            int left = Math.Max(0, Math.Min(rect.Left, imageWidth));
            int top = Math.Max(0, Math.Min(rect.Top, imageHeight));
            int right = Math.Max(left, Math.Min(rect.Right, imageWidth));
            int bottom = Math.Max(top, Math.Min(rect.Bottom, imageHeight));
            return new DrawingRect(left, top, right - left, bottom - top);
        }

        /// <summary>
        /// 生成推理结果摘要文本。
        /// </summary>
        /// <param name="nativeResult">native 推理结果。</param>
        /// <param name="boxCount">异常框数量。</param>
        /// <returns>结果摘要。</returns>
        private static string BuildResultMessage(LargeModelDinov2Result nativeResult, int boxCount)
        {
            if (nativeResult == null)
                return "大模型推理结果为空";
            if (nativeResult.ReturnCode < 0)
                return "大模型推理返回码：" + nativeResult.ReturnCode.ToString(CultureInfo.InvariantCulture);
            if (nativeResult.IsNg)
                return "检测到大模型异常区域：" + boxCount.ToString(CultureInfo.InvariantCulture) + " 个";

            return "OK";
        }

        /// <summary>
        /// 确保目录路径以分隔符结尾。
        /// </summary>
        /// <param name="path">目录路径。</param>
        /// <returns>带尾部分隔符的目录路径。</returns>
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
        /// 释放当前 native 检测器。
        /// </summary>
        private void ReleaseDetector()
        {
            if (_detector != null)
            {
                _detector.Dispose();
                _detector = null;
                _loadedDevice = null;
                _loadedModelPath = null;
            }
        }

        /// <summary>
        /// 记录成功加载模型后的检测器与实际设备模式。
        /// </summary>
        private sealed class LoadModelResult
        {
            /// <summary>
            /// 初始化模型加载结果。
            /// </summary>
            /// <param name="detector">已加载模型的检测器。</param>
            /// <param name="deviceMode">实际加载成功的设备模式。</param>
            /// <param name="modelPath">实际加载成功的模型路径。</param>
            public LoadModelResult(LargeModelDinov2Detector detector, int deviceMode, string modelPath)
            {
                Detector = detector;
                DeviceMode = deviceMode;
                ModelPath = modelPath;
            }

            /// <summary>
            /// 已加载模型的检测器。
            /// </summary>
            public LargeModelDinov2Detector Detector { get; private set; }

            /// <summary>
            /// 实际加载成功的设备模式。
            /// </summary>
            public int DeviceMode { get; private set; }

            /// <summary>
            /// 实际加载成功的模型路径。
            /// </summary>
            public string ModelPath { get; private set; }
        }
    }

    /// <summary>
    /// 大模型调用运行结果。
    /// </summary>
    internal sealed class LargeModelDetectionRunResult
    {
        /// <summary>
        /// 初始化大模型调用运行结果。
        /// </summary>
        /// <param name="nativeResult">native 推理结果。</param>
        /// <param name="manifest">模板清单。</param>
        /// <param name="roiRect">实际推理 ROI。</param>
        /// <param name="boxes">源图坐标系异常矩形。</param>
        /// <param name="isOk">最终 OK/NG 判定。</param>
        /// <param name="message">结果摘要。</param>
        /// <param name="actualDevice">实际推理设备。</param>
        /// <param name="actualModelPath">实际模型路径。</param>
        /// <param name="inputWidth">输入图像宽度。</param>
        /// <param name="inputHeight">输入图像高度。</param>
        /// <param name="imagePreparationMilliseconds">图像准备耗时。</param>
        /// <param name="nativeInferenceMilliseconds">native 推理耗时。</param>
        /// <param name="postprocessMilliseconds">结果处理耗时。</param>
        public LargeModelDetectionRunResult(
            LargeModelDinov2Result nativeResult,
            LargeModelTemplateManifest manifest,
            CvRect? roiRect,
            List<DrawingRect> boxes,
            bool isOk,
            string message,
            string actualDevice,
            string actualModelPath,
            int inputWidth,
            int inputHeight,
            long imagePreparationMilliseconds,
            long nativeInferenceMilliseconds,
            long postprocessMilliseconds)
        {
            NativeResult = nativeResult;
            Manifest = manifest;
            RoiRect = roiRect;
            Boxes = boxes ?? new List<DrawingRect>();
            IsOk = isOk;
            Message = message;
            ActualDevice = actualDevice;
            ActualModelPath = actualModelPath;
            InputWidth = inputWidth;
            InputHeight = inputHeight;
            ImagePreparationMilliseconds = imagePreparationMilliseconds;
            NativeInferenceMilliseconds = nativeInferenceMilliseconds;
            PostprocessMilliseconds = postprocessMilliseconds;
        }

        /// <summary>
        /// native 推理结果。
        /// </summary>
        public LargeModelDinov2Result NativeResult { get; }

        /// <summary>
        /// 模板清单。
        /// </summary>
        public LargeModelTemplateManifest Manifest { get; }

        /// <summary>
        /// 实际推理 ROI；为空表示整图推理。
        /// </summary>
        public CvRect? RoiRect { get; }

        /// <summary>
        /// 源图坐标系异常矩形。
        /// </summary>
        public List<DrawingRect> Boxes { get; }

        /// <summary>
        /// 当前检测是否 OK。
        /// </summary>
        public bool IsOk { get; }

        /// <summary>
        /// 结果摘要。
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 实际推理设备。
        /// </summary>
        public string ActualDevice { get; }

        /// <summary>
        /// 实际加载的模型文件路径。
        /// </summary>
        public string ActualModelPath { get; }

        /// <summary>
        /// 输入图像宽度。
        /// </summary>
        public int InputWidth { get; }

        /// <summary>
        /// 输入图像高度。
        /// </summary>
        public int InputHeight { get; }

        /// <summary>
        /// ROI 裁剪、BGR 转换与图像复制耗时。
        /// </summary>
        public long ImagePreparationMilliseconds { get; }

        /// <summary>
        /// native Infer 调用耗时。
        /// </summary>
        public long NativeInferenceMilliseconds { get; }

        /// <summary>
        /// native 返回转换和结果坐标处理耗时。
        /// </summary>
        public long PostprocessMilliseconds { get; }
    }
}
