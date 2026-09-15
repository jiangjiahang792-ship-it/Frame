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

function Assert-Contains {
    param([string]$Text, [string]$Expected, [string]$Message)

    if (-not $Text.Contains($Expected)) {
        throw $Message
    }
}

function Assert-NotContains {
    param([string]$Text, [string]$Unexpected, [string]$Message)

    if ($Text.Contains($Unexpected)) {
        throw $Message
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$schedulerSource = Get-Content -LiteralPath (Join-Path $projectRoot 'ResourceManagement\CpuWorkScheduler.cs') -Raw -Encoding UTF8
$interfacesSource = Get-Content -LiteralPath (Join-Path $projectRoot 'ResourceManagement\IResourceProfileServices.cs') -Raw -Encoding UTF8
$projectSource = Get-Content -LiteralPath (Join-Path $projectRoot 'TDJS-Vision.csproj') -Raw -Encoding UTF8

Assert-Contains $interfacesSource 'interface ICpuWorkScheduler' 'CPU调度器必须通过可替换接口接入。'
Assert-Contains $schedulerSource 'TraditionalVisionAlgorithm = 0' '传统视觉算法必须使用明确资源类型。'
Assert-Contains $schedulerSource 'ImageConversion = 1' '图像转换必须使用明确资源类型。'
Assert-Contains $schedulerSource 'ImageEncoding = 2' '图片编码必须使用明确资源类型。'
Assert-NotContains $schedulerSource 'AiInference' 'CPU调度器不得定义AI推理限流类型。'
Assert-NotContains $schedulerSource 'ThreadPool.Set' 'CPU调度器不得修改全局线程池。'
Assert-NotContains $schedulerSource 'ProcessorAffinity =' 'CPU调度器不得修改进程亲和性。'
Assert-Contains $schedulerSource 'CurrentConcurrency - _options.AdjustmentStep' 'CPU高水位每次必须按配置步长下降。'
Assert-Contains $schedulerSource 'CurrentConcurrency + _options.AdjustmentStep' 'CPU恢复水位每次必须按配置步长恢复。'
Assert-Contains $schedulerSource 'TaskCreationOptions.RunContinuationsAsynchronously' '等待许可不得在调度器锁内同步执行用户延续代码。'
Assert-Contains $projectSource '<Compile Include="ResourceManagement\CpuWorkScheduler.cs" />' '项目必须编译CPU调度器。'

$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$application = Get-Item -LiteralPath (Join-Path $debugDirectory '机器视觉AI检测系统V1.0.exe') -ErrorAction SilentlyContinue
Assert-True ($null -ne $application) 'CPU调度器压力测试需要最新Debug程序。'

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
    Assert-True ($null -ne $schedulerType) 'Debug程序尚未包含CPU调度器，请先完成编译。'

    $helperSource = @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.ResourceManagement;

/// <summary>CPU调度器确定性压力结果。</summary>
public sealed class CpuWorkSchedulerStressResult
{
    /// <summary>压力任务总数。</summary>
    public int AttemptedCount { get; set; }
    /// <summary>压力任务完成数。</summary>
    public int CompletedCount { get; set; }
    /// <summary>压力观察到的全局并发峰值。</summary>
    public int ObservedPeakActiveCount { get; set; }
    /// <summary>压力观察到的图像转换并发峰值。</summary>
    public int ObservedPeakImageConversionCount { get; set; }
    /// <summary>队首图像转换受限时图片编码是否成功前进。</summary>
    public bool HeadOfLineBypassWorked { get; set; }
    /// <summary>取消等待是否成功。</summary>
    public bool CancellationWorked { get; set; }
    /// <summary>高水位第一次调整后的并发。</summary>
    public int FirstHighConcurrency { get; set; }
    /// <summary>冷却完成后第二次高水位调整的并发。</summary>
    public int SecondHighConcurrency { get; set; }
    /// <summary>恢复水位持续并冷却完成后的并发。</summary>
    public int RecoveredConcurrency { get; set; }
    /// <summary>许可排队P99毫秒。</summary>
    public double WaitP99Milliseconds { get; set; }
    /// <summary>许可排队最大毫秒。</summary>
    public double WaitMaximumMilliseconds { get; set; }
    /// <summary>私有内存变化MB。</summary>
    public double PrivateMemoryDeltaMb { get; set; }
    /// <summary>托管内存变化MB。</summary>
    public double ManagedMemoryDeltaMb { get; set; }
    /// <summary>句柄变化。</summary>
    public int HandleDelta { get; set; }
    /// <summary>最终调度器诊断。</summary>
    public string SnapshotText { get; set; }
}

/// <summary>CPU调度器确定性压力宿主。</summary>
public static class CpuWorkSchedulerStressHarness
{
    /// <summary>执行双层边界、取消、动态调节和资源回收压力。</summary>
    public static CpuWorkSchedulerStressResult Run(int attemptedCount)
    {
        WarmUpRuntime();
        ForceCollection();
        Process process = Process.GetCurrentProcess();
        process.Refresh();
        long privateStart = process.PrivateMemorySize64;
        long managedStart = GC.GetTotalMemory(false);
        int handleStart = process.HandleCount;

        bool headOfLineBypassWorked;
        bool cancellationWorked;
        using (CpuWorkScheduler scheduler = new CpuWorkScheduler(CreateOptions(4, 2)))
        {
            ICpuWorkLease imageOne = scheduler.AcquireAsync(CpuWorkloadKind.ImageConversion, CancellationToken.None).Result;
            ICpuWorkLease imageTwo = scheduler.AcquireAsync(CpuWorkloadKind.ImageConversion, CancellationToken.None).Result;
            ICpuWorkLease traditionalOne = scheduler.AcquireAsync(CpuWorkloadKind.TraditionalVisionAlgorithm, CancellationToken.None).Result;
            ICpuWorkLease traditionalTwo = scheduler.AcquireAsync(CpuWorkloadKind.TraditionalVisionAlgorithm, CancellationToken.None).Result;

            CancellationTokenSource cancellation = new CancellationTokenSource();
            Task<ICpuWorkLease> blockedImage = scheduler.AcquireAsync(CpuWorkloadKind.ImageConversion, cancellation.Token);
            Task<ICpuWorkLease> queuedEncoding = scheduler.AcquireAsync(CpuWorkloadKind.ImageEncoding, CancellationToken.None);
            cancellation.Cancel();
            cancellationWorked = WaitForCancellation(blockedImage);

            traditionalOne.Dispose();
            headOfLineBypassWorked = queuedEncoding.Wait(2000);
            ICpuWorkLease encodingLease = headOfLineBypassWorked ? queuedEncoding.Result : null;
            if (encodingLease != null)
                encodingLease.Dispose();
            traditionalTwo.Dispose();
            imageOne.Dispose();
            imageTwo.Dispose();
            cancellation.Dispose();
        }

        int firstHighConcurrency;
        int secondHighConcurrency;
        int recoveredConcurrency;
        using (CpuWorkScheduler dynamicScheduler = new CpuWorkScheduler(CreateOptions(4, 2)))
        {
            dynamicScheduler.ObserveCpuSample(90D, 0L);
            dynamicScheduler.ObserveCpuSample(90D, 1000L);
            firstHighConcurrency = dynamicScheduler.GetSnapshot().CurrentConcurrency;
            dynamicScheduler.ObserveCpuSample(90D, 6000L);
            secondHighConcurrency = dynamicScheduler.GetSnapshot().CurrentConcurrency;
            dynamicScheduler.ObserveCpuSample(40D, 7000L);
            dynamicScheduler.ObserveCpuSample(40D, 8000L);
            dynamicScheduler.ObserveCpuSample(40D, 11000L);
            recoveredConcurrency = dynamicScheduler.GetSnapshot().CurrentConcurrency;
        }

        int activeCount = 0;
        int activeImageCount = 0;
        int peakActiveCount = 0;
        int peakImageCount = 0;
        CpuWorkSchedulerSnapshot finalSnapshot;
        using (CpuWorkScheduler stressScheduler = new CpuWorkScheduler(CreateOptions(4, 2)))
        {
            Task[] tasks = Enumerable.Range(0, attemptedCount).Select(index => Task.Run(async () =>
            {
                CpuWorkloadKind kind = index % 3 == 0
                    ? CpuWorkloadKind.ImageConversion
                    : (index % 3 == 1 ? CpuWorkloadKind.TraditionalVisionAlgorithm : CpuWorkloadKind.ImageEncoding);
                using (ICpuWorkLease lease = await stressScheduler.AcquireAsync(kind, CancellationToken.None))
                {
                    int currentActive = Interlocked.Increment(ref activeCount);
                    UpdateMaximum(ref peakActiveCount, currentActive);
                    if (kind == CpuWorkloadKind.ImageConversion)
                    {
                        int currentImage = Interlocked.Increment(ref activeImageCount);
                        UpdateMaximum(ref peakImageCount, currentImage);
                        Thread.Sleep(2);
                        Interlocked.Decrement(ref activeImageCount);
                    }
                    else
                    {
                        Thread.Sleep(2);
                    }
                    Interlocked.Decrement(ref activeCount);
                }
            })).ToArray();
            Task.WaitAll(tasks);
            finalSnapshot = stressScheduler.GetSnapshot();
        }

        ForceCollection();
        process.Refresh();
        return new CpuWorkSchedulerStressResult
        {
            AttemptedCount = attemptedCount,
            CompletedCount = (int)finalSnapshot.CompletedCount,
            ObservedPeakActiveCount = peakActiveCount,
            ObservedPeakImageConversionCount = peakImageCount,
            HeadOfLineBypassWorked = headOfLineBypassWorked,
            CancellationWorked = cancellationWorked,
            FirstHighConcurrency = firstHighConcurrency,
            SecondHighConcurrency = secondHighConcurrency,
            RecoveredConcurrency = recoveredConcurrency,
            WaitP99Milliseconds = finalSnapshot.WaitP99Milliseconds,
            WaitMaximumMilliseconds = finalSnapshot.WaitMaximumMilliseconds,
            PrivateMemoryDeltaMb = (process.PrivateMemorySize64 - privateStart) / 1024D / 1024D,
            ManagedMemoryDeltaMb = (GC.GetTotalMemory(false) - managedStart) / 1024D / 1024D,
            HandleDelta = process.HandleCount - handleStart,
            SnapshotText = finalSnapshot.ToLogText()
        };
    }

    /// <summary>在资源基线前预热线程池、异步延续和调度器类型。</summary>
    private static void WarmUpRuntime()
    {
        using (CpuWorkScheduler scheduler = new CpuWorkScheduler(CreateOptions(4, 2)))
        {
            Task[] tasks = Enumerable.Range(0, 128).Select(index => Task.Run(async () =>
            {
                CpuWorkloadKind kind = index % 2 == 0
                    ? CpuWorkloadKind.ImageConversion
                    : CpuWorkloadKind.TraditionalVisionAlgorithm;
                using (ICpuWorkLease lease = await scheduler.AcquireAsync(kind, CancellationToken.None))
                    Thread.Sleep(1);
            })).ToArray();
            Task.WaitAll(tasks);
        }
    }

    /// <summary>创建测试用CPU调度参数。</summary>
    private static CpuWorkSchedulerOptions CreateOptions(int maximumConcurrency, int imageConversionConcurrency)
    {
        return new CpuWorkSchedulerOptions
        {
            MaximumConcurrency = maximumConcurrency,
            MinimumConcurrency = 1,
            ImageConversionMaximumConcurrency = imageConversionConcurrency,
            HighCpuPercent = 85D,
            RecoveryCpuPercent = 65D,
            HighCpuSustainMilliseconds = 1000,
            RecoveryCpuSustainMilliseconds = 1000,
            AdjustmentIntervalMilliseconds = 1000,
            AdjustmentCooldownMilliseconds = 5000,
            AdjustmentStep = 1,
            WaitSampleCapacity = 2048
        };
    }

    /// <summary>确认等待任务进入取消状态。</summary>
    private static bool WaitForCancellation(Task<ICpuWorkLease> task)
    {
        try
        {
            task.Wait(2000);
            return false;
        }
        catch (AggregateException ex)
        {
            return ex.Flatten().InnerExceptions.All(item => item is TaskCanceledException);
        }
    }

    /// <summary>无锁更新整数峰值。</summary>
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

    /// <summary>执行两代GC完整回收。</summary>
    private static void ForceCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
'@

    $applicationReferencePath = Join-Path $debugDirectory 'TDJSVision.CpuSchedulerTestHost.dll'
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

    $result = [CpuWorkSchedulerStressHarness]::Run(1000)
    Assert-True $result.HeadOfLineBypassWorked '图像转换分类受限时，后续图片编码任务必须能够前进。'
    Assert-True $result.CancellationWorked '等待CPU许可必须支持取消。'
    Assert-True ($result.ObservedPeakActiveCount -le 4) 'CPU重任务峰值不得突破全局并发4。'
    Assert-True ($result.ObservedPeakImageConversionCount -le 2) '图像转换峰值不得突破分类并发2。'
    Assert-True ($result.CompletedCount -eq 1000) '1000个压力任务必须全部完成并归还许可。'
    Assert-True ($result.FirstHighConcurrency -eq 3) 'CPU高水位持续后并发必须从4降到3。'
    Assert-True ($result.SecondHighConcurrency -eq 2) '冷却结束且CPU继续高位后并发必须从3降到2。'
    Assert-True ($result.RecoveredConcurrency -eq 3) 'CPU恢复水位持续并经过冷却后并发必须从2恢复到3。'
    Assert-True ($result.PrivateMemoryDeltaMb -lt 32) '1000任务后私有内存增长不得达到32MB。'
    Assert-True ($result.ManagedMemoryDeltaMb -lt 8) '1000任务后托管内存增长不得达到8MB。'
    Assert-True ($result.HandleDelta -le 8) '1000任务后句柄增长不得超过8。'

    Write-Host ('CPU调度器压力通过：任务={0}/{1}；全局峰值={2}/4；转换峰值={3}/2；高位调整={4}->{5}；恢复={6}；等待P99={7:N3}ms；最大={8:N3}ms；私有内存变化={9:N3}MB；托管内存变化={10:N3}MB；句柄变化={11}；{12}' -f `
        $result.CompletedCount,
        $result.AttemptedCount,
        $result.ObservedPeakActiveCount,
        $result.ObservedPeakImageConversionCount,
        $result.FirstHighConcurrency,
        $result.SecondHighConcurrency,
        $result.RecoveredConcurrency,
        $result.WaitP99Milliseconds,
        $result.WaitMaximumMilliseconds,
        $result.PrivateMemoryDeltaMb,
        $result.ManagedMemoryDeltaMb,
        $result.HandleDelta,
        $result.SnapshotText)
}
finally {
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($assemblyResolver)
    $env:PATH = $originalPath
}
