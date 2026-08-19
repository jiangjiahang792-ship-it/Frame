$ErrorActionPreference = 'Stop'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
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

$projectRoot = Split-Path -Parent $PSScriptRoot
$pictureControlPath = Join-Path $projectRoot 'Forms\ImageViewer\YTPictrueBox.cs'
$pictureDesignerPath = Join-Path $projectRoot 'Forms\ImageViewer\YTPictrueBox.Designer.cs'
$matViewerPath = Join-Path $projectRoot 'Forms\ImageViewer\MatViewer.cs'
$singleImagePath = Join-Path $projectRoot 'Forms\ImageViewer\FrmSingleImage.cs'
$singleImageDesignerPath = Join-Path $projectRoot 'Forms\ImageViewer\FrmSingleImage.Designer.cs'

$pictureControlSource = Get-Content -LiteralPath $pictureControlPath -Raw -Encoding UTF8
$pictureDesignerSource = Get-Content -LiteralPath $pictureDesignerPath -Raw -Encoding UTF8
$matViewerSource = Get-Content -LiteralPath $matViewerPath -Raw -Encoding UTF8
$singleImageSource = Get-Content -LiteralPath $singleImagePath -Raw -Encoding UTF8
$singleImageDesignerSource = Get-Content -LiteralPath $singleImageDesignerPath -Raw -Encoding UTF8

Assert-ContainsText $pictureControlSource 'if (ReferenceEquals(previous, value))' 'The common picture control must not dispose an image assigned to itself.'
Assert-ContainsText $pictureControlSource 'previous?.Dispose();' 'The common picture control must dispose replaced images.'
Assert-ContainsText $pictureControlSource 'private void ReleaseImageResources()' 'The common picture control must release its final image.'
Assert-ContainsText $pictureControlSource 'Image = null;' 'Clear-image actions must use the common ownership path.'
Assert-ContainsText $pictureDesignerSource 'ReleaseImageResources();' 'The common picture-control Designer disposal must release its final image.'

Assert-ContainsText $matViewerSource 'using (var win = new MatViewer(name))' 'Modal Mat viewers must be disposed after closing.'
Assert-ContainsText $matViewerSource 'bitmap = mat == null ? null : BitmapConverter.ToBitmap(mat);' 'Mat conversion must complete before UI dispatch.'
Assert-ContainsText $matViewerSource 'Invoke(new Action<Bitmap>(SetViewerBitmap), bitmap);' 'Only the converted Bitmap should cross into the UI thread.'
Assert-NotContainsText $matViewerSource 'Invoke(new MethodInvoker(() => { ytPictrueBox1.Image = BitmapConverter.ToBitmap(mat); }))' 'Mat-to-Bitmap conversion must not run on the UI thread.'

Assert-ContainsText $singleImageSource 'private PendingViewerFrame _pendingViewerFrame;' 'Every process image window must expose one latest-frame slot.'
Assert-ContainsText $singleImageSource 'PendingViewerFrame superseded = Interlocked.Exchange(ref _pendingViewerFrame, frame);' 'A new process frame must replace the undisplayed old frame atomically.'
Assert-ContainsText $singleImageSource 'superseded.Dispose();' 'Superseded process frames must be released immediately.'
Assert-ContainsText $singleImageSource 'Interlocked.CompareExchange(ref _viewerDispatchScheduled, 1, 0)' 'Every image window must allow at most one queued UI refresh.'
Assert-ContainsText $singleImageSource 'private sealed class PendingViewerFrame : IDisposable' 'Bitmap, overlay data, and diagnostics must travel through the slot as one frame.'
Assert-ContainsText $singleImageSource 'frame.TransferBitmapOwnership();' 'Displayed Bitmaps must transfer from the slot to ShowImageControl.'
Assert-ContainsText $singleImageSource 'if (!showImageControl1.TrySetImage(bitmap, frame.DisplayResult))' 'The frame slot must retain ownership when the child display handle rejects an image.'
Assert-ContainsText $singleImageSource 'BitmapDiagnosticText = diagnosticEnabled ? GetBitmapDiagnosticText(bitmap) : string.Empty;' 'Debug frame diagnostics must be captured before a superseded Bitmap can be disposed.'
Assert-NotContainsText $singleImageSource 'GetBitmapDiagnosticText(submittedFrame.Bitmap)' 'Queued-dispatch diagnostics must not inspect a Bitmap that another producer may already have released.'
Assert-NotContainsText $singleImageSource 'GetBitmapDiagnosticText(superseded.Bitmap)' 'Superseded-frame diagnostics must use the immutable snapshot instead of the releasable Bitmap.'
Assert-ContainsText $singleImageSource 'GetTraceContextText(superseded.TraceContext)' 'Debug diagnostics must preserve the trace context of superseded frames.'
Assert-ContainsText $singleImageSource 'NodeImageShow.ImageShowChanged -= NodeImageShow_ImageShowChanged;' 'Disposed image windows must detach static frame events.'
Assert-ContainsText $singleImageSource 'private void ReleaseRuntimeResources()' 'Image-window disposal must drain the latest-frame slot.'
Assert-ContainsText $singleImageDesignerSource 'ReleaseRuntimeResources();' 'Image-window Designer disposal must drain the latest-frame slot.'

