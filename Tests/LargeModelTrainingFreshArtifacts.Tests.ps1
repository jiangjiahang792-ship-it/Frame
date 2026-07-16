$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$servicePath = Join-Path $root "Forms\AiTrainForm\LargeModelTrainingService.cs"

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

function Assert-Matches {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if (-not [System.Text.RegularExpressions.Regex]::IsMatch(
        $Text,
        $Pattern,
        [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        throw $Message
    }
}

function Assert-NotMatches {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if ([System.Text.RegularExpressions.Regex]::IsMatch(
        $Text,
        $Pattern,
        [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        throw $Message
    }
}

if (-not (Test-Path -LiteralPath $servicePath)) {
    throw "Missing large-model training service."
}

$service = Get-Content -LiteralPath $servicePath -Encoding UTF8 -Raw

Assert-Contains $service 'string modelPath = RebuildTrainingModelPath(runtime.RootPath, request, log, token);' "Training must rebuild the model for every run."
Assert-Contains $service 'string onnxPath = RebuildOnnxModelPath(root, request.InputWidth, request.InputHeight, sizeText, log, token);' "Every training run must rebuild ONNX before selecting CPU or GPU."
Assert-NotContains $service 'if (File.Exists(enginePath))' "GPU training must not reuse an existing TensorRT engine."
Assert-NotContains $service 'if (File.Exists(onnxPath))' "Training must not reuse an existing ONNX model."
Assert-Contains $service 'Path.GetFileNameWithoutExtension(onnxPath) + "_" + Guid.NewGuid().ToString("N") + ".building.onnx"' "ONNX export must use a unique temporary file for each run."
Assert-Contains $service 'Path.GetFileNameWithoutExtension(enginePath) + "_" + Guid.NewGuid().ToString("N") + ".building.engine"' "TensorRT export must use a unique temporary file for each run."
Assert-NotContains $service 'Path.GetFileNameWithoutExtension(onnxPath) + ".building.onnx"' "ONNX export must not use a shared fixed temporary file."
Assert-NotContains $service 'Path.GetFileNameWithoutExtension(enginePath) + ".building.engine"' "TensorRT export must not use a shared fixed temporary file."
Assert-Matches $service 'string tempOnnxPath = .*?Guid\.NewGuid\(\)\.ToString\("N"\).*?;\s*try' "A unique ONNX temporary path must enter generation without stale-file pre-cleaning."
Assert-Matches $service 'string tempEnginePath = .*?Guid\.NewGuid\(\)\.ToString\("N"\).*?;\s*try' "A unique Engine temporary path must enter generation without stale-file pre-cleaning."
Assert-Contains $service 'ExportOnnxModel(root, tempOnnxPath, width, height, sizeText, log, token);' "ONNX export must target the temporary file."
Assert-Contains $service 'ReplaceGeneratedFile(tempOnnxPath, onnxPath);' "A completed ONNX file must replace the previous formal model."
Assert-Contains $service 'ReplaceGeneratedFile(tempEnginePath, enginePath);' "A completed engine must replace the previous formal engine."
Assert-Contains $service 'TryDeleteGeneratedFile(tempOnnxPath);' "Failed ONNX export must clean its temporary file."
Assert-Contains $service 'TryDeleteGeneratedFile(tempEnginePath);' "Failed engine generation must clean its temporary file."
Assert-Matches $service '\u8BF7\u8865\u9F50 LargeModelDll \u4E0B\u7684 export_dinov2\.py\u3001dinov2 \u6E90\u7801\u76EE\u5F55\u548C third_party\\\\python_native_env\u3002' "Missing ONNX export dependencies must instruct users to restore the full export environment."
Assert-NotMatches $service '\u6216\u624B\u52A8\u653E\u5165 dinov2_' "Missing export dependencies must not suggest reusing a manually supplied ONNX model."
Assert-Matches $service 'private static string RebuildTrainingModelPath\(.*?string onnxPath = RebuildOnnxModelPath\(.*?if \(string.Equals\(normalizedDevice, "CPU".*?return onnxPath;.*?BuildTensorRtEngine\(root, trtexecPath, onnxPath, enginePath, normalizedPrecision, log, token\);' "CPU must return the new ONNX while GPU must build a new engine from it."
Assert-Matches $service 'PrepareCleanDirectory\(modelOutputRoot, workRoot\);.*?string bankPath = Path.Combine\(modelOutputRoot, "memory.bank"\);.*?detector.TrainMemoryBank\(modelPath, bankPath' "Every run must clean the bank output and train a new bank."

Write-Host "Large-model fresh-artifact training checks passed."
