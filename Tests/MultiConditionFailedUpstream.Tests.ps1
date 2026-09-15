$ErrorActionPreference = "Stop"

$root = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    (Get-Location).Path
}
else {
    Split-Path -Parent $PSScriptRoot
}

$paramPath = Join-Path $root "Node\6-LogicTool\MultiCondition\NodeParamMultiCondition.cs"
$param = Get-Content -Raw -Encoding UTF8 $paramPath

function Assert-Contains {
    param([string]$Content, [string]$Pattern, [string]$Message)
    if ($Content -notlike "*$Pattern*") {
        throw $Message
    }
}

Assert-Contains $param "BuildFailedUpstreamEvaluations(currentNode)" "MultiCondition must check failed upstream nodes before normal evaluation."
Assert-Contains $param "RuntimeStatus != NodeStatus.Failed" "Failed upstream check must use the node failed runtime status."
Assert-Contains $param "failedUpstreamEvaluations.Count > 0" "Failed upstream evaluations must short-circuit the normal condition path."
Assert-Contains $param "return false;" "Failed upstream nodes must force the final result to false."
Assert-Contains $param "Name =" "Failed upstream nodes must be written into condition details."
Assert-Contains $param "DiagnosticText =" "Failed upstream diagnostics must be written for troubleshooting."
Assert-Contains $param "JudgeWriteBackText =" "Failed upstream diagnostics must avoid judge write-back."

Write-Host "MultiCondition failed upstream check passed."
