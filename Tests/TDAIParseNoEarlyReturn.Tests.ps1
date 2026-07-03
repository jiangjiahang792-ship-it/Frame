$ErrorActionPreference = "Stop"

function Assert-BiteGlueLengthDoesNotReturnWholeParse {
    param(
        [string]$RelativePath
    )

    $sourcePath = Join-Path $PSScriptRoot "..\$RelativePath"
    $source = Get-Content -Path $sourcePath -Raw -Encoding UTF8
    $startMarker = "if (itemName == ""DetectItem.BiteGlueLength"")"
    $endMarker = "if (param.NeedConvert)"
    $startIndex = $source.IndexOf($startMarker)

    if ($startIndex -lt 0) {
        throw "$RelativePath is missing the BiteGlueLength branch."
    }

    $endIndex = $source.IndexOf($endMarker, $startIndex)
    if ($endIndex -lt 0) {
        throw "$RelativePath is missing the BiteGlueLength conversion boundary."
    }

    $biteGlueBlock = $source.Substring($startIndex, $endIndex - $startIndex)
    if ($biteGlueBlock.Contains("return;")) {
        throw "$RelativePath must not return the whole AI parser when BiteGlueLength is missing dependency boxes."
    }
}

Assert-BiteGlueLengthDoesNotReturnWholeParse "Node\3-Detection\TDAI\Parse\RL12Parse.cs"
Assert-BiteGlueLengthDoesNotReturnWholeParse "Node\3-Detection\TDAI\Parse\RL12angondingParse.cs"
Assert-BiteGlueLengthDoesNotReturnWholeParse "Node\3-Detection\TDAI\Parse\XMSGParse.cs"

Write-Host "TDAI BiteGlueLength no-early-return regression checks passed."
