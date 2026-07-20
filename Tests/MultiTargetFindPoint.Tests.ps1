$ErrorActionPreference = 'Stop'

function Assert-ContainsText {
    param([string]$Content, [string]$Expected, [string]$Message)
    if (-not $Content.Contains($Expected)) {
        throw $Message
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$folder = Join-Path $projectRoot 'Node\4-Measurement\FindPoint'
$result = Get-Content -LiteralPath (Join-Path $folder 'NodeResultFindPoint.cs') -Raw -Encoding UTF8
$node = Get-Content -LiteralPath (Join-Path $folder 'NodeFindPoint.cs') -Raw -Encoding UTF8
$form = Get-Content -LiteralPath (Join-Path $folder 'NodeParamFormFindPoint.cs') -Raw -Encoding UTF8
$targetWord = ([char]0x76EE).ToString() + ([char]0x6807).ToString()

Assert-ContainsText $result 'List<FindPointTargetResult> Items' 'FindPoint must expose ordered target result items.'
Assert-ContainsText $form 'ExecuteMeasures(' 'FindPoint must expose a multi-target execution entry point.'
Assert-ContainsText $form 'MultiTargetMeasurementRunner.Run' 'FindPoint must use the common multi-target runner.'
Assert-ContainsText $form 'GetValue<List<PositionCorrectionInfo>>' 'FindPoint must read the full correction list.'
Assert-ContainsText $form '_transformService.TransformPoints' 'FindPoint must transform every region point for each target.'
Assert-ContainsText $form 'ResolveAnchorIndex' 'FindPoint must resolve which target owns the single editing ROI.'
Assert-ContainsText $form 'InverseTransformPoints' 'FindPoint must normalize the editing ROI back to the baseline.'
Assert-ContainsText $form 'CreateFailure' 'FindPoint must retain a zero-valued failure item.'
Assert-ContainsText $node 'items.All' 'FindPoint overall status must require every target to pass.'
Assert-ContainsText $node $targetWord 'FindPoint display output must identify each target.'

Write-Host 'Multi-target FindPoint source checks passed.'
