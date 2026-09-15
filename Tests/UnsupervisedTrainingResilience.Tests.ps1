$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$preparerPath = Join-Path $root "Forms\AiTrainForm\UnsupervisedNativeDatasetPreparer.cs"
$servicePath = Join-Path $root "Forms\AiTrainForm\UnsupervisedTrainingService.cs"
$bootstrapperPath = Join-Path $root "Forms\AiTrainForm\UnsupervisedRuntimeBootstrapper.cs"
$resultRoot = Join-Path $root "Tests\Results"
$tempRoot = Join-Path $resultRoot ("UnsupervisedTrainingResilience-" + [Guid]::NewGuid().ToString("N"))

function Assert-Equal {
    param($Expected, $Actual, [string]$Message)
    if ($Expected -ne $Actual) {
        throw "$Message Expected=$Expected Actual=$Actual"
    }
}

function Assert-Contains {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if ($Text -notmatch [regex]::Escape($Pattern)) {
        throw $Message
    }
}

try {
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
    $sourceImage = Join-Path $tempRoot "single-ng.bmp"
    [System.IO.File]::WriteAllBytes($sourceImage, [byte[]](0..31))

    $preparerSource = Get-Content -LiteralPath $preparerPath -Encoding UTF8 -Raw
    $preparerSource = $preparerSource.Replace("nameof(ngPath)", '"ngPath"')
    Add-Type -TypeDefinition $preparerSource -Language CSharp

    $addedCount = [TDJS_Vision.Forms.AiTrainForm.UnsupervisedNativeDatasetPreparer]::EnsureMinimumNgSplitFiles($tempRoot, 1)
    Assert-Equal 1 $addedCount "Single-NG preparation must add one internal split copy."
    Assert-Equal 2 (Get-ChildItem -LiteralPath $tempRoot -File).Count "Native NG directory must contain two files after preparation."

    $secondAddedCount = [TDJS_Vision.Forms.AiTrainForm.UnsupervisedNativeDatasetPreparer]::EnsureMinimumNgSplitFiles($tempRoot, 1)
    Assert-Equal 0 $secondAddedCount "Native NG preparation must be idempotent."

    $service = Get-Content -LiteralPath $servicePath -Encoding UTF8 -Raw
    $bootstrapper = Get-Content -LiteralPath $bootstrapperPath -Encoding UTF8 -Raw
    Assert-Contains $service "NativeTrainingAttemptCount = 2" "Native -2 recovery must retry once with a fresh handle."
    Assert-Contains $service "PrepareCleanDirectory(modelOutputRoot" "Native retry must remove partial model output."
    Assert-Contains $service "return request.Threshold;" "Unsupervised calibration failures must fall back to the configured default threshold."
    Assert-Contains $service "catch (Exception ex)" "Calibration fallback must catch non-cancellation failures."
    Assert-Contains $bootstrapper "train_export.py" "Runtime preflight must validate the training script."
    Assert-Contains $bootstrapper "cpuPythonPath" "Runtime preflight must validate Python."
    Assert-Contains $bootstrapper "pretrained" "Runtime preflight must validate local pretrained assets."
}
finally {
    $resolvedResultRoot = [System.IO.Path]::GetFullPath($resultRoot).TrimEnd('\') + '\'
    $resolvedTempRoot = [System.IO.Path]::GetFullPath($tempRoot)
    if ($resolvedTempRoot.StartsWith($resolvedResultRoot, [System.StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $resolvedTempRoot)) {
        Remove-Item -LiteralPath $resolvedTempRoot -Recurse -Force
    }
}

Write-Host "Unsupervised training resilience checks passed."
