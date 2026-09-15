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
$algorithmPath = Join-Path $projectRoot 'Node\4-Measurement\Common\CaliperMeasurementAlgorithm.cs'
$qualityPath = Join-Path $projectRoot 'Node\4-Measurement\Common\CaliperLineQualityEvaluator.cs'
$paramPath = Join-Path $projectRoot 'Node\4-Measurement\CaliperLine\NodeParamCaliperLine.cs'
$formPath = Join-Path $projectRoot 'Node\4-Measurement\CaliperLine\NodeParamFormCaliperLine.cs'
$designerPath = Join-Path $projectRoot 'Node\4-Measurement\CaliperLine\NodeParamFormCaliperLine.Designer.cs'
$resultPath = Join-Path $projectRoot 'Node\4-Measurement\CaliperLine\NodeResultCaliperLine.cs'
$nodePath = Join-Path $projectRoot 'Node\4-Measurement\CaliperLine\NodeCaliperLine.cs'
$projectPath = Join-Path $projectRoot 'TDJS-Vision.csproj'

foreach ($path in @($algorithmPath, $qualityPath, $paramPath, $formPath, $designerPath, $resultPath, $nodePath, $projectPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required caliper quality file does not exist: $path"
    }
}

$algorithm = Get-Content -LiteralPath $algorithmPath -Raw -Encoding UTF8
$quality = Get-Content -LiteralPath $qualityPath -Raw -Encoding UTF8
$param = Get-Content -LiteralPath $paramPath -Raw -Encoding UTF8
$form = Get-Content -LiteralPath $formPath -Raw -Encoding UTF8
$designer = Get-Content -LiteralPath $designerPath -Raw -Encoding UTF8
$result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8
$node = Get-Content -LiteralPath $nodePath -Raw -Encoding UTF8
$project = Get-Content -LiteralPath $projectPath -Raw -Encoding UTF8

Assert-ContainsText $project '<Compile Include="Node\4-Measurement\Common\CaliperLineQualityEvaluator.cs" />' 'Project must compile the replaceable line quality evaluator.'
Assert-ContainsText $quality 'public interface ICaliperLineQualityEvaluator' 'Line quality validation must be replaceable through an interface.'
Assert-ContainsText $quality 'public sealed class DefaultCaliperLineQualityEvaluator : ICaliperLineQualityEvaluator' 'A default quality strategy must be provided.'
Assert-ContainsText $quality 'quality.ValidPointRatio' 'Quality validation must check the valid point ratio.'
Assert-ContainsText $quality 'quality.AverageResidual' 'Quality validation must check the average residual.'
Assert-ContainsText $quality 'quality.MaximumResidual' 'Quality validation must check the maximum residual.'
Assert-ContainsText $quality 'quality.CoverageRatio' 'Quality validation must check point coverage.'
Assert-ContainsText $quality 'quality.AngleDeviationDegrees' 'Quality validation must check the fitted direction.'

Assert-ContainsText $algorithm 'BuildConsensusLinePoints(candidatePoints, parameters, consensusDistance)' 'Line fitting must establish a deterministic consensus set before final fitting.'
Assert-ContainsText $algorithm 'currentPoints.Count' 'Consensus selection must prioritize the retained point count.'
Assert-ContainsText $algorithm 'DefaultCaliperLineQualityEvaluator.Instance' 'Public line measurement must apply the default quality gate.'
Assert-ContainsText $algorithm 'ICaliperLineQualityEvaluator qualityEvaluator)' 'Callers must be able to replace the quality evaluation strategy.'
Assert-ContainsText $algorithm 'result.ErrorMessage = evaluation.ErrorMessage;' 'Rejected fit reason must reach the public measurement result.'
Assert-NotContainsText $algorithm 'medianResidual * 2.5' 'Line fitting must not derive an expanding threshold from a contaminated point set.'

foreach ($content in @($algorithm, $param)) {
    Assert-ContainsText $content 'EnableQualityValidation { get; set; } = true;' 'Quality validation must default to enabled.'
    Assert-ContainsText $content 'MinimumValidPointRatio { get; set; } = 0.8f;' 'Valid point ratio must default to 80 percent.'
    Assert-ContainsText $content 'MaximumAverageResidual { get; set; } = 1.5f;' 'Average residual limit must have a production default.'
    Assert-ContainsText $content 'MaximumResidual { get; set; } = 3.5f;' 'Maximum residual limit must have a production default.'
    Assert-ContainsText $content 'MinimumCoverageRatio { get; set; } = 0.75f;' 'Coverage limit must have a production default.'
    Assert-ContainsText $content 'MaximumAngleDeviationDegrees { get; set; } = 5.0f;' 'Direction deviation limit must have a production default.'
}

Assert-ContainsText $designer 'this.Controls.Add(this.groupBoxQuality);' 'Quality controls must be visible in the WinForms Designer.'
Assert-ContainsText $designer 'this.groupBoxQuality.Text = "' 'Quality group must provide a visible localized title.'
Assert-ContainsText $designer 'private System.Windows.Forms.CheckBox checkBoxEnableQualityValidation;' 'Designer must declare the quality switch.'
Assert-ContainsText $form 'MinimumValidPointRatio = ParseRatio(' 'UI must validate and persist ratio settings.'
Assert-ContainsText $form 'MaximumAverageResidual = ParsePositiveFloat(' 'UI must validate and persist residual settings.'
Assert-ContainsText $form 'measure.Quality.CandidatePointCount' 'Execution must map quality statistics into target results.'

Assert-ContainsText $result 'public int CandidatePointCount { get; set; }' 'Summary result must expose the candidate point count.'
foreach ($property in @('ValidPointRatio', 'AverageResidual', 'MaximumResidual', 'CoverageRatio', 'AngleDeviation')) {
    Assert-ContainsText $result ("public double? $property { get; set; }") "Summary result must expose $property."
}
Assert-ContainsText $result 'public string ErrorMessage { get; set; } = string.Empty;' 'Summary result must expose the rejection reason.'
Assert-ContainsText $node 'result.ErrorMessage = first.ErrorMessage ?? string.Empty;' 'Node summary must publish the first target rejection reason.'
Assert-ContainsText $node 'item.AverageResidual' 'Result display must show fit quality instead of only an angle.'

Write-Host 'Caliper line fit quality source checks passed.'
