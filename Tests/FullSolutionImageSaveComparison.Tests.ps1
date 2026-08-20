param(
    [Parameter(Mandatory = $true)]
    [string]$ApplicationPath,

    [Parameter(Mandatory = $true)]
    [string]$VersionLabel,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [ValidateRange(1, 10000)]
    [int]$ImageCount = 500,

    [ValidateRange(64, 8192)]
    [int]$ImageWidth = 1024,

    [ValidateRange(64, 8192)]
    [int]$ImageHeight = 768,

    [ValidateRange(1, 100)]
    [int]$JpegQuality = 90,

    [ValidateRange(1, 16)]
    [int]$WorkerCount = 4,

    [ValidateRange(1, 10000)]
    [int]$QueueCapacity = 512,

    [ValidateRange(10, 600)]
    [int]$TimeoutSeconds = 120,

    [ValidateRange(0, 1000)]
    [int]$WarmupCount = 20,

    [switch]$RequireAllAccepted,

    [switch]$KeepRawFiles
)

$ErrorActionPreference = 'Stop'

# 生产程序集基于.NET Framework 4.8，统一转到Windows PowerShell隔离加载新旧同名程序集。
if ($PSVersionTable.PSEdition -eq 'Core') {
    $windowsPowerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $relayArguments = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', $PSCommandPath,
        '-ApplicationPath', $ApplicationPath,
        '-VersionLabel', $VersionLabel,
        '-OutputDirectory', $OutputDirectory,
        '-ImageCount', $ImageCount,
        '-ImageWidth', $ImageWidth,
        '-ImageHeight', $ImageHeight,
        '-JpegQuality', $JpegQuality,
        '-WorkerCount', $WorkerCount,
        '-QueueCapacity', $QueueCapacity,
        '-TimeoutSeconds', $TimeoutSeconds,
        '-WarmupCount', $WarmupCount
    )
    if ($RequireAllAccepted) { $relayArguments += '-RequireAllAccepted' }
    if ($KeepRawFiles) { $relayArguments += '-KeepRawFiles' }
    # Windows PowerShell 5.1按系统代码页读取无BOM脚本，因此该文件固定保存为UTF-8 BOM。
    & $windowsPowerShell @relayArguments
    exit $LASTEXITCODE
}

Add-Type -AssemblyName System.Drawing

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

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
$resultRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'Tests\Results\ImageSaveComparison'))
$resolvedOutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$requiredPrefix = $resultRoot.TrimEnd('\') + '\'
Assert-True ($resolvedOutputDirectory.StartsWith($requiredPrefix, [StringComparison]::OrdinalIgnoreCase)) '真实落盘输出目录必须位于Tests\Results\ImageSaveComparison内。'

if (Test-Path -LiteralPath $resolvedOutputDirectory) {
    Remove-Item -LiteralPath $resolvedOutputDirectory -Recurse -Force
}
[IO.Directory]::CreateDirectory($resolvedOutputDirectory) | Out-Null
[IO.Directory]::CreateDirectory($resultRoot) | Out-Null

$resolvedApplicationPath = (Resolve-Path -LiteralPath $ApplicationPath).Path
$applicationDirectory = Split-Path -Parent $resolvedApplicationPath
[Environment]::CurrentDirectory = $applicationDirectory
$originalPath = $env:PATH
$env:PATH = "$applicationDirectory;$(Join-Path $applicationDirectory 'dll\x64');$env:PATH"
$assemblyResolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    try {
        $requestedAssembly = New-Object Reflection.AssemblyName($eventArgs.Name)
    }
    catch {
        return $null
    }

    $dependencyPath = Join-Path $applicationDirectory ($requestedAssembly.Name + '.dll')
    if (Test-Path -LiteralPath $dependencyPath) {
        return [Reflection.Assembly]::LoadFrom($dependencyPath)
    }

    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($assemblyResolver)

try {
    $applicationAssembly = [Reflection.Assembly]::LoadFrom($resolvedApplicationPath)
    $processorType = $applicationAssembly.GetType('TDJS_Vision.Node._7_ResultProcessing.ImageSave.ImageQueueProcessor', $true)
    $taskType = $applicationAssembly.GetType('TDJS_Vision.Node._7_ResultProcessing.ImageSave.ImageQueueProcessor+SaveImageTask', $true)
    $isBoundedArchitecture = $null -ne $taskType.GetProperty('ImageLease')

    $helperSource = @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using OpenCvSharp;
using TDJS_Vision.Node._7_ResultProcessing.ImageSave;
#if BOUNDED
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.ResourceManagement;
#endif
using SaveImageTask = TDJS_Vision.Node._7_ResultProcessing.ImageSave.ImageQueueProcessor.SaveImageTask;

#if BOUNDED
/// <summary>共享源Mat的非拥有租约；正式测试结束后由测试宿主统一释放源Mat。</summary>
public sealed class SharedSourceImageLease : IImageResourceLease
{
    private int _disposed;

    /// <summary>已释放租约总数。</summary>
    public static int DisposeCount;

    /// <summary>重复释放租约总数。</summary>
    public static int DoubleDisposeCount;

    /// <summary>共享源图不通过租约公开OutputImage。</summary>
    public OutputImage Image { get { return null; } }

    /// <summary>幂等归还单个任务的借用权，不释放共享源Mat。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            Interlocked.Increment(ref DoubleDisposeCount);
            return;
        }

        Interlocked.Increment(ref DisposeCount);
    }

    /// <summary>清除正式测试前的租约计数。</summary>
    public static void Reset()
    {
        DisposeCount = 0;
        DoubleDisposeCount = 0;
    }
}
#endif

