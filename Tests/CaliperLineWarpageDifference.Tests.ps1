$ErrorActionPreference = 'Stop'

function Assert-ContainsText {
    param([string]$Content, [string]$Expected, [string]$Message)
    if (-not $Content.Contains($Expected)) {
        throw $Message
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$resultPath = Join-Path $projectRoot 'Node\4-Measurement\CaliperLine\NodeResultCaliperLine.cs'
$formPath = Join-Path $projectRoot 'Node\4-Measurement\CaliperLine\NodeParamFormCaliperLine.cs'
$nodePath = Join-Path $projectRoot 'Node\4-Measurement\CaliperLine\NodeCaliperLine.cs'

$result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8
$form = Get-Content -LiteralPath $formPath -Raw -Encoding UTF8
$node = Get-Content -LiteralPath $nodePath -Raw -Encoding UTF8
$warpageDisplayName = -join ([char[]](0x7FD8, 0x66F2, 0x5EA6, 0x5DEE, 0x503C))

Assert-ContainsText $result 'public double WarpageDifference { get; set; }' 'Target result must store warpage difference.'
Assert-ContainsText $result 'public double? WarpageDifference { get; set; }' 'Summary result must expose warpage difference.'
Assert-ContainsText $result ('[DisplayName("' + $warpageDisplayName + '")]') 'Warpage difference must use the expected display name.'
Assert-ContainsText $form 'CalculateWarpageDifference(' 'CaliperLine must calculate point-to-line distance range.'
Assert-ContainsText $form 'maxDistance - minDistance' 'Warpage difference must be max distance minus min distance.'
Assert-ContainsText $form 'CalculatePointLineDistance' 'Warpage difference must use perpendicular point-to-line distance.'
Assert-ContainsText $node 'result.WarpageDifference = first.WarpageDifference;' 'CaliperLine summary must publish warpage difference.'
Assert-ContainsText $node ($warpageDisplayName + ' {item.WarpageDifference:F3}px') 'Display text must include warpage difference.'

Write-Host 'CaliperLine warpage difference source checks passed.'
