$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$formPath = Join-Path $root "Node\5-EquipmentCommunication\CameraIO\ParamFormCameraIO.cs"
$form = Get-Content -LiteralPath $formPath -Encoding UTF8 -Raw

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

Assert-Contains $form "checkLineDirection(comboBoxLineMode.Text);" "切换相机后必须立即刷新当前线路模式对应的线路列表。"
Assert-Contains $form "foreach (var line in GetLineCandidates(lineSelectorOptions))" "线路列表必须使用SDK能力与标准线路探测候选项。"
Assert-Contains $form "读取相机【{m_camera.UserDefinedName}】线路【{line.Symbolic}】输出能力失败，已继续检查其他线路" "单条线路读取失败时必须记录日志并继续枚举。"
Assert-Contains $form "m_camera.SetLineSelector(originalLine);" "能力枚举完成后必须恢复相机原线路，避免参数页改变设备状态。"
Assert-Contains $form "相机【{m_camera.UserDefinedName}】没有读取到支持输出的IO线路" "没有输出线路时必须给出可诊断日志。"
Assert-Contains $form 'string symbolic = "Line" + lineIndex;' "SDK支持项为空时必须探测标准Line0至Line4。"
Assert-Contains $form 'string.Equals(symbolic, "Strobe", StringComparison.OrdinalIgnoreCase)' "必须兼容以Strobe表示输出能力的相机。"
Assert-Contains $form "mode.Value == 1 || mode.Value == 8" "线路模式符号名为空时必须兼容常见输出枚举值。"
Assert-Contains $form "IsOutputLineMode(lineModeOptions.CurEnumEntry)" "SDK模式支持项为空时必须检查线路当前模式。"

Write-Host "相机IO线路动态刷新检查通过。"
