using System;

namespace TDJS_Vision.ResourceManagement
{
    /// <summary>
    /// 按计划书公式计算CPU、内存和保存队列建议值的默认策略。
    /// </summary>
    public sealed class ResourceProfileCalculator : IResourceProfileCalculator
    {
        /// <summary>
        /// 计算自动资源建议，不执行任何全局线程或硬件设置。
        /// </summary>
        /// <param name="hardware">硬件资源快照。</param>
        /// <param name="workload">当前方案工作负载。</param>
        /// <param name="options">计算参数。</param>
        /// <returns>经过边界限制的自动资源档案。</returns>
        public AutomaticResourceProfile Calculate(
            HardwareResourceSnapshot hardware,
            ResourceWorkloadSnapshot workload,
            ResourceProfileCalculationOptions options)
        {
            hardware = hardware ?? HardwareResourceSnapshot.CreateFallback("资源档案没有收到硬件快照。");
            workload = workload ?? new ResourceWorkloadSnapshot();
            options = options ?? new ResourceProfileCalculationOptions();

            int physicalCores = Math.Max(1, hardware.PhysicalCoreCount);
            int availableLogicalProcessors = hardware.AvailableLogicalProcessorCount > 0
                ? hardware.AvailableLogicalProcessorCount
                : Math.Max(1, hardware.LogicalProcessorCount);
            int effectiveCoreCount = Math.Max(1, Math.Min(physicalCores, availableLogicalProcessors));
            int enabledProcesses = Math.Max(1, workload.EnabledProcessCount);
            int cameraCount = Math.Max(1, workload.CameraCount);
            int reserveByPercent = (int)Math.Ceiling(physicalCores * ClampDouble(options.CpuReservePercent, 0D, 0.90D));
            int reserveMaximum = Math.Max(1, physicalCores - 1);
            int reserveCores = Clamp(Math.Max(options.CpuReserveMinimum, reserveByPercent), 1, reserveMaximum);
            int cpuHeavyAvailable = Math.Max(1, Math.Min(physicalCores - reserveCores, effectiveCoreCount));
            int cpuHeavyConcurrency = Math.Min(enabledProcesses, Math.Min(cpuHeavyAvailable, Math.Max(1, options.CpuHeavyConcurrencyMaximum)));
            double cpuHighWatermarkPercent = ClampDouble(options.CpuHighWatermarkPercent, 50D, 100D);
            double cpuRecoveryWatermarkPercent = ClampDouble(
                options.CpuRecoveryWatermarkPercent,
                20D,
                Math.Max(20D, cpuHighWatermarkPercent - 5D));

            int imageConvertByCore = Math.Max(1, effectiveCoreCount / Math.Max(1, options.ImageConvertCoreDivisor));
            int imageConvertConcurrency = Math.Min(
                cpuHeavyConcurrency,
                Math.Min(cameraCount, Math.Min(imageConvertByCore, Math.Max(1, options.ImageConvertConcurrencyMaximum))));
            int imageSaveWorkers = Clamp(
                effectiveCoreCount / Math.Max(1, options.ImageSaveWorkerCoreDivisor),
                1,
                Math.Max(1, options.ImageSaveWorkerMaximum));

            long totalMemoryMb = hardware.TotalMemoryMb > 0 ? hardware.TotalMemoryMb : 4096L;
            long availableMemoryMb = hardware.AvailableMemoryMb > 0
                ? Math.Min(totalMemoryMb, hardware.AvailableMemoryMb)
                : Math.Min(totalMemoryMb, 2048L);
            int flowImageBudgetMb = CalculateMemoryBudgetMb(
                totalMemoryMb,
                availableMemoryMb,
                options.FlowImageTotalMemoryPercent,
                options.FlowImageAvailableMemoryPercent,
                options.FlowImageBudgetMaximumMb);
            int saveQueueBudgetMb = CalculateMemoryBudgetMb(
                totalMemoryMb,
                availableMemoryMb,
                options.SaveQueueTotalMemoryPercent,
                options.SaveQueueAvailableMemoryPercent,
                options.SaveQueueBudgetMaximumMb);
            int lowWatermarkMb = Math.Max(
                Math.Max(1, options.MemoryLowWatermarkMinimumMb),
                (int)Math.Ceiling(totalMemoryMb * ClampDouble(options.MemoryLowWatermarkPercent, 0D, 1D)));
            int criticalWatermarkMb = Math.Max(
                Math.Max(1, options.MemoryCriticalWatermarkMinimumMb),
                (int)Math.Ceiling(totalMemoryMb * ClampDouble(options.MemoryCriticalWatermarkPercent, 0D, 1D)));
            criticalWatermarkMb = Math.Min(criticalWatermarkMb, Math.Max(1, lowWatermarkMb - 1));

            bool useInitialQueueCapacity = workload.AverageImageBytes <= 0;
            int saveQueueCapacity = useInitialQueueCapacity
                ? Clamp(options.SaveQueueInitialCapacity, 1, Math.Max(1, options.SaveQueueMaximumCapacity))
                : CalculateSaveQueueCapacity(saveQueueBudgetMb, workload.AverageImageBytes, options);
            int saveDiskLowSpaceMb = Clamp(options.SaveDiskLowSpaceMb, 256, 102400);
            int saveDiskCriticalSpaceMb = Clamp(
                options.SaveDiskCriticalSpaceMb,
                64,
                Math.Max(64, saveDiskLowSpaceMb - 1));

            return new AutomaticResourceProfile
            {
                CreatedAt = DateTime.Now,
                Hardware = hardware,
                Workload = workload,
                CpuReserveCoreCount = reserveCores,
                CpuHeavyMaxConcurrency = Math.Max(1, cpuHeavyConcurrency),
                CpuHighWatermarkPercent = cpuHighWatermarkPercent,
                CpuRecoveryWatermarkPercent = cpuRecoveryWatermarkPercent,
                CpuHighDurationMs = Clamp(options.CpuHighDurationMs, 1000, 60000),
                CpuRecoveryDurationMs = Clamp(options.CpuRecoveryDurationMs, 1000, 300000),
                ResourceAdjustmentIntervalMs = Clamp(options.ResourceAdjustmentIntervalMs, 1000, 60000),
                ResourceAdjustmentCooldownMs = Clamp(options.ResourceAdjustmentCooldownMs, 5000, 600000),
                ConcurrencyAdjustmentStep = Clamp(options.ConcurrencyAdjustmentStep, 1, 4),
                ImageConvertMaxConcurrency = Math.Max(1, imageConvertConcurrency),
                ImageSaveWorkerCount = imageSaveWorkers,
                FlowImageMemoryBudgetMb = flowImageBudgetMb,
                SaveQueueMemoryBudgetMb = saveQueueBudgetMb,
                MemoryLowWatermarkMb = lowWatermarkMb,
                MemoryCriticalWatermarkMb = criticalWatermarkMb,
                SaveQueueCalculatedCapacity = saveQueueCapacity,
                UiMaxRefreshFps = Clamp(
                    options.UiMaxRefreshFps,
                    PerWindowUiFrameRateLimiter.MinimumFramesPerSecond,
                    PerWindowUiFrameRateLimiter.MaximumFramesPerSecond),
                ShutdownResourceDrainTimeoutMs = Clamp(options.ShutdownResourceDrainTimeoutMs, 0, 60000),
                ImageSizeSampleCount = Clamp(options.ImageSizeSampleCount, 1, 500),
                SaveDiskLowSpaceMb = saveDiskLowSpaceMb,
                SaveDiskCriticalSpaceMb = saveDiskCriticalSpaceMb,
                SaveSlowThresholdMs = Clamp(options.SaveSlowThresholdMs, 10, 60000),
                SaveDiskProbeIntervalMs = Clamp(options.SaveDiskProbeIntervalMs, 100, 60000),
                UsesInitialSaveQueueCapacity = useInitialQueueCapacity,
                AiConcurrencyPolicy = "FollowEditableFlow"
            };
        }

