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

$resultPath = Join-Path $projectRoot 'Node\3-Detection\ColorDiscern\NodeResultColorDiscern.cs'
$nodePath = Join-Path $projectRoot 'Node\3-Detection\ColorDiscern\NodeColorDiscern.cs'
$multiConditionFormPath = Join-Path $projectRoot 'Node\6-LogicTool\MultiCondition\NodeParamFormMultiCondition.cs'

$resultSource = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8
$nodeSource = Get-Content -LiteralPath $nodePath -Raw -Encoding UTF8
$multiConditionFormSource = Get-Content -LiteralPath $multiConditionFormPath -Raw -Encoding UTF8

Assert-True $resultSource.Contains('NodeResultColorDiscern : INodeResult, IJudgmentResult') '颜色识别结果必须支持统一判定回写。'
Assert-True $resultSource.Contains('[SubscriptionOutput(SubscriptionDataCategory.AlgorithmResult)]') '颜色识别结构化结果必须明确声明为算法结果。'
Assert-True $resultSource.Contains('[DisplayName("整体判定OK")]') '颜色识别必须公开整体判定布尔出口。'
Assert-True $resultSource.Contains('public bool JudgeOk { get; set; }') '颜色识别必须公开可回写的最终判定值。'
Assert-True $nodeSource.Contains('pendingResult.IsOk = isOk;') '颜色识别运行结果必须保存LAB和线序的原始整体判定。'
Assert-True $nodeSource.Contains('pendingResult.JudgeOk = isOk;') '颜色识别最终判定初值必须与原始整体判定一致。'
Assert-True $multiConditionFormSource.Contains('SubscriptionDataCategory.Boolean') '多条件判定必须继续接受布尔订阅。'

Write-Host '颜色识别多条件订阅回归检查通过。'

