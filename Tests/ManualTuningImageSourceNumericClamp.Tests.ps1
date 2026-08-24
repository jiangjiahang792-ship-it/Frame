$ErrorActionPreference = 'Stop'

function Get-ProjectSource {
    param([string]$RelativePath)
    $path = Join-Path $PSScriptRoot "..\$RelativePath"
    return Get-Content -Encoding UTF8 -Raw -Path $path
}

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Expected,
        [string]$Message
    )

    if (-not $Text.Contains($Expected)) {
        throw $Message
    }
}

$paramForm = Get-ProjectSource 'Node\1-Acquisition\ImageSource\ParamFormImageSource.cs'

Assert-Contains $paramForm 'SetNumericUpDownValueInRange(numericUpDownExposureTime, (decimal)paramSrc.ExposureTime);' 'Exposure refresh must clamp NumericUpDown value.'
Assert-Contains $paramForm 'SetNumericUpDownValueInRange(numericUpDownGain, (decimal)paramSrc.Gain);' 'Gain refresh must clamp NumericUpDown value.'
Assert-Contains $paramForm 'control.Minimum = value;' 'NumericUpDown must expand its minimum to preserve the scheme value.'
Assert-Contains $paramForm 'control.Maximum = value;' 'NumericUpDown must expand its maximum to preserve the scheme value.'

Write-Host "Manual tuning image source numeric clamp check passed."
