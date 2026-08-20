$ErrorActionPreference = 'Stop'

# 生产程序集基于.NET Framework 4.8，统一使用Windows PowerShell执行运行时压力。
if ($PSVersionTable.PSEdition -eq 'Core') {
    $windowsPowerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    & $windowsPowerShell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath
    exit $LASTEXITCODE
}

function Assert-True {
    param([bool]$Condition, [string]$Message)

    if (-not $Condition) {
        throw $Message
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$application = Get-Item -LiteralPath (Join-Path $debugDirectory '机器视觉AI检测系统V1.0.exe') -ErrorAction SilentlyContinue
Assert-True ($null -ne $application) 'CPU资源保护压力测试需要最新Debug程序。'

$productionSources = @(
    'ResourceManagement\CpuWorkScheduler.cs',
    'ResourceManagement\IResourceProfileServices.cs',
    'Process.cs',
    'Solution.cs'
)
foreach ($relativePath in $productionSources) {
    $source = Get-Item -LiteralPath (Join-Path $projectRoot $relativePath)
    Assert-True ($application.LastWriteTimeUtc -ge $source.LastWriteTimeUtc) "Debug程序早于$relativePath，请重新编译后再执行压力测试。"
}

[Environment]::CurrentDirectory = $debugDirectory
$originalPath = $env:PATH
$env:PATH = "$debugDirectory;$(Join-Path $debugDirectory 'dll\x64');$env:PATH"
$assemblyResolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    try {
        $requestedAssembly = New-Object Reflection.AssemblyName($eventArgs.Name)
    }
    catch {
        return $null
    }

    $dependencyPath = Join-Path $debugDirectory ($requestedAssembly.Name + '.dll')
    if (Test-Path -LiteralPath $dependencyPath) {
        return [Reflection.Assembly]::LoadFrom($dependencyPath)
    }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($assemblyResolver)

try {
    $applicationAssembly = [Reflection.Assembly]::LoadFrom($application.FullName)
    $schedulerType = $applicationAssembly.GetType('TDJS_Vision.ResourceManagement.CpuWorkScheduler', $false)
    Assert-True ($null -ne $schedulerType) 'Debug程序尚未包含CPU调度器。'

    $helperSource = @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.ResourceManagement;

/// <summary>单个动态并发阶段的四流程执行结果。</summary>
public sealed class CpuProtectionPhaseResult
{
    /// <summary>阶段目标并发额度。</summary>
    public int ExpectedConcurrency { get; set; }

    /// <summary>阶段实测活动峰值。</summary>
    public int PeakActiveCount { get; set; }

    /// <summary>阶段完成节点数。</summary>
    public int CompletedNodeCount { get; set; }

    /// <summary>四条流程是否均严格按自身节点顺序完成。</summary>
    public bool FlowOrderPreserved { get; set; }
}

/// <summary>不进入CPU许可池的线程池周期计时探针；仅衡量线程池调度响应，不代表真实UI、相机或PLC链路。</summary>
public sealed class ControlLaneProbe : IDisposable
{
    /// <summary>探针名称。</summary>
    private readonly string _name;

    /// <summary>周期触发器。</summary>
    private readonly Timer _timer;

    /// <summary>最近一次回调的高精度时间戳。</summary>
    private long _lastTimestamp;

    /// <summary>最大回调间隔的高精度计数。</summary>
    private long _maximumGapTicks;

    /// <summary>累计回调次数。</summary>
    private int _callbackCount;

    /// <summary>探针是否已停止。</summary>
    private int _disposed;

    /// <summary>创建并立即启动线程池周期计时探针。</summary>
    public ControlLaneProbe(string name, int intervalMilliseconds)
    {
        _name = name ?? string.Empty;
        _lastTimestamp = Stopwatch.GetTimestamp();
        _timer = new Timer(Tick, null, intervalMilliseconds, intervalMilliseconds);
    }

    /// <summary>探针名称。</summary>
    public string Name { get { return _name; } }

    /// <summary>累计回调次数。</summary>
    public int CallbackCount { get { return Volatile.Read(ref _callbackCount); } }

    /// <summary>最大回调间隔毫秒数。</summary>
    public double MaximumGapMilliseconds
    {
        get { return Interlocked.Read(ref _maximumGapTicks) * 1000D / Stopwatch.Frequency; }
    }

    /// <summary>停止周期探针。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;
        _timer.Dispose();
    }

    /// <summary>记录一次不受CPU许可限制的控制通道回调。</summary>
    private void Tick(object state)
    {
        if (Volatile.Read(ref _disposed) == 1)
            return;
        long now = Stopwatch.GetTimestamp();
        long previous = Interlocked.Exchange(ref _lastTimestamp, now);
        UpdateMaximum(ref _maximumGapTicks, Math.Max(0L, now - previous));
        Interlocked.Increment(ref _callbackCount);
    }

    /// <summary>以无锁方式更新最大值。</summary>
    private static void UpdateMaximum(ref long target, long value)
    {
        long current;
        do
        {
            current = Interlocked.Read(ref target);
            if (value <= current)
                return;
        }
        while (Interlocked.CompareExchange(ref target, value, current) != current);
    }
}

/// <summary>四流程动态CPU保护压力汇总。</summary>
public sealed class CpuResourceProtectionStressResult
{
    /// <summary>额度变化轨迹。</summary>
    public int[] ConcurrencyTrace { get; set; }

    /// <summary>各额度阶段结果。</summary>
    public CpuProtectionPhaseResult[] Phases { get; set; }

    /// <summary>降额时仍在运行的活动任务数。</summary>
    public int ActiveCountImmediatelyAfterDecrease { get; set; }

    /// <summary>最终调度器快照。</summary>
    public CpuWorkSchedulerSnapshot FinalSnapshot { get; set; }

    /// <summary>线程池计时探针A回调次数。</summary>
    public int UiCallbackCount { get; set; }

    /// <summary>线程池计时探针B回调次数。</summary>
    public int CameraCallbackCount { get; set; }

    /// <summary>线程池计时探针C回调次数。</summary>
    public int CommunicationCallbackCount { get; set; }

    /// <summary>线程池计时探针A最大回调间隔。</summary>
    public double UiMaximumGapMilliseconds { get; set; }

    /// <summary>线程池计时探针B最大回调间隔。</summary>
    public double CameraMaximumGapMilliseconds { get; set; }

    /// <summary>线程池计时探针C最大回调间隔。</summary>
    public double CommunicationMaximumGapMilliseconds { get; set; }

    /// <summary>完整压力墙钟毫秒数。</summary>
    public long WallMilliseconds { get; set; }

    /// <summary>强制回收后私有内存变化MB。</summary>
    public double PrivateMemoryDeltaMb { get; set; }

    /// <summary>强制回收后托管内存变化MB。</summary>
    public double ManagedMemoryDeltaMb { get; set; }

    /// <summary>强制回收后句柄变化。</summary>
    public int HandleDelta { get; set; }
}

/// <summary>使用生产CPU调度器执行四流程动态降额与恢复压力。</summary>
public static class CpuResourceProtectionStressHarness
{
    /// <summary>执行四流程、控制通道和资源回收压力。</summary>
    public static CpuResourceProtectionStressResult Run(int nodesPerFlowPerPhase)
    {
        if (nodesPerFlowPerPhase <= 0)
            throw new ArgumentOutOfRangeException("nodesPerFlowPerPhase");

        WarmUpRuntime();
        ForceCollection();
        Process process = Process.GetCurrentProcess();
        process.Refresh();
        long privateBefore = process.PrivateMemorySize64;
        long managedBefore = GC.GetTotalMemory(true);
        int handlesBefore = process.HandleCount;
        Stopwatch wall = Stopwatch.StartNew();

        CpuProtectionPhaseResult[] phases;
        int[] concurrencyTrace;
        int activeAfterDecrease;
        CpuWorkSchedulerSnapshot finalSnapshot;
        int uiCount;
        int cameraCount;
        int communicationCount;
        double uiGap;
        double cameraGap;
        double communicationGap;

        using (ControlLaneProbe uiProbe = new ControlLaneProbe("线程池计时探针A", 5))
        using (ControlLaneProbe cameraProbe = new ControlLaneProbe("线程池计时探针B", 5))
        using (ControlLaneProbe communicationProbe = new ControlLaneProbe("线程池计时探针C", 5))
        using (CpuWorkScheduler scheduler = new CpuWorkScheduler(CreateOptions()))
        {
            ICpuWorkLease[] activeLeases = Enumerable.Range(0, 4)
                .Select(index => scheduler.AcquireAsync(
                    CpuWorkloadKind.TraditionalVisionAlgorithm,
                    CancellationToken.None).GetAwaiter().GetResult())
                .ToArray();

            scheduler.ObserveCpuSample(90D, 0L);
            scheduler.ObserveCpuSample(90D, 5000L);
            activeAfterDecrease = scheduler.GetSnapshot().ActiveCount;
            foreach (ICpuWorkLease lease in activeLeases)
                lease.Dispose();

            CpuProtectionPhaseResult concurrencyThree = RunPhase(scheduler, 3, nodesPerFlowPerPhase);
            scheduler.ObserveCpuSample(90D, 35000L);
            CpuProtectionPhaseResult concurrencyTwo = RunPhase(scheduler, 2, nodesPerFlowPerPhase);
            scheduler.ObserveCpuSample(40D, 36000L);
            scheduler.ObserveCpuSample(40D, 66000L);
            CpuProtectionPhaseResult recoveredThree = RunPhase(scheduler, 3, nodesPerFlowPerPhase);
            scheduler.ObserveCpuSample(40D, 96000L);
            CpuProtectionPhaseResult recoveredFour = RunPhase(scheduler, 4, nodesPerFlowPerPhase);

            phases = new[] { concurrencyThree, concurrencyTwo, recoveredThree, recoveredFour };
            concurrencyTrace = new[] { 4, 3, 2, 3, 4 };
            finalSnapshot = scheduler.GetSnapshot();
            Thread.Sleep(50);
            uiCount = uiProbe.CallbackCount;
            cameraCount = cameraProbe.CallbackCount;
            communicationCount = communicationProbe.CallbackCount;
            uiGap = uiProbe.MaximumGapMilliseconds;
            cameraGap = cameraProbe.MaximumGapMilliseconds;
            communicationGap = communicationProbe.MaximumGapMilliseconds;
        }

        wall.Stop();
        ForceCollection();
        process.Refresh();
        return new CpuResourceProtectionStressResult
        {
            ConcurrencyTrace = concurrencyTrace,
            Phases = phases,
            ActiveCountImmediatelyAfterDecrease = activeAfterDecrease,
            FinalSnapshot = finalSnapshot,
            UiCallbackCount = uiCount,
            CameraCallbackCount = cameraCount,
            CommunicationCallbackCount = communicationCount,
            UiMaximumGapMilliseconds = uiGap,
            CameraMaximumGapMilliseconds = cameraGap,
            CommunicationMaximumGapMilliseconds = communicationGap,
            WallMilliseconds = wall.ElapsedMilliseconds,
            PrivateMemoryDeltaMb = (process.PrivateMemorySize64 - privateBefore) / 1024D / 1024D,
            ManagedMemoryDeltaMb = (GC.GetTotalMemory(true) - managedBefore) / 1024D / 1024D,
            HandleDelta = process.HandleCount - handlesBefore
        };
    }

    /// <summary>在指定动态额度下同步启动四条可编辑流程。</summary>
    private static CpuProtectionPhaseResult RunPhase(
        CpuWorkScheduler scheduler,
        int expectedConcurrency,
        int nodesPerFlow)
    {
        int activeCount = 0;
        int peakActiveCount = 0;
        int completedNodeCount = 0;
        List<int>[] flowOrders = Enumerable.Range(0, 4)
            .Select(index => new List<int>(nodesPerFlow))
            .ToArray();
        ManualResetEventSlim startGate = new ManualResetEventSlim(false);
        ManualResetEventSlim firstWaveRelease = new ManualResetEventSlim(false);
        CountdownEvent firstWaveReady = new CountdownEvent(expectedConcurrency);
        Task[] flows = Enumerable.Range(0, 4)
            .Select(flowIndex => Task.Run(async () =>
            {
                startGate.Wait();
                for (int nodeIndex = 0; nodeIndex < nodesPerFlow; nodeIndex++)
                {
                    CpuWorkloadKind kind = (CpuWorkloadKind)(nodeIndex % 3);
                    using (ICpuWorkLease lease = await scheduler.AcquireAsync(kind, CancellationToken.None))
                    {
                        int active = Interlocked.Increment(ref activeCount);
                        UpdateMaximum(ref peakActiveCount, active);
                        if (nodeIndex == 0 && firstWaveReady.CurrentCount > 0)
                        {
                            firstWaveReady.Signal();
                            firstWaveRelease.Wait();
                        }

                        flowOrders[flowIndex].Add(nodeIndex);
                        BusyCpu(TimeSpan.FromMilliseconds(1));
                        Interlocked.Increment(ref completedNodeCount);
                        Interlocked.Decrement(ref activeCount);
                    }
                }
            }))
            .ToArray();

        startGate.Set();
        if (!firstWaveReady.Wait(5000))
            throw new TimeoutException("四流程首批CPU节点未在5秒内达到目标并发。预期=" + expectedConcurrency);
        firstWaveRelease.Set();
        if (!Task.WaitAll(flows, 30000))
            throw new TimeoutException("四流程CPU节点未在30秒内完成。并发=" + expectedConcurrency);

        bool orderPreserved = flowOrders.All(order =>
            order.Count == nodesPerFlow &&
            order.Select((value, index) => value == index).All(value => value));
        firstWaveReady.Dispose();
        firstWaveRelease.Dispose();
        startGate.Dispose();
        return new CpuProtectionPhaseResult
        {
            ExpectedConcurrency = expectedConcurrency,
            PeakActiveCount = peakActiveCount,
            CompletedNodeCount = completedNodeCount,
            FlowOrderPreserved = orderPreserved
        };
    }

    /// <summary>创建与生产自动档案规则一致的四流程测试参数。</summary>
    private static CpuWorkSchedulerOptions CreateOptions()
    {
        return new CpuWorkSchedulerOptions
        {
            MaximumConcurrency = 4,
            MinimumConcurrency = 1,
            ImageConversionMaximumConcurrency = 4,
            HighCpuPercent = 85D,
            RecoveryCpuPercent = 65D,
            HighCpuSustainMilliseconds = 5000,
            RecoveryCpuSustainMilliseconds = 10000,
            AdjustmentIntervalMilliseconds = 5000,
            AdjustmentCooldownMilliseconds = 30000,
            AdjustmentStep = 1,
            WaitSampleCapacity = 2048
        };
    }

    /// <summary>执行短时确定性CPU工作，模拟传统视觉、转换和编码节点。</summary>
    private static void BusyCpu(TimeSpan duration)
    {
        long durationTicks = Math.Max(1L, (long)(duration.TotalSeconds * Stopwatch.Frequency));
        long started = Stopwatch.GetTimestamp();
        double value = 0D;
        while (Stopwatch.GetTimestamp() - started < durationTicks)
            value += Math.Sqrt(value + 1.2345D);
        GC.KeepAlive(value);
    }

    /// <summary>以无锁方式更新活动峰值。</summary>
    private static void UpdateMaximum(ref int target, int value)
    {
        int current;
        do
        {
            current = Volatile.Read(ref target);
            if (value <= current)
                return;
        }
        while (Interlocked.CompareExchange(ref target, value, current) != current);
    }

    /// <summary>预热任务、定时器和调度器运行时路径。</summary>
    private static void WarmUpRuntime()
    {
        using (CpuWorkScheduler scheduler = new CpuWorkScheduler(CreateOptions()))
        using (ICpuWorkLease lease = scheduler.AcquireAsync(
            CpuWorkloadKind.TraditionalVisionAlgorithm,
            CancellationToken.None).GetAwaiter().GetResult())
        {
            BusyCpu(TimeSpan.FromMilliseconds(1));
        }
        using (ControlLaneProbe probe = new ControlLaneProbe("预热", 5))
            Thread.Sleep(20);
    }

    /// <summary>执行完整垃圾回收，观察稳定资源基线。</summary>
    private static void ForceCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
'@

    $applicationReferencePath = Join-Path $debugDirectory 'TDJSVision.CpuProtectionTestHost.dll'
    Copy-Item -LiteralPath $application.FullName -Destination $applicationReferencePath -Force
    try {
        Add-Type -TypeDefinition $helperSource -Language CSharp -ReferencedAssemblies @(
            'mscorlib',
            'System',
            'System.Core',
            'System.Threading',
            $applicationReferencePath)
    }
    finally {
        Remove-Item -LiteralPath $applicationReferencePath -Force -ErrorAction SilentlyContinue
    }

    $result = [CpuResourceProtectionStressHarness]::Run(50)
    Assert-True (($result.ConcurrencyTrace -join ',') -eq '4,3,2,3,4') 'CPU动态额度必须按4→3→2→3→4变化。'
    Assert-True ($result.ActiveCountImmediatelyAfterDecrease -eq 4) '降额不得取消四个已经开始的CPU节点。'
    foreach ($phase in $result.Phases) {
        Assert-True ($phase.PeakActiveCount -eq $phase.ExpectedConcurrency) "并发$($phase.ExpectedConcurrency)阶段没有精确执行额度边界。"
        Assert-True ($phase.CompletedNodeCount -eq 200) "并发$($phase.ExpectedConcurrency)阶段四流程节点没有全部完成。"
        Assert-True $phase.FlowOrderPreserved "并发$($phase.ExpectedConcurrency)阶段改变了流程内部节点顺序。"
    }
    Assert-True ($result.FinalSnapshot.GrantedCount -eq 804 -and $result.FinalSnapshot.CompletedCount -eq 804) '全部CPU许可必须恰好取得并归还一次。'
    Assert-True ($result.FinalSnapshot.ActiveCount -eq 0 -and $result.FinalSnapshot.WaitingCount -eq 0) '压力结束后CPU活动和等待必须归零。'
    Assert-True ($result.FinalSnapshot.ConcurrencyDecreaseCount -eq 2 -and $result.FinalSnapshot.ConcurrencyIncreaseCount -eq 2) '动态降额与恢复计数必须各为2次。'
    Assert-True ($result.UiCallbackCount -ge 10 -and $result.UiMaximumGapMilliseconds -lt 250) '线程池计时探针A在CPU压力期间响应不足。'
    Assert-True ($result.CameraCallbackCount -ge 10 -and $result.CameraMaximumGapMilliseconds -lt 250) '线程池计时探针B在CPU压力期间响应不足。'
    Assert-True ($result.CommunicationCallbackCount -ge 10 -and $result.CommunicationMaximumGapMilliseconds -lt 250) '线程池计时探针C在CPU压力期间响应不足。'
    Assert-True ($result.PrivateMemoryDeltaMb -lt 32) '四流程CPU压力后私有内存增长不得达到32MB。'
    Assert-True ($result.ManagedMemoryDeltaMb -lt 8) '四流程CPU压力后托管内存增长不得达到8MB。'
    Assert-True ($result.HandleDelta -le 12) '四流程CPU压力后句柄增长不得超过12。'

    Write-Host ('四流程CPU保护压力通过：额度={0}；阶段峰值={1}；完成许可={2}/{3}；线程池计时探针A/B/C最大间隔={4:N3}/{5:N3}/{6:N3}ms；墙钟={7}ms；私有/托管内存变化={8:N3}/{9:N3}MB；句柄变化={10}；{11}；该探针指标不代表真实UI消息循环、相机回调或PLC通信。' -f `
        ($result.ConcurrencyTrace -join '→'),
        (($result.Phases | ForEach-Object { $_.PeakActiveCount }) -join '/'),
        $result.FinalSnapshot.CompletedCount,
        $result.FinalSnapshot.GrantedCount,
        $result.UiMaximumGapMilliseconds,
        $result.CameraMaximumGapMilliseconds,
        $result.CommunicationMaximumGapMilliseconds,
        $result.WallMilliseconds,
        $result.PrivateMemoryDeltaMb,
        $result.ManagedMemoryDeltaMb,
        $result.HandleDelta,
        $result.FinalSnapshot.ToLogText())
}
finally {
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($assemblyResolver)
    $env:PATH = $originalPath
}
