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
$targetWord = ([char]0x76EE).ToString() + ([char]0x6807).ToString()
$tools = @(
    @{ Name = 'LineLineAngle'; Target = 'LineLineAngleTargetResult'; RequiresRegionTransform = $false; RequiresBoundsCenter = $true; RequiresSharedImageSize = $true },
    @{ Name = 'PointPointDistance'; Target = 'PointPointDistanceTargetResult'; RequiresRegionTransform = $false; RequiresBoundsCenter = $false; RequiresSharedImageSize = $false },
    @{ Name = 'PointLineDistance'; Target = 'PointLineDistanceTargetResult'; RequiresRegionTransform = $false; RequiresBoundsCenter = $true; RequiresSharedImageSize = $false },
    @{ Name = 'PointRegionDistance'; Target = 'PointRegionDistanceTargetResult'; RequiresRegionTransform = $true; RequiresBoundsCenter = $true; RequiresSharedImageSize = $false }
)

foreach ($tool in $tools) {
    $folder = Join-Path $projectRoot ("Node\4-Measurement\" + $tool.Name)
    $resultPath = Join-Path $folder ("NodeResult" + $tool.Name + '.cs')
    $nodePath = Join-Path $folder ("Node" + $tool.Name + '.cs')
    $formPath = Join-Path $folder ("NodeParamForm" + $tool.Name + '.cs')

    $result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8
    $node = Get-Content -LiteralPath $nodePath -Raw -Encoding UTF8
    $form = Get-Content -LiteralPath $formPath -Raw -Encoding UTF8

    Assert-ContainsText $result ("List<" + $tool.Target + "> Items") ($tool.Name + ' must expose ordered target result items.')
    Assert-ContainsText $form 'ExecuteMeasures(' ($tool.Name + ' must expose a multi-target execution entry point.')
    Assert-ContainsText $form 'MeasurementDataSourceMode.Draw' ($tool.Name + ' must only expand drawn geometry.')
    Assert-ContainsText $form 'MultiTargetMeasurementRunner.Run' ($tool.Name + ' must use the common multi-target runner.')
    Assert-ContainsText $form 'GetValue<List<PositionCorrectionInfo>>' ($tool.Name + ' must read the full correction list.')
    Assert-ContainsText $form 'ResolveAnchorIndex' ($tool.Name + ' must resolve which target owns the single editing ROI.')
    Assert-ContainsText $form 'InverseTransformPoint' ($tool.Name + ' must normalize the editing ROI back to the baseline.')
    Assert-ContainsText $form 'CreateFailure' ($tool.Name + ' must retain a zero-valued failure item.')
    Assert-ContainsText $form 'corrections = corrections ?? new List<PositionCorrectionInfo>();' ($tool.Name + ' must return an empty NG result instead of throwing for zero targets.')
    Assert-ContainsText $form 'if (corrections.Count == 0)' ($tool.Name + ' must skip image access when there are zero targets.')
    Assert-ContainsText $form 'SetDisplayResult(null);' ($tool.Name + ' must clear the previous multi-target overlay before editing its single geometry.')
    $readMarker = 'private IReadOnlyList<PositionCorrectionInfo> ReadCorrections(NodeParam' + $tool.Name + ' param)'
    $readStart = $form.IndexOf($readMarker)
    $readEnd = $form.IndexOf('private ', $readStart + $readMarker.Length)
    $readBody = $form.Substring($readStart, $readEnd - $readStart)
    Assert-NotContainsText $readBody 'PositionCorrectionHelper.EnsureValid' ($tool.Name + ' must isolate an invalid correction as one failed target.')
    Assert-ContainsText $node 'items.All' ($tool.Name + ' overall status must require every target to pass.')
    Assert-ContainsText $node $targetWord ($tool.Name + ' display output must identify each target.')

    if ($tool.RequiresRegionTransform) {
        Assert-ContainsText $form '_transformService.TransformPoints' 'PointRegionDistance must transform every region point.'
    }
    if ($tool.RequiresBoundsCenter) {
        Assert-ContainsText $form 'MeasurementNodeHelper.CalculateBoundsCenter' ($tool.Name + ' must resolve ROI ownership from the full geometry bounds center.')
    }
    if ($tool.RequiresSharedImageSize) {
        Assert-ContainsText $form 'CaptureDisplayImageSize(result, sharedGray)' 'LineLineAngle must reuse the measured image dimensions for every target.'
    }
}

Write-Host 'Multi-target geometry measurement source checks passed.'
