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

$paramPath = Join-Path $projectRoot 'Node\7-ResultProcessing\ResultOverlayDraw2\NodeParamResultOverlayDraw2.cs'
$builderPath = Join-Path $projectRoot 'Node\7-ResultProcessing\ResultOverlayDraw2\NodeResultOverlayDraw2.cs'
$formPath = Join-Path $projectRoot 'Node\7-ResultProcessing\ResultOverlayDraw2\NodeParamFormResultOverlayDraw2.cs'
$designerPath = Join-Path $projectRoot 'Node\7-ResultProcessing\ResultOverlayDraw2\NodeParamFormResultOverlayDraw2.Designer.cs'
$imageViewerPath = Join-Path $projectRoot 'Forms\ImageViewer\FrmSingleImage.cs'
$paramSource = Get-Content -LiteralPath $paramPath -Raw -Encoding UTF8
$builderSource = Get-Content -LiteralPath $builderPath -Raw -Encoding UTF8
$formSource = Get-Content -LiteralPath $formPath -Raw -Encoding UTF8
$designerSource = Get-Content -LiteralPath $designerPath -Raw -Encoding UTF8
$imageViewerSource = Get-Content -LiteralPath $imageViewerPath -Raw -Encoding UTF8

Assert-ContainsText $paramSource 'Roi' 'ROI结果绘制2必须增加自动ROI类型。'
Assert-ContainsText $paramSource 'public string JudgeText1' 'ROI结果绘制2必须保存单个颜色判定节点。'
Assert-ContainsText $paramSource 'public string JudgeText2' 'ROI结果绘制2必须保存单个颜色判定结果。'
Assert-ContainsText $paramSource 'public bool HasNewJudgeSubscription' 'ROI结果绘制2必须明确判断是否配置了新颜色判定。'
Assert-ContainsText $paramSource 'public List<ResultOverlayDraw2ColorRule> ColorRules' '旧版颜色规则字段必须继续保留。'
Assert-ContainsText $paramSource 'Line,' '旧版线类型必须继续保留。'
Assert-ContainsText $paramSource 'Rectangle,' '旧版矩形类型必须继续保留。'
Assert-ContainsText $paramSource 'Region' '旧版区域类型必须继续保留。'

Assert-ContainsText $builderSource 'case ResultOverlayDraw2ItemType.Roi:' '构建器必须处理自动ROI类型。'
Assert-ContainsText $builderSource 'param.HasNewJudgeSubscription' '构建器必须优先使用新版单个布尔判定。'
Assert-ContainsText $builderSource 'OverlayGeometryAdapterRegistry.TryAppend' '自动ROI必须通过统一几何适配器绘制。'
Assert-ContainsText $builderSource 'OverrideSourceColor' '构建器必须区分覆盖颜色和保留来源颜色。'
Assert-ContainsText $builderSource 'ResolveSubscriptionCategory' '自动ROI必须读取统一订阅类别。'
Assert-ContainsText $builderSource 'IsSuccessfulEmptyRoiResult' '成功但无几何的算法或测量结果不能误报“未找到”。'

Assert-ContainsText $designerSource 'this.buttonAddText.Text = "添加文本";' '界面必须提供添加文本按钮。'
Assert-ContainsText $designerSource 'this.buttonAddRoi.Text = "添加ROI";' '界面必须提供添加ROI按钮。'
Assert-ContainsText $designerSource 'this.buttonDeleteItem.Text = "删除";' '界面必须保留删除按钮。'
Assert-ContainsText $designerSource 'this.nodeSubscriptionJudge' '颜色判定必须改为单个订阅控件。'
Assert-ContainsText $designerSource 'this.labelLegacyRules' '旧方案颜色规则必须提供兼容提示。'
Assert-ContainsText $designerSource 'this.buttonToggleAdvanced.Text = "展开高级设置";' '低频文本参数必须默认折叠。'
Assert-True (-not $designerSource.Contains('加线')) '界面不能继续提供加线按钮。'
Assert-True (-not $designerSource.Contains('加矩形')) '界面不能继续提供加矩形按钮。'
Assert-True (-not $designerSource.Contains('加区域')) '界面不能继续提供加区域按钮。'
Assert-True (-not $designerSource.Contains('添加规则')) '界面不能继续提供颜色规则编辑。'
Assert-True (-not $designerSource.Contains('comboBoxItemType')) '界面不能继续要求用户选择具体几何类型。'
Assert-True (-not $designerSource.Contains('textBoxName')) '界面不能继续要求用户维护绘制项名称。'

