$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sourcePaths = @(
    (Join-Path $projectRoot 'Startup\IDeviceLicenseSynchronizer.cs'),
    (Join-Path $projectRoot 'Startup\DeviceLicenseSynchronizationResult.cs'),
    (Join-Path $projectRoot 'Startup\DeviceLicenseSynchronizer.cs')
)

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Expected,
        [string]$Message
    )

    Assert-True ($Text.Contains($Expected)) $Message
}

function Assert-NotContains {
    param(
        [string]$Text,
        [string]$Unexpected,
        [string]$Message
    )

    Assert-True (-not $Text.Contains($Unexpected)) $Message
}

foreach ($sourcePath in $sourcePaths) {
    Assert-True (Test-Path -LiteralPath $sourcePath -PathType Leaf) "Missing device license synchronization source: $sourcePath"
}

$compiledTypes = @(Add-Type -Path $sourcePaths -PassThru -WarningAction SilentlyContinue)
$synchronizerType = $compiledTypes | Where-Object { $_.FullName -eq 'TDJS_Vision.Startup.DeviceLicenseSynchronizer' } | Select-Object -First 1
Assert-True ($null -ne $synchronizerType) 'DeviceLicenseSynchronizer type was not found.'

$synchronizer = [Activator]::CreateInstance($synchronizerType, $true)
$synchronizeMethod = $synchronizerType.GetMethod('Synchronize')
Assert-True ($null -ne $synchronizeMethod) 'DeviceLicenseSynchronizer must expose Synchronize.'

function Invoke-LicenseSynchronization {
    param([string]$ApplicationDirectory)

    return @($synchronizeMethod.Invoke($synchronizer, @($ApplicationDirectory)))
}

