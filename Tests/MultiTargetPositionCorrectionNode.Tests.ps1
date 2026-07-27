$ErrorActionPreference = 'Stop'

function Assert-ContainsText {
    param(
        [string]$Content,
        [string]$Expected,
        [string]$Message
    )

    if (-not $Content.Contains($Expected)) {
        throw $Message
    }
}

function Assert-NotContainsText {
    param(
        [string]$Content,
        [string]$Unexpected,
        [string]$Message
    )

    if ($Content.Contains($Unexpected)) {
        throw $Message
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$matchResultPath = Join-Path $projectRoot 'Node\3-Detection\MatchTemplate\NodeResultMatchTemplate.cs'
$matchNodePath = Join-Path $projectRoot 'Node\3-Detection\MatchTemplate\NodeMatchTemplate.cs'
$positionResultPath = Join-Path $projectRoot 'Node\4-Measurement\PositionCorrection\NodeResultPositionCorrection.cs'
$commonResultPath = Join-Path $projectRoot 'Node\4-Measurement\Common\MultiTargetPositionCorrectionResult.cs'
$positionNodePath = Join-Path $projectRoot 'Node\4-Measurement\PositionCorrection\NodePositionCorrection.cs'
$positionFormPath = Join-Path $projectRoot 'Node\4-Measurement\PositionCorrection\NodeParamFormPositionCorrection.cs'
$positionDesignerPath = Join-Path $projectRoot 'Node\4-Measurement\PositionCorrection\NodeParamFormPositionCorrection.Designer.cs'

foreach ($path in @($matchResultPath, $matchNodePath, $positionResultPath, $positionNodePath, $positionFormPath, $positionDesignerPath, $commonResultPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required production file does not exist: $path"
    }
}

$matchResult = Get-Content -LiteralPath $matchResultPath -Raw -Encoding UTF8
$matchNode = Get-Content -LiteralPath $matchNodePath -Raw -Encoding UTF8
$positionResult = Get-Content -LiteralPath $positionResultPath -Raw -Encoding UTF8
$commonResult = Get-Content -LiteralPath $commonResultPath -Raw -Encoding UTF8
$positionNode = Get-Content -LiteralPath $positionNodePath -Raw -Encoding UTF8
$positionForm = Get-Content -LiteralPath $positionFormPath -Raw -Encoding UTF8
$positionDesigner = Get-Content -LiteralPath $positionDesignerPath -Raw -Encoding UTF8

Assert-ContainsText $matchResult 'List<TemplateMatchPose> Poses' 'Template matching result must expose all poses.'
Assert-ContainsText $matchNode 'ScaleX = 1.0' 'Template matching poses must explicitly output ScaleX one.'
Assert-ContainsText $matchNode 'ScaleY = 1.0' 'Template matching poses must explicitly output ScaleY one.'
Assert-ContainsText $positionResult 'MultiTargetPositionCorrectionResult' 'Position correction node result must use the common collection contract.'
Assert-ContainsText $commonResult 'List<PositionCorrectionInfo> Items' 'The common position correction result must expose all correction items.'
Assert-ContainsText $commonResult 'TemplateMatchPose BasePose' 'The common position correction result must expose its baseline pose.'
Assert-ContainsText $positionNode 'poses[0]' 'Position correction must use the first pose as the default baseline.'
Assert-ContainsText $positionDesigner 'nodeSubscriptionPoses' 'The designer must contain the pose list subscription control.'
Assert-NotContainsText $positionDesigner 'nodeSubscriptionX' 'The old X subscription control must be removed.'
Assert-NotContainsText $positionDesigner 'nodeSubscriptionY' 'The old Y subscription control must be removed.'
Assert-NotContainsText $positionDesigner 'nodeSubscriptionAngle' 'The old angle subscription control must be removed.'
Assert-ContainsText $positionForm 'GetValue<List<TemplateMatchPose>>' 'The form must read the full pose list subscription.'

Write-Host 'Multi-target position correction source checks passed.'
