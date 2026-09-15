$ErrorActionPreference = 'Stop'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$application = Get-Item -LiteralPath (Join-Path $debugDirectory '机器视觉AI检测系统V1.0.exe') -ErrorAction SilentlyContinue
$processPath = Join-Path $projectRoot 'Process.cs'
$solutionPath = Join-Path $projectRoot 'Solution.cs'
$triggerNodePath = Join-Path $projectRoot 'Node\6-LogicTool\ProcessTrigger\NodeProcessTrigger.cs'
$sourceFiles = @($processPath, $solutionPath, $triggerNodePath) | ForEach-Object { Get-Item -LiteralPath $_ }

Assert-True ($null -ne $application) '流程工件接入测试需要最新Debug程序。'
Assert-True (($sourceFiles | Measure-Object -Property LastWriteTimeUtc -Maximum).Maximum -le $application.LastWriteTimeUtc) 'Debug程序早于流程工件接入源码，请先重新编译。'

$processSource = Get-Content -LiteralPath $processPath -Raw -Encoding UTF8
$solutionSource = Get-Content -LiteralPath $solutionPath -Raw -Encoding UTF8
$triggerNodeSource = Get-Content -LiteralPath $triggerNodePath -Raw -Encoding UTF8

Assert-True (-not $processSource.Contains('_triggerAction')) '被动流程触发不得依赖可被其他工件覆盖的流程共享委托。'
Assert-True (-not $processSource.Contains('SetTriggerAction')) '方案运行器不得向流程注入跨工件共享触发闭包。'
Assert-True ([regex]::Matches($processSource, 'EnterManagedProcessRunAsync\(').Count -eq 5) '四个流程运行入口必须全部在节点执行前进入FIFO工件门。'
Assert-True ([regex]::Matches($processSource, 'ExitManagedProcessRunAsync\(').Count -eq 5) '四个流程运行入口必须全部在finally中封口并释放工件门。'
Assert-True ([regex]::Matches($processSource, 'bool invocationSucceeded = false;').Count -eq 4) '四个流程入口必须使用本次调用局部终态，不能读取共享Success封口。'
Assert-True ([regex]::Matches($processSource, 'ExitManagedProcessRunAsync\(managedRunScope, invocationSucceeded\)').Count -eq 4) '四个流程入口必须用局部终态退出工件作用域。'
Assert-True ($processSource.Contains('process.ExitManagedProcessRunAsync(managedRunScope, invocationSucceeded)')) '静态流程入口必须用局部终态退出工件作用域。'
Assert-True ($processSource.Contains('ThrowIfInvocationFailed(result, cancellationToken);')) '流程触发必须检查目标流程本次独立结果。'
Assert-True ($processSource.Contains('PushProcessRunPath()')) '流程必须在等待FIFO前执行触发环检测。'

$awaitedTriggerCount = [regex]::Matches($triggerNodeSource, 'await\s+_process\.TriggerProcess\(').Count
$allTriggerCount = [regex]::Matches($triggerNodeSource, '_process\.TriggerProcess\(').Count
Assert-True ($awaitedTriggerCount -eq 3) '流程触发节点的OK、NG和直接触发分支必须全部等待被动流程。'
Assert-True ($allTriggerCount -eq $awaitedTriggerCount) '流程触发节点不得残留未等待调用。'

