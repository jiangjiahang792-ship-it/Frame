$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$designerPath = Join-Path $root "Forms\AiTrainForm\UnsupervisedTrainForm.Designer.cs"
$codePath = Join-Path $root "Forms\AiTrainForm\UnsupervisedTrainForm.cs"

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

$designer = Get-Content -LiteralPath $designerPath -Encoding UTF8 -Raw
$code = Get-Content -LiteralPath $codePath -Encoding UTF8 -Raw
$setSelectedOkText = -join @([char]0x9009, [char]0x4E2D, [char]0x8BBE, [char]0x4F, [char]0x4B)
$setSelectedNgText = -join @([char]0x9009, [char]0x4E2D, [char]0x8BBE, [char]0x4E, [char]0x47)

Assert-Contains $designer "tabControlMain" "Missing main TabControl."
Assert-Contains $designer "tabPageCheck" "Missing check tab."
Assert-Contains $designer "tabPageTraining" "Missing training tab."
Assert-Contains $designer "tabPageCheck.Text" "Missing check tab Text assignment."
Assert-Contains $designer "tabPageTraining.Text" "Missing training tab Text assignment."
Assert-Contains $designer "buttonSelectImageFolder" "Missing image folder button."
Assert-Contains $designer "buttonReloadImages" "Missing reload button."
Assert-Contains $designer "buttonFilterAll" "Missing All filter button."
Assert-Contains $designer "buttonFilterOk" "Missing OK filter button."
Assert-Contains $designer "buttonFilterNg" "Missing NG filter button."
Assert-Contains $designer "buttonSetSelectedOk" "Missing selected-image OK assignment button."
Assert-Contains $designer "buttonSetSelectedNg" "Missing selected-image NG assignment button."
Assert-Contains $designer ('this.buttonSetSelectedOk.Text = "' + $setSelectedOkText + '";') "Selected OK assignment button must use Simplified Chinese text."
Assert-Contains $designer ('this.buttonSetSelectedNg.Text = "' + $setSelectedNgText + '";') "Selected NG assignment button must use Simplified Chinese text."
Assert-Contains $designer "this.buttonSetSelectedOk.Click += new System.EventHandler(this.buttonSetSelectedOk_Click);" "Selected OK assignment button must be wired."
Assert-Contains $designer "this.buttonSetSelectedNg.Click += new System.EventHandler(this.buttonSetSelectedNg_Click);" "Selected NG assignment button must be wired."
Assert-Contains $designer "flowLayoutPanelImages" "Missing thumbnail container."
Assert-Contains $designer "imageROIEditControlPreview" "Missing ROI preview control."
Assert-Contains $designer "buttonDrawRoi" "Missing draw ROI action."
Assert-Contains $designer "buttonClearRoi" "Missing clear ROI action."
Assert-Contains $designer "tableLayoutPanelMainRoot" "Main form must use a root layout panel so the top navigation background matches the workspace."
Assert-Contains $designer "panelMainNavigation" "Missing dark top navigation panel."
Assert-Contains $designer "buttonNavCheck" "Missing custom check navigation button."
Assert-Contains $designer "buttonNavTraining" "Missing custom training navigation button."
Assert-Contains $designer "this.tableLayoutPanelMainRoot.Controls.Add(this.panelMainNavigation, 0, 0);" "Top navigation panel must occupy the first root row."
Assert-Contains $designer "this.tableLayoutPanelMainRoot.Controls.Add(this.tabControlMain, 0, 1);" "Tab content must sit below the custom navigation row."
Assert-Contains $designer "this.tabControlMain.ItemSize = new System.Drawing.Size(118, 34);" "Designer must keep native TabControl headers visible so the training page can be selected visually."
Assert-NotContains $designer "this.tabControlMain.ItemSize = new System.Drawing.Size(1, 1);" "Designer must not hide native TabControl headers; runtime code should hide them instead."
Assert-Contains $designer "this.buttonNavCheck.Click += new System.EventHandler(this.buttonNavCheck_Click);" "Check navigation button must switch tabs."
Assert-Contains $designer "this.buttonNavTraining.Click += new System.EventHandler(this.buttonNavTraining_Click);" "Training navigation button must switch tabs."
Assert-Contains $designer "this.buttonFilterAll.Size = new System.Drawing.Size(175, 63);" "All filter button must be large enough for category labels."
Assert-Contains $designer "this.buttonFilterOk.Size = new System.Drawing.Size(175, 63);" "OK filter button must be large enough for category labels."
Assert-Contains $designer "this.buttonFilterNg.Size = new System.Drawing.Size(175, 63);" "NG filter button must be large enough for category labels."
Assert-Contains $designer "tableLayoutPanelRoiToolbar" "ROI toolbar must use a layout panel to prevent title and buttons from overlapping."
Assert-Contains $designer "this.tableLayoutPanelRoiToolbar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));" "ROI toolbar title column must take remaining width."
Assert-Contains $designer "this.tableLayoutPanelRoiToolbar.SetColumnSpan(this.labelPreviewTitle, 3);" "Preview title must use the full toolbar width."
Assert-Contains $designer "this.labelPreviewTitle.AutoEllipsis = true;" "Preview title must ellipsize long file names instead of overlapping buttons."
Assert-Contains $designer "comboBoxModelType" "Missing model type control."
Assert-Contains $designer "numericUpDownThreshold" "Missing threshold control."
Assert-Contains $designer "numericUpDownMiniArea" "Missing mini area control."
Assert-Contains $designer "comboBoxDevice" "Missing CPU/GPU device control."
Assert-Contains $designer "textBoxTemplateName" "Missing template name control."
Assert-Contains $designer "textBoxModelOutputPath" "Missing model output path control."
Assert-Contains $designer "progressBarTraining" "Missing training progress bar."
Assert-Contains $designer "textBoxTrainingLog" "Missing training log control."
Assert-Contains $designer "this.WindowState = System.Windows.Forms.FormWindowState.Maximized;" "Unsupervised form must open maximized."
Assert-Contains $designer "this.Shown += new System.EventHandler(this.UnsupervisedTrainForm_Shown);" "Unsupervised form must repair maximized bounds on shown."
Assert-Contains $designer "this.Resize += new System.EventHandler(this.UnsupervisedTrainForm_Resize);" "Unsupervised form must repair maximized bounds after minimize and maximize."

