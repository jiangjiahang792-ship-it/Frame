$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$paramPath = Join-Path $projectRoot 'Node\3-Detection\MatchTemplate\NodeParamMatchTemplate.cs'
$resultPath = Join-Path $projectRoot 'Node\3-Detection\MatchTemplate\NodeResultMatchTemplate.cs'
$nodePath = Join-Path $projectRoot 'Node\3-Detection\MatchTemplate\NodeMatchTemplate.cs'
$formPath = Join-Path $projectRoot 'Node\3-Detection\MatchTemplate\NodeParamFormMatchTemplate.cs'
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

$paramSource = Get-Content -LiteralPath $paramPath -Encoding UTF8 -Raw
$resultSource = Get-Content -LiteralPath $resultPath -Encoding UTF8 -Raw
$nodeSource = Get-Content -LiteralPath $nodePath -Encoding UTF8 -Raw
$formSource = Get-Content -LiteralPath $formPath -Encoding UTF8 -Raw
$designerSource = Get-Content -LiteralPath $designerPath -Encoding UTF8 -Raw

Assert-Contains $paramSource 'MatchTemplateSortMode' 'Template matching parameters must declare the multi-target sort mode.'
Assert-Contains $paramSource 'ColumnAscending' 'Template matching must support column sort output by center X ascending.'
Assert-Contains $paramSource 'RowAscending' 'Template matching must support row sort output by center Y ascending.'
Assert-Contains $paramSource 'SortMode' 'Template matching parameters must save the selected sort mode.'

Assert-Contains $nodeSource 'OrderMatches' 'Template matching results must be sorted before target numbering and downstream output.'
Assert-Contains $nodeSource 'm.Box.CenterX' 'Column sort must use match center X.'
Assert-Contains $nodeSource 'm.Box.CenterY' 'Row sort must use match center Y.'
Assert-Contains $nodeSource 'BuildTargetVariableNames' 'Template matching node must expose sorted target coordinates at configuration time.'
Assert-Contains $nodeSource 'param.ResultNum' 'Template matching dynamic output count must follow the maximum match count.'

Assert-Contains $resultSource 'IDynamicResultVariables' 'Template matching result must publish dynamic variables for arithmetic subscriptions.'
Assert-Contains $resultSource 'TryGetDynamicVariable' 'Template matching result must allow arithmetic operation to read sorted target coordinates.'
Assert-Contains $resultSource 'TargetCenterXName' 'Dynamic output names must include target center X names.'
Assert-Contains $resultSource 'TargetCenterYName' 'Dynamic output names must include target center Y names.'

Assert-Contains $formSource 'comboBoxSortMode' 'Template matching parameter form must save and restore the sort mode control.'
Assert-Contains $designerSource 'comboBoxSortMode' 'Template matching designer must include the sort mode combo box.'

Write-Host 'Match template sorted dynamic output contract checks passed.'
