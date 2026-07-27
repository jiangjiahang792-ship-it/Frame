$ErrorActionPreference = 'Stop'

function Assert-ContainsText {
    param([string]$Content, [string]$Expected, [string]$Message)
    if (-not $Content.Contains($Expected)) {
        throw $Message
    }
}

function Assert-NotContainsText {
    param([string]$Content, [string]$Unexpected, [string]$Message)
    if ($Content.Contains($Unexpected)) {
        throw $Message
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$fastSamplingText = ([char]0x5FEB).ToString() + ([char]0x901F) + ([char]0x91C7) + ([char]0x6837)
$antiSamplingText = ([char]0x6297).ToString() + ([char]0x5E72) + ([char]0x6270) + ([char]0x91C7) + ([char]0x6837)
$samplingModeText = ([char]0x91C7).ToString() + ([char]0x6837) + ([char]0x6A21) + ([char]0x5F0F)

$dualTools = @(
    @{
        Name = 'LineLineAngle'
        FirstProperty = 'Line1SamplingMode'
        SecondProperty = 'Line2SamplingMode'
        SelectorArgument = 'firstLine'
        AlgorithmArguments = @('firstLine')
        FirstSettings = 'line1Settings'
        SecondSettings = 'line2Settings'
    },
    @{
        Name = 'PointPointDistance'
        FirstProperty = 'Point1SamplingMode'
        SecondProperty = 'Point2SamplingMode'
        SelectorArgument = 'firstPoint'
        AlgorithmArguments = @('firstPoint')
        FirstSettings = 'point1Settings'
        SecondSettings = 'point2Settings'
    },
    @{
        Name = 'PointLineDistance'
        FirstProperty = 'PointSamplingMode'
        SecondProperty = 'LineSamplingMode'
        SelectorArgument = 'point'
        AlgorithmArguments = @('true', 'false')
        FirstSettings = 'pointSettings'
        SecondSettings = 'lineSettings'
    }
)

foreach ($tool in $dualTools) {
    $folder = Join-Path $projectRoot ('Node\4-Measurement\' + $tool.Name)
    $param = Get-Content -LiteralPath (Join-Path $folder ('NodeParam' + $tool.Name + '.cs')) -Raw -Encoding UTF8
    $form = Get-Content -LiteralPath (Join-Path $folder ('NodeParamForm' + $tool.Name + '.cs')) -Raw -Encoding UTF8
    $designer = Get-Content -LiteralPath (Join-Path $folder ('NodeParamForm' + $tool.Name + '.Designer.cs')) -Raw -Encoding UTF8

    Assert-ContainsText $param ('CaliperSamplingMode ' + $tool.FirstProperty + ' { get; set; } = CaliperSamplingMode.Fast;') ($tool.Name + ' must default its first sampling mode to fast.')
    Assert-ContainsText $param ('CaliperSamplingMode ' + $tool.SecondProperty + ' { get; set; } = CaliperSamplingMode.Fast;') ($tool.Name + ' must default its second sampling mode to fast.')
    Assert-ContainsText $param ('GetSamplingMode(bool ' + $tool.SelectorArgument + ')') ($tool.Name + ' must expose per-ROI sampling mode lookup.')
    Assert-ContainsText $form 'CaliperSamplingMode SamplingMode { get; set; } = CaliperSamplingMode.Fast;' ($tool.Name + ' ROI settings must default to fast sampling.')
    Assert-ContainsText $form ('_roiRunSettings[0].SamplingMode = param.' + $tool.FirstProperty + ';') ($tool.Name + ' must load the first ROI sampling mode.')
    Assert-ContainsText $form ('_roiRunSettings[1].SamplingMode = param.' + $tool.SecondProperty + ';') ($tool.Name + ' must load the second ROI sampling mode.')
    Assert-ContainsText $form 'comboBoxSamplingMode.SelectedIndex = settings.SamplingMode == CaliperSamplingMode.AntiInterference ? 1 : 0;' ($tool.Name + ' must load unknown values as fast sampling.')
    Assert-ContainsText $form 'settings.SamplingMode = comboBoxSamplingMode.SelectedIndex == 1' ($tool.Name + ' must save the active ROI sampling mode.')
    Assert-ContainsText $form ($tool.FirstProperty + ' = param.' + $tool.FirstProperty) ($tool.Name + ' must preserve the first mode during position correction.')
    Assert-ContainsText $form ($tool.SecondProperty + ' = param.' + $tool.SecondProperty) ($tool.Name + ' must preserve the second mode during position correction.')
    Assert-ContainsText $form ($tool.FirstProperty + ' = ' + $tool.FirstSettings + '.SamplingMode') ($tool.Name + ' must persist the first ROI mode.')
    Assert-ContainsText $form ($tool.SecondProperty + ' = ' + $tool.SecondSettings + '.SamplingMode') ($tool.Name + ' must persist the second ROI mode.')
    foreach ($argument in $tool.AlgorithmArguments) {
        Assert-ContainsText $form ('SamplingMode = param.GetSamplingMode(' + $argument + '),') ($tool.Name + ' must pass sampling mode ' + $argument + ' to the public algorithm.')
    }
    Assert-ContainsText $form ('comboBoxSamplingMode.Items.Add("' + $fastSamplingText + '");') ($tool.Name + ' must show the fast sampling option.')
    Assert-ContainsText $form ('comboBoxSamplingMode.Items.Add("' + $antiSamplingText + '");') ($tool.Name + ' must show the anti-interference sampling option.')
    Assert-ContainsText $designer 'this.groupBoxRunParams.Controls.Add(this.comboBoxSamplingMode);' ($tool.Name + ' sampling selector must live in Designer.')
    Assert-ContainsText $designer ('this.labelSamplingMode.Text = "' + $samplingModeText + '";') ($tool.Name + ' sampling label must use simplified Chinese.')
    Assert-ContainsText $designer 'private System.Windows.Forms.ComboBox comboBoxSamplingMode;' ($tool.Name + ' Designer must declare the sampling selector.')
}

$regionFolder = Join-Path $projectRoot 'Node\4-Measurement\PointRegionDistance'
$regionParam = Get-Content -LiteralPath (Join-Path $regionFolder 'NodeParamPointRegionDistance.cs') -Raw -Encoding UTF8
$regionForm = Get-Content -LiteralPath (Join-Path $regionFolder 'NodeParamFormPointRegionDistance.cs') -Raw -Encoding UTF8
$regionDesigner = Get-Content -LiteralPath (Join-Path $regionFolder 'NodeParamFormPointRegionDistance.Designer.cs') -Raw -Encoding UTF8

Assert-ContainsText $regionParam 'CaliperSamplingMode SamplingMode { get; set; } = CaliperSamplingMode.Fast;' 'PointRegionDistance must default to fast sampling.'
Assert-ContainsText $regionForm 'comboBoxSamplingMode.SelectedIndex = param.SamplingMode == CaliperSamplingMode.AntiInterference ? 1 : 0;' 'PointRegionDistance must load unknown values as fast sampling.'
Assert-ContainsText $regionForm 'SamplingMode = param.SamplingMode,' 'PointRegionDistance must preserve sampling mode during position correction.'
Assert-ContainsText $regionForm 'SamplingMode = comboBoxSamplingMode.SelectedIndex == 1' 'PointRegionDistance must save the selected sampling mode.'
Assert-ContainsText $regionForm ('comboBoxSamplingMode.Items.Add("' + $fastSamplingText + '");') 'PointRegionDistance must show the fast sampling option.'
Assert-ContainsText $regionForm ('comboBoxSamplingMode.Items.Add("' + $antiSamplingText + '");') 'PointRegionDistance must show the anti-interference sampling option.'
Assert-ContainsText $regionDesigner 'this.groupBoxRunParams.Controls.Add(this.comboBoxSamplingMode);' 'PointRegionDistance sampling selector must live in Designer.'
Assert-ContainsText $regionDesigner ('this.labelSamplingMode.Text = "' + $samplingModeText + '";') 'PointRegionDistance sampling label must use simplified Chinese.'
Assert-ContainsText $regionDesigner 'private System.Windows.Forms.ComboBox comboBoxSamplingMode;' 'PointRegionDistance Designer must declare the sampling selector.'

$serialTools = @(
    'CaliperLine',
    'CaliperCircle',
    'CaliperEllipse',
    'FindPoint',
    'LineLineAngle',
    'PointPointDistance',
    'PointLineDistance',
    'PointRegionDistance'
)

foreach ($tool in $serialTools) {
    $formPath = Join-Path $projectRoot ("Node\4-Measurement\{0}\NodeParamForm{0}.cs" -f $tool)
    $form = Get-Content -LiteralPath $formPath -Raw -Encoding UTF8
    Assert-ContainsText $form 'MultiTargetMeasurementRunner.Run(' ($tool + ' must use serial multi-target execution.')
    Assert-NotContainsText $form 'MultiTargetMeasurementRunner.RunParallel(' ($tool + ' must not use parallel multi-target execution.')
}

Write-Host 'Composite measurement sampling and serial execution source checks passed.'
