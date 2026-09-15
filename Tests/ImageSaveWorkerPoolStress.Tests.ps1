param(
    [ValidateRange(1, 100)]
    [int]$FormalRoundCount = 3,

    [ValidateRange(1, 10000)]
    [int]$AttemptsPerRound = 60,

    [string]$ResultPath = ''
)

$ErrorActionPreference = 'Stop'
# 生产程序基于.NET Framework 4.8，PowerShell 7会加载不兼容的System.Web类型转发。
if ($PSVersionTable.PSEdition -eq 'Core') {
    $windowsPowerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $relayArguments = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', $PSCommandPath,
        '-FormalRoundCount', $FormalRoundCount,
        '-AttemptsPerRound', $AttemptsPerRound
    )
    if (-not [string]::IsNullOrWhiteSpace($ResultPath)) {
        $relayArguments += @('-ResultPath', $ResultPath)
    }
    & $windowsPowerShell @relayArguments
    exit $LASTEXITCODE
}

Add-Type -AssemblyName System.Drawing

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Invoke-FullCollection {
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
    [GC]::Collect()
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$projectXml = [xml](Get-Content -LiteralPath (Join-Path $projectRoot 'TDJS-Vision.csproj') -Raw -Encoding UTF8)
$assemblyName = [string](
    $projectXml.Project.PropertyGroup |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.AssemblyName) } |
        Select-Object -First 1
).AssemblyName
$application = Get-Item -LiteralPath (Join-Path $debugDirectory ($assemblyName + '.exe')) -ErrorAction SilentlyContinue
Assert-True ($null -ne $application) '图像保存工作池压力测试需要最新Debug程序。'
$productionSources = @(
    'ResourceManagement\BoundedPriorityWorkQueue.cs',
    'ResourceManagement\ImageSaveStorageGuard.cs',
    'Node\1-Acquisition\ImageSource\NodeResultImageSource.cs',
    'Node\7-ResultProcessing\ImageSave\NodeSaveImage.cs',
    'Solution.cs'
) | ForEach-Object { Get-Item -LiteralPath (Join-Path $projectRoot $_) }
$latestProductionSource = $productionSources | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
Assert-True ($application.LastWriteTimeUtc -ge $latestProductionSource.LastWriteTimeUtc) 'Debug程序早于本轮保存队列源码，请先重新编译后再执行压力测试。'

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
[Reflection.Assembly]::LoadFrom($application.FullName) | Out-Null

$helperSource = @'
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._7_ResultProcessing.ImageSave;
using TDJS_Vision.ResourceManagement;
using SaveImageTask = TDJS_Vision.Node._7_ResultProcessing.ImageSave.ImageQueueProcessor.SaveImageTask;

public sealed class StressMatLease : IImageResourceLease
{
    private Mat _image;
    private int _disposed;

    public static int DisposeCount;
    public static int DoubleDisposeCount;

    public StressMatLease(Mat image)
    {
        _image = image;
    }

    public OutputImage Image { get { return null; } }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            Interlocked.Increment(ref DoubleDisposeCount);
            return;
        }

        Mat image = Interlocked.Exchange(ref _image, null);
        if (image != null)
            image.Dispose();
        Interlocked.Increment(ref DisposeCount);
    }

    public static void Reset()
    {
        DisposeCount = 0;
        DoubleDisposeCount = 0;
    }
}

public sealed class ImageSaveWorkerStressResult
{
    public int AttemptedCount { get; set; }
    public int AcceptedCount { get; set; }
    public int RejectedCount { get; set; }
    public int EvictedNormalCount { get; set; }
    public long CompletedCount { get; set; }
    public long FailedCount { get; set; }
    public long CanceledCount { get; set; }
    public int FileCount { get; set; }
    public int InvalidJpegCount { get; set; }
    public long TotalFileBytes { get; set; }
    public int EmptyFileCount { get; set; }
    public int TemporaryFileCount { get; set; }
    public int LeaseDisposeCount { get; set; }
    public int LeaseDoubleDisposeCount { get; set; }
    public int LeaseFactoryCallCount { get; set; }
    public int ActiveLeaseCountAfterAdmission { get; set; }
    public int RejectedOutputActiveLeaseCount { get; set; }
    public int FinalActiveImageLeaseCount { get; set; }
    public bool DisposedSnapshotRejected { get; set; }
    public bool StaleSnapshotBudgetRejected { get; set; }
    public int PeakQueueCount { get; set; }
    public long PeakQueueBytes { get; set; }
    public int PeakActiveWorkers { get; set; }
    public long EnqueueElapsedMilliseconds { get; set; }
    public double EnqueueP95Milliseconds { get; set; }
    public double EnqueueP99Milliseconds { get; set; }
    public double EnqueueMaxMilliseconds { get; set; }
    public long StopElapsedMilliseconds { get; set; }
    public bool GracefullyStopped { get; set; }
    public int FinalQueuedCount { get; set; }
    public bool FullyStopped { get; set; }
    public long SynchronousRetiredReleaseCount { get; set; }
    public int PeakRetiredTaskCount { get; set; }
    public long PeakRetiredTaskBytes { get; set; }
    public long LowSpaceSkippedNormalCount { get; set; }
    public long CriticalSpaceSkippedCount { get; set; }
    public long LowSpaceAllowedHighPriorityCount { get; set; }
    public long StorageProbeFailureCount { get; set; }
    public long SlowWriteCount { get; set; }
    public long MaximumWriteElapsedMilliseconds { get; set; }
    public long CpuGrantedCount { get; set; }
    public long CpuCompletedCount { get; set; }
    public long CpuCanceledWaitCount { get; set; }
    public int CpuPeakActiveCount { get; set; }
    public int CpuPeakImageConversionCount { get; set; }
    public int CpuFinalActiveCount { get; set; }
    public int CpuFinalWaitingCount { get; set; }
    public int CpuActiveCountDuringStoragePrepare { get; set; }
    public int CpuImageConversionAcquireCount { get; set; }
    public int CpuImageEncodingAcquireCount { get; set; }
    public bool MultiTargetBytesIdentical { get; set; }
}

public sealed class DeterministicStorageGuard : IImageSaveStorageGuard
{
    public ImageSaveStorageStatus Status { get; set; }
    public int WriteDelayMilliseconds { get; set; }
    public int LowSpaceThresholdMb { get; set; }
    public int CriticalSpaceThresholdMb { get; set; }
    public int SlowWriteThresholdMs { get; set; }
    public int ProbeIntervalMs { get; set; }
    public Func<int> CpuActiveCountProvider { get; set; }
    public int MaximumCpuActiveCountDuringPrepareWrite { get; private set; }

    public DeterministicStorageGuard(ImageSaveStorageStatus status, int writeDelayMilliseconds, int slowWriteThresholdMs)
    {
        Status = status;
        WriteDelayMilliseconds = Math.Max(0, writeDelayMilliseconds);
        LowSpaceThresholdMb = 2048;
        CriticalSpaceThresholdMb = 512;
        SlowWriteThresholdMs = Math.Max(1, slowWriteThresholdMs);
        ProbeIntervalMs = 1000;
    }

    public ImageSaveStorageDecision Evaluate(IReadOnlyList<string> targetDirectories, bool isHighPriority)
    {
        return new ImageSaveStorageDecision
        {
            Status = Status,
            StorageRoot = "测试磁盘",
            AvailableSpaceMb = Status == ImageSaveStorageStatus.SkippedCriticalSpace ? 128 : 1024,
            Reason = "确定性保存磁盘压力测试"
        };
    }

    public void PrepareWrite(string targetDirectory)
    {
        if (CpuActiveCountProvider != null)
            MaximumCpuActiveCountDuringPrepareWrite = Math.Max(
                MaximumCpuActiveCountDuringPrepareWrite,
                CpuActiveCountProvider());
        if (WriteDelayMilliseconds > 0)
            Thread.Sleep(WriteDelayMilliseconds);
    }

    public bool IsSlowWrite(long elapsedMilliseconds)
    {
        return elapsedMilliseconds >= SlowWriteThresholdMs;
    }
}

public sealed class TrackingCpuWorkScheduler : ICpuWorkScheduler
{
    private int _activeCount;
    private int _disposed;
    private long _grantedCount;
    private long _completedCount;

    public int ActiveCount { get { return Volatile.Read(ref _activeCount); } }
    public int ImageConversionAcquireCount { get; private set; }
    public int ImageEncodingAcquireCount { get; private set; }

