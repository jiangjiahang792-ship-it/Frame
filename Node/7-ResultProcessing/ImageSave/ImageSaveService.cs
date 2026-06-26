using Logger;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;

namespace TDJS_Vision.Node._7_ResultProcessing.ImageSave
{
    /// <summary>
    /// 图像保存优先级，队列满时高优先级请求可以淘汰普通优先级请求。
    /// </summary>
    public enum ImageSavePriority
    {
        /// <summary>
        /// 普通优先级，通常用于 OK 图或未区分 OK/NG 的普通记录图。
        /// </summary>
        Normal = 0,

        /// <summary>
        /// 高优先级，通常用于 NG 图或必须优先追溯的关键图。
        /// </summary>
        High = 1
    }

    /// <summary>
    /// 图像保存入队状态。
    /// </summary>
    public enum ImageSaveEnqueueStatus
    {
        /// <summary>
        /// 已成功加入后台保存队列。
        /// </summary>
        Accepted,

        /// <summary>
        /// 已成功加入后台保存队列，同时淘汰了一个普通优先级请求。
        /// </summary>
        AcceptedAfterDropNormal,

        /// <summary>
        /// 当前图片不满足保存条件，例如只存 OK 但当前结果为 NG。
        /// </summary>
        SkippedByCondition,

        /// <summary>
        /// 当前请求因为队列已满被丢弃。
        /// </summary>
        DroppedQueueFull,

        /// <summary>
        /// 当前请求无有效图片或无有效保存目录，被丢弃。
        /// </summary>
        DroppedInvalid
    }

    /// <summary>
    /// 图像保存入队结果。
    /// </summary>
    public class ImageSaveEnqueueResult
    {
        /// <summary>
        /// 入队状态。
        /// </summary>
        public ImageSaveEnqueueStatus Status { get; private set; }

        /// <summary>
        /// 当前队列长度。
        /// </summary>
        public int QueueLength { get; private set; }

        /// <summary>
        /// 结果说明。
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// 初始化图像保存入队结果。
        /// </summary>
        /// <param name="status">入队状态。</param>
        /// <param name="queueLength">当前队列长度。</param>
        /// <param name="message">结果说明。</param>
        public ImageSaveEnqueueResult(ImageSaveEnqueueStatus status, int queueLength, string message)
        {
            Status = status;
            QueueLength = queueLength;
            Message = message;
        }

        /// <summary>
        /// 当前结果是否表示请求已加入保存队列。
        /// </summary>
        public bool IsAccepted
        {
            get
            {
                return Status == ImageSaveEnqueueStatus.Accepted ||
                    Status == ImageSaveEnqueueStatus.AcceptedAfterDropNormal;
            }
        }
    }

    /// <summary>
    /// 图像保存队列运行状态。
    /// </summary>
    public class ImageSaveQueueStatus
    {
        /// <summary>
        /// 当前排队请求数量。
        /// </summary>
        public int QueueLength { get; set; }

        /// <summary>
        /// 队列最大容量。
        /// </summary>
        public int QueueCapacity { get; set; }

        /// <summary>
        /// 普通优先级排队请求数量。
        /// </summary>
        public int NormalQueueLength { get; set; }

        /// <summary>
        /// 高优先级排队请求数量。
        /// </summary>
        public int HighQueueLength { get; set; }

        /// <summary>
        /// 已接受请求数量。
        /// </summary>
        public long AcceptedCount { get; set; }

        /// <summary>
        /// 因队列满或请求无效而丢弃的请求数量。
        /// </summary>
        public long DroppedCount { get; set; }

        /// <summary>
        /// 已完成写盘请求数量。
        /// </summary>
        public long SavedCount { get; set; }

        /// <summary>
        /// 后台写盘失败请求数量。
        /// </summary>
        public long FailedCount { get; set; }
    }

    /// <summary>
    /// 单张图像的后台保存请求。
    /// </summary>
    public class ImageSaveRequest
    {
        /// <summary>
        /// 需要保存的位图，入队成功或失败后由保存服务负责释放。
        /// </summary>
        public Bitmap Image { get; set; }

        /// <summary>
        /// 保存目录集合，不包含最终文件名。
        /// </summary>
        public List<string> SaveDirectories { get; set; } = new List<string>();

