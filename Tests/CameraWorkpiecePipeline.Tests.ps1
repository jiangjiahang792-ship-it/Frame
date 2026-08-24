$ErrorActionPreference = 'Stop'

# 生产程序基于.NET Framework 4.8，PowerShell 7不直接承载其OpenCvSharp原生依赖。
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

$projectRoot = Split-Path -Parent $PSScriptRoot
$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$application = Get-Item -LiteralPath (Join-Path $debugDirectory '机器视觉AI检测系统V1.0.exe') -ErrorAction SilentlyContinue
$source = Get-Item -LiteralPath (Join-Path $projectRoot 'ResourceManagement\CameraWorkpiecePipeline.cs')
Assert-True ($null -ne $application) '相机工件管线测试需要最新Debug程序。'
Assert-True ($application.LastWriteTimeUtc -ge $source.LastWriteTimeUtc) 'Debug程序早于相机工件管线源码，请先重新编译。'

[Environment]::CurrentDirectory = $debugDirectory
$assemblyResolveHandler = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $assemblyName = [Reflection.AssemblyName]::new($eventArgs.Name)
    $dependencyPath = Join-Path $debugDirectory ($assemblyName.Name + '.dll')
    if (Test-Path -LiteralPath $dependencyPath) {
        return [Reflection.Assembly]::LoadFrom($dependencyPath)
    }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($assemblyResolveHandler)
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$openCvAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $debugDirectory 'OpenCvSharp.dll'))
$identityType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceIdentity', $true)
$contextType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceExecutionContext', $true)
$budgetType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceFrameMemoryBudgetManager', $true)
$registryType = $assembly.GetType('TDJS_Vision.ResourceManagement.CameraTriggerTicketRegistry', $true)
$triggerKindType = $assembly.GetType('TDJS_Vision.ResourceManagement.CameraTriggerKind', $true)
$generatorType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceIdentityGenerator', $true)
$nodeImageSourceType = $assembly.GetType('TDJS_Vision.Node._1_Acquisition.ImageSource.NodeImageSource', $true)
$matType = $openCvAssembly.GetType('OpenCvSharp.Mat', $true)
$matScalarType = $openCvAssembly.GetType('OpenCvSharp.Scalar', $true)
$matTypeType = $openCvAssembly.GetType('OpenCvSharp.MatType', $true)

# 用纯C#线程执行并发边界，避免PowerShell后台线程缺少Runspace影响测试结论。
Add-Type -TypeDefinition @'
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

public sealed class CameraTriggerConcurrencyProbe : IDisposable
{
    private readonly ManualResetEventSlim _entered = new ManualResetEventSlim(false);
    private readonly ManualResetEventSlim _release = new ManualResetEventSlim(false);
    private int _triggerCallCount;

    public int TriggerCallCount { get { return Volatile.Read(ref _triggerCallCount); } }

    public bool WaitUntilEntered(int millisecondsTimeout)
    {
        return _entered.Wait(millisecondsTimeout);
    }

    public void Release()
    {
        _release.Set();
    }

    public Task IssueBlockingAsync(object ticket)
    {
        return InvokeAsync(ticket, new Action(() => {
            Interlocked.Increment(ref _triggerCallCount);
            _entered.Set();
            _release.Wait();
        }));
    }

    public Task IssueImmediateAsync(object ticket)
    {
        return InvokeAsync(ticket, new Action(() =>
            Interlocked.Increment(ref _triggerCallCount)));
    }

    public static Task DisposeTicketAsync(object ticket)
    {
        return Task.Run(() => ((IDisposable)ticket).Dispose());
    }

