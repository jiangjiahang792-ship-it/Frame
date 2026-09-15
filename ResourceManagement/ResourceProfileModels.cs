using System;
using System.Collections.Generic;
using System.Linq;

namespace TDJS_Vision.ResourceManagement
{
    /// <summary>
    /// 当前计算机与进程可使用的硬件资源快照。
    /// </summary>
    public sealed class HardwareResourceSnapshot
    {
        /// <summary>快照采集时间。</summary>
        public DateTime CapturedAt { get; set; }

        /// <summary>CPU名称。</summary>
        public string CpuName { get; set; } = string.Empty;

        /// <summary>物理核心数量。</summary>
        public int PhysicalCoreCount { get; set; }

        /// <summary>操作系统报告的逻辑处理器数量。</summary>
        public int LogicalProcessorCount { get; set; }

        /// <summary>当前进程亲和性允许使用的逻辑处理器数量。</summary>
        public int AvailableLogicalProcessorCount { get; set; }

        /// <summary>物理内存总量，单位MB。</summary>
        public long TotalMemoryMb { get; set; }

        /// <summary>采集时可用物理内存，单位MB。</summary>
        public long AvailableMemoryMb { get; set; }

        /// <summary>程序所在磁盘的可用空间，单位MB。</summary>
        public long SystemDriveFreeSpaceMb { get; set; }

        /// <summary>程序所在磁盘名称。</summary>
        public string SystemDriveName { get; set; } = string.Empty;

        /// <summary>显卡名称集合；只用于诊断，不参与AI并发计算。</summary>
        public List<string> GpuNames { get; set; } = new List<string>();

        /// <summary>硬件读取失败后采用降级值的原因集合。</summary>
        public List<string> ProbeWarnings { get; set; } = new List<string>();

        /// <summary>当前进程是否为64位进程。</summary>
        public bool Is64BitProcess { get; set; }

        /// <summary>
        /// 创建不会阻止软件启动的保守硬件快照。
        /// </summary>
        /// <param name="warning">触发降级的原因。</param>
        /// <returns>带降级原因的硬件快照。</returns>
        public static HardwareResourceSnapshot CreateFallback(string warning)
        {
            int logicalCount = Math.Max(1, Environment.ProcessorCount);
            HardwareResourceSnapshot snapshot = new HardwareResourceSnapshot
            {
                CapturedAt = DateTime.Now,
                CpuName = "读取失败，使用保守值",
                PhysicalCoreCount = Math.Max(1, logicalCount / 2),
                LogicalProcessorCount = logicalCount,
                AvailableLogicalProcessorCount = logicalCount,
                TotalMemoryMb = 4096,
                AvailableMemoryMb = 2048,
                SystemDriveFreeSpaceMb = 0,
                SystemDriveName = "未知",
                Is64BitProcess = Environment.Is64BitProcess
            };
            snapshot.ProbeWarnings.Add(string.IsNullOrWhiteSpace(warning) ? "硬件信息读取失败。" : warning);
            return snapshot;
        }

        /// <summary>
        /// 生成现场可检索的硬件摘要。
        /// </summary>
        /// <returns>硬件资源摘要文本。</returns>
        public string ToLogText()
        {
            string gpuText = GpuNames == null || GpuNames.Count == 0
                ? "未读取到"
                : string.Join(" | ", GpuNames.Where(name => !string.IsNullOrWhiteSpace(name)));
            return $"CPU={CpuName}；物理核心={PhysicalCoreCount}；逻辑线程={LogicalProcessorCount}；进程可用线程={AvailableLogicalProcessorCount}；总内存={TotalMemoryMb}MB；启动可用内存={AvailableMemoryMb}MB；磁盘={SystemDriveName},{SystemDriveFreeSpaceMb}MB可用；GPU={gpuText}；64位进程={Is64BitProcess}";
        }
    }

    /// <summary>
    /// 当前方案对资源档案计算有影响的工作负载快照。
    /// </summary>
    public sealed class ResourceWorkloadSnapshot
    {
        /// <summary>当前启用流程数量。</summary>
        public int EnabledProcessCount { get; set; }

        /// <summary>当前二维与三维相机总数。</summary>
        public int CameraCount { get; set; }

        /// <summary>采样得到的平均单帧或单个保存任务持有字节数；尚未采样时为0。</summary>
        public long AverageImageBytes { get; set; }
    }

