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

$parsePath = Join-Path $projectRoot 'Node\3-Detection\TDAI\Parse\LineCoreFront_C1_Seg.cs'
$source = Get-Content -LiteralPath $parsePath -Raw -Encoding UTF8

$usesDisplayResolver = $source.Contains('ResolveCountDisplayValue(item, valueStr, param.IsAutoStudy, isOk)')
$hasDisplayResolver = $source.Contains('private static string ResolveCountDisplayValue(')
$keepsRealtimeForStudyOrNg = $source.Contains('if (isAutoStudy || !isOk)')
$writesDisplayValue = $source.Contains('ParseCommon.SetDetectItemCurValue(ref param, itemName, displayValueStr)')

Assert-True -Condition $usesDisplayResolver -Message '线芯数量OK显示必须通过学习值解析函数。'
Assert-True -Condition $hasDisplayResolver -Message '线芯数量解析器必须提供独立的学习值显示函数。'
Assert-True -Condition $keepsRealtimeForStudyOrNg -Message '自动学习或NG时必须显示实时检测数量。'
Assert-True -Condition $writesDisplayValue -Message '线芯数量当前值必须写入最终显示值，避免OK时覆盖为实时抖动数量。'

Write-Host 'LineCoreFront count display regression checks passed.'
