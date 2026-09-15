using System;
using System.Collections.Generic;
using System.IO;

namespace TDJS_Vision.ResourceManagement
{
    /// <summary>
    /// 保存任务开始图像转换前的磁盘准入状态。
    /// </summary>
    public enum ImageSaveStorageStatus
    {
        /// <summary>磁盘空间正常，允许保存。</summary>
        Allowed,

        /// <summary>磁盘低于告警水位，但当前任务是需要优先保留的NG图。</summary>
        AllowedHighPriorityUnderLowSpace,

        /// <summary>磁盘低于告警水位，普通图片停止保存。</summary>
        SkippedLowSpace,

        /// <summary>磁盘低于严重水位，全部图片停止保存。</summary>
        SkippedCriticalSpace,

        /// <summary>磁盘空间探测失败；为避免监控故障误停产，当前任务继续保存。</summary>
        ProbeFailedAllowed
    }

    /// <summary>
    /// 保存任务磁盘准入结果，携带限制磁盘和可用空间供日志诊断。
    /// </summary>
    public sealed class ImageSaveStorageDecision
    {
        /// <summary>本次准入状态。</summary>
        public ImageSaveStorageStatus Status { get; set; }

        /// <summary>限制本次准入的磁盘根目录。</summary>
        public string StorageRoot { get; set; }

        /// <summary>限制磁盘的可用空间，探测失败时为-1。</summary>
        public long AvailableSpaceMb { get; set; }

        /// <summary>探测失败或跳过保存的简体中文原因。</summary>
        public string Reason { get; set; }

        /// <summary>当前状态是否要求直接释放任务而不执行转图和写盘。</summary>
        public bool ShouldSkip =>
            Status == ImageSaveStorageStatus.SkippedLowSpace ||
            Status == ImageSaveStorageStatus.SkippedCriticalSpace;
    }

    /// <summary>
    /// 磁盘可用空间探针，允许压力测试注入确定性的低空间结果。
    /// </summary>
    public interface IImageSaveDiskSpaceProbe
    {
        /// <summary>
        /// 尝试读取指定磁盘根目录的可用空间。
        /// </summary>
        /// <param name="storageRoot">已经规范化的磁盘根目录。</param>
        /// <param name="availableSpaceMb">成功时返回可用空间MB。</param>
        /// <param name="errorMessage">失败时返回错误原因。</param>
        /// <returns>读取成功返回true。</returns>
        bool TryGetAvailableSpaceMb(string storageRoot, out long availableSpaceMb, out string errorMessage);
    }

    /// <summary>
    /// 保存磁盘保护接口，隔离空间探测、慢盘阈值和可控压力注入。
    /// </summary>
    public interface IImageSaveStorageGuard
    {
        /// <summary>低空间告警水位，单位MB。</summary>
        int LowSpaceThresholdMb { get; }

        /// <summary>严重空间水位，单位MB。</summary>
        int CriticalSpaceThresholdMb { get; }

        /// <summary>单个目标路径写盘慢耗时阈值，单位ms。</summary>
        int SlowWriteThresholdMs { get; }

        /// <summary>磁盘空间探测结果缓存时间，单位ms。</summary>
        int ProbeIntervalMs { get; }

        /// <summary>
        /// 在图像转Bitmap前判断本任务是否允许继续保存。
        /// </summary>
        /// <param name="targetDirectories">任务需要写入的全部目标目录。</param>
        /// <param name="isHighPriority">当前任务是否为NG高优先级任务。</param>
        /// <returns>磁盘准入结果。</returns>
        ImageSaveStorageDecision Evaluate(IReadOnlyList<string> targetDirectories, bool isHighPriority);

        /// <summary>
        /// 写入单个目标目录前执行准备动作；生产实现不等待，压力测试可注入确定性慢盘延迟。
        /// </summary>
        /// <param name="targetDirectory">即将写入的目标目录。</param>
        void PrepareWrite(string targetDirectory);

        /// <summary>
        /// 判断一次目标目录写入是否达到慢盘阈值。
        /// </summary>
        /// <param name="elapsedMilliseconds">本次目录准备、编码和原子发布总耗时。</param>
        /// <returns>达到慢盘阈值返回true。</returns>
        bool IsSlowWrite(long elapsedMilliseconds);
    }

