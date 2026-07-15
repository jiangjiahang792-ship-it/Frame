$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$csprojPath = Join-Path $root "TDJS-Vision.csproj"
$formMainPath = Join-Path $root "FormMain.cs"
$formMainDesignerPath = Join-Path $root "FormMain.Designer.cs"
$toolTreePath = Join-Path $root "ToolTreeView.xml"
$nodeTypePath = Join-Path $root "Node\INode.cs"
$nodeFactoryPath = Join-Path $root "Node\NodeFactory.cs"
$nodeBasePath = Join-Path $root "Node\NodeBase.cs"
$flowCanvasPath = Join-Path $root "Forms\ProcessNew\ProcessFlowCanvas.cs"
$taskPath = Join-Path $root "FLOW_CANVAS_B_PLAN_TASKS.md"

$runtimeBootstrapperPath = Join-Path $root "Forms\AiTrainForm\LargeModelRuntimeBootstrapper.cs"
$detectorPath = Join-Path $root "Forms\AiTrainForm\LargeModelDinov2Detector.cs"
$trainingServicePath = Join-Path $root "Forms\AiTrainForm\LargeModelTrainingService.cs"
$trainingModelsPath = Join-Path $root "Forms\AiTrainForm\LargeModelTrainingModels.cs"
$templatePackagePath = Join-Path $root "Forms\AiTrainForm\LargeModelTemplatePackage.cs"
$trainFormPath = Join-Path $root "Forms\AiTrainForm\LargeModelTrainForm.cs"
$trainDesignerPath = Join-Path $root "Forms\AiTrainForm\LargeModelTrainForm.Designer.cs"

$nodeDir = Join-Path $root "Node\3-Detection\LargeModel"
$nodePath = Join-Path $nodeDir "NodeLargeModelDetection.cs"
$paramPath = Join-Path $nodeDir "NodeParamLargeModelDetection.cs"
$resultPath = Join-Path $nodeDir "NodeResultLargeModelDetection.cs"
$paramFormPath = Join-Path $nodeDir "ParamFormLargeModelDetection.cs"
$paramDesignerPath = Join-Path $nodeDir "ParamFormLargeModelDetection.Designer.cs"
$runtimePath = Join-Path $nodeDir "LargeModelDetectionRuntime.cs"
$largeModelTemplateText = -join @([char]0x5927, [char]0x6A21, [char]0x578B, [char]0x6A21, [char]0x677F)
$largeModelCallText = -join @([char]0x5927, [char]0x6A21, [char]0x578B, [char]0x8C03, [char]0x7528)
$largeModelOutputText = -join @([char]0x5927, [char]0x6A21, [char]0x578B, [char]0x8F93, [char]0x51FA, [char]0x7ED3, [char]0x679C)
$largeModelIntegrationText = -join @([char]0x5927, [char]0x6A21, [char]0x578B, [char]0x7B97, [char]0x6CD5, [char]0x96C6, [char]0x6210)
$trainingParamsText = -join @([char]0x8BAD, [char]0x7EC3, [char]0x53C2, [char]0x6570)
$trainingEpochsText = -join @([char]0x8BAD, [char]0x7EC3, [char]0x8F6E, [char]0x6570)
$iterationText = -join @([char]0x8FED, [char]0x4EE3)
$summaryThresholdText = -join @([char]0x9608, [char]0x503C, [char]0x3A)
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