$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$projectXml = [xml](Get-Content -LiteralPath (Join-Path $projectRoot 'TDJS-Vision.csproj') -Raw -Encoding UTF8)
$assemblyName = [string](
    $projectXml.Project.PropertyGroup |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.AssemblyName) } |
        Select-Object -First 1
).AssemblyName
$application = Get-Item -LiteralPath (Join-Path $debugDirectory ($assemblyName + '.exe')) -ErrorAction SilentlyContinue
Assert-True ($null -ne $application) 'The Debug application is required for the common picture-control GDI stress check.'

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
$controlType = $assembly.GetType('TDJS_Vision.Forms.ImageViewer.YTPictrueBox', $true)
$control = [Activator]::CreateInstance($controlType)
$control.Width = 320
$control.Height = 240
$control.CreateControl()

$nativeMethods = @'
using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
public static class ImageViewerNativeMethods
{
    [DllImport("user32.dll")]
    public static extern int GetGuiResources(IntPtr processHandle, int flag);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr handle);
}

public static class LatestFrameProducer
{
    public static Exception Produce(object target, MethodInfo method, int count, int width, int height)
    {
        Exception error = null;
        Thread thread = new Thread(() =>
        {
            try
            {
                for (int index = 0; index < count; index++)
                    method.Invoke(target, new object[] { new Bitmap(width, height), null, null });
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.IsBackground = true;
        thread.Start();
        thread.Join();
        return error;
    }
}
'@
Add-Type -TypeDefinition $nativeMethods -Language CSharp -ReferencedAssemblies System.Drawing

$first = New-Object Drawing.Bitmap 320, 240
$second = New-Object Drawing.Bitmap 320, 240
$control.Image = $first
$control.Image = $second
$firstDisposed = $false
try {
    $unusedHandle = $first.GetHbitmap()
}
catch {
    $firstDisposed = $true
}
Assert-True $firstDisposed 'Replacing the common viewer image must dispose the previous Bitmap.'

$sameImageHandle = $second.GetHbitmap()
try {
    $control.Image = $second
    Assert-True ($second.Width -eq 320) 'Assigning the current image again must not dispose it.'
}
finally {
    [ImageViewerNativeMethods]::DeleteObject($sameImageHandle) | Out-Null
}

$process = [Diagnostics.Process]::GetCurrentProcess()
[GC]::Collect()
[GC]::WaitForPendingFinalizers()
$gdiBefore = [ImageViewerNativeMethods]::GetGuiResources($process.Handle, 0)

$commonReplacementStopwatch = [Diagnostics.Stopwatch]::StartNew()
for ($i = 0; $i -lt 500; $i++) {
    $bitmap = New-Object Drawing.Bitmap 320, 240
    $control.Image = $bitmap
}
$commonReplacementStopwatch.Stop()

[GC]::Collect()
[GC]::WaitForPendingFinalizers()
$gdiAfter = [ImageViewerNativeMethods]::GetGuiResources($process.Handle, 0)
$gdiGrowth = $gdiAfter - $gdiBefore
$control.Dispose()

Assert-True ($gdiGrowth -le 25) "Common picture-control replacement leaked GDI resources: growth=$gdiGrowth."

$windowType = $assembly.GetType('TDJS_Vision.Forms.ImageViewer.FrmSingleImage', $true)
$window = [Activator]::CreateInstance($windowType, @('ImageWindow1'))
$window.CreateControl()
$windowHandle = $window.Handle
Assert-True ($window.IsHandleCreated -and $windowHandle -ne [IntPtr]::Zero) 'The process image window must create its UI handle before background production.'
$setViewerImage = $windowType.GetMethod('SetViewerImage', [Reflection.BindingFlags]'Instance,NonPublic')
Assert-True ($null -ne $setViewerImage) 'The process image-window update entry is required for integrated replacement testing.'
$pendingFrameField = $windowType.GetField('_pendingViewerFrame', [Reflection.BindingFlags]'Instance,NonPublic')
$dispatchField = $windowType.GetField('_viewerDispatchScheduled', [Reflection.BindingFlags]'Instance,NonPublic')
Assert-True ($null -ne $pendingFrameField) 'The process image window must retain one inspectable latest-frame slot.'
Assert-True ($null -ne $dispatchField) 'The process image window must retain one inspectable UI dispatch flag.'

[GC]::Collect()
[GC]::WaitForPendingFinalizers()
$windowGdiBefore = [ImageViewerNativeMethods]::GetGuiResources($process.Handle, 0)
$windowProductionStopwatch = [Diagnostics.Stopwatch]::StartNew()
$producerError = [LatestFrameProducer]::Produce($window, $setViewerImage, 500, 320, 240)
$windowProductionStopwatch.Stop()
Assert-True ($null -eq $producerError) ('Background latest-frame production failed: ' + $producerError)
Assert-True ($null -ne $pendingFrameField.GetValue($window)) 'A blocked UI thread must retain exactly the latest pending frame.'
Assert-True ([int]$dispatchField.GetValue($window) -eq 1) 'A blocked UI thread must have exactly one queued refresh task.'

$uiDrainStopwatch = [Diagnostics.Stopwatch]::StartNew()
[Windows.Forms.Application]::DoEvents()
$uiDrainStopwatch.Stop()
Assert-True ($null -eq $pendingFrameField.GetValue($window)) 'UI processing must consume the latest pending frame.'
Assert-True ([int]$dispatchField.GetValue($window) -eq 0) 'UI processing must clear the queued refresh flag.'
[GC]::Collect()
[GC]::WaitForPendingFinalizers()
$windowGdiAfter = [ImageViewerNativeMethods]::GetGuiResources($process.Handle, 0)
$windowGdiGrowth = $windowGdiAfter - $windowGdiBefore
$window.Dispose()

Assert-True ($windowGdiGrowth -le 25) "Process image-window replacement leaked GDI resources: growth=$windowGdiGrowth."

$disposeWindow = [Activator]::CreateInstance($windowType, @('ImageWindow2'))
$disposeWindow.CreateControl()
$disposeWindowHandle = $disposeWindow.Handle
Assert-True ($disposeWindow.IsHandleCreated -and $disposeWindowHandle -ne [IntPtr]::Zero) 'The disposal-race window must create its UI handle.'
$disposeProducerError = [LatestFrameProducer]::Produce($disposeWindow, $setViewerImage, 500, 320, 240)
Assert-True ($null -eq $disposeProducerError) ('Background disposal-race production failed: ' + $disposeProducerError)
$frameBeforeDispose = $pendingFrameField.GetValue($disposeWindow)
Assert-True ($null -ne $frameBeforeDispose) 'The disposal-race window must own the latest pending frame before disposal.'
$frameBitmapProperty = $frameBeforeDispose.GetType().GetProperty('Bitmap', [Reflection.BindingFlags]'Instance,Public')
$bitmapBeforeDispose = $frameBitmapProperty.GetValue($frameBeforeDispose, $null)

$disposeStopwatch = [Diagnostics.Stopwatch]::StartNew()
$disposeWindow.Dispose()
$disposeStopwatch.Stop()
Assert-True ($null -eq $pendingFrameField.GetValue($disposeWindow)) 'Window disposal must drain the latest pending frame immediately.'
Assert-True ([int]$dispatchField.GetValue($disposeWindow) -eq 0) 'Window disposal must clear the queued-refresh flag.'
$pendingBitmapDisposed = $false
try {
    $unusedPendingHandle = $bitmapBeforeDispose.GetHbitmap()
}
catch {
    $pendingBitmapDisposed = $true
}
Assert-True $pendingBitmapDisposed 'Window disposal must release the Bitmap retained in the latest-frame slot.'
[AppDomain]::CurrentDomain.remove_AssemblyResolve($assemblyResolver)

Write-Host "Image viewer and latest-frame lifecycle checks passed. Common replacements=500/$($commonReplacementStopwatch.ElapsedMilliseconds)ms, window production=500/$($windowProductionStopwatch.ElapsedMilliseconds)ms, UI latest-frame drain=$($uiDrainStopwatch.ElapsedMilliseconds)ms, disposal drain=$($disposeStopwatch.ElapsedMilliseconds)ms, pending/dispatch peak=1/1, GDI growth=$gdiGrowth/$windowGdiGrowth."
