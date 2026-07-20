$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$controlPath = Join-Path $root "Forms\CameraAdd\CameraLiveDebugControl.cs"
$formPath = Join-Path $root "Forms\CameraAdd\FrmCameraListView.cs"

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
$form = Get-Content -LiteralPath $formPath -Encoding UTF8 -Raw

Assert-Contains $control "Camera2DDebugStateSnapshot" "Missing 2D camera debug state snapshot."
Assert-Contains $control "Capture2DDebugStateForDebug()" "Start preview must capture flow camera trigger state first."
Assert-Contains $control "Restore2DDebugStateAfterDebug()" "Stop/dispose must restore flow camera trigger state."
Assert-Regex $control "Start2DCollecting\(\)[\s\S]*?Capture2DDebugStateForDebug\(\)[\s\S]*?Apply2DTriggerSettingsBeforeCollecting\(\)" "Start2DCollecting must capture before applying debug trigger settings."
Assert-Regex $control "StopCollectingSilently\(\)[\s\S]*?Restore2DDebugStateAfterDebug\(\)" "StopCollectingSilently must restore camera state."
Assert-Contains $control "SetDefaultContinuousPreviewSelection()" "Debug view must default to continuous preview selection."
Assert-Regex $control "LoadCameraInfo\(\)[\s\S]*?TryLoadCameraParameterValues\(\);[\s\S]*?SetDefaultContinuousPreviewSelection\(\)" "UI must read real state during load, then default to temporary continuous preview."
Assert-Contains $control "StartDefaultContinuousPreview()" "Debug view must auto start a temporary continuous preview when shown."
Assert-Regex $control "OnHandleCreated[\s\S]*?StartDefaultContinuousPreview\(\)" "Auto preview must start after the WinForms handle is ready."
Assert-Contains $control "_camera.SetTriggerMode(_camera2DDebugStateSnapshot.TriggerMode);" "Restore must write original trigger mode."
Assert-Contains $control "_camera.SetTriggerSource(_camera2DDebugStateSnapshot.TriggerSource);" "Restore must write original trigger source."
Assert-Contains $control "_camera.SetTriggerDelay(_camera2DDebugStateSnapshot.TriggerDelay);" "Restore must write original trigger delay."

Assert-Contains $form "ShowLastSelectedCameraOrFirst()" "Camera form must auto show last selected camera."
Assert-Contains $form "_lastSelectedCamera" "Camera form must remember last selected 2D camera."
Assert-Contains $form "_lastSelectedCamera3D" "Camera form must remember last selected 3D camera."
Assert-Regex $form "FrmCameraListView_Shown[\s\S]*?ShowLastSelectedCameraOrFirst\(\)" "Shown event must restore last selected camera."
Assert-Regex $form "FrmCameraListView_FormClosing[\s\S]*?ClearPreviewPanel\(\)" "Form closing must release preview control so the control restores camera state."
if ($form -match "SetTriggerSource\(TriggerSource\.LINE0\)") {
    throw "Camera form closing must not hard-code trigger source to LINE0."
}

Write-Host "Camera live debug state restore checks passed."