    public Task<ICpuWorkLease> AcquireAsync(CpuWorkloadKind workloadKind, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Volatile.Read(ref _disposed) == 1)
            throw new ObjectDisposedException("TrackingCpuWorkScheduler");
        Interlocked.Increment(ref _activeCount);
        Interlocked.Increment(ref _grantedCount);
        if (workloadKind == CpuWorkloadKind.ImageConversion)
            ImageConversionAcquireCount++;
        if (workloadKind == CpuWorkloadKind.ImageEncoding)
            ImageEncodingAcquireCount++;
        return Task.FromResult<ICpuWorkLease>(new TrackingCpuWorkLease(this, workloadKind));
    }

    public void ObserveCpuSample(double cpuPercent, long timestampMilliseconds)
    {
    }

    public void Reconfigure(CpuWorkSchedulerOptions options)
    {
    }

    public CpuWorkSchedulerSnapshot GetSnapshot()
    {
        return new CpuWorkSchedulerSnapshot
        {
            ActiveCount = ActiveCount,
            GrantedCount = Interlocked.Read(ref _grantedCount),
            CompletedCount = Interlocked.Read(ref _completedCount)
        };
    }

    public void Dispose()
    {
        Volatile.Write(ref _disposed, 1);
    }

    private void Release()
    {
        Interlocked.Decrement(ref _activeCount);
        Interlocked.Increment(ref _completedCount);
    }

    private sealed class TrackingCpuWorkLease : ICpuWorkLease
    {
        private TrackingCpuWorkScheduler _owner;
        public CpuWorkloadKind WorkloadKind { get; private set; }
        public double WaitElapsedMilliseconds { get { return 0D; } }

        public TrackingCpuWorkLease(TrackingCpuWorkScheduler owner, CpuWorkloadKind workloadKind)
        {
            _owner = owner;
            WorkloadKind = workloadKind;
        }

        public void Dispose()
        {
            TrackingCpuWorkScheduler owner = Interlocked.Exchange(ref _owner, null);
            if (owner != null)
                owner.Release();
        }
    }
}

public sealed class ThrowingStressMatLease : IImageResourceLease
{
    private Mat _image;
    private int _disposed;

    public static int DisposeAttemptCount;

    public ThrowingStressMatLease(Mat image)
    {
        _image = image;
    }

    public OutputImage Image { get { return null; } }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        Mat image = Interlocked.Exchange(ref _image, null);
        if (image != null)
            image.Dispose();
        Interlocked.Increment(ref DisposeAttemptCount);
        throw new InvalidOperationException("模拟租约释放异常");
    }
}

public static class ImageSaveWorkerStressHarness
{
    public static ImageSaveWorkerStressResult RunAdmissionLeaseFactoryScenario(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        ImageQueueProcessor processor = new ImageQueueProcessor(
            1,
            1,
            16L * 1024L * 1024L,
            1000,
            "接纳后租约测试");
        int leaseFactoryCallCount = 0;
        Mat firstImage = new Mat(128, 128, MatType.CV_8UC3, Scalar.All(63));
        OutputImage firstOutput = OutputImage.FromOwnedSingleImage(firstImage);
        SaveImageTask firstTask = new SaveImageTask
        {
            SourceImage = null,
            TargetDirectories = new List<string> { outputDirectory },
            ImageName = "接纳任务",
            NeedCompress = true,
            CompressValue = 90,
            EstimatedBytes = firstOutput.GetRetainedImageBytesEstimateForAdmission(),
            OwnerKey = "两阶段接纳流程"
        };
        BoundedQueueAdmissionResult firstAdmission = processor.EnqueueImage(
            firstTask,
            () =>
            {
                Interlocked.Increment(ref leaseFactoryCallCount);
                return firstOutput.AcquireSaveSnapshot();
            });
        if (!firstAdmission.Accepted)
            firstTask.Dispose();

        Mat secondImage = new Mat(128, 128, MatType.CV_8UC3, Scalar.All(127));
        OutputImage secondOutput = OutputImage.FromOwnedSingleImage(secondImage);
        SaveImageTask secondTask = new SaveImageTask
        {
            SourceImage = null,
            TargetDirectories = new List<string> { outputDirectory },
            ImageName = "拒绝任务",
            NeedCompress = true,
            CompressValue = 90,
            EstimatedBytes = secondOutput.GetRetainedImageBytesEstimateForAdmission(),
            OwnerKey = "两阶段接纳流程"
        };
        BoundedQueueAdmissionResult secondAdmission = processor.EnqueueImage(
            secondTask,
            () =>
            {
                Interlocked.Increment(ref leaseFactoryCallCount);
                return secondOutput.AcquireSaveSnapshot();
            });
        if (!secondAdmission.Accepted)
            secondTask.Dispose();

        int activeLeaseCountAfterAdmission = firstOutput.ActiveLeaseCount;
        int rejectedOutputActiveLeaseCount = secondOutput.ActiveLeaseCount;
        bool stopped = processor.StopProcessing();
        int finalActiveImageLeaseCount = firstOutput.ActiveLeaseCount + secondOutput.ActiveLeaseCount;
        firstOutput.Dispose();
        secondOutput.Dispose();

        ImageQueueProcessor disposedProcessor = new ImageQueueProcessor(
            1,
            1,
            16L * 1024L * 1024L,
            1000,
            "释放竞态接纳测试");
        Mat disposedImage = new Mat(128, 128, MatType.CV_8UC3, Scalar.All(191));
        OutputImage disposedOutput = OutputImage.FromOwnedSingleImage(disposedImage);
        SaveImageTask disposedTask = new SaveImageTask
        {
            SourceImage = null,
            TargetDirectories = new List<string> { outputDirectory },
            ImageName = "释放后任务",
            NeedCompress = true,
            CompressValue = 90,
            EstimatedBytes = disposedOutput.GetRetainedImageBytesEstimateForAdmission(),
            OwnerKey = "两阶段接纳流程"
        };
        disposedOutput.Dispose();
        bool disposedSnapshotRejected = false;
        try
        {
            disposedProcessor.EnqueueImage(
                disposedTask,
                () => disposedOutput.AcquireSaveSnapshot());
        }
        catch (ObjectDisposedException)
        {
            disposedSnapshotRejected = true;
        }
        finally
        {
            disposedTask.Dispose();
            disposedProcessor.StopProcessing();
        }

        ImageQueueProcessor staleBudgetProcessor = new ImageQueueProcessor(
            1,
            1,
            1L * 1024L * 1024L,
            1000,
            "缓存失真预算测试");
        Mat staleOriginalImage = new Mat(16, 16, MatType.CV_8UC3, Scalar.All(31));
        OutputImage staleOutput = OutputImage.FromOwnedSingleImage(staleOriginalImage);
        SaveImageTask staleTask = new SaveImageTask
        {
            SourceImage = null,
            TargetDirectories = new List<string> { outputDirectory },
            ImageName = "缓存失真任务",
            NeedCompress = true,
            CompressValue = 90,
            EstimatedBytes = staleOutput.GetRetainedImageBytesEstimateForAdmission(),
            OwnerKey = "两阶段接纳流程"
        };
        Mat staleReplacementImage = new Mat(1024, 1024, MatType.CV_8UC3, Scalar.All(223));
        staleOutput.Bitmaps[0] = staleReplacementImage;
        bool staleSnapshotBudgetRejected = false;
        try
        {
            BoundedQueueAdmissionResult staleAdmission = staleBudgetProcessor.EnqueueImage(
                staleTask,
                () => staleOutput.AcquireSaveSnapshot());
            staleTask.Dispose();
            staleSnapshotBudgetRejected = !staleAdmission.Accepted &&
                staleAdmission.Status == BoundedQueueAdmissionStatus.RejectedItemExceedsBudget &&
                staleBudgetProcessor.RejectedTaskCount == 1 &&
                staleBudgetProcessor.QueuedCount == 0 &&
                staleOutput.ActiveLeaseCount == 0;
        }
        finally
        {
            staleTask.Dispose();
            staleBudgetProcessor.StopProcessing();
            staleOutput.Dispose();
            staleReplacementImage.Dispose();
        }

        return new ImageSaveWorkerStressResult
        {
            AttemptedCount = 2,
            AcceptedCount = (firstAdmission.Accepted ? 1 : 0) + (secondAdmission.Accepted ? 1 : 0),
            RejectedCount = (firstAdmission.Accepted ? 0 : 1) + (secondAdmission.Accepted ? 0 : 1),
            LeaseFactoryCallCount = leaseFactoryCallCount,
            ActiveLeaseCountAfterAdmission = activeLeaseCountAfterAdmission,
            RejectedOutputActiveLeaseCount = rejectedOutputActiveLeaseCount,
            FinalActiveImageLeaseCount = finalActiveImageLeaseCount,
            DisposedSnapshotRejected = disposedSnapshotRejected && disposedProcessor.QueuedCount == 0,
            StaleSnapshotBudgetRejected = staleSnapshotBudgetRejected,
            GracefullyStopped = stopped,
            FullyStopped = processor.IsFullyStopped,
            FinalQueuedCount = processor.QueuedCount
        };
    }