/// <summary>单次真实JPEG落盘的完整可比较结果。</summary>
public sealed class ImageSaveComparisonResult
{
    /// <summary>保存架构名称。</summary>
    public string Architecture { get; set; }
    /// <summary>计划投递图片数。</summary>
    public int AttemptedCount { get; set; }
    /// <summary>队列接收图片数。</summary>
    public int AcceptedCount { get; set; }
    /// <summary>队列拒绝图片数。</summary>
    public int RejectedCount { get; set; }
    /// <summary>淘汰普通任务数。</summary>
    public int EvictedCount { get; set; }
    /// <summary>最终完成文件数。</summary>
    public int FileCount { get; set; }
    /// <summary>空文件数。</summary>
    public int EmptyFileCount { get; set; }
    /// <summary>残留.saving临时文件数。</summary>
    public int TemporaryFileCount { get; set; }
    /// <summary>无法完整解码的JPEG数量。</summary>
    public int InvalidImageCount { get; set; }
    /// <summary>内容哈希不一致的JPEG数量。</summary>
    public int HashMismatchCount { get; set; }
    /// <summary>全部JPEG总字节数。</summary>
    public long TotalFileBytes { get; set; }
    /// <summary>单个JPEG最小字节数。</summary>
    public long MinimumFileBytes { get; set; }
    /// <summary>单个JPEG最大字节数。</summary>
    public long MaximumFileBytes { get; set; }
    /// <summary>首个JPEG的SHA256。</summary>
    public string FirstFileSha256 { get; set; }
    /// <summary>首个JPEG文件路径。</summary>
    public string FirstFilePath { get; set; }
    /// <summary>源图原始像素SHA256。</summary>
    public string SourcePixelSha256 { get; set; }
    /// <summary>全部任务入队墙钟毫秒。</summary>
    public long EnqueueWallMilliseconds { get; set; }
    /// <summary>入队P50毫秒。</summary>
    public double EnqueueP50Milliseconds { get; set; }
    /// <summary>入队P95毫秒。</summary>
    public double EnqueueP95Milliseconds { get; set; }
    /// <summary>入队P99毫秒。</summary>
    public double EnqueueP99Milliseconds { get; set; }
    /// <summary>入队最大毫秒。</summary>
    public double EnqueueMaximumMilliseconds { get; set; }
    /// <summary>从首个入队到全部目标文件完成并停止工作池的总墙钟毫秒。</summary>
    public long SaveWallMilliseconds { get; set; }
    /// <summary>从首个入队到全部目标文件完成的墙钟毫秒，不包含工作池停止等待。</summary>
    public long FilesCompletedWallMilliseconds { get; set; }
    /// <summary>全部目标文件完成后停止工作池的墙钟毫秒。</summary>
    public long ShutdownWallMilliseconds { get; set; }
    /// <summary>按最终JPEG字节和文件完成墙钟计算的保存吞吐MB/s。</summary>
    public double FileMegabytesPerSecond { get; set; }
    /// <summary>文件解码和哈希校验耗时毫秒。</summary>
    public long ValidationMilliseconds { get; set; }
    /// <summary>测试期间按全机逻辑线程归一化的平均CPU占用百分比。</summary>
    public double AverageCpuPercent { get; set; }
    /// <summary>测试期间按全机逻辑线程归一化的采样峰值CPU占用百分比。</summary>
    public double PeakCpuPercent { get; set; }
    /// <summary>CPU占用归一化使用的逻辑线程数。</summary>
    public int CpuLogicalProcessorCount { get; set; }
    /// <summary>进程写操作增量。</summary>
    public ulong ProcessWriteOperations { get; set; }
    /// <summary>进程写传输字节增量。</summary>
    public ulong ProcessWriteTransferBytes { get; set; }
    /// <summary>进程读操作增量。</summary>
    public ulong ProcessReadOperations { get; set; }
    /// <summary>进程读传输字节增量。</summary>
    public ulong ProcessReadTransferBytes { get; set; }
    /// <summary>测试起始私有内存MB。</summary>
    public double PrivateMemoryStartMb { get; set; }
    /// <summary>测试采样私有内存峰值MB。</summary>
    public double PrivateMemoryPeakMb { get; set; }
    /// <summary>强制完整回收后私有内存MB。</summary>
    public double PrivateMemoryAfterCollectionMb { get; set; }
    /// <summary>测试起始托管内存MB。</summary>
    public double ManagedMemoryStartMb { get; set; }
    /// <summary>测试采样托管内存峰值MB。</summary>
    public double ManagedMemoryPeakMb { get; set; }
    /// <summary>强制完整回收后托管内存MB。</summary>
    public double ManagedMemoryAfterCollectionMb { get; set; }
    /// <summary>测试起始句柄数。</summary>
    public int HandleStart { get; set; }
    /// <summary>测试采样句柄峰值。</summary>
    public int HandlePeak { get; set; }
    /// <summary>测试结束句柄数。</summary>
    public int HandleEnd { get; set; }
    /// <summary>测试起始GDI对象数。</summary>
    public int GdiStart { get; set; }
    /// <summary>测试采样GDI对象峰值。</summary>
    public int GdiPeak { get; set; }
    /// <summary>测试结束GDI对象数。</summary>
    public int GdiEnd { get; set; }
    /// <summary>新版队列峰值任务数。</summary>
    public int PeakQueueCount { get; set; }
    /// <summary>新版队列峰值租约字节。</summary>
    public long PeakQueueBytes { get; set; }
    /// <summary>新版实际工作线程峰值。</summary>
    public int PeakActiveWorkers { get; set; }
    /// <summary>新版完成任务计数。</summary>
    public long CompletedTaskCount { get; set; }
    /// <summary>新版失败任务计数。</summary>
    public long FailedTaskCount { get; set; }
    /// <summary>新版租约释放次数。</summary>
    public int LeaseDisposeCount { get; set; }
    /// <summary>新版租约重复释放次数。</summary>
    public int LeaseDoubleDisposeCount { get; set; }
    /// <summary>保存工作线程是否在时限内停止。</summary>
    public bool GracefullyStopped { get; set; }
}

