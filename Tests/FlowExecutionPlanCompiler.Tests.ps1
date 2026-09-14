$ErrorActionPreference = 'Stop'

# 以UTF-8读取后交给.NET Framework执行，避免Windows PowerShell按ANSI解释中文。
if ($PSVersionTable.PSEdition -eq 'Core') {
    $env:TDJS_FLOW_COMPILER_TEST_PATH = $PSCommandPath
    & "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command 'try { Invoke-Expression (Get-Content -LiteralPath $env:TDJS_FLOW_COMPILER_TEST_PATH -Raw -Encoding UTF8) } catch { Write-Host $_; exit 1 }'
    exit $LASTEXITCODE
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

$scriptDirectory = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $env:TDJS_FLOW_COMPILER_TEST_PATH }
$projectRoot = Split-Path -Parent $scriptDirectory
$buildDirectory = if ($env:TDJS_TEST_BUILD_DIRECTORY) { $env:TDJS_TEST_BUILD_DIRECTORY } else { 'bin\x64\Debug' }
$debugDirectory = Join-Path $projectRoot $buildDirectory
$application = Get-Item -LiteralPath (Join-Path $debugDirectory '机器视觉AI检测系统V1.0.exe') -ErrorAction SilentlyContinue
$source = Get-Item -LiteralPath (Join-Path $projectRoot 'ResourceManagement\FlowExecutionPlanCompiler.cs')
Assert-True ($null -ne $application) '流程编译器测试需要最新Debug程序。'
Assert-True ($application.LastWriteTimeUtc -ge $source.LastWriteTimeUtc) 'Debug程序早于流程编译器源码，请先重新编译。'

[Environment]::CurrentDirectory = $debugDirectory
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$processConfigType = $assembly.GetType('TDJS_Vision.ProcessConfig', $true)
$nodeConfigType = $assembly.GetType('TDJS_Vision.NodeConfig', $true)
$connectionConfigType = $assembly.GetType('TDJS_Vision.ProcessConnectionConfig', $true)
$nodeType = $assembly.GetType('TDJS_Vision.Node.NodeType', $true)
$branchType = $assembly.GetType('TDJS_Vision.ProcessConnectionBranch', $true)
$compositeParamType = $assembly.GetType('TDJS_Vision.Node._6_LogicTool.CompositeModule.NodeParamCompositeModule', $true)
$compilerType = $assembly.GetType('TDJS_Vision.ResourceManagement.FlowExecutionPlanCompiler', $true)

function New-NodeConfig {
    param(
        [int]$Id,
        [string]$Name,
        [string]$TypeName,
        [bool]$IsStartNode = $false,
        [bool]$Active = $true,
        [object]$Parameter = $null)

    $node = [Activator]::CreateInstance($nodeConfigType)
    $node.ID = $Id
    $node.NodeName = $Name
    $node.NodeType = [Enum]::Parse($nodeType, $TypeName)
    $node.Active = $Active
    $node.IsStartNode = $IsStartNode
    $node.NodeParam = $Parameter
    return $node
}

function New-ConnectionConfig {
    param(
        [string]$Id,
        [int]$From,
        [int]$To,
        [string]$Branch = 'Default')

    $connection = [Activator]::CreateInstance($connectionConfigType)
    $connection.ID = $Id
    $connection.FromNodeId = $From
    $connection.ToNodeId = $To
    $connection.Branch = [Enum]::Parse($branchType, $Branch)
    return $connection
}

function New-ProcessConfig {
    param([string]$Name)

    $config = [Activator]::CreateInstance($processConfigType)
    $config.ID = 1
    $config.ProcessName = $Name
    $config.HasCanvasGraph = $true
    return $config
}

$nested = [Activator]::CreateInstance($compositeParamType)
$nested.ModuleName = '内部检测模块'
$nested.HasCanvasGraph = $true
$nested.NodeInfos.Add((New-NodeConfig -Id 1 -Name '相机IO' -TypeName 'CameraIO' -IsStartNode $true))
$nested.NodeInfos.Add((New-NodeConfig -Id 2 -Name 'Modbus写入' -TypeName 'ModbusWrite'))
$nested.ConnectionInfos.Add((New-ConnectionConfig -Id 'n1' -From 1 -To 2))

