$ErrorActionPreference = 'Stop'

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Near {
    param(
        [double]$Actual,
        [double]$Expected,
        [string]$Message,
        [double]$Tolerance = 0.001
    )

    if ([Math]::Abs($Actual - $Expected) -gt $Tolerance) {
        throw "$Message Actual: $Actual, Expected: $Expected."
    }
}

$posePath = Join-Path $PSScriptRoot '..\Node\3-Detection\MatchTemplate\TemplateMatchPose.cs'
$correctionPath = Join-Path $PSScriptRoot '..\Node\4-Measurement\Common\PositionCorrectionInfo.cs'
$servicePath = Join-Path $PSScriptRoot '..\Node\4-Measurement\Common\MultiTargetTransformService.cs'

foreach ($path in @($posePath, $correctionPath, $servicePath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Multi-target transform production file does not exist: $path"
    }
}

$sourceParts = @(
    Get-Content -LiteralPath $posePath -Raw -Encoding UTF8
    Get-Content -LiteralPath $correctionPath -Raw -Encoding UTF8
    Get-Content -LiteralPath $servicePath -Raw -Encoding UTF8
)
$sourceBody = ($sourceParts | ForEach-Object { $_ -replace '(?m)^using [^;]+;\r?\n', '' }) -join [Environment]::NewLine
$source = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using TDJS_Vision.Node._3_Detection.MatchTemplate;
'@ + [Environment]::NewLine + $sourceBody

Add-Type -TypeDefinition $source -Language CSharp -ReferencedAssemblies System.Drawing

$basePose = New-Object TDJS_Vision.Node._3_Detection.MatchTemplate.TemplateMatchPose
$basePose.TargetIndex = 1
$basePose.CenterX = 100
$basePose.CenterY = 100
$basePose.Angle = 0
$basePose.ScaleX = 1
$basePose.ScaleY = 1
$basePose.Width = 40
$basePose.Height = 40
$basePose.IsValid = $true

$secondPose = New-Object TDJS_Vision.Node._3_Detection.MatchTemplate.TemplateMatchPose
$secondPose.TargetIndex = 2
$secondPose.CenterX = 220
$secondPose.CenterY = 140
$secondPose.Angle = 30
$secondPose.ScaleX = 0
$secondPose.ScaleY = -2
$secondPose.Width = 60
$secondPose.Height = 30
$secondPose.IsValid = $true

$firstCorrection = [TDJS_Vision.Node._4_Measurement.Common.PositionCorrectionInfo]::FromPoses($basePose, $basePose)
$secondCorrection = [TDJS_Vision.Node._4_Measurement.Common.PositionCorrectionInfo]::FromPoses($basePose, $secondPose)

Assert-Near $secondCorrection.CurrentScaleX 1 'ScaleX zero must normalize to one.'
Assert-Near $secondCorrection.CurrentScaleY 1 'ScaleY negative must normalize to one.'

$service = New-Object TDJS_Vision.Node._4_Measurement.Common.MultiTargetTransformService
$sourcePoint = New-Object System.Drawing.PointF(110, 100)
$transformedPoint = $service.TransformPoint($sourcePoint, $secondCorrection)
$restoredPoint = $service.InverseTransformPoint($transformedPoint, $secondCorrection)

Assert-Near $restoredPoint.X $sourcePoint.X 'Round trip must restore X.'
Assert-Near $restoredPoint.Y $sourcePoint.Y 'Round trip must restore Y.'

$corrections = New-Object 'System.Collections.Generic.List[TDJS_Vision.Node._4_Measurement.Common.PositionCorrectionInfo]'
$corrections.Add($firstCorrection)
$corrections.Add($secondCorrection)

$insideSecond = New-Object System.Drawing.PointF(220, 140)
$insideIndex = $service.ResolveAnchorIndex($insideSecond, $corrections)
Assert-True ($insideIndex -eq 1) 'The ROI inside target two must resolve index one.'

$outsideNearFirst = New-Object System.Drawing.PointF(60, 60)
$outsideIndex = $service.ResolveAnchorIndex($outsideNearFirst, $corrections)
Assert-True ($outsideIndex -eq 0) 'An outside ROI must resolve the nearest target.'

$points = New-Object 'System.Collections.Generic.List[System.Drawing.PointF]'
$points.Add((New-Object System.Drawing.PointF(100, 100)))
$points.Add((New-Object System.Drawing.PointF(110, 100)))
$transformedPoints = $service.TransformPoints($points, $secondCorrection)
Assert-True ($transformedPoints.Count -eq 2) 'Point transform must preserve count.'

Write-Host 'Multi-target transform behavior checks passed.'
