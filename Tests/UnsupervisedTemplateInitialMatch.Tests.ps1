$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$designer = Get-Content -LiteralPath (Join-Path $root "Forms\AiTrainForm\UnsupervisedTrainForm.Designer.cs") -Encoding UTF8 -Raw
$form = Get-Content -LiteralPath (Join-Path $root "Forms\AiTrainForm\UnsupervisedTrainForm.cs") -Encoding UTF8 -Raw
$roiForm = Get-Content -LiteralPath (Join-Path $root "Forms\AiTrainForm\UnsupervisedRoiSettingsForm.cs") -Encoding UTF8 -Raw
$roiDesigner = Get-Content -LiteralPath (Join-Path $root "Forms\AiTrainForm\UnsupervisedRoiSettingsForm.Designer.cs") -Encoding UTF8 -Raw
$models = Get-Content -LiteralPath (Join-Path $root "Forms\AiTrainForm\UnsupervisedTrainingModels.cs") -Encoding UTF8 -Raw
$service = Get-Content -LiteralPath (Join-Path $root "Forms\AiTrainForm\UnsupervisedTrainingService.cs") -Encoding UTF8 -Raw
$matcher = Get-Content -LiteralPath (Join-Path $root "Forms\AiTrainForm\UnsupervisedTemplateRoiMatcher.cs") -Encoding UTF8 -Raw
$runtime = Get-Content -LiteralPath (Join-Path $root "Node\3-Detection\Unsupervised\UnsupervisedDetectionRuntime.cs") -Encoding UTF8 -Raw

function Assert-Contains {
    param([string]$text, [string]$value, [string]$message)
    if (-not $text.Contains($value)) { throw $message }
}

function Assert-NotContains {
    param([string]$text, [string]$value, [string]$message)
    if ($text.Contains($value)) { throw $message }
}

Assert-Contains $designer 'this.buttonRoiSettings.Click +=' "Main form must expose one ROI settings entry."
Assert-Contains $roiDesigner 'this.checkBoxEnableCrop.CheckedChanged +=' "ROI dialog must expose crop enable state."
Assert-Contains $roiDesigner 'this.checkBoxEnableTemplateMatch.CheckedChanged +=' "ROI dialog must expose template-match state."
Assert-Contains $roiForm 'new ContourTemplateEditor()' "ROI dialog must reuse the latest contour-template editor."
Assert-Contains $roiForm 'if (!checkBoxEnableCrop.Checked) checkBoxEnableTemplateMatch.Checked = false;' "Template matching must depend on ROI cropping."
Assert-Contains $roiForm 'imageRoiEditor.MaximumRoiCount = 1;' "ROI settings must enforce a single ROI."
Assert-Contains $roiDesigner 'this.comboBoxRoiShape.Items.AddRange' "ROI settings must allow rectangle or circle selection."
Assert-Contains $form 'TemplateMatchSettings = _roiCropEnabled ? _templateMatchSettings : null' "Training request must persist initial-match settings only with ROI cropping."
Assert-Contains $matcher 'DefaultMaximumMatches = 1' "Default match count must be one."
Assert-Contains $matcher 'DefaultMinimumScore = 0.5D' "Default match score must be 0.5."
Assert-Contains $matcher 'DefaultRecognitionMinimumScore = 0.1D' "Runtime recognition score must default to 0.1."
Assert-Contains $matcher 'DefaultAngleStart = -360D' "Default start angle must be -360 degrees."
Assert-Contains $matcher 'DefaultAngleEnd = 360D' "Default end angle must be 360 degrees."
Assert-Contains $matcher 'result.FindOptions.AngleStartDegrees = UnsupervisedTemplateMatchSettings.DefaultAngleStart' "Runtime start angle must remain -360 degrees."
Assert-Contains $matcher 'result.FindOptions.AngleEndDegrees = UnsupervisedTemplateMatchSettings.DefaultAngleEnd' "Runtime end angle must remain 360 degrees."
Assert-Contains $service 'Mat croppedImage = new Mat(image, rect).Clone();' "Training must crop the configured fixed ROI directly."
Assert-NotContains $service 'UnsupervisedTemplateRoiMatcher.Crop(image' "Training must not execute template matching."
Assert-NotContains $service 'NewContourMatchSession templateMatchSession' "Training must not create a template matcher session."
Assert-Contains $service 'TemplateMatchParameters = request.TemplateMatchSettings?.Parameters?.Copy()' "Manifest must persist template-match parameters."
Assert-Contains $matcher 'Cv2.WarpAffine' "Template-following ROI must use affine alignment."
Assert-Contains $runtime 'UnsupervisedTemplateRoiMatcher.Crop(sourceImage, rect, settings, templateMatchSession, out followedRoi)' "Runtime must crop the same template-following ROI."
Assert-Contains $runtime 'private readonly NewContourMatchSession _templateMatchSession' "Runtime must reuse the template matcher cache."
Assert-Contains $runtime 'CreateRecognitionParameters(manifest.TemplateMatchParameters)' "Runtime must use the 0.1 recognition parameters."
Assert-Contains $runtime 'out RotatedRect? affineRoi' "Runtime must return the affine ROI geometry."
Assert-Contains $matcher 'TransformBaselineRoi' "Template following must transform the baseline ROI for display."
Assert-Contains $form 'TemplateMatchSettings = _roiCropEnabled ? _templateMatchSettings : null' "Training settings must remain persisted."

Write-Host "Unsupervised template initial-match checks passed."
