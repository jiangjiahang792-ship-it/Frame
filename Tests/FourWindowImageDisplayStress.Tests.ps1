$ErrorActionPreference = 'Stop'

# Production binaries target .NET Framework 4.8, so keep the runtime probe on Windows PowerShell.
if ($PSVersionTable.PSEdition -eq 'Core') {
    $windowsPowerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    & $windowsPowerShell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath
    exit $LASTEXITCODE
}

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

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
Assert-True ($null -ne $application) 'The Debug application is required for the four-window display stress test.'

[Environment]::CurrentDirectory = $debugDirectory
$assemblyResolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $requestedAssembly = New-Object Reflection.AssemblyName($eventArgs.Name)
    $dependencyPath = Join-Path $debugDirectory ($requestedAssembly.Name + '.dll')
    if (Test-Path -LiteralPath $dependencyPath) {
        return [Reflection.Assembly]::LoadFrom($dependencyPath)
    }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($assemblyResolver)

$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$windowType = $assembly.GetType('TDJS_Vision.Forms.ImageViewer.FrmSingleImage', $true)
$nodeImageShowType = $assembly.GetType('TDJS_Vision.Node._1_Acquisition.ImageSource.NodeImageShow', $true)
$tryAcquireMethod = $nodeImageShowType.GetMethod('TryAcquireWindowRefresh', [Reflection.BindingFlags]'Public,Static')
$publishMethod = $nodeImageShowType.GetMethod('PublishImageShowChanged', [Reflection.BindingFlags]'Public,Static')
$limiterProperty = $nodeImageShowType.GetProperty('UiFrameRateLimiter', [Reflection.BindingFlags]'Public,Static')
$pendingFrameField = $windowType.GetField('_pendingViewerFrame', [Reflection.BindingFlags]'Instance,NonPublic')
$dispatchField = $windowType.GetField('_viewerDispatchScheduled', [Reflection.BindingFlags]'Instance,NonPublic')
Assert-True ($null -ne $tryAcquireMethod -and $null -ne $publishMethod) 'The production display gate and publisher are required.'
Assert-True ($null -ne $limiterProperty) 'The replaceable production display limiter is required.'
Assert-True ($null -ne $pendingFrameField -and $null -ne $dispatchField) 'The bounded window state is required.'

$stressHelper = @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using OpenCvSharp;
using OpenCvSharp.Extensions;

public sealed class FourWindowDisplayStressResult
{
    public int[] AttemptedPerWindow { get; set; }
    public int[] AllowedPerWindow { get; set; }
    public int[] ClaimedPerWindow { get; set; }
    public double[] AverageConversionMilliseconds { get; set; }
    public double[] P95ConversionMilliseconds { get; set; }
    public double[] P99ConversionMilliseconds { get; set; }
    public double[] MaximumConversionMilliseconds { get; set; }
    public Exception[] Errors { get; set; }
    public long ElapsedMilliseconds { get; set; }
}

public sealed class FourWindowDisplayStressSession : IDisposable
{
    private readonly CountdownEvent _completed;
    private readonly CancellationTokenSource _cancellation;
    private readonly ManualResetEventSlim _start;
    private readonly Thread[] _workers;
    private readonly Stopwatch _stopwatch;
    private int _finished;

    public FourWindowDisplayStressSession(
        FourWindowDisplayStressResult result,
        CountdownEvent completed,
        CancellationTokenSource cancellation,
        ManualResetEventSlim start,
        Thread[] workers,
        Stopwatch stopwatch)
    {
        Result = result;
        _completed = completed;
        _cancellation = cancellation;
        _start = start;
        _workers = workers;
        _stopwatch = stopwatch;
    }

    public FourWindowDisplayStressResult Result { get; private set; }

    public bool IsCompleted
    {
        get { return _completed.IsSet; }
    }

    public FourWindowDisplayStressResult Wait()
    {
        _completed.Wait();
        Finish();
        return Result;
    }

    public void Cancel()
    {
        _cancellation.Cancel();
        _start.Set();
    }

    public bool CancelAndWait(int timeoutMilliseconds)
    {
        Cancel();
        if (!_completed.Wait(timeoutMilliseconds))
            return false;

        Finish();
        return true;
    }

