param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
    throw "Release executable does not exist: $ExecutablePath"
}

$assembly = [System.Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ExecutablePath).Path)
$namespace = "TDJS_Vision.Node._3_Detection.TDAI.Yolo8."
$workerType = $assembly.GetType($namespace + "YoloIsolatedWorkerProgram", $true)
$requestType = $assembly.GetType($namespace + "YoloIsolatedRequest", $true)
$modelField = $workerType.GetField("currentModel", [System.Reflection.BindingFlags]"NonPublic, Static")
$handleRequest = $workerType.GetMethod("HandleRequest", [System.Reflection.BindingFlags]"NonPublic, Static")

foreach ($name in @("Yolo8Det", "Yolo8Obb", "Yolo8Seg", "Yolo8Pose")) {
    $modelType = $assembly.GetType($namespace + $name, $true)
    $model = [System.Activator]::CreateInstance($modelType)
    $model.ScoreThreshold = [float]0.35
    $model.NMSThreshold = [float]0.45
    $modelField.SetValue($null, $model)

    try {
        $request = [System.Activator]::CreateInstance($requestType, $true)
        $request.Command = "update-thresholds"
        $request.ScoreThreshold = [float]0.72
        $request.NmsThreshold = [float]0.28
        $response = $handleRequest.Invoke($null, @($request))

        if (-not $response.Success -or
            $model.ScoreThreshold -ne [float]0.72 -or
            $model.NMSThreshold -ne [float]0.28) {
            throw "$name did not apply the worker threshold update."
        }
    }
    finally {
        $modelField.SetValue($null, $null)
    }
}

Write-Host "YOLO worker threshold update checks passed for DET, OBB, SEG and POSE."
