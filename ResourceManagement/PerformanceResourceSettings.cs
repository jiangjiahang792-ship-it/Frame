using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;

namespace TDJS_Vision.ResourceManagement
{
    /// <summary>
    /// 性能与资源设置使用的稳定配置键。
    /// </summary>
    public static class PerformanceResourceSettingKeys
    {
        /// <summary>启用CPU动态调节等自适应资源策略。</summary>
        public const string AdaptiveResourceManagementEnabled = "AdaptiveResourceManagementEnabled";

        /// <summary>系统保留物理核心数。</summary>
        public const string CpuReserveCoreCount = "CpuReserveCoreCount";

        /// <summary>CPU重任务最大并发数。</summary>
        public const string CpuHeavyMaxConcurrency = "CpuHeavyMaxConcurrency";

        /// <summary>图像转换最大并发数。</summary>
        public const string ImageConvertMaxConcurrency = "ImageConvertMaxConcurrency";

        /// <summary>图片保存工作线程数。</summary>
        public const string ImageSaveWorkerCount = "ImageSaveWorkerCount";

        /// <summary>CPU高水位百分比。</summary>
        public const string CpuHighWatermarkPercent = "CpuHighWatermarkPercent";

        /// <summary>CPU恢复水位百分比。</summary>
        public const string CpuRecoveryWatermarkPercent = "CpuRecoveryWatermarkPercent";

        /// <summary>CPU高水位持续秒数。</summary>
        public const string CpuHighDurationSeconds = "CpuHighDurationSeconds";

        /// <summary>CPU恢复水位持续秒数。</summary>
        public const string CpuRecoveryDurationSeconds = "CpuRecoveryDurationSeconds";

        /// <summary>资源策略评估周期，单位毫秒。</summary>
        public const string ResourceAdjustmentIntervalMs = "ResourceAdjustmentIntervalMs";

        /// <summary>CPU并发调整冷却秒数。</summary>
        public const string ResourceAdjustmentCooldownSeconds = "ResourceAdjustmentCooldownSeconds";

        /// <summary>单次并发调整步长。</summary>
        public const string ConcurrencyAdjustmentStep = "ConcurrencyAdjustmentStep";

        /// <summary>流程图像预算占总内存百分比。</summary>
        public const string FlowImageMemoryBudgetPercent = "FlowImageMemoryBudgetPercent";

        /// <summary>保存队列预算占总内存百分比。</summary>
        public const string SaveQueueMemoryBudgetPercent = "SaveQueueMemoryBudgetPercent";

        /// <summary>可用内存低水位百分比。</summary>
        public const string MemoryLowWatermarkPercent = "MemoryLowWatermarkPercent";

        /// <summary>可用内存严重水位百分比。</summary>
        public const string MemoryCriticalWatermarkPercent = "MemoryCriticalWatermarkPercent";

        /// <summary>可用内存低水位最小值，单位MB。</summary>
        public const string MemoryLowWatermarkMinimumMb = "MemoryLowWatermarkMinimumMb";

        /// <summary>可用内存严重水位最小值，单位MB。</summary>
        public const string MemoryCriticalMinimumMb = "MemoryCriticalMinimumMb";

        /// <summary>图像资源大小滚动采样数量。</summary>
        public const string ImageSizeSampleCount = "ImageSizeSampleCount";

        /// <summary>停止时资源排空等待毫秒数。</summary>
        public const string ShutdownResourceDrainTimeoutMs = "ShutdownResourceDrainTimeoutMs";

        /// <summary>每个图像窗口最大刷新帧率。</summary>
        public const string UiMaxRefreshFps = "UiMaxRefreshFps";

        /// <summary>尚无图像样本时的保存队列初始容量。</summary>
        public const string SaveQueueInitialCapacity = "SaveQueueInitialCapacity";

        /// <summary>保存队列自动计算容量上限。</summary>
        public const string SaveQueueMaximumCapacity = "SaveQueueMaximumCapacity";

        /// <summary>普通图片停止保存的磁盘低空间水位。</summary>
        public const string SaveDiskLowSpaceMb = "SaveDiskLowSpaceMb";

        /// <summary>全部图片停止保存的磁盘严重空间水位。</summary>
        public const string SaveDiskCriticalSpaceMb = "SaveDiskCriticalSpaceMb";

        /// <summary>单个保存目标慢写阈值。</summary>
        public const string SaveSlowThresholdMs = "SaveSlowThresholdMs";

        /// <summary>保存磁盘空间探测缓存周期。</summary>
        public const string SaveDiskProbeIntervalMs = "SaveDiskProbeIntervalMs";
    }

