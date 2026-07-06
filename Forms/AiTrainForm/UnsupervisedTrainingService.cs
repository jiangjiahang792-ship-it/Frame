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

            token.ThrowIfCancellationRequested();
            progress?.Report(1);

            string pythonPath = UnsupervisedAnomalibDetector.FindPython(runtime.RootPath);
            using (var detector = new UnsupervisedAnomalibDetector(runtime.RootPath))
            {
                SetActiveDetector(detector);
                try
                {
                    log?.Report("正在初始化无监督训练环境。");
                    int initRet = detector.InitForTraining(request.ModelType, request.Threshold, request.InputWidth, request.InputHeight, request.MiniArea, request.MaxEpochs, request.BatchSize, request.Device, pythonPath, false);
                    if (initRet != 0)
                        throw new InvalidOperationException("初始化无监督训练失败，返回码：" + initRet.ToString(CultureInfo.InvariantCulture));

                    token.ThrowIfCancellationRequested();
                    log?.Report("开始训练模型。");
                    int trainRet = detector.TrainModel(dataset.OkPath, dataset.NgCount > 0 ? dataset.NgPath : string.Empty, modelOutputRoot, request.MaxEpochs, value => progress?.Report(ClampProgress(value)));
                    if (trainRet != 0)
                        throw new InvalidOperationException("无监督训练失败，返回码：" + trainRet.ToString(CultureInfo.InvariantCulture));
                }
                finally
                {
                    SetActiveDetector(null);
                }
            }

            token.ThrowIfCancellationRequested();
            string onnxPath = UnsupervisedAnomalibDetector.FindNewestOnnx(modelOutputRoot);
            log?.Report("正在打包模板文件。");
            UnsupervisedTemplateManifest manifest = BuildManifest(request, dataset);
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
            if (!request.Images.Any(item => item.Category == UnsupervisedImageCategory.OK))
                throw new InvalidOperationException("训练至少需要 OK 图片。");
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
            const int minOkCount = 2;
            const int minNgCount = 2;

            if (dataset.OkCount < minOkCount)
            {
                throw new InvalidOperationException("OK 图片数量不足，当前 " + dataset.OkCount.ToString(CultureInfo.InvariantCulture) + " 张，最少 " + minOkCount.ToString(CultureInfo.InvariantCulture) + " 张。");
            }

            if (dataset.NgCount == 1)
            {
                throw new InvalidOperationException("NG 图片数量不足，当前 1 张，最少 " + minNgCount.ToString(CultureInfo.InvariantCulture) + " 张；如需纯 OK 训练，请不要放入 NG 图像。");
            }
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
            Directory.CreateDirectory(okPath);
            Directory.CreateDirectory(ngPath);

            int okCount = 0;
            int ngCount = 0;
            int index = 0;
            foreach (UnsupervisedImageItem item in request.Images)
            {
                token.ThrowIfCancellationRequested();
                string targetFolder = item.Category == UnsupervisedImageCategory.NG ? ngPath : okPath;
                bool saved = request.RoiRect.HasValue
                    ? CropImageToRoi(item.FilePath, targetFolder, request.RoiRect.Value, index)
                    : CopyImageToDataset(item.FilePath, targetFolder, index);

                if (saved)
                {
                    if (item.Category == UnsupervisedImageCategory.NG) ngCount++;
                    else okCount++;
                }
                index++;
            }

            if (okCount == 0)
                throw new InvalidOperationException("没有可用于训练的 OK 图片。");

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
        /// <returns>模板清单。</returns>
        private static UnsupervisedTemplateManifest BuildManifest(UnsupervisedTrainingRequest request, DatasetBuildResult dataset)
        {
            CvRect roi = request.RoiRect.GetValueOrDefault();
            return new UnsupervisedTemplateManifest
            {
                Version = 1,
                TemplateName = request.TemplateName,
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                ModelType = request.ModelType,
                Threshold = request.Threshold,
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