/// <summary>Windows进程I/O累计计数。</summary>
[StructLayout(LayoutKind.Sequential)]
public struct SaveComparisonIoCounters
{
    /// <summary>累计读操作数。</summary>
    public ulong ReadOperationCount;
    /// <summary>累计写操作数。</summary>
    public ulong WriteOperationCount;
    /// <summary>累计其他I/O操作数。</summary>
    public ulong OtherOperationCount;
    /// <summary>累计读传输字节。</summary>
    public ulong ReadTransferCount;
    /// <summary>累计写传输字节。</summary>
    public ulong WriteTransferCount;
    /// <summary>累计其他传输字节。</summary>
    public ulong OtherTransferCount;
}

/// <summary>测试期间低频采样进程资源峰值。</summary>
internal sealed class SaveComparisonResourceSampler : IDisposable
{
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly Thread _thread;
    private volatile bool _stopping;
    private TimeSpan _previousCpuTime;
    private long _previousTimestamp;
    private double _cpuPercentTotal;
    private int _cpuSampleCount;

    /// <summary>私有内存采样峰值字节。</summary>
    public long PeakPrivateBytes { get; private set; }
    /// <summary>托管内存采样峰值字节。</summary>
    public long PeakManagedBytes { get; private set; }
    /// <summary>进程句柄采样峰值。</summary>
    public int PeakHandles { get; private set; }
    /// <summary>GDI对象采样峰值。</summary>
    public int PeakGdi { get; private set; }
    /// <summary>按全机逻辑线程归一化的平均CPU占用百分比。</summary>
    public double AverageCpuPercent { get { return _cpuSampleCount == 0 ? 0.0 : _cpuPercentTotal / _cpuSampleCount; } }
    /// <summary>按全机逻辑线程归一化的采样峰值CPU占用百分比。</summary>
    public double PeakCpuPercent { get; private set; }