    /// <summary>
    /// 当前计算机独立保存的性能与资源设置；不参与方案序列化。
    /// </summary>
    public sealed class MachinePerformanceResourceSettings
    {
        /// <summary>当前配置文件结构版本。</summary>
        public int SchemaVersion { get; set; } = 1;

        /// <summary>是否允许CPU并发根据全机负载动态下降和恢复。</summary>
        public bool AdaptiveResourceManagementEnabled { get; set; } = true;

        /// <summary>稳定配置键到手动值的映射；不存在的键继续使用自动推荐。</summary>
        public Dictionary<string, string> ManualOverrides { get; set; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// 创建与当前设置互不共享字典的快照。
        /// </summary>
        /// <returns>可安全交给运行线程读取的配置快照。</returns>
        public MachinePerformanceResourceSettings Clone()
        {
            return new MachinePerformanceResourceSettings
            {
                SchemaVersion = Math.Max(1, SchemaVersion),
                AdaptiveResourceManagementEnabled = AdaptiveResourceManagementEnabled,
                ManualOverrides = ManualOverrides == null
                    ? new Dictionary<string, string>(StringComparer.Ordinal)
                    : new Dictionary<string, string>(ManualOverrides, StringComparer.Ordinal)
            };
        }
    }

    /// <summary>
    /// 机器性能配置的可替换持久化接口。
    /// </summary>
    public interface IPerformanceResourceSettingsStore
    {
        /// <summary>读取机器性能配置，文件不存在时返回默认设置。</summary>
        MachinePerformanceResourceSettings Load();

        /// <summary>原子保存机器性能配置。</summary>
        void Save(MachinePerformanceResourceSettings settings);
    }

    /// <summary>
    /// 把机器性能设置保存到本机LocalApplicationData目录的JSON存储器。
    /// </summary>
    public sealed class JsonPerformanceResourceSettingsStore : IPerformanceResourceSettingsStore
    {
        /// <summary>配置文件名。</summary>
        public const string DefaultFileName = "performance-resource-settings.json";

        /// <summary>配置文件完整路径。</summary>
        private readonly string _filePath;

        /// <summary>
        /// 创建使用指定文件的机器配置存储器。
        /// </summary>
        /// <param name="filePath">配置文件完整路径。</param>
        public JsonPerformanceResourceSettingsStore(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("机器性能配置路径不能为空。", nameof(filePath));

            _filePath = Path.GetFullPath(filePath);
        }

        /// <summary>
        /// 创建默认机器配置存储器。
        /// </summary>
        /// <returns>位于本机用户LocalApplicationData目录的存储器。</returns>
        public static JsonPerformanceResourceSettingsStore CreateDefault()
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TDJS-Vision");
            return new JsonPerformanceResourceSettingsStore(Path.Combine(directory, DefaultFileName));
        }

        /// <summary>
        /// 读取机器性能配置，缺失文件使用默认设置，损坏文件向调用方报告异常。
        /// </summary>
        /// <returns>已经规范化字典比较规则的设置。</returns>
        public MachinePerformanceResourceSettings Load()
        {
            if (!File.Exists(_filePath))
                return new MachinePerformanceResourceSettings();

            string json = File.ReadAllText(_filePath);
            MachinePerformanceResourceSettings settings =
                JsonConvert.DeserializeObject<MachinePerformanceResourceSettings>(json)
                ?? new MachinePerformanceResourceSettings();
            return settings.Clone();
        }