function Get-ResultByTargetSuffix {
    param(
        [object[]]$Results,
        [string]$TargetSuffix
    )

    return $Results | Where-Object { $_.TargetPath.EndsWith($TargetSuffix, [StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
}

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ("TDJS-Vision-DeviceLicenseSync-" + [Guid]::NewGuid().ToString('N'))
$applicationDirectory = Join-Path $testRoot 'Application'
$missingSourceDirectory = Join-Path $testRoot 'MissingSource'

try {
    [IO.Directory]::CreateDirectory($applicationDirectory) | Out-Null
    [IO.Directory]::CreateDirectory($missingSourceDirectory) | Out-Null

    $sourceLicensePath = Join-Path $applicationDirectory 'device.license'
    $expectedBytes = [byte[]](1, 3, 5, 7, 9, 11, 13)
    [IO.File]::WriteAllBytes($sourceLicensePath, $expectedBytes)

    $largeModelDirectory = Join-Path $applicationDirectory 'LargeModelDll'
    $unsupervisedDirectory = Join-Path $applicationDirectory 'UnsupervisedDll'
    $yoloGpuDirectory = Join-Path $applicationDirectory 'YoloGPUDll'
    $yolo1050Directory = Join-Path $yoloGpuDirectory '1050tidll'
    $yolo750Directory = Join-Path $yoloGpuDirectory '750dll'
    foreach ($directory in @($largeModelDirectory, $unsupervisedDirectory, $yoloGpuDirectory, $yolo1050Directory)) {
        [IO.Directory]::CreateDirectory($directory) | Out-Null
    }

    $largeModelLicensePath = Join-Path $largeModelDirectory 'device.license'
    $unsupervisedLicensePath = Join-Path $unsupervisedDirectory 'device.license'
    $yoloGpuLicensePath = Join-Path $yoloGpuDirectory 'device.license'
    $yolo1050LicensePath = Join-Path $yolo1050Directory 'device.license'

    [IO.File]::WriteAllBytes($unsupervisedLicensePath, [byte[]](2, 4, 6))
    [IO.File]::WriteAllBytes($yoloGpuLicensePath, $expectedBytes)
    $unchangedWriteTimeUtc = [DateTime]::UtcNow.AddHours(-2)
    [IO.File]::SetLastWriteTimeUtc($yoloGpuLicensePath, $unchangedWriteTimeUtc)

    $lockedLicenseBytes = [byte[]](21, 22, 23)
    [IO.File]::WriteAllBytes($yolo1050LicensePath, $lockedLicenseBytes)
    $lockedLicenseStream = [IO.File]::Open($yolo1050LicensePath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        $results = @(Invoke-LicenseSynchronization $applicationDirectory)
    }
    finally {
        $lockedLicenseStream.Dispose()
    }
    Assert-True ($results.Count -eq 5) "Expected five target results, actual: $($results.Count)."
    Assert-True ([Convert]::ToBase64String([IO.File]::ReadAllBytes($largeModelLicensePath)) -eq [Convert]::ToBase64String($expectedBytes)) 'Large model license was not copied from the application root.'
    Assert-True ([Convert]::ToBase64String([IO.File]::ReadAllBytes($unsupervisedLicensePath)) -eq [Convert]::ToBase64String($expectedBytes)) 'Stale unsupervised license was not overwritten.'
    Assert-True ([IO.File]::GetLastWriteTimeUtc($yoloGpuLicensePath) -eq $unchangedWriteTimeUtc) 'An unchanged license must not be rewritten.'
    Assert-True ([Convert]::ToBase64String([IO.File]::ReadAllBytes($yolo1050LicensePath)) -eq [Convert]::ToBase64String($lockedLicenseBytes)) 'A failed replacement must preserve the previous valid license.'
    Assert-True (@(Get-ChildItem -LiteralPath $yolo1050Directory -Filter 'device.license.*.tmp' -File).Count -eq 0) 'A failed replacement must clean its temporary license file.'
    Assert-True (-not (Test-Path -LiteralPath $yolo750Directory)) 'The synchronizer must not create a missing runtime directory.'

    $largeModelResult = Get-ResultByTargetSuffix $results 'LargeModelDll\device.license'
    $unsupervisedResult = Get-ResultByTargetSuffix $results 'UnsupervisedDll\device.license'
    $yoloGpuResult = Get-ResultByTargetSuffix $results 'YoloGPUDll\device.license'
    $yolo1050Result = Get-ResultByTargetSuffix $results 'YoloGPUDll\1050tidll\device.license'
    $yolo750Result = Get-ResultByTargetSuffix $results 'YoloGPUDll\750dll\device.license'

    Assert-True ([int]$largeModelResult.Status -eq 0) 'A missing target license must return status 0 (copied).'
    Assert-True ([int]$unsupervisedResult.Status -eq 0) 'A stale target license must return status 0 (copied).'
    Assert-True ([int]$yoloGpuResult.Status -eq 1) 'An identical target license must return status 1 (unchanged).'
    Assert-True ([int]$yolo1050Result.Status -eq 3) 'A target copy exception must return status 3 (failed).'
    Assert-True ([int]$yolo750Result.Status -eq 2) 'A missing target directory must return status 2 (skipped).'

    $missingSourceResults = @(Invoke-LicenseSynchronization $missingSourceDirectory)
    Assert-True ($missingSourceResults.Count -eq 1) 'A missing source license must return exactly one failure result.'
    Assert-True ([int]$missingSourceResults[0].Status -eq 3) 'A missing source license must return status 3 (failed).'
    Assert-True ($missingSourceResults[0].Message.Contains('device.license')) 'The missing source message must identify device.license.'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}

$programPath = Join-Path $projectRoot 'Program.cs'
$projectPath = Join-Path $projectRoot 'TDJS-Vision.csproj'
$synchronizerPath = Join-Path $projectRoot 'Startup\DeviceLicenseSynchronizer.cs'
$programSource = Get-Content -LiteralPath $programPath -Encoding UTF8 -Raw
$projectSource = Get-Content -LiteralPath $projectPath -Encoding UTF8 -Raw
$synchronizerSource = Get-Content -LiteralPath $synchronizerPath -Encoding UTF8 -Raw

Assert-Contains $projectSource '<Compile Include="Startup\IDeviceLicenseSynchronizer.cs" />' 'The project must compile IDeviceLicenseSynchronizer.cs.'
Assert-Contains $projectSource '<Compile Include="Startup\DeviceLicenseSynchronizationResult.cs" />' 'The project must compile DeviceLicenseSynchronizationResult.cs.'
Assert-Contains $projectSource '<Compile Include="Startup\DeviceLicenseSynchronizer.cs" />' 'The project must compile DeviceLicenseSynchronizer.cs.'
Assert-Contains $programSource 'private static readonly IDeviceLicenseSynchronizer DeviceLicenseSynchronizer = new DeviceLicenseSynchronizer();' 'Program must compose the license synchronizer through its interface.'
Assert-Contains $programSource 'private static void SynchronizeDeviceLicense()' 'Program must isolate startup license synchronization in one method.'
Assert-Contains $programSource 'DeviceLicenseSynchronizationStatus.Failed' 'Program must distinguish failed synchronization results.'
Assert-Contains $programSource 'MsgLevel.Warn' 'Failed synchronization results must use warning logs.'
Assert-Contains $programSource 'MsgLevel.Exception' 'Unexpected synchronization exceptions must use exception logs.'
Assert-Contains $synchronizerSource 'File.Replace(temporaryPath, targetPath, null)' 'An existing license must be replaced atomically.'
Assert-Contains $synchronizerSource 'File.Move(temporaryPath, targetPath)' 'A new license must be moved atomically from the target directory.'
Assert-NotContains $synchronizerSource 'File.Copy(sourcePath, targetPath, true)' 'The synchronizer must not overwrite the active license in place.'

$runApplicationIndex = $programSource.IndexOf('private static void RunApplication(string[] args)')
$synchronizationCallIndex = $programSource.IndexOf('SynchronizeDeviceLicense();', $runApplicationIndex)
$startupContextIndex = $programSource.IndexOf('new StartupApplicationContext(solutionPath)', $runApplicationIndex)
Assert-True ($synchronizationCallIndex -gt $runApplicationIndex) 'RunApplication must invoke license synchronization.'
Assert-True ($synchronizationCallIndex -lt $startupContextIndex) 'License synchronization must run before StartupApplicationContext is created.'

$synchronizationMethodIndex = $programSource.IndexOf('private static void SynchronizeDeviceLicense()')
$nextMethodIndex = $programSource.IndexOf('private static void ConfigureThreadPoolMinimums()', $synchronizationMethodIndex)
$synchronizationMethodSource = $programSource.Substring($synchronizationMethodIndex, $nextMethodIndex - $synchronizationMethodIndex)
Assert-True (-not $synchronizationMethodSource.Contains('MessageBoxTD.Show')) 'License synchronization must not display a message box.'
Assert-True (-not $synchronizationMethodSource.Contains('return;')) 'License synchronization failure must not terminate the startup flow.'

Write-Host 'Device license startup synchronization behavior checks passed.'