    /// <summary>创建10ms资源采样线程。</summary>
    public SaveComparisonResourceSampler()
    {
        _thread = new Thread(SampleLoop);
        _thread.IsBackground = true;
        _thread.Name = "真实落盘资源采样";
    }

    /// <summary>启动资源采样。</summary>
    public void Start()
    {
        _process.Refresh();
        _previousCpuTime = _process.TotalProcessorTime;
        _previousTimestamp = Stopwatch.GetTimestamp();
        SampleResourcePeaks();
        _thread.Start();
    }

    /// <summary>停止采样并等待采样线程退出。</summary>
    public void Dispose()
    {
        _stopping = true;
        _thread.Join(5000);
        SampleResourcePeaks();
        _process.Dispose();
    }

    /// <summary>持续更新资源峰值。</summary>
    private void SampleLoop()
    {
        while (!_stopping)
        {
            Thread.Sleep(10);
            try
            {
                SampleResourcePeaks();
            }
            catch
            {
                // 下一采样周期继续，单次资源读取失败不能影响真实保存任务。
            }
        }
    }

    /// <summary>更新内存、句柄、GDI和归一化CPU采样值。</summary>
    private void SampleResourcePeaks()
    {
        long currentTimestamp = Stopwatch.GetTimestamp();
        _process.Refresh();
        TimeSpan currentCpuTime = _process.TotalProcessorTime;
        long elapsedTicks = currentTimestamp - _previousTimestamp;
        if (_previousTimestamp != 0L && elapsedTicks > 0L)
        {
            double elapsedMilliseconds = elapsedTicks * 1000.0 / Stopwatch.Frequency;
            double cpuMilliseconds = (currentCpuTime - _previousCpuTime).TotalMilliseconds;
            double cpuPercent = cpuMilliseconds / elapsedMilliseconds / Math.Max(1, Environment.ProcessorCount) * 100.0;
            cpuPercent = Math.Max(0.0, Math.Min(100.0, cpuPercent));
            _cpuPercentTotal += cpuPercent;
            _cpuSampleCount++;
            PeakCpuPercent = Math.Max(PeakCpuPercent, cpuPercent);
        }

        _previousCpuTime = currentCpuTime;
        _previousTimestamp = currentTimestamp;
        PeakPrivateBytes = Math.Max(PeakPrivateBytes, _process.PrivateMemorySize64);
        PeakManagedBytes = Math.Max(PeakManagedBytes, GC.GetTotalMemory(false));
        PeakHandles = Math.Max(PeakHandles, _process.HandleCount);
        PeakGdi = Math.Max(PeakGdi, ImageSaveComparisonHarness.GetGuiResources(_process.Handle, 0));
    }
}

