$ErrorActionPreference = 'Stop'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Assert-ContainsText {
    param([string]$Content, [string]$Expected, [string]$Message)
    if (-not $Content.Contains($Expected)) {
        throw $Message
    }
}

$projectRoot = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    (Get-Location).Path
}
else {
    Split-Path -Parent $PSScriptRoot
}
$subscriptionPath = Join-Path $projectRoot 'Node\NodeSubscription.cs'
$designerPath = Join-Path $projectRoot 'Node\NodeSubscription.Designer.cs'
$source = Get-Content -LiteralPath $subscriptionPath -Raw -Encoding UTF8
$designer = Get-Content -LiteralPath $designerPath -Raw -Encoding UTF8

Assert-ContainsText $source 'SetInputContract(SubscriptionInputContract inputContract)' 'NodeSubscription 必须允许调用方声明统一输入契约。'
Assert-ContainsText $source 'SubscriptionPortCatalog.GetOutputs' 'NodeSubscription 必须从统一端口目录生成候选。'
Assert-ContainsText $source 'SubscriptionTypeCompatibility.ConvertValue' 'NodeSubscription 运行取值必须使用统一转换服务。'
Assert-ContainsText $source 'SubscriptionSelectionItem' 'NodeSubscription 必须把界面文字与旧方案持久化路径分开。'
Assert-ContainsText $source 'SetShowAdvancedResults(bool showAdvancedResults)' 'NodeSubscription 必须允许调用方默认展开高级结果。'
Assert-ContainsText $source 'IsLegacySelection' 'NodeSubscription 必须显示旧方案兼容订阅状态。'
Assert-ContainsText $source 'IsMissing' 'NodeSubscription 必须显示结果不存在状态。'
Assert-True (-not $source.Contains('comboBox2.SelectedIndex = index1 == -1 ? 0 : index1;')) '结果不存在时不能自动回退到第一项。'

Assert-ContainsText $designer 'toolStripMenuItemShowAdvancedResults' '高级结果菜单必须由 Designer 创建。'
Assert-ContainsText $designer 'this.toolStripMenuItemShowAdvancedResults.Text = "显示高级结果";' '高级结果菜单必须使用中文 Text。'
Assert-ContainsText $designer 'this.toolStripMenuItemShowAdvancedResults.CheckOnClick = true;' '高级结果菜单必须支持勾选。'
Assert-ContainsText $designer 'this.comboBox2.ContextMenuStrip = this.contextMenuStripResults;' '结果下拉框必须绑定高级结果右键菜单。'

$sharedVariableSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Node\6-LogicTool\SharedVariable\NodeParamFormSharedVariable.cs') -Raw -Encoding UTF8
Assert-ContainsText $sharedVariableSource 'nodeSubscription1.SetInputContract(SubscriptionInputContract.AnyVisible());' '共享变量节点必须继续接受任意可见订阅类型。'
Assert-ContainsText $sharedVariableSource 'nodeSubscription1.SetShowAdvancedResults(true);' '共享变量节点必须默认展开高级结果，允许直接订阅图像源输出图像。'
Assert-True ($sharedVariableSource.IndexOf('nodeSubscription1.SetShowAdvancedResults(true);') -lt $sharedVariableSource.IndexOf('nodeSubscription1.Init(node);')) '共享变量节点必须在 Init 前默认展开高级结果。'