    /// <summary>
    /// 使用DriveInfo读取本地或映射磁盘可用空间的生产探针。
    /// </summary>
    public sealed class DriveImageSaveDiskSpaceProbe : IImageSaveDiskSpaceProbe
    {
        /// <summary>
        /// 尝试读取指定磁盘根目录的可用空间。
        /// </summary>
        public bool TryGetAvailableSpaceMb(string storageRoot, out long availableSpaceMb, out string errorMessage)
        {
            availableSpaceMb = -1;
            errorMessage = string.Empty;
            try
            {
                DriveInfo drive = new DriveInfo(storageRoot);
                if (!drive.IsReady)
                {
                    errorMessage = "磁盘尚未就绪。";
                    return false;
                }

                availableSpaceMb = Math.Max(0L, drive.AvailableFreeSpace / 1024L / 1024L);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
    }

    /// <summary>
    /// 带短时缓存的磁盘保护器，避免每个保存节点在检测线程上重复访问磁盘状态。
    /// </summary>
    public sealed class CachedImageSaveStorageGuard : IImageSaveStorageGuard
    {
        /// <summary>默认低空间告警水位。</summary>
        public const int DefaultLowSpaceThresholdMb = 2048;

        /// <summary>默认严重空间水位。</summary>
        public const int DefaultCriticalSpaceThresholdMb = 512;

        /// <summary>默认单路径慢写阈值。</summary>
        public const int DefaultSlowWriteThresholdMs = 500;

        /// <summary>默认磁盘空间探测缓存时间。</summary>
        public const int DefaultProbeIntervalMs = 1000;

        /// <summary>保护磁盘空间缓存的一致性锁。</summary>
        private readonly object _cacheSync = new object();

        /// <summary>按磁盘根目录保存的最近空间探测结果。</summary>
        private readonly Dictionary<string, CachedDiskSpaceSample> _samples =
            new Dictionary<string, CachedDiskSpaceSample>(StringComparer.OrdinalIgnoreCase);

        /// <summary>实际读取磁盘空间的可替换探针。</summary>
        private readonly IImageSaveDiskSpaceProbe _diskSpaceProbe;

        /// <summary>磁盘空间探测缓存时间。</summary>
        private readonly TimeSpan _probeInterval;

        /// <summary>低空间告警水位，单位MB。</summary>
        public int LowSpaceThresholdMb { get; }

        /// <summary>严重空间水位，单位MB。</summary>
        public int CriticalSpaceThresholdMb { get; }

        /// <summary>单个目标路径写盘慢耗时阈值，单位ms。</summary>
        public int SlowWriteThresholdMs { get; }

        /// <summary>磁盘空间探测结果缓存时间，单位ms。</summary>
        public int ProbeIntervalMs { get; }

        /// <summary>
        /// 创建使用生产默认参数的磁盘保护器。
        /// </summary>
        /// <returns>使用DriveInfo探针的磁盘保护器。</returns>
        public static CachedImageSaveStorageGuard CreateDefault()
        {
            return new CachedImageSaveStorageGuard(
                new DriveImageSaveDiskSpaceProbe(),
                DefaultLowSpaceThresholdMb,
                DefaultCriticalSpaceThresholdMb,
                DefaultSlowWriteThresholdMs,
                DefaultProbeIntervalMs);
        }

        /// <summary>
        /// 使用指定阈值创建磁盘保护器。
        /// </summary>
        /// <param name="diskSpaceProbe">可用空间探针。</param>
        /// <param name="lowSpaceThresholdMb">普通图片停止保存水位。</param>
        /// <param name="criticalSpaceThresholdMb">全部图片停止保存水位。</param>
        /// <param name="slowWriteThresholdMs">单路径慢写阈值。</param>
        /// <param name="probeIntervalMs">空间探测缓存时间。</param>
        public CachedImageSaveStorageGuard(
            IImageSaveDiskSpaceProbe diskSpaceProbe,
            int lowSpaceThresholdMb,
            int criticalSpaceThresholdMb,
            int slowWriteThresholdMs,
            int probeIntervalMs)
        {
            _diskSpaceProbe = diskSpaceProbe ?? throw new ArgumentNullException(nameof(diskSpaceProbe));
            LowSpaceThresholdMb = Math.Max(2, lowSpaceThresholdMb);
            CriticalSpaceThresholdMb = Math.Max(1, Math.Min(criticalSpaceThresholdMb, LowSpaceThresholdMb - 1));
            SlowWriteThresholdMs = Math.Max(1, slowWriteThresholdMs);
            ProbeIntervalMs = Math.Max(0, probeIntervalMs);
            _probeInterval = TimeSpan.FromMilliseconds(ProbeIntervalMs);
        }

        /// <summary>
        /// 在图像转Bitmap前检查全部目标磁盘，任何一个目标达到严重水位都会停止本任务。
        /// </summary>
        public ImageSaveStorageDecision Evaluate(IReadOnlyList<string> targetDirectories, bool isHighPriority)
        {
            if (targetDirectories == null || targetDirectories.Count == 0)
            {
                return new ImageSaveStorageDecision
                {
                    Status = ImageSaveStorageStatus.ProbeFailedAllowed,
                    AvailableSpaceMb = -1,
                    Reason = "保存任务没有可供磁盘探测的目标目录。"
                };
            }

            long minimumAvailableSpaceMb = long.MaxValue;
            string limitingRoot = string.Empty;
            string firstProbeError = string.Empty;
            HashSet<string> visitedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < targetDirectories.Count; index++)
            {
                string storageRoot;
                try
                {
                    storageRoot = GetStorageRoot(targetDirectories[index]);
                }
                catch (Exception ex)
                {
                    if (string.IsNullOrEmpty(firstProbeError))
                        firstProbeError = ex.Message;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(storageRoot) || !visitedRoots.Add(storageRoot))
                    continue;

                CachedDiskSpaceSample sample = GetDiskSpaceSample(storageRoot);
                if (!sample.Success)
                {
                    if (string.IsNullOrEmpty(firstProbeError))
                        firstProbeError = sample.ErrorMessage;
                    continue;
                }

                if (sample.AvailableSpaceMb < minimumAvailableSpaceMb)
                {
                    minimumAvailableSpaceMb = sample.AvailableSpaceMb;
                    limitingRoot = storageRoot;
                }
            }

            if (minimumAvailableSpaceMb == long.MaxValue)
            {
                return new ImageSaveStorageDecision
                {
                    Status = ImageSaveStorageStatus.ProbeFailedAllowed,
                    StorageRoot = limitingRoot,
                    AvailableSpaceMb = -1,
                    Reason = "无法读取保存磁盘可用空间：" + (string.IsNullOrWhiteSpace(firstProbeError) ? "未知原因" : firstProbeError)
                };
            }

            if (minimumAvailableSpaceMb < CriticalSpaceThresholdMb)
            {
                return new ImageSaveStorageDecision
                {
                    Status = ImageSaveStorageStatus.SkippedCriticalSpace,
                    StorageRoot = limitingRoot,
                    AvailableSpaceMb = minimumAvailableSpaceMb,
                    Reason = $"保存磁盘可用空间低于严重水位{CriticalSpaceThresholdMb}MB。"
                };
            }

            if (minimumAvailableSpaceMb < LowSpaceThresholdMb)
            {
                return new ImageSaveStorageDecision
                {
                    Status = isHighPriority
                        ? ImageSaveStorageStatus.AllowedHighPriorityUnderLowSpace
                        : ImageSaveStorageStatus.SkippedLowSpace,
                    StorageRoot = limitingRoot,
                    AvailableSpaceMb = minimumAvailableSpaceMb,
                    Reason = isHighPriority
                        ? $"保存磁盘低于{LowSpaceThresholdMb}MB，当前NG图片继续保存。"
                        : $"保存磁盘低于{LowSpaceThresholdMb}MB，普通图片停止保存。"
                };
            }

            return new ImageSaveStorageDecision
            {
                Status = string.IsNullOrEmpty(firstProbeError)
                    ? ImageSaveStorageStatus.Allowed
                    : ImageSaveStorageStatus.ProbeFailedAllowed,
                StorageRoot = limitingRoot,
                AvailableSpaceMb = minimumAvailableSpaceMb,
                Reason = firstProbeError
            };
        }

        /// <summary>
        /// 生产环境写入前不增加等待；该接口仅为可控慢盘压力测试保留注入点。
        /// </summary>
        public void PrepareWrite(string targetDirectory)
        {
        }

        /// <summary>
        /// 判断一次目标目录写入是否达到慢盘阈值。
        /// </summary>
        public bool IsSlowWrite(long elapsedMilliseconds)
        {
            return elapsedMilliseconds >= SlowWriteThresholdMs;
        }

        /// <summary>
        /// 取得目标目录所属的磁盘根目录。
        /// </summary>
        /// <param name="targetDirectory">目标目录。</param>
        /// <returns>本地盘符或UNC共享根目录。</returns>
        private static string GetStorageRoot(string targetDirectory)
        {
            if (string.IsNullOrWhiteSpace(targetDirectory))
                throw new ArgumentException("保存目标目录为空。", nameof(targetDirectory));

            string fullPath = Path.GetFullPath(targetDirectory);
            string storageRoot = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(storageRoot))
                throw new IOException("无法确定保存目标所在磁盘：" + targetDirectory);
            return storageRoot;
        }

