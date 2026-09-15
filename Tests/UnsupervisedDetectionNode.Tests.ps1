$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$csprojPath = Join-Path $root "TDJS-Vision.csproj"
$toolTreePath = Join-Path $root "ToolTreeView.xml"
$nodeTypePath = Join-Path $root "Node\INode.cs"
$nodeFactoryPath = Join-Path $root "Node\NodeFactory.cs"
$processEditPanelPath = Join-Path $root "Forms\ProcessNew\ProcessEditPanel.cs"
$flowCanvasPath = Join-Path $root "Forms\ProcessNew\ProcessFlowCanvas.cs"
$wizardPath = Join-Path $root "Forms\ProcessNew\FormNewProcessWizard.cs"
$nodeBasePath = Join-Path $root "Node\NodeBase.cs"
$detectorPath = Join-Path $root "Forms\AiTrainForm\UnsupervisedAnomalibDetector.cs"
$packagePath = Join-Path $root "Forms\AiTrainForm\UnsupervisedTemplatePackage.cs"
$ifPath = Join-Path $root "Node\6-LogicTool\If\NodeParamFormIf.cs"
$conditionRunPath = Join-Path $root "Node\6-LogicTool\ConditionRun\NodeParamFormConditionRun.cs"
$messageBoxPath = Join-Path $root "Node\6-LogicTool\MessageBox\NodeParamFormMessageBox.cs"
$summarizePath = Join-Path $root "Node\7-ResultProcessing\ResultSummarize\ParamFormSummarize.cs"
$overlayDrawPath = Join-Path $root "Node\7-ResultProcessing\ResultOverlayDraw\NodeResultOverlayDraw.cs"
$overlayDraw2Path = Join-Path $root "Node\7-ResultProcessing\ResultOverlayDraw2\NodeResultOverlayDraw2.cs"
$zhLanguagePath = Join-Path $root "Languages\zh-CN.json"
$enLanguagePath = Join-Path $root "Languages\en-US.json"

$nodeDir = Join-Path $root "Node\3-Detection\Unsupervised"
$nodePath = Join-Path $nodeDir "NodeUnsupervisedDetection.cs"
$paramPath = Join-Path $nodeDir "NodeParamUnsupervisedDetection.cs"
$resultPath = Join-Path $nodeDir "NodeResultUnsupervisedDetection.cs"
$formPath = Join-Path $nodeDir "ParamFormUnsupervisedDetection.cs"
$designerPath = Join-Path $nodeDir "ParamFormUnsupervisedDetection.Designer.cs"
$runtimePath = Join-Path $nodeDir "UnsupervisedDetectionRuntime.cs"
$unsupervisedNodeText = -join @([char]0x65E0, [char]0x76D1, [char]0x7763, [char]0x68C0, [char]0x6D4B)
$unsupervisedTemplateText = -join @([char]0x65E0, [char]0x76D1, [char]0x7763, [char]0x6A21, [char]0x677F)
$unsupervisedOutputText = -join @([char]0x65E0, [char]0x76D1, [char]0x7763, [char]0x8F93, [char]0x51FA, [char]0x7ED3, [char]0x679C)
$thresholdText = -join @([char]0x5F02, [char]0x5E38, [char]0x9608, [char]0x503C, [char]0x28, [char]0x30, [char]0x3D, [char]0x6A21, [char]0x677F, [char]0x29)
$inferenceBatchText = -join @([char]0x63A8, [char]0x7406, [char]0x6279, [char]0x6B21)
$miniAreaText = -join @([char]0x6700, [char]0x5C0F, [char]0x7F3A, [char]0x9677, [char]0x9762, [char]0x79EF)
$cpuFallbackText = -join @([char]0x43, [char]0x50, [char]0x55, [char]0x56DE, [char]0x9000)
$summaryThresholdText = -join @([char]0x9608, [char]0x503C, [char]0x3A)
$summaryBatchText = -join @([char]0x6279, [char]0x6B21, [char]0x3A)
$summaryAreaText = -join @([char]0x9762, [char]0x79EF, [char]0x3A)
$summaryBoxesText = -join @([char]0x6846, [char]0x3A)

function Assert-FileExists {
    param([string]$Path, [string]$Message)
    if (-not (Test-Path -LiteralPath $Path)) {
        throw $Message
    }
}

