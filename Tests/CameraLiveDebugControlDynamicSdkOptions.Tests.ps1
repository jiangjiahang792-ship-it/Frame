$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$controlPath = Join-Path $root "Forms\CameraAdd\CameraLiveDebugControl.cs"
$cameraInterfacePath = Join-Path $root "Device\Camera\ICamera.cs"
$cameraHikPath = Join-Path $root "Device\Camera\CameraHik.cs"

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if ($Text -notmatch [regex]::Escape($Pattern)) {
        throw $Message
    }
}

function Assert-Regex {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if ($Text -notmatch $Pattern) {
        throw $Message
    }
}

$control = Get-Content -LiteralPath $controlPath -Encoding UTF8 -Raw
$cameraInterface = Get-Content -LiteralPath $cameraInterfacePath -Encoding UTF8 -Raw
$cameraHik = Get-Content -LiteralPath $cameraHikPath -Encoding UTF8 -Raw

Assert-Contains $control "Load2DCameraSdkOptionItems()" "相机实时调试控件必须从 SDK 加载触发源、触发极性、线路选择和线路模式可选项。"
Assert-Contains $control "ApplyTriggerParameterVisibility()" "触发模式变化后必须联动隐藏无效触发参数。"
Assert-Contains $control "SetTableRowVisible(tableLayoutPanelTrigger, 2, triggerEnabled" "触发模式关闭时必须隐藏触发源行。"
Assert-Contains $control "SetTableRowVisible(tableLayoutPanelTrigger, 3, triggerEnabled && IsHardwareTriggerSource()" "非硬件线路触发时必须隐藏触发极性行。"
Assert-Contains $control "SetTableRowVisible(tableLayoutPanelTrigger, 4, triggerEnabled" "触发模式关闭时必须隐藏触发延迟行。"
Assert-Contains $control "SetTableRowVisible(tableLayoutPanelTrigger, 5, triggerEnabled && softwareTriggerSource" "软件触发时必须显示手动软触发按钮行。"
Assert-Contains $control "buttonSoftTrigger.Click += ButtonSoftTrigger_Click;" "软触发按钮必须绑定手动触发事件。"
Assert-Contains $control "private void ButtonSoftTrigger_Click" "必须提供软触发按钮点击处理。"
Assert-Contains $control "_camera.GrabOne();" "软触发按钮必须只发送一次软触发命令。"
Assert-Contains $control "if (IsSoftwareTriggerSource())" "主动取帧轮询必须识别软件触发源。"
Assert-Contains $control "软件触发等待手动执行" "软件触发模式不能由轮询自动连续触发。"
Assert-Contains $control "Apply2DTriggerSettingsBeforeCollecting()" "开始采集时必须按界面当前触发模式和触发源重新写入 SDK。"
Assert-Contains $control "_camera.SetTriggerSource(Get2DTriggerSource(comboTriggerSource.Text));" "开始采集时必须把软件触发或线路触发源写入 SDK。"
Assert-Contains $control "ApplyIoOutputParameterVisibility()" "线路模式变化后必须联动隐藏无效输出参数。"
Assert-Contains $control "lineSelectorOptions = _camera.GetLineSelector();" "线路选择器必须优先使用 SDK 可选线路。"
Assert-Contains $control "RefreshLineModeItemsForSelectedLine()" "切换线路选择后必须重新读取当前线路的线路模式可选项。"
Assert-Contains $control "_camera.SetLineSelector(GetLineName(comboLineSelector.Text));" "读取线路模式前必须先把 SDK LineSelector 切到当前线路。"
Assert-Contains $control "SetComboBoxItems(comboLineMode, GetEnumDisplayItems(lineModeOptions" "线路模式必须优先使用当前线路的 SDK 可选模式。"
Assert-Contains $control "comboLineMode.Text" "线路模式写入必须使用界面当前中文值。"

Assert-Contains $cameraInterface "CameraEnumValue GetTriggerSourceOptions();" "相机接口必须暴露触发源 SDK 可选枚举。"
Assert-Contains $cameraInterface "CameraEnumValue GetTriggerActivationOptions();" "相机接口必须暴露触发极性 SDK 可选枚举。"
Assert-Regex $cameraHik "if\s*\(\s*GetTriggerMode\(\)\s*==\s*TriggerModel\.Off\s*\)\s*\{[\s\S]*?return\s+TriggerSource\.Auto;" "触发模式关闭时才应返回连续采集，不能在触发打开时返回 Auto。"
Assert-Contains $cameraHik "SupportEnumEntries" "相机实现必须保留 SDK 枚举支持项，供界面按真实能力显示。"
Assert-Contains $control "if (IsHardwareTriggerSource(currentTriggerSource))" "初始化调试界面时只有线路硬触发才允许读取触发极性。"
Assert-Contains $control "TriggerEdge = IsHardwareTriggerSource(triggerSource) ? TryGetCurrent2DTriggerEdge() : null" "保存调试状态时软触发不能读取触发极性。"
Assert-Contains $control "if (!IsHardwareTriggerSource())" "触发极性事件必须阻止非线路触发写入。"

Write-Host "相机实时调试 SDK 动态选项检查通过。"
