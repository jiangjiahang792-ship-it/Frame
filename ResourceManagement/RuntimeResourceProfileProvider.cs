using System;

namespace TDJS_Vision.ResourceManagement
{
    /// <summary>
    /// 缓存硬件快照并按当前方案规模生成资源档案的默认提供器。
    /// </summary>
    public sealed class RuntimeResourceProfileProvider : IRuntimeResourceProfileProvider
    {
        /// <summary>保护硬件快照缓存的同步锁。</summary>
        private readonly object _hardwareLock = new object();

        /// <summary>可替换的硬件读取探针。</summary>
        private readonly IHardwareResourceProbe _hardwareProbe;

        /// <summary>可替换的资源档案计算策略。</summary>
        private readonly IResourceProfileCalculator _calculator;

        /// <summary>当前计算参数。</summary>
        private readonly ResourceProfileCalculationOptions _options;

        /// <summary>首次读取后缓存的硬件快照。</summary>
        private HardwareResourceSnapshot _hardwareSnapshot;

        /// <summary>
        /// 创建运行资源档案提供器。
        /// </summary>
        /// <param name="hardwareProbe">硬件资源探针。</param>
        /// <param name="calculator">资源档案计算策略。</param>
        /// <param name="options">资源档案计算参数。</param>
        public RuntimeResourceProfileProvider(
            IHardwareResourceProbe hardwareProbe,
            IResourceProfileCalculator calculator,
            ResourceProfileCalculationOptions options)
        {
            _hardwareProbe = hardwareProbe ?? throw new ArgumentNullException(nameof(hardwareProbe));
            _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
            _options = options ?? new ResourceProfileCalculationOptions();
        }

        /// <summary>
        /// 创建使用Windows硬件探针和默认计算公式的提供器。
        /// </summary>
        /// <returns>默认运行资源档案提供器。</returns>
        public static RuntimeResourceProfileProvider CreateDefault()
        {
            return new RuntimeResourceProfileProvider(
                new WindowsHardwareResourceProbe(),
                new ResourceProfileCalculator(),
                new ResourceProfileCalculationOptions());
        }

        /// <summary>
        /// 根据当前方案规模生成资源档案，硬件读取失败时自动使用保守快照。
        /// </summary>
        /// <param name="workload">当前方案工作负载。</param>
        /// <returns>自动资源档案。</returns>
        public AutomaticResourceProfile BuildProfile(ResourceWorkloadSnapshot workload)
        {
            HardwareResourceSnapshot hardware = GetHardwareSnapshot();
            return _calculator.Calculate(hardware, workload, _options);
        }

        /// <summary>
        /// 清除硬件快照缓存，下一次计算时重新读取硬件。
        /// </summary>
        public void ResetHardwareCache()
        {
            lock (_hardwareLock)
            {
                _hardwareSnapshot = null;
            }
        }

        /// <summary>
        /// 获取缓存硬件快照；探针异常时转换为保守快照。
        /// </summary>
        /// <returns>可用于计算的硬件快照。</returns>
        private HardwareResourceSnapshot GetHardwareSnapshot()
        {
            lock (_hardwareLock)
            {
                if (_hardwareSnapshot != null)
                    return _hardwareSnapshot;

                try
                {
                    _hardwareSnapshot = _hardwareProbe.Capture();
                    if (_hardwareSnapshot == null)
                        _hardwareSnapshot = HardwareResourceSnapshot.CreateFallback("硬件探针返回空快照。");
                }
                catch (Exception ex)
                {
                    _hardwareSnapshot = HardwareResourceSnapshot.CreateFallback("硬件探针异常：" + ex.Message);
                }

                return _hardwareSnapshot;
            }
        }
    }
}