$fixedContracts = @(
    @('Node\1-Acquisition\ImageShow\ParamFormImageShow.cs', 'nodeSubscription1', 'OutputImage'),
    @('Node\2-ImagePreprocessing\ImageSplit\ParamFormImageSplit.cs', 'nodeSubscription1', 'OutputImage'),
    @('Node\2-ImagePreprocessing\ImageCrop\NodeParamFormImageCrop.cs', 'nodeSubscription1', 'OutputImage'),
    @('Node\2-ImagePreprocessing\ImageRotate\NodeParamFormImageRotate.cs', 'nodeSubscription1', 'OutputImage'),
    @('Node\2-ImagePreprocessing\ImagePreprocess\NodeParamFormImagePreprocess.cs', 'nodeSubscriptionImage', 'OutputImage'),
    @('Node\3-Detection\FindLine\NodeParamFormFindLine.cs', 'nodeSubscription1', 'OutputImage'),
    @('Node\3-Detection\FindCircle\NodeParamFormFindCircle.cs', 'nodeSubscription1', 'OutputImage'),
    @('Node\3-Detection\QRScan\NodeParamFormQRScan.cs', 'nodeSubscription1', 'OutputImage'),
    @('Node\3-Detection\MatchTemplate\NodeParamFormMatchTemplate.cs', 'nodeSubscription1', 'OutputImage'),
    @('Node\3-Detection\ColorDiscern\NodeParamFormColorDiscern.cs', 'nodeSubscription1', 'OutputImage'),
    @('Node\3-Detection\ColorDiscern\ColorCreate.cs', 'nodeSubscription1', 'OutputImage'),
    @('Node\3-Detection\BinaryAnalysis\NodeParamFormBinaryAnalysis.cs', 'nodeSubscription1', 'OutputImage'),
    @('Node\3-Detection\BatteryEar\NodeParamFormBatteryEar.cs', 'nodeSubscription1', 'OutputImage'),
    @('Node\4-Measurement\CaliperLine\NodeParamFormCaliperLine.cs', 'nodeSubscription1', 'OutputImage'),
    @('Node\4-Measurement\CaliperCircle\NodeParamFormCaliperCircle.cs', 'nodeSubscription1', 'OutputImage'),
    @('Node\4-Measurement\CaliperEllipse\NodeParamFormCaliperEllipse.cs', 'nodeSubscription1', 'OutputImage'),
    @('Node\4-Measurement\FindPoint\NodeParamFormFindPoint.cs', 'nodeSubscriptionImage', 'OutputImage'),
    @('Node\4-Measurement\LineLineAngle\NodeParamFormLineLineAngle.cs', 'nodeSubscriptionImage', 'OutputImage'),
    @('Node\4-Measurement\PointPointDistance\NodeParamFormPointPointDistance.cs', 'nodeSubscriptionImage', 'OutputImage'),
    @('Node\4-Measurement\PointLineDistance\NodeParamFormPointLineDistance.cs', 'nodeSubscriptionImage', 'OutputImage'),
    @('Node\4-Measurement\PointRegionDistance\NodeParamFormPointRegionDistance.cs', 'nodeSubscriptionImage', 'OutputImage'),
    @('Node\7-ResultProcessing\ResultOverlayDraw\NodeParamFormResultOverlayDraw.cs', 'nodeSubscriptionImage', 'OutputImage'),
    @('Node\7-ResultProcessing\ResultOverlayDraw2\NodeParamFormResultOverlayDraw2.cs', 'nodeSubscriptionImage', 'OutputImage'),
    @('Node\7-ResultProcessing\ImageDraw\NodeParamFormImageDraw.cs', 'nodeSubscription1', 'OutputImage'),
    @('Node\7-ResultProcessing\ImageSave\ParamFormSaveImage.cs', 'nodeSubscriptionImg2Save', 'OutputImage'),
    @('Node\5-EquipmentCommunication\CameraIO\ParamFormCameraIO.cs', 'nodeSubscription1', 'AlgorithmResult'),
    @('Node\5-EquipmentCommunication\AIResultSend\ParamFormSignalSend.cs', 'nodeSubscription1', 'AlgorithmResult'),
    @('Node\7-ResultProcessing\ImageDraw\NodeParamFormImageDraw.cs', 'nodeSubscription2', 'AlgorithmResult'),
    @('Node\7-ResultProcessing\ImageDraw\NodeParamFormImageDraw.cs', 'nodeSubscription3', 'AlgorithmResult'),
    @('Node\7-ResultProcessing\DataShow\NodeParamFormDataShow.cs', 'nodeSubscription1', 'AlgorithmResult'),
    @('Node\7-ResultProcessing\ImageSave\ParamFormSaveImage.cs', 'nodeSubscriptionBarCode', 'string'),
    @('Node\5-EquipmentCommunication\TCPClient\ParamFormTCPClient.cs', 'nodeSubscription1', 'bool'),
    @('Node\5-EquipmentCommunication\TCPServer\ParamFormTCPServer.cs', 'nodeSubscription1', 'bool')
)