Assert-Contains $code "LoadImagesAsync" "Missing async image loading logic."
Assert-Contains $code "RenderImageCards" "Missing thumbnail rendering logic."
Assert-Contains $code "UnsupervisedImageCardControl" "Thumbnail rendering must use a lightweight owner-drawn card control."
Assert-Contains $code "Controls.AddRange(cards.ToArray())" "Thumbnail rendering must batch add card controls for smoother scrolling."
Assert-Contains $code "flowLayoutPanelImages.SuspendLayout();" "Thumbnail rendering must suspend layout while rebuilding cards."
Assert-Contains $code "flowLayoutPanelImages.ResumeLayout(true);" "Thumbnail rendering must resume layout with immediate layout so all cards are positioned instead of overlapping."
Assert-Contains $code "flowLayoutPanelImages.PerformLayout();" "Thumbnail rendering must force FlowLayoutPanel to arrange all thumbnail cards."
Assert-Contains $code "EnableDoubleBufferedScrolling(flowLayoutPanelImages)" "Thumbnail container must enable double buffering for smoother scrolling."
Assert-Contains $code "RememberLastImageFolder" "Form must remember the last loaded image folder."
Assert-Contains $code "RestoreLastImageFolder" "Form must restore the last image folder when reopened."
Assert-Contains $code "TryAutoReloadLastImageFolderAsync" "Form must auto reload the remembered image folder when reopened."
Assert-Contains $code "LastImageFolder" "Form must persist the last image folder for the current application session."
Assert-Contains $code "InspectionSelectedBorderColor" "Selected thumbnail must use a dedicated blue border color."
Assert-Contains $code "SetSelectedImageCategory" "Missing selected-image OK/NG classification logic."
Assert-Contains $code "buttonSetSelectedOk_Click" "Missing selected OK click handler."
Assert-Contains $code "buttonSetSelectedNg_Click" "Missing selected NG click handler."
Assert-Contains $code "RefreshSelectedImageAfterCategoryChange" "Changing OK/NG must keep selection state coherent after filtering."
Assert-Contains $code "ReleaseRuntimeResources" "Form must release loaded images and preview state when the dialog closes."
Assert-NotContains $code "var picture = new PictureBox" "Thumbnail cards must not create a PictureBox for every image."
Assert-NotContains $code "var label = new Label" "Thumbnail cards must not create a Label for every image."
Assert-Contains $code "GetTrainingRoiRectOrNull" "Missing optional ROI training decision."
Assert-Contains $code "FullImageTrainingWhenNoRoi" "Missing full-image training path marker."
Assert-Contains $code "buttonStartTraining_Click" "Missing start training event."
Assert-Contains $code "ApplyInspectionTheme" "Missing dark inspection workspace theme."
Assert-Contains $code "UpdateNavigationButtonState" "Missing custom navigation selected-state refresh."
Assert-Contains $code "ApplyRuntimeTabHeaderMode" "Missing runtime-only TabControl header hiding."
Assert-Contains $code "IsDesignerHosted" "Missing designer host guard for visible design-time tabs."
Assert-Contains $code "LicenseManager.UsageMode" "Designer detection must use LicenseManager in constructor-time code."
Assert-Contains $code "FitButtonText" "Missing button text auto-fit helper."
Assert-Contains $code "FitLabelText" "Missing preview title text auto-fit helper."
Assert-Contains $code "ApplyTrainingTheme" "Missing dark training workspace theme."
Assert-Contains $code "ApplyTrainingInputStyle" "Missing dark input styling for training controls."
Assert-Contains $code "ApplyActionButtonStyle" "Missing shared dark action button styling."
Assert-Contains $code "InspectionGridBackColor" "Missing inspection grid color token."
Assert-Contains $code "EnsureMaximizedBounds" "Missing maximized bounds repair logic."
Assert-Contains $code "MaximizedBounds" "Missing explicit maximized bounds assignment."
Assert-Contains $code "Owner.Bounds" "Maximized bounds must be able to cover the owner window."

Write-Host "UnsupervisedTrainForm layout checks passed."