    public static ImageSaveWorkerStressResult RunCpuPermitStopCancellationScenario(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        StressMatLease.Reset();
        CpuWorkScheduler cpuScheduler = new CpuWorkScheduler(CreateCpuSchedulerOptions(1, 1));
        ICpuWorkLease blockingLease = cpuScheduler.AcquireAsync(
            CpuWorkloadKind.TraditionalVisionAlgorithm,
            CancellationToken.None).GetAwaiter().GetResult();
        ImageQueueProcessor processor = new ImageQueueProcessor(
            1,
            4,
            16L * 1024L * 1024L,
            1000,
            "CPU许可等待停止取消测试",
            CachedImageSaveStorageGuard.CreateDefault(),
            cpuScheduler);
        processor.StartProcessing();
        Mat image = new Mat(128, 128, MatType.CV_8UC3, Scalar.All(127));
        SaveImageTask task = new SaveImageTask
        {
            SourceImage = image,
            TargetDirectories = new List<string> { outputDirectory },
            ImageName = "CPU许可等待停止取消",
            NeedCompress = true,
            CompressValue = 90,
            EstimatedBytes = checked((long)image.Rows * (long)image.Step()),
            OwnerKey = "CPU许可等待停止取消流程",
            ImageLease = new StressMatLease(image)
        };
        BoundedQueueAdmissionResult admission = processor.EnqueueImage(task);
        if (!admission.Accepted)
            task.Dispose();
        bool reachedCpuWait = SpinWait.SpinUntil(
            () => processor.ActiveWorkerCount == 1 && cpuScheduler.GetSnapshot().WaitingCount == 1,
            2000);
        Stopwatch stopStopwatch = Stopwatch.StartNew();
        bool stopped = processor.StopProcessing();
        stopStopwatch.Stop();
        CpuWorkSchedulerSnapshot beforeBlockerRelease = cpuScheduler.GetSnapshot();
        blockingLease.Dispose();
        CpuWorkSchedulerSnapshot finalCpuSnapshot = cpuScheduler.GetSnapshot();
        cpuScheduler.Dispose();
        FileInfo[] files = new DirectoryInfo(outputDirectory).GetFiles("*.jpg", SearchOption.AllDirectories);
        return new ImageSaveWorkerStressResult
        {
            AttemptedCount = 1,
            AcceptedCount = admission.Accepted ? 1 : 0,
            CompletedCount = processor.CompletedTaskCount,
            FailedCount = processor.FailedTaskCount,
            CanceledCount = processor.CanceledTaskCount,
            FileCount = files.Length,
            LeaseDisposeCount = StressMatLease.DisposeCount,
            LeaseDoubleDisposeCount = StressMatLease.DoubleDisposeCount,
            StopElapsedMilliseconds = stopStopwatch.ElapsedMilliseconds,
            GracefullyStopped = stopped,
            FullyStopped = processor.IsFullyStopped,
            FinalQueuedCount = processor.QueuedCount,
            CpuGrantedCount = finalCpuSnapshot.GrantedCount,
            CpuCompletedCount = finalCpuSnapshot.CompletedCount,
            CpuCanceledWaitCount = finalCpuSnapshot.CanceledWaitCount,
            CpuFinalActiveCount = finalCpuSnapshot.ActiveCount,
            CpuFinalWaitingCount = beforeBlockerRelease.WaitingCount,
            MultiTargetBytesIdentical = reachedCpuWait
        };
    }

    public static ImageSaveWorkerStressResult RunCpuStorageSeparationScenario(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        string firstTargetDirectory = Path.Combine(outputDirectory, "目录一");
        string secondTargetDirectory = Path.Combine(outputDirectory, "目录二");
        Directory.CreateDirectory(firstTargetDirectory);
        Directory.CreateDirectory(secondTargetDirectory);
        StressMatLease.Reset();
        TrackingCpuWorkScheduler cpuScheduler = new TrackingCpuWorkScheduler();
        DeterministicStorageGuard storageGuard = new DeterministicStorageGuard(
            ImageSaveStorageStatus.Allowed,
            100,
            50);
        storageGuard.CpuActiveCountProvider = () => cpuScheduler.ActiveCount;
        ImageQueueProcessor processor = new ImageQueueProcessor(
            1,
            4,
            16L * 1024L * 1024L,
            5000,
            "CPU与慢盘分段测试",
            storageGuard,
            cpuScheduler);
        processor.StartProcessing();
        Mat image = new Mat(128, 128, MatType.CV_8UC3, Scalar.All(127));
        SaveImageTask task = new SaveImageTask
        {
            SourceImage = image,
            TargetDirectories = new List<string> { firstTargetDirectory, secondTargetDirectory },
            ImageName = "CPU与慢盘分段",
            NeedCompress = true,
            CompressValue = 90,
            EstimatedBytes = checked((long)image.Rows * (long)image.Step()),
            OwnerKey = "CPU与慢盘分段流程",
            ImageLease = new StressMatLease(image)
        };
        BoundedQueueAdmissionResult admission = processor.EnqueueImage(task);
        if (!admission.Accepted)
            task.Dispose();
        bool stopped = processor.StopProcessing();
        CpuWorkSchedulerSnapshot cpuSnapshot = cpuScheduler.GetSnapshot();
        cpuScheduler.Dispose();
        FileInfo[] files = new DirectoryInfo(outputDirectory).GetFiles("*.jpg", SearchOption.AllDirectories);
        bool multiTargetBytesIdentical = files.Length == 2 &&
            File.ReadAllBytes(files[0].FullName).SequenceEqual(File.ReadAllBytes(files[1].FullName));
        return new ImageSaveWorkerStressResult
        {
            AttemptedCount = 1,
            AcceptedCount = admission.Accepted ? 1 : 0,
            CompletedCount = processor.CompletedTaskCount,
            FailedCount = processor.FailedTaskCount,
            FileCount = files.Length,
            InvalidJpegCount = files.Count(file => !IsValidJpeg(file.FullName, 128, 128)),
            LeaseDisposeCount = StressMatLease.DisposeCount,
            GracefullyStopped = stopped,
            FullyStopped = processor.IsFullyStopped,
            CpuGrantedCount = cpuSnapshot.GrantedCount,
            CpuCompletedCount = cpuSnapshot.CompletedCount,
            CpuFinalActiveCount = cpuSnapshot.ActiveCount,
            CpuActiveCountDuringStoragePrepare = storageGuard.MaximumCpuActiveCountDuringPrepareWrite,
            CpuImageConversionAcquireCount = cpuScheduler.ImageConversionAcquireCount,
            CpuImageEncodingAcquireCount = cpuScheduler.ImageEncodingAcquireCount,
            MultiTargetBytesIdentical = multiTargetBytesIdentical
        };
    }