        /// <summary>
        /// 返回缓存内仍有效的空间样本，过期后在保存消费者线程刷新。
        /// </summary>
        /// <param name="storageRoot">规范化磁盘根目录。</param>
        /// <returns>空间探测样本。</returns>
        private CachedDiskSpaceSample GetDiskSpaceSample(string storageRoot)
        {
            DateTime now = DateTime.UtcNow;
            lock (_cacheSync)
            {
                if (_samples.TryGetValue(storageRoot, out CachedDiskSpaceSample cached) &&
                    now - cached.CapturedAtUtc <= _probeInterval)
                {
                    return cached;
                }

                bool success = _diskSpaceProbe.TryGetAvailableSpaceMb(
                    storageRoot,
                    out long availableSpaceMb,
                    out string errorMessage);
                CachedDiskSpaceSample sample = new CachedDiskSpaceSample
                {
                    CapturedAtUtc = now,
                    Success = success,
                    AvailableSpaceMb = success ? Math.Max(0L, availableSpaceMb) : -1L,
                    ErrorMessage = errorMessage ?? string.Empty
                };
                _samples[storageRoot] = sample;
                return sample;
            }
        }

        /// <summary>
        /// 单个磁盘根目录最近一次探测结果。
        /// </summary>
        private sealed class CachedDiskSpaceSample
        {
            /// <summary>样本采集UTC时间。</summary>
            public DateTime CapturedAtUtc { get; set; }

