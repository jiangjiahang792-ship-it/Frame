param(
    [string]$SchemePath = 'C:\Users\34652\Desktop\9_21号方案_组合模块版.Sol'
)

$ErrorActionPreference = 'Stop'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
    $script:Checks++
}

function Get-NodeParameter {
    param($Node)
    Assert-True ($Node.NodeParam.Count -eq 1) "节点 $($Node.ID) 参数包装异常。"
    return @($Node.NodeParam.Values)[0]
}

function Assert-Connection {
    param($Scope, [int]$From, [int]$To, [string]$Branch = $null)
    $matches = @($Scope.ConnectionInfos | Where-Object {
        [int]$_.FromNodeId -eq $From -and [int]$_.ToNodeId -eq $To -and
            ([string]::IsNullOrEmpty($Branch) -or [string]$_.Branch -eq $Branch)
    })
    Assert-True ($matches.Count -eq 1) "缺少或重复连线：$From -> $To $Branch"
}

function Assert-NoConnection {
    param($Scope, [int]$From, [int]$To)
    $matches = @($Scope.ConnectionInfos | Where-Object {
        [int]$_.FromNodeId -eq $From -and [int]$_.ToNodeId -eq $To
    })
    Assert-True ($matches.Count -eq 0) "存在不应保留的直连：$From -> $To"
}

function Get-AllNodes {
    param($Scope)
    foreach ($node in @($Scope.NodeInfos)) {
        $node
        if ($node.NodeType -eq 'CompositeModule') {
            Get-AllNodes (Get-NodeParameter $node)
        }
    }
}

$Checks = 0
$scheme = Get-Content -LiteralPath $SchemePath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
$station2 = @($scheme.ProcessInfos | Where-Object ProcessName -eq '工位2主流程')
Assert-True ($station2.Count -eq 1) '未找到唯一的工位2主流程。'

$recipeCall = @($station2[0].NodeInfos | Where-Object { [int]$_.ID -eq 79 -and $_.NodeType -eq 'CompositeModule' })
Assert-True ($recipeCall.Count -eq 1) '工位2主流程缺少 79.组合模块_工位2配方。'
$recipe = Get-NodeParameter $recipeCall[0]

$aiNodes = @($recipe.NodeInfos | Where-Object NodeType -eq 'AITD')
Assert-True ($aiNodes.Count -eq 1) '工位2配方组合模块必须有且只有一个 AI 检测节点。'
$ai = $aiNodes[0]
$aiParam = Get-NodeParameter $ai
Assert-True ($ai.NodeName -eq '目标分类_工位2') '工位2 AI 节点名称不正确。'
Assert-True ($aiParam.Text1 -eq '89.共享变量' -and $aiParam.Text2 -eq '变量.共享变量值') '工位2 AI 节点没有订阅组合模块输入图像。'
Assert-True ($aiParam.ModelName -eq 'AST_工位2模型') '工位2 AI 节点模型不是 AST_工位2模型。'
Assert-True ($aiParam.CurDetectItemName -eq 'AST-工位2检测项' -and $aiParam.DetectItemName1 -eq 'AST-工位2检测项') '工位2 AI 检测项名称不正确。'
Assert-True ([string]$aiParam.ConfigPath -like '*Ai\AST-工位2模型.ai') '工位2 AI 配置路径不正确。'

$conditionNodes = @($recipe.NodeInfos | Where-Object {
    $_.NodeType -eq 'MultiCondition' -and [int]$_.ID -ne 78
})
Assert-True ($conditionNodes.Count -ge 1) '工位2组合模块缺少 AI 结果判断节点。'
$condition = @($conditionNodes | Where-Object { $_.NodeName -eq '多条件判断_工位2AI结果' })[0]
Assert-True ($null -ne $condition) '工位2 AI 结果判断节点名称不正确。'
$conditionParam = Get-NodeParameter $condition
Assert-True ($conditionParam.Conditions.Count -eq 1) '工位2 AI 结果判断条件数量不正确。'
$conditionItem = $conditionParam.Conditions[0]
Assert-True ([int]$conditionItem.SourceNodeId -eq [int]$ai.ID -and $conditionItem.PropertyPath -eq 'JudgeOk') '工位2 AI 结果判断没有绑定判定OK。'

$overlayNodes = @($recipe.NodeInfos | Where-Object NodeType -eq 'ResultOverlayDraw')
Assert-True ($overlayNodes.Count -eq 1) '工位2组合模块必须有且只有一个 ROI 结果绘制节点。'
$overlay = $overlayNodes[0]
$overlayParam = Get-NodeParameter $overlay
Assert-True ($overlayParam.ImageText1 -eq '89.共享变量') '工位2 ROI 结果绘制没有使用组合模块输入图像。'
Assert-True (@($overlayParam.Items).Count -eq 2) '工位2 ROI 结果绘制必须包含文本和ROI两个绘制项。'
Assert-True (@($overlayParam.Items | Where-Object SourceText1 -eq "$($ai.ID).目标分类_工位2").Count -eq 2) '工位2 ROI 绘制项没有订阅 AI 输出。'

$showNode = @($recipe.NodeInfos | Where-Object { [int]$_.ID -eq 90 -and $_.NodeType -eq 'ImageShow' })[0]
$showParam = Get-NodeParameter $showNode
Assert-True ($showParam.Text1 -eq "$($overlay.ID).$($overlay.NodeName)" -and $showParam.Text2 -eq '输出图像') '工位2显示节点没有显示 AI 绘制结果。'

Assert-Connection $recipe 89 ([int]$ai.ID)
Assert-Connection $recipe ([int]$ai.ID) ([int]$condition.ID)
Assert-Connection $recipe ([int]$condition.ID) 91 'True'
Assert-Connection $recipe ([int]$condition.ID) 92 'False'
Assert-Connection $recipe ([int]$condition.ID) 93 'Default'
Assert-Connection $recipe ([int]$condition.ID) ([int]$overlay.ID) 'Default'
Assert-Connection $recipe ([int]$overlay.ID) 90
Assert-Connection $recipe 89 94
Assert-NoConnection $recipe 89 90
Assert-NoConnection $recipe 89 91
Assert-NoConnection $recipe 89 92
Assert-NoConnection $recipe 89 93

Assert-True ($scheme.DetectItemDic.ContainsKey('AST-工位2检测项')) '方案缺少 AST-工位2检测项。'
Assert-True (@($scheme.DetectItemDic['AST-工位2检测项']).Count -eq 5) 'AST-工位2检测项数量不正确。'

$allIds = @($scheme.ProcessInfos | ForEach-Object { Get-AllNodes $_ } | ForEach-Object { [int]$_.ID })
Assert-True (($allIds | Group-Object | Where-Object Count -gt 1).Count -eq 0) '方案存在重复节点ID。'
Assert-True ([int]$scheme.NodeCount -ge ($allIds | Measure-Object -Maximum).Maximum) '方案 NodeCount 小于实际最大节点ID。'

Write-Host "PASS: 工位2组合模块AI节点结构验证 $Checks 项断言"
