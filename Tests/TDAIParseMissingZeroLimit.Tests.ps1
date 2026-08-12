$ErrorActionPreference = "Stop"

function Assert-ParserUsesZeroLimitFallback {
    param(
        [string]$RelativePath
    )

    $sourcePath = Join-Path $PSScriptRoot "..\$RelativePath"
    $source = Get-Content -Path $sourcePath -Raw -Encoding UTF8

    if (-not $source.Contains("ParseCommon.IsValueWithinLimits(item, count)")) {
        throw "$RelativePath must judge count items by configured min/max, including count 0."
    }

    if (-not $source.Contains("ParseCommon.IsValueWithinLimits(item, 0F)")) {
        throw "$RelativePath must judge missing non-count items as value 0 by configured min/max."
    }

    if (-not $source.Contains('ParseCommon.SetDetectItemCurValue(ref param, itemName, "0")')) {
        throw "$RelativePath must write current value 0 when a configured item is not detected."
    }

    if ($source.Contains("ngCountItems")) {
        throw "$RelativePath must not bypass configured min/max with hard-coded no-result count items."
    }
}

Assert-ParserUsesZeroLimitFallback "Node\3-Detection\TDAI\Parse\RL12Parse.cs"
Assert-ParserUsesZeroLimitFallback "Node\3-Detection\TDAI\Parse\RL12angondingParse.cs"
Assert-ParserUsesZeroLimitFallback "Node\3-Detection\TDAI\Parse\XMSGParse.cs"
Assert-ParserUsesZeroLimitFallback "Node\3-Detection\TDAI\Parse\ClosingTerminals_encrypted_Parse.cs"

Write-Host "TDAI missing-detection zero-limit regression checks passed."
