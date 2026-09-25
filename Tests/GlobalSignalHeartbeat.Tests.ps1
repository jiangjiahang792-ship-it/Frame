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

$formSource = Get-ProjectSource 'Forms\GlobalSignalSettings\FormGlobalSignal.cs'
$designerSource = Get-ProjectSource 'Forms\GlobalSignalSettings\FormGlobalSignal.Designer.cs'
$formMainSource = Get-ProjectSource 'FormMain.cs'
$configHelperSource = Get-ProjectSource 'ConfigHelper.cs'
$solutionSource = Get-ProjectSource 'Solution.cs'
$taskRecordSource = Get-ProjectSource '任务记录.md'

Assert-Contains $designerSource 'this.groupBox4.Text = "心跳信号";' '全局信号界面必须新增心跳信号分组。'
Assert-Contains $designerSource 'this.heartbeatCameraColumn.HeaderText = "选择相机";' '心跳表格必须包含选择相机列。'
Assert-Contains $designerSource 'this.heartbeatDeviceColumn.HeaderText = "通信设备";' '心跳表格必须包含通信设备列。'
Assert-Contains $designerSource 'this.heartbeatAddressColumn.HeaderText = "地址";' '心跳表格必须包含地址列。'
Assert-Contains $designerSource 'this.heartbeatTypeColumn.HeaderText = "写入类型";' '心跳表格必须包含写入类型列。'
Assert-Contains $designerSource 'this.heartbeatIntervalColumn.HeaderText = "间隔时间";' '心跳表格必须包含间隔时间列。'
Assert-Contains $designerSource 'this.heartbeatOnValueColumn.HeaderText = "On值";' '心跳表格必须包含On值列。'
Assert-Contains $designerSource 'this.heartbeatOffValueColumn.HeaderText = "Off值";' '心跳表格必须包含Off值列。'

Assert-Contains $formSource 'public List<HeartbeatGlobalSignalSettings> HeartbeatSignals { get; set; }' '全局信号配置必须持久化心跳信号列表。'
Assert-Contains $formSource 'public class HeartbeatGlobalSignalSettings' '心跳信号必须使用独立配置类保存相机、通信设备和周期。'
Assert-Contains $formSource 'public string Type { get; set; }' '心跳信号必须持久化写入类型。'
Assert-Contains $formSource 'public static void StartHeartbeatSignals()' '必须提供启动心跳后台发送的公共入口。'
Assert-Contains $formSource 'public static void StopHeartbeatSignals()' '必须提供停止旧心跳后台发送的公共入口。'
Assert-Contains $formSource 'await WriteHeartbeatValueAsync(signal, signal.OnValue, token);' '心跳循环必须按配置写入On值。'
Assert-Contains $formSource 'await WriteHeartbeatValueAsync(signal, signal.OffValue, token);' '心跳循环必须按配置写入Off值。'
Assert-Contains $formSource 'await Task.Delay(intervalMilliseconds, token);' '心跳On/Off之间必须按间隔时间等待。'
$heartbeatTypeBlock = Get-SourceBlock $designerSource '// heartbeatTypeColumn' 'this.heartbeatTypeColumn.MinimumWidth = 6;'
Assert-Contains $heartbeatTypeBlock '"布尔",' '心跳写入类型必须与PLC写入节点统一，包含布尔。'
Assert-Contains $heartbeatTypeBlock '"Int16",' '心跳写入类型必须与PLC写入节点统一，包含Int16。'
Assert-Contains $heartbeatTypeBlock '"UInt16",' '心跳写入类型必须与PLC写入节点统一，包含UInt16。'
Assert-Contains $heartbeatTypeBlock '"Int32",' '心跳写入类型必须与PLC写入节点统一，包含Int32。'
Assert-Contains $heartbeatTypeBlock '"UInt32",' '心跳写入类型必须与PLC写入节点统一，包含UInt32。'
Assert-Contains $heartbeatTypeBlock '"浮点",' '心跳写入类型必须与PLC写入节点统一，包含浮点。'
Assert-Contains $heartbeatTypeBlock '"字符串"' '心跳写入类型必须与PLC写入节点统一，包含字符串。'
Assert-NotContains $heartbeatTypeBlock '"整数"' '心跳写入类型不能再显示不明确的整数，应显示Int32。'
Assert-NotContains $heartbeatTypeBlock '"整数类型"' '心跳写入类型不能再显示整数类型，应显示整数。'
Assert-NotContains $heartbeatTypeBlock '"short类型"' '心跳写入类型不能再显示short类型。'
Assert-NotContains $heartbeatTypeBlock '"ushort类型"' '心跳写入类型不能再显示ushort类型。'
Assert-NotContains $heartbeatTypeBlock '"浮点类型"' '心跳写入类型不能再显示浮点类型，应显示浮点。'
Assert-NotContains $heartbeatTypeBlock '"字符串类型"' '心跳写入类型不能再显示字符串类型，应显示字符串。'
Assert-Contains $formSource 'switch (NormalizeHeartbeatType(signal.Type))' '心跳写入必须先标准化旧方案类型文本再分发。'
Assert-Contains $formSource 'case "Int16":' '心跳PLC/Modbus写入必须支持Int16。'
Assert-Contains $formSource 'case "UInt16":' '心跳PLC/Modbus写入必须支持UInt16。'
Assert-Contains $formSource 'case "Int32":' '心跳PLC/Modbus写入必须支持Int32。'
Assert-Contains $formSource 'case "UInt32":' '心跳PLC/Modbus写入必须支持UInt32。'
Assert-Contains $formSource 'if (!IsHeartbeatCameraReady(signal))' '相机掉线时必须暂停心跳发送，重连后循环继续。'
Assert-Contains $formSource 'SaveHeartbeatSignalsAndRefresh();' '心跳配置保存后必须刷新后台发送线程。'
Assert-Contains $formSource 'LoadHeartbeatConfig(dataGridView4, Solution.Instance.GlobalSignal.HeartbeatSignals);' '打开心跳界面时必须加载已保存配置。'
Assert-Contains $formSource 'dataGridView4.DataError += DataGridView_DataError;' '心跳表格必须拦截DataGridView默认错误弹窗。'
Assert-Contains $formSource 'e.ThrowException = false;' '全局信号表格DataError必须禁止继续抛出默认异常对话框。'
Assert-Contains $formSource 'BuildCommunicationDeviceNameOptions()' '通信设备下拉必须补齐方案中已保存的旧设备名。'
Assert-Contains $formSource 'BuildHeartbeatCameraNameOptions()' '心跳相机下拉必须补齐方案中已保存的旧相机名。'
Assert-Contains $formSource 'AddUniqueNonEmpty(names, signal.CameraName);' '心跳相机下拉必须包含已保存但当前设备列表中不存在的相机名。'
Assert-Contains $formSource 'AddUniqueNonEmpty(names, signal.DeviceName);' '心跳通信设备下拉必须包含已保存但当前设备列表中不存在的设备名。'

Assert-Contains $configHelperSource 'FormGlobalSignal.StartHeartbeatSignals();' '方案加载成功后必须在统一加载入口自动启动心跳。'
Assert-Contains $formMainSource 'FormGlobalSignal.StopHeartbeatSignals();' '主窗体关闭前必须停止心跳线程。'
Assert-Contains $solutionSource 'FormGlobalSignal.StopHeartbeatSignals();' '方案重置或切换方案时必须先停止旧心跳。'
Assert-Contains $taskRecordSource '全局信号心跳设置' '任务记录必须登记全局信号心跳设置改动。'

Write-Host 'Global signal heartbeat source checks passed.'
