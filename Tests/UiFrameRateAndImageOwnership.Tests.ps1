$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Equal {
    param($Actual, $Expected, [string]$Message)
    if ($Actual -ne $Expected) {
        throw "$Message Actual=$Actual, Expected=$Expected."
    }
}

function Assert-ContainsText {
    param([string]$Content, [string]$Expected, [string]$Message)
    if (-not $Content.Contains($Expected)) {
        throw $Message
    }
}

function Assert-NotContainsText {
    param([string]$Content, [string]$Expected, [string]$Message)
    if ($Content.Contains($Expected)) {
        throw $Message
    }
}

function Test-BitmapDisposed {
    param([Drawing.Bitmap]$Bitmap)

    try {
        $handle = $Bitmap.GetHbitmap()
        [UiFrameRateNativeMethods]::DeleteObject($handle) | Out-Null
        return $false
    }
    catch {
        return $true
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$limiterSource = Get-Content -LiteralPath (Join-Path $projectRoot 'ResourceManagement\PerWindowUiFrameRateLimiter.cs') -Raw -Encoding UTF8
$interfacesSource = Get-Content -LiteralPath (Join-Path $projectRoot 'ResourceManagement\IResourceProfileServices.cs') -Raw -Encoding UTF8
$modelsSource = Get-Content -LiteralPath (Join-Path $projectRoot 'ResourceManagement\ResourceProfileModels.cs') -Raw -Encoding UTF8
$calculatorSource = Get-Content -LiteralPath (Join-Path $projectRoot 'ResourceManagement\ResourceProfileCalculator.cs') -Raw -Encoding UTF8
$imageShowSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Node\1-Acquisition\ImageShow\NodeImageShow.cs') -Raw -Encoding UTF8
$imageShow3DSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Node\1-Acquisition\ImageShow3D\NodeImageShow3D.cs') -Raw -Encoding UTF8
$singleImageSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Forms\ImageViewer\FrmSingleImage.cs') -Raw -Encoding UTF8
$showImageControlSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Forms\DispShowImage\ShowImageControl.cs') -Raw -Encoding UTF8
$projectSource = Get-Content -LiteralPath (Join-Path $projectRoot 'TDJS-Vision.csproj') -Raw -Encoding UTF8

Assert-ContainsText $interfacesSource 'interface IUiFrameRateLimiter' 'UI frame-rate control must remain replaceable.'
Assert-ContainsText $limiterSource 'ConcurrentDictionary<string, WindowRateState>' 'Every image window must own an independent rate state.'
Assert-ContainsText $limiterSource 'lock (state.SyncRoot)' 'Only callers targeting the same window may contend on a rate-state lock.'
Assert-ContainsText $modelsSource 'public int UiMaxRefreshFps { get; set; } = PerWindowUiFrameRateLimiter.DefaultMaximumFramesPerSecond;' 'The future UI-adjustable FPS parameter must default to 30.'
Assert-ContainsText $calculatorSource 'UiMaxRefreshFps = Clamp(' 'The resource profile must clamp the FPS parameter.'
Assert-ContainsText $projectSource '<Compile Include="ResourceManagement\PerWindowUiFrameRateLimiter.cs" />' 'The production project must compile the frame-rate limiter.'

$twoDimensionalGate = $imageShowSource.IndexOf('bool refreshGranted = TryAcquireWindowRefresh(', [StringComparison]::Ordinal)
$twoDimensionalPermit = $imageShowSource.IndexOf('CpuWorkloadKind.ImageConversion', [StringComparison]::Ordinal)
$twoDimensionalConvert = $imageShowSource.IndexOf('image = firstMat.ToBitmap();', [StringComparison]::Ordinal)
Assert-True ($twoDimensionalGate -ge 0 -and $twoDimensionalGate -lt $twoDimensionalPermit -and $twoDimensionalPermit -lt $twoDimensionalConvert) '二维显示节点必须先执行帧率门控，再取得CPU转换许可，最后执行Mat转Bitmap。'
$threeDimensionalGate = $imageShow3DSource.IndexOf('bool refreshGranted = NodeImageShow.TryAcquireWindowRefresh(', [StringComparison]::Ordinal)
$threeDimensionalPermit = $imageShow3DSource.IndexOf('CpuWorkloadKind.ImageConversion', [StringComparison]::Ordinal)
$threeDimensionalConvert = $imageShow3DSource.IndexOf('image = firstMat.ToBitmap();', [StringComparison]::Ordinal)
Assert-True ($threeDimensionalGate -ge 0 -and $threeDimensionalGate -lt $threeDimensionalPermit -and $threeDimensionalPermit -lt $threeDimensionalConvert) '三维显示节点必须先执行帧率门控，再取得CPU转换许可，最后执行Mat转Bitmap。'
Assert-ContainsText $imageShowSource 'eventArgs?.DisposeUnclaimedBitmap();' 'The publisher must release a Bitmap that no window claims.'
Assert-ContainsText $imageShowSource 'return eventArgs.IsBitmapClaimed;' 'The publisher must report whether a target window claimed ownership.'
Assert-ContainsText $singleImageSource 'if (!e.TryTakeBitmap(out Bitmap bitmap))' 'Only the matching window may claim a published Bitmap.'
Assert-ContainsText $singleImageSource 'e.ConfirmBitmapOwnershipTransfer(bitmap);' 'The matching window must confirm ownership only after accepting the Bitmap.'
Assert-NotContainsText $singleImageSource 'SetViewerImage(e.Bitmap' 'The UI must not read an unclaimed public Bitmap field.'
Assert-ContainsText $imageShowSource 'if (eventArgs.HasUnconfirmedBitmapClaim)' 'A subscriber exception after claim must trigger publisher cleanup.'
Assert-ContainsText $showImageControlSource 'public bool TrySetImage(Bitmap bmp, AlgorithmResult displayResult)' 'The child display control must explicitly report whether it accepted Bitmap ownership.'
Assert-ContainsText $singleImageSource 'if (!showImageControl1.TrySetImage(bitmap, frame.DisplayResult))' 'The latest-frame slot must release a Bitmap rejected before the child handle exists.'

$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$projectXml = [xml](Get-Content -LiteralPath (Join-Path $projectRoot 'TDJS-Vision.csproj') -Raw -Encoding UTF8)
$assemblyName = [string](
    $projectXml.Project.PropertyGroup |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.AssemblyName) } |
        Select-Object -First 1
).AssemblyName
$application = Get-Item -LiteralPath (Join-Path $debugDirectory ($assemblyName + '.exe')) -ErrorAction SilentlyContinue
Assert-True ($null -ne $application) 'The Debug application is required for UI frame-rate runtime checks.'

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