function Assert-Contains {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if ($Text.IndexOf($Pattern, [System.StringComparison]::Ordinal) -lt 0) {
        throw $Message
    }
}

function Assert-NotContains {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if ($Text.IndexOf($Pattern, [System.StringComparison]::Ordinal) -ge 0) {
        throw $Message
    }
}

Assert-FileExists $nodePath "Missing unsupervised detection node class."
Assert-FileExists $paramPath "Missing unsupervised detection param class."
Assert-FileExists $resultPath "Missing unsupervised detection result class."
Assert-FileExists $formPath "Missing unsupervised detection parameter form."
Assert-FileExists $designerPath "Missing unsupervised detection designer file."
Assert-FileExists $runtimePath "Missing unsupervised detection runtime adapter."

$csproj = Get-Content -LiteralPath $csprojPath -Encoding UTF8 -Raw
$toolTree = Get-Content -LiteralPath $toolTreePath -Encoding UTF8 -Raw
$nodeType = Get-Content -LiteralPath $nodeTypePath -Encoding UTF8 -Raw
$nodeFactory = Get-Content -LiteralPath $nodeFactoryPath -Encoding UTF8 -Raw
$processEditPanel = Get-Content -LiteralPath $processEditPanelPath -Encoding UTF8 -Raw
$flowCanvas = Get-Content -LiteralPath $flowCanvasPath -Encoding UTF8 -Raw
$wizard = Get-Content -LiteralPath $wizardPath -Encoding UTF8 -Raw
$nodeBase = Get-Content -LiteralPath $nodeBasePath -Encoding UTF8 -Raw
$detector = Get-Content -LiteralPath $detectorPath -Encoding UTF8 -Raw
$package = Get-Content -LiteralPath $packagePath -Encoding UTF8 -Raw
$ifForm = Get-Content -LiteralPath $ifPath -Encoding UTF8 -Raw
$conditionRun = Get-Content -LiteralPath $conditionRunPath -Encoding UTF8 -Raw
$messageBox = Get-Content -LiteralPath $messageBoxPath -Encoding UTF8 -Raw
$summarize = Get-Content -LiteralPath $summarizePath -Encoding UTF8 -Raw
$overlayDraw = Get-Content -LiteralPath $overlayDrawPath -Encoding UTF8 -Raw
$overlayDraw2 = Get-Content -LiteralPath $overlayDraw2Path -Encoding UTF8 -Raw
$node = Get-Content -LiteralPath $nodePath -Encoding UTF8 -Raw
$param = Get-Content -LiteralPath $paramPath -Encoding UTF8 -Raw
$result = Get-Content -LiteralPath $resultPath -Encoding UTF8 -Raw
$form = Get-Content -LiteralPath $formPath -Encoding UTF8 -Raw
$designer = Get-Content -LiteralPath $designerPath -Encoding UTF8 -Raw
$runtime = Get-Content -LiteralPath $runtimePath -Encoding UTF8 -Raw
$zhLanguage = Get-Content -LiteralPath $zhLanguagePath -Encoding UTF8 -Raw
$enLanguage = Get-Content -LiteralPath $enLanguagePath -Encoding UTF8 -Raw

Assert-Contains $nodeType "UnsupervisedDetection" "NodeType must include UnsupervisedDetection."
Assert-Contains $nodeFactory "case NodeType.UnsupervisedDetection:" "NodeFactory must create the unsupervised detection node."
Assert-Contains $nodeFactory "new NodeUnsupervisedDetection" "NodeFactory must instantiate NodeUnsupervisedDetection."
Assert-Contains $processEditPanel "case NodeType.UnsupervisedDetection:" "ProcessEditPanel must deserialize/create the unsupervised detection node."
Assert-Contains $toolTree ('Text="' + $unsupervisedNodeText + '"') "Tool tree must show the unsupervised detection node in Chinese."
Assert-Contains $toolTree 'Tag="UnsupervisedDetection"' "Tool tree must use the UnsupervisedDetection node type."
Assert-Contains $wizard "case NodeType.UnsupervisedDetection:" "Wizard icon logic must know the unsupervised detection node type."
Assert-Contains $wizard 'return "US";' "Wizard must provide a compact icon text for unsupervised detection."
Assert-Contains $zhLanguage '"ProcessNew.NodeType.UnsupervisedDetection"' "Chinese language file must include the node display name key."
Assert-Contains $enLanguage '"ProcessNew.NodeType.UnsupervisedDetection"' "English language file must include the node display name key."

