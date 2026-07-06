$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$formBasePath = Join-Path $root "FormBase.cs"
$formMainPath = Join-Path $root "FormMain.cs"
$formMainDesignerPath = Join-Path $root "FormMain.Designer.cs"

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

function Assert-NotContains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if ($Text -match [regex]::Escape($Pattern)) {
        throw $Message
    }
}

$formBase = Get-Content -LiteralPath $formBasePath -Encoding UTF8 -Raw
$formMain = Get-Content -LiteralPath $formMainPath -Encoding UTF8 -Raw
$formMainDesigner = Get-Content -LiteralPath $formMainDesignerPath -Encoding UTF8 -Raw

Assert-Contains $formBase "WmGetMinMaxInfo" "FormBase must handle WM_GETMINMAXINFO for borderless maximize and restore."
Assert-Contains $formBase "ApplyMaximizedBounds" "FormBase must refresh maximized bounds instead of fixing them once on load."
Assert-Contains $formBase "GetMaximizedTargetBounds" "FormBase must centralize maximized target bounds calculation."
Assert-Contains $formBase "Owner.Bounds" "FormBase maximized bounds must support covering the owner window."
Assert-Contains $formBase "UpdateMaximizeButtonText" "FormBase must keep the custom maximize button state synchronized after restore."
Assert-NotContains $formBase "MaximizedBounds = Screen.FromHandle(Handle).WorkingArea;" "FormBase must not hard-code maximized bounds to screen working area on load."

Assert-Contains $formMainDesigner "this.tableLayoutPanel2.AutoSize = false;" "FormMain root layout must not AutoSize while docked fill."
Assert-Contains $formMainDesigner "new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F)" "FormMain dock row must fill remaining client height."
Assert-NotContains $formMainDesigner "this.IsMdiContainer = true;" "FormMain must not be an MDI container when DockPanel hosts documents."

Assert-Contains $formMain "ShowOwnedMaximizedDialog(FrmNewProcessWizard);" "Process wizard must use the owned maximized dialog helper."
Assert-Contains $formMain "dialog.ShowDialog(this);" "Owned maximized dialog helper must open dialogs with FormMain as owner."
Assert-Contains $formMain "ShowOwnedMaximizedDialog" "FormMain must use a shared helper for owned maximized dialogs."

Write-Host "Form maximize behavior checks passed."
