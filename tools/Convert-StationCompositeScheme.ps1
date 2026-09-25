# 将四工位方案中的同步配方/存图调用原位封装为组合模块；只输出新文件。
param(
    [Parameter(Mandatory=$true)][string]$SourcePath,
    [Parameter(Mandatory=$true)][string]$DestinationPath
)
$ErrorActionPreference = 'Stop'
$sourceFull = (Resolve-Path -LiteralPath $SourcePath).Path
$destinationFull = [IO.Path]::GetFullPath($DestinationPath)
if ($sourceFull -eq $destinationFull -or (Test-Path -LiteralPath $destinationFull)) {
    throw '目标必须是尚不存在的新方案文件。'
}
$sourceHash = (Get-FileHash -LiteralPath $sourceFull -Algorithm SHA256).Hash
$scheme = Get-Content -LiteralPath $sourceFull -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
$original = Get-Content -LiteralPath $sourceFull -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
$moduleType = 'TDJS_Vision.Node._6_LogicTool.CompositeModule.NodeParamCompositeModule'
$script:changedIds = [Collections.Generic.HashSet[int]]::new()
$script:moduleCount = 0
$script:normalizedReferences = @{}

# 获取节点参数的多态包装内容。
function Get-NodeParameter($Node) {
    if ($Node.NodeParam.Count -ne 1) { throw "节点 $($Node.ID) 参数包装异常。" }
    return @($Node.NodeParam.Values)[0]
}

# 在原有触发节点位置替换调用，保留编号、连线锚点、启用状态和画布位置。
function Set-CompositeCall($Caller, $Target, [string]$ModuleName) {
    $calls = @($Caller.NodeInfos | Where-Object {
        $_.NodeType -eq 'ProcessTrigger' -and (Get-NodeParameter $_).ProcessName -eq $Target.ProcessName
    })
    if ($calls.Count -ne 1) { throw "流程 $($Caller.ProcessName) 的调用数不是 1。" }
    $node = $calls[0]
    if ((Get-NodeParameter $node).UseOkOrNg -or !$Target.IsPassiveTriggered -or !$Target.Enable -or
        $Caller.Group -ne $Target.Group -or $Caller.Level -ne $Target.Level) {
        throw "流程 $($Target.ProcessName) 不满足同步封装前提。"
    }
    $node.NodeType = 'CompositeModule'
    $node.NodeName = $ModuleName
    $node.NodeParam = @{ $moduleType = [ordered]@{
        ModuleName = $ModuleName
        SourceProcessId = $Target.ID
        SourceProcessName = $Target.ProcessName
        HasCanvasGraph = $Target.HasCanvasGraph
        NodeInfos = @($Target.NodeInfos)
        ConnectionInfos = @($Target.ConnectionInfos)
        InputPorts = @()
        UsesDirectInputs = $true
        UsesDirectOutputs = $true
        InputBindings = @()
        OutputPorts = @()
    } }
    [void]$script:changedIds.Add([int]$node.ID)
    $script:moduleCount++
}

# 工位主流程 -> 配方组合 -> 存图组合，沿用原来等待目标流程完成的调用位置。
$removedIds = [Collections.Generic.HashSet[int]]::new()
$stations = @($scheme.ProcessInfos |
    Where-Object { $_.ProcessName -match '^工位(\d+)主流程$' } |
    ForEach-Object { [int]$Matches[1] } |
    Sort-Object)
if ($stations.Count -eq 0) { throw '未找到工位主流程。' }
foreach ($station in $stations) {
    $main = @($scheme.ProcessInfos | Where-Object ProcessName -eq "工位${station}主流程")
    $recipe = @($scheme.ProcessInfos | Where-Object ProcessName -eq "工位${station}_1_5DS-QEHBE配方")
    $save = @($scheme.ProcessInfos | Where-Object ProcessName -eq "工位${station}存图流程")
    if ($main.Count -ne 1 -or $recipe.Count -ne 1 -or $save.Count -ne 1) { throw "工位 $station 流程不完整。" }
    Set-CompositeCall $recipe[0] $save[0] "组合模块_工位${station}存图"
    Set-CompositeCall $main[0] $recipe[0] "组合模块_工位${station}配方"
    [void]$removedIds.Add([int]$recipe[0].ID)
    [void]$removedIds.Add([int]$save[0].ID)
}
$scheme.ProcessInfos = @($scheme.ProcessInfos | Where-Object { !$removedIds.Contains([int]$_.ID) })
# ProcessCount/NodeCount 是编号高水位而不是集合长度，保持原值以避免后续新增编号冲突。
$scheme.SolName = $destinationFull

