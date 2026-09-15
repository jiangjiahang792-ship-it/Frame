$ErrorActionPreference = "Stop"

$root = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    (Get-Location).Path
}
else {
    Split-Path -Parent $PSScriptRoot
}

$nodePath = Join-Path $root "Node\4-Measurement\PositionCorrection\NodePositionCorrection.cs"
$formPath = Join-Path $root "Node\4-Measurement\PositionCorrection\NodeParamFormPositionCorrection.cs"
$node = Get-Content -Raw -Encoding UTF8 $nodePath
$form = Get-Content -Raw -Encoding UTF8 $formPath
$readPosesStart = $form.IndexOf("private List<TemplateMatchPose> ReadPoses()", [StringComparison]::Ordinal)
$updateBaselineStart = $form.IndexOf("private void UpdateBaselineStatus()", [StringComparison]::Ordinal)
if ($readPosesStart -lt 0 -or $updateBaselineStart -le $readPosesStart) {
    throw "Cannot locate ReadPoses body."
}

$readPoses = $form.Substring($readPosesStart, $updateBaselineStart - $readPosesStart)

function Assert-Contains {
    param([string]$Content, [string]$Pattern, [string]$Message)
    if ($Content -notlike "*$Pattern*") {
        throw $Message
    }
}

function Assert-NotContains {
    param([string]$Content, [string]$Pattern, [string]$Message)
    if ($Content -like "*$Pattern*") {
        throw $Message
    }
}

Assert-Contains $readPoses 'return poses ?? new List<TemplateMatchPose>();' "Position correction form must return an empty pose list instead of throwing."
Assert-NotContains $readPoses "throw new Exception" "Reading empty template poses must not abort the process."
Assert-Contains $node 'return new List<PositionCorrectionInfo>();' "Position correction must convert empty poses to an empty correction list."
Assert-Contains $node "TargetCount = 0" "Empty correction result must expose zero targets."
Assert-Contains $node "IsOk = false" "Empty correction result must be NG."
Assert-Contains $node "JudgeOk = false" "Empty correction result must publish JudgeOk false."
Assert-Contains $node 'nodeResult.IsOk ? "OK" : "NG"' "Position correction log must show OK/NG without failing."

Write-Host "PositionCorrection empty template NG checks passed."
