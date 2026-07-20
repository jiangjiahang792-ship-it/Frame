$ErrorActionPreference = 'Stop'

function Assert-ContainsText {
    param([string]$Content, [string]$Expected, [string]$Message)
    if (-not $Content.Contains($Expected)) {
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
    Assert-ContainsText $node 'items.All' ($tool.Name + ' overall status must require every target to pass.')
    Assert-ContainsText $node $targetWord ($tool.Name + ' display output must identify each target.')
}

Write-Host 'Multi-target caliper source checks passed.'
