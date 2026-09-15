$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$modelsPath = Join-Path $root "Forms\AiTrainForm\UnsupervisedTrainingModels.cs"
$packagePath = Join-Path $root "Forms\AiTrainForm\UnsupervisedTemplatePackage.cs"
$servicePath = Join-Path $root "Forms\AiTrainForm\UnsupervisedTrainingService.cs"
$csprojPath = Join-Path $root "TDJS-Vision.csproj"

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

Assert-FileExists $modelsPath "Missing unsupervised training models file."
Assert-FileExists $packagePath "Missing unsupervised template package file."
Assert-FileExists $servicePath "Missing unsupervised training service file."

$models = Get-Content -LiteralPath $modelsPath -Encoding UTF8 -Raw
$package = Get-Content -LiteralPath $packagePath -Encoding UTF8 -Raw
$service = Get-Content -LiteralPath $servicePath -Encoding UTF8 -Raw
$csproj = Get-Content -LiteralPath $csprojPath -Encoding UTF8 -Raw

Assert-Contains $models "UnsupervisedTemplateManifest" "Template manifest model must exist."
Assert-Contains $models "RoiEnabled" "Manifest must record ROI usage."
Assert-Contains $models "RoiX" "Manifest must record ROI X."
Assert-Contains $models "InputWidth" "Manifest must record input width."
Assert-Contains $models "Threshold" "Manifest must record threshold."
Assert-Contains $package "ZipArchive" "Template must use single-file zip package."
Assert-Contains $package "manifest.json" "Template package must include manifest.json."
Assert-Contains $package "model.onnx" "Template package must include model.onnx."
Assert-Contains $service "UnsupervisedTemplatePackage.Create" "Training success must create template package."
Assert-Contains $service "CropImageToRoi" "Training service must support ROI crop dataset."
Assert-Contains $service "CopyImageToDataset" "Training service must support full-image dataset."
Assert-Contains $service "ValidateDatasetImageCount(dataset);" "Training service must validate dataset image count before native training."
Assert-Contains $service "okImageCount < 1 || ngImageCount < 1" "Training service must allow the minimum 1 OK and 1 NG samples."
Assert-Contains $service "CalibrationOkImagePath" "Training service must keep an OK calibration image outside the training set."
Assert-Contains $service "CalibrationNgImagePath" "Training service must keep an NG calibration image outside the training set."
Assert-Contains $service "CalibrateThreshold" "Training service must calibrate the recognition score after native training."
Assert-Contains $service "HasCalibrationImages(dataset)" "Training service must skip automatic score calibration when held-out samples are unavailable."
Assert-Contains $service "UnsupervisedNativeDatasetPreparer.EnsureMinimumNgSplitFiles" "Single-NG training must prepare enough files for native val/test splitting."
Assert-Contains $service "return request.Threshold;" "Automatic score calibration failures must use the default threshold without failing training."
Assert-Contains $service "BuildManifest(request, dataset, calibratedThreshold)" "Template manifest must persist the calibrated threshold."
Assert-Contains $service "throw new InvalidOperationException" "Training service must surface dataset count errors to the UI dialog path."
Assert-Contains $csproj "System.IO.Compression" "Project must reference System.IO.Compression."

Write-Host "Unsupervised template package checks passed."
