$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$interfacePath = Join-Path $root "Node\INodeRuntimePreloader.cs"
$coordinatorPath = Join-Path $root "Startup\SolutionAiRuntimePreloader.cs"
$largeNodePath = Join-Path $root "Node\3-Detection\LargeModel\NodeLargeModelDetection.cs"
$unsupervisedNodePath = Join-Path $root "Node\3-Detection\Unsupervised\NodeUnsupervisedDetection.cs"
$configPath = Join-Path $root "ConfigHelper.cs"
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

if (-not (Test-Path -LiteralPath $interfacePath)) {
    throw "Missing shared AI runtime preload interface."
}
if (-not (Test-Path -LiteralPath $coordinatorPath)) {
    throw "Missing solution AI runtime preload coordinator."
}

$interface = Get-Content -LiteralPath $interfacePath -Encoding UTF8 -Raw
$coordinator = Get-Content -LiteralPath $coordinatorPath -Encoding UTF8 -Raw
$largeNode = Get-Content -LiteralPath $largeNodePath -Encoding UTF8 -Raw
$unsupervisedNode = Get-Content -LiteralPath $unsupervisedNodePath -Encoding UTF8 -Raw
$config = Get-Content -LiteralPath $configPath -Encoding UTF8 -Raw
$project = Get-Content -LiteralPath $projectPath -Encoding UTF8 -Raw

Assert-Contains $interface "interface INodeRuntimePreloader" "Preload contract must be interface based."
Assert-Contains $interface "bool HasSavedRuntimeConfiguration" "Preload contract must identify configured nodes."
Assert-Contains $interface "Task PreloadSavedRuntimeAsync()" "Preload contract must expose an asynchronous operation."

Assert-Contains $coordinator "Solution.Instance.AllProcesses" "Coordinator must traverse restored processes."
Assert-Contains $coordinator "if (!process.Enable)" "Coordinator must skip disabled processes."
Assert-Contains $coordinator "if (!node.Active)" "Coordinator must skip disabled nodes."
Assert-Contains $coordinator "INodeRuntimePreloader preloader" "Coordinator must depend on the shared preload contract."
Assert-Contains $coordinator "preloader.HasSavedRuntimeConfiguration" "Coordinator must skip unconfigured AI nodes."
Assert-Contains $coordinator "PreloadSavedRuntimeAsync().GetAwaiter().GetResult()" "Coordinator must await each preload before starting the next one."
Assert-NotContains $coordinator "Task.WhenAll" "Solution preload must not initialize GPU models in parallel."
Assert-Contains $coordinator "StartupProgressContext.ReportItem" "Coordinator must report preload progress on the loading screen."
Assert-Contains $coordinator "StartupProgressContext.ReportFailure" "Coordinator must aggregate node preload failures."
Assert-Contains $coordinator "catch (Exception ex)" "Coordinator must isolate failures per node."

Assert-Contains $largeNode "NodeLargeModelDetection : NodeBase, INodeRuntimePreloader" "Large-model node must implement the shared preload contract."
Assert-Contains $largeNode "public Task PreloadSavedRuntimeAsync()" "Large-model node must preload restored parameters."
Assert-Contains $largeNode "return PreloadRuntimeAsync(param);" "Large-model restored preload must reuse save-time preload."
Assert-Contains $unsupervisedNode "NodeUnsupervisedDetection : NodeBase, INodeRuntimePreloader" "Unsupervised node must implement the shared preload contract."
Assert-Contains $unsupervisedNode "public Task PreloadSavedRuntimeAsync()" "Unsupervised node must preload restored parameters."
Assert-Contains $unsupervisedNode "return PreloadRuntimeAsync(param);" "Unsupervised restored preload must reuse save-time preload."

$eventIndex = $config.IndexOf("DeserializationCompletionEvent?.Invoke(null, flag);", [System.StringComparison]::Ordinal)
$preloadIndex = $config.IndexOf("SolutionAiRuntimePreloader.PreloadEnabledNodes();", [System.StringComparison]::Ordinal)
if ($eventIndex -lt 0 -or $preloadIndex -le $eventIndex) {
    throw "Solution load must preload AI runtimes after all nodes are restored."
}

Assert-Contains $project '<Compile Include="Node\INodeRuntimePreloader.cs" />' "Project must compile the preload contract."
Assert-Contains $project '<Compile Include="Startup\SolutionAiRuntimePreloader.cs" />' "Project must compile the preload coordinator."

Write-Host "Solution AI runtime preload checks passed."
