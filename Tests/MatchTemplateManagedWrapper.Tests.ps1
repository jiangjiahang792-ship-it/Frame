$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$matcherPath = Join-Path $projectRoot 'Node\3-Detection\MatchTemplate\FastTemplateMatcher.cs'
$designerPath = Join-Path $projectRoot 'Node\3-Detection\MatchTemplate\NodeParamFormMatchTemplate.Designer.cs'

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Expected,
        [string]$Message
    )

    Assert-True -Condition ($Text.Contains($Expected)) -Message $Message
}

function Assert-NotContains {
    param(
        [string]$Text,
        [string]$Unexpected,
        [string]$Message
    )

    Assert-True -Condition (-not $Text.Contains($Unexpected)) -Message $Message
}

$matcherSource = Get-Content -LiteralPath $matcherPath -Encoding UTF8 -Raw
$designerSource = Get-Content -LiteralPath $designerPath -Encoding UTF8 -Raw

Assert-Contains $matcherSource 'GetMatchApiVersion' 'Managed wrapper must validate the new native API version.'
Assert-Contains $matcherSource 'GetMatchStatusMessage' 'Managed wrapper must expose native diagnostic text.'
Assert-Contains $matcherSource 'SavePatternFile' 'Managed wrapper must expose learned-pattern saving.'
Assert-Contains $matcherSource 'LoadPatternFile' 'Managed wrapper must expose learned-pattern loading.'
Assert-Contains $matcherSource 'if (count < 0)' 'Managed wrapper must treat native negative return values as errors.'
Assert-Contains $matcherSource 'throw CreateNativeException(' 'Managed wrapper must report MatchMem errors with native status text.'
Assert-Contains $matcherSource ', count);' 'Managed wrapper must pass the native MatchMem status code into the error formatter.'
Assert-NotContains $matcherSource 'Math.Max(0, Math.Min(count, maxResults))' 'Managed wrapper must not convert native errors into zero matches.'

Assert-Contains $designerSource 'labelToleranceAngle' 'Run-parameter designer must expose the new angle range control.'
Assert-Contains $designerSource 'textBoxToleranceAngle' 'Run-parameter designer must expose the new angle range input.'
Assert-NotContains $designerSource 'comboBoxPolarity' 'Old polarity parameter control must be removed from the designer.'
Assert-NotContains $designerSource 'comboBoxScaleMode' 'Old scale-mode placeholder control must be removed from the designer.'

Write-Host 'Match template managed wrapper and parameter designer checks passed.'
