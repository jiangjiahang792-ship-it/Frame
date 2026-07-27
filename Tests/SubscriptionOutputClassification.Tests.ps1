$ErrorActionPreference = 'Stop'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

$projectRoot = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    (Get-Location).Path
}
else {
    Split-Path -Parent $PSScriptRoot
}
$nodeRoot = Join-Path $projectRoot 'Node'
$files = Get-ChildItem -LiteralPath $nodeRoot -Recurse -Filter '*.cs' -File
$displayCount = 0
$classifiedCount = 0
$hiddenCount = 0
$unclassified = New-Object System.Collections.Generic.List[string]

foreach ($file in $files) {
    $lines = Get-Content -LiteralPath $file.FullName -Encoding UTF8
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if (-not $lines[$index].Contains('[DisplayName(')) {
            continue
        }

        $displayCount++
        $previous = $index - 1
        while ($previous -ge 0 -and [string]::IsNullOrWhiteSpace($lines[$previous])) {
            $previous--
        }

        if ($previous -ge 0 -and $lines[$previous].Contains('[SubscriptionOutput')) {
            $classifiedCount++
            if ($lines[$previous].Contains('SubscriptionOutputVisibility.Hidden')) {
                $hiddenCount++
            }
        }
        else {
            $relativePath = $file.FullName.Substring($projectRoot.Length + 1)
            $unclassified.Add("${relativePath}:$($index + 1)")
        }
    }
}

Assert-True ($displayCount -eq 230) "当前静态公开结果数量应为 230，实际为 $displayCount。"
Assert-True ($classifiedCount -eq $displayCount) ("存在未分类输出：" + ($unclassified -join ', '))
Assert-True ($hiddenCount -eq 46) "隐藏结果数量应为 46，实际为 $hiddenCount。"

$caliperLine = Get-Content -LiteralPath (Join-Path $nodeRoot '4-Measurement\CaliperLine\NodeResultCaliperLine.cs') -Raw -Encoding UTF8
$findPoint = Get-Content -LiteralPath (Join-Path $nodeRoot '4-Measurement\FindPoint\NodeResultFindPoint.cs') -Raw -Encoding UTF8
$matchTemplate = Get-Content -LiteralPath (Join-Path $nodeRoot '3-Detection\MatchTemplate\NodeResultMatchTemplate.cs') -Raw -Encoding UTF8
$positionCorrection = Get-Content -LiteralPath (Join-Path $nodeRoot '4-Measurement\Common\MultiTargetPositionCorrectionResult.cs') -Raw -Encoding UTF8

Assert-True ($caliperLine.Contains('SubscriptionDataCategory.PointCollection')) '卡尺找线边缘点必须明确归类为点集合。'
Assert-True ($findPoint.Contains('SubscriptionDataCategory.Region')) '找点区域点集必须明确归类为区域。'
Assert-True ($findPoint.Contains('SubscriptionDataCategory.Contour')) '找点轮廓集合必须明确归类为轮廓。'
Assert-True ($matchTemplate.Contains('SubscriptionDataCategory.Pose')) '模板位姿列表必须明确归类为位姿。'
Assert-True ($positionCorrection.Contains('SubscriptionDataCategory.PositionCorrection')) '位置修正列表必须明确归类为位置修正。'

Write-Host 'Subscription output classification checks passed.'
