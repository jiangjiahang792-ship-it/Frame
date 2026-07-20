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
Assert-Contains $service "dataset.OkCount < minOkCount" "Training service must reject insufficient OK images before native training."
Assert-Contains $service "dataset.NgCount == 1" "Training service must reject one NG image before native training."
Assert-Contains $service "const int minNgCount = 2;" "Training service must keep the native-compatible NG minimum."
Assert-Contains $service "throw new InvalidOperationException" "Training service must surface dataset count errors to the UI dialog path."
Assert-Contains $csproj "System.IO.Compression" "Project must reference System.IO.Compression."

Write-Host "Unsupervised template package checks passed."