Assert-Contains $csproj 'Node\3-Detection\Unsupervised\NodeUnsupervisedDetection.cs' "Project must compile NodeUnsupervisedDetection."
Assert-Contains $csproj 'Node\3-Detection\Unsupervised\NodeParamUnsupervisedDetection.cs' "Project must compile NodeParamUnsupervisedDetection."
Assert-Contains $csproj 'Node\3-Detection\Unsupervised\NodeResultUnsupervisedDetection.cs' "Project must compile NodeResultUnsupervisedDetection."
Assert-Contains $csproj 'Node\3-Detection\Unsupervised\ParamFormUnsupervisedDetection.cs' "Project must compile ParamFormUnsupervisedDetection."
Assert-Contains $csproj 'Node\3-Detection\Unsupervised\ParamFormUnsupervisedDetection.Designer.cs' "Project must compile the designer file."
Assert-Contains $csproj 'DependentUpon>ParamFormUnsupervisedDetection.cs' "Designer file must be attached to the form for WinForms designer support."

Assert-Contains $detector "LoadModel" "Native detector wrapper must support loading a trained template model."
Assert-Contains $detector "InferBgr" "Native detector wrapper must expose BGR inference."
Assert-Contains $detector 'EntryPoint = "Infer"' "Native detector wrapper must declare the Infer export."
Assert-Contains $detector "UnsupervisedAnomalibResult" "Inference result DTO must carry return code, score and boxes."
Assert-Contains $package "Extract" "Template package must support extracting manifest and model.onnx for inference."
Assert-Contains $runtime "UnsupervisedTemplatePackage.Extract" "Runtime adapter must extract the training-window template package."
Assert-Contains $runtime "RoiEnabled" "Runtime adapter must honor template ROI metadata."
Assert-Contains $runtime "OffsetBoxesToSourceImage" "Runtime adapter must offset ROI inference boxes back to source image coordinates."
Assert-Contains $runtime "OffsetBoxesToSourceImage(nativeResult.Boxes, roiRect, sourceImage.Width, sourceImage.Height, inferImage.Width, inferImage.Height, manifest.InputWidth, manifest.InputHeight)" "Runtime adapter must scale native model-coordinate boxes before drawing on source images."
Assert-Contains $runtime "ScaleBoxToInferenceImage" "Runtime adapter must convert native model-coordinate boxes to inference image coordinates."
Assert-Contains $runtime "modelInputWidth" "Runtime adapter must use the template model input width when scaling boxes."
Assert-Contains $runtime "modelInputHeight" "Runtime adapter must use the template model input height when scaling boxes."
Assert-Contains $runtime "ResolveThreshold" "Runtime adapter must resolve the node anomaly threshold."
Assert-Contains $runtime "ResolveMiniArea" "Runtime adapter must resolve the node minimum defect area."
Assert-Contains $runtime "ResolveInferenceBatchSize" "Runtime adapter must resolve the node inference batch size."
Assert-Contains $nodeBase "GetCanvasParameterSummary" "NodeBase must expose a canvas runtime-parameter summary hook."
Assert-Contains $flowCanvas "DrawNodeParameterSummary" "Flow canvas must draw runtime parameters on the node card."
Assert-Contains $runtime "LoadModelWithFallback" "Runtime adapter must retry GPU/AUTO template loading with CPU fallback."
Assert-Contains $runtime "ShouldRetryLoadModelOnCpu" "Runtime adapter must isolate the CPU fallback decision for failed GPU/AUTO loading."
Assert-Contains $runtime $cpuFallbackText "Runtime adapter must report when GPU/AUTO loading is recovered by CPU fallback."
Assert-Contains $runtime "BuildLoadModelFailureMessage" "Runtime adapter must include model loading diagnostics when all load attempts fail."