        /// <summary>
        /// 不含扩展名的图片基础名称。
        /// </summary>
        public string ImageName { get; set; }

        /// <summary>
        /// 是否按 JPEG 质量参数压缩保存。
        /// </summary>
        public bool NeedCompress { get; set; }

        /// <summary>
        /// JPEG 压缩质量。
        /// </summary>
        public long CompressValue { get; set; } = 100;

        /// <summary>
        /// 保存优先级。
        /// </summary>
        public ImageSavePriority Priority { get; set; }

        /// <summary>
        /// 是否为 NG 图。
        /// </summary>
        public bool IsNg { get; set; }

        /// <summary>
        /// 来源流程编号。
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// 来源流程名称。
        /// </summary>
        public string ProcessName { get; set; }

        /// <summary>
        /// 来源节点编号。
        /// </summary>
        public int NodeId { get; set; }

        /// <summary>
        /// 来源节点名称。
        /// </summary>
        public string NodeName { get; set; }

        /// <summary>
        /// 请求创建时间。
        /// </summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 构建用于日志展示的来源描述。
        /// </summary>
        /// <returns>来源流程和节点描述。</returns>
        public string BuildSourceText()
        {
            return $"流程【{ProcessName}】节点【{NodeId}.{NodeName}】";
        }
    }

    /// <summary>
    /// 图像保存服务接口，所有保存图片节点通过该接口投递后台保存请求。
    /// </summary>
    public interface IImageSaveService
    {
        /// <summary>
        /// 将图像保存请求加入后台队列。
        /// </summary>
        /// <param name="request">图像保存请求。</param>
        /// <returns>入队结果。</returns>
        ImageSaveEnqueueResult Enqueue(ImageSaveRequest request);

        /// <summary>
        /// 获取当前保存队列状态。
        /// </summary>
        /// <returns>保存队列状态。</returns>
        ImageSaveQueueStatus GetStatus();

        /// <summary>
        /// 启动后台保存线程。
        /// </summary>
        void Start();

        /// <summary>
        /// 停止后台保存线程。
        /// </summary>
        /// <param name="waitForDrain">是否等待队列中已有请求保存完成。</param>
        void Stop(bool waitForDrain);
    }

    /// <summary>
    /// 全局图像保存服务，使用专用后台线程和有界队列，避免保存图片占用默认线程池。
    /// </summary>
    public sealed class ImageSaveService : IImageSaveService
    {
        /// <summary>
        /// 默认全局队列容量。
        /// </summary>
        private const int DefaultQueueCapacity = 500;

        /// <summary>
        /// 默认后台保存线程数量。
        /// </summary>
        private const int DefaultWorkerCount = 2;

        /// <summary>
        /// 全局图像保存服务实例。
        /// </summary>
        private static readonly Lazy<ImageSaveService> _instance =
            new Lazy<ImageSaveService>(() => new ImageSaveService(DefaultQueueCapacity, DefaultWorkerCount), true);

        /// <summary>
        /// 队列同步锁。
        /// </summary>
        private readonly object _queueLock = new object();

        /// <summary>
        /// 高优先级保存请求队列。
        /// </summary>
        private readonly Queue<ImageSaveRequest> _highPriorityQueue = new Queue<ImageSaveRequest>();

        /// <summary>
        /// 普通优先级保存请求队列。
        /// </summary>
        private readonly Queue<ImageSaveRequest> _normalPriorityQueue = new Queue<ImageSaveRequest>();

        /// <summary>
        /// 后台线程等待新任务的信号。
        /// </summary>
        private readonly AutoResetEvent _queueSignal = new AutoResetEvent(false);

        /// <summary>
        /// 后台保存线程集合。
        /// </summary>
        private readonly List<Thread> _workers = new List<Thread>();

        /// <summary>
        /// 队列最大容量。
        /// </summary>
        private readonly int _queueCapacity;

        /// <summary>
        /// 后台保存线程数量。
        /// </summary>
        private readonly int _workerCount;

        /// <summary>
        /// 当前排队请求数量。
        /// </summary>
        private int _queuedCount;

        /// <summary>
        /// 后台线程是否已经启动。
        /// </summary>
        private bool _started;

        /// <summary>
        /// 服务是否正在停止。
        /// </summary>
        private bool _stopping;