Assert-ContainsText $formSource 'AddItem(ResultOverlayDraw2ItemType.Roi)' '添加ROI按钮必须创建自动ROI项。'
Assert-ContainsText $formSource 'SubscriptionDataCategory.MeasurementResult' 'ROI订阅必须接受多目标测量结果。'
Assert-ContainsText $formSource 'SubscriptionDataCategory.AlgorithmResult' 'ROI订阅必须接受完整算法结果。'
Assert-ContainsText $formSource 'nodeSubscriptionJudge.SetExpectedValueType<bool>()' '颜色判定订阅必须只接受布尔值。'
Assert-ContainsText $formSource 'labelLegacyRules.Visible' '参数窗体必须显示旧颜色规则兼容状态。'
Assert-ContainsText $formSource 'new EnumOption<DisplayTextPosition>("左上角"' '文本位置选项必须使用简体中文显示。'
Assert-True (-not $formSource.Contains('comboBoxTextPosition.DataSource = Enum.GetValues')) '参数界面不能直接显示英文枚举名。'
Assert-ContainsText $imageViewerSource 'AddSourceNodeTextToFilter(filter, param.JudgeText1);' '手动调参过滤必须包含新版单布尔判定来源。'
Assert-ContainsText $imageViewerSource 'if (!param.HasNewJudgeSubscription && param.ColorRules != null)' '手动调参过滤必须只在没有新判定时读取旧规则。'

$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$application = Get-Item -LiteralPath (Join-Path $debugDirectory '机器视觉AI检测系统V1.0.exe') -ErrorAction SilentlyContinue
Assert-True ($null -ne $application) '找不到Debug程序，无法执行参数序列化兼容检查。'

[Environment]::CurrentDirectory = $debugDirectory
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$paramType = $assembly.GetType('TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw2.NodeParamResultOverlayDraw2', $true)
$itemType = $assembly.GetType('TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw2.ResultOverlayDraw2Item', $true)
$itemTypeEnum = $assembly.GetType('TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw2.ResultOverlayDraw2ItemType', $true)
$newtonsoftAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $debugDirectory 'Newtonsoft.Json.dll'))
$jsonConvertType = $newtonsoftAssembly.GetType('Newtonsoft.Json.JsonConvert', $true)

$newParam = [Activator]::CreateInstance($paramType)
$newParam.JudgeText1 = '8.多条件判断'
$newParam.JudgeText2 = '判定结果'
$newItem = [Activator]::CreateInstance($itemType)
$newItem.ItemType = [Enum]::Parse($itemTypeEnum, 'Roi')
$newItem.Name = 'ROI1'
$newParam.Items.Add($newItem)
$newJson = $jsonConvertType.GetMethod('SerializeObject', [Type[]]@([object])).Invoke($null, @($newParam))
$deserializeMethod = $jsonConvertType.GetMethods() |
    Where-Object { $_.Name -eq 'DeserializeObject' -and $_.GetParameters().Count -eq 2 -and $_.GetParameters()[1].ParameterType -eq [Type] } |
    Select-Object -First 1
$newRoundTrip = $deserializeMethod.Invoke($null, @($newJson, $paramType))
Assert-True $newRoundTrip.HasNewJudgeSubscription '新版单布尔判定订阅序列化往返后必须保留。'
Assert-True ($newRoundTrip.Items.Count -eq 1 -and $newRoundTrip.Items[0].ItemType.ToString() -eq 'Roi') '新版自动ROI项序列化往返后必须保留。'

$oldJson = '{"ColorRules":[{"Enabled":true,"SourceText1":"2.多条件","SourceText2":"判定结果","ConditionName":"整体结果"}],"Items":[{"Enabled":true,"ItemType":"Line","Name":"旧线段"}]}'
$oldRoundTrip = $deserializeMethod.Invoke($null, @($oldJson, $paramType))
Assert-True (-not $oldRoundTrip.HasNewJudgeSubscription) '旧方案没有新版判定订阅时必须保持兼容分支。'
Assert-True ($oldRoundTrip.ColorRules.Count -eq 1) '旧方案颜色规则反序列化后必须保留。'
Assert-True ($oldRoundTrip.Items.Count -eq 1 -and $oldRoundTrip.Items[0].ItemType.ToString() -eq 'Line') '旧方案线段类型反序列化后必须保留。'

Write-Host 'ROI结果绘制2参数与构建流程简化检查通过。'