foreach ($contract in $fixedContracts) {
    $path = Join-Path $projectRoot $contract[0]
    $control = $contract[1]
    $typeName = $contract[2]
    $content = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    $declaration = "$control.SetExpectedValueType<$typeName>();"
    $initialization = "$control.Init(node);"
    Assert-ContainsText $content $declaration "$($contract[0]) 的 $control 必须声明 $typeName 输入。"
    Assert-True ($content.IndexOf($declaration) -lt $content.IndexOf($initialization)) "$($contract[0]) 的输入契约必须在 Init 前设置。"
}

$multiCategoryForms = @(
    'Node\1-Acquisition\ImageShow3D\ParamFormImageShow3D.cs',
    'Node\3-Detection\TDAI\ParamFormTDAI.cs',
    'Node\3-Detection\Unsupervised\ParamFormUnsupervisedDetection.cs',
    'Node\3-Detection\LargeModel\ParamFormLargeModelDetection.cs',
    'Node\7-ResultProcessing\ResultSummarize\ParamFormSummarize.cs',
    'Node\7-ResultProcessing\ImageSave\ParamFormSaveImage.cs'
)
foreach ($relativePath in $multiCategoryForms) {
    $content = Get-Content -LiteralPath (Join-Path $projectRoot $relativePath) -Raw -Encoding UTF8
    Assert-ContainsText $content '.SetInputContract(' "$relativePath 必须为多类型输入声明统一契约。"
}

$subscriptionFiles = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Node') -Recurse -Filter '*.cs' -File
$unconfiguredControls = New-Object System.Collections.Generic.List[string]
foreach ($file in $subscriptionFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    $matches = [regex]::Matches($content, '(nodeSubscription\w*|nodeSub\w*)\.Init\(node\);')
    foreach ($match in $matches) {
        $control = $match.Groups[1].Value
        $hasContract = $content.Contains("$control.SetExpectedValueType") -or
            $content.Contains("$control.SetInputContract")
        $isNodeOnlySelector = $content.Contains("$control.HideText2();")
        if (-not $hasContract -and -not $isNodeOnlySelector) {
            $relative = $file.FullName.Substring($projectRoot.Length + 1)
            $unconfiguredControls.Add("$relative::$control")
        }
    }
}
Assert-True ($unconfiguredControls.Count -eq 0) ("以下结果订阅控件没有明确输入契约：" + (($unconfiguredControls | Sort-Object -Unique) -join ', '))

$customSelectors = @(
    @('Node\6-LogicTool\ArithmeticOperation\NodeParamFormArithmeticOperation.cs', 'ArithmeticOperationAlgorithm.GetReadableMembers(ownerType)'),
    @('Node\6-LogicTool\MultiCondition\NodeParamFormMultiCondition.cs', 'MultiConditionReflection.GetReadableMembers(ownerType)'),
    @('Node\6-LogicTool\CompositeModule\NodeParamFormCompositeModule.cs', 'CompositePortValueHelper.GetReadableMembers(ownerType)'),
    @('Node\6-LogicTool\CompositeModule\NodeParamFormCompositeOutput.cs', 'CompositePortValueHelper.GetReadableMembers(ownerType)')
)
foreach ($selector in $customSelectors) {
    $content = Get-Content -LiteralPath (Join-Path $projectRoot $selector[0]) -Raw -Encoding UTF8
    Assert-ContainsText $content 'SubscriptionPortCatalog.GetOutputs' "$($selector[0]) 必须使用统一端口目录。"
    Assert-True (-not $content.Contains($selector[1])) "$($selector[0]) 不能继续递归反射结果成员生成新候选。"
}

$arithmeticSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Node\6-LogicTool\ArithmeticOperation\NodeParamFormArithmeticOperation.cs') -Raw -Encoding UTF8
$multiConditionSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Node\6-LogicTool\MultiCondition\NodeParamFormMultiCondition.cs') -Raw -Encoding UTF8
Assert-ContainsText $arithmeticSource 'SubscriptionDataCategory.Number' '四则运算新候选必须只接受数值类别。'
Assert-ContainsText $multiConditionSource 'SubscriptionDataCategory.Boolean' '多条件新候选必须接受布尔类别。'
Assert-ContainsText $multiConditionSource 'SubscriptionDataCategory.Number' '多条件新候选必须接受数值类别。'
Assert-ContainsText $multiConditionSource 'SubscriptionDataCategory.Text' '多条件新候选必须接受文本类别。'

Write-Host 'Subscription consumer contract checks passed.'
