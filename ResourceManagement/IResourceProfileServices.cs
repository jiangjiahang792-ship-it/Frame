using System;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node;

namespace TDJS_Vision.ResourceManagement
{
    /// <summary>
    /// 读取当前计算机硬件资源的可替换探针。
    /// </summary>
    public interface IHardwareResourceProbe
    {
        /// <summary>
        /// 读取硬件资源；读取局部失败时应返回带警告的降级快照。
        /// </summary>
        /// <returns>硬件资源快照。</returns>
        HardwareResourceSnapshot Capture();
    }

    /// <summary>
    /// 根据硬件和方案规模计算自动资源档案的可替换策略。
    /// </summary>
    public interface IResourceProfileCalculator
    {
        /// <summary>
        /// 计算自动资源建议，不直接修改线程池、CPU亲和性或AI并发。
        /// </summary>
        /// <param name="hardware">硬件资源快照。</param>
        /// <param name="workload">当前方案工作负载。</param>
        /// <param name="options">计算参数。</param>
        /// <returns>自动资源建议档案。</returns>
        AutomaticResourceProfile Calculate(
            HardwareResourceSnapshot hardware,
            ResourceWorkloadSnapshot workload,
            ResourceProfileCalculationOptions options);
    }

    /// <summary>
    /// 为运行中的方案提供自动资源档案，并隔离硬件读取缓存策略。
    /// </summary>
    public interface IRuntimeResourceProfileProvider
    {
        /// <summary>
        /// 根据当前方案规模生成资源档案。
        /// </summary>
        /// <param name="workload">当前方案工作负载。</param>
        /// <returns>自动资源档案。</returns>
        AutomaticResourceProfile BuildProfile(ResourceWorkloadSnapshot workload);

        /// <summary>
        /// 在同一硬件和工作负载快照下生成自动推荐与最终生效档案。
        /// </summary>
        /// <param name="workload">当前方案工作负载。</param>
        /// <returns>自动值、机器设置和最终值的同批结果。</returns>
        ResourceProfileBuildResult BuildProfileResult(ResourceWorkloadSnapshot workload);

        /// <summary>
        /// 使用指定的机器设置草稿生成预览，不保存也不修改运行组件。
        /// </summary>
        /// <param name="workload">当前方案工作负载。</param>
        /// <param name="settings">待预览的机器设置草稿。</param>
        /// <returns>自动值、草稿手动值和最终值的同批结果。</returns>
        ResourceProfileBuildResult BuildProfileResult(
            ResourceWorkloadSnapshot workload,
            MachinePerformanceResourceSettings settings);

        /// <summary>
        /// 清除硬件快照缓存，供硬件环境变化或测试时重新读取。
        /// </summary>
        void ResetHardwareCache();
    }

    /// <summary>
    /// CPU重任务许可；释放许可后，调度器才能启动下一项CPU重任务。
    /// </summary>
    public interface ICpuWorkLease : IDisposable
    {
        /// <summary>当前许可对应的CPU工作类型。</summary>
        CpuWorkloadKind WorkloadKind { get; }

        /// <summary>取得许可前的排队耗时，单位毫秒。</summary>
        double WaitElapsedMilliseconds { get; }
    }

    /// <summary>
    /// 全方案共享的可替换CPU重任务调度器；只控制开始时机，不改变流程节点顺序。
    /// </summary>
    public interface ICpuWorkScheduler : IDisposable
    {
        /// <summary>
        /// 异步取得一项CPU重任务许可。
        /// </summary>
        /// <param name="workloadKind">CPU工作类型。</param>
        /// <param name="cancellationToken">取消等待的令牌。</param>
        /// <returns>必须释放的CPU工作许可。</returns>
        Task<ICpuWorkLease> AcquireAsync(CpuWorkloadKind workloadKind, CancellationToken cancellationToken);

        /// <summary>
        /// 输入一次已经按全机逻辑线程归一化的CPU采样。
        /// </summary>
        /// <param name="cpuPercent">CPU使用率，范围0～100。</param>
        /// <param name="timestampMilliseconds">单调递增时间戳，单位毫秒。</param>
        void ObserveCpuSample(double cpuPercent, long timestampMilliseconds);

        /// <summary>
        /// 使用新的硬件档案参数重新配置调度器；正在运行的许可不会被中断。
        /// </summary>
        /// <param name="options">CPU调度参数。</param>
        void Reconfigure(CpuWorkSchedulerOptions options);

        /// <summary>
        /// 读取当前并发、排队和动态调整诊断快照。
        /// </summary>
        /// <returns>只读诊断快照。</returns>
        CpuWorkSchedulerSnapshot GetSnapshot();
    }

    /// <summary>
    /// 把流程节点分类为需要受控的CPU重任务；未分类节点保持原执行方式。
    /// </summary>
    public interface ICpuWorkloadClassifier
    {
        /// <summary>
        /// 尝试读取指定节点的CPU工作类型。
        /// </summary>
        /// <param name="node">待执行流程节点。</param>
        /// <param name="workloadKind">分类成功时返回CPU工作类型。</param>
        /// <returns>节点需要CPU许可返回true；否则返回false。</returns>
        bool TryClassify(NodeBase node, out CpuWorkloadKind workloadKind);
    }

    /// <summary>
    /// 可替换的全机CPU使用率采样器。
    /// </summary>
    public interface ISystemCpuUsageSampler
    {
        /// <summary>
        /// 尝试读取0～100范围的全机CPU使用率。
        /// </summary>
        /// <param name="cpuPercent">采样成功时返回CPU使用率。</param>
        /// <returns>已经具备有效前后采样点时返回true。</returns>
        bool TrySample(out double cpuPercent);
    }

    /// <summary>
    /// 按图像窗口限制显示帧率的可替换策略。
    /// </summary>
    public interface IUiFrameRateLimiter
    {
        /// <summary>
        /// 尝试取得指定窗口本次图像转换与显示许可。
        /// </summary>
        /// <param name="windowName">标准化后的图像窗口名称。</param>
        /// <param name="maximumFramesPerSecond">每秒最大显示帧数。</param>
        /// <returns>允许本次显示返回 true；应跳过本次显示返回 false。</returns>
        bool TryAcquire(string windowName, int maximumFramesPerSecond);

        /// <summary>
        /// 清除指定窗口的限速状态，使重新打开后的第一帧立即显示。
        /// </summary>
        /// <param name="windowName">图像窗口名称。</param>
        void Reset(string windowName);

        /// <summary>
        /// 清除全部窗口的限速状态。
        /// </summary>
        void ResetAll();
    }
}
