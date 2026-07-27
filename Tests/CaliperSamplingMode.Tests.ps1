$ErrorActionPreference = 'Stop'

function Assert-ContainsText {
    param([string]$Content, [string]$Expected, [string]$Message)
    if (-not $Content.Contains($Expected)) {
        throw $Message
    }
}

function Assert-MatchText {
    param([string]$Content, [string]$Pattern, [string]$Message)
    if ($Content -notmatch $Pattern) {
        throw $Message
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$algorithmPath = Join-Path $projectRoot 'Node\4-Measurement\Common\CaliperMeasurementAlgorithm.cs'
$algorithm = Get-Content -LiteralPath $algorithmPath -Raw -Encoding UTF8
$fastSamplingText = ([char]0x5FEB).ToString() + ([char]0x901F) + ([char]0x91C7) + ([char]0x6837)
$antiInterferenceSamplingText = ([char]0x6297).ToString() + ([char]0x5E72) + ([char]0x6270) + ([char]0x91C7) + ([char]0x6837)
$samplingModeText = ([char]0x91C7).ToString() + ([char]0x6837) + ([char]0x6A21) + ([char]0x5F0F)

Assert-MatchText $algorithm 'public\s+enum\s+CaliperSamplingMode\s*\{[^}]*Fast\s*=\s*0[^}]*AntiInterference\s*=\s*1' 'Sampling enum must keep Fast at zero and AntiInterference at one.'
foreach ($type in @('CaliperLineParams', 'CaliperCircleParams', 'CaliperEllipseParams')) {
    $start = $algorithm.IndexOf('public class ' + $type)
    $next = $algorithm.IndexOf('public class ', $start + 13)
    $body = $algorithm.Substring($start, $next - $start)
    Assert-ContainsText $body 'CaliperSamplingMode SamplingMode { get; set; } = CaliperSamplingMode.Fast;' ($type + ' must default to fast sampling.')
}

Assert-ContainsText $algorithm 'p.SamplingMode' 'Every caliper algorithm must pass its sampling mode to profile generation.'
Assert-ContainsText $algorithm 'SampleFastPixel(gray, px, py)' 'Profile generation must expose the legacy fast center-line path.'
Assert-ContainsText $algorithm 'samplingMode == CaliperSamplingMode.AntiInterference' 'Only the explicit anti-interference value may use averaged sampling.'
Assert-ContainsText $algorithm 'SampleAveragedPixel(gray, px, py, averageDirX, averageDirY, averageWidth)' 'Anti-interference mode must retain averaged bilinear sampling.'

$nodeParamFiles = @(
    'CaliperLine\NodeParamCaliperLine.cs',
    'CaliperCircle\NodeParamCaliperCircle.cs',
    'CaliperEllipse\NodeParamCaliperEllipse.cs'
)
foreach ($relative in $nodeParamFiles) {
    $content = Get-Content -LiteralPath (Join-Path $projectRoot ('Node\4-Measurement\' + $relative)) -Raw -Encoding UTF8
    Assert-ContainsText $content 'CaliperSamplingMode SamplingMode { get; set; } = CaliperSamplingMode.Fast;' ($relative + ' must persist fast sampling by default.')
}

$tools = @('CaliperLine', 'CaliperCircle', 'CaliperEllipse')
foreach ($tool in $tools) {
    $folder = Join-Path $projectRoot ('Node\4-Measurement\' + $tool)
    $form = Get-Content -LiteralPath (Join-Path $folder ('NodeParamForm' + $tool + '.cs')) -Raw -Encoding UTF8
    $designer = Get-Content -LiteralPath (Join-Path $folder ('NodeParamForm' + $tool + '.Designer.cs')) -Raw -Encoding UTF8
    Assert-ContainsText $form ('comboBoxSamplingMode.Items.Add("' + $fastSamplingText + '");') ($tool + ' must show the fast Chinese option.')
    Assert-ContainsText $form ('comboBoxSamplingMode.Items.Add("' + $antiInterferenceSamplingText + '");') ($tool + ' must show the anti-interference Chinese option.')
    Assert-ContainsText $form 'param.SamplingMode == CaliperSamplingMode.AntiInterference ? 1 : 0' ($tool + ' must load invalid or fast values as fast.')
    Assert-ContainsText $form 'SamplingMode = comboBoxSamplingMode.SelectedIndex == 1' ($tool + ' must save the selected mode.')
    Assert-ContainsText $form 'SamplingMode = param.SamplingMode' ($tool + ' must preserve the mode during ROI transformation.')
    Assert-ContainsText $form 'SamplingMode = runtimeParam.SamplingMode' ($tool + ' must pass the mode to the public algorithm parameter.')
    Assert-ContainsText $designer 'this.groupBoxCaliper.Controls.Add(this.comboBoxSamplingMode);' ($tool + ' sampling selector must live in the Designer file.')
    Assert-ContainsText $designer ('this.labelSamplingMode.Text = "' + $samplingModeText + '";') ($tool + ' sampling label must use simplified Chinese.')
    Assert-ContainsText $designer 'private System.Windows.Forms.ComboBox comboBoxSamplingMode;' ($tool + ' Designer must declare the sampling selector.')
}

Write-Host 'Caliper sampling mode source checks passed.'
