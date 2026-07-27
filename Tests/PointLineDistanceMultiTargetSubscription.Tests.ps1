$ErrorActionPreference = 'Stop'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Assert-ContainsText {
    param([string]$Content, [string]$Expected, [string]$Message)
    if (-not $Content.Contains($Expected)) {
        throw $Message
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$pairerPath = Join-Path $projectRoot 'Node\4-Measurement\Common\MultiTargetMeasurementPairer.cs'
$projectPath = Join-Path $projectRoot 'TDJS-Vision.csproj'
$formPath = Join-Path $projectRoot 'Node\4-Measurement\PointLineDistance\NodeParamFormPointLineDistance.cs'
$readerPath = Join-Path $projectRoot 'Node\4-Measurement\Common\GeometryMeasurement.cs'

if (-not (Test-Path -LiteralPath $pairerPath)) {
    throw 'Multi-target measurement pairer is missing.'
}

$pairerBody = Get-Content -LiteralPath $pairerPath -Raw -Encoding UTF8
Add-Type -TypeDefinition $pairerBody -Language CSharp

$points = [System.Collections.Generic.List[TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]]::new()
$points.Add([TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]::new(1, $true, 10, ''))
$points.Add([TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]::new(2, $false, 0, 'point target 2 failed'))
$points.Add([TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]::new(3, $true, 30, ''))

$lines = [System.Collections.Generic.List[TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]]::new()
$lines.Add([TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]::new(3, $true, 300, ''))
$lines.Add([TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]::new(1, $true, 100, ''))
$lines.Add([TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]::new(2, $true, 200, ''))

$pairs = [TDJS_Vision.Node._4_Measurement.Common.MultiTargetMeasurementPairer]::PairByTargetIndex($points, $lines, 'point', 'line')
Assert-True ($pairs.Count -eq 3) 'Three points and three lines must produce three pairs.'
Assert-True ($pairs[0].TargetIndex -eq 1 -and $pairs[0].First.Value -eq 10 -and $pairs[0].Second.Value -eq 100) 'Target 1 must pair with target 1.'
Assert-True (-not $pairs[1].IsOk -and $pairs[1].TargetIndex -eq 2) 'A failed target 2 must retain an NG placeholder.'
Assert-True ($pairs[2].IsOk -and $pairs[2].TargetIndex -eq 3 -and $pairs[2].First.Value -eq 30 -and $pairs[2].Second.Value -eq 300) 'Target 3 must not shift after target 2 fails.'

$eightPoints = [System.Collections.Generic.List[TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]]::new()
$eightLines = [System.Collections.Generic.List[TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]]::new()
for ($targetIndex = 1; $targetIndex -le 8; $targetIndex++) {
    $eightPoints.Add([TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]::new($targetIndex, $true, $targetIndex * 10, ''))
    $eightLines.Add([TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]::new($targetIndex, $true, $targetIndex * 100, ''))
}

$oneLine = [System.Collections.Generic.List[TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]]::new()
$oneLine.Add([TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]::new(1, $true, 100, ''))
$pointToCommonLinePairs = [TDJS_Vision.Node._4_Measurement.Common.MultiTargetMeasurementPairer]::PairByTargetIndex($eightPoints, $oneLine, 'point', 'line')
Assert-True ($pointToCommonLinePairs.Count -eq 8) 'Eight points and one common line must produce eight pairs.'
Assert-True ($pointToCommonLinePairs[7].IsOk -and $pointToCommonLinePairs[7].TargetIndex -eq 8 -and $pointToCommonLinePairs[7].First.Value -eq 80 -and $pointToCommonLinePairs[7].Second.Value -eq 100) 'The common line must be broadcast to point target 8.'

$onePoint = [System.Collections.Generic.List[TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]]::new()
$onePoint.Add([TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]::new(1, $true, 10, ''))
$commonPointToLinePairs = [TDJS_Vision.Node._4_Measurement.Common.MultiTargetMeasurementPairer]::PairByTargetIndex($onePoint, $eightLines, 'point', 'line')
Assert-True ($commonPointToLinePairs.Count -eq 8) 'One common point and eight lines must produce eight pairs.'
Assert-True ($commonPointToLinePairs[7].IsOk -and $commonPointToLinePairs[7].TargetIndex -eq 8 -and $commonPointToLinePairs[7].First.Value -eq 10 -and $commonPointToLinePairs[7].Second.Value -eq 800) 'The common point must be broadcast to line target 8.'

$failedSingleLine = [System.Collections.Generic.List[TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]]::new()
$failedSingleLine.Add([TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]::new(1, $false, 0, 'common line failed'))
$failedBroadcastPairs = [TDJS_Vision.Node._4_Measurement.Common.MultiTargetMeasurementPairer]::PairByTargetIndex($eightPoints, $failedSingleLine, 'point', 'line')
Assert-True ($failedBroadcastPairs.Count -eq 8) 'A failed common line must still retain all point target positions.'
Assert-True (-not $failedBroadcastPairs[7].IsOk -and $failedBroadcastPairs[7].Second.ErrorMessage -eq 'common line failed') 'A failed common line must preserve its original NG reason for every target.'

$missingLines = [System.Collections.Generic.List[TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]]::new()
$missingLines.Add([TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]::new(1, $true, 100, ''))
$missingLines.Add([TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]::new(3, $true, 300, ''))
$missingPairs = [TDJS_Vision.Node._4_Measurement.Common.MultiTargetMeasurementPairer]::PairByTargetIndex($points, $missingLines, 'point', 'line')
Assert-True ($missingPairs.Count -eq 3) 'A missing side must retain the union of target indexes.'
Assert-True (-not $missingPairs[1].IsOk -and $missingPairs[1].Second.ErrorMessage.Contains('2')) 'A missing target 2 line must create an explicit NG placeholder.'

$duplicateRejected = $false
try {
    $duplicatePoints = [System.Collections.Generic.List[TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]]::new()
    $duplicatePoints.Add([TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]::new(1, $true, 10, ''))
    $duplicatePoints.Add([TDJS_Vision.Node._4_Measurement.Common.IndexedMeasurementValue[int]]::new(1, $true, 20, ''))
    [void][TDJS_Vision.Node._4_Measurement.Common.MultiTargetMeasurementPairer]::PairByTargetIndex($duplicatePoints, $lines, 'point', 'line')
}
catch {
    $duplicateRejected = $_.Exception.Message.Contains('1')
}
Assert-True $duplicateRejected 'Duplicate target indexes must be rejected.'

$project = Get-Content -LiteralPath $projectPath -Raw -Encoding UTF8
$form = Get-Content -LiteralPath $formPath -Raw -Encoding UTF8
$reader = Get-Content -LiteralPath $readerPath -Raw -Encoding UTF8
Assert-ContainsText $project 'Node\4-Measurement\Common\MultiTargetMeasurementPairer.cs' 'The project must compile the pairer.'
Assert-ContainsText $form 'ReadSubscribedPointTargets' 'Point-line distance must read multi-target point items.'
Assert-ContainsText $form 'ReadSubscribedLineTargets' 'Point-line distance must read multi-target line items.'
Assert-ContainsText $form 'PairByTargetIndex' 'Point-line distance must pair by TargetIndex.'
Assert-ContainsText $form 'ExecuteSubscribedMeasures' 'Subscribe mode must execute all target pairs.'
Assert-ContainsText $reader 'TryReadMultiTargetItems' 'The shared reader must expose multi-target items.'
Assert-ContainsText $reader 'TryReadPoint(object source' 'The shared reader must read a point from one target item.'
Assert-ContainsText $reader 'TryReadLine(object source' 'The shared reader must read a line from one target item.'

Write-Host 'Point-line multi-target subscription checks passed.'
