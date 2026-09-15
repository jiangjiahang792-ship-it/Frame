using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using OpenCvSharp;
using CvRect = OpenCvSharp.Rect;

namespace TDJS_Vision.Forms.AiTrainForm
{
    /// <summary>
    /// 无监督训练服务，负责数据集准备、native 训练和模板打包。
    /// </summary>
    public sealed class UnsupervisedTrainingService
    {
        /// <summary>
        /// 自动识别分数校准时最多读取的异常框数量。
        /// </summary>
        private const int CalibrationMaxBoxes = 128;

        /// <summary>
        /// native 子进程发生内部错误时允许的总训练次数。
        /// </summary>
        private const int NativeTrainingAttemptCount = 2;

        /// <summary>
        /// 当前训练使用的 native 检测器。
        /// </summary>
        private UnsupervisedAnomalibDetector _activeDetector;

        /// <summary>
        /// 当前训练检测器访问锁。
        /// </summary>
        private readonly object _activeDetectorLock = new object();

        /// <summary>
        /// 执行一次无监督训练。
        /// </summary>
        /// <param name="request">训练请求。</param>
        /// <param name="progress">训练进度回调。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>训练结果。</returns>
        public UnsupervisedTrainingResult Train(UnsupervisedTrainingRequest request, IProgress<int> progress, IProgress<string> log, CancellationToken token)
        {
            ValidateRequest(request);
            UnsupervisedRuntimeStatus runtime = UnsupervisedRuntimeBootstrapper.ValidateRuntime();
            if (!runtime.IsReady)
                throw new InvalidOperationException(runtime.Message);

            string modelRoot = UnsupervisedRuntimeBootstrapper.EnsureModelRoot();
            EnsureTemplatePathInModelRoot(request.OutputTemplatePath, modelRoot);

            string workRoot = Path.Combine(modelRoot, "_UnsupervisedTraining", SafeFileName(request.TemplateName));
            string datasetRoot = Path.Combine(workRoot, "dataset");
            string modelOutputRoot = Path.Combine(workRoot, "model");
            PrepareCleanDirectory(datasetRoot, workRoot);
            PrepareCleanDirectory(modelOutputRoot, workRoot);

            log?.Report("正在准备训练数据集。");
            DatasetBuildResult dataset = BuildDataset(request, datasetRoot, token);
            log?.Report("数据集准备完成，OK=" + dataset.OkCount.ToString(CultureInfo.InvariantCulture) + "，NG=" + dataset.NgCount.ToString(CultureInfo.InvariantCulture));
            ValidateDatasetImageCount(dataset);
            int compatibilityFileCount = UnsupervisedNativeDatasetPreparer.EnsureMinimumNgSplitFiles(dataset.NgPath, dataset.NgCount);
            if (compatibilityFileCount > 0)
            {
                log?.Report("单张 NG 已生成 native 内部拆分副本，客户样本统计仍为 1 张。");
            }

            token.ThrowIfCancellationRequested();
            progress?.Report(1);

            string pythonPath = UnsupervisedAnomalibDetector.FindPython(runtime.RootPath);
            TrainNativeModel(runtime.RootPath, pythonPath, modelOutputRoot, request, dataset, progress, log, token);

            token.ThrowIfCancellationRequested();
            string onnxPath = UnsupervisedAnomalibDetector.FindNewestOnnx(modelOutputRoot);
            log?.Report("正在打包模板文件。");
            float calibratedThreshold = CalibrateThreshold(runtime.RootPath, onnxPath, pythonPath, request, dataset, log, token);
            token.ThrowIfCancellationRequested();
            UnsupervisedTemplateManifest manifest = BuildManifest(request, dataset, calibratedThreshold);
            UnsupervisedTemplatePackage.Create(request.OutputTemplatePath, manifest, onnxPath);
            progress?.Report(100);

            return new UnsupervisedTrainingResult
            {
                TemplatePath = request.OutputTemplatePath,
                OnnxPath = onnxPath,
                OkCount = dataset.OkCount,
                NgCount = dataset.NgCount
            };
        }

