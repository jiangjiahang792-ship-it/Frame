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

$paramPath = Join-Path $projectRoot 'Node\7-ResultProcessing\ResultOverlayDraw\NodeParamResultOverlayDraw.cs'
$builderPath = Join-Path $projectRoot 'Node\7-ResultProcessing\ResultOverlayDraw\NodeResultOverlayDraw.cs'
$formPath = Join-Path $projectRoot 'Node\7-ResultProcessing\ResultOverlayDraw\NodeParamFormResultOverlayDraw.cs'
$designerPath = Join-Path $projectRoot 'Node\7-ResultProcessing\ResultOverlayDraw\NodeParamFormResultOverlayDraw.Designer.cs'
$imageViewerPath = Join-Path $projectRoot 'Forms\ImageViewer\FrmSingleImage.cs'
$paramSource = Get-Content -LiteralPath $paramPath -Raw -Encoding UTF8
$builderSource = Get-Content -LiteralPath $builderPath -Raw -Encoding UTF8
$formSource = Get-Content -LiteralPath $formPath -Raw -Encoding UTF8
$designerSource = Get-Content -LiteralPath $designerPath -Raw -Encoding UTF8
$imageViewerSource = Get-Content -LiteralPath $imageViewerPath -Raw -Encoding UTF8

Assert-True (-not $paramSource.Contains('public string JudgeText1')) '实际ROI结果绘制参数不能继续保存独立颜色判定节点。'
Assert-True (-not $paramSource.Contains('public string JudgeText2')) '实际ROI结果绘制参数不能继续保存独立颜色判定结果。'
Assert-True (-not $paramSource.Contains('public bool HasNewJudgeSubscription')) '实际ROI结果绘制参数不能继续维护独立颜色判定状态。'
Assert-ContainsText $paramSource 'Roi' '实际ROI结果绘制必须增加自动ROI类型。'
Assert-ContainsText $paramSource 'public int ColorArgb' '旧方案每项固定颜色字段必须保留。'
Assert-ContainsText $paramSource 'public bool UseJudgeColor' '旧方案每项判定颜色字段必须保留。'
Assert-ContainsText $paramSource 'Line,' '旧方案线类型必须保留。'
Assert-ContainsText $paramSource 'Rectangle,' '旧方案矩形类型必须保留。'
Assert-ContainsText $paramSource 'Region' '旧方案区域类型必须保留。'

Assert-ContainsText $builderSource 'case ResultOverlayDrawItemType.Roi:' '实际ROI结果绘制构建器必须处理自动ROI。'
Assert-ContainsText $builderSource 'OverlayGeometryAdapterRegistry.TryAppend' '实际ROI结果绘制必须复用统一几何适配器。'
Assert-True (-not $builderSource.Contains('ResolveGlobalColorState')) '实际ROI结果绘制不能继续读取全局颜色判定订阅。'
Assert-ContainsText $builderSource 'ResolveAutomaticJudgeOk' '实际ROI结果绘制必须从每个来源结果自动解析判定状态。'
Assert-ContainsText $builderSource 'texts.AddRange(BuildTextValueLines' '实际ROI结果绘制文本项必须使用多目标值展开结果。'
Assert-ContainsText $builderSource 'ResolveSubscriptionCategory' '自动ROI必须使用统一订阅数据类别。'

Assert-ContainsText $designerSource 'this.buttonAddRoi.Text = "添加ROI";' '实际参数窗体必须显示添加ROI按钮。'
Assert-ContainsText $designerSource 'this.buttonAddText.Text = "添加文本";' '实际参数窗体必须显示添加文本按钮。'
Assert-ContainsText $designerSource 'this.buttonDeleteItem.Text = "删除";' '实际参数窗体必须显示删除按钮。'
Assert-ContainsText $designerSource 'this.Text = "ROI结果绘制";' '实际参数窗体标题必须保持ROI结果绘制。'
Assert-True (-not $designerSource.Contains('加线')) '实际参数窗体不能继续显示加线按钮。'
Assert-True (-not $designerSource.Contains('加矩形')) '实际参数窗体不能继续显示加矩形按钮。'
Assert-True (-not $designerSource.Contains('加区域')) '实际参数窗体不能继续显示加区域按钮。'
Assert-True (-not $designerSource.Contains('comboBoxItemType')) '实际参数窗体不能继续要求选择具体几何类型。'
Assert-True (-not $designerSource.Contains('textBoxName')) '实际参数窗体不能继续要求维护绘制项名称。'
Assert-True (-not $designerSource.Contains('nodeSubscriptionJudge')) '实际参数窗体不能继续显示独立颜色判定订阅控件。'
Assert-True (-not $designerSource.Contains('颜色判定')) '实际参数窗体不能继续显示颜色判定文字。'

