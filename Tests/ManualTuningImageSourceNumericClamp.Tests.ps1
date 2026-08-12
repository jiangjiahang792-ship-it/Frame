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
Assert-Contains $paramForm 'if (value < control.Minimum)' 'NumericUpDown clamp must protect the minimum value.'
Assert-Contains $paramForm 'else if (value > control.Maximum)' 'NumericUpDown clamp must protect the maximum value.'

Write-Host "Manual tuning image source numeric clamp check passed."