    public static ImageSaveWorkerStressResult RunStorageScenario(
        string outputDirectory,
        ImageSaveStorageStatus status,
        bool isHighPriority,
        int writeDelayMilliseconds,
        int slowWriteThresholdMs,
        int attemptedCount)
    {
        Directory.CreateDirectory(outputDirectory);
        StressMatLease.Reset();
        DeterministicStorageGuard storageGuard = new DeterministicStorageGuard(
            status,
            writeDelayMilliseconds,
            slowWriteThresholdMs);
        ImageQueueProcessor processor = new ImageQueueProcessor(
            1,
            4,
            16L * 1024L * 1024L,
            5000,
            "保存磁盘保护测试",
            storageGuard);
        processor.StartProcessing();
        int accepted = 0;
        int rejected = 0;
        int evicted = 0;
        for (int index = 0; index < attemptedCount; index++)
        {
            Mat image = new Mat(128, 128, MatType.CV_8UC3, Scalar.All(index % 251));
            SaveImageTask task = new SaveImageTask
            {
                SourceImage = image,
                TargetDirectories = new List<string> { outputDirectory },
                ImageName = "磁盘保护",
                NeedCompress = true,
                CompressValue = 90,
                IsHighPriority = isHighPriority,
                EstimatedBytes = checked((long)image.Rows * (long)image.Step()),
                OwnerKey = "磁盘保护流程",
                ImageLease = new StressMatLease(image)
            };
            BoundedQueueAdmissionResult admission = processor.EnqueueImage(task);
            if (admission.Accepted)
            {
                accepted++;
                evicted += admission.EvictedLowPriorityCount;
            }
            else
            {
                rejected++;
                task.Dispose();
            }
        }

        bool stopped = processor.StopProcessing();
        ImageSaveQueueDiagnosticsSnapshot snapshot = processor.GetDiagnosticsSnapshot();
        FileInfo[] files = new DirectoryInfo(outputDirectory).GetFiles("*.jpg", SearchOption.TopDirectoryOnly);
        return new ImageSaveWorkerStressResult
        {
            AttemptedCount = attemptedCount,
            AcceptedCount = accepted,
            RejectedCount = rejected,
            EvictedNormalCount = evicted,
            CompletedCount = processor.CompletedTaskCount,
            FailedCount = processor.FailedTaskCount,
            FileCount = files.Length,
            InvalidJpegCount = files.Count(file => !IsValidJpeg(file.FullName, 128, 128)),
            TotalFileBytes = files.Sum(file => file.Length),
            LeaseDisposeCount = StressMatLease.DisposeCount,
            LeaseDoubleDisposeCount = StressMatLease.DoubleDisposeCount,
            PeakQueueCount = processor.PeakQueuedCount,
            PeakQueueBytes = processor.PeakQueuedBytes,
            PeakActiveWorkers = processor.PeakActiveWorkerCount,
            GracefullyStopped = stopped,
            FinalQueuedCount = processor.QueuedCount,
            FullyStopped = processor.IsFullyStopped,
            LowSpaceSkippedNormalCount = snapshot.LowSpaceSkippedNormalTaskCount,
            CriticalSpaceSkippedCount = snapshot.CriticalSpaceSkippedTaskCount,
            LowSpaceAllowedHighPriorityCount = snapshot.LowSpaceAllowedHighPriorityTaskCount,
            StorageProbeFailureCount = snapshot.StorageProbeFailureCount,
            SlowWriteCount = snapshot.SlowWriteCount,
            MaximumWriteElapsedMilliseconds = snapshot.MaximumWriteElapsedMilliseconds
        };
    }