    public void Dispose()
    {
        if (!_completed.IsSet && !CancelAndWait(30000))
        {
            Environment.FailFast("Four-window workers did not stop while disposing the stress session.");
        }

        Finish();
        _cancellation.Dispose();
        _start.Dispose();
        _completed.Dispose();
    }

    private void Finish()
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
            return;

        for (int index = 0; index < _workers.Length; index++)
        {
            if (_workers[index] != null)
                _workers[index].Join();
        }
        _stopwatch.Stop();
        Result.ElapsedMilliseconds = _stopwatch.ElapsedMilliseconds;
    }
}

public static class FourWindowDisplayStressRunner
{
    public static bool WarmUpPublish(
        MethodInfo publish,
        string windowName,
        int width,
        int height)
    {
        using (Mat mat = new Mat(height, width, MatType.CV_8UC3, new Scalar(16, 64, 128)))
        {
            Bitmap bitmap = null;
            try
            {
                bitmap = BitmapConverter.ToBitmap(mat);
                Bitmap publisherBitmap = bitmap;
                bitmap = null;
                return (bool)publish.Invoke(
                    null,
                    new object[] { null, windowName, publisherBitmap, null, null });
            }
            finally
            {
                if (bitmap != null)
                    bitmap.Dispose();
            }
        }
    }