Assert-ContainsText $formSource 'AddItem(ResultOverlayDrawItemType.Roi)' '添加ROI按钮必须创建实际节点的Roi项。'
Assert-True (-not $formSource.Contains('nodeSubscriptionJudge')) '实际参数窗体逻辑不能继续读写独立颜色判定订阅。'
Assert-ContainsText $formSource 'SubscriptionDataCategory.MeasurementResult' 'ROI订阅必须接受测量结果。'
Assert-ContainsText $formSource 'new EnumOption<DisplayTextPosition>("左上角"' '文本位置必须使用简体中文显示。'
$oldCollectorStart = $imageViewerSource.IndexOf('private static void AddResultOverlayDrawSources', [StringComparison]::Ordinal)
$newCollectorStart = $imageViewerSource.IndexOf('private static void AddResultOverlayDraw2Sources', [StringComparison]::Ordinal)
Assert-True ($oldCollectorStart -ge 0 -and $newCollectorStart -gt $oldCollectorStart) '找不到实际ROI结果绘制的手动调参依赖收集方法。'
$oldCollectorSource = $imageViewerSource.Substring($oldCollectorStart, $newCollectorStart - $oldCollectorStart)
Assert-True (-not $oldCollectorSource.Contains('param.JudgeText1')) '实际ROI结果绘制的手动调参范围不能继续收集独立颜色判定来源。'

$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$application = Get-Item -LiteralPath (Join-Path $debugDirectory '机器视觉AI检测系统V1.0.exe') -ErrorAction SilentlyContinue
Assert-True ($null -ne $application) '找不到Debug程序，无法执行实际ROI结果绘制序列化检查。'

[Environment]::CurrentDirectory = $debugDirectory
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$paramType = $assembly.GetType('TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw.NodeParamResultOverlayDraw', $true)
$itemType = $assembly.GetType('TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw.ResultOverlayDrawItem', $true)
$itemTypeEnum = $assembly.GetType('TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw.ResultOverlayDrawItemType', $true)
$newtonsoftAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $debugDirectory 'Newtonsoft.Json.dll'))
$jsonConvertType = $newtonsoftAssembly.GetType('Newtonsoft.Json.JsonConvert', $true)

$newParam = [Activator]::CreateInstance($paramType)
$newItem = [Activator]::CreateInstance($itemType)
$newItem.ItemType = [Enum]::Parse($itemTypeEnum, 'Roi')
$newParam.Items.Add($newItem)
$newJson = $jsonConvertType.GetMethod('SerializeObject', [Type[]]@([object])).Invoke($null, @($newParam))
$deserializeMethod = $jsonConvertType.GetMethods() |
    Where-Object { $_.Name -eq 'DeserializeObject' -and $_.GetParameters().Count -eq 2 -and $_.GetParameters()[1].ParameterType -eq [Type] } |
    Select-Object -First 1
$newRoundTrip = $deserializeMethod.Invoke($null, @($newJson, $paramType))
Assert-True ($newRoundTrip.Items[0].ItemType.ToString() -eq 'Roi') '新版自动ROI类型序列化往返后必须保留。'
Assert-True ($null -eq $paramType.GetProperty('JudgeText1')) '运行程序集中的实际参数类型不能继续暴露独立颜色判定节点。'
Assert-True ($null -eq $paramType.GetProperty('JudgeText2')) '运行程序集中的实际参数类型不能继续暴露独立颜色判定结果。'