    public static ImageSaveWorkerStressResult Run(string outputDirectory, int attemptedCount)
    {
        Directory.CreateDirectory(outputDirectory);
        StressMatLease.Reset();
        CpuWorkScheduler cpuScheduler = new CpuWorkScheduler(CreateCpuSchedulerOptions());
        ImageQueueProcessor processor = new ImageQueueProcessor(
            2,
            8,
            128L * 1024L * 1024L,
            5000,
            "保存压力测试",
            CachedImageSaveStorageGuard.CreateDefault(),
            cpuScheduler);
        processor.StartProcessing();
        int accepted = 0;
        int rejected = 0;
        int evicted = 0;
        int nextIndex = -1;
        ConcurrentBag<long> enqueueLatencyTicks = new ConcurrentBag<long>();
        Stopwatch enqueueStopwatch = Stopwatch.StartNew();
        Task[] producers = new Task[4];
        for (int producerIndex = 0; producerIndex < producers.Length; producerIndex++)
        {
            int producerId = producerIndex;
            producers[producerIndex] = Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    int index = Interlocked.Increment(ref nextIndex);
                    if (index >= attemptedCount)
                        return;

                    Mat image = new Mat(768, 1024, MatType.CV_8UC3, Scalar.All(index % 251));
                    StressMatLease lease = new StressMatLease(image);
                    SaveImageTask task = new SaveImageTask
                    {
                        SourceImage = image,
                        TargetDirectories = new List<string> { outputDirectory },
                        ImageName = "并发同名",
                        NeedCompress = true,
                        CompressValue = 90,
                        IsHighPriority = index % 10 == 0,
                        EstimatedBytes = checked((long)image.Rows * (long)image.Step()),
                        OwnerKey = "流程" + producerId,
                        ImageLease = lease
                    };

                    long enqueueStarted = Stopwatch.GetTimestamp();
                    BoundedQueueAdmissionResult result = processor.EnqueueImage(task);
                    enqueueLatencyTicks.Add(Stopwatch.GetTimestamp() - enqueueStarted);
                    if (result.Accepted)
                    {
                        Interlocked.Increment(ref accepted);
                        Interlocked.Add(ref evicted, result.EvictedLowPriorityCount);
                    }
                    else
                    {
                        Interlocked.Increment(ref rejected);
                        task.Dispose();
                    }
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
        Task.WaitAll(producers);
        enqueueStopwatch.Stop();

        Stopwatch stopStopwatch = Stopwatch.StartNew();
        bool gracefullyStopped = processor.StopProcessing();
        stopStopwatch.Stop();
        if (!gracefullyStopped)
        {
            Stopwatch tailWait = Stopwatch.StartNew();
            while (processor.ActiveWorkerCount > 0 && tailWait.ElapsedMilliseconds < 30000)
                Thread.Sleep(10);
            if (processor.ActiveWorkerCount > 0)
                Environment.FailFast("保存压力工作任务超过30秒仍未退出。", null);
        }

        FileInfo[] files = new DirectoryInfo(outputDirectory).GetFiles("*.jpg", SearchOption.TopDirectoryOnly);
        int temporaryFileCount = new DirectoryInfo(outputDirectory).GetFiles("*.saving", SearchOption.TopDirectoryOnly).Length;
        double[] enqueueLatencies = enqueueLatencyTicks
            .Select(ticks => ticks * 1000.0 / Stopwatch.Frequency)
            .OrderBy(value => value)
            .ToArray();
        CpuWorkSchedulerSnapshot cpuSnapshot = cpuScheduler.GetSnapshot();
        cpuScheduler.Dispose();
        return new ImageSaveWorkerStressResult
        {
            AttemptedCount = attemptedCount,
            AcceptedCount = accepted,
            RejectedCount = rejected,
            EvictedNormalCount = evicted,
            CompletedCount = processor.CompletedTaskCount,
            FailedCount = processor.FailedTaskCount,
            FileCount = files.Length,
            InvalidJpegCount = files.Count(file => !IsValidJpeg(file.FullName, 1024, 768)),
            TotalFileBytes = files.Sum(file => file.Length),
            EmptyFileCount = files.Count(file => file.Length == 0),
            TemporaryFileCount = temporaryFileCount,
            LeaseDisposeCount = StressMatLease.DisposeCount,
            LeaseDoubleDisposeCount = StressMatLease.DoubleDisposeCount,
            PeakQueueCount = processor.PeakQueuedCount,
            PeakQueueBytes = processor.PeakQueuedBytes,
            PeakActiveWorkers = processor.PeakActiveWorkerCount,
            EnqueueElapsedMilliseconds = enqueueStopwatch.ElapsedMilliseconds,
            EnqueueP95Milliseconds = GetPercentile(enqueueLatencies, 0.95),
            EnqueueP99Milliseconds = GetPercentile(enqueueLatencies, 0.99),
            EnqueueMaxMilliseconds = enqueueLatencies.Length == 0 ? 0.0 : enqueueLatencies[enqueueLatencies.Length - 1],
            StopElapsedMilliseconds = stopStopwatch.ElapsedMilliseconds,
            GracefullyStopped = gracefullyStopped,
            FinalQueuedCount = processor.QueuedCount,
            FullyStopped = processor.IsFullyStopped,
            SynchronousRetiredReleaseCount = processor.SynchronousRetiredReleaseCount,
            PeakRetiredTaskCount = processor.PeakRetiredTaskCount,
            PeakRetiredTaskBytes = processor.PeakRetiredTaskBytes,
            CpuGrantedCount = cpuSnapshot.GrantedCount,
            CpuCompletedCount = cpuSnapshot.CompletedCount,
            CpuPeakActiveCount = cpuSnapshot.PeakActiveCount,
            CpuPeakImageConversionCount = cpuSnapshot.PeakImageConversionCount,
            CpuFinalActiveCount = cpuSnapshot.ActiveCount,
            CpuFinalWaitingCount = cpuSnapshot.WaitingCount
        };
    }

    private static CpuWorkSchedulerOptions CreateCpuSchedulerOptions()
    {
        return CreateCpuSchedulerOptions(4, 4);
    }

    private static CpuWorkSchedulerOptions CreateCpuSchedulerOptions(
        int maximumConcurrency,
        int imageConversionMaximumConcurrency)
    {
        return new CpuWorkSchedulerOptions
        {
            MaximumConcurrency = maximumConcurrency,
            MinimumConcurrency = 1,
            ImageConversionMaximumConcurrency = imageConversionMaximumConcurrency,
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

    private static double GetPercentile(double[] sortedValues, double percentile)
    {
        if (sortedValues == null || sortedValues.Length == 0)
            return 0.0;

        int index = (int)Math.Ceiling(sortedValues.Length * percentile) - 1;
        index = Math.Max(0, Math.Min(sortedValues.Length - 1, index));
        return sortedValues[index];
    }

    private static bool IsValidJpeg(string filePath, int expectedWidth, int expectedHeight)
    {
        try
        {
            using (System.Drawing.Image image = System.Drawing.Image.FromFile(filePath))
                return image.Width == expectedWidth && image.Height == expectedHeight;
        }
        catch
        {
            return false;
        }
    }

    public static ImageSaveWorkerStressResult RunWriteFailure(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        StressMatLease.Reset();
        ImageQueueProcessor processor = new ImageQueueProcessor(1, 4, 64L * 1024L * 1024L, 5000, "写盘失败测试");
        processor.StartProcessing();
        Mat image = new Mat(128, 128, MatType.CV_8UC3, Scalar.All(127));
        SaveImageTask task = new SaveImageTask
        {
            SourceImage = image,
            TargetDirectories = new List<string> { outputDirectory, outputDirectory + "\0非法目录" },
            ImageName = "失败图片",
            NeedCompress = true,
            CompressValue = 90,
            EstimatedBytes = checked((long)image.Rows * (long)image.Step()),
            OwnerKey = "失败流程",
            ImageLease = new StressMatLease(image)
        };
        BoundedQueueAdmissionResult admission = processor.EnqueueImage(task);
        if (!admission.Accepted)
            task.Dispose();
        bool stopped = processor.StopProcessing();
        FileInfo[] files = new DirectoryInfo(outputDirectory).GetFiles("*.jpg", SearchOption.TopDirectoryOnly);
        int temporaryFileCount = new DirectoryInfo(outputDirectory).GetFiles("*.saving", SearchOption.TopDirectoryOnly).Length;
        return new ImageSaveWorkerStressResult
        {
            AttemptedCount = 1,
            AcceptedCount = admission.Accepted ? 1 : 0,
            RejectedCount = admission.Accepted ? 0 : 1,
            CompletedCount = processor.CompletedTaskCount,
            FailedCount = processor.FailedTaskCount,
            FileCount = files.Length,
            TemporaryFileCount = temporaryFileCount,
            LeaseDisposeCount = StressMatLease.DisposeCount,
            LeaseDoubleDisposeCount = StressMatLease.DoubleDisposeCount,
            GracefullyStopped = stopped,
            FinalQueuedCount = processor.QueuedCount,
            FullyStopped = processor.IsFullyStopped
        };
    }

    public static ImageSaveWorkerStressResult RunCleanupFailure(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        StressMatLease.Reset();
        ThrowingStressMatLease.DisposeAttemptCount = 0;
        ImageQueueProcessor processor = new ImageQueueProcessor(1, 4, 64L * 1024L * 1024L, 5000, "清理异常测试");
        processor.StartProcessing();

        Mat firstImage = new Mat(128, 128, MatType.CV_8UC3, Scalar.All(63));
        SaveImageTask firstTask = new SaveImageTask
        {
            SourceImage = firstImage,
            TargetDirectories = new List<string> { outputDirectory },
            ImageName = "清理异常前",
            NeedCompress = true,
            CompressValue = 90,
            EstimatedBytes = checked((long)firstImage.Rows * (long)firstImage.Step()),
            OwnerKey = "清理异常流程",
            ImageLease = new ThrowingStressMatLease(firstImage)
        };
        Mat secondImage = new Mat(128, 128, MatType.CV_8UC3, Scalar.All(127));
        SaveImageTask secondTask = new SaveImageTask
        {
            SourceImage = secondImage,
            TargetDirectories = new List<string> { outputDirectory },
            ImageName = "清理异常后",
            NeedCompress = true,
            CompressValue = 90,
            EstimatedBytes = checked((long)secondImage.Rows * (long)secondImage.Step()),
            OwnerKey = "清理异常流程",
            ImageLease = new StressMatLease(secondImage)
        };

        BoundedQueueAdmissionResult firstAdmission = processor.EnqueueImage(firstTask);
        if (!firstAdmission.Accepted)
            firstTask.Dispose();
        BoundedQueueAdmissionResult secondAdmission = processor.EnqueueImage(secondTask);
        if (!secondAdmission.Accepted)
            secondTask.Dispose();
        bool stopped = processor.StopProcessing();
        FileInfo[] files = new DirectoryInfo(outputDirectory).GetFiles("*.jpg", SearchOption.TopDirectoryOnly);
        return new ImageSaveWorkerStressResult
        {
            AttemptedCount = 2,
            AcceptedCount = (firstAdmission.Accepted ? 1 : 0) + (secondAdmission.Accepted ? 1 : 0),
            RejectedCount = (firstAdmission.Accepted ? 0 : 1) + (secondAdmission.Accepted ? 0 : 1),
            CompletedCount = processor.CompletedTaskCount,
            FailedCount = processor.FailedTaskCount,
            FileCount = files.Length,
            LeaseDisposeCount = StressMatLease.DisposeCount,
            LeaseDoubleDisposeCount = StressMatLease.DoubleDisposeCount,
            GracefullyStopped = stopped,
            FullyStopped = processor.IsFullyStopped,
            FinalQueuedCount = processor.QueuedCount
        };
    }

    public static ImageSaveWorkerStressResult RunZeroTimeout(string outputDirectory, int attemptedCount)
    {
        Directory.CreateDirectory(outputDirectory);
        StressMatLease.Reset();
        ImageQueueProcessor processor = new ImageQueueProcessor(
            1,
            8,
            128L * 1024L * 1024L,
            0,
            "零等待停止测试");
        processor.StartProcessing();
        int accepted = 0;
        int rejected = 0;
        for (int index = 0; index < attemptedCount; index++)
        {
            Mat image = new Mat(2048, 2048, MatType.CV_8UC3, Scalar.All(index % 251));
            SaveImageTask task = new SaveImageTask
            {
                SourceImage = image,
                TargetDirectories = new List<string> { outputDirectory },
                ImageName = "停止测试",
                NeedCompress = true,
                CompressValue = 95,
                EstimatedBytes = checked((long)image.Rows * (long)image.Step()),
                OwnerKey = "停止流程",
                ImageLease = new StressMatLease(image)
            };
            BoundedQueueAdmissionResult admission = processor.EnqueueImage(task);
            if (admission.Accepted)
                accepted++;
            else
            {
                rejected++;
                task.Dispose();
            }
        }

        bool stopped = processor.StopProcessing();
        Stopwatch releaseWait = Stopwatch.StartNew();
        while (StressMatLease.DisposeCount < attemptedCount && releaseWait.ElapsedMilliseconds < 30000)
            Thread.Sleep(10);
        if (StressMatLease.DisposeCount < attemptedCount)
            Environment.FailFast("停止超时场景仍有Mat租约没有释放。", null);
        bool fullyStopped = processor.StopProcessing();

        return new ImageSaveWorkerStressResult
        {
            AttemptedCount = attemptedCount,
            AcceptedCount = accepted,
            RejectedCount = rejected,
            CompletedCount = processor.CompletedTaskCount,
            FailedCount = processor.FailedTaskCount,
            LeaseDisposeCount = StressMatLease.DisposeCount,
            LeaseDoubleDisposeCount = StressMatLease.DoubleDisposeCount,
            PeakQueueCount = processor.PeakQueuedCount,
            PeakQueueBytes = processor.PeakQueuedBytes,
            PeakActiveWorkers = processor.PeakActiveWorkerCount,
            GracefullyStopped = stopped,
            FinalQueuedCount = processor.QueuedCount,
            FullyStopped = fullyStopped && processor.IsFullyStopped
        };
    }
}
'@

$applicationReferencePath = Join-Path $debugDirectory 'TDJSVision.TestHost.dll'
Copy-Item -LiteralPath $application.FullName -Destination $applicationReferencePath -Force
$references = @(
    'mscorlib',
    'System',
    'System.Drawing',
    'System.Core',
    'System.Threading',
    'System.Threading.Thread',
    'System.Collections',
    'System.Runtime',
    'System.IO.FileSystem',
    'System.Linq',
    $applicationReferencePath,
    (Join-Path $debugDirectory 'OpenCvSharp.dll'),
    (Join-Path $debugDirectory 'OpenCvSharp.Extensions.dll')
)
try {
    Add-Type -TypeDefinition $helperSource -Language CSharp -ReferencedAssemblies $references
}
finally {
    Remove-Item -LiteralPath $applicationReferencePath -Force -ErrorAction SilentlyContinue
}

$testOutputRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot ('Tests\_temp_image_save_worker_stress_' + [Guid]::NewGuid().ToString('N'))))
$expectedParent = [IO.Path]::GetFullPath((Join-Path $projectRoot 'Tests'))
Assert-True ($testOutputRoot.StartsWith($expectedParent, [StringComparison]::OrdinalIgnoreCase)) '压力测试输出目录必须位于项目Tests目录内。'
[IO.Directory]::CreateDirectory($testOutputRoot) | Out-Null

try {
    $admissionLeaseDirectory = Join-Path $testOutputRoot 'admission_lease_factory'
    $admissionLease = [ImageSaveWorkerStressHarness]::RunAdmissionLeaseFactoryScenario($admissionLeaseDirectory)
    Assert-True ($admissionLease.AcceptedCount -eq 1 -and $admissionLease.RejectedCount -eq 1) '容量1且消费者未启动时必须恰好接收1个、拒绝1个任务。'
    Assert-True ($admissionLease.LeaseFactoryCallCount -eq $admissionLease.AcceptedCount) 'Mat租约工厂调用次数必须严格等于队列接收次数。'
    Assert-True ($admissionLease.ActiveLeaseCountAfterAdmission -eq 1 -and $admissionLease.RejectedOutputActiveLeaseCount -eq 0) '只有已接收任务可以持有OutputImage租约，满队列拒绝不得取得租约。'
    Assert-True ($admissionLease.FinalActiveImageLeaseCount -eq 0) '队列停止后已接收任务的OutputImage租约必须归零。'
    Assert-True $admissionLease.DisposedSnapshotRejected '任务预判后若OutputImage并发释放，原子快照必须拒绝发布且队列保持为空。'
    Assert-True $admissionLease.StaleSnapshotBudgetRejected 'Bitmaps原地变化导致缓存偏小时，接纳快照必须按实际资源拒绝发布且释放租约。'
    Assert-True ($admissionLease.GracefullyStopped -and $admissionLease.FullyStopped -and $admissionLease.FinalQueuedCount -eq 0) '两阶段接纳测试结束后队列和基础资源必须完整释放。'

    # 首轮作为完整Mat转Bitmap、JPEG编码、原子同名写盘和租约释放预热，不计入正式数据。
    $warmupDirectory = Join-Path $testOutputRoot 'warmup'
    $warmup = [ImageSaveWorkerStressHarness]::Run($warmupDirectory, 12)
    Assert-True ($warmup.FailedCount -eq 0) '保存工作池预热不允许写盘失败。'

    $lowNormalDirectory = Join-Path $testOutputRoot 'low_normal'
    $lowNormal = [ImageSaveWorkerStressHarness]::RunStorageScenario(
        $lowNormalDirectory,
        [TDJS_Vision.ResourceManagement.ImageSaveStorageStatus]::SkippedLowSpace,
        $false,
        0,
        500,
        1)
    Assert-True ($lowNormal.AcceptedCount -eq 1 -and $lowNormal.LowSpaceSkippedNormalCount -eq 1) '低空间普通图必须被队列接收后明确降级跳过。'
    Assert-True ($lowNormal.CompletedCount -eq 0 -and $lowNormal.FailedCount -eq 0 -and $lowNormal.FileCount -eq 0) '低空间跳过普通图不能误记成功、失败或产生文件。'
    Assert-True ($lowNormal.LeaseDisposeCount -eq 1 -and $lowNormal.LeaseDoubleDisposeCount -eq 0) '低空间跳过普通图后Mat租约必须恰好释放一次。'

    $lowNgDirectory = Join-Path $testOutputRoot 'low_ng'
    $lowNg = [ImageSaveWorkerStressHarness]::RunStorageScenario(
        $lowNgDirectory,
        [TDJS_Vision.ResourceManagement.ImageSaveStorageStatus]::AllowedHighPriorityUnderLowSpace,
        $true,
        0,
        500,
        1)
    Assert-True ($lowNg.LowSpaceAllowedHighPriorityCount -eq 1 -and $lowNg.CompletedCount -eq 1 -and $lowNg.FileCount -eq 1) '低空间下NG图必须继续完成保存并留下诊断计数。'
    Assert-True ($lowNg.LeaseDisposeCount -eq 1 -and $lowNg.LeaseDoubleDisposeCount -eq 0) '低空间NG图保存后Mat租约必须恰好释放一次。'

    $criticalDirectory = Join-Path $testOutputRoot 'critical'
    $critical = [ImageSaveWorkerStressHarness]::RunStorageScenario(
        $criticalDirectory,
        [TDJS_Vision.ResourceManagement.ImageSaveStorageStatus]::SkippedCriticalSpace,
        $true,
        0,
        500,
        1)
    Assert-True ($critical.CriticalSpaceSkippedCount -eq 1 -and $critical.CompletedCount -eq 0 -and $critical.FileCount -eq 0) '严重低空间必须连NG图也停止保存。'
    Assert-True ($critical.LeaseDisposeCount -eq 1 -and $critical.LeaseDoubleDisposeCount -eq 0) '严重低空间跳过后Mat租约必须恰好释放一次。'

    $probeFailureDirectory = Join-Path $testOutputRoot 'probe_failure'
    $probeFailure = [ImageSaveWorkerStressHarness]::RunStorageScenario(
        $probeFailureDirectory,
        [TDJS_Vision.ResourceManagement.ImageSaveStorageStatus]::ProbeFailedAllowed,
        $false,
        0,
        500,
        1)
    Assert-True ($probeFailure.StorageProbeFailureCount -eq 1 -and $probeFailure.CompletedCount -eq 1 -and $probeFailure.FileCount -eq 1) '空间探测失败必须告警但不能误停产。'

    $slowDirectory = Join-Path $testOutputRoot 'slow_disk'
    $slow = [ImageSaveWorkerStressHarness]::RunStorageScenario(
        $slowDirectory,
        [TDJS_Vision.ResourceManagement.ImageSaveStorageStatus]::Allowed,
        $false,
        100,
        50,
        20)
    Assert-True ($slow.PeakQueueCount -le 4 -and $slow.PeakActiveWorkers -le 1) '慢盘压力不得突破队列容量4或保存并发1。'
    Assert-True ($slow.RejectedCount -gt 0) '固定100ms慢盘和容量4下必须出现明确拒绝，证明投递线程没有无界堆积。'
    Assert-True ($slow.SlowWriteCount -eq $slow.CompletedCount -and $slow.MaximumWriteElapsedMilliseconds -ge 100) '每个完成的100ms慢写都必须被阈值诊断捕获。'
    Assert-True (($slow.CompletedCount + $slow.EvictedNormalCount) -eq $slow.AcceptedCount) '慢盘场景全部接收任务必须有明确最终去向。'
    Assert-True ($slow.LeaseDisposeCount -eq 20 -and $slow.LeaseDoubleDisposeCount -eq 0) '慢盘压力中全部尝试任务的Mat租约必须恰好释放一次。'
    Assert-True ($slow.FailedCount -eq 0 -and $slow.FullyStopped) '慢盘降速不能形成写盘失败或遗留工作线程。'

    $cpuSeparationDirectory = Join-Path $testOutputRoot 'cpu_storage_separation'
    $cpuSeparation = [ImageSaveWorkerStressHarness]::RunCpuStorageSeparationScenario($cpuSeparationDirectory)
    Assert-True ($cpuSeparation.CompletedCount -eq 1 -and $cpuSeparation.FailedCount -eq 0 -and $cpuSeparation.FileCount -eq 2 -and $cpuSeparation.InvalidJpegCount -eq 0) 'CPU与慢盘分段场景必须把同一张有效JPEG写入两个目录。'
    Assert-True ($cpuSeparation.CpuImageConversionAcquireCount -eq 1 -and $cpuSeparation.CpuImageEncodingAcquireCount -eq 1) '单张保存必须分别取得一次转换许可和一次编码许可。'
    Assert-True $cpuSeparation.MultiTargetBytesIdentical '同一张图片写入两个目录时必须复用完全一致的JPEG编码字节。'
    Assert-True ($cpuSeparation.CpuGrantedCount -eq 2 -and $cpuSeparation.CpuCompletedCount -eq 2) '单张保存的两个CPU许可必须全部归还。'
    Assert-True ($cpuSeparation.CpuActiveCountDuringStoragePrepare -eq 0) '100ms慢盘准备期间不得持有CPU重任务许可。'
    Assert-True ($cpuSeparation.CpuFinalActiveCount -eq 0 -and $cpuSeparation.LeaseDisposeCount -eq 1 -and $cpuSeparation.FullyStopped) 'CPU与慢盘分段场景停止后CPU活动和Mat租约必须归零。'

    $cpuStopDirectory = Join-Path $testOutputRoot 'cpu_permit_stop_cancellation'
    $cpuStop = [ImageSaveWorkerStressHarness]::RunCpuPermitStopCancellationScenario($cpuStopDirectory)
    Assert-True $cpuStop.MultiTargetBytesIdentical '保存任务必须已经出队并真实等待被占满的CPU许可。'
    Assert-True ($cpuStop.AcceptedCount -eq 1 -and $cpuStop.CompletedCount -eq 0 -and $cpuStop.FailedCount -eq 0 -and $cpuStop.CanceledCount -eq 1) '等待CPU许可的保存任务停止后必须形成一次明确取消，不能误报完成或失败。'
    Assert-True ($cpuStop.GracefullyStopped -and $cpuStop.FullyStopped -and $cpuStop.StopElapsedMilliseconds -le 1100) 'CPU许可被占满时保存工作池必须在1000ms停止预算附近完整退出。'
    Assert-True ($cpuStop.LeaseDisposeCount -eq 1 -and $cpuStop.LeaseDoubleDisposeCount -eq 0 -and $cpuStop.FinalQueuedCount -eq 0) '停止取消后已出队Mat租约必须恰好释放一次且队列归零。'
    Assert-True ($cpuStop.FileCount -eq 0) '取得CPU许可前被停止的任务不得产生图片文件。'
    Assert-True ($cpuStop.CpuGrantedCount -eq 1 -and $cpuStop.CpuCompletedCount -eq 1 -and $cpuStop.CpuCanceledWaitCount -eq 1) '外部占位许可必须正常归还，保存CPU等待必须只取消一次。'
    Assert-True ($cpuStop.CpuFinalActiveCount -eq 0 -and $cpuStop.CpuFinalWaitingCount -eq 0) '停止回归结束后CPU活动和等待必须归零。'
    Write-Host ("CPU许可饱和停止回归：停止={0}ms；完成/失败/取消={1}/{2}/{3}；CPU许可/归还/等待取消={4}/{5}/{6}；Mat释放={7}；全部线程退出={8}" -f `
        $cpuStop.StopElapsedMilliseconds,
        $cpuStop.CompletedCount,
        $cpuStop.FailedCount,
        $cpuStop.CanceledCount,
        $cpuStop.CpuGrantedCount,
        $cpuStop.CpuCompletedCount,
        $cpuStop.CpuCanceledWaitCount,
        $cpuStop.LeaseDisposeCount,
        $cpuStop.FullyStopped)

    $failureDirectory = Join-Path $testOutputRoot 'failure'
    $failure = [ImageSaveWorkerStressHarness]::RunWriteFailure($failureDirectory)
    Assert-True $failure.GracefullyStopped '写盘失败任务也必须在5000ms内结束工作池。'
    Assert-True ($failure.AcceptedCount -eq 1) '写盘失败场景的任务必须先被工作池接收。'
    Assert-True ($failure.CompletedCount -eq 0 -and $failure.FailedCount -eq 1) '非法目录必须形成一个明确失败，不能误报保存成功。'
    Assert-True ($failure.FileCount -eq 0) '多目录保存中途失败时必须回滚前面已经发布的图片。'
    Assert-True ($failure.TemporaryFileCount -eq 0) '写盘失败后不允许残留.saving临时文件。'
    Assert-True ($failure.LeaseDisposeCount -eq 1 -and $failure.LeaseDoubleDisposeCount -eq 0) '写盘失败后Mat租约必须恰好释放一次。'
    Assert-True ($failure.FinalQueuedCount -eq 0) '写盘失败后队列必须归零。'

    $cleanupFailureDirectory = Join-Path $testOutputRoot 'cleanup_failure'
    $cleanupFailure = [ImageSaveWorkerStressHarness]::RunCleanupFailure($cleanupFailureDirectory)
    Assert-True $cleanupFailure.GracefullyStopped '租约释放异常后保存工作池仍必须正常退出。'
    Assert-True $cleanupFailure.FullyStopped '租约释放异常后保存消费者和回收线程都必须退出。'
    Assert-True ($cleanupFailure.AcceptedCount -eq 2) '清理异常测试的两个任务都必须被接收。'
    Assert-True ($cleanupFailure.CompletedCount -eq 1 -and $cleanupFailure.FailedCount -eq 1) '首个任务清理失败后，消费者必须继续完成第二个任务。'
    Assert-True ($cleanupFailure.FileCount -eq 2) '清理异常不得终止消费者，前后两个文件都应完成原子写盘。'
    Assert-True ($cleanupFailure.LeaseDisposeCount -eq 1 -and $cleanupFailure.LeaseDoubleDisposeCount -eq 0) '清理异常后的正常任务租约必须恰好释放一次。'

    $timeoutDirectory = Join-Path $testOutputRoot 'timeout'
    $timeout = [ImageSaveWorkerStressHarness]::RunZeroTimeout($timeoutDirectory, 12)
    Assert-True (-not $timeout.GracefullyStopped) '零毫秒停止等待必须进入超时排空分支。'
    Assert-True ($timeout.FinalQueuedCount -eq 0) '停止超时后等待队列必须立即归零。'
    Assert-True ($timeout.LeaseDisposeCount -eq 12 -and $timeout.LeaseDoubleDisposeCount -eq 0) '停止超时后全部Mat租约仍必须恰好释放一次。'
    Assert-True ($timeout.PeakQueueCount -le 8 -and $timeout.PeakActiveWorkers -le 1) '停止超时场景不得突破容量8或并发1。'
    Assert-True $timeout.FullyStopped '首次停止超时后再次停止必须能够等待后台任务完全收敛。'

    Invoke-FullCollection
    $process = [Diagnostics.Process]::GetCurrentProcess()
    $baselinePrivateMb = $process.PrivateMemorySize64 / 1MB
    $baselineManagedMb = [GC]::GetTotalMemory($false) / 1MB
    $baselineHandleCount = $process.HandleCount
    $cpuStart = $process.TotalProcessorTime
    $stressStopwatch = [Diagnostics.Stopwatch]::StartNew()
    $roundResults = @()
    $roundResourceSnapshots = @()

    for ($round = 1; $round -le $FormalRoundCount; $round++) {
        $roundDirectory = Join-Path $testOutputRoot ("round_{0}" -f $round)
        $result = [ImageSaveWorkerStressHarness]::Run($roundDirectory, $AttemptsPerRound)
        Assert-True $result.GracefullyStopped "第${round}轮必须在5000ms内排空并停止。"
        Assert-True ($result.PeakQueueCount -le 8) "第${round}轮队列峰值超过容量8。"
        Assert-True ($result.PeakQueueBytes -le (128MB)) "第${round}轮队列字节峰值超过128MB。"
        Assert-True ($result.PeakActiveWorkers -le 2) "第${round}轮实际保存并发超过2。"
        Assert-True ($result.PeakRetiredTaskCount -le 8) "第${round}轮淘汰回收通道任务峰值超过8。"
        Assert-True ($result.PeakRetiredTaskBytes -le (128MB)) "第${round}轮淘汰回收通道字节峰值超过128MB。"
        Assert-True ($result.SynchronousRetiredReleaseCount -eq 0) "第${round}轮淘汰回收通道不应退化为投递线程同步释放。"
        Assert-True (($result.AcceptedCount + $result.RejectedCount) -eq $result.AttemptedCount) "第${round}轮存在没有明确入队结果的任务。"
        Assert-True (($result.CompletedCount + $result.EvictedNormalCount) -eq $result.AcceptedCount) "第${round}轮接收任务去向不完整。"
        Assert-True ($result.FailedCount -eq 0) "第${round}轮不允许图片转换或写盘失败。"
        Assert-True ($result.FileCount -eq $result.CompletedCount) "第${round}轮完成任务数与唯一文件数不一致。"
        Assert-True ($result.EmptyFileCount -eq 0) "第${round}轮不允许产生空文件。"
        Assert-True ($result.InvalidJpegCount -eq 0) "第${round}轮全部JPEG必须可解码且尺寸为1024x768。"
        Assert-True ($result.TemporaryFileCount -eq 0) "第${round}轮不允许残留.saving临时文件。"
        Assert-True ($result.LeaseDisposeCount -eq $result.AttemptedCount) "第${round}轮每份Mat租约必须恰好释放一次。"
        Assert-True ($result.CpuGrantedCount -eq ($result.CompletedCount * 2)) "第${round}轮每张完成图片必须各取得一次转换许可和一次编码许可。"
        Assert-True ($result.CpuCompletedCount -eq $result.CpuGrantedCount) "第${round}轮CPU许可必须全部释放。"
        Assert-True ($result.CpuPeakActiveCount -le 2 -and $result.CpuPeakImageConversionCount -le 2) "第${round}轮保存CPU活动不得超过两个保存线程。"
        Assert-True ($result.CpuFinalActiveCount -eq 0 -and $result.CpuFinalWaitingCount -eq 0) "第${round}轮停止后CPU活动和等待必须归零。"
        Assert-True ($result.LeaseDoubleDisposeCount -eq 0) "第${round}轮不允许重复释放租约。"
        Assert-True ($result.EnqueueP99Milliseconds -lt 50) "第${round}轮入队P99超过50ms。"
        Assert-True ($result.EnqueueMaxMilliseconds -lt 500) "第${round}轮存在超过500ms的入队停顿。"
        $roundResults += $result

        Invoke-FullCollection
        $process.Refresh()
        $roundResourceSnapshots += [pscustomobject]@{
            Round = $round
            PrivateMemoryMb = [Math]::Round(($process.PrivateMemorySize64 / 1MB), 3)
            ManagedMemoryMb = [Math]::Round(([GC]::GetTotalMemory($false) / 1MB), 3)
            HandleCount = $process.HandleCount
        }
    }

    $stressStopwatch.Stop()
    Invoke-FullCollection
    $process.Refresh()
    $finalPrivateMb = $process.PrivateMemorySize64 / 1MB
    $finalManagedMb = [GC]::GetTotalMemory($false) / 1MB
    $finalHandleCount = $process.HandleCount
    Assert-True (($finalPrivateMb - $baselinePrivateMb) -lt 64) "${FormalRoundCount}轮正式压力后私有内存增长不得达到64MB。"
    Assert-True (($finalManagedMb - $baselineManagedMb) -lt 16) "${FormalRoundCount}轮正式压力后托管内存增长不得达到16MB。"
    Assert-True (($finalHandleCount - $baselineHandleCount) -le 16) "${FormalRoundCount}轮正式压力后进程句柄增长不得超过16个。"
    foreach ($result in $roundResults) {
        Write-Host ("保存工作池正式轮：四流程并发尝试={0}；接收={1}；拒绝={2}；淘汰普通={3}；完成={4}；文件={5}；队列峰值={6}/8；字节峰值={7:N2}MB/128MB；并发峰值={8}/2；投递阶段={9}ms；入队P95={10:N3}ms；P99={11:N3}ms；最大={12:N3}ms；停止排空={13}ms；失败={14}；租约释放={15}；重复释放={16}；同步淘汰释放={17}" -f `
            $result.AttemptedCount,
            $result.AcceptedCount,
            $result.RejectedCount,
            $result.EvictedNormalCount,
            $result.CompletedCount,
            $result.FileCount,
            $result.PeakQueueCount,
            ($result.PeakQueueBytes / 1MB),
            $result.PeakActiveWorkers,
            $result.EnqueueElapsedMilliseconds,
            $result.EnqueueP95Milliseconds,
            $result.EnqueueP99Milliseconds,
            $result.EnqueueMaxMilliseconds,
            $result.StopElapsedMilliseconds,
            $result.FailedCount,
            $result.LeaseDisposeCount,
            $result.LeaseDoubleDisposeCount,
            $result.SynchronousRetiredReleaseCount)
    }
    Write-Host ("保存工作池资源结果：私有内存基线={0:N2}MB；结束={1:N2}MB；变化={2:N2}MB；托管内存基线={3:N2}MB；结束={4:N2}MB；变化={5:N2}MB；句柄基线={6}；结束={7}；变化={8}" -f `
        $baselinePrivateMb,
        $finalPrivateMb,
        ($finalPrivateMb - $baselinePrivateMb),
        $baselineManagedMb,
        $finalManagedMb,
        ($finalManagedMb - $baselineManagedMb),
        $baselineHandleCount,
        $finalHandleCount,
        ($finalHandleCount - $baselineHandleCount))

    $process.Refresh()
    $cpuMilliseconds = ($process.TotalProcessorTime - $cpuStart).TotalMilliseconds
    $averageCpuPercent = $cpuMilliseconds / [Math]::Max(1.0, $stressStopwatch.Elapsed.TotalMilliseconds) / [Math]::Max(1, [Environment]::ProcessorCount) * 100.0
    $summary = [ordered]@{
        TestName = '当前版保存工作池同进程长稳'
        FormalRoundCount = $FormalRoundCount
        AttemptsPerRound = $AttemptsPerRound
        TotalAttemptedCount = [int](($roundResults | Measure-Object -Property AttemptedCount -Sum).Sum)
        TotalAcceptedCount = [int](($roundResults | Measure-Object -Property AcceptedCount -Sum).Sum)
        TotalRejectedCount = [int](($roundResults | Measure-Object -Property RejectedCount -Sum).Sum)
        TotalEvictedNormalCount = [int](($roundResults | Measure-Object -Property EvictedNormalCount -Sum).Sum)
        TotalCompletedCount = [long](($roundResults | Measure-Object -Property CompletedCount -Sum).Sum)
        TotalFailedCount = [long](($roundResults | Measure-Object -Property FailedCount -Sum).Sum)
        TotalFileCount = [int](($roundResults | Measure-Object -Property FileCount -Sum).Sum)
        TotalFileBytes = [long](($roundResults | Measure-Object -Property TotalFileBytes -Sum).Sum)
        TotalLeaseDisposeCount = [int](($roundResults | Measure-Object -Property LeaseDisposeCount -Sum).Sum)
        TotalLeaseDoubleDisposeCount = [int](($roundResults | Measure-Object -Property LeaseDoubleDisposeCount -Sum).Sum)
        PeakQueueCount = [int](($roundResults | Measure-Object -Property PeakQueueCount -Maximum).Maximum)
        PeakQueueBytes = [long](($roundResults | Measure-Object -Property PeakQueueBytes -Maximum).Maximum)
        PeakActiveWorkers = [int](($roundResults | Measure-Object -Property PeakActiveWorkers -Maximum).Maximum)
        EnqueueP99MaximumMilliseconds = [double](($roundResults | Measure-Object -Property EnqueueP99Milliseconds -Maximum).Maximum)
        EnqueueAbsoluteMaximumMilliseconds = [double](($roundResults | Measure-Object -Property EnqueueMaxMilliseconds -Maximum).Maximum)
        StressWallMilliseconds = $stressStopwatch.ElapsedMilliseconds
        CpuLogicalProcessorCount = [Environment]::ProcessorCount
        AverageCpuPercent = [Math]::Round($averageCpuPercent, 3)
        PrivateMemoryBaselineMb = [Math]::Round($baselinePrivateMb, 3)
        PrivateMemoryFinalMb = [Math]::Round($finalPrivateMb, 3)
        PrivateMemoryDeltaMb = [Math]::Round(($finalPrivateMb - $baselinePrivateMb), 3)
        ManagedMemoryBaselineMb = [Math]::Round($baselineManagedMb, 3)
        ManagedMemoryFinalMb = [Math]::Round($finalManagedMb, 3)
        ManagedMemoryDeltaMb = [Math]::Round(($finalManagedMb - $baselineManagedMb), 3)
        HandleBaseline = $baselineHandleCount
        HandleFinal = $finalHandleCount
        HandleDelta = $finalHandleCount - $baselineHandleCount
        RoundResourceSnapshots = $roundResourceSnapshots
    }

    if (-not [string]::IsNullOrWhiteSpace($ResultPath)) {
        $resolvedResultPath = [IO.Path]::GetFullPath($ResultPath)
        $allowedResultRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'Tests\Results\ImageSaveComparison')).TrimEnd('\') + '\'
        Assert-True ($resolvedResultPath.StartsWith($allowedResultRoot, [StringComparison]::OrdinalIgnoreCase)) '长稳结果必须写入Tests\Results\ImageSaveComparison目录。'
        [IO.Directory]::CreateDirectory((Split-Path -Parent $resolvedResultPath)) | Out-Null
        $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedResultPath -Encoding UTF8
        Write-Host "RESULT_JSON=$resolvedResultPath"
    }
    $summary | ConvertTo-Json -Depth 8 | Write-Host
}
finally {
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($assemblyResolver)
    $env:PATH = $originalPath
    if (Test-Path -LiteralPath $testOutputRoot) {
        Remove-Item -LiteralPath $testOutputRoot -Recurse -Force
    }
}