Assert-True ($solutionSource.Contains('using (WorkpieceContextAccessor.Push(workpieceContext))')) '方案流程组每轮必须发布共享工件上下文。'
Assert-True ($solutionSource.Contains('List<ProcessInvocationResult> activeResults')) '方案终态必须汇总不可变的本次流程结果。'
Assert-True ($solutionSource.Contains('WorkpieceIdentityGenerator.ResetSession();')) '新生产会话必须在首条运行会话登记时重建身份纪元。'
Assert-True ($solutionSource.Contains('if (isFirstSession)')) '身份纪元重建必须受首条活动会话条件保护。'
Assert-True ($solutionSource.Contains('context.Identity.SessionEpoch == WorkpieceIdentityGenerator.SessionEpoch')) '继承上下文前必须校验当前生产会话纪元。'
Assert-True ($solutionSource.Contains('OrderedSignalCoordinator.RegisterWorkpiece(context);')) '工件上下文创建后必须立即登记到当前会话有序协调器。'
Assert-True ($solutionSource.Contains('RenewOrderedSignalCoordinator();')) '新生产会话和方案重置必须淘汰旧有序协调器。'
Assert-True ($solutionSource.Contains('RenewCameraProductionPipeline();')) '新生产会话和方案重置必须淘汰旧相机票据注册表。'
Assert-True ($solutionSource.Contains('coordinator.OrderGapDetected += HandleOrderedSignalGapDetected;')) '当前有序协调器必须绑定顺序缺口停机处理器。'
Assert-True ($solutionSource.Contains('if (!ReferenceEquals(sender, _orderedSignalCoordinator))')) '迟到的旧协调器缺口报警不得误停新生产会话。'
Assert-True ($solutionSource.Contains('(_runFaultStopRequested || _cancellationTokenSource.IsCancellationRequested)')) '故障或正常停止后必须拒绝活动会话退出前的新运行入口。'
Assert-True ($solutionSource.IndexOf('TryCommitRunStop(out CancellationTokenSource tokenSource);', [StringComparison]::Ordinal) -lt $solutionSource.IndexOf('CancelTokenSourceNoThrow(tokenSource, "方案停止");', [StringComparison]::Ordinal)) '正常停止必须先提交门闩，再执行可能较慢的令牌取消。'
Assert-True ($solutionSource.Contains('CancelTokenSourceNoThrow(tokenSource, "有序信号顺序缺口");')) '当前会话顺序缺口必须真实取消方案运行令牌。'
Assert-True ($solutionSource.Contains('fault != null && !fault.RequiresSolutionStop')) '可恢复相机告警必须在提交全局停机门闩前直接返回。'
Assert-True (-not $solutionSource.Contains('workpieceContext.TryComplete(terminalState);')) '方案流程组根工件不得绕过协调器直接封口。'
Assert-True ($solutionSource.Contains('await CompleteWorkpieceExecutionAsync(')) '方案流程组根工件必须通过协调器封口并推进顺序域。'
Assert-True ($processSource.Contains('CompleteWorkpieceExecutionAsync(')) '自有工件必须通过协调器封口，不能只等待上下文本地终态。'

[Environment]::CurrentDirectory = $debugDirectory
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$processType = $assembly.GetType('TDJS_Vision.Process', $true)
$solutionType = $assembly.GetType('TDJS_Vision.Solution', $true)
$contextType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceExecutionContext', $true)
$gapEventArgsType = $assembly.GetType('TDJS_Vision.ResourceManagement.OrderedSignalGapEventArgs', $true)
$cameraTriggerKindType = $assembly.GetType('TDJS_Vision.ResourceManagement.CameraTriggerKind', $true)
$taskType = [Threading.Tasks.Task]

# 使用纯C#线程阻塞真实票据触发入口，避免PowerShell后台线程缺少Runspace影响竞态测试。
Add-Type -TypeDefinition @'
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

public sealed class BlockingCameraTriggerProbe : IDisposable
{
    private readonly ManualResetEventSlim _entered = new ManualResetEventSlim(false);
    private readonly ManualResetEventSlim _release = new ManualResetEventSlim(false);

    public bool WaitUntilEntered(int millisecondsTimeout)
    {
        return _entered.Wait(millisecondsTimeout);
    }