    /// <summary>
    /// 自动资源档案计算参数，后续系统设置界面可以生成本机覆盖值。
    /// </summary>
    public sealed class ResourceProfileCalculationOptions
    {
        /// <summary>系统保留物理核心比例。</summary>
        public double CpuReservePercent { get; set; } = 0.20D;

        /// <summary>系统最少保留物理核心数。</summary>
        public int CpuReserveMinimum { get; set; } = 2;

        /// <summary>CPU重任务自动并发上限。</summary>
        public int CpuHeavyConcurrencyMaximum { get; set; } = 8;

        /// <summary>触发CPU重任务并发下降的CPU高水位百分比。</summary>
        public double CpuHighWatermarkPercent { get; set; } = 85D;

        /// <summary>允许CPU重任务并发恢复的CPU低水位百分比。</summary>
        public double CpuRecoveryWatermarkPercent { get; set; } = 65D;

        /// <summary>CPU高水位必须持续的毫秒数。</summary>
        public int CpuHighDurationMs { get; set; } = 5000;

        /// <summary>CPU低水位必须持续的毫秒数。</summary>
        public int CpuRecoveryDurationMs { get; set; } = 10000;

        /// <summary>运行时CPU资源策略采样和评估周期，单位毫秒。</summary>
        public int ResourceAdjustmentIntervalMs { get; set; } = 5000;

        /// <summary>两次CPU并发调整之间的冷却时间，单位毫秒。</summary>
        public int ResourceAdjustmentCooldownMs { get; set; } = 30000;

        /// <summary>每次CPU并发调整的许可数量。</summary>
        public int ConcurrencyAdjustmentStep { get; set; } = 1;

        /// <summary>每多少个物理核心允许一个图像转换并发。</summary>
        public int ImageConvertCoreDivisor { get; set; } = 4;

        /// <summary>图像转换自动并发上限。</summary>
        public int ImageConvertConcurrencyMaximum { get; set; } = 4;

        /// <summary>每多少个物理核心允许一个保存工作线程。</summary>
        public int ImageSaveWorkerCoreDivisor { get; set; } = 8;

        /// <summary>图片保存自动工作线程上限。</summary>
        public int ImageSaveWorkerMaximum { get; set; } = 2;

        /// <summary>流程图像预算占总内存比例。</summary>
        public double FlowImageTotalMemoryPercent { get; set; } = 0.125D;

        /// <summary>流程图像预算占启动可用内存比例。</summary>
        public double FlowImageAvailableMemoryPercent { get; set; } = 0.25D;

        /// <summary>流程图像预算绝对上限，单位MB。</summary>
        public int FlowImageBudgetMaximumMb { get; set; } = 2048;

        /// <summary>保存队列预算占总内存比例。</summary>
        public double SaveQueueTotalMemoryPercent { get; set; } = 0.04D;

        /// <summary>保存队列预算占启动可用内存比例。</summary>
        public double SaveQueueAvailableMemoryPercent { get; set; } = 0.10D;

        /// <summary>保存队列预算绝对上限，单位MB。</summary>
        public int SaveQueueBudgetMaximumMb { get; set; } = 512;

        /// <summary>可用内存低水位比例。</summary>
        public double MemoryLowWatermarkPercent { get; set; } = 0.15D;

        /// <summary>可用内存低水位最小值，单位MB。</summary>
        public int MemoryLowWatermarkMinimumMb { get; set; } = 1536;

        /// <summary>可用内存严重水位比例。</summary>
        public double MemoryCriticalWatermarkPercent { get; set; } = 0.08D;

        /// <summary>可用内存严重水位最小值，单位MB。</summary>
        public int MemoryCriticalWatermarkMinimumMb { get; set; } = 768;

        /// <summary>没有图像采样时使用的保存队列初始容量。</summary>
        public int SaveQueueInitialCapacity { get; set; } = 8;

        /// <summary>保存队列计算容量下限。</summary>
        public int SaveQueueMinimumCapacity { get; set; } = 4;

        /// <summary>保存队列计算容量上限。</summary>
        public int SaveQueueMaximumCapacity { get; set; } = 50;

        /// <summary>每个图像窗口允许的最大显示帧率。</summary>
        public int UiMaxRefreshFps { get; set; } = PerWindowUiFrameRateLimiter.DefaultMaximumFramesPerSecond;

        /// <summary>停止后台工作池时等待活动资源任务退出的最长毫秒数。</summary>
        public int ShutdownResourceDrainTimeoutMs { get; set; } = 5000;

