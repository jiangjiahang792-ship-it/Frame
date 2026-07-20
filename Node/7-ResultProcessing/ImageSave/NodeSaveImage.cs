using Logger;
using Sunny.UI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using TDJS_Vision.Diagnostics;
using TDJS_Vision.Forms.DispShowImage;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using static TDJS_Vision.Node._7_ResultProcessing.ImageSave.ImageQueueProcessor;

namespace TDJS_Vision.Node._7_ResultProcessing.ImageSave
{
    public class NodeImageSave : NodeBase
    {
        /// <summary>保存图像后台队列处理器。</summary>
        private readonly ImageQueueProcessor processor;

        public NodeImageSave(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType) 
        {
            ParamForm = new ParamFormImageSave();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultImageSave();;
            processor = new ImageQueueProcessor(4, $"{nodeId}.{nodeName}");
            // 开始处理队列
            processor.StartProcessing();
        }

        /// <summary>
        /// 释放保存图像节点时停止后台保存队列，避免反复加载方案后遗留工作线程。
        /// </summary>
        /// <param name="disposing">是否释放托管资源。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                processor?.StopProcessing();

            base.Dispose(disposing);
        }

        /// <summary>
        /// 节点运行
        /// </summary>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;

            // 参数合法性校验
            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return new NodeReturn(NodeRunFlag.StopRun);
            }
            if (ParamForm.Params == null)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行参数未设置或保存！");
            }

            if (ParamForm is ParamFormImageSave form)
            {
                if(form.Params is NodeParamSaveImage param)
                {
                    try
                    {
                        SetStatus(NodeStatus.Unexecuted, "*");
                        await base.CheckTokenCancel(token);

                        Stopwatch enqueueStopwatch = Stopwatch.StartNew();
                        string barcode = string.Empty;
                        ImageSaveJudgment imageSaveJudgment = null;
                        OutputImage outputImage;

                        // 参数获取订阅控件的值
                        if (param.IsBarCode)
                        {
                            barcode = form.GetBarCode();
                            if (barcode.IsNullOrEmpty())
                                throw new Exception($"获取条码结果失败！");
                        }
                        if (param.NeedOkNg)
                            imageSaveJudgment = form.GetImageSaveJudgment();
                        outputImage = form.GetOutputImageForSave();

                        // 参数合法性判断
                        if (param.SavePath.IsNullOrEmpty())
                            throw new Exception($"存图路径未设置！");

                        // 保存图像节点只做轻量任务投递，耗时的 ToBitmap、标注绘制、压缩和写盘都由后台队列处理。
                        SaveImageEnqueueResult enqueueResult = EnqueueSaveImage(param, outputImage, imageSaveJudgment, barcode);
                        long enqueueElapsed = enqueueStopwatch.ElapsedMilliseconds;
                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        Result.RunTime = time;
                        LogHelper.AddLog(
                            MsgLevel.Debug,
                            $"【性能诊断-保存图像快速入队】节点({ID}.{NodeName}) 节点计时={time}ms；快速入队={enqueueElapsed}ms；路径决策={enqueueResult.PathDecisionMs}ms；队列投递={enqueueResult.EnqueueMs}ms；目标路径={enqueueResult.TargetPathCount}；入队任务={enqueueResult.EnqueuedTaskCount}；已跳过={enqueueResult.Skipped}；当前队列={processor.QueuedCount}；图像={PerformanceSpikeDiagnostics.GetOutputImageText(outputImage)}",
                            true);
                        if (showLog)
                            LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);
                        return new NodeReturn(NodeRunFlag.ContinueRun);
                    }
                    catch (OperationCanceledException)
                    {
                        LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                        SetRunResult(startTime, NodeStatus.Unexecuted);
                        throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
                    }
                    catch (Exception ex)
                    {
                        LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                        SetRunResult(startTime, NodeStatus.Failed);
                        throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
                    }

                }
            }
            return new NodeReturn(NodeRunFlag.StopRun);
        }

        /// <summary>
        /// 保存图像主流程入队结果，用于诊断主流程是否只做轻量投递。
        /// </summary>
        private struct SaveImageEnqueueResult
        {
            /// <summary>实际投递到后台队列的任务数量。</summary>
            public int EnqueuedTaskCount;
            /// <summary>当前保存规则命中的目标目录数量。</summary>
            public int TargetPathCount;
            /// <summary>保存路径规则判断耗时。</summary>
            public long PathDecisionMs;
            /// <summary>队列投递耗时。</summary>
            public long EnqueueMs;
            /// <summary>当前图片是否因保存规则未命中而跳过。</summary>
            public bool Skipped;
        }

        /// <summary>
        /// 将保存图像任务快速投递到后台队列；当前方法不做 ToBitmap、不绘制标注、不写磁盘。
        /// </summary>
        /// <param name="param">保存图像节点参数。</param>
        /// <param name="outputImage">订阅得到的输出图像。</param>
        /// <param name="imageSaveJudgment">用于 OK/NG 目录判断的统一判定结果。</param>
        /// <param name="barcode">条码命名值。</param>
        /// <returns>主流程入队诊断结果。</returns>
        private SaveImageEnqueueResult EnqueueSaveImage(NodeParamSaveImage param, OutputImage outputImage, ImageSaveJudgment imageSaveJudgment, string barcode)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            DateTime time = DateTime.Now;
            // 1.存图路径（不包含图片名称）
            string imageSavePath;

            // 2.拼接日期路径
            string nowDataTime = time.ToString("yyyyMMdd");
            imageSavePath = Path.Combine(param.SavePath, nowDataTime);

            // 3.图片名称
            string imageName = param.IsBarCode ? barcode : time.ToString("yyyy-MM-dd_HH-mm-ss-fff");
            if (param.NeedCompress)
                imageName += "-CAN"; // 压缩图


            // 4.是否区分早晚班
            if (param.IsDayNight)
            {
                string dayNight = IsDayWorking(time.TimeOfDay, param.DayDataTime, param.NightDataTime) ? "早班" : "晚班";
                imageSavePath = Path.Combine(imageSavePath, dayNight);
            }

            List<string> paths = new List<string>();

            // 5.是否区分OK/NG
            if (param.NeedOkNg)
            {
                if (imageSaveJudgment == null)
                    throw new Exception($"无法获取或解析 OK/NG 判定结果！");

                // 根据统一判定结果创建 OK/NG 文件夹，兼容 AI 结果和多条件布尔结果。
                bool allAreOk = imageSaveJudgment.IsOk;
                string okNg = allAreOk ? "OK" : "NG";
                imageSavePath = Path.Combine(imageSavePath, okNg);

                //要保存什么图片
                switch (param.ImageTypeToSave)
                {
                    case ImageTypeToSave.OkAndNg:
                        if (allAreOk)
                            paths.Add(imageSavePath);
                        else
                            AddNgImagePaths(paths, imageSavePath, imageSaveJudgment);
                        break;
                    case ImageTypeToSave.OnlyOk:
                        if (allAreOk)
                            paths.Add(imageSavePath);
                        break;
                    case ImageTypeToSave.OnlyNg:
                        if (!allAreOk)
                            AddNgImagePaths(paths, imageSavePath, imageSaveJudgment);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                paths.Add(imageSavePath);
            }

            long afterPathDecision = stopwatch.ElapsedMilliseconds;
            if (paths.Count == 0)
            {
                return new SaveImageEnqueueResult
                {
                    EnqueuedTaskCount = 0,
                    TargetPathCount = 0,
                    PathDecisionMs = afterPathDecision,
                    EnqueueMs = stopwatch.ElapsedMilliseconds - afterPathDecision,
                    Skipped = true
                };
            }

            Mat sourceImage = outputImage.Bitmaps[0];
            SaveImageTask saveImageTask = QueueImageForSave(sourceImage, outputImage.DisplayResult, paths, imageName, param.NeedCompress, param.CompressValue);
            processor.EnqueueImage(saveImageTask);

            return new SaveImageEnqueueResult
            {
                EnqueuedTaskCount = 1,
                TargetPathCount = paths.Count,
                PathDecisionMs = afterPathDecision,
                EnqueueMs = stopwatch.ElapsedMilliseconds - afterPathDecision,
                Skipped = false
            };
        }

        /// <summary>
        /// 将 NG 检测项对应的子目录追加到目标路径集合；没有检测项分类时按配置回落到通用 NG 目录。
        /// </summary>
        /// <param name="paths">目标路径集合。</param>
        /// <param name="imageSavePath">OK/NG 根路径。</param>
        /// <param name="imageSaveJudgment">统一保存判定结果。</param>
        private static void AddNgImagePaths(List<string> paths, string imageSavePath, ImageSaveJudgment imageSaveJudgment)
        {
            if (paths == null || imageSaveJudgment == null)
                return;

            if (imageSaveJudgment.NgCategoryNames.Count == 0)
            {
                if (imageSaveJudgment.UseGenericNgDirectoryWhenNoCategory)
                    paths.Add(imageSavePath);

                return;
            }

            foreach (string categoryName in imageSaveJudgment.NgCategoryNames)
                paths.Add(Path.Combine(imageSavePath, categoryName));
        }

        /// <summary>
        /// 生成后台保存任务，任务只持有图像引用和保存元数据，不在主流程转 Bitmap。
        /// </summary>
        /// <param name="sourceImage">待保存的源 Mat 引用。</param>
        /// <param name="displayResult">需要写入保存图的显示结果。</param>
        /// <param name="targetDirectories">保存目标目录集合。</param>
        /// <param name="imageName">图片名称，不含扩展名。</param>
        /// <param name="needCompress">是否压缩。</param>
        /// <param name="compressValue">压缩质量。</param>
        /// <returns>后台保存任务。</returns>
        private SaveImageTask QueueImageForSave(Mat sourceImage, AlgorithmResult displayResult, List<string> targetDirectories, string imageName, bool needCompress, long compressValue = 100)
        {
            // 创建保存任务
            SaveImageTask saveTask = new SaveImageTask
            {
                SourceImage = sourceImage,
                DisplayResult = displayResult,
                TargetDirectories = targetDirectories == null ? new List<string>() : new List<string>(targetDirectories),
                ImageName = imageName,
                NeedCompress = needCompress,
                CompressValue = compressValue
            };

            return saveTask;
        }

        /// <summary>
        /// 判断是否早班
        /// </summary>
        /// <param name="time"></param>
        /// <param name="daydateTime"></param>
        /// <param name="nightdataTime"></param>
        /// <returns></returns>
        private bool IsDayWorking(TimeSpan time, DateTime daydateTime, DateTime nightdataTime)
        {
            TimeSpan dayStartTime = daydateTime.TimeOfDay;
            TimeSpan nightStartTime = nightdataTime.TimeOfDay;
            return time >= dayStartTime && time < nightStartTime;
        }

        /// <summary>
        /// 获取保存图片类型的中文文本，便于现场日志阅读。
        /// </summary>
        /// <param name="imageTypeToSave">保存图片类型。</param>
        /// <returns>保存图片类型中文文本。</returns>
        private static string GetImageTypeText(ImageTypeToSave imageTypeToSave)
        {
            switch (imageTypeToSave)
            {
                case ImageTypeToSave.OkAndNg:
                    return "OK和NG";
                case ImageTypeToSave.OnlyOk:
                    return "仅OK";
                case ImageTypeToSave.OnlyNg:
                    return "仅NG";
                default:
                    return imageTypeToSave.ToString();
            }
        }

    }

    public class ImageQueueProcessor
    {
        private readonly BlockingCollection<SaveImageTask> _imageQueue = new BlockingCollection<SaveImageTask>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly int _workerCount; // 工作线程的数量
        private readonly string _ownerText;
        private int _started;
        private int _stopped;

        /// <summary>当前等待保存的图像数量。</summary>
        public int QueuedCount => _imageQueue.Count;

        /// <summary>后台保存工作线程数量。</summary>
        public int WorkerCount => _workerCount;

        /// <summary>
        /// 后台保存任务；只借用流程输出 Mat 引用，后台线程自行转换 Bitmap 并负责释放生成的 Bitmap。
        /// </summary>
        public sealed class SaveImageTask
        {
            /// <summary>待保存的源图像引用，保存队列不接管该 Mat 的释放权。</summary>
            public Mat SourceImage { get; set; }
            /// <summary>需要写入保存图的显示叠加结果。</summary>
            public AlgorithmResult DisplayResult { get; set; }
            /// <summary>保存目标目录集合。</summary>
            public List<string> TargetDirectories { get; set; } = new List<string>();
            /// <summary>图片名称，不包含扩展名。</summary>
            public string ImageName { get; set; }
            /// <summary>是否需要压缩。</summary>
            public bool NeedCompress { get; set; }
            /// <summary>压缩质量。</summary>
            public long CompressValue { get; set; }
        }

        public ImageQueueProcessor(int workerCount, string ownerText)
        {
            if (workerCount <= 0) throw new ArgumentOutOfRangeException(nameof(workerCount), "Worker count must be greater than zero.");
            _workerCount = workerCount;
            _ownerText = ownerText;
        }

        //开始存图
        public void StartProcessing()
        {
            if (Interlocked.Exchange(ref _started, 1) == 1)
                return;

            for (int i = 0; i < _workerCount; i++)
            {
                Task.Run(() => ProcessImages(_cancellationTokenSource.Token));
            }

            LogHelper.AddLog(
                MsgLevel.Debug,
                $"【内存诊断-保存队列启动】节点({_ownerText}) 工作线程={_workerCount}；内存={PerformanceSpikeDiagnostics.GetMemoryText()}",
                true);
        }

        //添加任务
        public void EnqueueImage(SaveImageTask saveImagedata)
        {
            if (saveImagedata == null) throw new ArgumentNullException(nameof(saveImagedata), "保存任务不能为空。");
            if (!OutputImage.HasValidImage(saveImagedata.SourceImage)) throw new ArgumentException("保存任务的源图像为空。", nameof(saveImagedata));
            if (saveImagedata.TargetDirectories == null || saveImagedata.TargetDirectories.Count == 0) throw new ArgumentException("保存任务的目标目录为空。", nameof(saveImagedata));
            if (_imageQueue.IsAddingCompleted || _stopped == 1)
                throw new InvalidOperationException("保存队列已停止，不能继续入队。");

            //添加任务(生产者生产数据)
            _imageQueue.Add(saveImagedata);
        }

        /// <summary>
        /// 线程处理图片
        /// </summary>
        /// <param name="cancellationToken">令牌，标记线程是否取消执行</param>
        private void ProcessImages(CancellationToken cancellationToken)
        {
            //_imageQueue.GetConsumingEnumerable(cancellationToken):按顺序消费数据，并且可以在取消请求时停止消费。(消费者消费数据)
            foreach (var saveImagedata in _imageQueue.GetConsumingEnumerable(cancellationToken))
            {
                string imageInfo = PerformanceSpikeDiagnostics.GetMatText(saveImagedata.SourceImage);
                Bitmap bitmap = null;
                MemorySnapshot beforeSaveSnapshot = PerformanceSpikeDiagnostics.CaptureMemorySnapshot();
                Stopwatch stopwatch = Stopwatch.StartNew();
                try
                {
                    bitmap = CreateBitmapForSave(saveImagedata, stopwatch, out SaveImageRenderTiming renderTiming);
                    MemorySnapshot afterRenderSnapshot = PerformanceSpikeDiagnostics.CaptureMemorySnapshot();
                    LogHelper.AddLog(
                        MsgLevel.Debug,
                        $"【内存诊断-保存图像后台取图】节点({_ownerText}) 源图={imageInfo}；待保存Bitmap={PerformanceSpikeDiagnostics.GetBitmapText(bitmap)}；有标注={renderTiming.HasDrawableDisplayResult}；ToBitmap={renderTiming.ToBitmapMs}ms；确保可绘制={renderTiming.EnsureDrawableMs}ms；写入标注={renderTiming.DrawDisplayResultMs}ms；后台取图总耗时={renderTiming.TotalMs}ms；目标路径={saveImagedata.TargetDirectories.Count}；私有内存变化={afterRenderSnapshot.PrivateMemoryMb - beforeSaveSnapshot.PrivateMemoryMb:F1}MB；GDI变化={afterRenderSnapshot.GdiObjectCount - beforeSaveSnapshot.GdiObjectCount}；前={beforeSaveSnapshot.ToLogText()}；后={afterRenderSnapshot.ToLogText()}",
                        true);

                    foreach (string targetDirectory in saveImagedata.TargetDirectories)
                    {
                        long beforePath = stopwatch.ElapsedMilliseconds;
                        Directory.CreateDirectory(targetDirectory);
                        string fileName = GetFileName(targetDirectory, saveImagedata.ImageName);
                        long afterPath = stopwatch.ElapsedMilliseconds;

                        //处理图片(压缩图片)
                        SaveWithCompress(bitmap, fileName, saveImagedata.NeedCompress, saveImagedata.CompressValue);
                        long afterSave = stopwatch.ElapsedMilliseconds;
                        MemorySnapshot afterSaveSnapshot = PerformanceSpikeDiagnostics.CaptureMemorySnapshot();
                        LogHelper.AddLog(
                            MsgLevel.Debug,
                            $"【内存诊断-保存队列消费】节点({_ownerText}) 保存完成；路径={fileName}；源图={imageInfo}；保存Bitmap={PerformanceSpikeDiagnostics.GetBitmapText(bitmap)}；压缩={saveImagedata.NeedCompress}；路径准备={afterPath - beforePath}ms；保存耗时={afterSave - afterPath}ms；后台总耗时={afterSave}ms；队列剩余={QueuedCount}；私有内存变化={afterSaveSnapshot.PrivateMemoryMb - beforeSaveSnapshot.PrivateMemoryMb:F1}MB；GDI变化={afterSaveSnapshot.GdiObjectCount - beforeSaveSnapshot.GdiObjectCount}；前={beforeSaveSnapshot.ToLogText()}；后={afterSaveSnapshot.ToLogText()}",
                            true);
                    }
                }
                catch (OperationCanceledException)
                {
                    // 如果取消了任务，则忽略这个异常
                }
                catch (Exception ex)
                {
                    LogHelper.AddLog(MsgLevel.Exception, $"压缩图像失败: {ex.Message}");
                }
                finally
                {
                    MemorySnapshot beforeDisposeSnapshot = PerformanceSpikeDiagnostics.CaptureMemorySnapshot();
                    bitmap?.Dispose();
                    MemorySnapshot afterDisposeSnapshot = PerformanceSpikeDiagnostics.CaptureMemorySnapshot();
                    LogHelper.AddLog(
                        MsgLevel.Debug,
                        $"【内存诊断-保存队列释放】节点({_ownerText}) 已释放后台生成Bitmap；源图={imageInfo}；释放后队列={QueuedCount}；私有内存变化={afterDisposeSnapshot.PrivateMemoryMb - beforeDisposeSnapshot.PrivateMemoryMb:F1}MB；GDI变化={afterDisposeSnapshot.GdiObjectCount - beforeDisposeSnapshot.GdiObjectCount}；前={beforeDisposeSnapshot.ToLogText()}；后={afterDisposeSnapshot.ToLogText()}",
                        true);
                }
            }
        }

        /// <summary>
        /// 后台生成保存 Bitmap 的分段耗时。
        /// </summary>
        private struct SaveImageRenderTiming
        {
            /// <summary>Mat 转 Bitmap 耗时。</summary>
            public long ToBitmapMs;
            /// <summary>确保 Bitmap 可绘制耗时。</summary>
            public long EnsureDrawableMs;
            /// <summary>写入显示叠加层耗时。</summary>
            public long DrawDisplayResultMs;
            /// <summary>后台取图总耗时。</summary>
            public long TotalMs;
            /// <summary>是否存在可绘制显示结果。</summary>
            public bool HasDrawableDisplayResult;
        }

        /// <summary>
        /// 在保存后台线程中将 Mat 转成待保存 Bitmap，并按需写入显示标注。
        /// </summary>
        /// <param name="task">后台保存任务。</param>
        /// <param name="stopwatch">当前任务计时器。</param>
        /// <param name="timing">输出分段耗时。</param>
        /// <returns>后台线程独占并负责释放的 Bitmap。</returns>
        private static Bitmap CreateBitmapForSave(SaveImageTask task, Stopwatch stopwatch, out SaveImageRenderTiming timing)
        {
            timing = new SaveImageRenderTiming();
            long beforeToBitmap = stopwatch.ElapsedMilliseconds;
            Bitmap bitmap = task.SourceImage.ToBitmap();
            long afterToBitmap = stopwatch.ElapsedMilliseconds;
            timing.ToBitmapMs = afterToBitmap - beforeToBitmap;

            timing.HasDrawableDisplayResult = ParamFormImageSave.HasDrawableDisplayResult(task.DisplayResult);
            if (timing.HasDrawableDisplayResult)
            {
                bitmap = ParamFormImageSave.EnsureDrawableBitmap(bitmap);
                long afterEnsureDrawable = stopwatch.ElapsedMilliseconds;
                timing.EnsureDrawableMs = afterEnsureDrawable - afterToBitmap;
                ShowImageControl.DrawDisplayResultToBitmap(bitmap, task.DisplayResult);
                long afterDrawDisplayResult = stopwatch.ElapsedMilliseconds;
                timing.DrawDisplayResultMs = afterDrawDisplayResult - afterEnsureDrawable;
                timing.TotalMs = afterDrawDisplayResult;
            }
            else
            {
                timing.TotalMs = afterToBitmap;
            }

            return bitmap;
        }

        // 保存图片并压缩
        private void SaveWithCompress(Bitmap bitmap, string imagePath, bool isCompress, long compressValue = 100)
        {
            if (isCompress)
            {
                using (EncoderParameters encoderParams = new EncoderParameters(1))
                using (EncoderParameter qualityParameter = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, compressValue))
                {
                    encoderParams.Param[0] = qualityParameter;
                    ImageCodecInfo jpegCodec = GetEncoderInfo("image/jpeg");
                    bitmap.Save(imagePath, jpegCodec, encoderParams);
                }
            }
            else
            {
                bitmap.Save(imagePath);
            }
        }

        /// <summary>
        /// 生成不覆盖已有文件的保存路径。
        /// </summary>
        /// <param name="filePath">保存目录。</param>
        /// <param name="fileName">图片名称，不含扩展名。</param>
        /// <returns>最终保存文件路径。</returns>
        private static string GetFileName(string filePath, string fileName)
        {
            string newFileName = fileName;
            string fullPath = Path.Combine(filePath, newFileName + ".jpg");
            if (File.Exists(fullPath))
            {
                int suffix = 1;
                do
                {
                    newFileName = $"{fileName}_{suffix}";
                    fullPath = Path.Combine(filePath, newFileName + ".jpg");
                    suffix++;
                }
                while (File.Exists(fullPath));
            }
            return fullPath;
        }

        private ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            int j;
            ImageCodecInfo[] encoders;
            encoders = ImageCodecInfo.GetImageEncoders();
            for (j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }

        public void StopProcessing()
        {
            if (Interlocked.Exchange(ref _stopped, 1) == 1)
                return;

            _imageQueue.CompleteAdding(); // 标记不再添加新的元素
            _cancellationTokenSource.Cancel(); // 取消所有任务
            int disposedPendingCount = DisposePendingImages();
            LogHelper.AddLog(
                MsgLevel.Debug,
                $"【内存诊断-保存队列停止】节点({_ownerText}) 停止保存队列；已释放待保存图片={disposedPendingCount}；剩余队列={QueuedCount}；内存={PerformanceSpikeDiagnostics.GetMemoryText()}",
                true);
        }

        /// <summary>
        /// 停止队列时释放尚未被消费线程取走的Bitmap，避免关闭或重开方案时遗留待保存图。
        /// </summary>
        /// <returns>已释放的待保存图片数量。</returns>
        private int DisposePendingImages()
        {
            int disposedCount = 0;
            while (_imageQueue.TryTake(out SaveImageTask pendingTask))
            {
                // 保存任务只借用上游 Mat 引用，停止队列时不能在这里释放上游图像。
                if (pendingTask != null)
                    disposedCount++;
            }

            return disposedCount;
        }
    }
}