        /// <summary>
        /// 按总内存比例、启动可用内存比例和绝对上限中的最小值计算预算。
        /// </summary>
        /// <param name="totalMemoryMb">总内存，单位MB。</param>
        /// <param name="availableMemoryMb">可用内存，单位MB。</param>
        /// <param name="totalPercent">总内存预算比例。</param>
        /// <param name="availablePercent">可用内存预算比例。</param>
        /// <param name="maximumMb">绝对预算上限，单位MB。</param>
        /// <returns>至少为1MB的内存预算。</returns>
        private static int CalculateMemoryBudgetMb(
            long totalMemoryMb,
            long availableMemoryMb,
            double totalPercent,
            double availablePercent,
            int maximumMb)
        {
            double byTotal = totalMemoryMb * ClampDouble(totalPercent, 0D, 1D);
            double byAvailable = availableMemoryMb * ClampDouble(availablePercent, 0D, 1D);
            double result = Math.Min(Math.Min(byTotal, byAvailable), Math.Max(1, maximumMb));
            return Math.Max(1, (int)Math.Floor(result));
        }

        /// <summary>
        /// 根据保存内存预算和平均任务持有字节数计算有界队列容量。
        /// </summary>
        /// <param name="saveQueueBudgetMb">保存队列内存预算，单位MB。</param>
        /// <param name="averageImageBytes">平均保存任务持有字节数。</param>
        /// <param name="options">队列容量边界参数。</param>
        /// <returns>经过上下限约束的队列容量。</returns>
        private static int CalculateSaveQueueCapacity(
            int saveQueueBudgetMb,
            long averageImageBytes,
            ResourceProfileCalculationOptions options)
        {
            long budgetBytes = (long)saveQueueBudgetMb * 1024L * 1024L;
            long calculated = averageImageBytes <= 0 ? options.SaveQueueInitialCapacity : budgetBytes / averageImageBytes;
            int maximum = Math.Max(1, options.SaveQueueMaximumCapacity);
            int minimum = Clamp(options.SaveQueueMinimumCapacity, 1, maximum);
            int safeCalculated = calculated > int.MaxValue ? maximum : (int)Math.Max(0, calculated);
            return Clamp(safeCalculated, minimum, maximum);
        }

        /// <summary>
        /// 将整数限制在闭区间内。
        /// </summary>
        /// <param name="value">原始值。</param>
        /// <param name="minimum">最小值。</param>
        /// <param name="maximum">最大值。</param>
        /// <returns>限制后的整数。</returns>
        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
                return minimum;
            return value > maximum ? maximum : value;
        }

        /// <summary>
        /// 将浮点数限制在闭区间内。
        /// </summary>
        /// <param name="value">原始值。</param>
        /// <param name="minimum">最小值。</param>
        /// <param name="maximum">最大值。</param>
        /// <returns>限制后的浮点数。</returns>
        private static double ClampDouble(double value, double minimum, double maximum)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return minimum;
            if (value < minimum)
                return minimum;
            return value > maximum ? maximum : value;
        }
    }
}