        /// <summary>保存任务完整图像资源字节数的滚动采样数量。</summary>
        public int ImageSizeSampleCount { get; set; } = RollingImageResourceSizeSampler.DefaultCapacity;

        /// <summary>普通图片停止保存的磁盘低空间水位，单位MB。</summary>
        public int SaveDiskLowSpaceMb { get; set; } = CachedImageSaveStorageGuard.DefaultLowSpaceThresholdMb;

        /// <summary>全部图片停止保存的磁盘严重空间水位，单位MB。</summary>
        public int SaveDiskCriticalSpaceMb { get; set; } = CachedImageSaveStorageGuard.DefaultCriticalSpaceThresholdMb;

        /// <summary>单个目标目录写盘慢耗时阈值，单位ms。</summary>
        public int SaveSlowThresholdMs { get; set; } = CachedImageSaveStorageGuard.DefaultSlowWriteThresholdMs;

        /// <summary>磁盘空间探测结果缓存时间，单位ms。</summary>
        public int SaveDiskProbeIntervalMs { get; set; } = CachedImageSaveStorageGuard.DefaultProbeIntervalMs;
    }

    /// <summary>
    /// 根据硬件与当前方案规模计算得到的自动资源建议档案。
    /// </summary>
    public sealed class AutomaticResourceProfile
    {
        /// <summary>是否允许根据系统负载动态调整CPU重任务并发。</summary>
        public bool AdaptiveResourceManagementEnabled { get; set; } = true;

        /// <summary>档案生成时间。</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>档案使用的硬件快照。</summary>
        public HardwareResourceSnapshot Hardware { get; set; }

        /// <summary>档案使用的工作负载快照。</summary>
        public ResourceWorkloadSnapshot Workload { get; set; }

        /// <summary>系统保留物理核心数。</summary>
        public int CpuReserveCoreCount { get; set; }

        /// <summary>CPU重任务建议最大并发数。</summary>
        public int CpuHeavyMaxConcurrency { get; set; }

        /// <summary>触发CPU重任务并发下降的CPU高水位百分比。</summary>
        public double CpuHighWatermarkPercent { get; set; }

        /// <summary>允许CPU重任务并发恢复的CPU低水位百分比。</summary>
        public double CpuRecoveryWatermarkPercent { get; set; }

        /// <summary>CPU高水位必须持续的毫秒数。</summary>
        public int CpuHighDurationMs { get; set; }

        /// <summary>CPU低水位必须持续的毫秒数。</summary>
        public int CpuRecoveryDurationMs { get; set; }

        /// <summary>运行时CPU资源策略采样和评估周期，单位毫秒。</summary>
        public int ResourceAdjustmentIntervalMs { get; set; }

        /// <summary>两次CPU并发调整之间的冷却时间，单位毫秒。</summary>
        public int ResourceAdjustmentCooldownMs { get; set; }

        /// <summary>每次CPU并发调整的许可数量。</summary>
        public int ConcurrencyAdjustmentStep { get; set; }

        /// <summary>图像转换建议最大并发数。</summary>
        public int ImageConvertMaxConcurrency { get; set; }

        /// <summary>图片保存建议工作线程数。</summary>
        public int ImageSaveWorkerCount { get; set; }

        /// <summary>流程图像总内存预算，单位MB。</summary>
        public int FlowImageMemoryBudgetMb { get; set; }

        /// <summary>图片保存队列内存预算，单位MB。</summary>
        public int SaveQueueMemoryBudgetMb { get; set; }

        /// <summary>可用内存低水位，单位MB。</summary>
        public int MemoryLowWatermarkMb { get; set; }

        /// <summary>可用内存严重水位，单位MB。</summary>
        public int MemoryCriticalWatermarkMb { get; set; }

        /// <summary>根据保存预算与平均任务字节数计算的队列容量。</summary>
        public int SaveQueueCalculatedCapacity { get; set; }

        /// <summary>AI并发策略；固定跟随可编辑流程。</summary>
        public string AiConcurrencyPolicy { get; set; } = "FollowEditableFlow";

        /// <summary>每个图像窗口独立执行的最大显示帧率。</summary>
        public int UiMaxRefreshFps { get; set; }

        /// <summary>当前是否因为没有图像采样而使用初始保存队列容量。</summary>
        public bool UsesInitialSaveQueueCapacity { get; set; }

