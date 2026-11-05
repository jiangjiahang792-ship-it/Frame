$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$designer = Get-Content -LiteralPath (Join-Path $root "Forms\AiTrainForm\UnsupervisedTrainForm.Designer.cs") -Encoding UTF8 -Raw
$form = Get-Content -LiteralPath (Join-Path $root "Forms\AiTrainForm\UnsupervisedTrainForm.cs") -Encoding UTF8 -Raw
$models = Get-Content -LiteralPath (Join-Path $root "Forms\AiTrainForm\UnsupervisedTrainingModels.cs") -Encoding UTF8 -Raw
$service = Get-Content -LiteralPath (Join-Path $root "Forms\AiTrainForm\UnsupervisedTrainingService.cs") -Encoding UTF8 -Raw
$matcher = Get-Content -LiteralPath (Join-Path $root "Forms\AiTrainForm\UnsupervisedTemplateRoiMatcher.cs") -Encoding UTF8 -Raw
$runtime = Get-Content -LiteralPath (Join-Path $root "Node\3-Detection\Unsupervised\UnsupervisedDetectionRuntime.cs") -Encoding UTF8 -Raw

function Assert-Contains {
    param([string]$text, [string]$value, [string]$message)
    if (-not $text.Contains($value)) { throw $message }
}

Assert-Contains $designer 'this.checkBoxTemplateInitialMatch.Text =' "Missing initial-match checkbox."
Assert-Contains $designer 'this.buttonConfigureTemplateMatch.Text =' "Missing template configuration button."
Assert-Contains $form 'new ContourTemplateEditor()' "Training form must reuse the latest contour-template editor."
Assert-Contains $form 'TemplateMatchSettings = checkBoxTemplateInitialMatch.Checked ? _templateMatchSettings : null' "Training request must persist initial-match settings."
Assert-Contains $matcher 'DefaultMaximumMatches = 1' "Default match count must be one."
Assert-Contains $matcher 'DefaultMinimumScore = 0.5D' "Default match score must be 0.5."
Assert-Contains $matcher 'DefaultAngleStart = -360D' "Default start angle must be -360 degrees."
Assert-Contains $matcher 'DefaultAngleEnd = 360D' "Default end angle must be 360 degrees."
Assert-Contains $service 'UnsupervisedTemplateRoiMatcher.Crop(image, roi, templateMatchSettings, templateMatchSession)' "Training must crop a template-following ROI."
Assert-Contains $service 'NewContourMatchSession templateMatchSession' "Training must reuse the template matcher cache."
Assert-Contains $service 'TemplateMatchParameters = request.TemplateMatchSettings?.Parameters?.Copy()' "Manifest must persist template-match parameters."
Assert-Contains $matcher 'Cv2.WarpAffine' "Template-following ROI must use affine alignment."
Assert-Contains $runtime 'UnsupervisedTemplateRoiMatcher.Crop(sourceImage, rect, settings, templateMatchSession)' "Runtime must crop the same template-following ROI."
Assert-Contains $runtime 'private readonly NewContourMatchSession _templateMatchSession' "Runtime must reuse the template matcher cache."

Write-Host "Unsupervised template initial-match checks passed."
