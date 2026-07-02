using Logger;
using Sunny.UI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Diagnostics;
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
                        base.CheckTokenCancel(token);

                        // 参数获取订阅控件的值
                        if (param.IsBarCode)
                        {
                            param.Barcode = form.GetBarCode();
                            if (param.Barcode.IsNullOrEmpty())
                                throw new Exception($"获取条码结果失败！");
                        }
                        if (param.NeedOkNg)
                            param.AlgorithmResult = form.GetAiResult();
                        param.Image = form.GetImage();

                        // 参数合法性判断
                        if (param.SavePath.IsNullOrEmpty())
                            throw new Exception($"存图路径未设置！");
                        if (param.Image == null)
                            throw new Exception($"订阅的图片对象为空！");

                        // 异步队列存图
                        await SaveImage(param);
                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        Result.RunTime = time;
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

        private async Task SaveImage(NodeParamSaveImage param)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            MemorySnapshot beforeSnapshot = PerformanceSpikeDiagnostics.CaptureMemorySnapshot();
            DateTime time = DateTime.Now;
            bool originalImageTransferred = false;
            int enqueuedCount = 0;
            int clonedImageCount = 0;
            // 1.存图路径（不包含图片名称）
            string imageSavePath;

            try
            {
                // 2.拼接日期路径
                string nowDataTime = time.ToString("yyyyMMdd");
                imageSavePath = Path.Combine(param.SavePath, nowDataTime);

                // 3.图片名称
                string imageName = param.IsBarCode ? param.Barcode : time.ToString("yyyy-MM-dd_HH-mm-ss-fff");
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
                    if (param.AlgorithmResult == null)
                        throw new Exception($"无法获取/解析AI检测结果！");

                    // 根据AI检测总结果判断创建OK/NG文件夹
                    bool allAreOk = param.AlgorithmResult.DetectResults.Count > 0 && param.AlgorithmResult.DetectResults.All(pair => pair.Value.All(v => v.IsOk));
                    string okNg = allAreOk ? "OK" : "NG";
                    imageSavePath = Path.Combine(imageSavePath, okNg);

                    //要保存什么图片
                    switch (param.ImageTypeToSave)
                    {
                        case ImageTypeToSave.OkAndNg:
                            if (allAreOk)
                            {
                                paths.Add(imageSavePath);
                            }
                            else
                            {
                                foreach (var pair in param.AlgorithmResult.DetectResults)
                                {
                                    if (!pair.Value.TrueForAll(item => item.IsOk))
                                        paths.Add(Path.Combine(imageSavePath, pair.Key));
                                }
                            }
                            break;
                        case ImageTypeToSave.OnlyOk:
                            if (allAreOk)
                            {
                                paths.Add(imageSavePath);
                            }
                            break;
                        case ImageTypeToSave.OnlyNg:
                            if (!allAreOk)
                            {
                                foreach (var pair in param.AlgorithmResult.DetectResults)
                                {
                                    if (!pair.Value.TrueForAll(item => item.IsOk))
                                        paths.Add(Path.Combine(imageSavePath, pair.Key));
                                }
                            }
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
                LogHelper.AddLog(
                    MsgLevel.Info,
                    $"【内存诊断-保存图像入队准备】节点({ID}.{NodeName}) 图片={PerformanceSpikeDiagnostics.GetBitmapText(param.Image)}；保存类型={GetImageTypeText(param.ImageTypeToSave)}；区分OKNG={param.NeedOkNg}；压缩={param.NeedCompress}；目标路径数={paths.Count}；当前队列={processor.QueuedCount}；路径决策耗时={afterPathDecision}ms；内存={PerformanceSpikeDiagnostics.GetMemoryText()}",
                    true);

                if (paths.Count == 0)
                {
                    LogHelper.AddLog(
                        MsgLevel.Info,
                        $"【内存诊断-保存图像跳过】节点({ID}.{NodeName}) 当前图片按保存规则不需要入队，将释放临时Bitmap；保存类型={GetImageTypeText(param.ImageTypeToSave)}；区分OKNG={param.NeedOkNg}；图片={PerformanceSpikeDiagnostics.GetBitmapText(param.Image)}；内存={PerformanceSpikeDiagnostics.GetMemoryText()}",
                        true);
                    return;
                }

                for (int i = 0; i < paths.Count; i++)
                {
                    string targetPath = paths[i];
                    Bitmap taskBitmap = CreateBitmapForSaveTask(param.Image, i, paths.Count, out bool cloned);
                    if (cloned)
                        clonedImageCount++;

                    bool queued = false;
                    bool usesOriginalImage = ReferenceEquals(taskBitmap, param.Image);
                    SaveImageTask saveImageTask = await Task.Run(() =>
                    {
                        return QueueImageForSave(taskBitmap, targetPath, imageName, param.NeedCompress, param.CompressValue);
                    });

                    try
                    {
                        processor.EnqueueImage(saveImageTask);
                        queued = true;
                        enqueuedCount++;
                        if (usesOriginalImage)
                        {
                            originalImageTransferred = true;
                            param.Image = null;
                        }
                    }
                    finally
                    {
                        if (!queued && !usesOriginalImage)
                            taskBitmap?.Dispose();
                    }
                }

                MemorySnapshot afterEnqueueSnapshot = PerformanceSpikeDiagnostics.CaptureMemorySnapshot();
                LogHelper.AddLog(
                    MsgLevel.Info,
                    $"【内存诊断-保存图像入队完成】节点({ID}.{NodeName}) 入队={enqueuedCount}；克隆Bitmap={clonedImageCount}；原图已转移={originalImageTransferred}；当前队列={processor.QueuedCount}；总耗时={stopwatch.ElapsedMilliseconds}ms；私有内存变化={afterEnqueueSnapshot.PrivateMemoryMb - beforeSnapshot.PrivateMemoryMb:F1}MB；GDI变化={afterEnqueueSnapshot.GdiObjectCount - beforeSnapshot.GdiObjectCount}；前={beforeSnapshot.ToLogText()}；后={afterEnqueueSnapshot.ToLogText()}",
                    true);
            }
            finally
            {
                if (!originalImageTransferred && param.Image != null)
                {
                    string imageInfo = PerformanceSpikeDiagnostics.GetBitmapText(param.Image);
                    MemorySnapshot beforeDisposeSnapshot = PerformanceSpikeDiagnostics.CaptureMemorySnapshot();
                    param.Image.Dispose();
                    param.Image = null;
                    MemorySnapshot afterDisposeSnapshot = PerformanceSpikeDiagnostics.CaptureMemorySnapshot();
                    LogHelper.AddLog(
                        MsgLevel.Info,
                        $"【内存诊断-保存图像释放】节点({ID}.{NodeName}) 原始临时Bitmap未转移给队列，已释放；图片={imageInfo}；私有内存变化={afterDisposeSnapshot.PrivateMemoryMb - beforeDisposeSnapshot.PrivateMemoryMb:F1}MB；GDI变化={afterDisposeSnapshot.GdiObjectCount - beforeDisposeSnapshot.GdiObjectCount}；前={beforeDisposeSnapshot.ToLogText()}；后={afterDisposeSnapshot.ToLogText()}",
                        true);
                }
            }
        }

        /// <summary>
        /// 为保存任务创建独立的Bitmap所有权，多目标保存时避免同一个Bitmap被多个线程同时保存和释放。
        /// </summary>
        /// <param name="source">原始待保存位图。</param>
        /// <param name="pathIndex">当前保存路径索引。</param>
        /// <param name="pathCount">总保存路径数量。</param>
        /// <param name="cloned">返回是否创建了克隆图。</param>
        /// <returns>当前保存任务独占的位图。</returns>
        private static Bitmap CreateBitmapForSaveTask(Bitmap source, int pathIndex, int pathCount, out bool cloned)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source), "待保存图片为空。");

            bool useOriginalImage = pathCount <= 1 || pathIndex == pathCount - 1;
            cloned = !useOriginalImage;
            return useOriginalImage ? source : new Bitmap(source);
        }

        /// <summary>
        /// 生成任务
        /// </summary>
        /// <param name="image"></param>
        /// <param name="savePath"></param>
        /// <param name="imageName"></param>
        /// <param name="needCompress"></param>
        /// <param name="compressValue"></param>
        /// <returns></returns>
        private SaveImageTask QueueImageForSave(Bitmap image, string savePath, string imageName, bool needCompress, long compressValue = 100)
        {
            Directory.CreateDirectory(savePath);

            // 生成保存图片的绝对路径
            string fileName = GetFileName(savePath, imageName);

            // 创建保存任务
            SaveImageTask saveTask = new SaveImageTask
            {
                Image = image,
                Path = fileName,
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

        private string GetFileName(string filePath, string fileName)
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

        public struct SaveImageTask
        {
            public Bitmap Image { get; set; }
            public string Path { get; set; }
            public bool NeedCompress { get; set; } //是否需要压缩
            public long CompressValue { get; set; } //压缩阈值
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
                MsgLevel.Info,
                $"【内存诊断-保存队列启动】节点({_ownerText}) 工作线程={_workerCount}；内存={PerformanceSpikeDiagnostics.GetMemoryText()}",
                true);
        }

        //添加任务
        public void EnqueueImage(SaveImageTask saveImagedata)
        {
            if (saveImagedata.Image == null) throw new ArgumentNullException(nameof(saveImagedata), "Cannot enqueue a null image.");
            if (_imageQueue.IsAddingCompleted || _stopped == 1)
                throw new InvalidOperationException("保存队列已停止，不能继续入队。");

            //添加任务(生产者生产数据)
            _imageQueue.Add(saveImagedata);
            LogHelper.AddLog(
                MsgLevel.Info,
                $"【内存诊断-保存队列入队】节点({_ownerText}) 队列长度={QueuedCount}；路径={saveImagedata.Path}；图片={PerformanceSpikeDiagnostics.GetBitmapText(saveImagedata.Image)}；压缩={saveImagedata.NeedCompress}；内存={PerformanceSpikeDiagnostics.GetMemoryText()}",
                true);
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
                string imageInfo = PerformanceSpikeDiagnostics.GetBitmapText(saveImagedata.Image);
                MemorySnapshot beforeSaveSnapshot = PerformanceSpikeDiagnostics.CaptureMemorySnapshot();
                Stopwatch stopwatch = Stopwatch.StartNew();
                try
                {
                    //处理图片(压缩图片)
                    SaveWithCompress(saveImagedata.Image, saveImagedata.Path, saveImagedata.NeedCompress, saveImagedata.CompressValue);
                    MemorySnapshot afterSaveSnapshot = PerformanceSpikeDiagnostics.CaptureMemorySnapshot();
                    LogHelper.AddLog(
                        MsgLevel.Info,
                        $"【内存诊断-保存队列消费】节点({_ownerText}) 保存完成；路径={saveImagedata.Path}；图片={imageInfo}；压缩={saveImagedata.NeedCompress}；保存耗时={stopwatch.ElapsedMilliseconds}ms；队列剩余={QueuedCount}；私有内存变化={afterSaveSnapshot.PrivateMemoryMb - beforeSaveSnapshot.PrivateMemoryMb:F1}MB；GDI变化={afterSaveSnapshot.GdiObjectCount - beforeSaveSnapshot.GdiObjectCount}；前={beforeSaveSnapshot.ToLogText()}；后={afterSaveSnapshot.ToLogText()}",
                        true);
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
                    saveImagedata.Image.Dispose();
                    MemorySnapshot afterDisposeSnapshot = PerformanceSpikeDiagnostics.CaptureMemorySnapshot();
                    LogHelper.AddLog(
                        MsgLevel.Info,
                        $"【内存诊断-保存队列释放】节点({_ownerText}) 已释放保存任务Bitmap；路径={saveImagedata.Path}；图片={imageInfo}；释放后队列={QueuedCount}；私有内存变化={afterDisposeSnapshot.PrivateMemoryMb - beforeDisposeSnapshot.PrivateMemoryMb:F1}MB；GDI变化={afterDisposeSnapshot.GdiObjectCount - beforeDisposeSnapshot.GdiObjectCount}；前={beforeDisposeSnapshot.ToLogText()}；后={afterDisposeSnapshot.ToLogText()}",
                        true);
                }
            }
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
                MsgLevel.Info,
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
                try
                {
                    pendingTask.Image?.Dispose();
                    disposedCount++;
                }
                catch
                {
                }
            }

            return disposedCount;
        }
    }
}