    public Task IssueAsync(object ticket)
    {
        return Task.Run(() =>
        {
            try
            {
                ticket.GetType().GetMethod("IssueSoftwareTrigger").Invoke(
                    ticket,
                    new object[] { new Action(() => { _entered.Set(); _release.Wait(); }) });
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        });
    }

    public void Release()
    {
        _release.Set();
    }

    public void Dispose()
    {
        _release.Set();
        _entered.Dispose();
        _release.Dispose();
    }
}
'@

$triggerMethods = @($processType.GetMethods() | Where-Object { $_.Name -eq 'TriggerProcess' })
Assert-True ($triggerMethods.Count -eq 2) 'TriggerProcess必须同时保留旧签名和显式取消令牌签名。'
Assert-True (($triggerMethods | Where-Object { $_.ReturnType -ne $taskType }).Count -eq 0) 'TriggerProcess所有公开重载必须返回Task。'
Assert-True ($null -ne $processType.GetMethod('GetWorkpieceGateSnapshot')) 'Process必须公开只读队列诊断快照。'
Assert-True ($null -ne $solutionType.GetProperty('WorkpieceContextAccessor')) 'Solution必须持有全方案唯一工件上下文访问器。'
Assert-True ($null -ne $solutionType.GetProperty('OrderedSignalCoordinator')) 'Solution必须公开当前生产会话唯一有序协调器。'

$process = [Activator]::CreateInstance($processType, @('环检测流程'))
$pushPathMethod = $processType.GetMethod('PushProcessRunPath', [Reflection.BindingFlags]'Instance,NonPublic')

$cancelSource = [Threading.CancellationTokenSource]::new()
$cancelSource.Cancel()
$runInternalMethod = $processType.GetMethod('RunInternalWithResultAsync')
$cancelledTask = $runInternalMethod.Invoke($process, @($false, $true, $cancelSource.Token))
$cancelledResult = $cancelledTask.GetAwaiter().GetResult()
Assert-True ($cancelledResult.Cancelled -and -not $cancelledResult.Succeeded) '已取消的调用令牌必须在流程准入前产生独立取消结果。'
$cancelSource.Dispose()

$solution = $solutionType.GetProperty('Instance').GetValue($null)
$createContextMethod = $solutionType.GetMethod('CreateWorkpieceExecutionContext', [Reflection.BindingFlags]'Instance,NonPublic')
$canInheritMethod = $solutionType.GetMethod('CanInheritWorkpieceContext', [Reflection.BindingFlags]'Instance,NonPublic')
$generatorProperty = $solutionType.GetProperty('WorkpieceIdentityGenerator', [Reflection.BindingFlags]'Instance,NonPublic')
$generator = $generatorProperty.GetValue($solution)
$resetSessionMethod = $generator.GetType().GetMethod('ResetSession', [Reflection.BindingFlags]'Instance,NonPublic')
$renewCoordinatorMethod = $solutionType.GetMethod('RenewOrderedSignalCoordinator', [Reflection.BindingFlags]'Instance,NonPublic')
$coordinatorProperty = $solutionType.GetProperty('OrderedSignalCoordinator')
$cameraRegistryProperty = $solutionType.GetProperty('CameraTriggerTicketRegistry')
$contextAccessor = $solutionType.GetProperty('WorkpieceContextAccessor').GetValue($solution)

$cycleContext = $createContextMethod.Invoke($solution, @('环检测产线', [long]1, '环检测测试', [Threading.CancellationToken]::None))
$cycleContextScope = $contextAccessor.Push($cycleContext)
$pathScope = $pushPathMethod.Invoke($process, @())
$pathScope.BindIdentity($cycleContext.Identity)
$cycleRejected = $false
try {
    $null = $pushPathMethod.Invoke($process, @())
}
catch {
    $currentException = $_.Exception
    while ($null -ne $currentException.InnerException) {
        $currentException = $currentException.InnerException
    }
    $cycleRejected = $currentException -is [InvalidOperationException]
}
finally {
    $pathScope.Dispose()
    $cycleContextScope.Dispose()
}
Assert-True $cycleRejected '同一工件异步调用链重复进入相同流程必须在等待FIFO前拒绝，不能形成死锁。'

$managedProcess = [Activator]::CreateInstance($processType, @('上下文激活流程'))
$managedPathScope = $pushPathMethod.Invoke($managedProcess, @())
$enterManagedMethod = $processType.GetMethod('EnterManagedProcessRunAsync', [Reflection.BindingFlags]'Instance,NonPublic')
$managedScope = $null
try {
    $managedScopeTask = $enterManagedMethod.Invoke(
        $managedProcess,
        @('集成行为测试', [Threading.CancellationToken]::None, $managedPathScope))
    $managedScope = $managedScopeTask.GetAwaiter().GetResult()
    $managedPathScope = $null
    $managedScope.ActivateOwnedContext($contextAccessor)
    Assert-True ([object]::ReferenceEquals($contextAccessor.Current, $managedScope.Context)) 'FIFO获准后必须在流程调用者上下文同步激活工件上下文。'
    $succeededState = [Enum]::Parse($managedScope.Context.TerminalState.GetType(), 'Succeeded')
    $managedScope.CompleteOwnedContextAsync($succeededState).GetAwaiter().GetResult() | Out-Null
}
finally {
    if ($null -ne $managedScope) {
        $managedScope.Dispose()
    }
    if ($null -ne $managedPathScope) {
        $managedPathScope.Dispose()
    }
}
Assert-True ($null -eq $contextAccessor.Current) '流程作用域释放后必须恢复调用者原有工件上下文。'

$activeContext = $createContextMethod.Invoke($solution, @('继承测试产线', [long]1, '接入测试', [Threading.CancellationToken]::None))
Assert-True ([bool]$canInheritMethod.Invoke($solution, @($activeContext))) '当前会话内尚未封口的上下文必须允许被动流程继承。'
$activeContext.TryComplete([Enum]::Parse($activeContext.TerminalState.GetType(), 'Succeeded')) | Out-Null
$activeContext.Completion.GetAwaiter().GetResult() | Out-Null
Assert-True (-not [bool]$canInheritMethod.Invoke($solution, @($activeContext))) '已经封口的上下文不得被延迟异步分支继承。'

$oldEpochContext = $createContextMethod.Invoke($solution, @('旧会话产线', [long]1, '旧会话测试', [Threading.CancellationToken]::None))
$retiredCoordinator = $coordinatorProperty.GetValue($solution)
$resetSessionMethod.Invoke($generator, @()) | Out-Null
$renewCoordinatorMethod.Invoke($solution, @()) | Out-Null
$currentCoordinator = $coordinatorProperty.GetValue($solution)
Assert-True (-not [object]::ReferenceEquals($retiredCoordinator, $currentCoordinator)) '生产会话纪元重建时必须同步替换有序协调器。'
Assert-True (-not [bool]$canInheritMethod.Invoke($solution, @($oldEpochContext))) '旧会话纪元上下文不得进入新生产会话。'

$leasedContext = $createContextMethod.Invoke($solution, @('分支租约产线', [long]1, '分支租约测试', [Threading.CancellationToken]::None))
$acquireLeaseMethod = $contextType.GetMethod('TryAcquireExecutionLease', [Reflection.BindingFlags]'Instance,NonPublic')
$leaseArguments = @($null)
$leaseAcquired = [bool]$acquireLeaseMethod.Invoke($leasedContext, $leaseArguments)
$executionLease = $leaseArguments[0]
Assert-True ($leaseAcquired -and $null -ne $executionLease) '活动工件必须能够原子取得在途流程分支租约。'
$leasedContext.TryComplete([Enum]::Parse($leasedContext.TerminalState.GetType(), 'Succeeded')) | Out-Null
Assert-True (-not $leasedContext.Completion.IsCompleted) '父工件请求封口后必须等待已准入的子流程分支退出。'
$executionLease.Complete([Enum]::Parse($leasedContext.TerminalState.GetType(), 'Succeeded'))
$executionLease.Dispose()
$leasedTerminal = $leasedContext.Completion.GetAwaiter().GetResult()
Assert-True ($leasedTerminal.ToString() -eq 'Succeeded') '最后一个在途流程分支退出后必须发布父工件终态。'

$failedBranchContext = $createContextMethod.Invoke($solution, @('失败分支产线', [long]1, '分支失败测试', [Threading.CancellationToken]::None))
$failedLeaseArguments = @($null)
$null = $acquireLeaseMethod.Invoke($failedBranchContext, $failedLeaseArguments)
$failedBranchContext.TryComplete([Enum]::Parse($failedBranchContext.TerminalState.GetType(), 'Succeeded')) | Out-Null
$failedLeaseArguments[0].Complete([Enum]::Parse($failedBranchContext.TerminalState.GetType(), 'Faulted'))
$failedTerminal = $failedBranchContext.Completion.GetAwaiter().GetResult()
Assert-True ($failedTerminal.ToString() -eq 'Faulted') '子流程失败必须覆盖父流程先前请求的成功终态。'

$commitGapStopMethod = $solutionType.GetMethod('TryCommitOrderedSignalGapStop', [Reflection.BindingFlags]'Instance,NonPublic')
$commitCameraStopMethod = $solutionType.GetMethod('TryCommitCameraProductionFaultStop', [Reflection.BindingFlags]'Instance,NonPublic')
$commitRunStopMethod = $solutionType.GetMethod('TryCommitRunStop', [Reflection.BindingFlags]'Instance,NonPublic')
$beginExternalRunMethod = $solutionType.GetMethod('TryBeginExternalRunSession', [Reflection.BindingFlags]'Instance,NonPublic')
$endExternalRunMethod = $solutionType.GetMethod('EndExternalRunSession', [Reflection.BindingFlags]'Instance,NonPublic')
$activeRunCountField = $solutionType.GetField('_activeRunSessionCount', [Reflection.BindingFlags]'Instance,NonPublic')

$retiredCommitArguments = [object[]]@($retiredCoordinator, $null)
$retiredCommitted = [bool]$commitGapStopMethod.Invoke($solution, $retiredCommitArguments)
Assert-True (-not $retiredCommitted -and $null -eq $retiredCommitArguments[1]) '旧协调器迟到报警被错误提交到当前会话。'

# Commit a stop before cancellation, then prove a zero-active-session handoff rotates the token atomically.
$handoffCommitArguments = [object[]]@($currentCoordinator, $null)
Assert-True ([bool]$commitGapStopMethod.Invoke($solution, $handoffCommitArguments)) '当前协调器缺口停机状态没有提交。'
$retiredTokenSource = $handoffCommitArguments[1]
Assert-True ($null -ne $retiredTokenSource) '缺口停机提交没有捕获待取消令牌源。'
$retiredToken = $retiredTokenSource.Token
$retiredCameraRegistry = $cameraRegistryProperty.GetValue($solution)
Assert-True ([bool]$beginExternalRunMethod.Invoke($solution, @())) '零活动会话未能在缺口提交后建立新会话。'
$freshToken = $solution.CancellationToken
Assert-True (-not $freshToken.IsCancellationRequested) '缺口提交后的新会话复用了待取消令牌。'
Assert-True ($freshToken -ne $retiredToken) '缺口提交后的新会话没有更换运行令牌。'
Assert-True (-not [object]::ReferenceEquals($retiredCameraRegistry, $cameraRegistryProperty.GetValue($solution))) '停止后的新运行会话没有更换相机生产票据注册表。'
try {
    $retiredTokenSource.Cancel()
}
catch [ObjectDisposedException] {
    # 会话交接可能在延迟报警回调恢复前释放旧令牌源。
}
Assert-True (-not $solution.CancellationToken.IsCancellationRequested) '迟到的旧令牌取消误停了已经完成交接的新会话。'
$endExternalRunMethod.Invoke($solution, @()) | Out-Null

# While a run is active, the committed stop must reject every new entry until that run exits.
Assert-True ([bool]$beginExternalRunMethod.Invoke($solution, @())) '活动停机门闩测试未能建立首个运行会话。'
$activeCoordinator = $coordinatorProperty.GetValue($solution)
$activeCommitArguments = [object[]]@($activeCoordinator, $null)
Assert-True ([bool]$commitGapStopMethod.Invoke($solution, $activeCommitArguments)) '活动会话缺口停机状态没有提交。'
$activeCameraRegistry = $cameraRegistryProperty.GetValue($solution)
Assert-True (-not [bool]$beginExternalRunMethod.Invoke($solution, @())) '缺口停机提交后仍允许第二个运行入口进入。'
Assert-True ([object]::ReferenceEquals($activeCameraRegistry, $cameraRegistryProperty.GetValue($solution))) '被拒绝的运行入口错误换代了活动相机票据注册表。'
$activeCommitArguments[1].Cancel()
$endExternalRunMethod.Invoke($solution, @()) | Out-Null

# 正常停止同样必须等旧会话完全退出，再以新令牌和新相机票据表开始。
Assert-True ([bool]$beginExternalRunMethod.Invoke($solution, @())) '正常停止换代测试未能建立首个运行会话。'
$normalStopToken = $solution.CancellationToken
$normalStopCameraRegistry = $cameraRegistryProperty.GetValue($solution)
$normalStopCommitArguments = [object[]]@($null)
Assert-True ([bool]$commitRunStopMethod.Invoke($solution, $normalStopCommitArguments)) '正常停止没有先提交停机门闩。'
Assert-True (-not [bool]$beginExternalRunMethod.Invoke($solution, @())) '正常停止后旧会话退出前仍允许新运行入口进入。'
Assert-True ([object]::ReferenceEquals($normalStopCameraRegistry, $cameraRegistryProperty.GetValue($solution))) '正常停止排空期间错误换代了相机票据注册表。'
$normalStopCommitArguments[0].Cancel()
Assert-True ($normalStopToken.IsCancellationRequested) '正常停止提交后没有取消当前运行令牌。'
$endExternalRunMethod.Invoke($solution, @()) | Out-Null
Assert-True ([bool]$beginExternalRunMethod.Invoke($solution, @())) '旧会话退出后没有允许新运行会话启动。'
Assert-True (-not $solution.CancellationToken.IsCancellationRequested) '停止后的新运行会话仍使用已取消令牌。'
Assert-True (-not [object]::ReferenceEquals($normalStopCameraRegistry, $cameraRegistryProperty.GetValue($solution))) '停止后的新运行会话没有取得全新的相机票据注册表。'
$endExternalRunMethod.Invoke($solution, @()) | Out-Null

# 零活动时也要先换代再启动；旧Stop令牌的迟到取消不得穿透到新会话。
$zeroActiveStopArguments = [object[]]@($null)
Assert-True ([bool]$commitRunStopMethod.Invoke($solution, $zeroActiveStopArguments)) '零活动正常停止没有提交停机门闩。'
$zeroActiveRetiredTokenSource = $zeroActiveStopArguments[0]
$zeroActiveRetiredToken = $zeroActiveRetiredTokenSource.Token
Assert-True ([bool]$beginExternalRunMethod.Invoke($solution, @())) '零活动正常停止后没有换代启动新会话。'
Assert-True ($solution.CancellationToken -ne $zeroActiveRetiredToken) '零活动正常停止后的新会话复用了待取消令牌。'
try { $zeroActiveRetiredTokenSource.Cancel() } catch [ObjectDisposedException] { }
Assert-True (-not $solution.CancellationToken.IsCancellationRequested) '正常停止旧令牌的迟到取消误停了新会话。'
$endExternalRunMethod.Invoke($solution, @()) | Out-Null

# 相机故障必须先提交停机门闩；延迟取消旧令牌不能误停已经换代的新会话。
$retiredCameraFaultArguments = [object[]]@($normalStopCameraRegistry, $null)
Assert-True (-not [bool]$commitCameraStopMethod.Invoke($solution, $retiredCameraFaultArguments)) '旧相机票据表迟到故障被提交到当前会话。'
$cameraFaultRegistry = $cameraRegistryProperty.GetValue($solution)
$cameraFaultArguments = [object[]]@($cameraFaultRegistry, $null)
Assert-True ([bool]$commitCameraStopMethod.Invoke($solution, $cameraFaultArguments)) '当前相机生产故障没有提交停机门闩。'
$cameraFaultTokenSource = $cameraFaultArguments[1]
$cameraFaultToken = $cameraFaultTokenSource.Token
Assert-True ([bool]$beginExternalRunMethod.Invoke($solution, @())) '零活动会话未能在相机故障提交后换代启动。'
Assert-True ($solution.CancellationToken -ne $cameraFaultToken) '相机故障提交后的新会话复用了待取消令牌。'
try { $cameraFaultTokenSource.Cancel() } catch [ObjectDisposedException] { }
Assert-True (-not $solution.CancellationToken.IsCancellationRequested) '迟到的相机故障取消误停了新运行会话。'

# 活动会话内建立阻塞票据后再结束；首会话初始化失败不得提前增加活动计数，释放阻塞后必须能够重新启动。
$initializationFailureRegistry = $cameraRegistryProperty.GetValue($solution)
$initializationFailureContext = $createContextMethod.Invoke(
    $solution,
    @('初始化失败测试产线', [long]1, '票据表关闭超时测试', $solution.CancellationToken))
$softwareTriggerKind = [Enum]::Parse($cameraTriggerKindType, 'Software')
$blockingTicket = $initializationFailureRegistry.Register(
    'CAMERA-SESSION-INIT-FAILURE',
    $initializationFailureContext,
    $softwareTriggerKind,
    [DateTime]::UtcNow.AddSeconds(10),
    [long]256,
    [long]4096)
$blockingProbe = [BlockingCameraTriggerProbe]::new()
$blockingTriggerTask = $blockingProbe.IssueAsync($blockingTicket)
Assert-True ($blockingProbe.WaitUntilEntered(2000)) '初始化失败测试没有进入真实票据触发调用。'
$endExternalRunMethod.Invoke($solution, @()) | Out-Null
$initializationFailed = $false
try {
    $null = $beginExternalRunMethod.Invoke($solution, @())
}
catch {
    $currentException = $_.Exception
    while ($null -ne $currentException.InnerException) {
        $currentException = $currentException.InnerException
    }
    $initializationFailed = $currentException -is [TimeoutException]
}
Assert-True $initializationFailed '旧票据表关闭超时没有中止首会话初始化。'
Assert-True ([int]$activeRunCountField.GetValue($solution) -eq 0) '首会话初始化失败遗留了虚假活动计数。'
Assert-True (-not $solution.IsRunning) '首会话初始化失败后方案仍被标记为运行中。'
$blockingProbe.Release()
Assert-True ($blockingTriggerTask.Wait(5000)) '阻塞中的票据触发调用没有退出。'
$blockingTriggerTask.GetAwaiter().GetResult() | Out-Null
Assert-True ([bool]$beginExternalRunMethod.Invoke($solution, @())) '票据触发退出后首会话初始化不能重试。'
$endExternalRunMethod.Invoke($solution, @()) | Out-Null
$blockingProbe.Dispose()

Write-Host '工件上下文与流程运行入口集成测试通过。'