$config = New-ProcessConfig -Name '主流程'
$config.NodeInfos.Add((New-NodeConfig -Id 1 -Name '开始' -TypeName 'UNKNOWN' -IsStartNode $true))
$config.NodeInfos.Add((New-NodeConfig -Id 2 -Name '串口发送' -TypeName 'ComSend'))
$config.NodeInfos.Add((New-NodeConfig -Id 3 -Name '组合模块' -TypeName 'CompositeModule' -Parameter $nested))
$config.NodeInfos.Add((New-NodeConfig -Id 4 -Name 'PLC写入' -TypeName 'PLCWrite'))
$config.NodeInfos.Add((New-NodeConfig -Id 5 -Name 'Modbus读取' -TypeName 'ModbusRead'))
$config.NodeInfos.Add((New-NodeConfig -Id 6 -Name '相机IO手动' -TypeName 'CameraIOManual'))
for ($id = 1; $id -lt 6; $id++) {
    $config.ConnectionInfos.Add((New-ConnectionConfig -Id "c$id" -From $id -To ($id + 1)))
}

$compiler = [Activator]::CreateInstance($compilerType)
$plan = $compiler.Compile($config, [long]11, ' 产线A ')
Assert-True $plan.IsProductionReady '串行外发和组合模块内部串行外发应通过静态生产检查。'
Assert-True ($plan.FlowRevision -eq 11 -and $plan.OrderDomain -eq '产线A') '编译计划必须冻结流程版本并规范生产顺序域。'
Assert-True ($plan.Nodes.Count -eq 8) '编译计划必须包含顶层6个节点和组合模块内部2个节点。'
Assert-True ($plan.Connections.Count -eq 6) '编译计划必须包含顶层5条和组合模块内部1条连线。'
Assert-True ($plan.PotentialSignals.Count -eq 0) '全部外发节点直接执行，不应登记有序信号。'
$signalKinds = @($plan.PotentialSignals | ForEach-Object { $_.SignalKind.ToString() } | Sort-Object)
Assert-True ($signalKinds.Count -eq 0) '组合模块内部也不得登记有序信号。'
Assert-True (($plan.Nodes | Where-Object { $_.NodePath -like '*组合模块*相机IO*' }).Count -eq 1) '组合模块内部节点仍必须带完整嵌套路径。'

$compiledName = $plan.Nodes[1].NodeName
$config.NodeInfos[1].NodeName = '后续改名'
$config.ConnectionInfos[0].ToNodeId = 99
Assert-True ($plan.Nodes[1].NodeName -eq $compiledName) '编译计划不能随源节点后续改名而变化。'
Assert-True ($plan.Connections[0].ToNodeId -eq 2) '编译计划不能随源连线后续修改而变化。'

$collectionIsReadOnly = $false
try {
    ([System.Collections.IList]$plan.Nodes).Add($plan.Nodes[0])
}
catch [System.NotSupportedException] {
    $collectionIsReadOnly = $true
}
Assert-True $collectionIsReadOnly '编译计划公开的节点集合必须拒绝外部修改。'

$parallel = New-ProcessConfig -Name '并行外发错误流程'
$parallel.NodeInfos.Add((New-NodeConfig -Id 1 -Name '开始' -TypeName 'UNKNOWN' -IsStartNode $true))
$parallel.NodeInfos.Add((New-NodeConfig -Id 2 -Name '串口发送' -TypeName 'ComSend'))
$parallel.NodeInfos.Add((New-NodeConfig -Id 3 -Name 'PLC写入' -TypeName 'PLCWrite'))
$parallel.ConnectionInfos.Add((New-ConnectionConfig -Id 'p1' -From 1 -To 2))
$parallel.ConnectionInfos.Add((New-ConnectionConfig -Id 'p2' -From 1 -To 3))
$parallelPlan = $compiler.Compile($parallel, [long]12, '产线A')
$parallelErrors = @($parallelPlan.Diagnostics | Where-Object { $_.Code.ToString() -eq 'UnorderedParallelSignals' })
Assert-True $parallelPlan.IsProductionReady '直接发送允许多个外发节点并行执行。'
Assert-True ($parallelErrors.Count -eq 0) '直接发送节点不能被旧有序规则拦截。'

