$ErrorActionPreference = "Stop"

$root = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    (Get-Location).Path
}
else {
    Split-Path -Parent $PSScriptRoot
}
$paramPath = Join-Path $root "Node\6-LogicTool\MultiCondition\NodeParamMultiCondition.cs"
$nodePath = Join-Path $root "Node\6-LogicTool\MultiCondition\NodeMultiCondition.cs"
$formPath = Join-Path $root "Node\6-LogicTool\MultiCondition\NodeParamFormMultiCondition.cs"
$runParamPath = Join-Path $root "Forms\SolRunParam\SolRunParam.cs"

function Assert-Contains {
    param([string]$Content, [string]$Pattern, [string]$Message)
    if ($Content -notlike "*$Pattern*") {
        throw $Message
    }
}

function Assert-NotContains {
    param([string]$Content, [string]$Pattern, [string]$Message)
    if ($Content -like "*$Pattern*") {
        throw $Message
    }
}

$param = Get-Content -Raw -Encoding UTF8 $paramPath
$node = Get-Content -Raw -Encoding UTF8 $nodePath
$form = Get-Content -Raw -Encoding UTF8 $formPath
$runParam = Get-Content -Raw -Encoding UTF8 $runParamPath

Assert-Contains $param 'public bool Enabled { get; set; } = true;' '多条件检测项缺少独立的启用状态，旧方案也无法默认保持启用。'
Assert-Contains $param 'if (condition == null || !condition.Enabled)' '多条件执行器没有跳过已禁用条件。'
Assert-Contains $param 'if (enabledConditionCount == 0)' '多条件执行器没有处理全部条件禁用的边界。'
Assert-Contains $param 'return true;' '全部条件禁用时必须判定为 OK/True。'
Assert-Contains $node 'condition == null || !condition.Enabled' '已禁用条件不应继续建立运行依赖。'
Assert-Contains $form 'Enabled = condition.Enabled' '参数窗体重新保存时会丢失检测启用状态。'
Assert-Contains $form 'Enabled = Enabled' '参数窗体没有把检测启用状态写回参数。'
Assert-Contains $runParam 'Enable = condition.Enabled' '手动调参表仍把“是否显示”误当作“是否检测”。'
Assert-Contains $runParam 'condition.Enabled = item.Info.Enable;' '手动调参表没有把检测开关写回独立启用状态。'
Assert-NotContains $runParam 'condition.EnableRunParamAdjust = item.Info.Enable;' '手动调参保存仍会关闭“显示在运行参数表”开关，导致行消失。'

Write-Host "多条件手动禁用行为检查通过。"
