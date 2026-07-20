$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$controlPath = Join-Path $root "Forms\CameraAdd\CameraLiveDebugControl.cs"
$designerPath = Join-Path $root "Forms\CameraAdd\CameraLiveDebugControl.Designer.cs"

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
$designer = Get-Content -LiteralPath $designerPath -Encoding UTF8 -Raw

Assert-Contains $designer "buttonSyncBrightnessToProcess" "Designer must contain the workflow brightness sync button."
Assert-Contains $designer "同步亮度到流程" "Sync button text must be shown in Simplified Chinese."
Assert-Regex $designer "tableLayoutPanelBasic\.Controls\.Add\(this\.buttonSyncBrightnessToProcess, 1, 6\)" "Sync button must be placed after exposure and gain on the basic property page."
Assert-Contains $control "buttonSyncBrightnessToProcess.Click += ButtonSyncBrightnessToProcess_Click;" "Sync button click event must be bound."
Assert-Contains $control "ButtonSyncBrightnessToProcess_Click" "Preview control must handle workflow brightness sync clicks."
Assert-Contains $control "SyncBrightnessToProcessCameras" "Preview control must sync tuned brightness into workflow image source nodes."
Assert-Contains $control "TryUpdateImageSourceBrightness" "Preview control must update matching image source parameters safely."
Assert-Contains $control "IsSame2DCamera" "Preview control must match workflow camera nodes against the current camera."
Assert-Contains $control "NodeParamImageSoucre" "Workflow sync must target image source node parameters."
Assert-Contains $control "param.ExposureTime = exposure" "Workflow sync must write exposure time into node parameters."
Assert-Contains $control "param.Gain = gain" "Workflow sync must write gain into node parameters."
Assert-Contains $control "Solution.Instance.IsModify = true" "Workflow sync must mark the solution modified."

Write-Host "Camera live debug workflow brightness sync checks passed."
