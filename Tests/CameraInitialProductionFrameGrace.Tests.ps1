param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$outputDirectory = Join-Path $ProjectRoot "bin\x64\$Configuration"
$assemblyPath = Get-ChildItem -LiteralPath $outputDirectory -Filter '*.exe' |
    Where-Object { $_.Name -notmatch 'vshost' } |
    Select-Object -First 1 -ExpandProperty FullName
if ([string]::IsNullOrWhiteSpace($assemblyPath)) {
    throw "Test assembly was not found in: $outputDirectory"
}

$assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
$cameraType = $assembly.GetType('TDJS_Vision.Device.Camera.CameraHik', $true)
$nodeType = $assembly.GetType('TDJS_Vision.Node._1_Acquisition.ImageSource.NodeImageSource', $true)
$camera = [Activator]::CreateInstance($cameraType)
$nativeOpenedField = $cameraType.GetField('_nativeDeviceOpened', [Reflection.BindingFlags]'Instance,NonPublic')
$graceField = $cameraType.GetField('_initialProductionFrameGraceReserved', [Reflection.BindingFlags]'Instance,NonPublic')
$reserveMethod = $cameraType.GetMethod('TryReserveInitialProductionFrameGrace', [Reflection.BindingFlags]'Instance,Public')
$timeoutMethod = $nodeType.GetMethod('AddInitialProductionFrameStartupGrace', [Reflection.BindingFlags]'Static,NonPublic')
if ($null -eq $nativeOpenedField -or $null -eq $graceField -or $null -eq $reserveMethod -or $null -eq $timeoutMethod) {
    throw 'Initial frame startup grace members are incomplete.'
}

# Do not connect to the real SDK; only set the atomic state behind IsOpen.
$nativeOpenedField.SetValue($camera, 1)
$probeTypeName = 'CameraInitialFrameGraceConcurrencyProbe'
if ($null -eq ($probeTypeName -as [type])) {
    Add-Type -TypeDefinition @"
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

public static class CameraInitialFrameGraceConcurrencyProbe
{
    public static int Race(object camera, MethodInfo reserveMethod, int contenderCount)
    {
        int winnerCount = 0;
        Parallel.For(0, contenderCount, index =>
        {
            if ((bool)reserveMethod.Invoke(camera, null))
                Interlocked.Increment(ref winnerCount);
        });
        return winnerCount;
    }
}
"@
}

$winnerCount = [CameraInitialFrameGraceConcurrencyProbe]::Race($camera, $reserveMethod, 128)
if ($winnerCount -ne 1) {
    throw "Exactly one concurrent caller must reserve startup grace. Actual=$winnerCount."
}
if ([bool]$reserveMethod.Invoke($camera, $null)) {
    throw 'Startup grace must not be reserved twice in one connection session.'
}

# Simulate the reset performed after a successful reconnect.
$graceField.SetValue($camera, 0)
if (-not [bool]$reserveMethod.Invoke($camera, $null)) {
    throw 'A reconnected camera must receive a new startup grace reservation.'
}
if ([bool]$reserveMethod.Invoke($camera, $null)) {
    throw 'Reconnect startup grace must still be reserved only once.'
}

$firstTimeout = [int]$timeoutMethod.Invoke($null, @([int]100, [bool]$true))
$steadyTimeout = [int]$timeoutMethod.Invoke($null, @([int]100, [bool]$false))
$overflowTimeout = [int]$timeoutMethod.Invoke($null, @([int][int]::MaxValue, [bool]$true))
if ($firstTimeout -ne 600) {
    throw "A 100ms initial timeout plus grace must equal 600ms. Actual=$firstTimeout."
}
if ($steadyTimeout -ne 100) {
    throw "Steady-state timeout must remain 100ms. Actual=$steadyTimeout."
}
if ($overflowTimeout -ne [int]::MaxValue) {
    throw "Startup grace addition must prevent integer overflow. Actual=$overflowTimeout."
}

Write-Output 'Camera initial production frame grace checks passed: one winner across 128 contenders; 100ms becomes 600ms only for the initial frame.'
