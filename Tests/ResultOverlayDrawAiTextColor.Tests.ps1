$ErrorActionPreference = "Stop"

$root = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    (Get-Location).Path
}
else {
    Split-Path -Parent $PSScriptRoot
}

$builderPath = Join-Path $root "Node\7-ResultProcessing\ResultOverlayDraw\NodeResultOverlayDraw.cs"
$builder = Get-Content -Raw -Encoding UTF8 $builderPath

function Assert-Contains {
    param([string]$Content, [string]$Pattern, [string]$Message)
    if ($Content -notlike "*$Pattern*") {
        throw $Message
    }
}

$appendTextStart = $builder.IndexOf("private static void AppendText(", [StringComparison]::Ordinal)
$autoRoiStart = $builder.IndexOf("private static void AppendAutomaticRoi(", [StringComparison]::Ordinal)
if ($appendTextStart -lt 0 -or $autoRoiStart -le $appendTextStart) {
    throw "Cannot locate AppendText body."
}

$appendText = $builder.Substring($appendTextStart, $autoRoiStart - $appendTextStart)

Assert-Contains $appendText "AlgorithmResult sourceAlgorithmResult = TryGetAlgorithmResult(sourceValue);" "Text drawing must detect direct AlgorithmResult subscription."
Assert-Contains $appendText "AppendAlgorithmResultTexts(displayResult, sourceAlgorithmResult, item);" "AI result text drawing must preserve per-line ColorText colors."
Assert-Contains $appendText "return;" "AI result text drawing must not fall through to the unified source OK/NG color path."
Assert-Contains $builder "new ColorText(displayText, textInfo.Color)" "AI result text copy must keep original ColorText color."
Assert-Contains $builder "result.IsOk ? item.OkColor : item.NgColor" "DetectResults fallback must color each detection item by its own IsOk value."

Write-Host "ResultOverlayDraw AI text color checks passed."
