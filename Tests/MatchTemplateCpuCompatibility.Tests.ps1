$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$matcherPath = Join-Path $projectRoot 'Node\3-Detection\MatchTemplate\FastTemplateMatcher.cs'
$paramFormPath = Join-Path $projectRoot 'Node\3-Detection\MatchTemplate\NodeParamFormMatchTemplate.cs'
$matchNodePath = Join-Path $projectRoot 'Node\3-Detection\MatchTemplate\NodeMatchTemplate.cs'
$nccNodePath = Join-Path $projectRoot 'Node\3-Detection\MatchTemplate\NodeNccMatchTemplate.cs'
$factoryPath = Join-Path $projectRoot 'Node\NodeFactory.cs'
$nodeTypePath = Join-Path $projectRoot 'Node\INode.cs'
$processEditPanelPath = Join-Path $projectRoot 'Forms\ProcessNew\ProcessEditPanel.cs'
$projectPath = Join-Path $projectRoot 'TDJS-Vision.csproj'
$runtimeProfilePath = Join-Path $projectRoot 'Native\MatchTool.runtime.txt'
$toolTreePath = Join-Path $projectRoot 'ToolTreeView.xml'

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Expected,
        [string]$Message
    )

    Assert-True -Condition ($Text.Contains($Expected)) -Message $Message
}

$matcherSource = Get-Content -LiteralPath $matcherPath -Encoding UTF8 -Raw
$paramFormSource = Get-Content -LiteralPath $paramFormPath -Encoding UTF8 -Raw
$matchNodeSource = Get-Content -LiteralPath $matchNodePath -Encoding UTF8 -Raw
$nccNodeSource = Get-Content -LiteralPath $nccNodePath -Encoding UTF8 -Raw
$factorySource = Get-Content -LiteralPath $factoryPath -Encoding UTF8 -Raw
$nodeTypeSource = Get-Content -LiteralPath $nodeTypePath -Encoding UTF8 -Raw
$processEditPanelSource = Get-Content -LiteralPath $processEditPanelPath -Encoding UTF8 -Raw
$projectSource = Get-Content -LiteralPath $projectPath -Encoding UTF8 -Raw
$runtimeProfile = Get-Content -LiteralPath $runtimeProfilePath -Encoding UTF8 -Raw
$toolTreeSource = Get-Content -LiteralPath $toolTreePath -Encoding UTF8 -Raw

Assert-Contains $matcherSource 'IsProcessorFeaturePresent' 'Template matching must check CPU features before entering the AVX2 native library.'
Assert-Contains $matcherSource 'PfAvx2InstructionsAvailable = 40' 'Template matching must use the Windows AVX2 processor feature id.'
Assert-Contains $matcherSource 'EnsureCpuInstructionSetSupported' 'Template matching native initialization must gate unsupported CPUs before DLL API calls.'
Assert-Contains $matcherSource 'ShouldUseManagedCpuFallback' 'The original template matching node must keep its CPU fallback path.'
Assert-Contains $matcherSource 'MatchWithManagedCpuFallback' 'The original template matching node must keep the managed CPU fallback implementation.'
Assert-Contains $matcherSource 'Cv2.MatchTemplate' 'The original template matching CPU fallback must remain available.'
Assert-Contains $matcherSource 'forceNativeDemoAlgorithm' 'NCC template matching must have a switch that forces the demo native algorithm path.'
Assert-Contains $matcherSource '!forceNativeDemoAlgorithm && ShouldUseManagedCpuFallback()' 'NCC template matching must bypass the original managed fallback path.'
Assert-Contains $matcherSource 'RequiresAvx2' 'Template matching must read the native runtime profile.'
Assert-Contains $matcherSource 'InvalidOperationException' 'Unsupported CPU errors must be user-visible instead of entering native code.'
Assert-Contains $matcherSource 'AVX2' 'Unsupported CPU errors must explain the AVX2 compatibility requirement.'
Assert-Contains $matcherSource 'CpuSupportsAvx2' 'Diagnostics must log the detected CPU AVX2 capability.'

