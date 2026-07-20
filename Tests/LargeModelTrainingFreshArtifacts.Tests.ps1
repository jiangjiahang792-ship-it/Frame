$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$servicePath = Join-Path $root "Forms\AiTrainForm\LargeModelTrainingService.cs"
$pipelinePath = Join-Path $root "Forms\AiTrainForm\LargeModelTrainingPipeline.cs"
$detectorPath = Join-Path $root "Forms\AiTrainForm\LargeModelDinov2Detector.cs"
$templatePackagePath = Join-Path $root "Forms\AiTrainForm\LargeModelTemplatePackage.cs"
$productionBehaviorPath = Join-Path $root "Tests\LargeModelTrainingProductionBehavior.Tests.ps1"
$projectPath = Join-Path $root "TDJS-Vision.csproj"

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
if (-not (Test-Path -LiteralPath $pipelinePath)) {
    throw "Missing executable large-model training pipeline coordinator."
}
if (-not (Test-Path -LiteralPath $productionBehaviorPath)) {
    throw "Missing executable production-entry large-model behavior tests."
}

$service = Get-Content -LiteralPath $servicePath -Encoding UTF8 -Raw
$pipeline = Get-Content -LiteralPath $pipelinePath -Encoding UTF8 -Raw
$detector = Get-Content -LiteralPath $detectorPath -Encoding UTF8 -Raw
$templatePackage = Get-Content -LiteralPath $templatePackagePath -Encoding UTF8 -Raw
$project = Get-Content -LiteralPath $projectPath -Encoding UTF8 -Raw
$cleanupFailureText = -join @([char]0x4E34, [char]0x65F6, [char]0x6A21, [char]0x578B, [char]0x6587, [char]0x4EF6, [char]0x6E05, [char]0x7406, [char]0x5931, [char]0x8D25, [char]0xFF1A)

