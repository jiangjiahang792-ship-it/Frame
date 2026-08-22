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

Assert-True ($callback.Contains('CopyMemory(packet.Buffer, sourceData, frameInfo.nFrameLen);')) 'The SDK callback must copy source bytes before returning.'
Assert-True ($source.Contains('EntryPoint = "RtlMoveMemory"')) 'The native copy import must use a real kernel32 export.'
Assert-True ($callback.Contains('_pendingRawFrames.Enqueue(packet);')) 'The SDK callback must enqueue raw frames for the worker.'
Assert-True (-not $callback.Contains('ConvertFrameToIndependentMat(')) 'Pixel conversion and Mat creation are forbidden in the SDK callback.'
Assert-True (-not $callback.Contains('RaiseMatImageReceived(')) 'Subscriber dispatch is forbidden in the SDK callback.'
Assert-True (-not $callback.Contains('PerformanceSpikeDiagnostics.Log')) 'Per-frame diagnostic logging is forbidden in the SDK callback.'
Assert-True (-not $callback.Contains('LogHelper.AddLog')) 'Callback exceptions must be reported by the worker.'
Assert-True ($source.Contains('Priority = ThreadPriority.AboveNormal')) 'Each camera must own an above-normal conversion worker.'
Assert-True ($source.Contains('GetIntValue("PayloadSize")')) 'Raw buffers must be allocated from the SDK payload size before grabbing.'
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
