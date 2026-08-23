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

[Environment]::CurrentDirectory = $debugDirectory
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$processType = $assembly.GetType('TDJS_Vision.Process', $true)
$solutionType = $assembly.GetType('TDJS_Vision.Solution', $true)
$contextType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceExecutionContext', $true)
$taskType = [Threading.Tasks.Task]

$triggerMethods = @($processType.GetMethods() | Where-Object { $_.Name -eq 'TriggerProcess' })
Assert-True ($triggerMethods.Count -eq 2) 'TriggerProcess必须同时保留旧签名和显式取消令牌签名。'
Assert-True (($triggerMethods | Where-Object { $_.ReturnType -ne $taskType }).Count -eq 0) 'TriggerProcess所有公开重载必须返回Task。'
Assert-True ($null -ne $processType.GetMethod('GetWorkpieceGateSnapshot')) 'Process必须公开只读队列诊断快照。'
Assert-True ($null -ne $solutionType.GetProperty('WorkpieceContextAccessor')) 'Solution必须持有全方案唯一工件上下文访问器。'

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
$contextAccessor = $solutionType.GetProperty('WorkpieceContextAccessor').GetValue($solution)

$cycleContext = $createContextMethod.Invoke($solution, @('测试产线', [long]1, '环检测测试', [Threading.CancellationToken]::None))
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

$activeContext = $createContextMethod.Invoke($solution, @('测试产线', [long]1, '接入测试', [Threading.CancellationToken]::None))
Assert-True ([bool]$canInheritMethod.Invoke($solution, @($activeContext))) '当前会话内尚未封口的上下文必须允许被动流程继承。'
$activeContext.TryComplete([Enum]::Parse($activeContext.TerminalState.GetType(), 'Succeeded')) | Out-Null
$activeContext.Completion.GetAwaiter().GetResult() | Out-Null
Assert-True (-not [bool]$canInheritMethod.Invoke($solution, @($activeContext))) '已经封口的上下文不得被延迟异步分支继承。'

$oldEpochContext = $createContextMethod.Invoke($solution, @('测试产线', [long]1, '旧会话测试', [Threading.CancellationToken]::None))
$resetSessionMethod.Invoke($generator, @()) | Out-Null
Assert-True (-not [bool]$canInheritMethod.Invoke($solution, @($oldEpochContext))) '旧会话纪元上下文不得进入新生产会话。'

$leasedContext = $createContextMethod.Invoke($solution, @('测试产线', [long]1, '分支租约测试', [Threading.CancellationToken]::None))
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

$failedBranchContext = $createContextMethod.Invoke($solution, @('测试产线', [long]1, '分支失败测试', [Threading.CancellationToken]::None))
$failedLeaseArguments = @($null)
$null = $acquireLeaseMethod.Invoke($failedBranchContext, $failedLeaseArguments)
$failedBranchContext.TryComplete([Enum]::Parse($failedBranchContext.TerminalState.GetType(), 'Succeeded')) | Out-Null
$failedLeaseArguments[0].Complete([Enum]::Parse($failedBranchContext.TerminalState.GetType(), 'Faulted'))
$failedTerminal = $failedBranchContext.Completion.GetAwaiter().GetResult()
Assert-True ($failedTerminal.ToString() -eq 'Faulted') '子流程失败必须覆盖父流程先前请求的成功终态。'

Write-Host '工件上下文与流程运行入口集成测试通过。'