Assert-Contains $project 'Forms\AiTrainForm\LargeModelTrainingPipeline.cs' "The executable training coordinator must be compiled into the production project."
Assert-Contains $service 'new LargeModelTrainingCoordinator()' "The production training service must create the executable coordinator."
Assert-Contains $service '_trainingCoordinator.Execute(runtimeRoot, useGpu, stages, log, token)' "The production entry must run the whole chain through the coordinator."
Assert-Contains $service 'ILargeModelTrainingStages' "The production path must provide injectable training stages."
Assert-Contains $service 'RebuildOnnxModelPath' "Every training run must rebuild ONNX."
Assert-Contains $service 'RebuildTensorRtEnginePath' "GPU training must rebuild TensorRT Engine from this run's ONNX."
Assert-NotContains $service 'if (File.Exists(enginePath))' "GPU training must not reuse an existing TensorRT engine."
Assert-NotContains $service 'if (File.Exists(onnxPath))' "Training must not reuse an existing ONNX model."
Assert-Contains $pipeline 'Guid.NewGuid().ToString("N") + ".building" + extension' "Every generated artifact must use a unique temporary path."
Assert-Contains $pipeline 'actualLength < minimumLength' "Generated artifacts must meet an explicit minimum length."
Assert-Contains $pipeline 'validator.Validate(temporaryPath)' "Generated artifacts must pass loadability validation before publication."
Assert-Matches $pipeline 'validator\.Validate\(temporaryPath\);.*?token\.ThrowIfCancellationRequested\(\);.*?ReplaceGeneratedFile\(temporaryPath, targetPath\);' "Cancellation must be rechecked immediately before formal model replacement."
Assert-Contains $pipeline 'FileShare.None' "The whole training chain must use a cross-process exclusive file lock."
Assert-Contains $pipeline 'token.WaitHandle.WaitOne(RetryIntervalMilliseconds)' "Waiting for the cross-process lock must support cancellation."
Assert-Contains $pipeline 'TryDeleteGeneratedFile(temporaryPath, log)' "Failed or cancelled generation must attempt temporary-file cleanup with logging."
Assert-Contains $pipeline $cleanupFailureText "Cleanup failure must emit a Simplified Chinese log."
Assert-Contains $service 'MinimumOnnxArtifactLength' "ONNX publication must use an explicit minimum valid length."
Assert-Contains $service 'MinimumEngineArtifactLength' "Engine publication must use an explicit minimum valid length."
Assert-NotContains $service 'NativeModelArtifactValidator' "Artifact publication must not call Dinov2AD init inside the main process because native access violations terminate the application."
Assert-NotContains $service 'detector.LoadModel(modelPath, string.Empty, _deviceMode)' "Artifact publication must not perform an extra in-process native model initialization before Bank training."
Assert-Contains $service 'ValidateOnnxArtifactInSubprocess' "ONNX artifacts must be validated in an isolated Python subprocess."
Assert-Contains $service 'onnx.checker.check_model' "The isolated ONNX validator must execute the ONNX structural checker."
Assert-Contains $service 'ValidateTensorRtEngineInSubprocess' "TensorRT engines must be validated in an isolated trtexec subprocess."
Assert-Contains $service '--loadEngine=' "The isolated TensorRT validator must load the newly generated temporary engine."
Assert-Contains $service 'TryKillProcessAndWait(process, log)' "Cancellation must kill and bounded-wait the exporter or builder process."
Assert-Matches $service 'token\.ThrowIfCancellationRequested\(\);\s*process\.Start\(\);' "External processes must check cancellation immediately before start."
Assert-Matches $service 'process\.WaitForExit\(\);\s*token\.ThrowIfCancellationRequested\(\);' "External processes must check cancellation after final exit."
Assert-Contains $service 'LargeModelProcessOutputBuffer processOutput' "stdout and stderr must use the thread-safe output buffer."
Assert-NotContains $service 'StringBuilder processOutput = new StringBuilder();' "stdout and stderr must not concurrently mutate a raw StringBuilder."
Assert-Matches $service '\u8BF7\u8865\u9F50 LargeModelDll \u4E0B\u7684 export_dinov2\.py\u3001dinov2 \u6E90\u7801\u76EE\u5F55\u548C third_party\\\\python_native_env\u3002' "Missing ONNX export dependencies must instruct users to restore the full export environment."
Assert-NotMatches $service '\u6216\u624B\u52A8\u653E\u5165 dinov2_' "Missing export dependencies must not suggest reusing a manually supplied ONNX model."
Assert-Contains $service 'ResolveTrainingWorkRoot(modelRoot, request.TemplateName, out _trustedTrainingRoot)' "Production stages must resolve work directories below the fixed trusted training root."
Assert-Contains $service 'PrepareCleanDirectory(_modelOutputRoot, _trustedTrainingRoot)' "Bank cleanup must be authorized by the fixed trusted root rather than a user-derived work root."
Assert-Contains $service 'ValidateTemplateName(request.TemplateName)' "The production entry must reject current-directory and parent-directory template names before cleanup."
Assert-Matches $service 'detector\.TrainMemoryBank\(\s*modelPath,\s*_bankPath,\s*_deviceMode,.*?token\);' "Production Bank training must pass cancellation into the real detector."
Assert-Contains $detector 'if (!_disposed && !token.IsCancellationRequested)' "A native Init handle must only publish while the detector remains active and uncancelled."
Assert-Contains $detector 'ReleaseNativeHandle(initializedHandle)' "A late native Init handle must be released instead of reviving a disposed detector."
Assert-Contains $templatePackage 'Guid.NewGuid().ToString("N") + ".building" + extension' "Template publication must use a GUID temporary .tdlarge path."
Assert-Contains $templatePackage 'ValidateTemporaryPackage(temporaryPath, manifest.ModelFileName, token)' "A temporary template must be validated before atomic publication."
Assert-Contains $templatePackage 'ReplaceTemplateFile(temporaryPath, fullTemplatePath)' "A validated template must be atomically published from the same-directory temporary path."

Write-Host "Large-model fresh-artifact training checks passed."
