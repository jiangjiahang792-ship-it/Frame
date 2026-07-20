$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$unsupervisedDetector = Get-Content -LiteralPath (Join-Path $root "Forms\AiTrainForm\UnsupervisedAnomalibDetector.cs") -Encoding UTF8 -Raw
$largeDetector = Get-Content -LiteralPath (Join-Path $root "Forms\AiTrainForm\LargeModelDinov2Detector.cs") -Encoding UTF8 -Raw
$unsupervisedRuntime = Get-Content -LiteralPath (Join-Path $root "Node\3-Detection\Unsupervised\UnsupervisedDetectionRuntime.cs") -Encoding UTF8 -Raw
$largeRuntime = Get-Content -LiteralPath (Join-Path $root "Node\3-Detection\LargeModel\LargeModelDetectionRuntime.cs") -Encoding UTF8 -Raw
$unsupervisedNode = Get-Content -LiteralPath (Join-Path $root "Node\3-Detection\Unsupervised\NodeUnsupervisedDetection.cs") -Encoding UTF8 -Raw
$largeNode = Get-Content -LiteralPath (Join-Path $root "Node\3-Detection\LargeModel\NodeLargeModelDetection.cs") -Encoding UTF8 -Raw

function Assert-Contains {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if ($Text.IndexOf($Pattern, [System.StringComparison]::Ordinal) -lt 0) {
        throw $Message
    }
}

function Assert-NotContains {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if ($Text.IndexOf($Pattern, [System.StringComparison]::Ordinal) -ge 0) {
        throw $Message
    }
}

Assert-Contains $unsupervisedRuntime 'return "GPU_ORT";' "Plain GPU must use the same ORT backend as the native demo."
Assert-Contains $unsupervisedRuntime 'return "AUTO_ORT";' "Plain AUTO must use the ORT auto backend."
Assert-Contains $unsupervisedRuntime 'device.StartsWith("AUTO"' "Only AUTO unsupervised backends may retry on CPU."
Assert-NotContains $largeRuntime 'return deviceMode == 0 || deviceMode == 1;' "Explicit large-model GPU must not silently retry on CPU."
Assert-Contains $largeRuntime 'return deviceMode == 0;' "Only large-model AUTO mode may retry on CPU."

foreach ($detectorSource in @($unsupervisedDetector, $largeDetector)) {
    Assert-Contains $detectorSource "out long inputPreparationMilliseconds" "Detector must report managed input preparation time."
    Assert-Contains $detectorSource "out long nativeInferenceMilliseconds" "Detector must report native inference time."
    Assert-Contains $detectorSource "Stopwatch.StartNew()" "Detector must use a high-resolution stopwatch."
}

foreach ($runtimeSource in @($unsupervisedRuntime, $largeRuntime)) {
    Assert-Contains $runtimeSource "private string _loadedDevice" "Runtime must retain the actual loaded device."
    Assert-Contains $runtimeSource "private string _loadedModelPath" "Runtime must retain the actual loaded model path."
    Assert-Contains $runtimeSource "LogLoadedRuntime" "Runtime must log the actual device and model after loading."
    Assert-Contains $runtimeSource "ImagePreparationMilliseconds" "Runtime result must expose image preparation time."
    Assert-Contains $runtimeSource "NativeInferenceMilliseconds" "Runtime result must expose native inference time."
    Assert-Contains $runtimeSource "PostprocessMilliseconds" "Runtime result must expose postprocess time."
    Assert-Contains $runtimeSource "ActualDevice" "Runtime result must expose actual device."
    Assert-Contains $runtimeSource "ActualModelPath" "Runtime result must expose actual model path."
    Assert-Contains $runtimeSource "InputWidth" "Runtime result must expose input width."
    Assert-Contains $runtimeSource "InputHeight" "Runtime result must expose input height."
}

foreach ($nodeSource in @($unsupervisedNode, $largeNode)) {
    Assert-Contains $nodeSource "runResult.ActualDevice" "Node success log must include the actual device."
    Assert-Contains $nodeSource "runResult.ImagePreparationMilliseconds" "Node success log must include image preparation time."
    Assert-Contains $nodeSource "runResult.NativeInferenceMilliseconds" "Node success log must include native inference time."
    Assert-Contains $nodeSource "runResult.PostprocessMilliseconds" "Node success log must include postprocess time."
    Assert-Contains $nodeSource "runResult.InputWidth" "Node success log must include input dimensions."
}

Write-Host "Inference backend diagnostics checks passed."
