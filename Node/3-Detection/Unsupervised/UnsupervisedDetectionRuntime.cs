using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using OpenCvSharp;
using TDJS_Vision.Forms.AiTrainForm;
using CvRect = OpenCvSharp.Rect;
using DrawingRect = System.Drawing.Rectangle;

namespace TDJS_Vision.Node._3_Detection.Unsupervised
{
    /// <summary>
    /// 无监督检测节点的模板加载与推理运行适配器。
    /// </summary>
    internal sealed class UnsupervisedDetectionRuntime : IDisposable
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
        /// 当前已加载模型使用的异常阈值。
        /// </summary>
        private float _loadedThreshold;

        /// <summary>
        /// 当前已加载模型使用的最小缺陷面积。
        /// </summary>
        private float _loadedMiniArea;

        /// <summary>
        /// 当前已加载模型使用的推理批次。
        /// </summary>
        private int _loadedInferenceBatchSize;

        /// <summary>
        /// 当前模板解包结果。
        /// </summary>
        private UnsupervisedTemplateExtractResult _extractResult;

        /// <summary>
        /// 当前 native 检测器。
        /// </summary>
        private UnsupervisedAnomalibDetector _detector;

        /// <summary>
        /// 执行一次无监督检测。
        /// </summary>
        /// <param name="sourceImage">流程输入图像。</param>
        /// <param name="param">节点参数。</param>
        /// <returns>无监督检测结果。</returns>
        public UnsupervisedDetectionRunResult Infer(Mat sourceImage, NodeParamUnsupervisedDetection param)
        {
            if (sourceImage == null || sourceImage.Empty())
                throw new InvalidOperationException("无监督检测输入图像为空。");
            if (param == null)
                throw new ArgumentNullException(nameof(param));

            lock (_syncRoot)
            {
                EnsureModelLoaded(param);
                UnsupervisedTemplateManifest manifest = _extractResult.Manifest;
                CvRect? roiRect = null;
                using (Mat inferImage = BuildInferenceImage(sourceImage, manifest, out roiRect))
                {
                    int maxBoxes = param.MaxBoxes <= 0 ? NodeParamUnsupervisedDetection.DefaultMaxBoxes : param.MaxBoxes;
                    float threshold = ResolveThreshold(param, manifest);
                    float miniArea = ResolveMiniArea(param, manifest);
                    UnsupervisedAnomalibResult nativeResult = _detector.InferBgr(inferImage, threshold, miniArea, maxBoxes);
                    List<DrawingRect> boxes = OffsetBoxesToSourceImage(nativeResult.Boxes, roiRect, sourceImage.Width, sourceImage.Height);
                    bool isOk = nativeResult.ReturnCode == 0 && boxes.Count == 0;
                    string message = BuildResultMessage(nativeResult, boxes.Count);
                    return new UnsupervisedDetectionRunResult(nativeResult, manifest, roiRect, boxes, isOk, message);
                }
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
        private void EnsureModelLoaded(NodeParamUnsupervisedDetection param)
        {
            string templatePath = ResolveTemplatePath(param.TemplatePath);
            long templateTicks = File.GetLastWriteTimeUtc(templatePath).Ticks;
            if (_detector != null &&
                string.Equals(_loadedTemplatePath, templatePath, StringComparison.OrdinalIgnoreCase) &&
                _loadedTemplateTicks == templateTicks &&
                _extractResult != null &&
                IsLoadedRuntimeParamMatched(param, _extractResult.Manifest))
            {
                return;
            }

            UnsupervisedRuntimeStatus runtime = UnsupervisedRuntimeBootstrapper.ValidateRuntime();
            if (!runtime.IsReady)
                throw new InvalidOperationException(runtime.Message);

            string extractRoot = Path.Combine(UnsupervisedRuntimeBootstrapper.EnsureModelRoot(), "_UnsupervisedInference");
            Directory.CreateDirectory(extractRoot);
            UnsupervisedTemplateExtractResult extractResult = UnsupervisedTemplatePackage.Extract(templatePath, extractRoot);
            string pythonPath = UnsupervisedAnomalibDetector.FindPython(runtime.RootPath);
            string device = NormalizeDevice(extractResult.Manifest.Device);
            float threshold = ResolveThreshold(param, extractResult.Manifest);
            float miniArea = ResolveMiniArea(param, extractResult.Manifest);
            int batchSize = ResolveInferenceBatchSize(param, extractResult.Manifest);

            LoadModelResult loadResult = LoadModelWithFallback(runtime.RootPath, extractResult, threshold, miniArea, batchSize, device, pythonPath);

            ReleaseDetector();
            _detector = loadResult.Detector;
            _extractResult = extractResult;
            _loadedTemplatePath = templatePath;
            _loadedTemplateTicks = templateTicks;
            _loadedThreshold = threshold;
            _loadedMiniArea = miniArea;
            _loadedInferenceBatchSize = batchSize;
        }

        /// <summary>
        /// 判断当前 native 模型句柄是否已匹配节点运行参数。
        /// </summary>
        /// <param name="param">节点参数。</param>
        /// <param name="manifest">模板清单。</param>
        /// <returns>运行参数一致时返回 true。</returns>
        private bool IsLoadedRuntimeParamMatched(NodeParamUnsupervisedDetection param, UnsupervisedTemplateManifest manifest)
        {
            return AreFloatEqual(_loadedThreshold, ResolveThreshold(param, manifest)) &&
                AreFloatEqual(_loadedMiniArea, ResolveMiniArea(param, manifest)) &&
                _loadedInferenceBatchSize == ResolveInferenceBatchSize(param, manifest);
        }

        /// <summary>
        /// 解析本次推理使用的异常阈值。
        /// </summary>
        /// <param name="param">节点参数。</param>
        /// <param name="manifest">模板清单。</param>
        /// <returns>有效异常阈值。</returns>
        private static float ResolveThreshold(NodeParamUnsupervisedDetection param, UnsupervisedTemplateManifest manifest)
        {
            if (param != null && param.Threshold > 0F && param.Threshold <= 1F)
                return param.Threshold;
            if (manifest != null && manifest.Threshold > 0F && manifest.Threshold <= 1F)
                return manifest.Threshold;

            return NodeParamUnsupervisedDetection.DefaultThreshold;
        }

        /// <summary>
        /// 解析本次推理使用的最小缺陷面积。
        /// </summary>
        /// <param name="param">节点参数。</param>
        /// <param name="manifest">模板清单。</param>
        /// <returns>有效最小缺陷面积。</returns>
        private static float ResolveMiniArea(NodeParamUnsupervisedDetection param, UnsupervisedTemplateManifest manifest)
        {
            if (param != null && param.MiniArea >= 0F)
                return param.MiniArea;
            if (manifest != null && manifest.MiniArea >= 0F)
                return manifest.MiniArea;

            return NodeParamUnsupervisedDetection.DefaultMiniArea;
        }

        /// <summary>
        /// 解析本次推理加载模型使用的批次大小。
        /// </summary>
        /// <param name="param">节点参数。</param>
        /// <param name="manifest">模板清单。</param>
        /// <returns>有效推理批次。</returns>
        private static int ResolveInferenceBatchSize(NodeParamUnsupervisedDetection param, UnsupervisedTemplateManifest manifest)
        {
            if (param != null && param.InferenceBatchSize > 0)
                return param.InferenceBatchSize;
            if (manifest != null && manifest.BatchSize > 0)
                return manifest.BatchSize;

            return NodeParamUnsupervisedDetection.DefaultInferenceBatchSize;
        }

        /// <summary>
        /// 规范化 native 推理设备名称，兼容模板中的大小写和历史文本。
        /// </summary>
        /// <param name="device">模板记录的设备文本。</param>
        /// <returns>native 接口可识别的设备名称。</returns>
        private static string NormalizeDevice(string device)
        {
            if (string.IsNullOrWhiteSpace(device))
                return "CPU";

            string text = device.Trim().ToUpperInvariant();
            if (text.StartsWith("AUTO", StringComparison.Ordinal))
                return "AUTO";
            if (text.Contains("GPU"))
                return "GPU";

            return "CPU";
        }

        /// <summary>
        /// 加载无监督模板模型；GPU/AUTO 初始化失败时使用 CPU 回退，避免训练设备限制推理设备。
        /// </summary>
        /// <param name="runtimeRoot">无监督运行环境目录。</param>
        /// <param name="extractResult">模板解包结果。</param>
        /// <param name="threshold">异常阈值。</param>
        /// <param name="miniArea">最小缺陷面积。</param>
        /// <param name="batchSize">推理批次。</param>
        /// <param name="device">首选推理设备。</param>
        /// <param name="pythonPath">Python 路径。</param>
        /// <returns>加载成功的检测器结果。</returns>
        private static LoadModelResult LoadModelWithFallback(string runtimeRoot, UnsupervisedTemplateExtractResult extractResult, float threshold, float miniArea, int batchSize, string device, string pythonPath)
        {
            int firstRet;
            UnsupervisedAnomalibDetector detector = LoadModelOnce(runtimeRoot, extractResult, threshold, miniArea, batchSize, device, pythonPath, out firstRet);
            if (firstRet == 0)
                return new LoadModelResult(detector, device);

            detector.Dispose();
            if (!ShouldRetryLoadModelOnCpu(device))
                throw new InvalidOperationException(BuildLoadModelFailureMessage(extractResult, device, firstRet, null, threshold, miniArea, batchSize, pythonPath));

            int cpuRet;
            UnsupervisedAnomalibDetector cpuDetector = LoadModelOnce(runtimeRoot, extractResult, threshold, miniArea, batchSize, "CPU", pythonPath, out cpuRet);
            if (cpuRet == 0)
            {
                Trace.TraceWarning("无监督模型 " + device + " 加载失败，返回码=" + firstRet.ToString(CultureInfo.InvariantCulture) + "，已执行 CPU回退。");
                return new LoadModelResult(cpuDetector, "CPU");
            }

            cpuDetector.Dispose();
            throw new InvalidOperationException(BuildLoadModelFailureMessage(extractResult, device, firstRet, cpuRet, threshold, miniArea, batchSize, pythonPath));
        }

        /// <summary>
        /// 单次调用 native LoadModel 初始化模型。
        /// </summary>
        /// <param name="runtimeRoot">无监督运行环境目录。</param>
        /// <param name="extractResult">模板解包结果。</param>
        /// <param name="threshold">异常阈值。</param>
        /// <param name="miniArea">最小缺陷面积。</param>
        /// <param name="batchSize">推理批次。</param>
        /// <param name="device">推理设备。</param>
        /// <param name="pythonPath">Python 路径。</param>
        /// <param name="returnCode">native 返回码。</param>
        /// <returns>本次初始化使用的检测器实例。</returns>
        private static UnsupervisedAnomalibDetector LoadModelOnce(string runtimeRoot, UnsupervisedTemplateExtractResult extractResult, float threshold, float miniArea, int batchSize, string device, string pythonPath, out int returnCode)
        {
            UnsupervisedAnomalibDetector detector = new UnsupervisedAnomalibDetector(runtimeRoot);
            returnCode = detector.LoadModel(
                extractResult.Manifest.ModelType,
                extractResult.ModelPath,
                threshold,
                extractResult.Manifest.InputWidth,
                extractResult.Manifest.InputHeight,
                miniArea,
                batchSize,
                device,
                pythonPath);

            return detector;
        }

        /// <summary>
        /// 判断当前失败是否允许用 CPU 重新初始化。
        /// </summary>
        /// <param name="device">首选推理设备。</param>
        /// <returns>GPU 或 AUTO 失败时返回 true。</returns>
        private static bool ShouldRetryLoadModelOnCpu(string device)
        {
            return string.Equals(device, "GPU", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(device, "AUTO", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 生成 LoadModel 全部尝试失败后的诊断信息。
        /// </summary>
        /// <param name="extractResult">模板解包结果。</param>
        /// <param name="device">首选推理设备。</param>
        /// <param name="firstRet">首选设备返回码。</param>
        /// <param name="cpuRet">CPU 回退返回码，未回退时为空。</param>
        /// <param name="threshold">异常阈值。</param>
        /// <param name="miniArea">最小缺陷面积。</param>
        /// <param name="batchSize">推理批次。</param>
        /// <param name="pythonPath">Python 路径。</param>
        /// <returns>可直接展示给用户的诊断文本。</returns>
        private static string BuildLoadModelFailureMessage(UnsupervisedTemplateExtractResult extractResult, string device, int firstRet, int? cpuRet, float threshold, float miniArea, int batchSize, string pythonPath)
        {
            string message = "加载无监督模板失败，返回码：" + firstRet.ToString(CultureInfo.InvariantCulture) +
                "，设备=" + device +
                "，批次=" + batchSize.ToString(CultureInfo.InvariantCulture) +
                "，阈值=" + threshold.ToString("0.###", CultureInfo.InvariantCulture) +
                "，最小缺陷面积=" + miniArea.ToString("0.###", CultureInfo.InvariantCulture) +
                "，模型=" + extractResult.ModelPath +
                "，Python=" + pythonPath;

            if (cpuRet.HasValue)
                message += "，CPU回退返回码：" + cpuRet.Value.ToString(CultureInfo.InvariantCulture);

            return message;
        }

        /// <summary>
        /// 比较两个浮点配置值是否一致。
        /// </summary>
        /// <param name="left">左值。</param>
        /// <param name="right">右值。</param>
        /// <returns>近似一致时返回 true。</returns>
        private static bool AreFloatEqual(float left, float right)
        {
            return Math.Abs(left - right) < 0.0001F;
        }

        /// <summary>
        /// 解析并校验模板路径。
        /// </summary>
        /// <param name="templatePath">原始模板路径。</param>
        /// <returns>模板完整路径。</returns>
        private static string ResolveTemplatePath(string templatePath)
        {
            if (string.IsNullOrWhiteSpace(templatePath))
                throw new InvalidOperationException("请选择无监督模板文件。");

            string fullTemplatePath = Path.GetFullPath(templatePath);
            if (!File.Exists(fullTemplatePath))
                throw new FileNotFoundException("未找到无监督模板文件。", fullTemplatePath);

            string modelRoot = EnsureTrailingSeparator(Path.GetFullPath(UnsupervisedRuntimeBootstrapper.GetModelRoot()));
            if (!fullTemplatePath.StartsWith(modelRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("无监督检测节点只能使用训练窗口生成到 Model 文件夹中的模板。");

            return fullTemplatePath;
        }

        /// <summary>
        /// 根据模板 ROI 设置构建 native 推理图像。
        /// </summary>
        /// <param name="sourceImage">源图。</param>
        /// <param name="manifest">模板清单。</param>
        /// <param name="roiRect">实际使用的 ROI。</param>
        /// <returns>待推理图像。</returns>
        private static Mat BuildInferenceImage(Mat sourceImage, UnsupervisedTemplateManifest manifest, out CvRect? roiRect)
        {
            roiRect = null;
            if (manifest == null || !manifest.RoiEnabled)
                return sourceImage.Clone();

            CvRect rect = ClampRect(new CvRect(manifest.RoiX, manifest.RoiY, manifest.RoiWidth, manifest.RoiHeight), sourceImage.Width, sourceImage.Height);
            if (rect.Width <= 0 || rect.Height <= 0)
                throw new InvalidOperationException("无监督模板 ROI 超出当前图像范围。");

            roiRect = rect;
            return new Mat(sourceImage, rect).Clone();
        }

        /// <summary>
        /// 将 ROI 内异常框偏移回源图坐标。
        /// </summary>
        /// <param name="boxes">native 返回矩形。</param>
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
        private static string BuildResultMessage(UnsupervisedAnomalibResult nativeResult, int boxCount)
        {
            if (nativeResult == null)
                return "无监督推理结果为空";
            if (nativeResult.ReturnCode != 0)
                return "无监督推理返回码：" + nativeResult.ReturnCode.ToString(CultureInfo.InvariantCulture);
            if (boxCount > 0)
                return "检测到异常区域：" + boxCount.ToString(CultureInfo.InvariantCulture) + " 个";
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
            }
        }

        /// <summary>
        /// 记录成功加载模型后的检测器与实际推理设备。
        /// </summary>
        private sealed class LoadModelResult
        {
            /// <summary>
            /// 初始化模型加载结果。
            /// </summary>
            /// <param name="detector">已加载模型的检测器。</param>
            /// <param name="device">实际加载成功的推理设备。</param>
            public LoadModelResult(UnsupervisedAnomalibDetector detector, string device)
            {
                Detector = detector;
                Device = device;
            }

            /// <summary>
            /// 已加载模型的检测器。
            /// </summary>
            public UnsupervisedAnomalibDetector Detector { get; private set; }

            /// <summary>
            /// 实际加载成功的推理设备。
            /// </summary>
            public string Device { get; private set; }
        }
    }

    /// <summary>
    /// 无监督检测运行结果。
    /// </summary>
    internal sealed class UnsupervisedDetectionRunResult
    {
        /// <summary>
        /// 初始化无监督检测运行结果。
        /// </summary>
        /// <param name="nativeResult">native 推理结果。</param>
        /// <param name="manifest">模板清单。</param>
        /// <param name="roiRect">实际推理 ROI。</param>
        /// <param name="boxes">源图坐标系异常矩形。</param>
        /// <param name="isOk">最终 OK/NG 判定。</param>
        /// <param name="message">结果摘要。</param>
        public UnsupervisedDetectionRunResult(UnsupervisedAnomalibResult nativeResult, UnsupervisedTemplateManifest manifest, CvRect? roiRect, List<DrawingRect> boxes, bool isOk, string message)
        {
            NativeResult = nativeResult;
            Manifest = manifest;
            RoiRect = roiRect;
            Boxes = boxes ?? new List<DrawingRect>();
            IsOk = isOk;
            Message = message;
        }

        /// <summary>
        /// native 推理结果。
        /// </summary>
        public UnsupervisedAnomalibResult NativeResult { get; }

        /// <summary>
        /// 模板清单。
        /// </summary>
        public UnsupervisedTemplateManifest Manifest { get; }

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
    }
}