        /// <summary>
        /// 使用同目录临时文件和替换操作保存完整配置，避免中途退出留下半份JSON。
        /// </summary>
        /// <param name="settings">待保存的完整配置。</param>
        public void Save(MachinePerformanceResourceSettings settings)
        {
            MachinePerformanceResourceSettings snapshot =
                (settings ?? new MachinePerformanceResourceSettings()).Clone();
            string directory = Path.GetDirectoryName(_filePath);
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("机器性能配置目录无效。");

            Directory.CreateDirectory(directory);
            string temporaryPath = _filePath + ".tmp";
            string json = JsonConvert.SerializeObject(snapshot, Formatting.Indented);
            File.WriteAllText(temporaryPath, json);

            try
            {
                if (File.Exists(_filePath))
                {
                    File.Replace(temporaryPath, _filePath, null, true);
                }
                else
                {
                    File.Move(temporaryPath, _filePath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>
    /// 缓存并发布机器性能配置不可变快照的进程级服务。
    /// </summary>
    public sealed class PerformanceResourceSettingsProvider
    {
        /// <summary>默认服务的延迟初始化器。</summary>
        private static readonly Lazy<PerformanceResourceSettingsProvider> DefaultProvider =
            new Lazy<PerformanceResourceSettingsProvider>(
                () => new PerformanceResourceSettingsProvider(JsonPerformanceResourceSettingsStore.CreateDefault()),
                true);

        /// <summary>只保护首次加载和显式重新加载，不参与正常快照读取。</summary>
        private readonly object _loadSyncRoot = new object();

        /// <summary>串行化配置文件读取和保存，避免多次写入互相覆盖临时文件。</summary>
        private readonly object _persistenceSyncRoot = new object();

        /// <summary>可替换的配置存储器。</summary>
        private readonly IPerformanceResourceSettingsStore _store;

        /// <summary>首次访问后缓存的配置快照。</summary>
        private MachinePerformanceResourceSettings _settings;

        /// <summary>最近一次加载失败原因；成功时为空。</summary>
        private string _lastLoadWarning = string.Empty;

        /// <summary>
        /// 创建使用指定存储器的配置服务。
        /// </summary>
        /// <param name="store">机器配置存储器。</param>
        public PerformanceResourceSettingsProvider(IPerformanceResourceSettingsStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>全程序共享的默认机器配置服务。</summary>
        public static PerformanceResourceSettingsProvider Default => DefaultProvider.Value;

        /// <summary>
        /// 读取当前配置快照；损坏文件使用安全默认值并保留告警原因。
        /// </summary>
        /// <returns>与内部缓存不共享字典的快照。</returns>
        public MachinePerformanceResourceSettings GetSnapshot()
        {
            MachinePerformanceResourceSettings snapshot =
                Interlocked.CompareExchange(ref _settings, null, null);
            if (snapshot != null)
                return snapshot.Clone();

            lock (_loadSyncRoot)
            {
                snapshot = Interlocked.CompareExchange(ref _settings, null, null);
                if (snapshot == null)
                {
                    string warning;
                    lock (_persistenceSyncRoot)
                    {
                        snapshot = LoadOrCreateDefault(out warning);
                        Interlocked.Exchange(ref _lastLoadWarning, warning);
                        Interlocked.Exchange(ref _settings, snapshot);
                    }
                }
            }

            return snapshot.Clone();
        }

        /// <summary>
        /// 读取最近一次配置加载告警。
        /// </summary>
        /// <returns>没有告警时返回空字符串。</returns>
        public string GetLastLoadWarning()
        {
            GetSnapshot();
            return Interlocked.CompareExchange(ref _lastLoadWarning, null, null) ?? string.Empty;
        }

        /// <summary>
        /// 一次性保存并发布新的完整配置快照。
        /// </summary>
        /// <param name="settings">经过界面完整校验的设置。</param>
        public void Save(MachinePerformanceResourceSettings settings)
        {
            MachinePerformanceResourceSettings snapshot =
                (settings ?? new MachinePerformanceResourceSettings()).Clone();
            lock (_persistenceSyncRoot)
            {
                _store.Save(snapshot);
                Interlocked.Exchange(ref _settings, snapshot);
                Interlocked.Exchange(ref _lastLoadWarning, string.Empty);
            }
        }

        /// <summary>
        /// 清除内存缓存，下一次访问重新读取配置文件。
        /// </summary>
        public void Reload()
        {
            lock (_loadSyncRoot)
            {
                Interlocked.Exchange(ref _settings, null);
                Interlocked.Exchange(ref _lastLoadWarning, string.Empty);
            }
        }

        /// <summary>从持久化存储读取配置，异常只降级到默认值。</summary>
        private MachinePerformanceResourceSettings LoadOrCreateDefault(out string warning)
        {
            try
            {
                warning = string.Empty;
                return (_store.Load() ?? new MachinePerformanceResourceSettings()).Clone();
            }
            catch (Exception ex)
            {
                warning = "机器性能配置读取失败，已使用自动推荐：" + ex.Message;
                return new MachinePerformanceResourceSettings();
            }
        }
    }

    /// <summary>
    /// 同一次计算得到的自动推荐和最终生效资源档案。
    /// </summary>
    public sealed class ResourceProfileBuildResult
    {
        /// <summary>未应用任何机器手动覆盖的自动推荐档案。</summary>
        public AutomaticResourceProfile AutomaticProfile { get; set; }

        /// <summary>应用机器手动覆盖并完成安全边界校验的最终档案。</summary>
        public AutomaticResourceProfile EffectiveProfile { get; set; }

        /// <summary>参与本次计算的机器配置快照。</summary>
        public MachinePerformanceResourceSettings Settings { get; set; }

        /// <summary>按稳定配置键保存的自动参数值，供系统设置界面直接显示。</summary>
        public Dictionary<string, string> AutomaticParameterValues { get; set; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>按稳定配置键保存的最终参数值，已经完成范围和关联边界收敛。</summary>
        public Dictionary<string, string> EffectiveParameterValues { get; set; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// 在自动计算完成后合并机器手动值，并统一执行关联参数安全边界。
    /// </summary>
    public sealed class ResourceProfileOverrideResolver
    {
        /// <summary>资源档案计算器。</summary>
        private readonly IResourceProfileCalculator _calculator;

        /// <summary>
        /// 创建资源档案覆盖合并器。
        /// </summary>
        /// <param name="calculator">自动资源档案计算器。</param>
        public ResourceProfileOverrideResolver(IResourceProfileCalculator calculator)
        {
            _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        }

        /// <summary>
        /// 分别生成自动档案和最终生效档案。
        /// </summary>
        /// <param name="hardware">硬件快照。</param>
        /// <param name="workload">方案工作负载。</param>
        /// <param name="baseOptions">框架默认计算参数。</param>
        /// <param name="settings">机器手动覆盖。</param>
        /// <returns>自动值、手动配置和最终值的同批结果。</returns>
        public ResourceProfileBuildResult Build(
            HardwareResourceSnapshot hardware,
            ResourceWorkloadSnapshot workload,
            ResourceProfileCalculationOptions baseOptions,
            MachinePerformanceResourceSettings settings)
        {
            settings = (settings ?? new MachinePerformanceResourceSettings()).Clone();
            ResourceProfileCalculationOptions automaticOptions = CloneOptions(baseOptions);
            AutomaticResourceProfile automaticProfile = _calculator.Calculate(hardware, workload, automaticOptions);

            bool hasManualOverrides = settings.ManualOverrides != null && settings.ManualOverrides.Count > 0;
            ResourceProfileCalculationOptions effectiveOptions = CloneOptions(baseOptions);
            AutomaticResourceProfile effectiveProfile;
            if (hasManualOverrides)
            {
                ApplyCalculationOverrides(effectiveOptions, settings);
                effectiveProfile = _calculator.Calculate(hardware, workload, effectiveOptions);
                ApplyFinalProfileOverrides(effectiveProfile, settings, effectiveOptions);
            }
            else
            {
                effectiveProfile = CloneProfile(automaticProfile);
            }

            effectiveProfile.AdaptiveResourceManagementEnabled = settings.AdaptiveResourceManagementEnabled;

            return new ResourceProfileBuildResult
            {
                AutomaticProfile = automaticProfile,
                EffectiveProfile = effectiveProfile,
                Settings = settings,
                AutomaticParameterValues = CreateParameterValues(automaticOptions, automaticProfile),
                EffectiveParameterValues = CreateParameterValues(effectiveOptions, effectiveProfile)
            };
        }

        /// <summary>把计算输入和档案输出映射为系统设置界面使用的稳定参数值。</summary>
        private static Dictionary<string, string> CreateParameterValues(
            ResourceProfileCalculationOptions options,
            AutomaticResourceProfile profile)
        {
            options = options ?? new ResourceProfileCalculationOptions();
            profile = profile ?? new AutomaticResourceProfile();
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [PerformanceResourceSettingKeys.AdaptiveResourceManagementEnabled] =
                    profile.AdaptiveResourceManagementEnabled ? bool.TrueString : bool.FalseString,
                [PerformanceResourceSettingKeys.CpuReserveCoreCount] = FormatNumber(profile.CpuReserveCoreCount),
                [PerformanceResourceSettingKeys.CpuHeavyMaxConcurrency] = FormatNumber(profile.CpuHeavyMaxConcurrency),
                [PerformanceResourceSettingKeys.ImageConvertMaxConcurrency] = FormatNumber(profile.ImageConvertMaxConcurrency),
                [PerformanceResourceSettingKeys.ImageSaveWorkerCount] = FormatNumber(profile.ImageSaveWorkerCount),
                [PerformanceResourceSettingKeys.CpuHighWatermarkPercent] = FormatNumber(profile.CpuHighWatermarkPercent),
                [PerformanceResourceSettingKeys.CpuRecoveryWatermarkPercent] = FormatNumber(profile.CpuRecoveryWatermarkPercent),
                [PerformanceResourceSettingKeys.CpuHighDurationSeconds] = FormatNumber(profile.CpuHighDurationMs / 1000D),
                [PerformanceResourceSettingKeys.CpuRecoveryDurationSeconds] = FormatNumber(profile.CpuRecoveryDurationMs / 1000D),
                [PerformanceResourceSettingKeys.ResourceAdjustmentIntervalMs] = FormatNumber(profile.ResourceAdjustmentIntervalMs),
                [PerformanceResourceSettingKeys.ResourceAdjustmentCooldownSeconds] = FormatNumber(profile.ResourceAdjustmentCooldownMs / 1000D),
                [PerformanceResourceSettingKeys.ConcurrencyAdjustmentStep] = FormatNumber(profile.ConcurrencyAdjustmentStep),
                [PerformanceResourceSettingKeys.FlowImageMemoryBudgetPercent] = FormatNumber(options.FlowImageTotalMemoryPercent * 100D),
                [PerformanceResourceSettingKeys.SaveQueueMemoryBudgetPercent] = FormatNumber(options.SaveQueueTotalMemoryPercent * 100D),
                [PerformanceResourceSettingKeys.MemoryLowWatermarkPercent] = FormatNumber(options.MemoryLowWatermarkPercent * 100D),
                [PerformanceResourceSettingKeys.MemoryCriticalWatermarkPercent] = FormatNumber(options.MemoryCriticalWatermarkPercent * 100D),
                [PerformanceResourceSettingKeys.MemoryLowWatermarkMinimumMb] = FormatNumber(options.MemoryLowWatermarkMinimumMb),
                [PerformanceResourceSettingKeys.MemoryCriticalMinimumMb] = FormatNumber(options.MemoryCriticalWatermarkMinimumMb),
                [PerformanceResourceSettingKeys.ImageSizeSampleCount] = FormatNumber(profile.ImageSizeSampleCount),
                [PerformanceResourceSettingKeys.ShutdownResourceDrainTimeoutMs] = FormatNumber(profile.ShutdownResourceDrainTimeoutMs),
                [PerformanceResourceSettingKeys.UiMaxRefreshFps] = FormatNumber(profile.UiMaxRefreshFps),
                [PerformanceResourceSettingKeys.SaveQueueInitialCapacity] = FormatNumber(options.SaveQueueInitialCapacity),
                [PerformanceResourceSettingKeys.SaveQueueMaximumCapacity] = FormatNumber(options.SaveQueueMaximumCapacity),
                [PerformanceResourceSettingKeys.SaveDiskLowSpaceMb] = FormatNumber(profile.SaveDiskLowSpaceMb),
                [PerformanceResourceSettingKeys.SaveDiskCriticalSpaceMb] = FormatNumber(profile.SaveDiskCriticalSpaceMb),
                [PerformanceResourceSettingKeys.SaveSlowThresholdMs] = FormatNumber(profile.SaveSlowThresholdMs),
                [PerformanceResourceSettingKeys.SaveDiskProbeIntervalMs] = FormatNumber(profile.SaveDiskProbeIntervalMs)
            };
        }

        /// <summary>使用不受系统区域设置影响的紧凑数字格式。</summary>
        private static string FormatNumber(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        /// <summary>把影响计算公式的百分比、容量和阈值覆盖到计算参数副本。</summary>
        private static void ApplyCalculationOverrides(
            ResourceProfileCalculationOptions options,
            MachinePerformanceResourceSettings settings)
        {
            options.FlowImageTotalMemoryPercent = GetPercentOverride(
                settings,
                PerformanceResourceSettingKeys.FlowImageMemoryBudgetPercent,
                options.FlowImageTotalMemoryPercent,
                5D,
                30D);
            options.SaveQueueTotalMemoryPercent = GetPercentOverride(
                settings,
                PerformanceResourceSettingKeys.SaveQueueMemoryBudgetPercent,
                options.SaveQueueTotalMemoryPercent,
                1D,
                15D);
            options.MemoryLowWatermarkPercent = GetPercentOverride(
                settings,
                PerformanceResourceSettingKeys.MemoryLowWatermarkPercent,
                options.MemoryLowWatermarkPercent,
                5D,
                40D);
            options.MemoryCriticalWatermarkPercent = GetPercentOverride(
                settings,
                PerformanceResourceSettingKeys.MemoryCriticalWatermarkPercent,
                options.MemoryCriticalWatermarkPercent,
                2D,
                Math.Max(2D, options.MemoryLowWatermarkPercent * 100D - 2D));
            options.MemoryLowWatermarkMinimumMb = GetIntOverride(
                settings,
                PerformanceResourceSettingKeys.MemoryLowWatermarkMinimumMb,
                options.MemoryLowWatermarkMinimumMb,
                256,
                8192);
            options.MemoryCriticalWatermarkMinimumMb = GetIntOverride(
                settings,
                PerformanceResourceSettingKeys.MemoryCriticalMinimumMb,
                options.MemoryCriticalWatermarkMinimumMb,
                128,
                Math.Min(4096, Math.Max(128, options.MemoryLowWatermarkMinimumMb - 1)));
            options.ImageSizeSampleCount = GetIntOverride(
                settings,
                PerformanceResourceSettingKeys.ImageSizeSampleCount,
                options.ImageSizeSampleCount,
                1,
                500);
            options.ShutdownResourceDrainTimeoutMs = GetIntOverride(
                settings,
                PerformanceResourceSettingKeys.ShutdownResourceDrainTimeoutMs,
                options.ShutdownResourceDrainTimeoutMs,
                0,
                60000);
            options.UiMaxRefreshFps = GetIntOverride(
                settings,
                PerformanceResourceSettingKeys.UiMaxRefreshFps,
                options.UiMaxRefreshFps,
                PerWindowUiFrameRateLimiter.MinimumFramesPerSecond,
                PerWindowUiFrameRateLimiter.MaximumFramesPerSecond);
            options.SaveQueueInitialCapacity = GetIntOverride(
                settings,
                PerformanceResourceSettingKeys.SaveQueueInitialCapacity,
                options.SaveQueueInitialCapacity,
                1,
                50);
            options.SaveQueueMaximumCapacity = GetIntOverride(
                settings,
                PerformanceResourceSettingKeys.SaveQueueMaximumCapacity,
                options.SaveQueueMaximumCapacity,
                1,
                500);
            options.SaveQueueInitialCapacity = Math.Min(
                options.SaveQueueInitialCapacity,
                options.SaveQueueMaximumCapacity);
            options.SaveDiskLowSpaceMb = GetIntOverride(
                settings,
                PerformanceResourceSettingKeys.SaveDiskLowSpaceMb,
                options.SaveDiskLowSpaceMb,
                256,
                102400);
            options.SaveDiskCriticalSpaceMb = GetIntOverride(
                settings,
                PerformanceResourceSettingKeys.SaveDiskCriticalSpaceMb,
                options.SaveDiskCriticalSpaceMb,
                64,
                Math.Max(64, options.SaveDiskLowSpaceMb - 1));
            options.SaveSlowThresholdMs = GetIntOverride(
                settings,
                PerformanceResourceSettingKeys.SaveSlowThresholdMs,
                options.SaveSlowThresholdMs,
                10,
                60000);
            options.SaveDiskProbeIntervalMs = GetIntOverride(
                settings,
                PerformanceResourceSettingKeys.SaveDiskProbeIntervalMs,
                options.SaveDiskProbeIntervalMs,
                100,
                60000);
        }

        /// <summary>把用户直接指定的最终值覆盖到已计算档案，并执行成对参数约束。</summary>
        private static void ApplyFinalProfileOverrides(
            AutomaticResourceProfile profile,
            MachinePerformanceResourceSettings settings,
            ResourceProfileCalculationOptions options)
        {
            HardwareResourceSnapshot hardware = profile.Hardware ?? HardwareResourceSnapshot.CreateFallback("手动资源覆盖缺少硬件快照。");
            int physicalCores = Math.Max(1, hardware.PhysicalCoreCount);
            profile.CpuReserveCoreCount = GetIntOverride(
                settings,
                PerformanceResourceSettingKeys.CpuReserveCoreCount,
                profile.CpuReserveCoreCount,
                1,
                Math.Max(1, physicalCores - 1));
            int availableLogicalProcessors = hardware.AvailableLogicalProcessorCount > 0
                ? hardware.AvailableLogicalProcessorCount
                : Math.Max(1, hardware.LogicalProcessorCount);
            int effectiveCoreCount = Math.Max(1, Math.Min(physicalCores, availableLogicalProcessors));
            int availableCpuHeavyConcurrency = Math.Max(
                1,
                Math.Min(physicalCores - profile.CpuReserveCoreCount, effectiveCoreCount));
            int enabledProcessCount = Math.Max(1, profile.Workload?.EnabledProcessCount ?? 0);
            int automaticWithManualReserve = Math.Min(
                Math.Max(1, options?.CpuHeavyConcurrencyMaximum ?? 8),
                Math.Min(enabledProcessCount, availableCpuHeavyConcurrency));
            profile.CpuHeavyMaxConcurrency = GetIntOverride(
                settings,
                PerformanceResourceSettingKeys.CpuHeavyMaxConcurrency,
                automaticWithManualReserve,
                1,
                Math.Min(32, availableCpuHeavyConcurrency));
            profile.ImageConvertMaxConcurrency = GetIntOverride(
                settings,
                PerformanceResourceSettingKeys.ImageConvertMaxConcurrency,
                profile.ImageConvertMaxConcurrency,
                1,
                Math.Min(8, profile.CpuHeavyMaxConcurrency));
            profile.ImageSaveWorkerCount = GetIntOverride(
                settings,
                PerformanceResourceSettingKeys.ImageSaveWorkerCount,
                profile.ImageSaveWorkerCount,
                1,
                8);
            profile.CpuHighWatermarkPercent = GetDoubleOverride(
                settings,
                PerformanceResourceSettingKeys.CpuHighWatermarkPercent,
                profile.CpuHighWatermarkPercent,
                50D,
                100D);
            profile.CpuRecoveryWatermarkPercent = GetDoubleOverride(
                settings,
                PerformanceResourceSettingKeys.CpuRecoveryWatermarkPercent,
                profile.CpuRecoveryWatermarkPercent,
                20D,
                Math.Max(20D, profile.CpuHighWatermarkPercent - 5D));
            profile.CpuHighDurationMs = SecondsToMilliseconds(GetIntOverride(
                settings,
                PerformanceResourceSettingKeys.CpuHighDurationSeconds,
                Math.Max(1, profile.CpuHighDurationMs / 1000),
                1,
                60));
            profile.CpuRecoveryDurationMs = SecondsToMilliseconds(GetIntOverride(
                settings,
                PerformanceResourceSettingKeys.CpuRecoveryDurationSeconds,
                Math.Max(1, profile.CpuRecoveryDurationMs / 1000),
                1,
                300));
            profile.ResourceAdjustmentIntervalMs = GetIntOverride(
                settings,
                PerformanceResourceSettingKeys.ResourceAdjustmentIntervalMs,
                profile.ResourceAdjustmentIntervalMs,
                1000,
                60000);
            profile.ResourceAdjustmentCooldownMs = SecondsToMilliseconds(GetIntOverride(
                settings,
                PerformanceResourceSettingKeys.ResourceAdjustmentCooldownSeconds,
                Math.Max(5, profile.ResourceAdjustmentCooldownMs / 1000),
                5,
                600));
            profile.ConcurrencyAdjustmentStep = GetIntOverride(
                settings,
                PerformanceResourceSettingKeys.ConcurrencyAdjustmentStep,
                profile.ConcurrencyAdjustmentStep,
                1,
                4);
        }

        /// <summary>复制全部自动计算参数，避免机器覆盖修改框架默认对象。</summary>
        private static ResourceProfileCalculationOptions CloneOptions(ResourceProfileCalculationOptions source)
        {
            source = source ?? new ResourceProfileCalculationOptions();
            return new ResourceProfileCalculationOptions
            {
                CpuReservePercent = source.CpuReservePercent,
                CpuReserveMinimum = source.CpuReserveMinimum,
                CpuHeavyConcurrencyMaximum = source.CpuHeavyConcurrencyMaximum,
                CpuHighWatermarkPercent = source.CpuHighWatermarkPercent,
                CpuRecoveryWatermarkPercent = source.CpuRecoveryWatermarkPercent,
                CpuHighDurationMs = source.CpuHighDurationMs,
                CpuRecoveryDurationMs = source.CpuRecoveryDurationMs,
                ResourceAdjustmentIntervalMs = source.ResourceAdjustmentIntervalMs,
                ResourceAdjustmentCooldownMs = source.ResourceAdjustmentCooldownMs,
                ConcurrencyAdjustmentStep = source.ConcurrencyAdjustmentStep,
                ImageConvertCoreDivisor = source.ImageConvertCoreDivisor,
                ImageConvertConcurrencyMaximum = source.ImageConvertConcurrencyMaximum,
                ImageSaveWorkerCoreDivisor = source.ImageSaveWorkerCoreDivisor,
                ImageSaveWorkerMaximum = source.ImageSaveWorkerMaximum,
                FlowImageTotalMemoryPercent = source.FlowImageTotalMemoryPercent,
                FlowImageAvailableMemoryPercent = source.FlowImageAvailableMemoryPercent,
                FlowImageBudgetMaximumMb = source.FlowImageBudgetMaximumMb,
                SaveQueueTotalMemoryPercent = source.SaveQueueTotalMemoryPercent,
                SaveQueueAvailableMemoryPercent = source.SaveQueueAvailableMemoryPercent,
                SaveQueueBudgetMaximumMb = source.SaveQueueBudgetMaximumMb,
                MemoryLowWatermarkPercent = source.MemoryLowWatermarkPercent,
                MemoryLowWatermarkMinimumMb = source.MemoryLowWatermarkMinimumMb,
                MemoryCriticalWatermarkPercent = source.MemoryCriticalWatermarkPercent,
                MemoryCriticalWatermarkMinimumMb = source.MemoryCriticalWatermarkMinimumMb,
                SaveQueueInitialCapacity = source.SaveQueueInitialCapacity,
                SaveQueueMinimumCapacity = source.SaveQueueMinimumCapacity,
                SaveQueueMaximumCapacity = source.SaveQueueMaximumCapacity,
                UiMaxRefreshFps = source.UiMaxRefreshFps,
                ShutdownResourceDrainTimeoutMs = source.ShutdownResourceDrainTimeoutMs,
                ImageSizeSampleCount = source.ImageSizeSampleCount,
                SaveDiskLowSpaceMb = source.SaveDiskLowSpaceMb,
                SaveDiskCriticalSpaceMb = source.SaveDiskCriticalSpaceMb,
                SaveSlowThresholdMs = source.SaveSlowThresholdMs,
                SaveDiskProbeIntervalMs = source.SaveDiskProbeIntervalMs
            };
        }

        /// <summary>复制自动档案，保证自动值与最终值可以被界面独立读取。</summary>
        private static AutomaticResourceProfile CloneProfile(AutomaticResourceProfile source)
        {
            source = source ?? new AutomaticResourceProfile();
            return new AutomaticResourceProfile
            {
                AdaptiveResourceManagementEnabled = source.AdaptiveResourceManagementEnabled,
                CreatedAt = source.CreatedAt,
                Hardware = source.Hardware,
                Workload = source.Workload,
                CpuReserveCoreCount = source.CpuReserveCoreCount,
                CpuHeavyMaxConcurrency = source.CpuHeavyMaxConcurrency,
                CpuHighWatermarkPercent = source.CpuHighWatermarkPercent,
                CpuRecoveryWatermarkPercent = source.CpuRecoveryWatermarkPercent,
                CpuHighDurationMs = source.CpuHighDurationMs,
                CpuRecoveryDurationMs = source.CpuRecoveryDurationMs,
                ResourceAdjustmentIntervalMs = source.ResourceAdjustmentIntervalMs,
                ResourceAdjustmentCooldownMs = source.ResourceAdjustmentCooldownMs,
                ConcurrencyAdjustmentStep = source.ConcurrencyAdjustmentStep,
                ImageConvertMaxConcurrency = source.ImageConvertMaxConcurrency,
                ImageSaveWorkerCount = source.ImageSaveWorkerCount,
                FlowImageMemoryBudgetMb = source.FlowImageMemoryBudgetMb,
                SaveQueueMemoryBudgetMb = source.SaveQueueMemoryBudgetMb,
                MemoryLowWatermarkMb = source.MemoryLowWatermarkMb,
                MemoryCriticalWatermarkMb = source.MemoryCriticalWatermarkMb,
                SaveQueueCalculatedCapacity = source.SaveQueueCalculatedCapacity,
                AiConcurrencyPolicy = source.AiConcurrencyPolicy,
                UiMaxRefreshFps = source.UiMaxRefreshFps,
                UsesInitialSaveQueueCapacity = source.UsesInitialSaveQueueCapacity,
                ShutdownResourceDrainTimeoutMs = source.ShutdownResourceDrainTimeoutMs,
                ImageSizeSampleCount = source.ImageSizeSampleCount,
                SaveDiskLowSpaceMb = source.SaveDiskLowSpaceMb,
                SaveDiskCriticalSpaceMb = source.SaveDiskCriticalSpaceMb,
                SaveSlowThresholdMs = source.SaveSlowThresholdMs,
                SaveDiskProbeIntervalMs = source.SaveDiskProbeIntervalMs
            };
        }

        /// <summary>读取并限制整数手动值，缺失或格式错误时保留自动值。</summary>
        private static int GetIntOverride(
            MachinePerformanceResourceSettings settings,
            string key,
            int automaticValue,
            int minimum,
            int maximum)
        {
            string text;
            int parsed;
            if (!TryGetOverride(settings, key, out text) ||
                !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                parsed = automaticValue;

            return Math.Max(minimum, Math.Min(maximum, parsed));
        }

        /// <summary>读取并限制浮点手动值，缺失或格式错误时保留自动值。</summary>
        private static double GetDoubleOverride(
            MachinePerformanceResourceSettings settings,
            string key,
            double automaticValue,
            double minimum,
            double maximum)
        {
            string text;
            double parsed;
            if (!TryGetOverride(settings, key, out text) ||
                !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ||
                double.IsNaN(parsed) ||
                double.IsInfinity(parsed))
                parsed = automaticValue;

            return Math.Max(minimum, Math.Min(maximum, parsed));
        }

        /// <summary>读取百分比手动值并转换为0～1比例。</summary>
        private static double GetPercentOverride(
            MachinePerformanceResourceSettings settings,
            string key,
            double automaticRatio,
            double minimumPercent,
            double maximumPercent)
        {
            double percent = GetDoubleOverride(
                settings,
                key,
                automaticRatio * 100D,
                minimumPercent,
                maximumPercent);
            return percent / 100D;
        }

        /// <summary>读取非空手动覆盖文本。</summary>
        private static bool TryGetOverride(
            MachinePerformanceResourceSettings settings,
            string key,
            out string value)
        {
            value = null;
            return settings?.ManualOverrides != null &&
                settings.ManualOverrides.TryGetValue(key, out value) &&
                !string.IsNullOrWhiteSpace(value);
        }

        /// <summary>把安全范围内的秒数转换为毫秒。</summary>
        private static int SecondsToMilliseconds(int seconds)
        {
            return checked(seconds * 1000);
        }
    }
}