Assert-True -Condition (Test-Path -LiteralPath $runtimeProfilePath -PathType Leaf) -Message 'MatchTool runtime profile must be shipped with the native DLL.'
Assert-Contains $runtimeProfile 'instruction_set=x64-generic' 'The current runtime profile must identify the shipped native DLL as generic x64.'
Assert-Contains $runtimeProfile 'requires_avx2=false' 'The current runtime profile must mark AVX2 as optional.'
Assert-Contains $runtimeProfile 'CPU' 'The runtime profile must document the CPU compatibility behavior.'
Assert-Contains $projectSource '<Content Include="Native\MatchTool.runtime.txt">' 'Project file must copy the MatchTool runtime profile to the output directory.'
Assert-Contains $projectSource 'Compile Include="Node\3-Detection\MatchTemplate\NodeNccMatchTemplate.cs"' 'Project file must compile the separate NCC node.'
Assert-Contains $toolTreeSource 'Tag="MatchTemplate"' 'Detection toolbox must keep the original template matching node unchanged.'
Assert-Contains $toolTreeSource 'Tag="NccMatchTemplate"' 'Detection toolbox must expose NCC template matching as a separate node.'
Assert-Contains $nodeTypeSource 'MatchTemplate' 'The original template matching node type must remain available.'
Assert-Contains $nodeTypeSource 'NccMatchTemplate' 'The NCC template matching node type must be added separately.'
Assert-Contains $factorySource 'case NodeType.MatchTemplate:' 'Factory must keep the original template matching registration.'
Assert-Contains $factorySource 'new NodeMatchTemplate' 'Factory must still create the original template matching node.'
Assert-Contains $factorySource 'case NodeType.NccMatchTemplate:' 'Factory must register the NCC template matching node type.'
Assert-Contains $factorySource 'new NodeNccMatchTemplate' 'Factory must create the separate NCC template matching node.'
Assert-Contains $processEditPanelSource 'case NodeType.NccMatchTemplate:' 'Process canvas drag creation must register the NCC template matching node type.'
Assert-Contains $processEditPanelSource 'new NodeNccMatchTemplate' 'Process canvas drag creation must create the separate NCC template matching node.'
Assert-Contains $matchNodeSource ': this(nodeId, nodeName, process, nodeType, false,' 'Original template matching node must use the default algorithm path.'
Assert-Contains $nccNodeSource ': base(nodeId, nodeName, process, nodeType, true,' 'NCC node must force the demo native algorithm path.'
Assert-Contains $nccNodeSource 'MatchTemplateFromSubscribedMat' 'NCC node must use the Mat fast path instead of the original Bitmap refresh path.'
Assert-Contains $nccNodeSource 'BuildResult(matchResult, param,' 'NCC node must build results from the fast-path match result.'
Assert-Contains $nccNodeSource 'outputMat, outputGrayMat' 'NCC node must reuse subscribed Mat output without converting the result image back from Bitmap.'
Assert-True -Condition (-not $nccNodeSource.Contains('UpdataImage')) -Message 'NCC node must not refresh the form Bitmap during runtime.'
Assert-Contains $matcherSource 'Match(NodeParamMatchTemplate param, Mat sourceImage' 'NCC native matcher must accept Mat input directly.'
Assert-Contains $paramFormSource 'nativeInputMat' 'NCC node must prefer the subscribed gray Mat for native matching.'
Assert-Contains $matcherSource 'PrepareNativeTemplate' 'NCC native matcher must reuse an existing 24bpp template without copying it every frame.'
Assert-True -Condition (-not $matcherSource.Contains('PrepareNativeSourceMat')) -Message 'NCC native matcher must not convert the subscribed Mat before MatchMem.'
Assert-Contains $matcherSource 'channels = source.Channels()' 'NCC native matcher must forward the source Mat channel count to MatchMem.'
Assert-Contains $matcherSource 'FastMatchEngineHandle.MatchMat' 'NCC native matcher must pass Mat memory directly to MatchMem.'

Write-Host 'Match template CPU compatibility checks passed.'
