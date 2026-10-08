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

# 将保证限定在实际委托方法内，避免无关代码中的同名调用掩盖接线断裂。
function Get-SourceBlock {
    param([string]$Text, [string]$StartMarker, [string]$EndMarker)
    $startIndex = $Text.IndexOf($StartMarker)
    if ($startIndex -lt 0) { throw "未找到代码块开始标记：$StartMarker" }
    $endIndex = $Text.IndexOf($EndMarker, $startIndex)
    if ($endIndex -lt 0) { throw "未找到代码块结束标记：$EndMarker" }
    return $Text.Substring($startIndex, $endIndex - $startIndex)
}

# 检查同步占位和异步收尾次序，不允许准备信号前留下重入窗口。
function Assert-InOrder {
    param([string]$Text, [string[]]$Patterns, [string]$Message)
    $position = 0
    foreach ($pattern in $Patterns) {
        $index = $Text.IndexOf($pattern, $position, [StringComparison]::Ordinal)
        if ($index -lt 0) { throw "$Message 缺失或顺序错误：$pattern" }
        $position = $index + $pattern.Length
    }
}

$controller = Get-ProjectSource 'ResourceManagement\SolutionRunAdmissionController.cs'
$formMain = Get-ProjectSource 'FormMain.cs'
$runtime = Get-ProjectSource 'FormMain.RuntimeInterface.cs'
$adapter = Get-ProjectSource 'Forms/RuntimeDesigner/RuntimeFrameworkRunController.cs'
$solution = Get-ProjectSource 'Solution.cs'
$stressTool = Get-ProjectSource 'Tests\FullSolutionComparison.Tests.ps1'
$project = Get-ProjectSource 'TDJS-Vision.csproj'
$singleEntry = Get-SourceBlock $formMain 'private bool TryStartSingleSolutionRun(out Task runTask)' 'private async Task ExecuteSingleSolutionRunAsync('
$hostBinding = Get-SourceBlock $runtime 'private RuntimeFrameworkRunController RuntimeRunController =>' 'private IReadOnlyList<RuntimeProcessChoice> GetRuntimeProcesses()'
$hostRun = Get-SourceBlock $runtime 'private async Task RunRuntimeFrameworkAsync(' 'private async Task StopRuntimeFrameworkAsync()'
$admission = Get-SourceBlock $adapter 'internal bool TryRunAsync(' 'private async Task ExecuteAsync('
$execution = Get-SourceBlock $adapter 'private async Task ExecuteAsync(' 'private Task StartHostRun('
$coreStart = Get-SourceBlock $adapter 'private Task StartHostRun(' 'public Task StopAsync()'
$solutionRun = Get-SourceBlock $solution 'public Task Run(bool isCyclical = false)' 'public List<ICamera> StartCameraCallbackGrabbingForProcesses('

