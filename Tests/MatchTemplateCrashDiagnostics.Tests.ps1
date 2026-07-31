$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$matcherPath = Join-Path $projectRoot 'Node\3-Detection\MatchTemplate\FastTemplateMatcher.cs'
$formPath = Join-Path $projectRoot 'Node\3-Detection\MatchTemplate\NodeParamFormMatchTemplate.cs'

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

$matcherSource = Get-Content -LiteralPath $matcherPath -Encoding UTF8 -Raw
$formSource = Get-Content -LiteralPath $formPath -Encoding UTF8 -Raw

Assert-Contains $matcherSource 'MatchTemplateDiagnosticLog' 'Template matching must have a direct file diagnostic logger.'
Assert-Contains $matcherSource 'AppendAllText' 'Diagnostic logging must write directly to disk instead of only UI buffered logs.'
Assert-Contains $matcherSource 'CreateMatchEngine' 'Diagnostic logging must bracket native engine creation.'
Assert-Contains $matcherSource 'LearnPatternMem' 'Diagnostic logging must bracket native template learning.'
Assert-Contains $matcherSource 'MatchMem' 'Diagnostic logging must bracket native matching.'
Assert-Contains $matcherSource 'DescribeBitmap' 'Diagnostic logging must include image size and format details.'
Assert-Contains $matcherSource 'DescribeParam' 'Diagnostic logging must include runtime parameter details.'

Assert-Contains $formSource 'buttonRun_Click' 'Manual run handler must remain present.'
Assert-Contains $formSource 'WriteDiagnosticLog' 'Manual run path must call diagnostic logging.'
Assert-Contains $formSource 'CreateMatchTemplateContext' 'Context creation must remain a logged boundary before native matching.'
Assert-Contains $formSource 'ExecuteMatchTemplateContext' 'Native execution wrapper must remain a logged boundary.'
Assert-Contains $formSource 'ApplyMatchTemplateResult' 'Preview rendering must remain a logged boundary after native matching.'
Assert-Contains $formSource 'while (displayException.InnerException != null)' 'Template matching UI must unwrap nested native initialization errors for readable customer prompts.'

Write-Host 'Match template crash diagnostics checks passed.'