        /// <summary>
        /// 已接受请求数量。
        /// </summary>
        private long _acceptedCount;

        /// <summary>
        /// 已丢弃请求数量。
        /// </summary>
        private long _droppedCount;

        /// <summary>
        /// 已保存请求数量。
        /// </summary>
        private long _savedCount;

        /// <summary>
        /// 保存失败请求数量。
        /// </summary>
        private long _failedCount;

        /// <summary>
        /// 获取全局图像保存服务实例。
        /// </summary>
        public static ImageSaveService Instance
        {
            get { return _instance.Value; }
        }

        /// <summary>
        /// 初始化图像保存服务。
        /// </summary>
        /// <param name="queueCapacity">队列最大容量。</param>
        /// <param name="workerCount">后台保存线程数量。</param>
        private ImageSaveService(int queueCapacity, int workerCount)
        {
            _queueCapacity = Math.Max(1, queueCapacity);
            _workerCount = Math.Max(1, workerCount);
            Start();
        }

        /// <summary>
        /// 启动后台保存线程。
        /// </summary>
        public void Start()
        {
            lock (_queueLock)
            {
                if (_started)
                    return;

                _stopping = false;
                for (int i = 0; i < _workerCount; i++)
                {
                    Thread worker = new Thread(WorkerLoop);
                    worker.IsBackground = true;
                    worker.Name = $"ImageSaveWorker-{i + 1}";
                    _workers.Add(worker);
                    worker.Start();
                }

                _started = true;
                LogHelper.AddLog(MsgLevel.Info, $"图像保存服务已启动，保存线程数:{_workerCount}，队列容量:{_queueCapacity}", true);
            }
        }

        /// <summary>
        /// 将图像保存请求加入后台队列，队列满时按策略A丢弃低优先级请求。
        /// </summary>
        /// <param name="request">图像保存请求。</param>
        /// <returns>入队结果。</returns>
        public ImageSaveEnqueueResult Enqueue(ImageSaveRequest request)
        {
            if (!IsValidRequest(request))
            {
                DisposeRequestImage(request);
                Interlocked.Increment(ref _droppedCount);
                return new ImageSaveEnqueueResult(ImageSaveEnqueueStatus.DroppedInvalid, GetStatus().QueueLength, "保存请求无有效图片或保存目录。");
            }

            ImageSaveRequest droppedRequest = null;
            ImageSaveEnqueueStatus status;
            string message;
            int queueLength;

            lock (_queueLock)
            {
                if (_stopping)
                {
                    DisposeRequestImage(request);
                    Interlocked.Increment(ref _droppedCount);
                    return new ImageSaveEnqueueResult(ImageSaveEnqueueStatus.DroppedQueueFull, _queuedCount, "图像保存服务正在停止。");
                }

                if (_queuedCount < _queueCapacity)
                {
                    EnqueueCore(request);
                    status = ImageSaveEnqueueStatus.Accepted;
                    message = "保存请求已加入队列。";
                }
                else if (request.Priority == ImageSavePriority.High && _normalPriorityQueue.Count > 0)
                {
                    droppedRequest = _normalPriorityQueue.Dequeue();
                    _queuedCount--;
                    EnqueueCore(request);
                    status = ImageSaveEnqueueStatus.AcceptedAfterDropNormal;
                    message = "队列已满，高优先级请求已加入队列，并淘汰一个普通优先级请求。";
                }
                else
                {
                    DisposeRequestImage(request);
                    Interlocked.Increment(ref _droppedCount);
                    status = ImageSaveEnqueueStatus.DroppedQueueFull;
                    message = "队列已满，保存请求已按策略A丢弃。";
                }

                queueLength = _queuedCount;
            }

            if (droppedRequest != null)
            {
                DisposeRequestImage(droppedRequest);
                Interlocked.Increment(ref _droppedCount);
                LogHelper.AddLog(MsgLevel.Warn, $"图像保存队列已满，已丢弃普通优先级请求：{droppedRequest.BuildSourceText()}，文件名:{droppedRequest.ImageName}", true);
            }

            if (status == ImageSaveEnqueueStatus.DroppedQueueFull)
                LogHelper.AddLog(MsgLevel.Warn, $"图像保存请求被丢弃：{request.BuildSourceText()}，文件名:{request.ImageName}，队列长度:{queueLength}/{_queueCapacity}", true);

            return new ImageSaveEnqueueResult(status, queueLength, message);
        }

