$ErrorActionPreference = 'Stop'

function Assert-ContainsText {
    param([string]$Content, [string]$Expected, [string]$Message)
    if (-not $Content.Contains($Expected)) {
        throw $Message
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$algorithmPath = Join-Path $projectRoot 'Node\4-Measurement\Common\CaliperMeasurementAlgorithm.cs'
$paramPath = Join-Path $projectRoot 'Node\4-Measurement\CaliperLine\NodeParamCaliperLine.cs'
$formPath = Join-Path $projectRoot 'Node\4-Measurement\CaliperLine\NodeParamFormCaliperLine.cs'
$designerPath = Join-Path $projectRoot 'Node\4-Measurement\CaliperLine\NodeParamFormCaliperLine.Designer.cs'

$algorithm = Get-Content -LiteralPath $algorithmPath -Raw -Encoding UTF8
$param = Get-Content -LiteralPath $paramPath -Raw -Encoding UTF8
$form = Get-Content -LiteralPath $formPath -Raw -Encoding UTF8
$designer = Get-Content -LiteralPath $designerPath -Raw -Encoding UTF8
$subPixelText = -join ([char[]](0x4E9A, 0x50CF, 0x7D20))
$pixelText = -join ([char[]](0x50CF, 0x7D20))
$measureModeText = -join ([char[]](0x6D4B, 0x91CF, 0x7CBE, 0x5EA6))

$defaultMode = 'GeometryMeasureMode MeasureMode { get; set; } = GeometryMeasureMode.SubPixel;'
Assert-ContainsText $algorithm $defaultMode 'Public caliper line parameters must default to sub-pixel edge localization.'
Assert-ContainsText $param $defaultMode 'Caliper line node parameters must persist sub-pixel mode by default.'
Assert-ContainsText $algorithm 'p.MeasureMode, p.BlurSize' 'Line measurement must pass the selected precision mode into profile edge localization.'
Assert-ContainsText $algorithm 'measureMode != GeometryMeasureMode.Pixel' 'Only the explicit pixel mode may disable sub-pixel interpolation.'
Assert-ContainsText $algorithm 'return new PointF(px[bestIdx], py[bestIdx]);' 'Pixel mode must retain the integer-step gradient peak location.'

Assert-ContainsText $form ('comboBoxMeasureMode.Items.Add("' + $subPixelText + '");') 'The form must show the simplified Chinese sub-pixel option.'
Assert-ContainsText $form ('comboBoxMeasureMode.Items.Add("' + $pixelText + '");') 'The form must show the simplified Chinese pixel option.'
Assert-ContainsText $form 'param.MeasureMode == GeometryMeasureMode.Pixel ? 1 : 0' 'The form must load invalid or absent values as sub-pixel mode.'
Assert-ContainsText $form 'MeasureMode = comboBoxMeasureMode.SelectedIndex == 1' 'The form must persist the selected precision mode.'
Assert-ContainsText $form 'MeasureMode = param.MeasureMode' 'ROI transformation must preserve the selected precision mode.'
Assert-ContainsText $form 'MeasureMode = runtimeParam.MeasureMode' 'Runtime execution must pass the selected precision mode to the algorithm.'

Assert-ContainsText $designer 'this.groupBoxCaliper.Controls.Add(this.comboBoxMeasureMode);' 'The precision selector must be visible in the WinForms Designer.'
Assert-ContainsText $designer ('this.labelMeasureMode.Text = "' + $measureModeText + '";') 'The precision label must use simplified Chinese.'
Assert-ContainsText $designer 'private System.Windows.Forms.ComboBox comboBoxMeasureMode;' 'The Designer must declare the precision selector.'

Write-Host 'Caliper measure mode source checks passed.'
