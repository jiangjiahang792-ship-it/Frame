$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$nodeDir = Join-Path $root "Node\1-Acquisition\CameraExposureGain"
$nodePath = Join-Path $nodeDir "NodeCameraExposureGain.cs"
$paramPath = Join-Path $nodeDir "NodeParamCameraExposureGain.cs"
$resultPath = Join-Path $nodeDir "NodeResultCameraExposureGain.cs"
$formPath = Join-Path $nodeDir "ParamFormCameraExposureGain.cs"
$designerPath = Join-Path $nodeDir "ParamFormCameraExposureGain.Designer.cs"
$nodeFactoryPath = Join-Path $root "Node\NodeFactory.cs"
$processEditPanelPath = Join-Path $root "Forms\ProcessNew\ProcessEditPanel.cs"
$processWizardPath = Join-Path $root "Forms\ProcessNew\FormNewProcessWizard.cs"
$processFlowCanvasPath = Join-Path $root "Forms\ProcessNew\ProcessFlowCanvas.cs"
$nodeTypePath = Join-Path $root "Node\INode.cs"
$toolTreePath = Join-Path $root "ToolTreeView.xml"
$zhLanguagePath = Join-Path $root "Languages\zh-CN.json"
$enLanguagePath = Join-Path $root "Languages\en-US.json"
$csprojPath = Join-Path $root "TDJS-Vision.csproj"
$taskFileName = (-join @([char]0x4EFB, [char]0x52A1, [char]0x8BB0, [char]0x5F55)) + ".md"
$taskPath = Join-Path $root $taskFileName
$cameraExposureGainText = -join @([char]0x76F8, [char]0x673A, [char]0x66DD, [char]0x5149, [char]0x589E, [char]0x76CA)
$selectCameraText = -join @([char]0x9009, [char]0x62E9, [char]0x76F8, [char]0x673A)
$exposureText = (-join @([char]0x66DD, [char]0x5149)) + "(us)"
$gainText = -join @([char]0x589E, [char]0x76CA)
$taskTitleText = (-join @([char]0x65B0, [char]0x589E)) + $cameraExposureGainText + (-join @([char]0x8282, [char]0x70B9))

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

Assert-FileExists $nodePath "Missing camera exposure/gain runtime node."
Assert-FileExists $paramPath "Missing camera exposure/gain node param."
Assert-FileExists $resultPath "Missing camera exposure/gain node result."
Assert-FileExists $formPath "Missing camera exposure/gain param form."
Assert-FileExists $designerPath "Missing camera exposure/gain param designer."

$nodeSource = Get-Content -LiteralPath $nodePath -Raw -Encoding UTF8
$formSource = Get-Content -LiteralPath $formPath -Raw -Encoding UTF8
$designerSource = Get-Content -LiteralPath $designerPath -Raw -Encoding UTF8
$factorySource = Get-Content -LiteralPath $nodeFactoryPath -Raw -Encoding UTF8
$processEditPanelSource = Get-Content -LiteralPath $processEditPanelPath -Raw -Encoding UTF8
$processWizardSource = Get-Content -LiteralPath $processWizardPath -Raw -Encoding UTF8
$processFlowCanvasSource = Get-Content -LiteralPath $processFlowCanvasPath -Raw -Encoding UTF8
$nodeTypeSource = Get-Content -LiteralPath $nodeTypePath -Raw -Encoding UTF8
$toolTreeSource = Get-Content -LiteralPath $toolTreePath -Raw -Encoding UTF8
$zhLanguageSource = Get-Content -LiteralPath $zhLanguagePath -Raw -Encoding UTF8
$enLanguageSource = Get-Content -LiteralPath $enLanguagePath -Raw -Encoding UTF8
$csprojSource = Get-Content -LiteralPath $csprojPath -Raw -Encoding UTF8
$taskSource = Get-Content -LiteralPath $taskPath -Raw -Encoding UTF8

Assert-Contains $nodeTypeSource "CameraExposureGain" "NodeType enum is missing CameraExposureGain."
Assert-Contains $factorySource "case NodeType.CameraExposureGain:" "NodeFactory is missing CameraExposureGain branch."
Assert-Contains $factorySource "new NodeCameraExposureGain" "NodeFactory does not create NodeCameraExposureGain."
Assert-Contains $processEditPanelSource "case NodeType.CameraExposureGain:" "ProcessEditPanel drag-create switch is missing CameraExposureGain branch."
Assert-Contains $processEditPanelSource "new NodeCameraExposureGain" "ProcessEditPanel does not create NodeCameraExposureGain while dragging."
Assert-Contains $processWizardSource "case NodeType.CameraExposureGain:" "Process wizard toolbox display is missing CameraExposureGain."
Assert-Contains $processWizardSource 'return "EG";' "Process wizard toolbox icon text is missing CameraExposureGain abbreviation."
Assert-Contains $processFlowCanvasSource "case NodeType.CameraExposureGain:" "Process canvas band color is missing CameraExposureGain."
Assert-Contains $toolTreeSource "Text=`"$cameraExposureGainText`"" "Tool tree is missing camera exposure/gain entry text."
Assert-Contains $toolTreeSource 'Tag="CameraExposureGain"' "Tool tree entry is missing CameraExposureGain tag."
Assert-Contains $zhLanguageSource "`"ProcessNew.NodeType.CameraExposureGain`": `"$cameraExposureGainText`"" "Chinese language file is missing camera exposure/gain node name."
Assert-Contains $enLanguageSource '"ProcessNew.NodeType.CameraExposureGain": "Camera Exposure/Gain"' "English language file is missing camera exposure/gain node name."
Assert-Contains $csprojSource "Node\1-Acquisition\CameraExposureGain\NodeCameraExposureGain.cs" "Project file is missing runtime node compile item."
Assert-Contains $csprojSource "Node\1-Acquisition\CameraExposureGain\ParamFormCameraExposureGain.Designer.cs" "Project file is missing designer compile item."
Assert-Contains $designerSource "this.labelCamera.Text = `"$selectCameraText`";" "Designer is missing camera selector text."
Assert-Contains $designerSource "this.labelExposure.Text = `"$exposureText`";" "Designer is missing exposure text."
Assert-Contains $designerSource "this.labelGain.Text = `"$gainText`";" "Designer is missing gain text."
Assert-Contains $formSource "Solution.Instance.CameraDevices" "Param form does not load cameras from solution."
Assert-Contains $formSource "SetNumericUpDownValueInRange" "Param form is missing numeric range clamp."
Assert-Contains $nodeSource "param.Camera.SetExposureTime(param.ExposureTime);" "Runtime node does not write exposure."
Assert-Contains $nodeSource "param.Camera.SetGain(param.Gain);" "Runtime node does not write gain."
Assert-Contains $taskSource $taskTitleText "Task log is missing camera exposure/gain entry."