        /// <summary>停止后台工作池时等待活动资源任务退出的最长毫秒数。</summary>
        public int ShutdownResourceDrainTimeoutMs { get; set; }

        /// <summary>保存任务完整图像资源字节数的滚动采样数量。</summary>
        public int ImageSizeSampleCount { get; set; }

        /// <summary>普通图片停止保存的磁盘低空间水位，单位MB。</summary>
        public int SaveDiskLowSpaceMb { get; set; }

        /// <summary>全部图片停止保存的磁盘严重空间水位，单位MB。</summary>
        public int SaveDiskCriticalSpaceMb { get; set; }

        /// <summary>单个目标目录写盘慢耗时阈值，单位ms。</summary>
        public int SaveSlowThresholdMs { get; set; }

        /// <summary>磁盘空间探测结果缓存时间，单位ms。</summary>
        public int SaveDiskProbeIntervalMs { get; set; }

        /// <summary>
        /// 生成现场可检索的自动资源建议摘要。
        /// </summary>
        /// <returns>自动资源档案摘要。</returns>
        public string ToLogText()
        {
            ResourceWorkloadSnapshot workload = Workload ?? new ResourceWorkloadSnapshot();
            return $"自适应资源={AdaptiveResourceManagementEnabled}；启用流程={workload.EnabledProcessCount}；相机={workload.CameraCount}；平均图像={workload.AverageImageBytes}字节；图像样本={ImageSizeSampleCount}；CPU保留核心={CpuReserveCoreCount}；CPU重任务并发={CpuHeavyMaxConcurrency}；CPU高/恢复水位={CpuHighWatermarkPercent:F0}%/{CpuRecoveryWatermarkPercent:F0}%；CPU高/恢复持续={CpuHighDurationMs}/{CpuRecoveryDurationMs}ms；资源评估={ResourceAdjustmentIntervalMs}ms；调整冷却={ResourceAdjustmentCooldownMs}ms；调整步长={ConcurrencyAdjustmentStep}；图像转换并发={ImageConvertMaxConcurrency}；保存线程={ImageSaveWorkerCount}；流程图像预算={FlowImageMemoryBudgetMb}MB；保存队列预算={SaveQueueMemoryBudgetMb}MB；内存低水位={MemoryLowWatermarkMb}MB；严重水位={MemoryCriticalWatermarkMb}MB；保存队列容量={SaveQueueCalculatedCapacity}；磁盘低水位={SaveDiskLowSpaceMb}MB；磁盘严重水位={SaveDiskCriticalSpaceMb}MB；慢写阈值={SaveSlowThresholdMs}ms；磁盘采样={SaveDiskProbeIntervalMs}ms；UI最大帧率={UiMaxRefreshFps}；停止等待={ShutdownResourceDrainTimeoutMs}ms；使用初始容量={UsesInitialSaveQueueCapacity}；AI策略={AiConcurrencyPolicy}";
        }
    }

    /// <summary>
    /// 图片保存工作池对最新机器资源档案的应用状态。
    /// </summary>
    public enum ImageSaveProfileApplyState
    {
        /// <summary>工作池尚未创建，创建时会直接使用最新档案。</summary>
        UseLatestWhenCreated = 0,

        /// <summary>现有工作池已经使用最新档案。</summary>
        Applied = 1,

        /// <summary>现有工作池仍在运行，等待下一轮空闲启动或方案重置后应用。</summary>
        Pending = 2
    }

    /// <summary>
    /// 系统设置保存后对当前运行框架的资源参数应用结果。
    /// </summary>
    public sealed class RuntimeResourceProfileApplyResult
    {
        /// <summary>资源档案是否成功生成并应用到允许热更新的组件。</summary>
        public bool Success { get; set; }

        /// <summary>本次生成的最终运行档案。</summary>
        public AutomaticResourceProfile EffectiveProfile { get; set; }

        /// <summary>CPU调度、CPU采样和显示参数是否已经更新。</summary>
        public bool CpuAndDisplayApplied { get; set; }

        /// <summary>图像大小滚动采样容量是否已经更新。</summary>
        public bool ImageSampleApplied { get; set; }

        /// <summary>图片保存工作池对最新档案的应用状态。</summary>
        public ImageSaveProfileApplyState ImageSaveState { get; set; }

        /// <summary>供系统设置界面展示的简体中文结果。</summary>
        public string Message { get; set; } = string.Empty;
    }
}