$exclusive = New-ProcessConfig -Name '互斥分支流程'
$exclusive.NodeInfos.Add((New-NodeConfig -Id 1 -Name '条件' -TypeName 'If' -IsStartNode $true))
$exclusive.NodeInfos.Add((New-NodeConfig -Id 2 -Name 'OK串口发送' -TypeName 'ComSend'))
$exclusive.NodeInfos.Add((New-NodeConfig -Id 3 -Name 'NG写PLC' -TypeName 'PLCWrite'))
$exclusive.ConnectionInfos.Add((New-ConnectionConfig -Id 'e1' -From 1 -To 2 -Branch 'True'))
$exclusive.ConnectionInfos.Add((New-ConnectionConfig -Id 'e2' -From 1 -To 3 -Branch 'False'))
$exclusivePlan = $compiler.Compile($exclusive, [long]13, '产线A')
$exclusiveErrors = @($exclusivePlan.Diagnostics | Where-Object { $_.Code.ToString() -eq 'UnorderedParallelSignals' })
Assert-True ($exclusiveErrors.Count -eq 0) '同一条件的True和False互斥外发分支不能被误判为并行乱序。'
Assert-True $exclusivePlan.IsProductionReady '只有互斥外发分支的流程应通过静态生产检查。'

$disabledCondition = New-ProcessConfig -Name '禁用条件放行流程'
$disabledCondition.NodeInfos.Add((New-NodeConfig -Id 1 -Name '禁用条件' -TypeName 'If' -IsStartNode $true -Active $false))
$disabledCondition.NodeInfos.Add((New-NodeConfig -Id 2 -Name '串口发送' -TypeName 'ComSend'))
$disabledCondition.NodeInfos.Add((New-NodeConfig -Id 3 -Name 'PLC写入' -TypeName 'PLCWrite'))
$disabledCondition.ConnectionInfos.Add((New-ConnectionConfig -Id 'd1' -From 1 -To 2 -Branch 'True'))
$disabledCondition.ConnectionInfos.Add((New-ConnectionConfig -Id 'd2' -From 1 -To 3 -Branch 'False'))
$disabledConditionPlan = $compiler.Compile($disabledCondition, [long]14, '产线A')
Assert-True $disabledConditionPlan.IsProductionReady '禁用条件放行多个直接发送分支时也应允许启动。'
Assert-True (@($disabledConditionPlan.Diagnostics | Where-Object { $_.Code.ToString() -eq 'UnorderedParallelSignals' }).Count -eq 0) '禁用条件分支不再产生外发排序诊断。'

$multipleComponents = New-ProcessConfig -Name '多组件互斥流程'
$multipleComponents.NodeInfos.Add((New-NodeConfig -Id 1 -Name '显式开始' -TypeName 'UNKNOWN' -IsStartNode $true))
$multipleComponents.NodeInfos.Add((New-NodeConfig -Id 2 -Name '普通处理' -TypeName 'ImageRotate'))
$multipleComponents.NodeInfos.Add((New-NodeConfig -Id 10 -Name '独立条件' -TypeName 'If'))
$multipleComponents.NodeInfos.Add((New-NodeConfig -Id 11 -Name '串口发送' -TypeName 'ComSend'))
$multipleComponents.NodeInfos.Add((New-NodeConfig -Id 12 -Name 'PLC写入' -TypeName 'PLCWrite'))
$multipleComponents.ConnectionInfos.Add((New-ConnectionConfig -Id 'm1' -From 1 -To 2))
$multipleComponents.ConnectionInfos.Add((New-ConnectionConfig -Id 'm2' -From 10 -To 11 -Branch 'True'))
$multipleComponents.ConnectionInfos.Add((New-ConnectionConfig -Id 'm3' -From 10 -To 12 -Branch 'False'))
$multipleComponentsPlan = $compiler.Compile($multipleComponents, [long]15, '产线A')
Assert-True $multipleComponentsPlan.IsProductionReady '显式起点之外的独立组件也必须从无入线节点分析互斥分支。'

