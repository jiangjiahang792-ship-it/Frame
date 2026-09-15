$ErrorActionPreference = 'Stop'

function Assert-ContainsText {
    param([string]$Content, [string]$Expected, [string]$Message)
    if (-not $Content.Contains($Expected)) {
        throw $Message
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$geometryPath = Join-Path $projectRoot 'Node\4-Measurement\Common\GeometryMeasurement.cs'
$lineLinePath = Join-Path $projectRoot 'Node\4-Measurement\LineLineAngle\NodeParamFormLineLineAngle.cs'
$pointPointPath = Join-Path $projectRoot 'Node\4-Measurement\PointPointDistance\NodeParamFormPointPointDistance.cs'
$pointRegionPath = Join-Path $projectRoot 'Node\4-Measurement\PointRegionDistance\NodeParamFormPointRegionDistance.cs'

$geometry = Get-Content -LiteralPath $geometryPath -Raw -Encoding UTF8
$lineLine = Get-Content -LiteralPath $lineLinePath -Raw -Encoding UTF8
$pointPoint = Get-Content -LiteralPath $pointPointPath -Raw -Encoding UTF8
$pointRegion = Get-Content -LiteralPath $pointRegionPath -Raw -Encoding UTF8

Assert-ContainsText $geometry 'TryReadLinePoints(object source' 'The shared reader must read line points from one target item.'
Assert-ContainsText $geometry 'TryReadRegion(object source' 'The shared reader must read a region from one target item.'

Assert-ContainsText $lineLine 'ExecuteSubscribedMeasures' 'Line-line subscribe mode must execute all target pairs.'
Assert-ContainsText $lineLine 'ReadSubscribedLineTargets' 'Line-line subscribe mode must read all target lines.'
Assert-ContainsText $lineLine 'PairByTargetIndex' 'Line-line subscribe mode must pair by TargetIndex.'

Assert-ContainsText $pointPoint 'ExecuteSubscribedMeasures' 'Point-point subscribe mode must execute all target pairs.'
Assert-ContainsText $pointPoint 'ReadSubscribedPointTargets' 'Point-point subscribe mode must read all target points.'
Assert-ContainsText $pointPoint 'PairByTargetIndex' 'Point-point subscribe mode must pair by TargetIndex.'

Assert-ContainsText $pointRegion 'ExecuteSubscribedMeasures' 'Point-region subscribe mode must execute all target pairs.'
Assert-ContainsText $pointRegion 'ReadSubscribedPointTargets' 'Point-region subscribe mode must read all target points.'
Assert-ContainsText $pointRegion 'ReadSubscribedRegionTargets' 'Point-region subscribe mode must read all target regions.'
Assert-ContainsText $pointRegion 'PairByTargetIndex' 'Point-region subscribe mode must pair by TargetIndex.'

Write-Host 'Composite measurement multi-target subscription checks passed.'
