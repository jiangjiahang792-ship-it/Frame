$ErrorActionPreference = 'Stop'

function Get-ProjectSource {
    param(
        [string]$RelativePath
    )

    $sourcePath = Join-Path $PSScriptRoot "..\$RelativePath"
    return Get-Content -Path $sourcePath -Raw -Encoding UTF8
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

function Assert-LogHelperCallsDoNotBuildHeavyDiagnostics {
    param(
        [string]$RelativePath
    )

    $source = Get-ProjectSource $RelativePath
    $searchIndex = 0
    $heavyMarkers = @(
        'PerformanceSpikeDiagnostics.GetRuntimeText()',
        'PerformanceSpikeDiagnostics.GetExecutableIdentityText()',
        'PerformanceSpikeDiagnostics.GetCurrentProcessPathText()',
        'PerformanceSpikeDiagnostics.GetOutputImageText(',
        'ShowImageControl.SetImage',
        'ShowImageControl.SetDisplayResult'
    )

    while ($true) {
        $callStart = $source.IndexOf('LogHelper.AddLog(', $searchIndex)
        if ($callStart -lt 0) {
            break
        }

        $callEnd = $source.IndexOf(');', $callStart)
        if ($callEnd -lt 0) {
            throw "$RelativePath has an unterminated LogHelper.AddLog call."
        }

        $callText = $source.Substring($callStart, $callEnd - $callStart + 2)
        foreach ($marker in $heavyMarkers) {
            Assert-NotContains $callText $marker "$RelativePath must not build heavy diagnostic marker '$marker' before LogHelper.AddLog can filter the log level."
        }

        $searchIndex = $callEnd + 2
    }
}

function Assert-MethodBodyNotContains {
    param(
        [string]$Text,
        [string]$MethodStart,
        [string]$NextMarker,
        [string]$Pattern,
        [string]$Message
    )

    $start = $Text.IndexOf($MethodStart)
    if ($start -lt 0) {
        throw "Cannot find method marker '$MethodStart'."
    }

    $end = $Text.IndexOf($NextMarker, $start)
    if ($end -lt 0) {
        throw "Cannot find next marker '$NextMarker'."
    }

    $methodBody = $Text.Substring($start, $end - $start)
    Assert-NotContains $methodBody $Pattern $Message
}

function Assert-MethodBodyContains {
    param(
        [string]$Text,
        [string]$MethodStart,
        [string]$NextMarker,
        [string]$Pattern,
        [string]$Message
    )

    $start = $Text.IndexOf($MethodStart)
    if ($start -lt 0) {
        throw "Cannot find method marker '$MethodStart'."
    }

    $end = $Text.IndexOf($NextMarker, $start)
    if ($end -lt 0) {
        throw "Cannot find next marker '$NextMarker'."
    }

    $methodBody = $Text.Substring($start, $end - $start)
    Assert-Contains $methodBody $Pattern $Message
}

$diagnostics = Get-ProjectSource 'Diagnostics\PerformanceSpikeDiagnostics.cs'
$logHelper = Get-ProjectSource 'Forms\Logger\LogHelper.cs'
$cameraHik = Get-ProjectSource 'Device\Camera\CameraHik.cs'
$nodeImageSource = Get-ProjectSource 'Node\1-Acquisition\ImageSource\NodeImageSource.cs'
$process = Get-ProjectSource 'Process.cs'
$showImageControl = Get-ProjectSource 'Forms\DispShowImage\ShowImageControl.cs'
$frmSingleImage = Get-ProjectSource 'Forms\ImageViewer\FrmSingleImage.cs'

Assert-Contains $logHelper 'public static bool CanRecord(MsgLevel level)' 'LogHelper must expose one cheap recordability check that includes all log filters.'
Assert-Contains $logHelper 'OnlyLogException && (level == MsgLevel.Info || level == MsgLevel.Debug)' 'LogHelper.CanRecord must include the only-exception filter.'
Assert-Contains $diagnostics 'public static bool IsDiagnosticLogEnabled(MsgLevel level)' 'PerformanceSpikeDiagnostics must expose a cheap log-level guard for high-frequency diagnostics.'
Assert-Contains $diagnostics 'public static void LogIfEnabled(' 'PerformanceSpikeDiagnostics must expose a lazy diagnostic log helper.'
Assert-Contains $diagnostics 'Func<string> messageFactory' 'Diagnostic log helpers must accept Func<string> so heavy strings are built only after guards pass.'
Assert-Contains $diagnostics 'public static void LogSlowIfEnabled(' 'PerformanceSpikeDiagnostics must expose a threshold-gated lazy diagnostic log helper.'
Assert-Contains $diagnostics 'LogHelper.CanRecord(level)' 'Diagnostic log helpers must check the shared log filter before building messages.'

Assert-Contains $cameraHik 'PerformanceSpikeDiagnostics.LogSlowIfEnabled(' 'CameraHik callback conversion segment diagnostics must use the shared lazy slow-log helper.'
Assert-NotContains $cameraHik '回调Mat转换分段：ConvertPixelType={convertWatch.ElapsedMilliseconds}ms' 'CameraHik must not build callback conversion segment text for every frame before the diagnostic guard.'

Assert-Contains $nodeImageSource 'PerformanceSpikeDiagnostics.LogIfEnabled(' 'NodeImageSource high-frequency diagnostics must use the shared lazy helper.'
Assert-Contains $process 'PerformanceSpikeDiagnostics.LogIfEnabled(' 'Process skipped-node diagnostics must use the shared lazy helper.'
Assert-Contains $diagnostics 'if (!IsDiagnosticLogEnabled(MsgLevel.Debug))' 'Node memory diagnostics must skip resource sampling when Debug diagnostics are disabled.'
Assert-Contains $showImageControl 'PerformanceSpikeDiagnostics.LogIfEnabled(' 'ShowImageControl image display diagnostics must use the shared lazy helper.'
Assert-Contains $showImageControl 'PerformanceSpikeDiagnostics.LogSlowIfEnabled(' 'ShowImageControl slow image display diagnostics must use the shared lazy slow-log helper.'
Assert-Contains $frmSingleImage 'PerformanceSpikeDiagnostics.LogIfEnabled(' 'FrmSingleImage image display diagnostics must use the shared lazy helper.'
Assert-Contains $frmSingleImage 'PerformanceSpikeDiagnostics.LogSlowIfEnabled(' 'FrmSingleImage slow image display diagnostics must use the shared lazy slow-log helper.'
Assert-NotContains $frmSingleImage 'string bitmapInfo = GetBitmapDiagnosticText(bitmap);' 'FrmSingleImage must not build bitmap diagnostic text before the shared log guard.'
Assert-NotContains $frmSingleImage 'string displayInfo = GetDisplayResultDiagnosticText(displayResult);' 'FrmSingleImage must not build display-result diagnostic text before the shared log guard.'

Assert-LogHelperCallsDoNotBuildHeavyDiagnostics 'Device\Camera\CameraHik.cs'
Assert-LogHelperCallsDoNotBuildHeavyDiagnostics 'Node\1-Acquisition\ImageSource\NodeImageSource.cs'
Assert-LogHelperCallsDoNotBuildHeavyDiagnostics 'Process.cs'
Assert-LogHelperCallsDoNotBuildHeavyDiagnostics 'Forms\DispShowImage\ShowImageControl.cs'
Assert-LogHelperCallsDoNotBuildHeavyDiagnostics 'Forms\ImageViewer\FrmSingleImage.cs'
Assert-LogHelperCallsDoNotBuildHeavyDiagnostics 'Diagnostics\PerformanceSpikeDiagnostics.cs'
Assert-MethodBodyContains $diagnostics 'public static void LogNodeMemoryIfNeeded(' 'private static bool ShouldSampleNodeMemory()' 'LogIfEnabled(' 'Node memory diagnostics must use the shared lazy diagnostic entry.'
Assert-MethodBodyNotContains $diagnostics 'public static void LogNodeMemoryIfNeeded(' 'private static bool ShouldSampleNodeMemory()' 'LogHelper.AddLog(' 'Node memory diagnostics must not bypass the shared lazy diagnostic entry.'

Write-Host 'Diagnostic lazy evaluation regression checks passed.'