/// <summary>直接调用生产保存工作池的跨版本真实落盘宿主。</summary>
public static class ImageSaveComparisonHarness
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessIoCounters(IntPtr processHandle, out SaveComparisonIoCounters counters);

    [DllImport("user32.dll")]
    internal static extern int GetGuiResources(IntPtr processHandle, int resourceType);

    /// <summary>执行一次确定性噪声图JPEG落盘测试。</summary>
    public static ImageSaveComparisonResult Run(
        string outputDirectory,
        int imageCount,
        int width,
        int height,
        int jpegQuality,
        int workerCount,
        int queueCapacity,
        int timeoutMilliseconds,
        bool requireAllAccepted)
    {
        Directory.CreateDirectory(outputDirectory);
        ForceCollection();
        Process process = Process.GetCurrentProcess();
        process.Refresh();
        long privateStart = process.PrivateMemorySize64;
        long managedStart = GC.GetTotalMemory(false);
        int handleStart = process.HandleCount;
        int gdiStart = GetGuiResources(process.Handle, 0);
        SaveComparisonIoCounters ioStart = QueryIo(process.Handle);
        byte[] sourcePixels;
        Mat sourceImage = CreateNoiseImage(width, height, out sourcePixels);
        string sourcePixelHash = ComputeHash(sourcePixels);
        long estimatedBytes = checked((long)sourceImage.Rows * (long)sourceImage.Step());
        int accepted = 0;
        int rejected = 0;
        int evicted = 0;
        bool gracefullyStopped = true;
        long filesCompletedWallMilliseconds = 0L;
        List<long> enqueueTicks = new List<long>(imageCount);

#if BOUNDED
        SharedSourceImageLease.Reset();
        long memoryBudget = Math.Max(512L * 1024L * 1024L, checked(estimatedBytes * Math.Max(1, queueCapacity)));
        ImageQueueProcessor processor = new ImageQueueProcessor(
            workerCount,
            queueCapacity,
            memoryBudget,
            timeoutMilliseconds,
            "真实落盘同比");
#else
        ImageQueueProcessor processor = new ImageQueueProcessor(workerCount, "真实落盘同比");
#endif

        SaveComparisonResourceSampler sampler = new SaveComparisonResourceSampler();
        Stopwatch saveStopwatch = Stopwatch.StartNew();
        Stopwatch enqueueStopwatch = Stopwatch.StartNew();
        Stopwatch shutdownStopwatch = new Stopwatch();
        try
        {
            processor.StartProcessing();
            sampler.Start();
            for (int index = 0; index < imageCount; index++)
            {
                SaveImageTask task = new SaveImageTask
                {
                    SourceImage = sourceImage,
                    TargetDirectories = new List<string> { outputDirectory },
                    ImageName = "frame_" + index.ToString("D5"),
                    NeedCompress = true,
                    CompressValue = jpegQuality
                };
#if BOUNDED
                task.EstimatedBytes = estimatedBytes;
                task.OwnerKey = "真实落盘同比";
                task.ImageLease = new SharedSourceImageLease();
#endif
                long started = Stopwatch.GetTimestamp();
#if BOUNDED
                BoundedQueueAdmissionResult admission = processor.EnqueueImage(task);
                enqueueTicks.Add(Stopwatch.GetTimestamp() - started);
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
#else
                processor.EnqueueImage(task);
                enqueueTicks.Add(Stopwatch.GetTimestamp() - started);
                accepted++;
#endif
            }
            enqueueStopwatch.Stop();

            WaitForCompletedFiles(outputDirectory, accepted - evicted, timeoutMilliseconds);
            filesCompletedWallMilliseconds = saveStopwatch.ElapsedMilliseconds;
            shutdownStopwatch.Start();
#if BOUNDED
            gracefullyStopped = processor.StopProcessing();
#else
            processor.StopProcessing();
#endif
            shutdownStopwatch.Stop();
            saveStopwatch.Stop();
        }
        finally
        {
            if (enqueueStopwatch.IsRunning)
                enqueueStopwatch.Stop();
            if (saveStopwatch.IsRunning)
                saveStopwatch.Stop();
            if (shutdownStopwatch.IsRunning)
                shutdownStopwatch.Stop();
            sampler.Dispose();
            sourceImage.Dispose();
        }

        SaveComparisonIoCounters ioSaveEnd = QueryIo(process.Handle);

        if (requireAllAccepted && (accepted != imageCount || rejected != 0 || evicted != 0))
            throw new InvalidOperationException("同工作量落盘要求全部任务被接收且不得淘汰。请求=" + imageCount + "，接收=" + accepted + "，拒绝=" + rejected + "，淘汰=" + evicted);

        FileInfo[] files = new DirectoryInfo(outputDirectory).GetFiles("*.jpg", SearchOption.TopDirectoryOnly).OrderBy(file => file.Name).ToArray();
        int expectedFileCount = accepted - evicted;
        if (files.Length != expectedFileCount)
            throw new InvalidOperationException("完成文件数不等于任务最终去向。期望=" + expectedFileCount + "，实际=" + files.Length);

        Stopwatch validationStopwatch = Stopwatch.StartNew();
        int invalidImageCount = 0;
        int hashMismatchCount = 0;
        string firstHash = files.Length == 0 ? string.Empty : ComputeFileHash(files[0].FullName);
        foreach (FileInfo file in files)
        {
            try
            {
                using (System.Drawing.Image image = System.Drawing.Image.FromFile(file.FullName))
                {
                    if (image.Width != width || image.Height != height)
                        invalidImageCount++;
                }
            }
            catch
            {
                invalidImageCount++;
            }

            if (!String.Equals(firstHash, ComputeFileHash(file.FullName), StringComparison.OrdinalIgnoreCase))
                hashMismatchCount++;
        }
        validationStopwatch.Stop();

        int temporaryFileCount = new DirectoryInfo(outputDirectory).GetFiles("*.saving", SearchOption.TopDirectoryOnly).Length;
        int emptyFileCount = files.Count(file => file.Length == 0);
        long totalFileBytes = files.Sum(file => file.Length);
        ForceCollection();
        process.Refresh();
        double[] enqueueMilliseconds = enqueueTicks.Select(ticks => ticks * 1000.0 / Stopwatch.Frequency).OrderBy(value => value).ToArray();

        ImageSaveComparisonResult result = new ImageSaveComparisonResult
        {
#if BOUNDED
            Architecture = "当前版有界共享保存工作池",
#else
            Architecture = "旧版节点级无界保存队列",
#endif
            AttemptedCount = imageCount,
            AcceptedCount = accepted,
            RejectedCount = rejected,
            EvictedCount = evicted,
            FileCount = files.Length,
            EmptyFileCount = emptyFileCount,
            TemporaryFileCount = temporaryFileCount,
            InvalidImageCount = invalidImageCount,
            HashMismatchCount = hashMismatchCount,
            TotalFileBytes = totalFileBytes,
            MinimumFileBytes = files.Length == 0 ? 0 : files.Min(file => file.Length),
            MaximumFileBytes = files.Length == 0 ? 0 : files.Max(file => file.Length),
            FirstFileSha256 = firstHash,
            FirstFilePath = files.Length == 0 ? string.Empty : files[0].FullName,
            SourcePixelSha256 = sourcePixelHash,
            EnqueueWallMilliseconds = enqueueStopwatch.ElapsedMilliseconds,
            EnqueueP50Milliseconds = Percentile(enqueueMilliseconds, 0.50),
            EnqueueP95Milliseconds = Percentile(enqueueMilliseconds, 0.95),
            EnqueueP99Milliseconds = Percentile(enqueueMilliseconds, 0.99),
            EnqueueMaximumMilliseconds = enqueueMilliseconds.Length == 0 ? 0.0 : enqueueMilliseconds[enqueueMilliseconds.Length - 1],
            SaveWallMilliseconds = saveStopwatch.ElapsedMilliseconds,
            FilesCompletedWallMilliseconds = filesCompletedWallMilliseconds,
            ShutdownWallMilliseconds = shutdownStopwatch.ElapsedMilliseconds,
            FileMegabytesPerSecond = (totalFileBytes / 1024.0 / 1024.0) / Math.Max(0.001, filesCompletedWallMilliseconds / 1000.0),
            ValidationMilliseconds = validationStopwatch.ElapsedMilliseconds,
            AverageCpuPercent = sampler.AverageCpuPercent,
            PeakCpuPercent = sampler.PeakCpuPercent,
            CpuLogicalProcessorCount = Environment.ProcessorCount,
            ProcessWriteOperations = Delta(ioSaveEnd.WriteOperationCount, ioStart.WriteOperationCount),
            ProcessWriteTransferBytes = Delta(ioSaveEnd.WriteTransferCount, ioStart.WriteTransferCount),
            ProcessReadOperations = Delta(ioSaveEnd.ReadOperationCount, ioStart.ReadOperationCount),
            ProcessReadTransferBytes = Delta(ioSaveEnd.ReadTransferCount, ioStart.ReadTransferCount),
            PrivateMemoryStartMb = privateStart / 1024.0 / 1024.0,
            PrivateMemoryPeakMb = sampler.PeakPrivateBytes / 1024.0 / 1024.0,
            PrivateMemoryAfterCollectionMb = process.PrivateMemorySize64 / 1024.0 / 1024.0,
            ManagedMemoryStartMb = managedStart / 1024.0 / 1024.0,
            ManagedMemoryPeakMb = sampler.PeakManagedBytes / 1024.0 / 1024.0,
            ManagedMemoryAfterCollectionMb = GC.GetTotalMemory(false) / 1024.0 / 1024.0,
            HandleStart = handleStart,
            HandlePeak = sampler.PeakHandles,
            HandleEnd = process.HandleCount,
            GdiStart = gdiStart,
            GdiPeak = sampler.PeakGdi,
            GdiEnd = GetGuiResources(process.Handle, 0),
            GracefullyStopped = gracefullyStopped
        };
#if BOUNDED
        result.PeakQueueCount = processor.PeakQueuedCount;
        result.PeakQueueBytes = processor.PeakQueuedBytes;
        result.PeakActiveWorkers = processor.PeakActiveWorkerCount;
        result.CompletedTaskCount = processor.CompletedTaskCount;
        result.FailedTaskCount = processor.FailedTaskCount;
        result.LeaseDisposeCount = SharedSourceImageLease.DisposeCount;
        result.LeaseDoubleDisposeCount = SharedSourceImageLease.DoubleDisposeCount;
#endif
        return result;
    }

    /// <summary>读取当前进程I/O累计值。</summary>
    private static SaveComparisonIoCounters QueryIo(IntPtr processHandle)
    {
        SaveComparisonIoCounters counters;
        if (!GetProcessIoCounters(processHandle, out counters))
            throw new InvalidOperationException("GetProcessIoCounters失败，错误码=" + Marshal.GetLastWin32Error());
        return counters;
    }

    /// <summary>创建确定性随机像素的三通道测试图。</summary>
    private static Mat CreateNoiseImage(int width, int height, out byte[] pixels)
    {
        Mat image = new Mat(height, width, MatType.CV_8UC3);
        int byteCount = checked((int)((long)image.Rows * (long)image.Step()));
        pixels = new byte[byteCount];
        new Random(20260820).NextBytes(pixels);
        Marshal.Copy(pixels, 0, image.Data, pixels.Length);
        return image;
    }

    /// <summary>等待目标数量文件写完并解除独占占用。</summary>
    private static void WaitForCompletedFiles(string outputDirectory, int expectedCount, int timeoutMilliseconds)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        while (timeout.ElapsedMilliseconds < timeoutMilliseconds)
        {
            FileInfo[] files = new DirectoryInfo(outputDirectory).GetFiles("*.jpg", SearchOption.TopDirectoryOnly);
            if (files.Length == expectedCount && files.All(file => file.Length > 0 && CanOpenExclusively(file.FullName)))
                return;
            Thread.Sleep(10);
        }

        throw new TimeoutException("等待JPEG全部完成超时。期望=" + expectedCount + "，实际=" + new DirectoryInfo(outputDirectory).GetFiles("*.jpg").Length);
    }

    /// <summary>确认文件已经关闭写入句柄。</summary>
    private static bool CanOpenExclusively(string path)
    {
        try
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                return stream.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>计算有序数组分位数。</summary>
    private static double Percentile(double[] sortedValues, double percentile)
    {
        if (sortedValues == null || sortedValues.Length == 0)
            return 0.0;
        int index = (int)Math.Ceiling(sortedValues.Length * percentile) - 1;
        index = Math.Max(0, Math.Min(sortedValues.Length - 1, index));
        return sortedValues[index];
    }

    /// <summary>计算无符号累计计数增量。</summary>
    private static ulong Delta(ulong current, ulong previous)
    {
        return current >= previous ? current - previous : current;
    }

    /// <summary>计算内存字节SHA256。</summary>
    private static string ComputeHash(byte[] bytes)
    {
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
    }

    /// <summary>计算文件SHA256。</summary>
    private static string ComputeFileHash(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
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

    $applicationReferencePath = Join-Path $resultRoot ('TDJSVision.ImageSaveComparison.' + [Guid]::NewGuid().ToString('N') + '.dll')
    Copy-Item -LiteralPath $resolvedApplicationPath -Destination $applicationReferencePath -Force
    $references = @(
        'mscorlib',
        'System',
        'System.Drawing',
        'System.Core',
        'System.Threading',
        'System.Collections',
        'System.Runtime',
        'System.IO.FileSystem',
        'System.Linq',
        $applicationReferencePath,
        (Join-Path $applicationDirectory 'OpenCvSharp.dll'),
        (Join-Path $applicationDirectory 'OpenCvSharp.Extensions.dll')
    )
    try {
        $addTypeParameters = @{
            TypeDefinition = $helperSource
            Language = 'CSharp'
            ReferencedAssemblies = $references
        }
        if ($isBoundedArchitecture) {
            $compilerParameters = New-Object CodeDom.Compiler.CompilerParameters
            $compilerParameters.CompilerOptions = '/define:BOUNDED'
            $compilerParameters.GenerateInMemory = $true
            foreach ($reference in $references) {
                $compilerReference = if ([IO.Path]::IsPathRooted($reference)) { $reference } else { $reference + '.dll' }
                [void]$compilerParameters.ReferencedAssemblies.Add($compilerReference)
            }
            [void]$addTypeParameters.Remove('ReferencedAssemblies')
            $addTypeParameters.CompilerParameters = $compilerParameters
        }
        Add-Type @addTypeParameters
    }
    finally {
        Remove-Item -LiteralPath $applicationReferencePath -Force -ErrorAction SilentlyContinue
    }

    if ($WarmupCount -gt 0) {
        $warmupDirectory = $resolvedOutputDirectory + '-warmup'
        if (Test-Path -LiteralPath $warmupDirectory) {
            Remove-Item -LiteralPath $warmupDirectory -Recurse -Force
        }
        [void][ImageSaveComparisonHarness]::Run(
            $warmupDirectory,
            $WarmupCount,
            $ImageWidth,
            $ImageHeight,
            $JpegQuality,
            $WorkerCount,
            [Math]::Max($QueueCapacity, $WarmupCount + 8),
            ($TimeoutSeconds * 1000),
            $true)
        Remove-Item -LiteralPath $warmupDirectory -Recurse -Force
        Invoke-FullCollection
    }

    $result = [ImageSaveComparisonHarness]::Run(
        $resolvedOutputDirectory,
        $ImageCount,
        $ImageWidth,
        $ImageHeight,
        $JpegQuality,
        $WorkerCount,
        $QueueCapacity,
        ($TimeoutSeconds * 1000),
        $RequireAllAccepted.IsPresent)

    Assert-True ($result.EmptyFileCount -eq 0) '真实落盘不允许产生空文件。'
    Assert-True ($result.TemporaryFileCount -eq 0) '真实落盘结束不允许残留.saving临时文件。'
    Assert-True ($result.InvalidImageCount -eq 0) '所有JPEG必须能解码且尺寸正确。'
    Assert-True ($result.HashMismatchCount -eq 0) '同一确定性源图的全部JPEG内容哈希必须一致。'
    Assert-True ($result.FailedTaskCount -eq 0) '新版保存工作池不允许出现失败任务。'
    if ($isBoundedArchitecture) {
        Assert-True ($result.PeakQueueCount -le $QueueCapacity) '新版保存队列峰值不得突破配置容量。'
        Assert-True ($result.PeakActiveWorkers -le $WorkerCount) '新版保存并发不得突破配置线程数。'
        Assert-True ($result.LeaseDisposeCount -eq $ImageCount) '新版每个尝试任务的借用租约必须恰好释放一次。'
        Assert-True ($result.LeaseDoubleDisposeCount -eq 0) '新版保存任务不允许重复释放借用租约。'
        Assert-True $result.GracefullyStopped '新版保存工作池必须在超时内排空并停止。'
    }

    $safeLabel = $VersionLabel -replace '[^0-9A-Za-z\u4e00-\u9fa5_-]', '_'
    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $sampleDirectory = Join-Path $resultRoot 'Samples'
    [IO.Directory]::CreateDirectory($sampleDirectory) | Out-Null
    $samplePath = Join-Path $sampleDirectory ("${timestamp}-${safeLabel}-sample.jpg")
    if (-not [string]::IsNullOrWhiteSpace($result.FirstFilePath)) {
        Copy-Item -LiteralPath $result.FirstFilePath -Destination $samplePath -Force
    }

    $resultPath = Join-Path $resultRoot ("${timestamp}-${safeLabel}-${ImageCount}张.json")
    $record = [ordered]@{
        VersionLabel = $VersionLabel
        ApplicationPath = $resolvedApplicationPath
        ApplicationSha256 = (Get-FileHash -LiteralPath $resolvedApplicationPath -Algorithm SHA256).Hash
        IsBoundedArchitecture = $isBoundedArchitecture
        Parameters = [ordered]@{
            ImageCount = $ImageCount
            ImageWidth = $ImageWidth
            ImageHeight = $ImageHeight
            JpegQuality = $JpegQuality
            WorkerCount = $WorkerCount
            QueueCapacity = $QueueCapacity
            TimeoutSeconds = $TimeoutSeconds
            WarmupCount = $WarmupCount
            RequireAllAccepted = $RequireAllAccepted.IsPresent
        }
        Result = $result
        SampleFile = $samplePath
        RawFilesKept = $KeepRawFiles.IsPresent
        MeasurementBoundary = '直接调用对应版本生产ImageQueueProcessor；文件完成墙钟包含Mat转Bitmap、JPEG编码和操作系统缓存写入，线程停止另行计时；两者都不代表断电安全的物理介质刷盘时延。'
    }
    $record | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resultPath -Encoding UTF8

    if (-not $KeepRawFiles) {
        Remove-Item -LiteralPath $resolvedOutputDirectory -Recurse -Force
    }

    Write-Output ("RESULT_JSON={0}" -f $resultPath)
    Write-Output ($record | ConvertTo-Json -Depth 10)
}
finally {
    $env:PATH = $originalPath
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($assemblyResolver)
}