        /// <summary>
        /// 使用独立 native 句柄执行训练，并对可恢复的子进程内部错误重试一次。
        /// </summary>
        /// <param name="runtimeRoot">无监督运行环境根目录。</param>
        /// <param name="pythonPath">本次训练使用的 Python 路径。</param>
        /// <param name="modelOutputRoot">模型输出目录。</param>
        /// <param name="request">训练请求。</param>
        /// <param name="dataset">已准备的数据集。</param>
        /// <param name="progress">训练进度回调。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        private void TrainNativeModel(
            string runtimeRoot,
            string pythonPath,
            string modelOutputRoot,
            UnsupervisedTrainingRequest request,
            DatasetBuildResult dataset,
            IProgress<int> progress,
            IProgress<string> log,
            CancellationToken token)
        {
            for (int attempt = 1; attempt <= NativeTrainingAttemptCount; attempt++)
            {
                token.ThrowIfCancellationRequested();
                using (var detector = new UnsupervisedAnomalibDetector(runtimeRoot))
                {
                    SetActiveDetector(detector);
                    try
                    {
                        log?.Report(attempt == 1 ? "正在初始化无监督训练环境。" : "正在使用全新 native 句柄重新初始化训练环境。");
                        int initRet = detector.InitForTraining(
                            request.ModelType,
                            request.Threshold,
                            request.InputWidth,
                            request.InputHeight,
                            request.MiniArea,
                            request.MaxEpochs,
                            request.BatchSize,
                            request.Device,
                            pythonPath,
                            false);
                        if (initRet != 0)
                            throw CreateNativeTrainingException("初始化无监督训练", initRet);

                        token.ThrowIfCancellationRequested();
                        log?.Report(attempt == 1 ? "开始训练模型。" : "开始第 2 次训练模型。");
                        int trainRet = detector.TrainModel(
                            dataset.OkPath,
                            dataset.NgPath,
                            modelOutputRoot,
                            request.MaxEpochs,
                            value => progress?.Report(ClampProgress(value)));
                        if (trainRet == 0)
                            return;

                        token.ThrowIfCancellationRequested();
                        if (trainRet != -2 || attempt >= NativeTrainingAttemptCount)
                            throw CreateNativeTrainingException("无监督训练", trainRet);
                    }
                    finally
                    {
                        SetActiveDetector(null);
                    }
                }

                log?.Report("native 子进程返回内部错误 -2，正在清理本次残留并自动重试一次。");
                PrepareCleanDirectory(modelOutputRoot, Path.GetDirectoryName(modelOutputRoot));
            }
        }

