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
    @{ Name = 'CaliperLine'; Target = 'CaliperLineTargetResult' },
    @{ Name = 'CaliperCircle'; Target = 'CaliperCircleTargetResult' },
    @{ Name = 'CaliperEllipse'; Target = 'CaliperEllipseTargetResult' }
)

foreach ($tool in $tools) {
    $folder = Join-Path $projectRoot ("Node\4-Measurement\" + $tool.Name)
    $resultPath = Join-Path $folder ("NodeResult" + $tool.Name + '.cs')
    $nodePath = Join-Path $folder ("Node" + $tool.Name + '.cs')
    $formPath = Join-Path $folder ("NodeParamForm" + $tool.Name + '.cs')
    foreach ($path in @($resultPath, $nodePath, $formPath)) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Required caliper production file does not exist: $path"
        }
    }

    $result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8
    $node = Get-Content -LiteralPath $nodePath -Raw -Encoding UTF8
    $form = Get-Content -LiteralPath $formPath -Raw -Encoding UTF8

    Assert-ContainsText $result ("List<" + $tool.Target + "> Items") ($tool.Name + ' must expose ordered target result items.')
    Assert-ContainsText $form 'ExecuteMeasures(' ($tool.Name + ' must expose a multi-target execution entry point.')
    Assert-ContainsText $form 'MultiTargetMeasurementRunner.Run' ($tool.Name + ' must use the common multi-target runner.')
    Assert-ContainsText $form 'GetValue<List<PositionCorrectionInfo>>' ($tool.Name + ' must read the full correction list.')
    Assert-ContainsText $form 'ResolveAnchorIndex' ($tool.Name + ' must resolve which target owns the single editing ROI.')
    Assert-ContainsText $form 'InverseTransformPoint' ($tool.Name + ' must normalize the single editing ROI back to the baseline.')
    Assert-ContainsText $form 'CreateFailure' ($tool.Name + ' must create a retained failure item.')
    Assert-ContainsText $form 'corrections = corrections ?? new List<PositionCorrectionInfo>();' ($tool.Name + ' must return an empty NG result instead of throwing for zero targets.')
    Assert-ContainsText $form 'if (corrections.Count == 0)' ($tool.Name + ' must skip image access when there are zero targets.')
    Assert-ContainsText $form 'SetDisplayResult(null);' ($tool.Name + ' must clear the previous multi-target overlay before editing its single ROI.')
    $readMarker = 'private IReadOnlyList<PositionCorrectionInfo> ReadCorrections(NodeParam' + $tool.Name + ' param)'
    $readStart = $form.IndexOf($readMarker)
    $readEnd = $form.IndexOf('private ', $readStart + $readMarker.Length)
    $readBody = $form.Substring($readStart, $readEnd - $readStart)
    Assert-NotContainsText $readBody 'PositionCorrectionHelper.EnsureValid' ($tool.Name + ' must isolate an invalid correction as one failed target.')
    Assert-ContainsText $node 'items.All' ($tool.Name + ' overall status must require every target to pass.')
    Assert-ContainsText $node $targetWord ($tool.Name + ' display output must identify each target.')
}

$circleFormPath = Join-Path $projectRoot 'Node\4-Measurement\CaliperCircle\NodeParamFormCaliperCircle.cs'
$circleNodePath = Join-Path $projectRoot 'Node\4-Measurement\CaliperCircle\NodeCaliperCircle.cs'
$circleForm = Get-Content -LiteralPath $circleFormPath -Raw -Encoding UTF8
$circleNode = Get-Content -LiteralPath $circleNodePath -Raw -Encoding UTF8

Assert-ContainsText $circleForm 'MultiTargetMeasurementRunner.Run(' 'CaliperCircle must use serial target execution.'
Assert-NotContainsText $circleForm 'MultiTargetMeasurementRunner.RunParallel(' 'CaliperCircle must not use target-level parallel execution.'
Assert-NotContainsText $circleForm 'Parallel.For' 'CaliperCircle must delegate target ordering to the common serial runner.'

$executeMarker = 'internal List<CaliperCircleTargetResult> ExecuteMeasures(NodeParamCaliperCircle param, CancellationToken token)'
$executeStart = $circleForm.IndexOf($executeMarker)
$executeEnd = $circleForm.IndexOf('private ', $executeStart + $executeMarker.Length)
$executeBody = $circleForm.Substring($executeStart, $executeEnd - $executeStart)
$grayReadCount = ([regex]::Matches($executeBody, 'GetInputGrayMat\(')).Count
if ($grayReadCount -ne 1) {
    throw 'CaliperCircle must acquire exactly one gray image outside serial target execution.'
}

Assert-ContainsText $circleNode 'Stopwatch.StartNew()' 'CaliperCircle node runtime must use a high-resolution stopwatch.'
Assert-ContainsText $circleNode 'SetRunResult(stopwatch,' 'CaliperCircle must report wall-clock time from the stopwatch.'
Assert-NotContainsText $circleNode 'DateTime startTime = DateTime.Now;' 'CaliperCircle must not use DateTime for short runtime measurement.'

Write-Host 'Multi-target caliper source checks passed.'
