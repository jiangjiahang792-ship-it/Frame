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

$parsePath = Join-Path $projectRoot 'Node\3-Detection\TDAI\Parse\MultiTerminalParse.cs'
$zhPath = Join-Path $projectRoot 'Languages\zh-CN.json'
$source = Get-Content -LiteralPath $parsePath -Raw -Encoding UTF8
$zhSource = Get-Content -LiteralPath $zhPath -Raw -Encoding UTF8
$caseMatches = [regex]::Matches($source, 'case\s+"([^"]+)":')

Assert-True ($source.Contains('switch (itemName)')) 'MultiTerminalParse must keep itemName switch branches.'
Assert-True ($caseMatches.Count -eq 14) "MultiTerminalParse should expose 14 readable switch branches, actual: $($caseMatches.Count)."
Assert-True (-not ([regex]::IsMatch($source, 'case\s+"DetectItem\.'))) 'MultiTerminalParse switch branches must not use internal DetectItem keys.'
Assert-True ($source.Contains('string itemKey = item.Name;')) 'MultiTerminalParse must keep internal itemKey for persistence.'
Assert-True ($source.Contains('detectResults[itemKey] = singleResults;')) 'MultiTerminalParse result dictionary must keep internal keys.'
Assert-True ($source.Contains('Name = itemKey')) 'MultiTerminalParse SingleDetectResult names must keep internal keys.'
Assert-True ($zhSource.Contains('"DetectItem.FrontRivetCoreHeight"')) 'zh-CN language file must include FrontRivetCoreHeight.'

Write-Host 'MultiTerminalParse Chinese switch checks passed.'