function Assert-Matches {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if (-not [System.Text.RegularExpressions.Regex]::IsMatch($Text, $Pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        throw $Message
    }
}

Assert-FileExists $runtimeBootstrapperPath "Missing large-model runtime bootstrapper."
Assert-FileExists $detectorPath "Missing DINOv2 native wrapper."
Assert-FileExists $trainingServicePath "Missing large-model training service."
Assert-FileExists $trainingModelsPath "Missing large-model training models."
Assert-FileExists $templatePackagePath "Missing large-model template package."
Assert-FileExists $trainFormPath "Missing large-model training form."
Assert-FileExists $trainDesignerPath "Missing large-model training designer."
Assert-FileExists $nodePath "Missing large-model runtime node."
Assert-FileExists $paramPath "Missing large-model node param."
Assert-FileExists $resultPath "Missing large-model node result."
Assert-FileExists $paramFormPath "Missing large-model node param form."
Assert-FileExists $paramDesignerPath "Missing large-model node param designer."
Assert-FileExists $runtimePath "Missing large-model node runtime."

$csproj = Get-Content -LiteralPath $csprojPath -Encoding UTF8 -Raw
$formMain = Get-Content -LiteralPath $formMainPath -Encoding UTF8 -Raw
$formMainDesigner = Get-Content -LiteralPath $formMainDesignerPath -Encoding UTF8 -Raw
$toolTree = Get-Content -LiteralPath $toolTreePath -Encoding UTF8 -Raw
$nodeType = Get-Content -LiteralPath $nodeTypePath -Encoding UTF8 -Raw
$nodeFactory = Get-Content -LiteralPath $nodeFactoryPath -Encoding UTF8 -Raw
$nodeBase = Get-Content -LiteralPath $nodeBasePath -Encoding UTF8 -Raw
$flowCanvas = Get-Content -LiteralPath $flowCanvasPath -Encoding UTF8 -Raw
$bootstrapper = Get-Content -LiteralPath $runtimeBootstrapperPath -Encoding UTF8 -Raw
$detector = Get-Content -LiteralPath $detectorPath -Encoding UTF8 -Raw
$service = Get-Content -LiteralPath $trainingServicePath -Encoding UTF8 -Raw
$trainingModels = Get-Content -LiteralPath $trainingModelsPath -Encoding UTF8 -Raw
$templatePackage = Get-Content -LiteralPath $templatePackagePath -Encoding UTF8 -Raw
$trainForm = Get-Content -LiteralPath $trainFormPath -Encoding UTF8 -Raw
$trainDesigner = Get-Content -LiteralPath $trainDesignerPath -Encoding UTF8 -Raw
$node = Get-Content -LiteralPath $nodePath -Encoding UTF8 -Raw
$param = Get-Content -LiteralPath $paramPath -Encoding UTF8 -Raw
$result = Get-Content -LiteralPath $resultPath -Encoding UTF8 -Raw
$paramForm = Get-Content -LiteralPath $paramFormPath -Encoding UTF8 -Raw
$paramDesigner = Get-Content -LiteralPath $paramDesignerPath -Encoding UTF8 -Raw
$runtime = Get-Content -LiteralPath $runtimePath -Encoding UTF8 -Raw
$task = Get-Content -LiteralPath $taskPath -Encoding UTF8 -Raw

Assert-Contains $formMainDesigner 'this.toolStripMenuItem3.Click += new System.EventHandler(this.LargeModelTrainToolStripMenuItem_Click);' "FormMain large-model train menu click event is not bound."
Assert-Contains $formMain 'private LargeModelTrainForm largeModelTrainForm;' "FormMain must keep the large-model training form field."
Assert-Contains $formMain 'private void LargeModelTrainToolStripMenuItem_Click(object sender, EventArgs e)' "FormMain missing large-model train menu handler."
Assert-Contains $formMain 'using (var form = new LargeModelTrainForm())' "Large-model training form must be created per open."
Assert-Contains $formMain 'exclusiveWorkspacePresenter.ShowDialog(this, form);' "大模型训练窗口必须使用独占工作区显示接口。"

Assert-Contains $bootstrapper 'Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LargeModelDll")' "Large-model runtime must be isolated under LargeModelDll."
Assert-Contains $bootstrapper '"Dinov2AD.dll"' "Runtime check must include Dinov2AD.dll."
Assert-Contains $bootstrapper '"OpenCvBridge.dll"' "Runtime check must include OpenCvBridge.dll."
Assert-Contains $bootstrapper '"device.license"' "Runtime check must include device.license."
Assert-Contains $detector 'private const string DllName = "Dinov2AD.dll";' "DINOv2 wrapper must late-load Dinov2AD.dll."
Assert-Contains $detector "LoadLibraryEx" "DINOv2 wrapper must preload from LargeModelDll."
Assert-Contains $detector 'EntryPoint = "init"' "DINOv2 wrapper must declare init export."
Assert-Contains $detector 'EntryPoint = "train"' "DINOv2 wrapper must declare train export."
Assert-Contains $detector 'EntryPoint = "infer"' "DINOv2 wrapper must declare infer export."
Assert-Contains $detector 'EntryPoint = "release"' "DINOv2 wrapper must declare release export."
Assert-Contains $detector "InferBgr" "DINOv2 wrapper must expose an OpenCvSharp BGR inference entry."
Assert-Contains $detector "CvPtr" "DINOv2 inference must pass the OpenCvSharp Mat handle directly."
Assert-NotContains $detector "Cv2.ImWrite" "Large-model inference must not pass images through temp files."

Assert-Contains $service "LargeModelRuntimeBootstrapper.ValidateRuntime" "Training must validate LargeModelDll before running."
Assert-Contains $service "detector.TrainMemoryBank" "Training service must call DINOv2 train to generate the bank."
Assert-Contains $service 'LargeModelTemplatePackage.Create(request.OutputTemplatePath, manifest, modelPath, bankPath)' "Training package must include the selected native model and bank."
Assert-Contains $templatePackage "bank.bin" "Large-model package must include the generated bank."
Assert-Contains $templatePackage "manifest.json" "Large-model package must include the manifest."
Assert-Contains $templatePackage "WriteModel" "Large-model package must write the selected engine or ONNX model."
Assert-Contains $templatePackage "ModelPath" "Large-model extraction must expose the packaged model path."
Assert-Contains $templatePackage "ExtractModelIfPresent" "Large-model extraction must unpack the packaged model when present."
Assert-Contains $templatePackage "CompressionLevel.NoCompression" "Large-model package should store native model without expensive compression."
Assert-Contains $trainDesigner $largeModelTemplateText "Large-model train designer must contain Simplified Chinese text."
Assert-Contains $trainForm "LargeModelTrainingRequest" "Large-model train form must build the training request."
Assert-Contains $trainForm ".tdlarge" "Large-model template extension must be independent."
Assert-Contains $trainDesigner $trainingParamsText "Large-model train page must keep the demo-aligned training-parameter group."
Assert-Contains $trainDesigner "groupBoxTrainingParams" "Large-model train page must define a training-parameter group."
Assert-Contains $trainDesigner "tableLayoutPanelTrainingParams" "Large-model train page must define a training-parameter layout."
Assert-Contains $trainDesigner "comboBoxModelType" "Large-model train page must show model precision control from demo."
Assert-Contains $trainDesigner "numericUpDownThreshold" "Large-model train page must show image-threshold control from demo."
Assert-Matches $trainDesigner "numericUpDownThreshold\.Maximum\s*=\s*new decimal\(new int\[\]\s*\{\s*1000000," "Large-model image threshold is a distance threshold and must not be capped at 1."
Assert-Contains $trainDesigner "numericUpDownMiniArea" "Large-model train page must show area-threshold control from demo."
Assert-Contains $trainDesigner "numericUpDownInputWidth" "Large-model train page must show input-width control because DINOv2 export supports resolution."
Assert-Contains $trainDesigner "numericUpDownInputHeight" "Large-model train page must show input-height control because DINOv2 export supports resolution."
Assert-Contains $trainDesigner "labelInputWidth" "Large-model train page must show input-width label."
Assert-Contains $trainDesigner "labelInputHeight" "Large-model train page must show input-height label."
Assert-Contains $trainDesigner "this.numericUpDownInputWidth.Increment = new decimal(new int[] {" "Large-model input-width control must define a DINOv2 patch-size step."
Assert-Contains $trainDesigner "this.numericUpDownInputHeight.Increment = new decimal(new int[] {" "Large-model input-height control must define a DINOv2 patch-size step."
Assert-Contains $trainDesigner "            14," "Large-model input-size controls must step by DINOv2 patch size 14."
Assert-Contains $trainDesigner "comboBoxDevice" "Large-model train page must show device control from demo."
Assert-Contains $trainForm "comboBoxModelType.Text" "Large-model train form must read model precision from UI."
Assert-Contains $trainForm "numericUpDownThreshold.Value" "Large-model train form must read image threshold from UI."
Assert-Contains $trainForm "numericUpDownMiniArea.Value" "Large-model train form must read area threshold from UI."
Assert-Contains $trainForm "numericUpDownInputWidth.Value" "Large-model train form must read input width from UI."
Assert-Contains $trainForm "numericUpDownInputHeight.Value" "Large-model train form must read input height from UI."
Assert-Contains $trainForm "comboBoxDevice.Text" "Large-model train form must read device from UI."
Assert-NotContains $trainDesigner '"AUTO"' "Large-model train page must not show AUTO because the C# demo only exposes CPU/GPU."
Assert-NotContains $trainDesigner $trainingEpochsText "Large-model train form must not show training epochs text."
Assert-NotContains $trainDesigner $iterationText "Large-model train form must not show iteration text."
Assert-NotContains $trainDesigner "numericUpDownEpochs" "Large-model train form must not show epochs control."
Assert-NotContains $trainDesigner "numericUpDownBatchSize" "Large-model train form must not show batch-size control."
Assert-NotContains $trainDesigner "labelEpochs" "Large-model train form must not show epochs label."
Assert-NotContains $trainDesigner "labelBatchSize" "Large-model train form must not show batch-size label."
Assert-NotContains $trainForm "MaxEpochs" "Large-model train request must not pass unsupported epochs."
Assert-NotContains $trainForm "BatchSize" "Large-model train request must not pass unsupported batch size."
Assert-NotContains $trainForm $iterationText "Large-model train request must not pass unsupported iterations."
Assert-NotContains $trainingModels "MaxEpochs" "Large-model request model must not define unsupported epochs."
Assert-NotContains $trainingModels "BatchSize" "Large-model request model must not define unsupported batch size."
Assert-NotContains $service "MaxEpochs" "Large-model training service must not use unsupported epochs."
Assert-NotContains $service "BatchSize" "Large-model training service must not use unsupported batch size."

Assert-Contains $nodeType "LargeModelDetection" "NodeType must include LargeModelDetection."
Assert-Contains $nodeFactory "case NodeType.LargeModelDetection:" "NodeFactory must create the large-model node."
Assert-Contains $nodeFactory "new NodeLargeModelDetection" "NodeFactory must instantiate NodeLargeModelDetection."
Assert-Contains $toolTree ('Text="' + $largeModelCallText + '"') "Tool tree must show the large-model node."
Assert-Contains $toolTree 'Tag="LargeModelDetection"' "Tool tree tag must match NodeType."
Assert-Contains $nodeBase "NodeType.LargeModelDetection" "Node removal must release large-model native handles."
Assert-Contains $nodeBase "GetCanvasParameterSummary" "NodeBase must expose a canvas runtime-parameter summary hook."
Assert-Contains $flowCanvas "DrawNodeParameterSummary" "Flow canvas must draw runtime parameters on the node card."

Assert-Contains $csproj 'Forms\AiTrainForm\LargeModelTrainForm.cs' "Project must include the large-model training form."
Assert-Contains $csproj 'Forms\AiTrainForm\LargeModelTrainForm.Designer.cs' "Project must include the large-model designer."
Assert-Contains $csproj 'Node\3-Detection\LargeModel\NodeLargeModelDetection.cs' "Project must include the large-model node."
Assert-Contains $csproj 'Node\3-Detection\LargeModel\ParamFormLargeModelDetection.Designer.cs' "Project must include the large-model param designer."
Assert-Contains $csproj 'DependentUpon>LargeModelTrainForm.cs' "Large-model train designer must depend on the form."
Assert-Contains $csproj 'DependentUpon>ParamFormLargeModelDetection.cs' "Large-model node designer must depend on the param form."
Assert-NotContains $csproj '<Reference Include="Dinov2AD"' "Project must not hard-reference Dinov2AD.dll."
Assert-NotContains $csproj '<Content Include="LargeModelDll' "Project must not bind LargeModelDll as content."

Assert-Contains $param "TemplatePath" "Large-model params must save the template path."
Assert-Contains $param "ImageThreshold" "Large-model params must save the image threshold."
Assert-Contains $param "AreaThreshold" "Large-model params must save the area threshold."
Assert-Contains $param "DefaultImageThreshold = 0F" "Default image threshold must use the bank threshold."
Assert-NotContains $node "public override string GetCanvasParameterSummary()" "大模型调用节点不应在画布显示运行参数摘要。"
Assert-NotContains $node $summaryThresholdText "大模型调用节点不应显示阈值摘要。"
Assert-NotContains $node $summaryAreaText "大模型调用节点不应显示面积摘要。"
Assert-NotContains $node $summaryBoxesText "大模型调用节点不应显示框数摘要。"
Assert-Contains $result ('[DisplayName("' + $largeModelOutputText + '")]') "Large-model result must use Simplified Chinese display name."
Assert-Contains $node "LargeModelDetectionRuntime" "Large-model node must use the isolated runtime."
Assert-Contains $node "AlgorithmResult" "Large-model node must publish AlgorithmResult."
Assert-Contains $node "RectsNgMap" "Large-model node must publish anomaly boxes."
Assert-Contains $paramForm "nodeSubscription1.Init(node)" "Large-model param form must support image subscription."
Assert-Contains $paramDesigner ('Text = "' + $largeModelTemplateText + '"') "Large-model param designer must contain Simplified Chinese label text."
Assert-Matches $paramDesigner "numericUpDownThreshold\.Maximum\s*=\s*new decimal\(new int\[\]\s*\{\s*1000000," "Large-model runtime image threshold is a native distance threshold and must not be capped at 1."
Assert-NotContains $paramDesigner "numericUpDownInferenceBatchSize" "Large-model runtime param form must not show unsupported inference batch-size control."
Assert-NotContains $paramDesigner "labelInferenceBatchSize" "Large-model runtime param form must not show unsupported inference batch-size label."
Assert-NotContains $paramForm "GetValidInferenceBatchSize" "Large-model runtime param form must not keep unused inference batch-size helpers."
Assert-Contains $node "BuildOutputImageSummary(inputImage)" "Large-model node failure log must include input image diagnostics."
Assert-Contains $runtime "LargeModelTemplatePackage.Extract" "Large-model runtime must extract the template package."
Assert-Contains $runtime "ResolvePrimaryModelPath" "Large-model runtime must prefer the model packaged in the template."
Assert-Contains $runtime "extractResult.ModelPath" "Large-model runtime must load the packaged engine or ONNX before falling back."
Assert-Contains $runtime "OffsetBoxesToSourceImage" "Large-model runtime must offset ROI boxes back to source image."
Assert-Contains $runtime "IsOk" "Large-model result must expose OK/NG decision."
Assert-Contains $service "ValidatePatchSizeMultiple" "Training service must validate DINOv2 input size is a multiple of 14."
Assert-Contains $service "EnsureTrainingModelPath" "Training service must prepare the ONNX or engine matching the selected resolution."
Assert-Contains $service "EnsureOnnxModelPath" "Training service must auto-export the selected-resolution ONNX before failing."
Assert-Contains $service "ExportOnnxModel" "Training service must call the isolated DINOv2 exporter when ONNX is missing."
Assert-Contains $service '"export_dinov2.py"' "ONNX export must use the demo-aligned exporter script under LargeModelDll."
Assert-Contains $service 'Path.Combine(root, "third_party", "python_native_env", "python.exe")' "ONNX export must use the isolated Python runtime under LargeModelDll."
Assert-Contains $service 'Path.Combine(root, "dinov2")' "ONNX export must use the isolated DINOv2 source under LargeModelDll."
Assert-Contains $service "--pos-mode fixed" "ONNX export must keep the demo-recommended fixed positional embedding mode."
Assert-Contains $service "--output" "ONNX export must write the generated model to the requested LargeModelDll ONNX path."
Assert-Contains $service "BuildTensorRtEngine" "GPU training must be able to build TensorRT engine from the matching ONNX."
Assert-Contains $service "--onnx=" "TensorRT build must pass the ONNX path to trtexec."
Assert-Contains $service "--saveEngine=" "TensorRT build must save the selected-resolution engine."

Assert-Contains $task $largeModelIntegrationText "Task record must include the large-model integration entry."

Write-Host "Large model integration checks passed."