        /// <summary>
        /// 创建包含 native 返回码语义的训练异常。
        /// </summary>
        /// <param name="stage">失败阶段。</param>
        /// <param name="returnCode">native 返回码。</param>
        /// <returns>可直接显示和记录的异常。</returns>
        private static InvalidOperationException CreateNativeTrainingException(string stage, int returnCode)
        {
            string reason;
            switch (returnCode)
            {
                case -1:
                    reason = "native 参数无效";
                    break;
                case -2:
                    reason = "Python/anomalib 子进程执行失败";
                    break;
                case -3:
                    reason = "当前训练句柄已有任务运行";
                    break;
                case -4:
                    reason = "训练已取消";
                    break;
                case -5:
                    reason = "设备授权失败";
                    break;
                default:
                    reason = "未知 native 错误";
                    break;
            }

            return new InvalidOperationException(
                stage
                + "失败，返回码："
                + returnCode.ToString(CultureInfo.InvariantCulture)
                + "（"
                + reason
                + "）。");
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
                    _activeDetector.CancelTrain();
                }
            }
        }

        /// <summary>
        /// 设置当前训练检测器。
        /// </summary>
        /// <param name="detector">检测器实例。</param>
        private void SetActiveDetector(UnsupervisedAnomalibDetector detector)
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
        private static void ValidateRequest(UnsupervisedTrainingRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.TemplateName))
                throw new InvalidOperationException("模板名称不能为空。");
            if (string.IsNullOrWhiteSpace(request.OutputTemplatePath))
                throw new InvalidOperationException("模板输出路径不能为空。");
            if (request.Images == null || request.Images.Count == 0)
                throw new InvalidOperationException("请先加载训练图片。");
            int okImageCount = request.Images.Count(item => item.Category == UnsupervisedImageCategory.OK);
            int ngImageCount = request.Images.Count(item => item.Category == UnsupervisedImageCategory.NG);
            if (okImageCount < 1 || ngImageCount < 1)
                throw new InvalidOperationException("无监督训练至少需要 1 张 OK 图片和 1 张 NG 图片。");
            if (request.InputWidth <= 0 || request.InputHeight <= 0)
                throw new InvalidOperationException("训练输入尺寸必须大于 0。");
            if (request.BatchSize <= 0 || request.MaxEpochs <= 0)
                throw new InvalidOperationException("训练轮数和批次大小必须大于 0。");
        }

        /// <summary>
        /// 校验实际写入训练数据集的图片数量，避免 native 训练返回笼统的 -2 错误码。
        /// </summary>
        /// <param name="dataset">数据集构建结果。</param>
        private static void ValidateDatasetImageCount(DatasetBuildResult dataset)
        {
            if (dataset.OkCount <= 0)
                throw new InvalidOperationException("没有可用于训练的 OK 图片。");
            if (dataset.NgCount <= 0)
                throw new InvalidOperationException("没有可用于训练的 NG 图片。");
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
        private static DatasetBuildResult BuildDataset(UnsupervisedTrainingRequest request, string datasetRoot, CancellationToken token)
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
            int sourceOkCount = request.Images.Count(item => item.Category == UnsupervisedImageCategory.OK);
            int sourceNgCount = request.Images.Count(item => item.Category == UnsupervisedImageCategory.NG);
            bool canReserveCalibrationImages = sourceOkCount > 1 && sourceNgCount > 1;
            string calibrationOkImagePath = null;
            string calibrationNgImagePath = null;
            foreach (UnsupervisedImageItem item in request.Images)
            {
                token.ThrowIfCancellationRequested();
                bool isNg = item.Category == UnsupervisedImageCategory.NG;
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

            if (okCount == 0)
                throw new InvalidOperationException("没有可用于训练的 OK 图片。");

            return new DatasetBuildResult(okPath, ngPath, okCount, ngCount, calibrationOkImagePath, calibrationNgImagePath);
        }

        /// <summary>
        /// 复制整图到训练数据集；无 ROI 时使用该路径。
        /// </summary>
        /// <param name="sourceFile">源图路径。</param>
        /// <param name="targetFolder">目标目录。</param>
        /// <param name="index">图片序号。</param>
        /// <returns>保存成功返回目标路径，失败返回 null。</returns>
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
        /// <returns>保存成功返回目标路径，失败返回 null。</returns>
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
        /// 使用预留 OK/NG 图像自动校准无监督识别分数。
        /// </summary>
        /// <param name="runtimeRoot">无监督运行环境根目录。</param>
        /// <param name="onnxPath">训练输出的 ONNX 模型路径。</param>
        /// <param name="pythonPath">Python 路径。</param>
        /// <param name="request">训练请求。</param>
        /// <param name="dataset">包含预留校准图的数据集结果。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>可正确区分预留 OK/NG 的异常阈值。</returns>
        private static float CalibrateThreshold(
            string runtimeRoot,
            string onnxPath,
            string pythonPath,
            UnsupervisedTrainingRequest request,
            DatasetBuildResult dataset,
            IProgress<string> log,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!HasCalibrationImages(dataset))
            {
                log?.Report(
                    "无监督自动校准识别分数样本不足，使用默认异常阈值："
                    + request.Threshold.ToString("0.######", CultureInfo.InvariantCulture));
                return request.Threshold;
            }

            log?.Report("正在使用预留 OK/NG 图像自动校准无监督识别分数。");
            try
            {
                using (var detector = new UnsupervisedAnomalibDetector(runtimeRoot))
                using (Mat okImage = LoadCalibrationImage(dataset.CalibrationOkImagePath, "OK"))
                using (Mat ngImage = LoadCalibrationImage(dataset.CalibrationNgImagePath, "NG"))
                {
                    int loadRet = detector.LoadModel(
                        request.ModelType,
                        onnxPath,
                        request.Threshold,
                        request.InputWidth,
                        request.InputHeight,
                        request.MiniArea,
                        request.BatchSize,
                        request.Device,
                        pythonPath);
                    if (loadRet != 0)
                        throw new InvalidOperationException("无监督自动校准前加载模型失败，返回码：" + loadRet.ToString(CultureInfo.InvariantCulture));

                    UnsupervisedAnomalibResult okProbe = detector.InferBgr(okImage, request.Threshold, request.MiniArea, CalibrationMaxBoxes);
                    UnsupervisedAnomalibResult ngProbe = detector.InferBgr(ngImage, request.Threshold, request.MiniArea, CalibrationMaxBoxes);
                    EnsureCalibrationInferenceSucceeded(okProbe, "OK");
                    EnsureCalibrationInferenceSucceeded(ngProbe, "NG");
                    float threshold = SelectThresholdBetweenScores(okProbe.Score, ngProbe.Score, "无监督");
                    UnsupervisedAnomalibResult okVerify = detector.InferBgr(okImage, threshold, request.MiniArea, CalibrationMaxBoxes);
                    UnsupervisedAnomalibResult ngVerify = detector.InferBgr(ngImage, threshold, request.MiniArea, CalibrationMaxBoxes);
                    bool okPassed = okVerify.ReturnCode == 0 && okVerify.Boxes.Length == 0;
                    bool ngPassed = ngVerify.ReturnCode == 0 && ngVerify.Boxes.Length > 0;
                    if (!okPassed || !ngPassed)
                    {
                        throw new InvalidOperationException(
                            "无监督自动校准识别分数失败，预留 OK/NG 样本无法同时判对；OK分数="
                            + okProbe.Score.ToString("0.######", CultureInfo.InvariantCulture)
                            + "，NG分数="
                            + ngProbe.Score.ToString("0.######", CultureInfo.InvariantCulture)
                            + "，候选阈值="
                            + threshold.ToString("0.######", CultureInfo.InvariantCulture)
                            + "。请更换更有代表性的 OK/NG 样本或调整最小缺陷面积。");
                    }

                    log?.Report(
                        "无监督识别分数自动校准完成，OK分数="
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
                    "无监督自动校准识别分数未成功，已使用默认异常阈值 "
                    + request.Threshold.ToString("0.######", CultureInfo.InvariantCulture)
                    + "，不影响模型生成。原因："
                    + ex.Message);
                return request.Threshold;
            }
        }

        /// <summary>
        /// 校验自动分数探测的 native 推理是否成功。
        /// </summary>
        /// <param name="result">native 推理结果。</param>
        /// <param name="categoryText">校准图片分类。</param>
        private static void EnsureCalibrationInferenceSucceeded(UnsupervisedAnomalibResult result, string categoryText)
        {
            if (result == null)
                throw new InvalidOperationException("无监督自动校准 " + categoryText + " 图片未返回推理结果。");
            if (result.ReturnCode != 0)
            {
                throw new InvalidOperationException(
                    "无监督自动校准 "
                    + categoryText
                    + " 图片推理失败，返回码："
                    + result.ReturnCode.ToString(CultureInfo.InvariantCulture));
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
                throw new FileNotFoundException("未找到无监督自动校准使用的 " + categoryText + " 图片。", imagePath);

            Mat image = Cv2.ImRead(imagePath);
            if (image.Empty())
            {
                image.Dispose();
                throw new InvalidDataException("无监督自动校准使用的 " + categoryText + " 图片为空或无法读取：" + imagePath);
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
            if (threshold > 1.0)
                throw new InvalidOperationException(moduleName + "自动校准识别分数超过 1，当前无监督阈值范围无法保存该分数。");

            return (float)threshold;
        }

        /// <summary>
        /// 构建模板清单。
        /// </summary>
        /// <param name="request">训练请求。</param>
        /// <param name="dataset">数据集结果。</param>
        /// <param name="threshold">自动校准后的异常阈值。</param>
        /// <returns>模板清单。</returns>
        private static UnsupervisedTemplateManifest BuildManifest(UnsupervisedTrainingRequest request, DatasetBuildResult dataset, float threshold)
        {
            CvRect roi = request.RoiRect.GetValueOrDefault();
            return new UnsupervisedTemplateManifest
            {
                Version = 1,
                TemplateName = request.TemplateName,
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                ModelType = request.ModelType,
                Threshold = threshold,
                MiniArea = request.MiniArea,
                InputWidth = request.InputWidth,
                InputHeight = request.InputHeight,
                BatchSize = request.BatchSize,
                Device = request.Device,
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
        /// 限制进度范围。
        /// </summary>
        /// <param name="value">原始进度。</param>
        /// <returns>0 到 100 的进度。</returns>
        private static int ClampProgress(int value)
        {
            return Math.Max(0, Math.Min(100, value));
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
            /// <param name="calibrationOkImagePath">预留 OK 校准图路径。</param>
            /// <param name="calibrationNgImagePath">预留 NG 校准图路径。</param>
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