Add-Type -TypeDefinition @'
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
public static class UiFrameRateNativeMethods
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr handle);
}

public sealed class UiFrameRateStressResult
{
    public int AcceptedCount { get; set; }
    public int[] AcceptedPerWindow { get; set; }
    public long ElapsedMilliseconds { get; set; }
}

public static class UiFrameRateStressRunner
{
    public static UiFrameRateStressResult RunConcurrent(Func<string, int, bool> tryAcquire, int callsPerWindow)
    {
        string[] windows = { "ImageWindow1", "ImageWindow2", "ImageWindow3", "ImageWindow4" };
        int[] acceptedPerWindow = new int[windows.Length];
        Thread[] workers = new Thread[windows.Length];
        using (CountdownEvent ready = new CountdownEvent(windows.Length))
        using (ManualResetEventSlim start = new ManualResetEventSlim(false))
        {
            for (int windowIndex = 0; windowIndex < windows.Length; windowIndex++)
            {
                int capturedIndex = windowIndex;
                workers[windowIndex] = new Thread(() =>
                {
                    ready.Signal();
                    start.Wait();
                    int accepted = 0;
                    for (int index = 0; index < callsPerWindow; index++)
                    {
                        if (tryAcquire(windows[capturedIndex], 30))
                            accepted++;
                    }
                    acceptedPerWindow[capturedIndex] = accepted;
                });
                workers[windowIndex].IsBackground = true;
                workers[windowIndex].Start();
            }

            ready.Wait();
            Stopwatch stopwatch = Stopwatch.StartNew();
            start.Set();
            for (int index = 0; index < workers.Length; index++)
                workers[index].Join();
            stopwatch.Stop();

            int totalAccepted = 0;
            for (int index = 0; index < acceptedPerWindow.Length; index++)
                totalAccepted += acceptedPerWindow[index];

            return new UiFrameRateStressResult
            {
                AcceptedCount = totalAccepted,
                AcceptedPerWindow = acceptedPerWindow,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }
    }
}
'@

$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$limiterType = $assembly.GetType('TDJS_Vision.ResourceManagement.PerWindowUiFrameRateLimiter', $true)
$script:manualTimestamp = 0L
$timestampProvider = [Func[long]] { return [long]$script:manualTimestamp }
$limiter = [Activator]::CreateInstance($limiterType, @($timestampProvider, [long]1000))

foreach ($windowName in @('ImageWindow1', 'ImageWindow2', 'ImageWindow3', 'ImageWindow4')) {
    Assert-True ($limiter.TryAcquire($windowName, 30)) "The first frame for $windowName must pass independently."
    Assert-True (-not $limiter.TryAcquire($windowName, 30)) "An immediate second frame for $windowName must be dropped."
}

$script:manualTimestamp = 33L
Assert-True (-not $limiter.TryAcquire('ImageWindow1', 30)) 'Thirty FPS must wait for the full 34-tick ceiling interval.'
$script:manualTimestamp = 34L
Assert-True ($limiter.TryAcquire('ImageWindow1', 30)) 'Thirty FPS must permit the frame at the computed interval.'
Assert-True ($limiter.TryAcquire('ImageWindow2', 30)) 'Another window must independently permit its frame at the computed interval.'
$limiter.Reset('ImageWindow1')
Assert-True ($limiter.TryAcquire('ImageWindow1', 30)) 'Resetting one window must make its next frame immediately eligible.'
Assert-True (-not $limiter.TryAcquire('ImageWindow2', 30)) 'Resetting one window must not reset another window.'

$limiter.ResetAll()
$script:manualTimestamp = 0L
$tryAcquireMethod = $limiterType.GetMethod('TryAcquire', [Reflection.BindingFlags]'Instance,Public')
$productionLimiter = [Activator]::CreateInstance($limiterType)
$tryAcquireDelegate = [Delegate]::CreateDelegate([Func[string, int, bool]], $productionLimiter, $tryAcquireMethod)
$stressResult = [UiFrameRateStressRunner]::RunConcurrent($tryAcquireDelegate, 25000)
Assert-True ($stressResult.AcceptedCount -ge 4 -and $stressResult.AcceptedCount -lt 1000) 'The production clock stress run must remain rate-limited per window.'
Assert-True ($stressResult.ElapsedMilliseconds -lt 2000) 'The synchronized four-thread limiter stress run must finish within two seconds.'
foreach ($acceptedForWindow in $stressResult.AcceptedPerWindow) {
    Assert-True ($acceptedForWindow -ge 1) 'Every concurrently started window must independently obtain a frame permit.'
}

$imageShowParamType = $assembly.GetType('TDJS_Vision.Forms.ImageViewer.ImageShowPamra', $true)
$imageShowParamConstructor = $imageShowParamType.GetConstructors() | Select-Object -First 1
$unclaimedBitmap = (New-Object Drawing.Bitmap 64, 64).PSObject.BaseObject
$unclaimedArgs = $imageShowParamConstructor.Invoke([object[]]@('ImageWindow1', $unclaimedBitmap, $null, $null))
$unclaimedArgs.DisposeUnclaimedBitmap()
Assert-True (Test-BitmapDisposed $unclaimedBitmap) 'An unclaimed event Bitmap must be released.'

$claimedBitmap = (New-Object Drawing.Bitmap 64, 64).PSObject.BaseObject
$claimedArgs = $imageShowParamConstructor.Invoke([object[]]@('ImageWindow1', $claimedBitmap, $null, $null))
$takenBitmap = $null
$unusedBitmap = $null
Assert-True ($claimedArgs.TryTakeBitmap([ref]$takenBitmap)) 'The target window must claim a frame exactly once.'
Assert-True (-not $claimedArgs.TryTakeBitmap([ref]$unusedBitmap)) 'A second window must not claim the same Bitmap.'
Assert-True ($claimedArgs.ConfirmBitmapOwnershipTransfer($takenBitmap)) 'The target window must confirm the ownership transfer.'
$claimedArgs.DisposeUnclaimedBitmap()
$claimedHandle = $takenBitmap.GetHbitmap()
[UiFrameRateNativeMethods]::DeleteObject($claimedHandle) | Out-Null
$takenBitmap.Dispose()

$nodeImageShowType = $assembly.GetType('TDJS_Vision.Node._1_Acquisition.ImageSource.NodeImageShow', $true)
$publishMethod = $nodeImageShowType.GetMethod('PublishImageShowChanged', [Reflection.BindingFlags]'Public,Static')
$throwingHandler = [System.EventHandler[TDJS_Vision.Forms.ImageViewer.ImageShowPamra]] {
    param($sender, $eventArgs)
    $claimedBeforeFailure = $null
    [void]$eventArgs.TryTakeBitmap([ref]$claimedBeforeFailure)
    throw 'Expected subscriber failure after claiming the Bitmap.'
}
$imageShowEvent = $nodeImageShowType.GetEvent('ImageShowChanged', [Reflection.BindingFlags]'Public,Static')
$subscriberFailureBitmap = (New-Object Drawing.Bitmap 64, 64).PSObject.BaseObject
$imageShowEvent.AddEventHandler($null, $throwingHandler)
try {
    $subscriberFailureClaimed = $publishMethod.Invoke($null, @($null, 'ImageWindow98', $subscriberFailureBitmap, $null, $null))
}
finally {
    $imageShowEvent.RemoveEventHandler($null, $throwingHandler)
}
Assert-Equal $subscriberFailureClaimed $false 'A subscriber that throws before confirming transfer must not report ownership.'
Assert-True (Test-BitmapDisposed $subscriberFailureBitmap) 'A Bitmap claimed by a failing subscriber must be released by the publisher.'

$publisherBitmap = (New-Object Drawing.Bitmap 64, 64).PSObject.BaseObject
$publisherClaimed = $publishMethod.Invoke($null, @($null, 'ImageWindow99', $publisherBitmap, $null, $null))
Assert-Equal $publisherClaimed $false 'A publisher without a matching window must report an unclaimed frame.'
Assert-True (Test-BitmapDisposed $publisherBitmap) 'A publisher without subscribers must release its Bitmap.'

$windowType = $assembly.GetType('TDJS_Vision.Forms.ImageViewer.FrmSingleImage', $true)
$window = [Activator]::CreateInstance($windowType, @('ImageWindow3'))
$window.CreateControl()
$windowHandle = $window.Handle
Assert-True ($window.IsHandleCreated -and $windowHandle -ne [IntPtr]::Zero) 'The ownership integration window must create its UI handle.'
$claimedPublisherBitmap = (New-Object Drawing.Bitmap 64, 64).PSObject.BaseObject
$matchingWindowClaimed = $publishMethod.Invoke($null, @($null, 'ImageWindow3', $claimedPublisherBitmap, $null, $null))
Assert-Equal $matchingWindowClaimed $true 'A matching image window must report that it claimed the frame.'
Assert-True (Test-BitmapDisposed $claimedPublisherBitmap) 'A claimed frame rejected before the child display handle exists must be released immediately.'
$window.Dispose()

$shownWindow = [Activator]::CreateInstance($windowType, @('ImageWindow4'))
$shownWindow.ShowInTaskbar = $false
$shownWindow.StartPosition = [Windows.Forms.FormStartPosition]::Manual
$shownWindow.Location = New-Object Drawing.Point -10000, -10000
$shownWindow.CreateControl()
$shownWindowHandle = $shownWindow.Handle
[void][UiFrameRateNativeMethods]::ShowWindow($shownWindowHandle, 4)
[Windows.Forms.Application]::DoEvents()
$displayedBitmap = (New-Object Drawing.Bitmap 64, 64).PSObject.BaseObject
$shownWindowClaimed = $publishMethod.Invoke($null, @($null, 'ImageWindow4', $displayedBitmap, $null, $null))
Assert-Equal $shownWindowClaimed $true 'A shown image window must claim its matching frame.'
$displayedHandle = $displayedBitmap.GetHbitmap()
[UiFrameRateNativeMethods]::DeleteObject($displayedHandle) | Out-Null
$shownWindow.Dispose()
Assert-True (Test-BitmapDisposed $displayedBitmap) 'Disposing a shown target window must release its displayed Bitmap.'

[AppDomain]::CurrentDomain.remove_AssemblyResolve($assemblyResolver)
Write-Host "UI frame-rate and ownership checks passed. Synchronized four-thread burst=100000/$($stressResult.ElapsedMilliseconds)ms, accepted=$($stressResult.AcceptedCount), per-window=$($stressResult.AcceptedPerWindow -join ','), unclaimed, failed-subscriber and claimed Bitmap lifecycles verified."