    private static Task InvokeAsync(object ticket, Action triggerAction)
    {
        return Task.Run(() =>
        {
            try
            {
                ticket.GetType().GetMethod("IssueSoftwareTrigger").Invoke(
                    ticket,
                    new object[] { triggerAction });
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        });
    }

    public void Dispose()
    {
        _release.Set();
        _entered.Dispose();
        _release.Dispose();
    }
}
'@

$generator = [Activator]::CreateInstance($generatorType)
$budget = [Activator]::CreateInstance($budgetType)
$registry = [Activator]::CreateInstance($registryType, @($budget))
$prepareStreamResetMethod = $registryType.GetMethod(
    'PrepareCameraStreamReset',
    [Reflection.BindingFlags]'Instance,NonPublic')
$confirmStreamResetMethod = $registryType.GetMethod(
    'ConfirmCameraStreamReset',
    [Reflection.BindingFlags]'Instance,NonPublic')
$waitForProductionFrameMethod = $nodeImageSourceType.GetMethod(
    'WaitForProductionFrameAsync',
    [Reflection.BindingFlags]'Static,NonPublic')
$softwareTrigger = [Enum]::Parse($triggerKindType, 'Software')
$none = [Threading.CancellationToken]::None
$noOpTrigger = [Action] { }

function New-Context {
    param([string]$Domain)
    $identity = $generator.Next($Domain)
    return [Activator]::CreateInstance(
        $contextType,
        @($identity, [long]1, $Domain, '相机票据测试', [DateTime]::UtcNow, $none))
}

function New-TestMat {
    return [Activator]::CreateInstance(
        $matType,
        @([int]10, [int]10, $matTypeType::CV_8UC3, $matScalarType::All(1.0)))
}

function Confirm-CameraStreamReset {
    param([string]$CameraKey)
    Wait-ProductionFaultDrain
    $faultGeneration = $prepareStreamResetMethod.Invoke($registry, @($CameraKey))
    $null = $confirmStreamResetMethod.Invoke($registry, @($CameraKey, [long]$faultGeneration))
}

function Wait-ProductionFaultDrain {
    $deadlineUtc = [DateTime]::UtcNow.AddSeconds(2)
    while ($registry.GetSnapshot().PendingProductionFaultCount -ne 0) {
        if ([DateTime]::UtcNow -ge $deadlineUtc) {
            throw '独立故障派发线程未在2秒内排空。'
        }
        [Threading.Thread]::Sleep(5)
    }
}

$context1 = New-Context '产线A'
$context2 = New-Context '产线A'
$deadline = [DateTime]::UtcNow.AddSeconds(10)
$ticket1 = $registry.Register(' CAMERA-1 ', $context1, $softwareTrigger, $deadline, [long]256, [long]4096)
$ticket2 = $registry.Register('camera-1', $context2, $softwareTrigger, $deadline, [long]256, [long]4096)
$outOfOrderTriggerRejected = $false
try { $ticket2.IssueSoftwareTrigger($noOpTrigger) } catch { $outOfOrderTriggerRejected = $true }
Assert-True $outOfOrderTriggerRejected '前序生产票据尚未取得帧时，后序票据禁止提前发送真实触发。'
$ticket1.IssueSoftwareTrigger($noOpTrigger)
Assert-True ($registry.GetSnapshot().PendingTicketCount -eq 2) '同一相机票据必须按FIFO保留。'

$frame1 = New-TestMat
Assert-True ($registry.TryPublishFrame('CAMERA-1', [long]1, [uint32]100, [Diagnostics.Stopwatch]::GetTimestamp(), $frame1, $null)) '第一帧必须被生产票据接收。'
$envelope1 = $ticket1.Completion.GetAwaiter().GetResult()
Assert-True ($envelope1.Identity.Equals($context1.Identity)) '第一帧必须绑定FIFO队首工件。'
Assert-True (-not $ticket2.Completion.IsCompleted) '第二个工件不能越过第一工件取得帧。'
$ownedImage = $null
$ownedLease = $null
$envelope1.TransferOwnership([ref]$ownedImage, [ref]$ownedLease)
Assert-True ($ownedLease.ReservedBytes -eq 400) '预算必须校正为彩色源Mat与标准灰度缓存的实际总字节。'
$envelope1.Dispose()
$ownedImage.Dispose()
$ownedLease.Dispose()

$ticket2.IssueSoftwareTrigger($noOpTrigger)
$frame2 = New-TestMat
Assert-True ($registry.TryPublishFrame('CAMERA-1', [long]2, [uint32]101, [Diagnostics.Stopwatch]::GetTimestamp(), $frame2, $null)) '第二帧必须继续匹配下一票据。'
$envelope2 = $ticket2.Completion.GetAwaiter().GetResult()
Assert-True ($envelope2.Identity.Equals($context2.Identity)) '第二帧必须绑定第二工件。'
$envelope2.Dispose()
Assert-True ($budget.GetSnapshot().ReservedBytes -eq 0) '全部帧释放后实际字节预算必须归零。'

$context3 = New-Context '产线A'
$ticket3 = $registry.Register('CAMERA-1', $context3, $softwareTrigger, $deadline, [long]256, [long]4096)
$context4 = New-Context '产线A'
$ticket4 = $registry.Register('CAMERA-1', $context4, $softwareTrigger, $deadline, [long]256, [long]4096)
$ticket3.IssueSoftwareTrigger($noOpTrigger)
$brokenFrame = New-TestMat
Assert-True ($registry.TryPublishFrame('CAMERA-1', [long]4, [uint32]103, [Diagnostics.Stopwatch]::GetTimestamp(), $brokenFrame, $null)) '断层帧必须由生产管线接管并明确失败。'
$sequenceFaulted = $false
try {
    $null = $ticket3.Completion.GetAwaiter().GetResult()
}
catch {
    $sequenceFaulted = $true
}
Assert-True $sequenceFaulted 'SDK或内部帧号断层必须失败，不能绑定给后续工件。'
$blockedAfterSequenceFault = $false
try {
    $null = $ticket4.Completion.GetAwaiter().GetResult()
}
catch {
    $blockedAfterSequenceFault = $true
}
Assert-True $blockedAfterSequenceFault '前序帧断层后同相机全部后续票据必须立即失败，不能无限等待。'
Assert-True ($budget.GetSnapshot().ActiveLeaseCount -eq 0) '帧断层清场必须归还该相机全部票据预算。'
Assert-True ($registry.GetSnapshot().FrameSequenceFaultCount -eq 1) '帧号断层必须累计诊断。'
$resetRejected = $false
try {
    $registry.Register('CAMERA-1', (New-Context '产线A'), $softwareTrigger, $deadline, [long]256, [long]4096)
}
catch {
    $resetRejected = $true
}
Assert-True $resetRejected '故障相机在人工重置前必须拒绝新票据。'
Confirm-CameraStreamReset 'CAMERA-1'

$unarmedContext = New-Context '产线D'
$unarmedTicket = $registry.Register('CAMERA-4', $unarmedContext, $softwareTrigger, $deadline, [long]256, [long]4096)
$staleFrame = New-TestMat
Assert-True ($registry.TryPublishFrame('CAMERA-4', [long]1, [uint32]1, [Diagnostics.Stopwatch]::GetTimestamp(), $staleFrame, $null)) '未武装票据前到达的旧帧必须由生产管线接管并故障。'
$unarmedFaulted = $false
try {
    $null = $unarmedTicket.Completion.GetAwaiter().GetResult()
}
catch {
    $unarmedFaulted = $true
}
Assert-True $unarmedFaulted '软件触发票据未标记真实触发时禁止接收任何帧。'
Wait-ProductionFaultDrain
$unarmedResetRejected = $false
try { $registry.ResetCamera('CAMERA-4') } catch { $unarmedResetRejected = $true }
Assert-True $unarmedResetRejected '收到无法绑定的旧帧后普通重置必须拒绝，防止缓存中的下一旧帧污染新工件。'
Confirm-CameraStreamReset 'CAMERA-4'

$queuedBeforeTriggerContext = New-Context '产线旧帧'
$queuedBeforeTriggerTicket = $registry.Register('CAMERA-OLD', $queuedBeforeTriggerContext, $softwareTrigger, $deadline, [long]256, [long]4096)
$queuedBeforeTriggerTimestamp = [Diagnostics.Stopwatch]::GetTimestamp()
[Threading.Thread]::Sleep(1)
$queuedBeforeTriggerTicket.IssueSoftwareTrigger($noOpTrigger)
$queuedBeforeTriggerFrame = New-TestMat
Assert-True ($registry.TryPublishFrame('CAMERA-OLD', [long]1, [uint32]1, $queuedBeforeTriggerTimestamp, $queuedBeforeTriggerFrame, $null)) '触发前已进入SDK回调的旧帧必须由生产管线接管并拒绝。'
$queuedBeforeTriggerFaulted = $false
try { $null = $queuedBeforeTriggerTicket.Completion.GetAwaiter().GetResult() } catch { $queuedBeforeTriggerFaulted = $true }
Assert-True $queuedBeforeTriggerFaulted '触发前旧帧不得绑定到当前工件。'
Confirm-CameraStreamReset 'CAMERA-OLD'

$cancelContext1 = New-Context '产线E'
$cancelContext2 = New-Context '产线E'
$cancelTicket1 = $registry.Register('CAMERA-5', $cancelContext1, $softwareTrigger, $deadline, [long]256, [long]4096)
$cancelTicket2 = $registry.Register('CAMERA-5', $cancelContext2, $softwareTrigger, $deadline, [long]256, [long]4096)
$cancelTicket1.IssueSoftwareTrigger($noOpTrigger)
$cancelTicket1.Dispose()
$cancelAfterTriggerFaulted = $false
$cancelFollowerFaulted = $false
try { $null = $cancelTicket1.Completion.GetAwaiter().GetResult() } catch { $cancelAfterTriggerFaulted = $true }
try { $null = $cancelTicket2.Completion.GetAwaiter().GetResult() } catch { $cancelFollowerFaulted = $true }
Assert-True ($cancelAfterTriggerFaulted -and $cancelFollowerFaulted) '已发触发票据取消必须锁定相机并清场后续票据，禁止迟到帧错配。'
Assert-True ($budget.GetSnapshot().ActiveLeaseCount -eq 0) '已触发取消清场后全部票据预算必须归零。'
Confirm-CameraStreamReset 'CAMERA-5'

$terminalStateType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceTerminalState', $true)
$succeededTerminal = [Enum]::Parse($terminalStateType, 'Succeeded')
$cancelledTerminal = [Enum]::Parse($terminalStateType, 'Cancelled')
$sealContext = New-Context '产线F'
$sealTicket = $registry.Register('CAMERA-6', $sealContext, $softwareTrigger, $deadline, [long]256, [long]4096)
Assert-True ($sealContext.TryComplete($succeededTerminal)) '父工件应能请求成功封口。'
Assert-True (-not $sealContext.Completion.IsCompleted) '相机票据流程分支租约必须阻止父工件提前发布终态。'
$sealTicket.Dispose()
$sealedTerminal = $sealContext.Completion.GetAwaiter().GetResult()
Assert-True ($sealedTerminal -eq $cancelledTerminal) '未触发票据取消必须以Cancelled覆盖父工件请求的成功终态。'

$cancelBeforeIssueContext = New-Context '产线G'
$cancelBeforeIssueTicket = $registry.Register('CAMERA-7', $cancelBeforeIssueContext, $softwareTrigger, $deadline, [long]256, [long]4096)
$cancelBeforeIssueTicket.Dispose()
$triggerCallCount = 0
$issueAfterCancelRejected = $false
try {
    $cancelBeforeIssueTicket.IssueSoftwareTrigger([Action] { $script:triggerCallCount++ })
}
catch {
    $issueAfterCancelRejected = $true
}
Assert-True $issueAfterCancelRejected '票据取消先完成后必须拒绝发送真实软件触发。'
Assert-True ($triggerCallCount -eq 0) '取消后的软件触发动作绝不能被调用。'

$previewConflictTicket = $registry.Register(
    'CAMERA-GATE',
    (New-Context '产线软触发隔离'),
    $softwareTrigger,
    $deadline,
    [long]256,
    [long]4096)
$previewBeforeProductionTriggerRejected = $false
try {
    $unexpectedPreviewLease = $registry.EnterSoftwareTriggerCommand('CAMERA-GATE')
    $unexpectedPreviewLease.Dispose()
}
catch {
    $previewBeforeProductionTriggerRejected = $true
}
Assert-True $previewBeforeProductionTriggerRejected '生产票据已经登记但尚未触发时，普通预览软触发必须被拒绝。'
$previewConflictTicket.Dispose()

$previewLease = $registry.EnterSoftwareTriggerCommand('CAMERA-GATE')
$productionDuringPreviewRejected = $false
try {
    $registry.Register(
        'CAMERA-GATE',
        (New-Context '产线软触发隔离'),
        $softwareTrigger,
        $deadline,
        [long]256,
        [long]4096)
}
catch {
    $productionDuringPreviewRejected = $true
}
finally {
    $previewLease.Dispose()
}
Assert-True $productionDuringPreviewRejected '普通预览软触发执行期间，生产票据必须在真实触发前被拒绝。'

$fastPreviewLease = $registry.EnterSoftwareTriggerCommand('CAMERA-GATE')
$fastPreviewLease.MarkCommandStarted()
$fastPreviewFrame = New-TestMat
Assert-True (-not $registry.TryPublishFrame(
    'CAMERA-GATE',
    [long]1,
    [uint32]1,
    [Diagnostics.Stopwatch]::GetTimestamp(),
    $fastPreviewFrame,
    $null)) '预览帧在SDK命令返回前到达时，也必须提前命中预览帧标记。'
$fastPreviewFrame.Dispose()
$fastPreviewLease.MarkCommandSucceeded()
$fastPreviewLease.Dispose()
$afterFastPreviewTicket = $registry.Register(
    'CAMERA-GATE',
    (New-Context '产线软触发隔离'),
    $softwareTrigger,
    $deadline,
    [long]256,
    [long]4096)
$afterFastPreviewTicket.Dispose()

$failedPreviewLease = $registry.EnterSoftwareTriggerCommand('CAMERA-PREVIEW-FAIL')
$failedPreviewLease.MarkCommandStarted()
$failedPreviewLease.Dispose()
$productionAfterUnknownPreviewRejected = $false
try {
    $registry.Register(
        'CAMERA-PREVIEW-FAIL',
        (New-Context '产线预览失败'),
        $softwareTrigger,
        $deadline,
        [long]256,
        [long]4096)
}
catch {
    $productionAfterUnknownPreviewRejected = $true
}
Assert-True $productionAfterUnknownPreviewRejected '预览SDK命令开始后未确认成功时，生产必须保持流重启门禁。'
Confirm-CameraStreamReset 'CAMERA-PREVIEW-FAIL'
$afterPreviewRecoveryTicket = $registry.Register(
    'CAMERA-PREVIEW-FAIL',
    (New-Context '产线预览失败恢复'),
    $softwareTrigger,
    $deadline,
    [long]256,
    [long]4096)
$afterPreviewRecoveryTicket.Dispose()

$delayedPreviewLease = $registry.EnterSoftwareTriggerCommand('CAMERA-GATE')
$delayedPreviewLease.MarkCommandStarted()
$delayedPreviewLease.MarkCommandSucceeded()
$delayedPreviewLease.Dispose()
$productionBeforePreviewFrameRejected = $false
try {
    $registry.Register(
        'CAMERA-GATE',
        (New-Context '产线软触发隔离'),
        $softwareTrigger,
        $deadline,
        [long]256,
        [long]4096)
}
catch {
    $productionBeforePreviewFrameRejected = $true
}
Assert-True $productionBeforePreviewFrameRejected '预览命令已返回但对应帧尚未到达时，生产票据仍必须拒绝登记。'
$delayedPreviewFrame = New-TestMat
Assert-True (-not $registry.TryPublishFrame(
    'CAMERA-GATE',
    [long]2,
    [uint32]2,
    [Diagnostics.Stopwatch]::GetTimestamp(),
    $delayedPreviewFrame,
    $null)) '延迟预览帧必须留给旧预览显示链路，不能被生产票据接管。'
$delayedPreviewFrame.Dispose()

$productionGateTicket = $registry.Register(
    'CAMERA-GATE',
    (New-Context '产线软触发隔离'),
    $softwareTrigger,
    $deadline,
    [long]256,
    [long]4096)
$script:productionTriggerCommandCount = 0
$productionGateTicket.IssueSoftwareTrigger([Action] {
    $productionLease = $registry.EnterSoftwareTriggerCommand('CAMERA-GATE')
    try {
        $script:productionTriggerCommandCount++
    }
    finally {
        $productionLease.Dispose()
    }
})
Assert-True ($productionTriggerCommandCount -eq 1) '生产票据授权的软件触发命令必须正常执行一次。'
$previewTriggerRejected = $false
try {
    $unexpectedPreviewLease = $registry.EnterSoftwareTriggerCommand('CAMERA-GATE')
    $unexpectedPreviewLease.Dispose()
}
catch {
    $previewTriggerRejected = $true
}
Assert-True $previewTriggerRejected '生产票据已武装后，普通预览软触发必须被拒绝，防止下一帧错配。'
$productionGateTicket.Dispose()
Confirm-CameraStreamReset 'CAMERA-GATE'

$sameCameraContext = New-Context '产线H'
$sameCameraTicket = $registry.Register('CAMERA-8', $sameCameraContext, $softwareTrigger, $deadline, [long]256, [long]4096)
$sameCameraProbe = [CameraTriggerConcurrencyProbe]::new()
$blockingIssue = $sameCameraProbe.IssueBlockingAsync($sameCameraTicket)
Assert-True ($sameCameraProbe.WaitUntilEntered(2000)) '并发测试的软件触发动作必须进入。'
$concurrentCancel = [CameraTriggerConcurrencyProbe]::DisposeTicketAsync($sameCameraTicket)
Assert-True ($concurrentCancel.Wait(1000)) 'SDK触发未返回时取消不能等待SDK锁，否则可能阻塞相机停机。'
Assert-True ($sameCameraProbe.TriggerCallCount -eq 1) '同一相机软件触发只能执行一次。'
$sameCameraFaulted = $false
try { $null = $sameCameraTicket.Completion.GetAwaiter().GetResult() } catch { $sameCameraFaulted = $true }
Assert-True $sameCameraFaulted '触发提交后并发取消必须明确失败票据，禁止迟到帧错配。'
Assert-True ($registry.GetSnapshot().ActiveTriggerDispatchCount -eq 1) 'SDK触发调用未返回时必须保留活动触发诊断。'
$resetDuringDispatchRejected = $false
try { $registry.ResetCamera('CAMERA-8') } catch { $resetDuringDispatchRejected = $true }
Assert-True $resetDuringDispatchRejected 'SDK触发调用未返回时禁止重置相机，防止迟到帧绑定新工件。'
$sameCameraProbe.Release()
$null = [Threading.Tasks.Task]::WaitAll(@($blockingIssue, $concurrentCancel), 2000)
Assert-True ($blockingIssue.IsCompleted -and $concurrentCancel.IsCompleted) '释放闸门后真实触发调用必须及时返回。'
Assert-True ($registry.GetSnapshot().ActiveTriggerDispatchCount -eq 0) 'SDK触发调用返回后活动触发诊断必须归零。'
$resetWithLateFrameRiskRejected = $false
try { $registry.ResetCamera('CAMERA-8') } catch { $resetWithLateFrameRiskRejected = $true }
Assert-True $resetWithLateFrameRiskRejected 'SDK调用已返回但旧帧仍未排空时，普通重置仍必须拒绝。'
Assert-True ($registry.GetSnapshot().CameraCountRequiringStreamReset -ge 1) '清场已武装票据必须留下相机流重启诊断门禁。'
$sameCameraProbe.Dispose()
Confirm-CameraStreamReset 'CAMERA-8'

$cameraAContext = New-Context '产线I'
$cameraBContext = New-Context '产线I'
$cameraATicket = $registry.Register('CAMERA-9A', $cameraAContext, $softwareTrigger, $deadline, [long]256, [long]4096)
$cameraBTicket = $registry.Register('CAMERA-9B', $cameraBContext, $softwareTrigger, $deadline, [long]256, [long]4096)
$cameraAProbe = [CameraTriggerConcurrencyProbe]::new()
$cameraAIssue = $cameraAProbe.IssueBlockingAsync($cameraATicket)
Assert-True ($cameraAProbe.WaitUntilEntered(2000)) '相机A的慢触发必须进入。'
$cameraBProbe = [CameraTriggerConcurrencyProbe]::new()
$cameraBIssue = $cameraBProbe.IssueImmediateAsync($cameraBTicket)
Assert-True ($cameraBIssue.Wait(1000)) '相机A触发被阻塞时，相机B仍应独立完成触发。'
Assert-True ($cameraBProbe.TriggerCallCount -eq 1) '不同相机的软件触发动作必须真实执行。'
$cameraAProbe.Release()
Assert-True ($cameraAIssue.Wait(2000)) '释放相机A后其触发必须及时完成。'
$cameraATicket.Dispose()
$cameraBTicket.Dispose()
$cameraAProbe.Dispose()
$cameraBProbe.Dispose()
Confirm-CameraStreamReset 'CAMERA-9A'
Confirm-CameraStreamReset 'CAMERA-9B'

$timeoutContext = New-Context '产线B'
$timeoutTicket = $registry.Register(
    'CAMERA-2',
    $timeoutContext,
    $softwareTrigger,
    [DateTime]::UtcNow.AddMilliseconds(20),
    [long]128,
    [long]4096)
$timeoutFollower = $registry.Register(
    'CAMERA-2',
    (New-Context '产线B'),
    $softwareTrigger,
    [DateTime]::UtcNow.AddSeconds(10),
    [long]128,
    [long]4096)
[Threading.Thread]::Sleep(30)
Assert-True ($registry.FailExpired([DateTime]::UtcNow) -eq 2) '任一票据超时后必须清场同相机全部后续票据。'
$timedOut = $false
try {
    $null = $timeoutTicket.Completion.GetAwaiter().GetResult()
}
catch {
    $timedOut = $true
}
Assert-True $timedOut '超时票据必须完成为异常，不能无限等待。'
$timeoutFollowerStopped = $false
try {
    $null = $timeoutFollower.Completion.GetAwaiter().GetResult()
}
catch {
    $timeoutFollowerStopped = $true
}
Assert-True $timeoutFollowerStopped '前序超时后尚未到期的后续工件也必须明确停止等待。'
Assert-True ($budget.GetSnapshot().ActiveLeaseCount -eq 0) '超时票据必须归还触发前预算。'

$stuckTriggerTicket = $registry.Register(
    'CAMERA-STUCK-TRIGGER',
    (New-Context '产线触发卡住'),
    $softwareTrigger,
    [DateTime]::UtcNow.AddSeconds(10),
    [long]256,
    [long]4096)
$stuckTriggerTicket.IssueSoftwareTrigger($noOpTrigger)
$stuckTriggerFrame = New-TestMat
Assert-True ($registry.TryPublishFrame(
    'CAMERA-STUCK-TRIGGER',
    [long]1,
    [uint32]1,
    [Diagnostics.Stopwatch]::GetTimestamp(),
    $stuckTriggerFrame,
    $null)) '模拟SDK命令卡住前，生产帧必须先完成票据。'
$neverCompletedTrigger = [Threading.Tasks.TaskCompletionSource[bool]]::new()
$stuckWaitTask = $waitForProductionFrameMethod.Invoke(
    $null,
    @($registry, $stuckTriggerTicket, $neverCompletedTrigger.Task, [int]20, $none))
$stuckTriggerTimedOut = $false
try { $null = $stuckWaitTask.GetAwaiter().GetResult() } catch { $stuckTriggerTimedOut = $true }
Assert-True $stuckTriggerTimedOut '帧已到但SDK触发命令未返回时，节点共同超时必须失败，不能返回成功图像。'
Assert-True ($budget.GetSnapshot().ActiveLeaseCount -eq 0) 'SDK触发命令卡住超时后必须释放已完成信封的Mat预算。'
$previewAfterFaultRejected = $false
try {
    $unexpectedFaultedPreviewLease = $registry.EnterSoftwareTriggerCommand('CAMERA-STUCK-TRIGGER')
    $unexpectedFaultedPreviewLease.Dispose()
}
catch {
    $previewAfterFaultRejected = $true
}
Assert-True $previewAfterFaultRejected '生产触发故障后，物理停流重启确认前必须同时禁止预览软件触发。'
Confirm-CameraStreamReset 'CAMERA-STUCK-TRIGGER'

$smallBudgetContext = New-Context '产线C'
$budgetRejected = $false
try {
    $registry.Register('CAMERA-3', $smallBudgetContext, $softwareTrigger, $deadline, [long]1024, [long]512)
}
catch {
    $budgetRejected = $true
}
Assert-True $budgetRejected '触发前估算超过预算时必须先拒绝且不得生成票据。'
Assert-True ($budget.GetSnapshot().ActiveLeaseCount -eq 0) '触发前拒绝不能泄漏预算租约。'

Wait-ProductionFaultDrain
$shutdownContext = New-Context '产线K'
$shutdownTicket = $registry.Register(
    'CAMERA-SHUTDOWN',
    $shutdownContext,
    $softwareTrigger,
    $deadline,
    [long]256,
    [long]4096)
Assert-True ($shutdownContext.TryComplete($succeededTerminal)) '关闭测试工件必须先请求成功封口。'
Assert-True (-not $shutdownContext.Completion.IsCompleted) '注册表关闭前票据租约必须继续阻止工件提前封口。'
$registry.Dispose()
$shutdownTicketFaulted = $false
try { $null = $shutdownTicket.Completion.GetAwaiter().GetResult() } catch { $shutdownTicketFaulted = $true }
Assert-True $shutdownTicketFaulted '注册表关闭必须明确失败全部排队票据。'
Assert-True ($shutdownContext.Completion.GetAwaiter().GetResult().ToString() -eq 'Stopped') '注册表关闭必须把排队工件合并为Stopped终态。'
Assert-True ($budget.GetSnapshot().ActiveLeaseCount -eq 0) '注册表关闭必须归还全部票据预算。'
$useAfterDisposeRejected = $false
try {
    $registry.Register('CAMERA-DISPOSED', (New-Context '产线J'), $softwareTrigger, $deadline, [long]256, [long]4096)
}
catch [ObjectDisposedException] {
    $useAfterDisposeRejected = $true
}
Assert-True $useAfterDisposeRejected '注册表释放后必须明确拒绝新的生产操作。'
Write-Host '相机工件管线检查通过：触发前预算、单相机FIFO、工件绑定、实际字节校正、帧号断层、故障门禁、触发取消边界、多相机并行和超时回收均符合预期。'
[AppDomain]::CurrentDomain.remove_AssemblyResolve($assemblyResolveHandler)