Assert-Contains $controller 'public bool TryAcquire(out SolutionRunAdmissionLease lease)' '固定触发必须通过可测试的单方案准入器。'
Assert-Contains $controller 'if (_isActive)' '已有完整方案运行时必须明确走忙碌分支。'
Assert-Contains $controller '_busyRejectedCount++;' '忙碌触发必须留下独立拒绝计数。'
Assert-Contains $controller 'while (_durationSamples.Count > _durationSampleCapacity)' '触发耗时样本必须固定有界。'
Assert-Contains $controller 'public bool TryReset()' '正式压力前必须能够在空闲时清除预热计数。'
Assert-Contains $formMain 'if (TryStartSingleSolutionRun(out Task runTask))' '正常单次运行按钮必须复用同一准入链路。'
Assert-Contains $singleEntry 'RuntimeRunController.TryRunAsync(null, false, false, out Task admittedTask)' '单次准入必须把全流程、非循环、非自动模式传入共享控制器。'
Assert-Contains $hostBinding '_singleRunAdmissionController,' '共享控制器必须复用原单次准入器，不能另建互不相干的门禁。'
Assert-Contains $hostBinding 'GetRuntimeProcesses, RunRuntimeFrameworkAsync, StopRuntimeFrameworkAsync,' '共享控制器必须绑定生产运行链路。'
Assert-InOrder $admission @(
    'lock (sync)',
    'stopping || hostStopping() || (!pending && hostRunning())',
    '!admission.TryAcquire(out lease)',
    'task = Task.CompletedTask;',
    'return false;',
    'pending = true;',
    'task = ExecuteAsync(processId, continuous, automatic, lease);'
) '准入必须同步拒绝忙碌请求，执行前占位，不能异步排队。'
Assert-InOrder $execution @(
    'await run(processId, continuous, automatic, StartHostRun);',
    'finally',
    'lease.Complete(succeeded);',
    'pending = false;'
) '原准入租约必须覆盖完整宿主执行及信号收尾，并在 finally 释放。'
Assert-InOrder $coreStart @(
    'lock (sync)',
    'if (stopping || hostStopping()) throw',
    'if (hostRunning()) throw',
    'return start();'
) '核心启动前必须在同一锁内复查真实运行和停止状态。'
Assert-InOrder $hostRun @(
    'var solution = Solution.Instance;',
    'var roots = (solution.AllProcesses ?? new List<Process>()).ToArray();',
    'Process[] selected = roots;',
    'if (processId.HasValue)',
    'if (!automatic && !ValidateCameraConfigurationBeforeRun(selected)) return;',
    'await FormGlobalSignal.SendGlobalSignals(this, true);',
    'await start(() =>',
    'FormGlobalSignal.StartListenSignals();',
    'return solution.RunProcessesAsync(selected, continuous);',
    'finally',
    'FormGlobalSignal.StopListenSignals();',
    'if ((!continuous && !automatic) || (automatic && online) || (online && !started))',
    'await FormGlobalSignal.SendGlobalSignals(this, false);'
) '全流程单次必须沿校验、准备信号、原完整快照执行和结束信号链路运行。'
Assert-Contains $solutionRun 'return RunProcessesAsync(AllProcesses?.ToArray() ?? new Process[0], isCyclical);' '原方案入口和新宿主必须共用同一个流程快照核心。'
Assert-Contains $solutionRun 'if (!TryBeginRunSession())' '快照执行不能绕过方案会话门禁。'
Assert-Contains $solutionRun 'foreach (var group in processSnapshot.GroupBy(p => p.Group))' '快照执行必须保留原流程分组。'
Assert-Contains $solutionRun 'groupCopy.Select(p => p.RunLv).Distinct().OrderBy(lv => lv)' '快照执行必须保留原优先级顺序。'
Assert-Contains $solutionRun 'p.Enable &&' '快照执行必须保留原启用过滤。'
Assert-Contains $solutionRun '!p.IsPassiveTriggered' '快照执行不能绕过被动流程逻辑。'
Assert-Contains $solutionRun 'CancellationTokenSource.CreateLinkedTokenSource(Solution.Instance.CancellationToken)' '快照执行必须保留全局取消语义。'
Assert-Contains $solutionRun 'CompleteWorkpieceExecutionAsync(' '快照执行必须保留工件资源事务收尾。'
Assert-Contains $formMain 'm.Result = TryStartSingleSolutionRun(out _) ? new IntPtr(1) : IntPtr.Zero;' 'Debug触发和正常按钮必须进入同一单次准入方法。'
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
Assert-Contains $project '<Compile Include="Forms\RuntimeDesigner\RuntimeFrameworkRunController.cs" />' '项目必须编译共享生产运行适配器。'
Assert-NotContains $controller 'SemaphoreSlim' '方案触发准入不得形成等待队列或阻塞触发线程。'
Assert-NotContains $adapter 'SemaphoreSlim' '共享适配器也不得把忙碌触发改为等待队列。'
Assert-NotContains $controller 'AiConcurrency' '方案触发准入不得改变AI节点并发策略。'

Write-Host '固定50ms触发架构检查通过：单方案不重入、忙碌立即拒绝、Debug显式压力入口和原完整流程链均已接通。'
