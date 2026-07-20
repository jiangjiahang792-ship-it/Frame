$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$formBasePath = Join-Path $root "FormBase.cs"
$formMainPath = Join-Path $root "FormMain.cs"
$formMainDesignerPath = Join-Path $root "FormMain.Designer.cs"
$workspacePresenterPath = Join-Path $root "Forms\Workspace\ExclusiveWorkspacePresenter.cs"

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
$workspacePresenter = Get-Content -LiteralPath $workspacePresenterPath -Encoding UTF8 -Raw

Assert-Contains $formBase "WmGetMinMaxInfo" "FormBase must handle WM_GETMINMAXINFO for borderless maximize and restore."
Assert-Contains $formBase "ApplyMaximizedBounds" "FormBase must refresh maximized bounds instead of fixing them once on load."
Assert-Contains $formBase "GetMaximizedTargetBounds" "FormBase must centralize maximized target bounds calculation."
Assert-Contains $formBase "Owner.Bounds" "FormBase maximized bounds must support covering the owner window."
Assert-Contains $formBase "UpdateMaximizeButtonText" "FormBase must keep the custom maximize button state synchronized after restore."
Assert-NotContains $formBase "MaximizedBounds = Screen.FromHandle(Handle).WorkingArea;" "FormBase must not hard-code maximized bounds to screen working area on load."

Assert-NotContains $formMainDesigner "this.tableLayoutPanel2.AutoSize = true;" "主窗体根布局在填充停靠时不能启用自动尺寸。"
Assert-Contains $formMainDesigner "new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F)" "FormMain dock row must fill remaining client height."
Assert-NotContains $formMainDesigner "this.IsMdiContainer = true;" "FormMain must not be an MDI container when DockPanel hosts documents."

Assert-Contains $formMain "exclusiveWorkspacePresenter.ShowDialog(this, FrmNewProcessWizard);" "流程编辑窗口必须使用独占工作区显示接口。"
Assert-Contains $workspacePresenter "PrepareWorkspaceBounds(mainForm, workspaceForm);" "独占工作区必须统一处理最大化窗口的预定位。"
Assert-Contains $workspacePresenter "workspaceForm.ShowDialog(mainForm);" "独占工作区窗口必须以主窗体作为所有者。"
Assert-Contains $workspacePresenter "mainForm.Hide();" "显示独占工作区前必须隐藏主窗体。"

Write-Host "窗体最大化行为检查通过。"
