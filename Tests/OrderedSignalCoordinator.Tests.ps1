$ErrorActionPreference = 'Stop'

# The production application targets .NET Framework 4.8.
if ($PSVersionTable.PSEdition -eq 'Core') {
    $windowsPowerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    & $windowsPowerShell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $PSCommandPath
    exit $LASTEXITCODE
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Wait-TaskResult {
    param([Threading.Tasks.Task]$Task, [string]$Message)
    Assert-True ($Task.Wait(5000)) $Message
    return $Task.GetAwaiter().GetResult()
}

function Wait-Task {
    param([Threading.Tasks.Task]$Task, [string]$Message)
    Assert-True ($Task.Wait(5000)) $Message
    $null = $Task.GetAwaiter().GetResult()
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$buildDirectory = if ($env:TDJS_TEST_BUILD_DIRECTORY) { $env:TDJS_TEST_BUILD_DIRECTORY } else { 'bin\x64\Debug' }
$debugDirectory = Join-Path $projectRoot $buildDirectory
[xml]$project = Get-Content -Raw -Encoding UTF8 (Join-Path $projectRoot 'TDJS-Vision.csproj')
$assemblyName = @($project.Project.PropertyGroup.AssemblyName | Where-Object { $_ })[0]
$application = Get-Item -LiteralPath (Join-Path $debugDirectory ($assemblyName + '.exe')) -ErrorAction SilentlyContinue
$source = Get-Item -LiteralPath (Join-Path $projectRoot 'ResourceManagement\OrderedSignalCoordinator.cs')
Assert-True ($null -ne $application) 'A current Debug application is required.'
Assert-True ($application.LastWriteTimeUtc -ge $source.LastWriteTimeUtc) 'The Debug application is older than the coordinator source.'

[Environment]::CurrentDirectory = $debugDirectory
$assemblyResolveHandler = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $name = [Reflection.AssemblyName]::new($eventArgs.Name)
    $dependencyPath = Join-Path $debugDirectory ($name.Name + '.dll')
    if (Test-Path -LiteralPath $dependencyPath) {
        return [Reflection.Assembly]::LoadFrom($dependencyPath)
    }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($assemblyResolveHandler)
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$identityType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceIdentity', $true)
$contextType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceExecutionContext', $true)
$terminalType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceTerminalState', $true)
$nodeKindType = $assembly.GetType('TDJS_Vision.ResourceManagement.OrderedSignalNodeKind', $true)
$sendResultType = $assembly.GetType('TDJS_Vision.ResourceManagement.OrderedSignalSendResult', $true)
$sendStatusType = $assembly.GetType('TDJS_Vision.ResourceManagement.OrderedSignalSendStatus', $true)
$coordinatorType = $assembly.GetType('TDJS_Vision.ResourceManagement.OrderedSignalCoordinator', $true)
$reserveMethod = $contextType.GetMethod('ReserveSignalIntent', [Reflection.BindingFlags]'Instance,NonPublic')
$finalizeMethod = $contextType.GetMethod('FinalizeSignalIntent', [Reflection.BindingFlags]'Instance,NonPublic')

# All delegate bodies execute as plain C# on worker threads so test results do not depend on a PowerShell Runspace.
Add-Type -TypeDefinition @'
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

public sealed class OrderedSignalActionProbe : IDisposable
{
    private static int s_activeCount;
    private static int s_maxActiveCount;
    private readonly ManualResetEventSlim _entered = new ManualResetEventSlim(false);
    private readonly ManualResetEventSlim _release = new ManualResetEventSlim(false);
    private int _callCount;

    public object Result { get; set; }

    public bool ThrowAfterStart { get; set; }

    public int CallCount { get { return Volatile.Read(ref _callCount); } }

    public bool HasEntered { get { return _entered.IsSet; } }

    public static int MaxActiveCount { get { return Volatile.Read(ref s_maxActiveCount); } }

    public static void ResetConcurrency()
    {
        Volatile.Write(ref s_activeCount, 0);
        Volatile.Write(ref s_maxActiveCount, 0);
    }

    public Delegate CreateDelegate(Type resultType)
    {
        MethodInfo method = GetType().GetMethod("ExecuteAsync").MakeGenericMethod(resultType);
        Type taskType = typeof(Task<>).MakeGenericType(resultType);
        Type delegateType = typeof(Func<>).MakeGenericType(taskType);
        return Delegate.CreateDelegate(delegateType, this, method);
    }

    public bool WaitUntilEntered(int millisecondsTimeout)
    {
        return _entered.Wait(millisecondsTimeout);
    }

    public void Release()
    {
        _release.Set();
    }

    public Task<T> ExecuteAsync<T>()
    {
        int active = Interlocked.Increment(ref s_activeCount);
        UpdateMaximum(active);
        Interlocked.Increment(ref _callCount);
        _entered.Set();
        try
        {
            _release.Wait();
            if (ThrowAfterStart)
                throw new InvalidOperationException("simulated post-start failure");
            return Task.FromResult((T)Result);
        }
        finally
        {
            Interlocked.Decrement(ref s_activeCount);
        }
    }

    private static void UpdateMaximum(int value)
    {
        int current = Volatile.Read(ref s_maxActiveCount);
        while (value > current)
        {
            int observed = Interlocked.CompareExchange(ref s_maxActiveCount, value, current);
            if (observed == current)
                return;
            current = observed;
        }
    }

    public void Dispose()
    {
        _release.Set();
        _entered.Dispose();
        _release.Dispose();
    }
}
'@

$none = [Threading.CancellationToken]::None
$succeeded = [Enum]::Parse($terminalType, 'Succeeded')
$plcWrite = [Enum]::Parse($nodeKindType, 'PLCWrite')
$localCompleted = [Enum]::Parse($sendStatusType, 'LocalCallCompleted')
$cancelledBeforeStart = [Enum]::Parse($sendStatusType, 'CancelledBeforeStart')
$unknown = [Enum]::Parse($sendStatusType, 'Unknown')
$rejected = [Enum]::Parse($sendStatusType, 'Rejected')
$stopped = [Enum]::Parse($terminalType, 'Stopped')
$localResult = [Activator]::CreateInstance($sendResultType, @($localCompleted, 'completed', $null))

function New-Context {
    param([Guid]$Epoch, [string]$Domain, [long]$Sequence)
    $identity = [Activator]::CreateInstance($identityType, @($Epoch, $Domain, $Sequence))
    return [Activator]::CreateInstance(
        $contextType,
        @($identity, $Sequence, $Domain, 'test', [DateTime]::UtcNow, $none))
}

function New-Intent {
    param([object]$Context, [string]$Path, [string]$Endpoint, [OrderedSignalActionProbe]$Probe)
    $delegate = $Probe.CreateDelegate($sendResultType)
    return $reserveMethod.Invoke($Context, @($Path, $plcWrite, $Endpoint, $delegate))
}

function Complete-Intent {
    param([object]$Context, [object]$Intent, [object]$Result)
    $null = $finalizeMethod.Invoke($Context, @($Intent.IntentId, $Result.Status))
}

# A later workpiece may submit first, but it cannot execute before the previous workpiece is sealed.
$coordinator = [Activator]::CreateInstance($coordinatorType)
$epoch = [Guid]::NewGuid()
$context1 = New-Context $epoch 'LINE-A' 1
$context2 = New-Context $epoch 'LINE-A' 2
$coordinator.RegisterWorkpiece($context1)
$coordinator.RegisterWorkpiece($context2)
$probe1 = [OrderedSignalActionProbe]::new()
$probe2 = [OrderedSignalActionProbe]::new()
$probe1.Result = $localResult
$probe2.Result = $localResult
$intent1 = New-Intent $context1 'flow/signal-1' 'plc:shared' $probe1
$intent2 = New-Intent $context2 'flow/signal-2' 'plc:shared' $probe2
$task2 = $coordinator.SubmitAsync($intent2, $none)
Assert-True (-not $probe2.WaitUntilEntered(150)) 'A later workpiece bypassed the FIFO head.'
$task1 = $coordinator.SubmitAsync($intent1, $none)
Assert-True ($probe1.WaitUntilEntered(2000)) 'The FIFO head did not start.'
$probe1.Release()
$result1 = Wait-TaskResult $task1 'The first signal did not finish.'
Assert-True ($result1.Status -eq $localCompleted) 'The first signal returned an unexpected status.'
Complete-Intent $context1 $intent1 $result1
Assert-True (-not $probe2.WaitUntilEntered(150)) 'The next workpiece started before the first workpiece was sealed.'
Wait-Task ($coordinator.CompleteWorkpieceAsync($context1, $succeeded)) 'The first workpiece did not seal.'
Assert-True ($probe2.WaitUntilEntered(2000)) 'The next workpiece did not start after FIFO advancement.'
$probe2.Release()
$result2 = Wait-TaskResult $task2 'The second signal did not finish.'
Assert-True ($result2.Status -eq $localCompleted) 'The second signal returned an unexpected status.'
$duplicateResult = Wait-TaskResult ($coordinator.SubmitAsync($intent2, $none)) 'The active-workpiece duplicate did not return its first result.'
Assert-True ($duplicateResult.Status -eq $localCompleted) 'The active-workpiece duplicate changed its first result.'
Assert-True ($probe2.CallCount -eq 1) 'The active-workpiece duplicate repeated the device action.'
Complete-Intent $context2 $intent2 $result2
Wait-Task ($coordinator.CompleteWorkpieceAsync($context2, $succeeded)) 'The second workpiece did not seal.'
$retiredDuplicateRejected = $false
try {
    $null = $coordinator.SubmitAsync($intent2, $none)
}
catch {
    $currentException = $_.Exception
    while ($null -ne $currentException.InnerException) {
        $currentException = $currentException.InnerException
    }
    $retiredDuplicateRejected = $currentException -is [InvalidOperationException]
}
Assert-True $retiredDuplicateRejected 'A retired-workpiece duplicate was not explicitly rejected.'
Assert-True ($probe2.CallCount -eq 1) 'A retired-workpiece duplicate repeated the device action.'
$coordinator.Dispose()
$probe1.Dispose()
$probe2.Dispose()

# Signals inside one workpiece execute by their reserved sequence even when submitted in reverse order.
$coordinator = [Activator]::CreateInstance($coordinatorType)
$epoch = [Guid]::NewGuid()
$context = New-Context $epoch 'LINE-INNER' 1
$coordinator.RegisterWorkpiece($context)
$probe1 = [OrderedSignalActionProbe]::new()
$probe2 = [OrderedSignalActionProbe]::new()
$probe1.Result = $localResult
$probe2.Result = $localResult
$intent1 = New-Intent $context 'flow/inner-1' 'serial:inner' $probe1
$intent2 = New-Intent $context 'flow/inner-2' 'serial:inner' $probe2
$task2 = $coordinator.SubmitAsync($intent2, $none)
Assert-True (-not $probe2.WaitUntilEntered(150)) 'Signal sequence 2 bypassed missing sequence 1.'
$task1 = $coordinator.SubmitAsync($intent1, $none)
Assert-True ($probe1.WaitUntilEntered(2000)) 'Signal sequence 1 did not start.'
Assert-True (-not $probe2.HasEntered) 'Signal sequence 2 overlapped sequence 1.'
$probe1.Release()
$result1 = Wait-TaskResult $task1 'Signal sequence 1 did not finish.'
Assert-True ($probe2.WaitUntilEntered(2000)) 'Signal sequence 2 did not follow sequence 1.'
$probe2.Release()
$result2 = Wait-TaskResult $task2 'Signal sequence 2 did not finish.'
Complete-Intent $context $intent1 $result1
Complete-Intent $context $intent2 $result2
Wait-Task ($coordinator.CompleteWorkpieceAsync($context, $succeeded)) 'The multi-signal workpiece did not seal.'
$coordinator.Dispose()
$probe1.Dispose()
$probe2.Dispose()

# Two order domains sharing one physical endpoint must never overlap.
[OrderedSignalActionProbe]::ResetConcurrency()
$coordinator = [Activator]::CreateInstance($coordinatorType)
$epoch = [Guid]::NewGuid()
$contextA = New-Context $epoch 'LINE-A' 1
$contextB = New-Context $epoch 'LINE-B' 1
$coordinator.RegisterWorkpiece($contextA)
$coordinator.RegisterWorkpiece($contextB)
$probeA = [OrderedSignalActionProbe]::new()
$probeB = [OrderedSignalActionProbe]::new()
$probeA.Result = $localResult
$probeB.Result = $localResult
$intentA = New-Intent $contextA 'flow/a' 'camera-io:1' $probeA
$intentB = New-Intent $contextB 'flow/b' 'CAMERA-IO:1' $probeB
$taskA = $coordinator.SubmitAsync($intentA, $none)
$taskB = $coordinator.SubmitAsync($intentB, $none)
$deadline = [DateTime]::UtcNow.AddSeconds(2)
while (-not $probeA.HasEntered -and -not $probeB.HasEntered -and [DateTime]::UtcNow -lt $deadline) {
    [Threading.Thread]::Sleep(5)
}
Assert-True ($probeA.HasEntered -xor $probeB.HasEntered) 'Exactly one action should own a shared endpoint.'
$probeA.Release()
$probeB.Release()
$resultA = Wait-TaskResult $taskA 'The first shared-endpoint action did not finish.'
$resultB = Wait-TaskResult $taskB 'The second shared-endpoint action did not finish.'
Complete-Intent $contextA $intentA $resultA
Complete-Intent $contextB $intentB $resultB
Assert-True ([OrderedSignalActionProbe]::MaxActiveCount -eq 1) 'A physical endpoint executed concurrently.'
Wait-Task ($coordinator.CompleteWorkpieceAsync($contextA, $succeeded)) 'Domain A did not seal.'
Wait-Task ($coordinator.CompleteWorkpieceAsync($contextB, $succeeded)) 'Domain B did not seal.'
$coordinator.Dispose()
$probeA.Dispose()
$probeB.Dispose()

# Different physical endpoints are allowed to execute concurrently.
[OrderedSignalActionProbe]::ResetConcurrency()
$coordinator = [Activator]::CreateInstance($coordinatorType)
$epoch = [Guid]::NewGuid()
$contextA = New-Context $epoch 'LINE-A' 1
$contextB = New-Context $epoch 'LINE-B' 1
$coordinator.RegisterWorkpiece($contextA)
$coordinator.RegisterWorkpiece($contextB)
$probeA = [OrderedSignalActionProbe]::new()
$probeB = [OrderedSignalActionProbe]::new()
$probeA.Result = $localResult
$probeB.Result = $localResult
$intentA = New-Intent $contextA 'flow/a' 'plc:1' $probeA
$intentB = New-Intent $contextB 'flow/b' 'plc:2' $probeB
$taskA = $coordinator.SubmitAsync($intentA, $none)
$taskB = $coordinator.SubmitAsync($intentB, $none)
Assert-True ($probeA.WaitUntilEntered(2000)) 'The first independent endpoint did not start.'
Assert-True ($probeB.WaitUntilEntered(2000)) 'The second independent endpoint did not start.'
Assert-True ([OrderedSignalActionProbe]::MaxActiveCount -ge 2) 'Independent endpoints were unnecessarily serialized.'
$probeA.Release()
$probeB.Release()
$resultA = Wait-TaskResult $taskA 'The first independent action did not finish.'
$resultB = Wait-TaskResult $taskB 'The second independent action did not finish.'
Complete-Intent $contextA $intentA $resultA
Complete-Intent $contextB $intentB $resultB
Wait-Task ($coordinator.CompleteWorkpieceAsync($contextA, $succeeded)) 'Independent domain A did not seal.'
Wait-Task ($coordinator.CompleteWorkpieceAsync($contextB, $succeeded)) 'Independent domain B did not seal.'
$coordinator.Dispose()
$probeA.Dispose()
$probeB.Dispose()

# Cancellation while queued removes the action before start and blocks the affected order domain.
$coordinator = [Activator]::CreateInstance($coordinatorType)
$epoch = [Guid]::NewGuid()
$context1 = New-Context $epoch 'LINE-C' 1
$context2 = New-Context $epoch 'LINE-C' 2
$coordinator.RegisterWorkpiece($context1)
$coordinator.RegisterWorkpiece($context2)
$probe1 = [OrderedSignalActionProbe]::new()
$probe2 = [OrderedSignalActionProbe]::new()
$probe1.Result = $localResult
$probe2.Result = $localResult
$task1 = $coordinator.SubmitAsync((New-Intent $context1 'flow/head' 'serial:1' $probe1), $none)
Assert-True ($probe1.WaitUntilEntered(2000)) 'The cancellation test head did not start.'
$cts = [Threading.CancellationTokenSource]::new()
$task2 = $coordinator.SubmitAsync((New-Intent $context2 'flow/cancelled' 'serial:1' $probe2), $cts.Token)
$cts.Cancel()
$result2 = Wait-TaskResult $task2 'The queued cancellation did not finish.'
Assert-True ($result2.Status -eq $cancelledBeforeStart) 'Queued cancellation returned the wrong status.'
Assert-True ($probe2.CallCount -eq 0) 'A cancelled queued action still reached the device delegate.'
$probe1.Release()
$null = Wait-TaskResult $task1 'The cancellation test head did not finish.'
$coordinator.Dispose()
$cts.Dispose()
$probe1.Dispose()
$probe2.Dispose()

# A post-start exception is Unknown and keeps the endpoint unavailable until coordinator shutdown.
$coordinator = [Activator]::CreateInstance($coordinatorType)
$epoch = [Guid]::NewGuid()
$contextA = New-Context $epoch 'LINE-U1' 1
$contextB = New-Context $epoch 'LINE-U2' 1
$coordinator.RegisterWorkpiece($contextA)
$coordinator.RegisterWorkpiece($contextB)
$unknownProbe = [OrderedSignalActionProbe]::new()
$waitingProbe = [OrderedSignalActionProbe]::new()
$unknownProbe.Result = $localResult
$unknownProbe.ThrowAfterStart = $true
$waitingProbe.Result = $localResult
$unknownTask = $coordinator.SubmitAsync((New-Intent $contextA 'flow/unknown' 'plc:frozen' $unknownProbe), $none)
Assert-True ($unknownProbe.WaitUntilEntered(2000)) 'The unknown-result action did not start.'
$unknownProbe.Release()
$unknownResult = Wait-TaskResult $unknownTask 'The unknown-result action did not finish.'
Assert-True ($unknownResult.Status -eq $unknown) 'A post-start exception was not classified as Unknown.'
$waitingTask = $coordinator.SubmitAsync((New-Intent $contextB 'flow/waiting' 'PLC:FROZEN' $waitingProbe), $none)
[Threading.Thread]::Sleep(150)
Assert-True (-not $waitingProbe.HasEntered) 'A frozen endpoint allowed a second device action.'
$coordinator.Dispose()
$waitingResult = Wait-TaskResult $waitingTask 'Coordinator shutdown did not cancel the frozen-endpoint waiter.'
Assert-True ($waitingResult.Status -eq $cancelledBeforeStart) 'The frozen-endpoint waiter returned the wrong status.'
Assert-True ($waitingProbe.CallCount -eq 0) 'The frozen-endpoint waiter reached the device delegate.'
$unknownProbe.Dispose()
$waitingProbe.Dispose()

# A later workpiece waiting behind a missing head must raise one diagnostic and stop the domain.
$coordinator = [Activator]::CreateInstance($coordinatorType, @(1000))
$epoch = [Guid]::NewGuid()
$missingContext = New-Context $epoch 'LINE-GAP-TIMEOUT' 1
$waitingContext = New-Context $epoch 'LINE-GAP-TIMEOUT' 2
$coordinator.RegisterWorkpiece($missingContext)
$coordinator.RegisterWorkpiece($waitingContext)
$waitingProbe = [OrderedSignalActionProbe]::new()
$waitingProbe.Result = $localResult
$gapSource = 'ordered-gap-timeout-' + [Guid]::NewGuid().ToString('N')
$null = Register-ObjectEvent -InputObject $coordinator -EventName OrderGapDetected -SourceIdentifier $gapSource
$waitingTask = $waitingContext.SubmitOrderedSignalAsync(
    $coordinator,
    'flow/gap-waiting',
    $plcWrite,
    'plc:gap',
    $waitingProbe.CreateDelegate($sendResultType),
    $none)
$waitingSealTask = $coordinator.CompleteWorkpieceAsync($waitingContext, $succeeded)
Assert-True (-not $waitingSealTask.IsCompleted) 'The persistent-gap waiter sealed before its signal received a result.'
$gapEvent = Wait-Event -SourceIdentifier $gapSource -Timeout 3
Assert-True ($null -ne $gapEvent) 'A persistent order gap did not raise an active diagnostic.'
$gap = $gapEvent.SourceEventArgs
Assert-True ($gap.OrderDomain -eq 'LINE-GAP-TIMEOUT') 'The gap diagnostic reported the wrong order domain.'
Assert-True ($gap.ExpectedWorkpieceSequence -eq 1 -and $gap.ExpectedSignalSequence -eq 1) 'The gap diagnostic reported the wrong missing predecessor.'
Assert-True ($gap.FirstWaitingWorkpieceSequence -eq 2 -and $gap.FirstWaitingSignalSequence -eq 1) 'The gap diagnostic reported the wrong first waiter.'
Assert-True ($gap.WaitingIntentCount -eq 1 -and $gap.TimeoutMilliseconds -eq 1000) 'The gap diagnostic reported the wrong pressure or timeout.'
Assert-True ($gap.Message.Contains('LINE-GAP-TIMEOUT') -and $gap.Message.Contains('1000ms')) 'The gap diagnostic omitted stable domain or timeout evidence.'
$waitingResult = Wait-TaskResult $waitingTask 'The persistent-gap waiter did not receive a final result.'
Assert-True ($waitingResult.Status -eq $rejected) 'A persistent-gap waiter was not explicitly rejected.'
Assert-True ($waitingProbe.CallCount -eq 0) 'A persistent-gap waiter reached the device action.'
Wait-Task $waitingSealTask 'The persistent-gap waiter did not finish its automatic signal cleanup.'
Assert-True ((Wait-TaskResult $missingContext.Completion 'The missing-head context did not stop.') -eq $stopped) 'The missing-head context did not enter Stopped.'
Assert-True ((Wait-TaskResult $waitingContext.Completion 'The waiting context did not stop.') -eq $stopped) 'The waiting context did not enter Stopped.'
Remove-Event -SourceIdentifier $gapSource -ErrorAction SilentlyContinue
Unregister-Event -SourceIdentifier $gapSource -ErrorAction SilentlyContinue
$coordinator.Dispose()
$waitingProbe.Dispose()

# A missing signal inside a workpiece must upgrade an already requested successful seal to Stopped.
$coordinator = [Activator]::CreateInstance($coordinatorType, @(1000))
$epoch = [Guid]::NewGuid()
$innerGapContext = New-Context $epoch 'LINE-INNER-GAP' 1
$coordinator.RegisterWorkpiece($innerGapContext)
$innerProbe1 = [OrderedSignalActionProbe]::new()
$innerProbe2 = [OrderedSignalActionProbe]::new()
$innerProbe1.Result = $localResult
$innerProbe2.Result = $localResult
$innerIntent1 = New-Intent $innerGapContext 'flow/inner-gap-1' 'plc:inner-gap' $innerProbe1
$innerIntent2 = New-Intent $innerGapContext 'flow/inner-gap-2' 'plc:inner-gap' $innerProbe2
$innerGapSource = 'ordered-inner-gap-' + [Guid]::NewGuid().ToString('N')
$null = Register-ObjectEvent -InputObject $coordinator -EventName OrderGapDetected -SourceIdentifier $innerGapSource
$innerTask2 = $coordinator.SubmitAsync($innerIntent2, $none)
$innerSealTask = $coordinator.CompleteWorkpieceAsync($innerGapContext, $succeeded)
Assert-True (-not $innerSealTask.IsCompleted) 'The inner-gap workpiece sealed before its reserved signals finished.'
$innerGapEvent = Wait-Event -SourceIdentifier $innerGapSource -Timeout 3
Assert-True ($null -ne $innerGapEvent) 'A persistent inner-workpiece signal gap did not raise an alarm.'
$innerGap = $innerGapEvent.SourceEventArgs
Assert-True ($innerGap.ExpectedWorkpieceSequence -eq 1 -and $innerGap.ExpectedSignalSequence -eq 1) 'The inner gap reported the wrong expected signal.'
Assert-True ($innerGap.FirstWaitingWorkpieceSequence -eq 1 -and $innerGap.FirstWaitingSignalSequence -eq 2) 'The inner gap reported the wrong queued signal.'
$innerResult2 = Wait-TaskResult $innerTask2 'The inner-gap second signal did not reject.'
$innerResult1 = Wait-TaskResult ($coordinator.SubmitAsync($innerIntent1, $none)) 'The late missing signal did not receive the blocked-domain result.'
Assert-True ($innerResult1.Status -eq $rejected -and $innerResult2.Status -eq $rejected) 'The inner-gap signals did not both reject.'
Assert-True ($innerProbe1.CallCount -eq 0 -and $innerProbe2.CallCount -eq 0) 'An inner-gap signal reached the device action.'
Complete-Intent $innerGapContext $innerIntent1 $innerResult1
Complete-Intent $innerGapContext $innerIntent2 $innerResult2
Wait-Task $innerSealTask 'The inner-gap workpiece did not finish its escalated seal.'
Assert-True ((Wait-TaskResult $innerGapContext.Completion 'The inner-gap context has no terminal state.') -eq $stopped) 'The prior successful seal request was not upgraded to Stopped.'
Remove-Event -SourceIdentifier $innerGapSource -ErrorAction SilentlyContinue
Unregister-Event -SourceIdentifier $innerGapSource -ErrorAction SilentlyContinue
$coordinator.Dispose()
$innerProbe1.Dispose()
$innerProbe2.Dispose()

# A temporary reverse arrival resolved before the threshold must cancel the timer and emit no alarm.
$coordinator = [Activator]::CreateInstance($coordinatorType, @(1000))
$epoch = [Guid]::NewGuid()
$headContext = New-Context $epoch 'LINE-GAP-RECOVERY' 1
$nextContext = New-Context $epoch 'LINE-GAP-RECOVERY' 2
$coordinator.RegisterWorkpiece($headContext)
$coordinator.RegisterWorkpiece($nextContext)
$nextProbe = [OrderedSignalActionProbe]::new()
$nextProbe.Result = $localResult
$nextProbe.Release()
$nextIntent = New-Intent $nextContext 'flow/gap-recovered' 'plc:gap-recovered' $nextProbe
$recoverySource = 'ordered-gap-recovery-' + [Guid]::NewGuid().ToString('N')
$null = Register-ObjectEvent -InputObject $coordinator -EventName OrderGapDetected -SourceIdentifier $recoverySource
$nextTask = $coordinator.SubmitAsync($nextIntent, $none)
[Threading.Thread]::Sleep(200)
Wait-Task ($coordinator.CompleteWorkpieceAsync($headContext, $succeeded)) 'The temporary-gap head did not seal.'
$nextResult = Wait-TaskResult $nextTask 'The recovered-gap signal did not execute.'
Assert-True ($nextResult.Status -eq $localCompleted -and $nextProbe.CallCount -eq 1) 'The recovered-gap signal did not execute exactly once.'
Complete-Intent $nextContext $nextIntent $nextResult
Wait-Task ($coordinator.CompleteWorkpieceAsync($nextContext, $succeeded)) 'The recovered-gap workpiece did not seal.'
$unexpectedGap = Wait-Event -SourceIdentifier $recoverySource -Timeout 2
Assert-True ($null -eq $unexpectedGap) 'A resolved temporary gap raised a late false alarm.'
Remove-Event -SourceIdentifier $recoverySource -ErrorAction SilentlyContinue
Unregister-Event -SourceIdentifier $recoverySource -ErrorAction SilentlyContinue
$coordinator.Dispose()
$nextProbe.Dispose()

# A device action that legitimately runs longer than the gap threshold is not a missing predecessor.
$coordinator = [Activator]::CreateInstance($coordinatorType, @(1000))
$epoch = [Guid]::NewGuid()
$activeContext = New-Context $epoch 'LINE-LONG-DEVICE' 1
$queuedContext = New-Context $epoch 'LINE-LONG-DEVICE' 2
$coordinator.RegisterWorkpiece($activeContext)
$coordinator.RegisterWorkpiece($queuedContext)
$activeProbe = [OrderedSignalActionProbe]::new()
$queuedProbe = [OrderedSignalActionProbe]::new()
$activeProbe.Result = $localResult
$queuedProbe.Result = $localResult
$activeIntent = New-Intent $activeContext 'flow/long-device' 'plc:long-device' $activeProbe
$queuedIntent = New-Intent $queuedContext 'flow/queued-behind-long-device' 'plc:long-device' $queuedProbe
$activeSource = 'ordered-long-device-' + [Guid]::NewGuid().ToString('N')
$null = Register-ObjectEvent -InputObject $coordinator -EventName OrderGapDetected -SourceIdentifier $activeSource
$activeTask = $coordinator.SubmitAsync($activeIntent, $none)
Assert-True ($activeProbe.WaitUntilEntered(2000)) 'The long device action did not start.'
$queuedTask = $coordinator.SubmitAsync($queuedIntent, $none)
[Threading.Thread]::Sleep(100)
Assert-True (-not $queuedProbe.HasEntered) 'The later workpiece bypassed the running device action.'
$unexpectedActiveGap = Wait-Event -SourceIdentifier $activeSource -Timeout 2
Assert-True ($null -eq $unexpectedActiveGap) 'A running device action was misclassified as an order gap.'
$activeProbe.Release()
$activeResult = Wait-TaskResult $activeTask 'The long device action did not finish.'
Complete-Intent $activeContext $activeIntent $activeResult
Wait-Task ($coordinator.CompleteWorkpieceAsync($activeContext, $succeeded)) 'The long-device workpiece did not seal.'
Assert-True ($queuedProbe.WaitUntilEntered(2000)) 'The queued workpiece did not start after the long action sealed.'
$queuedProbe.Release()
$queuedResult = Wait-TaskResult $queuedTask 'The queued workpiece did not finish.'
Complete-Intent $queuedContext $queuedIntent $queuedResult
Wait-Task ($coordinator.CompleteWorkpieceAsync($queuedContext, $succeeded)) 'The queued workpiece did not seal.'
Remove-Event -SourceIdentifier $activeSource -ErrorAction SilentlyContinue
Unregister-Event -SourceIdentifier $activeSource -ErrorAction SilentlyContinue
$coordinator.Dispose()
$activeProbe.Dispose()
$queuedProbe.Dispose()

# Stress a large reverse-submission batch to expose gaps, duplicate sends, or scheduler stalls.
$stressCount = 10000
$coordinator = [Activator]::CreateInstance($coordinatorType)
$epoch = [Guid]::NewGuid()
$context = New-Context $epoch 'LINE-STRESS' 1
$coordinator.RegisterWorkpiece($context)
$probe = [OrderedSignalActionProbe]::new()
$probe.Result = $localResult
$probe.Release()
$intents = New-Object 'object[]' $stressCount
[Threading.Tasks.Task[]]$tasks = New-Object 'Threading.Tasks.Task[]' $stressCount
for ($index = 0; $index -lt $stressCount; $index++) {
    $intents[$index] = New-Intent $context ('flow/stress-' + $index) 'plc:stress' $probe
}
for ($index = $stressCount - 1; $index -ge 0; $index--) {
    $tasks[$index] = $coordinator.SubmitAsync($intents[$index], $none)
}
Assert-True ([Threading.Tasks.Task]::WaitAll($tasks, 60000)) 'The 10000-signal reverse batch timed out.'
Assert-True ($probe.CallCount -eq $stressCount) 'The 10000-signal reverse batch lost or duplicated a device action.'
for ($index = 0; $index -lt $stressCount; $index++) {
    $result = $tasks[$index].GetAwaiter().GetResult()
    Complete-Intent $context $intents[$index] $result
}
Wait-Task ($coordinator.CompleteWorkpieceAsync($context, $succeeded)) 'The stress workpiece did not seal.'
$coordinator.Dispose()
$probe.Dispose()

[AppDomain]::CurrentDomain.remove_AssemblyResolve($assemblyResolveHandler)
Write-Host 'OrderedSignalCoordinator.Tests.ps1 passed.'
