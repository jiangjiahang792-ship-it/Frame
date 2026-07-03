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

$designer = Get-Content -LiteralPath $designerPath -Encoding UTF8 -Raw
$code = Get-Content -LiteralPath $codePath -Encoding UTF8 -Raw

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
Assert-Contains $designer "flowLayoutPanelImages" "Missing thumbnail container."
Assert-Contains $designer "imageROIEditControlPreview" "Missing ROI preview control."
Assert-Contains $designer "buttonDrawRoi" "Missing draw ROI action."
Assert-Contains $designer "buttonClearRoi" "Missing clear ROI action."
Assert-Contains $designer "comboBoxModelType" "Missing model type control."
Assert-Contains $designer "numericUpDownThreshold" "Missing threshold control."
Assert-Contains $designer "numericUpDownMiniArea" "Missing mini area control."
Assert-Contains $designer "comboBoxDevice" "Missing CPU/GPU device control."
Assert-Contains $designer "textBoxTemplateName" "Missing template name control."
Assert-Contains $designer "textBoxModelOutputPath" "Missing model output path control."
Assert-Contains $designer "progressBarTraining" "Missing training progress bar."
Assert-Contains $designer "textBoxTrainingLog" "Missing training log control."

Assert-Contains $code "LoadImagesAsync" "Missing async image loading logic."
Assert-Contains $code "RenderImageCards" "Missing thumbnail rendering logic."
Assert-Contains $code "GetTrainingRoiRectOrNull" "Missing optional ROI training decision."
Assert-Contains $code "FullImageTrainingWhenNoRoi" "Missing full-image training path marker."
Assert-Contains $code "buttonStartTraining_Click" "Missing start training event."

Write-Host "UnsupervisedTrainForm layout checks passed."
