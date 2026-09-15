$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$csprojPath = Join-Path $root "TDJS-Vision.csproj"
$formPath = Join-Path $root "Forms\AiTrainForm\UnsupervisedTrainForm.cs"
$bootstrapperPath = Join-Path $root "Forms\AiTrainForm\UnsupervisedRuntimeBootstrapper.cs"
$detectorPath = Join-Path $root "Forms\AiTrainForm\UnsupervisedAnomalibDetector.cs"
$servicePath = Join-Path $root "Forms\AiTrainForm\UnsupervisedTrainingService.cs"

function Assert-FileExists {
    param([string]$Path, [string]$Message)
    if (-not (Test-Path -LiteralPath $Path)) {
        throw $Message
    }
}

function Assert-Contains {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if ($Text -notmatch [regex]::Escape($Pattern)) {
        throw $Message
    }
}

function Assert-NotContains {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if ($Text -match [regex]::Escape($Pattern)) {
        throw $Message
    }
}

Assert-FileExists $bootstrapperPath "Missing runtime bootstrapper."
Assert-FileExists $detectorPath "Missing native detector wrapper."
Assert-FileExists $servicePath "Missing training service."

$csproj = Get-Content -LiteralPath $csprojPath -Encoding UTF8 -Raw
$form = Get-Content -LiteralPath $formPath -Encoding UTF8 -Raw
$bootstrapper = Get-Content -LiteralPath $bootstrapperPath -Encoding UTF8 -Raw
$detector = Get-Content -LiteralPath $detectorPath -Encoding UTF8 -Raw
$service = Get-Content -LiteralPath $servicePath -Encoding UTF8 -Raw

Assert-Contains $bootstrapper 'Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UnsupervisedDll")' "Runtime root must be BaseDirectory UnsupervisedDll."
Assert-Contains $bootstrapper "AnomalibLib.dll" "Runtime check must include AnomalibLib.dll."
Assert-Contains $bootstrapper "device.license" "Runtime check must include license file."
Assert-Contains $bootstrapper "train_export.py" "Runtime check must include the Python training entry script."
Assert-Contains $bootstrapper "python_env" "Runtime check must include a Python executable."
Assert-Contains $bootstrapper "pretrained" "Runtime check must include local pretrained assets."
Assert-Contains $detector 'private const string DllName = "AnomalibLib.dll";' "Detector must delay-load by DLL name."
Assert-Contains $detector "LoadLibraryEx" "Detector must preload by full runtime path."
Assert-Contains $service "UnsupervisedRuntimeBootstrapper.ValidateRuntime" "Service must validate runtime before training."
Assert-Contains $form "RefreshRuntimeStatus" "Form should only refresh runtime status on open."

Assert-NotContains $csproj "<Reference Include=`"AnomalibLib" "Project must not hard-reference AnomalibLib."
Assert-NotContains $csproj "<Content Include=`"UnsupervisedDll" "Project must not bind UnsupervisedDll as content."

Write-Host "Unsupervised runtime isolation checks passed."
