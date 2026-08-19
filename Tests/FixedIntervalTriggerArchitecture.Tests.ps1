$ErrorActionPreference = 'Stop'

function Get-ProjectSource {
    param([string]$RelativePath)
    return Get-Content -LiteralPath (Join-Path $PSScriptRoot "..\$RelativePath") -Raw -Encoding UTF8
}

function Assert-Contains {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if (-not $Text.Contains($Pattern)) {
        throw $Message
    }
}

function Assert-NotContains {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if ($Text.Contains($Pattern)) {
        throw $Message
    }
}

$controller = Get-ProjectSource 'ResourceManagement\SolutionRunAdmissionController.cs'
$formMain = Get-ProjectSource 'FormMain.cs'
$stressTool = Get-ProjectSource 'Tests\FullSolutionComparison.Tests.ps1'
$project = Get-ProjectSource 'TDJS-Vision.csproj'

Assert-Contains $controller 'public bool TryAcquire(out SolutionRunAdmissionLease lease)' '固定触发必须通过可测试的单方案准入器。'
Assert-Contains $controller 'if (_isActive)' '已有完整方案运行时必须明确走忙碌分支。'
Assert-Contains $controller '_busyRejectedCount++;' '忙碌触发必须留下独立拒绝计数。'
Assert-Contains $controller 'while (_durationSamples.Count > _durationSampleCapacity)' '触发耗时样本必须固定有界。'
Assert-Contains $controller 'public bool TryReset()' '正式压力前必须能够在空闲时清除预热计数。'
Assert-Contains $formMain 'if (TryStartSingleSolutionRun(out Task runTask))' '正常单次运行按钮必须复用同一准入链路。'
Assert-Contains $formMain 'await Solution.Instance.Run(false);' '准入后仍必须执行原完整可编辑方案。'
Assert-Contains $formMain 'FixedTriggerStressMessage = 0x8451' 'Debug压力必须使用不抢焦点的窗口消息触发真实方案。'
Assert-Contains $formMain '#if DEBUG' '固定触发窗口消息入口必须只在Debug构建启用。'
Assert-Contains $formMain 'TDJS_VISION_FIXED_TRIGGER_STRESS' 'Debug压力入口还必须由显式环境变量开放。'
Assert-Contains $formMain '【固定触发汇总】' '全部请求结束后必须留下准入和耗时汇总。'
Assert-Contains $stressTool '[string]$RunMode = "SerialRounds"' '原串行同比口径必须保留为默认运行模式。'
Assert-Contains $stressTool '[int]$TriggerIntervalMilliseconds = 50' '固定触发测试默认必须每50ms发送一次。'
Assert-Contains $stressTool 'ScheduledMilliseconds' '每次触发必须记录绝对计划时刻。'
Assert-Contains $stressTool 'ScheduleDriftMilliseconds' '每次触发必须记录相对固定节拍的实际漂移。'
Assert-Contains $stressTool 'SendMessageTimeout(' '触发消息必须设置超时，UI无响应时不能无限卡住测试器。'
Assert-Contains $stressTool 'Sync-TestRuntimeConfiguration' '启动前必须同步并校验旧版许可证和界面布局。'
Assert-Contains $stressTool '固定节拍触发必须提供RuntimeReferenceDirectory' '固定触发不能绕过旧版运行依赖同步硬门槛。'
Assert-Contains $stressTool 'DockRightAutoHide' '预检必须确认运行日志保持旧版右侧自动折叠。'
Assert-Contains $stressTool '$initial.ProcessCount -ne $ExpectedProcessTreeCount' '正式触发前必须校验主程序和AI进程树数量。'
Assert-Contains $stressTool '$controllerCompleted -ne $controllerAccepted' '测试结束必须核对全部准入轮次已经退出。'
Assert-Contains $stressTool '$modbusSuccessCount -ne ($controllerAccepted * 4)' '通信结果必须按实际准入轮次核对。'
Assert-Contains $project '<Compile Include="ResourceManagement\SolutionRunAdmissionController.cs" />' '项目必须编译方案触发准入器。'
Assert-NotContains $controller 'SemaphoreSlim' '方案触发准入不得形成等待队列或阻塞触发线程。'
Assert-NotContains $controller 'AiConcurrency' '方案触发准入不得改变AI节点并发策略。'

Write-Host '固定50ms触发架构检查通过：单方案不重入、忙碌立即拒绝、Debug显式压力入口和原完整流程链均已接通。'
