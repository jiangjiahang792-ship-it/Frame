$ErrorActionPreference = 'Stop'

function Get-ProjectSource {
    param([string]$RelativePath)
    $sourcePath = Join-Path $PSScriptRoot "..\$RelativePath"
    return Get-Content -Path $sourcePath -Raw -Encoding UTF8
}

function Assert-Contains {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if (-not $Text.Contains($Pattern)) { throw $Message }
}

function Assert-NotContains {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if ($Text.Contains($Pattern)) { throw $Message }
}

function Get-SourceBlock {
    param([string]$Text, [string]$StartMarker, [string]$EndMarker)
    $startIndex = $Text.IndexOf($StartMarker)
    if ($startIndex -lt 0) { throw "未找到代码块开始标记：$StartMarker" }
    $endIndex = $Text.IndexOf($EndMarker, $startIndex)
    if ($endIndex -lt 0) { throw "未找到代码块结束标记：$EndMarker" }
    return $Text.Substring($startIndex, $endIndex - $startIndex)
}

$plcFormSource = Get-ProjectSource 'Node\5-EquipmentCommunication\PlcWrite\ParamFormPlcWrite.cs'
$plcNodeSource = Get-ProjectSource 'Node\5-EquipmentCommunication\PlcWrite\NodePlcWrite.cs'
$heartbeatDesignerSource = Get-ProjectSource 'Forms\GlobalSignalSettings\FormGlobalSignal.Designer.cs'
$heartbeatFormSource = Get-ProjectSource 'Forms\GlobalSignalSettings\FormGlobalSignal.cs'

Assert-Contains $plcFormSource 'comboBoxDataType.Items.Add("Int16");' 'PLC写入节点类型下拉必须包含Int16。'
Assert-Contains $plcFormSource 'comboBoxDataType.Items.Add("UInt16");' 'PLC写入节点类型下拉必须包含UInt16。'
Assert-Contains $plcFormSource 'comboBoxDataType.Items.Add("Int32");' 'PLC写入节点类型下拉必须包含Int32。'
Assert-Contains $plcFormSource 'comboBoxDataType.Items.Add("UInt32");' 'PLC写入节点类型下拉必须包含UInt32。'
Assert-NotContains $plcFormSource 'comboBoxDataType.Items.Add("整数");' 'PLC写入节点不能再使用不明确的“整数”选项。'
Assert-Contains $plcFormSource 'case "Int16":' 'PLC写入节点保存时必须识别Int16。'
Assert-Contains $plcFormSource 'case "UInt16":' 'PLC写入节点保存时必须识别UInt16。'
Assert-Contains $plcFormSource 'case "Int32":' 'PLC写入节点保存时必须识别Int32。'
Assert-Contains $plcFormSource 'case "UInt32":' 'PLC写入节点保存时必须识别UInt32。'

Assert-Contains $plcNodeSource 'dataType == typeof(short).Name' 'PLC写入运行时必须解析Int16。'
Assert-Contains $plcNodeSource 'dataType == typeof(ushort).Name' 'PLC写入运行时必须解析UInt16。'
Assert-Contains $plcNodeSource 'dataType == typeof(uint).Name' 'PLC写入运行时必须解析UInt32。'
Assert-Contains $plcNodeSource 'WriteTypedValuesAsync' 'PLC写入16位和UInt32必须走PLC显式类型写入接口。'
Assert-Contains $plcNodeSource 'short.Parse' 'PLC写入Int16必须按short范围校验。'
Assert-Contains $plcNodeSource 'ushort.Parse' 'PLC写入UInt16必须按ushort范围校验。'
Assert-Contains $plcNodeSource 'uint.Parse' 'PLC写入UInt32必须按uint范围校验。'

$heartbeatTypeBlock = Get-SourceBlock $heartbeatDesignerSource '// heartbeatTypeColumn' 'this.heartbeatTypeColumn.MinimumWidth = 6;'
Assert-Contains $heartbeatTypeBlock '"Int16",' '心跳写入类型必须包含Int16。'
Assert-Contains $heartbeatTypeBlock '"UInt16",' '心跳写入类型必须包含UInt16。'
Assert-Contains $heartbeatTypeBlock '"Int32",' '心跳写入类型必须包含Int32。'
Assert-Contains $heartbeatTypeBlock '"UInt32",' '心跳写入类型必须包含UInt32。'
Assert-NotContains $heartbeatTypeBlock '"整数"' '心跳写入类型不能再使用不明确的“整数”。'
Assert-Contains $heartbeatFormSource 'case "Int16":' '心跳写入必须支持Int16。'
Assert-Contains $heartbeatFormSource 'case "UInt16":' '心跳写入必须支持UInt16。'
Assert-Contains $heartbeatFormSource 'case "Int32":' '心跳写入必须支持Int32。'
Assert-Contains $heartbeatFormSource 'case "UInt32":' '心跳写入必须支持UInt32。'
Assert-Contains $heartbeatFormSource 'case "整数":' '心跳旧方案中的“整数”必须兼容。'
Assert-Contains $heartbeatFormSource 'return "Int32";' '心跳旧整数类型必须保持原32位语义。'

Write-Host 'PLC write integer width source checks passed.'