        /// <summary>
        /// 获取当前保存队列状态。
        /// </summary>
        /// <returns>保存队列状态。</returns>
        public ImageSaveQueueStatus GetStatus()
        {
            lock (_queueLock)
            {
                return new ImageSaveQueueStatus
                {
                    QueueLength = _queuedCount,
                    QueueCapacity = _queueCapacity,
                    NormalQueueLength = _normalPriorityQueue.Count,
                    HighQueueLength = _highPriorityQueue.Count,
                    AcceptedCount = Interlocked.Read(ref _acceptedCount),
                    DroppedCount = Interlocked.Read(ref _droppedCount),
                    SavedCount = Interlocked.Read(ref _savedCount),
                    FailedCount = Interlocked.Read(ref _failedCount)
                };
            }
        }

        /// <summary>
        /// 停止后台保存线程。
        /// </summary>
        /// <param name="waitForDrain">是否等待队列中已有请求保存完成。</param>
        public void Stop(bool waitForDrain)
        {
            lock (_queueLock)
            {
                _stopping = true;
                if (!waitForDrain)
                    ClearQueuedRequestsLocked();
            }

            for (int i = 0; i < _workerCount; i++)
                _queueSignal.Set();

            foreach (Thread worker in _workers)
            {
                if (worker != null && worker.IsAlive)
                    worker.Join(2000);
            }
        }

        /// <summary>
        /// 判断保存请求是否有效。
        /// </summary>
        /// <param name="request">保存请求。</param>
        /// <returns>请求有效时返回 true。</returns>
        private static bool IsValidRequest(ImageSaveRequest request)
        {
            return request != null &&
                request.Image != null &&
                request.SaveDirectories != null &&
                request.SaveDirectories.Count > 0 &&
                !string.IsNullOrWhiteSpace(request.ImageName);
        }

        /// <summary>
        /// 按优先级加入对应队列。
        /// </summary>
        /// <param name="request">保存请求。</param>
        private void EnqueueCore(ImageSaveRequest request)
        {
            if (request.Priority == ImageSavePriority.High)
                _highPriorityQueue.Enqueue(request);
            else
                _normalPriorityQueue.Enqueue(request);

            _queuedCount++;
            Interlocked.Increment(ref _acceptedCount);
            _queueSignal.Set();
        }

        /// <summary>
        /// 后台保存线程主循环。
        /// </summary>
        private void WorkerLoop()
        {
            while (true)
            {
                ImageSaveRequest request = DequeueRequestOrNull();
                if (request == null)
                {
                    if (ShouldWorkerExit())
                        return;

                    _queueSignal.WaitOne(50);
                    continue;
                }

                ProcessRequest(request);
            }
        }

        /// <summary>
        /// 从队列取出一个保存请求。
        /// </summary>
        /// <returns>保存请求；队列为空时返回 null。</returns>
        private ImageSaveRequest DequeueRequestOrNull()
        {
            lock (_queueLock)
            {
                ImageSaveRequest request = null;
                if (_highPriorityQueue.Count > 0)
                    request = _highPriorityQueue.Dequeue();
                else if (_normalPriorityQueue.Count > 0)
                    request = _normalPriorityQueue.Dequeue();

                if (request != null)
                    _queuedCount--;

                return request;
            }
        }

        /// <summary>
        /// 判断后台线程是否应该退出。
        /// </summary>
        /// <returns>服务停止且队列为空时返回 true。</returns>
        private bool ShouldWorkerExit()
        {
            lock (_queueLock)
            {
                return _stopping && _queuedCount == 0;
            }
        }

        /// <summary>
        /// 执行单个保存请求。
        /// </summary>
        /// <param name="request">保存请求。</param>
        private void ProcessRequest(ImageSaveRequest request)
        {
            try
            {
                foreach (string saveDirectory in request.SaveDirectories)
                {
                    if (string.IsNullOrWhiteSpace(saveDirectory))
                        continue;

                    SaveBitmapToDirectory(request.Image, saveDirectory, request.ImageName, request.NeedCompress, request.CompressValue);
                }

                Interlocked.Increment(ref _savedCount);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _failedCount);
                LogHelper.AddLog(MsgLevel.Exception, $"后台保存图像失败：{request.BuildSourceText()}，文件名:{request.ImageName}，原因:{ex.Message}", true);
            }
            finally
            {
                DisposeRequestImage(request);
            }
        }

