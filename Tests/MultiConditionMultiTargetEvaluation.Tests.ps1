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

Assert-Contains $param "MeasurementResultReader.TryReadMultiTargetItems" "MultiCondition must read measurement Items details."
Assert-Contains $param "TryReadMultiTargetConditionValue" "MultiCondition is missing the multi-target read entry."
Assert-Contains $param "ReadSingleTargetConditionValue" "MultiCondition must evaluate each target item."
Assert-Contains $param "AreAllMultiTargetValuesMatched" "Multi-target condition must require all targets to pass."
Assert-Contains $param "CanReadConditionPathFromAnyTarget" "MultiCondition must detect target item fields before falling back to summary values."
Assert-Contains $param "ShouldRequireTargetOk" "Failed target items must not pass numeric conditions."
Assert-Contains $param "MultiTargetValues.Count == 0" "Empty multi-target details must fail explicitly."

Write-Host "MultiCondition multi-target evaluation checks passed."