$canvasLegacy = New-ProcessConfig -Name '画布空连线顺序流程'
$canvasLegacy.HasCanvasGraph = $true
$canvasLegacy.NodeInfos.Add((New-NodeConfig -Id 1 -Name '串口发送' -TypeName 'ComSend'))
$canvasLegacy.NodeInfos.Add((New-NodeConfig -Id 2 -Name 'PLC写入' -TypeName 'PLCWrite'))
$canvasLegacyPlan = $compiler.Compile($canvasLegacy, [long]16, '产线A')
Assert-True $canvasLegacyPlan.IsProductionReady '无连线且无显式起点时，画布流程也应与运行器一致按列表顺序执行。'
Assert-True ($canvasLegacyPlan.Connections.Count -eq 1) '画布空连线顺序流程必须生成一条兼容连线。'

$emptyCompositeParam = [Activator]::CreateInstance($compositeParamType)
$emptyComposite = New-ProcessConfig -Name '空组合模块流程'
$emptyComposite.NodeInfos.Add((New-NodeConfig -Id 1 -Name '空组合模块' -TypeName 'CompositeModule' -IsStartNode $true -Parameter $emptyCompositeParam))
$emptyCompositePlan = $compiler.Compile($emptyComposite, [long]17, '产线A')
Assert-True (-not $emptyCompositePlan.IsProductionReady) '启用但没有内部节点的组合模块必须阻止生产启动。'
Assert-True (@($emptyCompositePlan.Diagnostics | Where-Object { $_.Code.ToString() -eq 'CompositeSnapshotMissing' }).Count -eq 1) '空组合模块必须产生缺失快照诊断。'

$broken = New-ProcessConfig -Name '断线流程'
$broken.NodeInfos.Add((New-NodeConfig -Id 1 -Name '开始' -TypeName 'UNKNOWN' -IsStartNode $true))
$broken.ConnectionInfos.Add((New-ConnectionConfig -Id 'b1' -From 1 -To 999))
$brokenPlan = $compiler.Compile($broken, [long]18, '产线A')
Assert-True (-not $brokenPlan.IsProductionReady) '引用不存在节点的连线必须阻止生产启动。'
Assert-True (@($brokenPlan.Diagnostics | Where-Object { $_.Code.ToString() -eq 'MissingConnectionNode' }).Count -eq 1) '断线流程必须给出稳定的缺失节点诊断。'

$legacy = New-ProcessConfig -Name '旧顺序流程'
$legacy.HasCanvasGraph = $false
$legacy.NodeInfos.Add((New-NodeConfig -Id 1 -Name '串口发送' -TypeName 'ComSend'))
$legacy.NodeInfos.Add((New-NodeConfig -Id 2 -Name 'PLC写入' -TypeName 'PLCWrite'))
$legacyPlan = $compiler.Compile($legacy, [long]19, '产线A')
Assert-True $legacyPlan.IsProductionReady '旧顺序流程应按节点列表顺序生成明确的外发先后关系。'
Assert-True ($legacyPlan.Connections.Count -eq 1) '旧顺序流程的不可变计划必须保存合成的顺序连线。'
Assert-True ($legacyPlan.Connections[0].ConnectionId -eq 'legacy-1-2') '旧顺序流程的合成连线必须使用稳定ID。'

Write-Host '流程编译器检查通过：不可变拓扑、组合模块递归、禁用分支、多组件起点、空组合、外发直接执行和顺序回退均符合预期。'
