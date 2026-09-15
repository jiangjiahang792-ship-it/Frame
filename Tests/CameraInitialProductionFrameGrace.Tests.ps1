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
            object reservation = reserveMethod.Invoke(camera, null);
            if (reservation == null)
                return;
            try
            {
                reservation.GetType().GetMethod("Commit").Invoke(reservation, null);
                Interlocked.Increment(ref winnerCount);
            }
            finally
            {
                ((IDisposable)reservation).Dispose();
            }
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
$unexpectedReservation = $reserveMethod.Invoke($camera, $null)
if ($null -ne $unexpectedReservation) {
    ([IDisposable]$unexpectedReservation).Dispose()
    throw 'Startup grace must not be reserved twice in one connection session.'
}

# A failed ticket registration disposes without Commit and must return the reservation.
$graceField.SetValue($camera, 0)
$rollbackReservation = $reserveMethod.Invoke($camera, $null)
if ($null -eq $rollbackReservation) {
    throw 'A fresh session must allow a provisional reservation.'
}
([IDisposable]$rollbackReservation).Dispose()
$reservationAfterRollback = $reserveMethod.Invoke($camera, $null)
if ($null -eq $reservationAfterRollback) {
    throw 'Disposal without Commit must return startup grace to the next ticket.'
}
$reservationAfterRollback.GetType().GetMethod('Commit').Invoke($reservationAfterRollback, $null)
([IDisposable]$reservationAfterRollback).Dispose()

# Simulate the reset performed only after a successful reconnect.
$graceField.SetValue($camera, 0)
$reconnectReservation = $reserveMethod.Invoke($camera, $null)
if ($null -eq $reconnectReservation) {
    throw 'A reconnected camera must receive a new startup grace reservation.'
}
$reconnectReservation.GetType().GetMethod('Commit').Invoke($reconnectReservation, $null)
([IDisposable]$reconnectReservation).Dispose()
$secondReconnectReservation = $reserveMethod.Invoke($camera, $null)
if ($null -ne $secondReconnectReservation) {
    ([IDisposable]$secondReconnectReservation).Dispose()
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