    public static FourWindowDisplayStressSession Start(
        MethodInfo tryAcquire,
        MethodInfo publish,
        string[] windowNames,
        int framesPerWindow,
        int width,
        int height,
        int intervalMilliseconds)
    {
        int windowCount = windowNames.Length;
        FourWindowDisplayStressResult result = new FourWindowDisplayStressResult
        {
            AttemptedPerWindow = new int[windowCount],
            AllowedPerWindow = new int[windowCount],
            ClaimedPerWindow = new int[windowCount],
            AverageConversionMilliseconds = new double[windowCount],
            P95ConversionMilliseconds = new double[windowCount],
            P99ConversionMilliseconds = new double[windowCount],
            MaximumConversionMilliseconds = new double[windowCount],
            Errors = new Exception[windowCount]
        };
        CountdownEvent ready = new CountdownEvent(windowCount);
        CountdownEvent completed = new CountdownEvent(windowCount);
        CancellationTokenSource cancellation = new CancellationTokenSource();
        ManualResetEventSlim start = new ManualResetEventSlim(false);
        Thread[] workers = new Thread[windowCount];
        Stopwatch totalStopwatch = new Stopwatch();

        for (int windowIndex = 0; windowIndex < windowCount; windowIndex++)
        {
            int capturedIndex = windowIndex;
            workers[windowIndex] = new Thread(() =>
            {
                List<long> conversionTicks = new List<long>();
                Mat source = null;
                try
                {
                    try
                    {
                        source = new Mat(
                            height,
                            width,
                            MatType.CV_8UC3,
                            new Scalar(16 + capturedIndex, 64 + capturedIndex, 128 + capturedIndex));
                    }
                    finally
                    {
                        ready.Signal();
                    }

                    start.Wait(cancellation.Token);
                    for (int frameIndex = 0; frameIndex < framesPerWindow; frameIndex++)
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        result.AttemptedPerWindow[capturedIndex]++;
                        object[] gateArguments = { windowNames[capturedIndex], 0 };
                        bool allowed = (bool)tryAcquire.Invoke(null, gateArguments);
                        if (allowed)
                        {
                            Bitmap bitmap = null;
                            try
                            {
                                Stopwatch conversionStopwatch = Stopwatch.StartNew();
                                bitmap = BitmapConverter.ToBitmap(source);
                                conversionStopwatch.Stop();
                                conversionTicks.Add(conversionStopwatch.ElapsedTicks);
                                result.AllowedPerWindow[capturedIndex]++;
                                Bitmap publisherBitmap = bitmap;
                                bitmap = null;
                                bool claimed = (bool)publish.Invoke(
                                    null,
                                    new object[] { null, windowNames[capturedIndex], publisherBitmap, null, null });
                                if (claimed)
                                    result.ClaimedPerWindow[capturedIndex]++;
                            }
                            finally
                            {
                                if (bitmap != null)
                                    bitmap.Dispose();
                            }
                        }

                        if (intervalMilliseconds > 0 &&
                            cancellation.Token.WaitHandle.WaitOne(intervalMilliseconds))
                        {
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (TargetInvocationException ex)
                {
                    result.Errors[capturedIndex] = ex.InnerException ?? ex;
                }
                catch (Exception ex)
                {
                    result.Errors[capturedIndex] = ex;
                }
                finally
                {
                    if (source != null)
                        source.Dispose();
                    PopulateConversionMetrics(result, capturedIndex, conversionTicks);
                    completed.Signal();
                }
            });
            workers[windowIndex].IsBackground = true;
            try
            {
                workers[windowIndex].Start();
            }
            catch (Exception ex)
            {
                result.Errors[windowIndex] = ex;
                workers[windowIndex] = null;
                ready.Signal();
                completed.Signal();
            }
        }

        if (!ready.Wait(10000))
        {
            cancellation.Cancel();
            start.Set();
            if (!completed.Wait(30000))
            {
                Environment.FailFast("Four image workers did not stop after the synchronized-start timeout.");
            }
            for (int index = 0; index < workers.Length; index++)
            {
                if (workers[index] != null)
                    workers[index].Join();
            }
            ready.Dispose();
            cancellation.Dispose();
            start.Dispose();
            completed.Dispose();
            throw new TimeoutException("The four image workers did not reach the synchronized start within ten seconds.");
        }
        ready.Dispose();
        totalStopwatch.Start();
        start.Set();
        return new FourWindowDisplayStressSession(
            result,
            completed,
            cancellation,
            start,
            workers,
            totalStopwatch);
    }

    private static void PopulateConversionMetrics(
        FourWindowDisplayStressResult result,
        int windowIndex,
        List<long> conversionTicks)
    {
        if (conversionTicks.Count == 0)
            return;

        conversionTicks.Sort();
        long totalTicks = 0;
        for (int index = 0; index < conversionTicks.Count; index++)
            totalTicks += conversionTicks[index];
        int p95Index = Math.Min(
            conversionTicks.Count - 1,
            Math.Max(0, (int)Math.Ceiling(conversionTicks.Count * 0.95D) - 1));
        int p99Index = Math.Min(
            conversionTicks.Count - 1,
            Math.Max(0, (int)Math.Ceiling(conversionTicks.Count * 0.99D) - 1));
        double millisecondsPerTick = 1000D / Stopwatch.Frequency;
        result.AverageConversionMilliseconds[windowIndex] =
            totalTicks * millisecondsPerTick / conversionTicks.Count;
        result.P95ConversionMilliseconds[windowIndex] =
            conversionTicks[p95Index] * millisecondsPerTick;
        result.P99ConversionMilliseconds[windowIndex] =
            conversionTicks[p99Index] * millisecondsPerTick;
        result.MaximumConversionMilliseconds[windowIndex] =
            conversionTicks[conversionTicks.Count - 1] * millisecondsPerTick;
    }
}

public static class FourWindowDisplayNativeMethods
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll")]
    public static extern int GetGuiResources(IntPtr processHandle, int flag);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    public static long GetAvailablePhysicalMemoryBytes()
    {
        MemoryStatusEx status = new MemoryStatusEx();
        status.Length = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
        return GlobalMemoryStatusEx(ref status) ? (long)status.AvailablePhysical : -1L;
    }
}
'@
$references = @(
    'mscorlib',
    'System',
    'System.Core',
    'System.Drawing',
    (Join-Path $debugDirectory 'OpenCvSharp.dll'),
    (Join-Path $debugDirectory 'OpenCvSharp.Extensions.dll')
)
Add-Type -TypeDefinition $stressHelper -Language CSharp -ReferencedAssemblies $references

$width = 1024
$height = 768
$framesPerWindow = 30
$intervalMilliseconds = 40
$roundCount = 3
$minimumStartAvailableMemoryMb = 512
$criticalAvailableMemoryMb = 256
$windowNames = [string[]]@('ImageWindow1', 'ImageWindow2', 'ImageWindow3', 'ImageWindow4')
$windows = New-Object System.Collections.Generic.List[object]
$roundSummaries = New-Object System.Collections.Generic.List[string]
$process = [Diagnostics.Process]::GetCurrentProcess()
$forcedRefreshCycles = 0

$warmupWindow = [Activator]::CreateInstance($windowType, @('ImageWindow1'))
$warmupWindow.ShowInTaskbar = $false
$warmupWindow.StartPosition = [Windows.Forms.FormStartPosition]::Manual
$warmupWindow.Location = New-Object Drawing.Point -10000, -10000
$warmupWindow.CreateControl()
$warmupWindowHandle = $warmupWindow.Handle
[void][FourWindowDisplayNativeMethods]::ShowWindow($warmupWindowHandle, 4)
[Windows.Forms.Application]::DoEvents()
$warmupClaimed = [FourWindowDisplayStressRunner]::WarmUpPublish(
    $publishMethod,
    'ImageWindow1',
    $width,
    $height)
Assert-True $warmupClaimed 'The full-size warm-up frame must be claimed by the warm-up window.'
[Windows.Forms.Application]::DoEvents()
$warmupWindow.Refresh()
$warmupWindow.Dispose()
[Windows.Forms.Application]::DoEvents()
Invoke-FullCollection
$process.Refresh()
$privateBefore = $process.PrivateMemorySize64
$managedBefore = [GC]::GetTotalMemory($false)
$gdiBefore = [FourWindowDisplayNativeMethods]::GetGuiResources($process.Handle, 0)
$availableBefore = [FourWindowDisplayNativeMethods]::GetAvailablePhysicalMemoryBytes()
Assert-True ($availableBefore -lt 0 -or $availableBefore -ge ($minimumStartAvailableMemoryMb * 1MB)) "Available physical memory is below the safe start threshold of ${minimumStartAvailableMemoryMb}MB."
$script:peakPrivateBytes = $privateBefore
$script:peakManagedBytes = $managedBefore
$script:peakGdiCount = $gdiBefore
$script:minimumAvailableBytes = $availableBefore

try {
    for ($windowIndex = 0; $windowIndex -lt $windowNames.Length; $windowIndex++) {
        $window = [Activator]::CreateInstance($windowType, @($windowNames[$windowIndex]))
        $window.ShowInTaskbar = $false
        $window.StartPosition = [Windows.Forms.FormStartPosition]::Manual
        $window.Location = New-Object Drawing.Point (-10000 + ($windowIndex * 20)), (-10000 + ($windowIndex * 20))
        $window.CreateControl()
        $windowHandle = $window.Handle
        [void][FourWindowDisplayNativeMethods]::ShowWindow($windowHandle, 4)
        $windows.Add($window)
    }
    [Windows.Forms.Application]::DoEvents()

    for ($round = 1; $round -le $roundCount; $round++) {
        $limiter = $limiterProperty.GetValue($null, $null)
        $limiter.ResetAll()
        $session = [FourWindowDisplayStressRunner]::Start(
            $tryAcquireMethod,
            $publishMethod,
            $windowNames,
            $framesPerWindow,
            $width,
            $height,
            $intervalMilliseconds)
        try {
            $deadline = [DateTime]::UtcNow.AddSeconds(15)
            $paintStopwatch = [Diagnostics.Stopwatch]::StartNew()
            $sampleStopwatch = [Diagnostics.Stopwatch]::StartNew()
            $memoryAbort = $false
            while (-not $session.IsCompleted -and [DateTime]::UtcNow -lt $deadline) {
                [Windows.Forms.Application]::DoEvents()
                if ($paintStopwatch.ElapsedMilliseconds -ge 16) {
                    foreach ($window in $windows) {
                        $window.Refresh()
                    }
                    $forcedRefreshCycles++
                    $paintStopwatch.Restart()
                }
                if ($sampleStopwatch.ElapsedMilliseconds -ge 25) {
                    $process.Refresh()
                    $currentPrivate = $process.PrivateMemorySize64
                    $currentManaged = [GC]::GetTotalMemory($false)
                    $currentGdi = [FourWindowDisplayNativeMethods]::GetGuiResources($process.Handle, 0)
                    $currentAvailable = [FourWindowDisplayNativeMethods]::GetAvailablePhysicalMemoryBytes()
                    if ($currentPrivate -gt $script:peakPrivateBytes) { $script:peakPrivateBytes = $currentPrivate }
                    if ($currentManaged -gt $script:peakManagedBytes) { $script:peakManagedBytes = $currentManaged }
                    if ($currentGdi -gt $script:peakGdiCount) { $script:peakGdiCount = $currentGdi }
                    if ($currentAvailable -ge 0 -and ($script:minimumAvailableBytes -lt 0 -or $currentAvailable -lt $script:minimumAvailableBytes)) {
                        $script:minimumAvailableBytes = $currentAvailable
                    }
                    if ($currentAvailable -ge 0 -and $currentAvailable -lt ($criticalAvailableMemoryMb * 1MB)) {
                        $memoryAbort = $true
                        $session.Cancel()
                    }
                    $sampleStopwatch.Restart()
                }
                Start-Sleep -Milliseconds 1
            }
            if (-not $session.IsCompleted) {
                $stopped = $session.CancelAndWait(30000)
                if (-not $stopped) {
                    [Environment]::FailFast("Four-window workers did not stop within 30 seconds after cancellation.")
                }
                throw "Round $round did not finish within 15 seconds."
            }
            $result = $session.Wait()
            Assert-True (-not $memoryAbort) "Round $round stopped because available physical memory fell below ${criticalAvailableMemoryMb}MB."
        }
        finally {
            $session.Dispose()
        }

        $drainStopwatch = [Diagnostics.Stopwatch]::StartNew()
        do {
            [Windows.Forms.Application]::DoEvents()
            $hasPendingWork = $false
            foreach ($window in $windows) {
                if ($null -ne $pendingFrameField.GetValue($window) -or
                    [int]$dispatchField.GetValue($window) -ne 0) {
                    $hasPendingWork = $true
                    break
                }
            }
            if ($hasPendingWork) {
                Start-Sleep -Milliseconds 1
            }
        } while ($hasPendingWork -and $drainStopwatch.ElapsedMilliseconds -lt 5000)
        $drainStopwatch.Stop()
        Assert-True (-not $hasPendingWork) "Round $round did not drain all four UI windows within five seconds."

        for ($windowIndex = 0; $windowIndex -lt $windowNames.Length; $windowIndex++) {
            Assert-True ($null -eq $result.Errors[$windowIndex]) ("Round $round window $($windowIndex + 1) failed: " + $result.Errors[$windowIndex])
            Assert-True ($result.AttemptedPerWindow[$windowIndex] -eq $framesPerWindow) "Round $round did not attempt every frame."
            Assert-True ($result.AllowedPerWindow[$windowIndex] -ge ($framesPerWindow - 1)) "Round $round rate-limited too many scheduled frames."
            Assert-True ($result.ClaimedPerWindow[$windowIndex] -eq $result.AllowedPerWindow[$windowIndex]) "Round $round lost a converted frame before window ownership transfer."
            Assert-True ($result.P99ConversionMilliseconds[$windowIndex] -lt 500) "Round $round observed a Mat-to-Bitmap P99 conversion time of at least 500ms."
            Assert-True ($result.MaximumConversionMilliseconds[$windowIndex] -lt 3000) "Round $round observed a Mat-to-Bitmap conversion stall of at least three seconds."
        }
        Assert-True ($result.ElapsedMilliseconds -lt 10000) "Round $round exceeded the ten-second four-window time budget."

        $process.Refresh()
        $tailPrivate = $process.PrivateMemorySize64
        $tailManaged = [GC]::GetTotalMemory($false)
        $tailGdi = [FourWindowDisplayNativeMethods]::GetGuiResources($process.Handle, 0)
        $tailAvailable = [FourWindowDisplayNativeMethods]::GetAvailablePhysicalMemoryBytes()
        if ($tailPrivate -gt $script:peakPrivateBytes) { $script:peakPrivateBytes = $tailPrivate }
        if ($tailManaged -gt $script:peakManagedBytes) { $script:peakManagedBytes = $tailManaged }
        if ($tailGdi -gt $script:peakGdiCount) { $script:peakGdiCount = $tailGdi }
        if ($tailAvailable -ge 0 -and ($script:minimumAvailableBytes -lt 0 -or $tailAvailable -lt $script:minimumAvailableBytes)) {
            $script:minimumAvailableBytes = $tailAvailable
        }
        Assert-True ($tailAvailable -lt 0 -or $tailAvailable -ge ($criticalAvailableMemoryMb * 1MB)) "Round $round ended below the critical available-memory threshold of ${criticalAvailableMemoryMb}MB."
        Invoke-FullCollection
        $roundSummaries.Add(
            "round=$round cadencedWall=$($result.ElapsedMilliseconds)ms drain=$($drainStopwatch.ElapsedMilliseconds)ms " +
            "allowed=$($result.AllowedPerWindow -join ',') claimed=$($result.ClaimedPerWindow -join ',') " +
            "avgMs=$(([string[]]($result.AverageConversionMilliseconds | ForEach-Object { $_.ToString('F2') })) -join ',') " +
            "p95Ms=$(([string[]]($result.P95ConversionMilliseconds | ForEach-Object { $_.ToString('F2') })) -join ',') " +
            "p99Ms=$(([string[]]($result.P99ConversionMilliseconds | ForEach-Object { $_.ToString('F2') })) -join ',') " +
            "maxMs=$(([string[]]($result.MaximumConversionMilliseconds | ForEach-Object { $_.ToString('F2') })) -join ',')")
    }
}
finally {
    foreach ($window in $windows) {
        $window.Dispose()
    }
    [Windows.Forms.Application]::DoEvents()
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($assemblyResolver)
}

Invoke-FullCollection
$process.Refresh()
$privateAfter = $process.PrivateMemorySize64
$managedAfter = [GC]::GetTotalMemory($false)
$gdiAfter = [FourWindowDisplayNativeMethods]::GetGuiResources($process.Handle, 0)
$privateGrowthMb = [Math]::Round(($privateAfter - $privateBefore) / 1MB, 2)
$managedGrowthMb = [Math]::Round(($managedAfter - $managedBefore) / 1MB, 2)
$peakPrivateGrowthMb = [Math]::Round(($script:peakPrivateBytes - $privateBefore) / 1MB, 2)
$peakManagedGrowthMb = [Math]::Round(($script:peakManagedBytes - $managedBefore) / 1MB, 2)
$peakGdiGrowth = $script:peakGdiCount - $gdiBefore
$minimumAvailableMb = if ($script:minimumAvailableBytes -lt 0) { -1 } else { [Math]::Round($script:minimumAvailableBytes / 1MB, 2) }
$gdiGrowth = $gdiAfter - $gdiBefore

Assert-True ($privateGrowthMb -lt 256) "Private memory growth exceeded 256MB: $privateGrowthMb MB."
Assert-True ($managedGrowthMb -lt 64) "Managed memory growth exceeded 64MB: $managedGrowthMb MB."
Assert-True ($gdiGrowth -le 10) "Four-window stress leaked GDI resources: growth=$gdiGrowth."

Write-Host ("四窗口组件级Mat转Bitmap发布/刷新压力通过；该证据不包含完整节点执行链和生产CPU调度链。" +
    ($roundSummaries -join ' | ') +
    "; image=${width}x${height}; frames/window/round=$framesPerWindow; " +
    "cadence=${intervalMilliseconds}ms; forcedRefreshCycles=$forcedRefreshCycles; " +
    "privateGrowth=${privateGrowthMb}MB; sampledPeakPrivateGrowth=${peakPrivateGrowthMb}MB; " +
    "managedGrowth=${managedGrowthMb}MB; sampledPeakManagedGrowth=${peakManagedGrowthMb}MB; " +
    "minimumAvailable=${minimumAvailableMb}MB; GDI growth=$gdiGrowth; sampledPeakGdiGrowth=$peakGdiGrowth.")