# 递归校验作用域内连线以及所有节点是否完整保留。
$script:flattened = @{}
function Test-Scope($Scope) {
    $ids = [Collections.Generic.HashSet[int]]::new()
    foreach ($node in $Scope.NodeInfos) {
        if (!$ids.Add([int]$node.ID) -or $script:flattened.ContainsKey([int]$node.ID)) { throw "重复节点编号 $($node.ID)" }
        $script:flattened[[int]$node.ID] = $node
    }
    # 共享变量写入仍保存旧显示名时，按同一作用域内的稳定编号修复名称，不改变订阅目标。
    foreach ($node in $Scope.NodeInfos) {
        if ($node.NodeType -ne 'SharedVariable') { continue }
        $parameter = Get-NodeParameter $node
        if ($parameter.IsRead -or $parameter.Text1 -notmatch '^(\d+)\.') { continue }
        $sourceId = [int]$Matches[1]
        $sourceNode = @($Scope.NodeInfos | Where-Object ID -eq $sourceId)
        if ($sourceNode.Count -eq 1) {
            $canonical = "$sourceId.$($sourceNode[0].NodeName)"
            if ($parameter.Text1 -cne $canonical) {
                $script:normalizedReferences[[int]$node.ID] = $canonical
                $parameter.Text1 = $canonical
            }
        }
    }
    foreach ($edge in $Scope.ConnectionInfos) {
        if (!$ids.Contains([int]$edge.FromNodeId) -or !$ids.Contains([int]$edge.ToNodeId)) { throw '存在跨作用域悬空连线。' }
    }
    foreach ($node in $Scope.NodeInfos) {
        if ($node.NodeType -eq 'CompositeModule') { Test-Scope (Get-NodeParameter $node) }
        elseif ($node.NodeType -eq 'ProcessTrigger') { throw '存在尚未迁移的流程触发节点。' }
    }
}
foreach ($flow in $scheme.ProcessInfos) { Test-Scope $flow }
$originalNodes = @($original.ProcessInfos | ForEach-Object { $_.NodeInfos })
$expectedTopLevelProcesses = $original.ProcessInfos.Count - ($stations.Count * 2)
$expectedModuleCount = $stations.Count * 2
if ($script:flattened.Count -ne $originalNodes.Count -or $scheme.ProcessInfos.Count -ne $expectedTopLevelProcesses -or $script:moduleCount -ne $expectedModuleCount) {
    throw '迁移前后节点数量或预期模块数量不一致。'
}
foreach ($node in $originalNodes) {
    if (!$script:changedIds.Contains([int]$node.ID)) {
        if ($script:normalizedReferences.ContainsKey([int]$node.ID)) {
            (Get-NodeParameter $node).Text1 = $script:normalizedReferences[[int]$node.ID]
        }
        if (($node | ConvertTo-Json -Depth 100 -Compress) -cne ($script:flattened[[int]$node.ID] | ConvertTo-Json -Depth 100 -Compress)) {
            throw "非调用节点 $($node.ID) 内容被意外修改。"
        }
    }
}
foreach ($flow in $original.ProcessInfos) {
    $newScope = @($scheme.ProcessInfos | Where-Object ID -eq $flow.ID)
    if (!$newScope.Count) {
        $newScope = @($script:flattened.Values | Where-Object NodeType -eq 'CompositeModule' |
            ForEach-Object { Get-NodeParameter $_ } | Where-Object SourceProcessId -eq $flow.ID)
    }
    if ($newScope.Count -ne 1 -or ($flow.ConnectionInfos | ConvertTo-Json -Depth 100 -Compress) -cne
        ($newScope[0].ConnectionInfos | ConvertTo-Json -Depth 100 -Compress)) { throw "流程 $($flow.ID) 的连线发生改变。" }
}
$json = $scheme | ConvertTo-Json -Depth 100
[IO.File]::WriteAllText($destinationFull, $json, [Text.UTF8Encoding]::new($false))
if ((Get-FileHash -LiteralPath $sourceFull -Algorithm SHA256).Hash -ne $sourceHash) { throw '原方案校验失败。' }
[pscustomobject]@{ SourceHash=$sourceHash; Output=$destinationFull; TopLevelProcesses=$scheme.ProcessInfos.Count;
    CompositeModules=$script:moduleCount; TotalNodes=$script:flattened.Count;
    NormalizedReferences=$script:normalizedReferences;
    UnchangedNodes=$originalNodes.Count-$script:changedIds.Count-$script:normalizedReferences.Count }
