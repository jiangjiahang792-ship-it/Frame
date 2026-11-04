$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$grid = Get-Content -Raw -Encoding UTF8 (Join-Path $root "Forms\SolRunParam\MyDataGridViewForm.cs")
$designer = Get-Content -Raw -Encoding UTF8 (Join-Path $root "Forms\SolRunParam\MyDataGridViewForm.Designer.cs")
$control = Get-Content -Raw -Encoding UTF8 (Join-Path $root "Forms\SolRunParam\SolRunParam.cs")
$detectItem = Get-Content -Raw -Encoding UTF8 (Join-Path $root "Node\3-Detection\TDAI\AIInputInfo.cs")
$condition = Get-Content -Raw -Encoding UTF8 (Join-Path $root "Node\6-LogicTool\MultiCondition\NodeParamMultiCondition.cs")

function Assert-Contains {
    param([string]$Content, [string]$Pattern, [string]$Message)
    if ($Content -notlike "*$Pattern*") { throw $Message }
}

Assert-Contains $designer 'this.hiddenColumn.HeaderText = "是否隐藏";' '设计器缺少最后的是否隐藏列。'
Assert-Contains $designer 'this.countColumn, this.hiddenColumn' '是否隐藏列没有放在表格最后。'
Assert-Contains $grid 'CurrentRole == UserRole.Manufacturer' '厂家专属权限判断缺失。'
Assert-Contains $grid 'hiddenColumn.Visible = isManufacturer;' '非厂家仍可能看到隐藏设置列。'
Assert-Contains $grid 'row.Visible = isManufacturer || item?.Info?.IsManualTuningHidden != true;' '非厂家没有过滤已隐藏参数。'
Assert-Contains $grid 'item.Info?.IsManualTuningHidden == true' '非厂家保存时没有保留原隐藏状态。'
Assert-Contains $detectItem 'public bool IsManualTuningHidden { get; set; }' 'AI检测项缺少隐藏状态持久化字段。'
Assert-Contains $condition 'public bool IsManualTuningHidden { get; set; }' '多条件项缺少隐藏状态持久化字段。'
Assert-Contains $control 'condition.IsManualTuningHidden = item.Info.IsManualTuningHidden;' '多条件隐藏状态没有写回方案。'

Write-Host "手动调参厂家专属隐藏权限检查通过。"
