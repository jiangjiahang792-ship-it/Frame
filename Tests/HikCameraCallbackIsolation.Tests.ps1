$ErrorActionPreference = 'Stop'

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-MethodSlice {
    param(
        [string]$Source,
        [string]$StartMarker,
        [string]$EndMarker
    )

    $start = $Source.IndexOf($StartMarker, [System.StringComparison]::Ordinal)
    $end = $Source.IndexOf($EndMarker, $start + $StartMarker.Length, [System.StringComparison]::Ordinal)
    Assert-True ($start -ge 0 -and $end -gt $start) "Cannot locate method slice: $StartMarker"
    return $Source.Substring($start, $end - $start)
}

$sourcePath = Join-Path $PSScriptRoot '..\Device\Camera\CameraHik.cs'
$source = Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8
$callback = Get-MethodSlice $source 'private void EnqueueRawFrameCore' 'private void EnsureRawFrameBuffers'
$nativeCallback = Get-MethodSlice $source 'private void GetImageCallBack' 'private void EnqueueRawFrameCore'

Assert-True ($callback.Contains('CopyMemory(packet.Buffer, sourceData, frameInfo.nFrameLen);')) 'The SDK callback must copy source bytes before returning.'
Assert-True ($nativeCallback.IndexOf('Stopwatch.GetTimestamp()', [System.StringComparison]::Ordinal) -lt $nativeCallback.IndexOf('lock (_sdkCallbackLifetimeLock)', [System.StringComparison]::Ordinal)) 'The callback identity timestamp must be captured before every lock.'
Assert-True ($source.Contains('EntryPoint = "RtlMoveMemory"')) 'The native copy import must use a real kernel32 export.'
Assert-True ($callback.Contains('_pendingRawFrames.Enqueue(packet);')) 'The SDK callback must enqueue raw frames for the worker.'
Assert-True (-not $callback.Contains('ConvertFrameToIndependentMat(')) 'Pixel conversion and Mat creation are forbidden in the SDK callback.'
Assert-True (-not $callback.Contains('RaiseMatImageReceived(')) 'Subscriber dispatch is forbidden in the SDK callback.'
Assert-True (-not $callback.Contains('PerformanceSpikeDiagnostics.Log')) 'Per-frame diagnostic logging is forbidden in the SDK callback.'
Assert-True (-not $callback.Contains('LogHelper.AddLog')) 'Callback exceptions must be reported by the worker.'
Assert-True ($source.Contains('Priority = ThreadPriority.Normal')) 'Camera ordering workers must use normal priority on multi-camera systems.'
Assert-True (-not $source.Contains('Priority = ThreadPriority.AboveNormal')) 'Per-camera workers must not multiply above-normal CPU competition.'
Assert-True (-not $source.Contains('Solution.Instance.AcquireCpuWorkAsync(')) 'Camera conversion must be independent of the shared CPU scheduler.'
Assert-True (-not $source.Contains('CpuWorkloadKind.ImageConversion')) 'Both callback and active camera conversion must bypass global conversion permits.'
Assert-True ($source.Contains('_frameConversionCancellation')) 'Stopping a camera must still cancel its own queued conversion work.'
Assert-True ($source.Contains('CPU许可等待=')) 'Diagnostics must retain the previous CPU-wait field for comparisons.'
Assert-True ($source.Contains('转换通道=每相机独立；共享CPU许可=不参与')) 'Diagnostics must identify the independent camera conversion policy.'
Assert-True ($source.Contains('packet.DiagnosticEnabled, cancellationToken)')) 'Production conversion must check cancellation under the per-camera conversion lock.'
Assert-True ($source.Contains('ThrowIfCalledFromFrameConversionWorker')) 'Camera lifecycle operations must reject conversion-thread self-wait.'
Assert-True ($source.Contains('ThrowIfCalledFromFrameConversionWorker("开始相机取流")')) 'Starting capture from the conversion callback must be rejected.'
Assert-True ($source.Contains('ThrowIfCalledFromFrameConversionWorker("主动取图")')) 'Active capture from the conversion callback must be rejected.'
Assert-True ($source.Contains('ThrowIfCalledFromFrameConversionWorker("创建设备句柄")')) 'Device recreation from the conversion callback must be rejected.'
Assert-True ($source.Contains('ThrowIfCalledFromFrameConversionWorker("打开相机")')) 'Opening from the conversion callback must be rejected.'
Assert-True ($source.Contains('MV_CC_GetImageBuffer_NET(ref frame, timeout)')) 'Active capture must retain the SDK frame acquisition timeout.'
Assert-True ($source.Contains('!sdkWasGrabbing && !HasFrameConversionWorker()')) 'A second stop call must retry cleanup after a previous worker-stop timeout.'
Assert-True ($source.Contains('lock (_cameraLifecycleLock)')) 'Start, stop, active capture, and close must share a lifecycle boundary.'
Assert-True ($source.Contains('_rawFrameAvailable.WaitOne();')) 'Idle camera workers must sleep until signaled instead of polling every 20ms.'
Assert-True (-not $source.Contains('_rawFrameAvailable.WaitOne(20)')) 'Per-camera polling must not multiply with camera count.'
Assert-True ($source.Contains('GetIntValue("PayloadSize")')) 'Raw buffers must be allocated from the SDK payload size before grabbing.'
Assert-True ($source.Contains('RawFrameBufferCountMinimum = 2')) 'Each camera must retain at least two raw buffers for callback isolation.'
Assert-True ($source.Contains('RawFrameBufferCountMaximum = 8')) 'Per-camera raw buffers must have a fixed upper bound.'
Assert-True ($source.Contains('RawFrameMemoryBudgetRatio = 0.25D')) 'All cameras must share a bounded raw-frame memory budget.'
Assert-True ($source.Contains('CalculateRawFrameBufferCount(requiredSize)')) 'Raw buffer count must be calculated from payload, memory, and configured camera count.'
Assert-True ($source.IndexOf('PrepareFrameConversionWorkerForStart();', [System.StringComparison]::Ordinal) -lt $source.IndexOf('EnsureRawFrameBuffers(payloadSize);', [System.StringComparison]::Ordinal)) 'Exited workers must be cleaned before the new raw-frame lease is allocated.'
Assert-True ($source.Contains('cancellation.Cancel();')) 'Worker-start failure must cancel the unpublished conversion token.'
Assert-True ($source.Contains('ReleaseRawFrameBuffersCore();')) 'Worker-start failure must release raw buffers and the global memory lease.'
Assert-True ($source.Contains('CameraRawFrameMemoryBudgetManager.Reserve(')) 'Raw buffers must atomically reserve the solution-wide memory budget.'
Assert-True ($source.Contains('_rawFrameMemoryLease')) 'Each camera must retain its global memory lease until raw buffers are released.'
Assert-True ($source.Contains('memoryLease?.Dispose();')) 'Stopping a camera must return its raw-frame memory reservation.'
Assert-True ($source.Contains('ThrowIfDisposed();')) 'Public camera operations must reject reuse after permanent disposal.'
Assert-True ($source.Contains('Volatile.Write(ref _disposed, 1)')) 'Successful native destruction must permanently mark the camera disposed.'
Assert-True ($source.Contains('throw CreateSdkException("销毁相机句柄", result)')) 'Native destroy failure must propagate to solution reset instead of losing the handle.'
Assert-True ($source.Contains('SDKFrameNum=')) 'Multi-camera diagnostics must preserve the Hik SDK frame number.'
Assert-True ($source.Contains('lock (_sdkCallbackLifetimeLock)')) 'Camera stop must synchronize with callbacks already in progress.'
Assert-True ($source.Contains('StopFrameConversionWorker();')) 'Camera stop must wait for the conversion worker.'
Assert-True ($source.Contains('ReleaseRawFrameBuffersCore();')) 'Camera close must release unmanaged raw-frame buffers.'

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class NativeMethodsForHikCallbackIsolation
{
    [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory", SetLastError = false)]
    public static extern void CopyMemory(IntPtr destination, IntPtr source, uint count);
}
'@

$sourceBuffer = [System.Runtime.InteropServices.Marshal]::AllocHGlobal(4)
$destinationBuffer = [System.Runtime.InteropServices.Marshal]::AllocHGlobal(4)
try {
    [System.Runtime.InteropServices.Marshal]::WriteInt32($sourceBuffer, 123456789)
    [NativeMethodsForHikCallbackIsolation]::CopyMemory($destinationBuffer, $sourceBuffer, 4)
    Assert-True ([System.Runtime.InteropServices.Marshal]::ReadInt32($destinationBuffer) -eq 123456789) 'RtlMoveMemory runtime copy verification failed.'
}
finally {
    [System.Runtime.InteropServices.Marshal]::FreeHGlobal($sourceBuffer)
    [System.Runtime.InteropServices.Marshal]::FreeHGlobal($destinationBuffer)
}

Write-Host 'Hik camera callback isolation checks passed.'
