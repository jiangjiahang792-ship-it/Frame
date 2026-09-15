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

        /// <summary>机器性能设置快照提供器；为空时只生成自动推荐。</summary>
        private readonly PerformanceResourceSettingsProvider _settingsProvider;

        /// <summary>自动值与机器手动值合并器。</summary>
        private readonly ResourceProfileOverrideResolver _overrideResolver;

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
            : this(hardwareProbe, calculator, options, null)
        {
        }

        /// <summary>
        /// 创建可读取机器性能手动覆盖的运行资源档案提供器。
        /// </summary>
        /// <param name="hardwareProbe">硬件资源探针。</param>
        /// <param name="calculator">资源档案计算器。</param>
        /// <param name="options">框架默认计算参数。</param>
        /// <param name="settingsProvider">机器性能设置快照提供器；为空时保持纯自动模式。</param>
        public RuntimeResourceProfileProvider(
            IHardwareResourceProbe hardwareProbe,
            IResourceProfileCalculator calculator,
            ResourceProfileCalculationOptions options,
            PerformanceResourceSettingsProvider settingsProvider)
        {
            _hardwareProbe = hardwareProbe ?? throw new ArgumentNullException(nameof(hardwareProbe));
            _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
            _options = options ?? new ResourceProfileCalculationOptions();
            _settingsProvider = settingsProvider;
            _overrideResolver = new ResourceProfileOverrideResolver(_calculator);
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
                new ResourceProfileCalculationOptions(),
                PerformanceResourceSettingsProvider.Default);
        }

        /// <summary>
        /// 根据当前方案规模生成资源档案，硬件读取失败时自动使用保守快照。
        /// </summary>
        /// <param name="workload">当前方案工作负载。</param>
        /// <returns>自动资源档案。</returns>
        public AutomaticResourceProfile BuildProfile(ResourceWorkloadSnapshot workload)
        {
            return BuildProfileResult(workload).EffectiveProfile;
        }

        /// <summary>
        /// 在同一硬件和工作负载快照下生成自动推荐与最终生效档案。
        /// </summary>
        /// <param name="workload">当前方案工作负载。</param>
        /// <returns>可直接供系统设置界面对照显示的同批结果。</returns>
        public ResourceProfileBuildResult BuildProfileResult(ResourceWorkloadSnapshot workload)
        {
            MachinePerformanceResourceSettings settings = _settingsProvider == null
                ? new MachinePerformanceResourceSettings()
                : _settingsProvider.GetSnapshot();
            return BuildProfileResult(workload, settings);
        }

        /// <summary>
        /// 使用指定机器设置草稿生成自动推荐与最终档案，不保存草稿。
        /// </summary>
        /// <param name="workload">当前方案工作负载。</param>
        /// <param name="settings">待预览的机器设置草稿。</param>
        /// <returns>自动值、草稿手动值和最终值的同批结果。</returns>
        public ResourceProfileBuildResult BuildProfileResult(
            ResourceWorkloadSnapshot workload,
            MachinePerformanceResourceSettings settings)
        {
            HardwareResourceSnapshot hardware = GetHardwareSnapshot();
            return _overrideResolver.Build(hardware, workload, _options, settings);
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