            /// <summary>本次空间探测是否成功。</summary>
            public bool Success { get; set; }

            /// <summary>成功时的可用空间MB。</summary>
            public long AvailableSpaceMb { get; set; }

            /// <summary>探测失败原因。</summary>
            public string ErrorMessage { get; set; }
        }
    }

    /// <summary>
    /// 保存工作池的不可变诊断快照，用于Debug智能体、停止摘要和压力测试。
    /// </summary>
    public sealed class ImageSaveQueueDiagnosticsSnapshot
    {
        /// <summary>快照时间。</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>当前等待任务数。</summary>
        public int QueuedCount { get; set; }

        /// <summary>等待任务容量。</summary>
        public int Capacity { get; set; }

        /// <summary>等待任务数峰值。</summary>
        public int PeakQueuedCount { get; set; }

        /// <summary>当前等待任务估算字节。</summary>
        public long QueuedBytes { get; set; }

        /// <summary>等待任务字节预算。</summary>
        public long MemoryBudgetBytes { get; set; }

        /// <summary>等待任务字节峰值。</summary>
        public long PeakQueuedBytes { get; set; }

        /// <summary>当前活动保存消费者。</summary>
        public int ActiveWorkerCount { get; set; }

        /// <summary>配置的固定保存消费者数量。</summary>
        public int WorkerCount { get; set; }

        /// <summary>实际活动消费者峰值。</summary>
        public int PeakActiveWorkerCount { get; set; }

        /// <summary>队列达到边界后的累计拒绝数。</summary>
        public long RejectedTaskCount { get; set; }

        /// <summary>为保留NG图片淘汰的普通图片数。</summary>
        public long EvictedNormalTaskCount { get; set; }

        /// <summary>成功写完全部目标路径的任务数。</summary>
        public long CompletedTaskCount { get; set; }

        /// <summary>转换、绘制或写盘失败的任务数。</summary>
        public long FailedTaskCount { get; set; }

        /// <summary>工作池停止时取消CPU许可等待的任务数。</summary>
        public long CanceledTaskCount { get; set; }

        /// <summary>低空间时跳过的普通图片数。</summary>
        public long LowSpaceSkippedNormalTaskCount { get; set; }

        /// <summary>严重低空间时跳过的全部图片数。</summary>
        public long CriticalSpaceSkippedTaskCount { get; set; }

        /// <summary>低空间时继续保留的NG图片数。</summary>
        public long LowSpaceAllowedHighPriorityTaskCount { get; set; }

        /// <summary>磁盘空间探测失败但继续保存的任务数。</summary>
        public long StorageProbeFailureCount { get; set; }

        /// <summary>达到慢写阈值的目标路径写入次数。</summary>
        public long SlowWriteCount { get; set; }

        /// <summary>观察到的单目标路径最大写入耗时。</summary>
        public long MaximumWriteElapsedMilliseconds { get; set; }

        /// <summary>
        /// 生成适合文件日志和右侧智能体显示的简体中文摘要。
        /// </summary>
        /// <returns>队列、资源和磁盘保护摘要。</returns>
        public string ToLogText()
        {
            return $"等待={QueuedCount}/{Capacity}；等待峰值={PeakQueuedCount}；字节={QueuedBytes}/{MemoryBudgetBytes}；字节峰值={PeakQueuedBytes}；活动={ActiveWorkerCount}/{WorkerCount}；活动峰值={PeakActiveWorkerCount}；完成={CompletedTaskCount}；失败={FailedTaskCount}；停止取消={CanceledTaskCount}；拒绝={RejectedTaskCount}；淘汰普通={EvictedNormalTaskCount}；低空间跳过普通={LowSpaceSkippedNormalTaskCount}；严重空间跳过={CriticalSpaceSkippedTaskCount}；低空间保留NG={LowSpaceAllowedHighPriorityTaskCount}；空间探测失败={StorageProbeFailureCount}；慢写={SlowWriteCount}；最慢写入={MaximumWriteElapsedMilliseconds}ms";
        }
    }
}
