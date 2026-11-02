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
Assert-Contains $appendText "AppendAlgorithmResultTexts(displayResult, sourceAlgorithmResult, item, param.OkColor, param.NgColor);" "AI result text drawing must use the overlay OK/NG palette."
Assert-Contains $appendText "return;" "AI result text drawing must not fall through to the unified source OK/NG color path."
Assert-Contains $builder "IsNgSourceColor(textInfo.Color) ? ngColor : okColor" "AI result text must map source judgment semantics to overlay colors."
Assert-Contains $builder "result.IsOk ? okColor : ngColor" "DetectResults fallback must use overlay colors by each detection item's IsOk value."

Write-Host "ResultOverlayDraw AI text color checks passed."
