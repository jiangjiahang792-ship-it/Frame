$ErrorActionPreference = 'Stop'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Assert-ContainsText {
    param([string]$Content, [string]$Expected, [string]$Message)
    if (-not $Content.Contains($Expected)) {
        throw $Message
    }
}

$projectRoot = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    (Get-Location).Path
}
else {
    Split-Path -Parent $PSScriptRoot
}
$readResultPath = Join-Path $projectRoot 'Node\5-EquipmentCommunication\ModbusRead\NodeResultModbusRead.cs'
$readNodePath = Join-Path $projectRoot 'Node\5-EquipmentCommunication\ModbusRead\NodeModbusRead.cs'
$writeFormPath = Join-Path $projectRoot 'Node\5-EquipmentCommunication\ModbusWrite\ParamFormModbusWrite.cs'
$writeDesignerPath = Join-Path $projectRoot 'Node\5-EquipmentCommunication\ModbusWrite\ParamFormModbusWrite.Designer.cs'

$readResult = Get-Content -LiteralPath $readResultPath -Raw -Encoding UTF8
$readNode = Get-Content -LiteralPath $readNodePath -Raw -Encoding UTF8
$writeForm = Get-Content -LiteralPath $writeFormPath -Raw -Encoding UTF8
$writeDesigner = Get-Content -LiteralPath $writeDesignerPath -Raw -Encoding UTF8

Assert-ContainsText $readResult 'case RegistersType.Short:' 'Modbus 动态结果必须处理 Short 类型。'
Assert-ContainsText $readResult 'return typeof(short);' 'Modbus Short 动态变量必须发布真实 Int16 类型。'
Assert-ContainsText $readNode 'ModbusReadDynamicVariable.GetValueType(param.DataType)' 'Modbus 节点必须在运行前按参数发布动态变量真实类型。'

Assert-ContainsText $writeForm 'nodeSubscription1.SetInputContract' 'Modbus 写入必须根据寄存器类型声明订阅契约。'
Assert-ContainsText $writeForm 'nodeSubscription1.GetValue<object>()' 'Modbus 写入必须先保留订阅值的真实类型。'
Assert-ContainsText $writeForm 'FormatSubscribedValue' 'Modbus 写入必须统一格式化订阅数值。'
Assert-ContainsText $writeForm 'RefreshSubscriptionContract' 'Modbus 写入类型变化后必须即时刷新输入契约。'
Assert-True (-not $writeForm.Contains('nodeSubscription1.GetValue<bool>()')) 'Modbus 写入不能再把 Short 固定读取成 bool。'

Assert-ContainsText $writeDesigner 'this.comboBoxType.SelectedIndexChanged += new System.EventHandler(this.comboBoxType_SelectedIndexChanged);' 'Modbus 类型下拉框事件必须由 Designer 绑定。'

Write-Host 'Modbus numeric subscription checks passed.'
