$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$programPath = Join-Path $root "Program.cs"
$projectPath = Join-Path $root "TDJS-Vision.csproj"
$yoloDir = Join-Path $root "Node\3-Detection\TDAI\Yolo8"

function Assert-Contains {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if ($Text.IndexOf($Pattern, [System.StringComparison]::Ordinal) -lt 0) {
        throw $Message
    }
}

$program = Get-Content -LiteralPath $programPath -Encoding UTF8 -Raw
$project = Get-Content -LiteralPath $projectPath -Encoding UTF8 -Raw
$det = Get-Content -LiteralPath (Join-Path $yoloDir "Yolo8Det.cs") -Encoding UTF8 -Raw
$obb = Get-Content -LiteralPath (Join-Path $yoloDir "Yolo8Obb.cs") -Encoding UTF8 -Raw
$seg = Get-Content -LiteralPath (Join-Path $yoloDir "Yolo8Seg.cs") -Encoding UTF8 -Raw
$pose = Get-Content -LiteralPath (Join-Path $yoloDir "Yolo8Pose.cs") -Encoding UTF8 -Raw
$client = Get-Content -LiteralPath (Join-Path $yoloDir "YoloIsolatedWorkerClient.cs") -Encoding UTF8 -Raw
$worker = Get-Content -LiteralPath (Join-Path $yoloDir "YoloIsolatedWorkerProgram.cs") -Encoding UTF8 -Raw

Assert-Contains $program "YoloIsolatedWorkerProgram.TryRun(args)" "Program must enter YOLO worker mode before main UI startup."
Assert-Contains $program "using (Mutex singleInstanceMutex" "Main application must keep single instance protection outside worker mode."

foreach ($file in @(
    "YoloIsolatedMessages.cs",
    "YoloIsolatedRuntimeContext.cs",
    "YoloIsolatedWorkerClient.cs",
    "YoloIsolatedWorkerProgram.cs")) {
    Assert-Contains $project "<Compile Include=`"Node\3-Detection\TDAI\Yolo8\$file`" />" "Project must compile $file."
}

foreach ($source in @($det, $obb, $seg, $pose)) {
    Assert-Contains $source "YoloIsolatedWorkerClient.Open" "Each YOLO model wrapper must open an isolated worker in the main process."
    Assert-Contains $source "YoloIsolatedRuntimeContext.IsWorkerProcess" "Each YOLO model wrapper must keep native loading local only inside the worker."
}

Assert-Contains $client "Application.ExecutablePath" "Worker client must launch the same deployed executable for simpler publishing."
Assert-Contains $client "RedirectStandardInput = true" "Worker client must communicate through redirected stdin."
Assert-Contains $client "RedirectStandardOutput = true" "Worker client must communicate through redirected stdout."
Assert-Contains $worker "YoloIsolatedRuntimeContext.MarkAsWorkerProcess()" "Worker process must mark itself to avoid recursive worker creation."
Assert-Contains $worker "JsonConvert.DeserializeObject<YoloIsolatedRequest>" "Worker must use structured JSON requests."
Assert-Contains $worker "Console.OpenStandardInput()" "Worker must read the redirected input handle without configuring a nonexistent WinExe console."
Assert-Contains $worker 'case "ping":' "Worker must provide a model-independent process handshake."
Assert-Contains $client "BuildWorkerExitMessage" "Worker failures must include process exit diagnostics."

if ($worker.IndexOf("Console.InputEncoding", [System.StringComparison]::Ordinal) -ge 0) {
    throw "WinExe workers must not set Console.InputEncoding because no console is attached."
}

Write-Host "YOLO runtime isolation checks passed."