$builderType = $assembly.GetType('TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw.ResultOverlayDrawBuilder', $true)
$bindingFlags = [Reflection.BindingFlags]::Static -bor [Reflection.BindingFlags]::NonPublic
$multiTargetTextBuilder = $builderType.GetMethod('BuildTextValueLines', $bindingFlags)
Assert-True ($null -ne $multiTargetTextBuilder) '找不到多目标文本值展开方法。'
$caliperLineResultType = $assembly.GetType('TDJS_Vision.Node._4_Measurement.CaliperLine.NodeResultCaliperLine', $true)
$caliperLineTargetType = $assembly.GetType('TDJS_Vision.Node._4_Measurement.CaliperLine.CaliperLineTargetResult', $true)
$caliperLineResult = [Activator]::CreateInstance($caliperLineResultType)
$targetListType = [System.Collections.Generic.List``1].MakeGenericType($caliperLineTargetType)
$targetList = [Activator]::CreateInstance($targetListType)
$target1 = [Activator]::CreateInstance($caliperLineTargetType)
$target1.TargetIndex = 1
$target1.IsOk = $true
$target1.Length = 128.737
$target2 = [Activator]::CreateInstance($caliperLineTargetType)
$target2.TargetIndex = 2
$target2.IsOk = $false
$target2.Length = 0
$target3 = [Activator]::CreateInstance($caliperLineTargetType)
$target3.TargetIndex = 3
$target3.IsOk = $true
$target3.Length = 126.512
$targetList.Add($target1)
$targetList.Add($target2)
$targetList.Add($target3)
$caliperLineResult.Items = $targetList
$multiTargetLines = $multiTargetTextBuilder.Invoke($null, @($caliperLineResult, '长度', [double]128.737))
Assert-True ($multiTargetLines.Count -eq 1) '多个模板目标的同一测量值必须合并成一个数组文本。'
Assert-True ($multiTargetLines[0] -eq '[128.737,0,126.512]') '多目标长度必须按模板顺序显示为方括号数组，失败目标保留数值0。'

$judgeResolver = $builderType.GetMethod('ResolveAutomaticJudgeOk', $bindingFlags)
Assert-True ($null -ne $judgeResolver) '找不到自动来源判定解析方法。'
$caliperResultType = $assembly.GetType('TDJS_Vision.Node._4_Measurement.CaliperCircle.NodeResultCaliperCircle', $true)
$caliperResult = [Activator]::CreateInstance($caliperResultType)
$caliperResult.JudgeOk = $false
$resolvedNg = $judgeResolver.Invoke($null, @($null, $caliperResult))
Assert-True (-not $resolvedNg) '来源节点JudgeOk=False时必须自动解析为NG。'
$caliperResult.JudgeOk = $true
$resolvedOk = $judgeResolver.Invoke($null, @($null, $caliperResult))
Assert-True $resolvedOk '来源节点JudgeOk=True时必须自动解析为OK。'
$resolvedDefault = $judgeResolver.Invoke($null, @($null, $null))
Assert-True $resolvedDefault '来源没有判定状态时必须默认OK。'
$resolvedNumber = $judgeResolver.Invoke($null, @([int]0, $null))
Assert-True $resolvedNumber '普通数值0没有判定语义，不能被误判为NG。'
$algorithmResultType = $assembly.GetType('TDJS_Vision.Node._3_Detection.TDAI.AlgorithmResult', $true)
$algorithmResult = [Activator]::CreateInstance($algorithmResultType)
$algorithmResult.IsAllOk = $false
$resolvedAlgorithmNg = $judgeResolver.Invoke($null, @($algorithmResult, $null))
Assert-True (-not $resolvedAlgorithmNg) 'AlgorithmResult.IsAllOk=False时必须自动解析为NG。'

$oldJson = '{"Items":[{"Enabled":true,"ItemType":"Line","Name":"旧线","ColorArgb":-23296,"UseJudgeColor":true,"OkColorArgb":-16711936,"NgColorArgb":-65536}]}'
$oldRoundTrip = $deserializeMethod.Invoke($null, @($oldJson, $paramType))
Assert-True ($oldRoundTrip.Items[0].ItemType.ToString() -eq 'Line') '旧方案线类型必须保留。'
Assert-True ($oldRoundTrip.Items[0].UseJudgeColor) '旧方案每项判定颜色模式必须保留。'
Assert-True ($oldRoundTrip.Items[0].ColorArgb -eq -23296) '旧方案固定颜色必须保留。'

Write-Host '实际ROI结果绘制两类型简化检查通过。'