        /// <summary>
        /// 将位图保存到指定目录，并用 CreateNew 防止多线程文件名冲突。
        /// </summary>
        /// <param name="bitmap">待保存位图。</param>
        /// <param name="saveDirectory">保存目录。</param>
        /// <param name="imageName">不含扩展名的图片名称。</param>
        /// <param name="needCompress">是否按质量参数压缩。</param>
        /// <param name="compressValue">JPEG 质量。</param>
        private static void SaveBitmapToDirectory(Bitmap bitmap, string saveDirectory, string imageName, bool needCompress, long compressValue)
        {
            Directory.CreateDirectory(saveDirectory);
            ImageCodecInfo jpegCodec = GetEncoderInfo("image/jpeg");
            for (int suffix = 0; suffix < 100000; suffix++)
            {
                string fullPath = BuildCandidatePath(saveDirectory, imageName, suffix);
                try
                {
                    using (FileStream stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        if (needCompress && jpegCodec != null)
                        {
                            using (EncoderParameters encoderParams = new EncoderParameters(1))
                            {
                                encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, ClampJpegQuality(compressValue));
                                bitmap.Save(stream, jpegCodec, encoderParams);
                            }
                        }
                        else
                        {
                            bitmap.Save(stream, ImageFormat.Jpeg);
                        }
                    }

                    return;
                }
                catch (IOException)
                {
                    continue;
                }
            }

            throw new IOException($"目录“{saveDirectory}”下无法生成可用的图片文件名：{imageName}.jpg");
        }

        /// <summary>
        /// 构建候选文件路径。
        /// </summary>
        /// <param name="saveDirectory">保存目录。</param>
        /// <param name="imageName">基础文件名。</param>
        /// <param name="suffix">重名后缀序号。</param>
        /// <returns>候选文件路径。</returns>
        private static string BuildCandidatePath(string saveDirectory, string imageName, int suffix)
        {
            string fileName = suffix <= 0 ? imageName + ".jpg" : $"{imageName}_{suffix}.jpg";
            return Path.Combine(saveDirectory, fileName);
        }

        /// <summary>
        /// 将 JPEG 质量限制在 1 到 100 之间。
        /// </summary>
        /// <param name="compressValue">输入质量。</param>
        /// <returns>有效 JPEG 质量。</returns>
        private static long ClampJpegQuality(long compressValue)
        {
            if (compressValue < 1)
                return 1;

            if (compressValue > 100)
                return 100;

            return compressValue;
        }

        /// <summary>
        /// 根据 MIME 类型获取图像编码器。
        /// </summary>
        /// <param name="mimeType">MIME 类型。</param>
        /// <returns>图像编码器；找不到时返回 null。</returns>
        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            ImageCodecInfo[] encoders = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo encoder in encoders)
            {
                if (encoder.MimeType == mimeType)
                    return encoder;
            }

            return null;
        }

        /// <summary>
        /// 清空当前队列中尚未处理的保存请求。
        /// </summary>
        private void ClearQueuedRequestsLocked()
        {
            while (_highPriorityQueue.Count > 0)
            {
                DisposeRequestImage(_highPriorityQueue.Dequeue());
                _queuedCount--;
                Interlocked.Increment(ref _droppedCount);
            }

            while (_normalPriorityQueue.Count > 0)
            {
                DisposeRequestImage(_normalPriorityQueue.Dequeue());
                _queuedCount--;
                Interlocked.Increment(ref _droppedCount);
            }
        }

        /// <summary>
        /// 释放保存请求持有的图像资源。
        /// </summary>
        /// <param name="request">保存请求。</param>
        private static void DisposeRequestImage(ImageSaveRequest request)
        {
            if (request == null || request.Image == null)
                return;

            try
            {
                request.Image.Dispose();
            }
            catch
            {
            }
            finally
            {
                request.Image = null;
            }
        }
    }
}
