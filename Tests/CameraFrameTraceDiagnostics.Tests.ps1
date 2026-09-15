$ErrorActionPreference = 'Stop'

function Get-ProjectSource {
    param([string]$RelativePath)

    $sourcePath = Join-Path $PSScriptRoot "..\$RelativePath"
    return Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8
}

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if (-not $Text.Contains($Pattern)) {
        throw $Message
    }
}

function Assert-NotContains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if ($Text.Contains($Pattern)) {
        throw $Message
    }
}

function Assert-Order {
    param(
        [string]$Text,
        [string]$First,
        [string]$Second,
        [string]$Message
    )

    $firstIndex = $Text.IndexOf($First)
    $secondIndex = $Text.IndexOf($Second)
    if ($firstIndex -lt 0 -or $secondIndex -lt 0 -or $firstIndex -ge $secondIndex) {
        throw $Message
    }
}

$registry = Get-ProjectSource 'Diagnostics\CameraFrameTraceRegistry.cs'
$camera = Get-ProjectSource 'Device\Camera\CameraHik.cs'
$imageSource = Get-ProjectSource 'Node\1-Acquisition\ImageSource\NodeImageSource.cs'
$project = Get-ProjectSource 'TDJS-Vision.csproj'
$callbackStart = $camera.IndexOf('private void GetImageCallBack')
$callbackEnd = $camera.IndexOf('private void EnqueueRawFrameCore', $callbackStart)
if ($callbackStart -lt 0 -or $callbackEnd -le $callbackStart) {
    throw 'Unable to locate the camera callback method for log-level validation.'
}
$cameraCallback = $camera.Substring($callbackStart, $callbackEnd - $callbackStart)

Assert-Contains $registry 'ConditionalWeakTable<Mat, CameraFrameTraceInfo>' 'The frame registry must use weak Mat keys.'
Assert-Contains $registry 'TraceTable.Remove(image);' 'Replacing metadata must not leave a stale registry value.'
Assert-Contains $registry 'public static void Attach(' 'The registry must attach metadata to each Mat wrapper.'
Assert-Contains $registry 'public static bool TryGet(' 'Process nodes must be able to read frame metadata.'

Assert-Contains $camera 'Interlocked.Increment(ref _callbackFrameSequence)' 'Each SDK callback must receive a thread-safe FrameId.'
Assert-Contains $camera 'ConvertFrameToIndependentMat(packet.Buffer, ref frameInfo, packet.FrameId, packet.DiagnosticEnabled, cancellationToken)' 'Mat conversion must use the same FrameId and camera cancellation token.'
Assert-Contains $camera 'private void RaiseMatImageReceived(Mat image, CameraFrameTraceInfo frameTrace)' 'Mat distribution must receive frame metadata.'
Assert-Contains $camera 'CameraFrameTraceRegistry.Attach(subscriberImage, frameTrace);' 'Every subscriber Mat wrapper must receive frame metadata.'
Assert-Order $camera 'CameraFrameTraceRegistry.Attach(subscriberImage, frameTrace);' '((Action<Mat>)invocationList[index]).Invoke(subscriberImage);' 'Metadata must be attached before subscriber invocation.'
Assert-Contains $camera 'PerformanceSpikeDiagnostics.FrameConvertSlowMs' 'Camera conversion must use its dedicated slow threshold.'
Assert-NotContains $cameraCallback 'LogHelper.AddLog(MsgLevel.Info' 'High-frequency camera callback timing must not use Info logs.'

Assert-Contains $imageSource 'CameraFrameTraceRegistry.TryGet(mat, out frameTrace);' 'The image source callback must read camera frame metadata.'
Assert-Contains $imageSource 'TraceId={Process?.CurrentPerformanceTraceId}' 'Image source diagnostics must include the process TraceId.'
Assert-Contains $imageSource 'RunId={Process?.CurrentRunId}' 'Image source diagnostics must include the current RunId.'
Assert-Contains $imageSource 'frameTrace.GetElapsedFromCallbackStartMilliseconds(currentTimestamp)' 'Diagnostics must measure SDK callback to the current process stage.'
Assert-Contains $imageSource 'long waitCompletedTimestamp = diagnosticEnabled ? Stopwatch.GetTimestamp() : 0L;' 'The camera wait completion timestamp must be captured only for Debug diagnostics.'
Assert-Order $imageSource '_cameraFrameAwaiter.BeginWaitAsync(token)' 'camera.GrabOne();' 'The wait must be armed before a software trigger on the captured camera.'
Assert-Order $imageSource '? registry.Register(' 'ticket.IssueSoftwareTrigger(() =>' 'Production ticket registration must precede the real software trigger.'

Assert-Contains $project '<Compile Include="Diagnostics\CameraFrameTraceRegistry.cs" />' 'The project must compile the camera frame registry.'

Write-Host 'Camera frame trace diagnostic regression checks passed.'