Assert-Contains $param "TemplatePath" "Node param must persist the trained template path."
Assert-Contains $param "Text1" "Node param must persist image subscription node text."
Assert-Contains $param "Text2" "Node param must persist image subscription result text."
Assert-Contains $param "Threshold" "Node param must persist the anomaly threshold."
Assert-Contains $param "InferenceBatchSize" "Node param must persist the inference batch size."
Assert-Contains $param "DefaultThreshold = 0F" "Default anomaly threshold must follow the calibrated template threshold."
Assert-Contains $param "FallbackThreshold = 0.3F" "Runtime must keep a legacy fallback threshold when a template has no calibrated score."
Assert-Contains $param "DefaultInferenceBatchSize = 0" "Default inference batch size must mean 'use template batch size' for legacy GPU templates."
Assert-Contains $param "MiniArea" "Node param must persist the minimum defect area."
Assert-NotContains $node "public override string GetCanvasParameterSummary()" "Unsupervised node must not display runtime parameters on the canvas node."
Assert-NotContains $node $summaryThresholdText "Unsupervised node must not display the threshold summary."
Assert-NotContains $node $summaryBatchText "Unsupervised node must not display the batch-size summary."
Assert-NotContains $node $summaryAreaText "Unsupervised node must not display the area summary."
Assert-NotContains $node $summaryBoxesText "Unsupervised node must not display the max-boxes summary."
Assert-Contains $form "nodeSubscription1.Init(node)" "Param form must initialize the image subscription control."
Assert-Contains $form "GetOutputImage" "Param form must expose the subscribed OutputImage."
Assert-Contains $form "numericUpDownThreshold.Value" "Param form must save the anomaly threshold control value."
Assert-Contains $form "numericUpDownInferenceBatchSize.Value" "Param form must save the inference batch size control value."
Assert-Contains $form "numericUpDownMiniArea.Value" "Param form must save the minimum defect area control value."
Assert-Contains $form "value < 0F || value > 1F" "Param form must allow zero threshold so new nodes can follow the template score."
Assert-Contains $form "param.InferenceBatchSize <= 0" "Param form must apply the template batch size when old node params have no explicit inference batch."
Assert-Contains $designer "nodeSubscription1" "Designer must contain the subscription control."
Assert-Contains $designer "textBoxTemplatePath" "Designer must contain the template path text box."
Assert-Contains $designer ('Text = "' + $unsupervisedTemplateText + '"') "Designer labels must use Simplified Chinese text."
Assert-Contains $designer "numericUpDownThreshold" "Designer must contain the anomaly threshold numeric control."
Assert-Contains $designer "numericUpDownInferenceBatchSize" "Designer must contain the inference batch numeric control."
Assert-Contains $designer "numericUpDownMiniArea" "Designer must contain the minimum defect area numeric control."
Assert-Contains $designer $thresholdText "Designer must label the anomaly threshold in Simplified Chinese."
Assert-Contains $designer ('Text = "' + $inferenceBatchText + '"') "Designer must label the inference batch size in Simplified Chinese."
Assert-Contains $designer ('Text = "' + $miniAreaText + '"') "Designer must label the minimum defect area in Simplified Chinese."

Assert-Contains $result ('[DisplayName("' + $unsupervisedOutputText + '")]') "Result must expose AlgorithmResult with a Chinese display name."
Assert-Contains $result "AlgorithmResult" "Result must expose AlgorithmResult for ROI drawing subscription."
Assert-Contains $node "res.AlgorithmResult.Clear();" "Node must clear the previous algorithm result before each run."
Assert-Contains $node "RectsNgMap" "Node must publish NG boxes through RectsNgMap."
Assert-Contains $node "ColorRotatedRect" "Node must convert abnormal boxes into drawable rectangles."
Assert-Contains $node "ApplyResultOutcomeStatus" "Node must mark successful NG outcomes on the canvas."
Assert-Contains $nodeBase "NodeType.UnsupervisedDetection" "Deleting the node must release the unsupervised model handle."

Assert-Contains $ifForm "case NodeType.UnsupervisedDetection:" "If node must support unsupervised detection OK/NG results."
Assert-Contains $conditionRun "case NodeType.UnsupervisedDetection:" "ConditionRun node must support unsupervised detection OK/NG results."
Assert-Contains $messageBox "case NodeType.UnsupervisedDetection:" "MessageBox node must support unsupervised detection OK/NG results."
Assert-Contains $summarize "case NodeType.UnsupervisedDetection:" "Result summarize must support unsupervised AlgorithmResult."
Assert-Contains $overlayDraw "NodeType.UnsupervisedDetection" "ROI overlay draw must treat successful empty unsupervised detection rectangles as a valid empty result."
Assert-Contains $overlayDraw2 "NodeType.UnsupervisedDetection" "ROI overlay draw 2 must treat successful empty unsupervised detection rectangles as a valid empty result."

Write-Host "Unsupervised detection node checks passed."
