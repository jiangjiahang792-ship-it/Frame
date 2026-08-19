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
        /// 清除硬件快照缓存，供硬件环境变化或测试时重新读取。
        /// </summary>
        void ResetHardwareCache();
    }
}
