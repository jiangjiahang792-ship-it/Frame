param([string]$BuildDirectory = 'bin\x64\Debug')

$ErrorActionPreference = 'Stop'

# 生产程序基于.NET Framework 4.8，PowerShell 7不直接承载其OpenCvSharp原生依赖。
if ($PSVersionTable.PSEdition -eq 'Core') {
    $windowsPowerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    & $windowsPowerShell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $PSCommandPath -BuildDirectory $BuildDirectory
    exit $LASTEXITCODE
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Get-InnermostException {
    param([Exception]$Exception)
    $current = $Exception
    while ($null -ne $current.InnerException) {
        $current = $current.InnerException
    }
    return $current
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$debugDirectory = if ([IO.Path]::IsPathRooted($BuildDirectory)) { $BuildDirectory } else { Join-Path $projectRoot $BuildDirectory }
$application = Get-Item -LiteralPath (Join-Path $debugDirectory '机器视觉AI检测系统V1.0.exe') -ErrorAction SilentlyContinue
$sources = @(
    Get-Item -LiteralPath (Join-Path $projectRoot 'ResourceManagement\CameraWorkpiecePipeline.cs')
    Get-Item -LiteralPath (Join-Path $projectRoot 'Node\1-Acquisition\ImageSource\NodeImageSource.cs')
)
Assert-True ($null -ne $application) '相机工件管线测试需要最新Debug程序。'
Assert-True ($application.LastWriteTimeUtc -ge ($sources.LastWriteTimeUtc | Measure-Object -Maximum).Maximum) 'Debug程序早于相机工件管线或图像源节点源码，请先重新编译。'

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
$triggerSourceType = $assembly.GetType('TDJS_Vision.Device.Camera.TriggerSource', $true)
$generatorType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceIdentityGenerator', $true)
$nodeImageSourceType = $assembly.GetType('TDJS_Vision.Node._1_Acquisition.ImageSource.NodeImageSource', $true)
$matType = $openCvAssembly.GetType('OpenCvSharp.Mat', $true)
$matScalarType = $openCvAssembly.GetType('OpenCvSharp.Scalar', $true)
$matTypeType = $openCvAssembly.GetType('OpenCvSharp.MatType', $true)

# 直接执行编译后的节点分类逻辑，防止Auto或线路触发在后续修改中走错生产分支。
$classifyTriggerMethod = $nodeImageSourceType.GetMethod(
    'GetProductionCameraTriggerKind',
    [Reflection.BindingFlags]'Static,NonPublic')
$softwareTrigger = [Enum]::Parse($triggerKindType, 'Software')
$hardwareTrigger = [Enum]::Parse($triggerKindType, 'Hardware')
foreach ($softwareSourceName in @('Auto', 'SOFT')) {
    $softwareSource = [Enum]::Parse($triggerSourceType, $softwareSourceName)
    Assert-True ($classifyTriggerMethod.Invoke($null, @($softwareSource)) -eq $softwareTrigger) "触发源$softwareSourceName没有归类为软触发。"
}
foreach ($hardwareSourceName in @('LINE0', 'LINE1', 'LINE2', 'LINE3', 'LINE4')) {
    $hardwareSource = [Enum]::Parse($triggerSourceType, $hardwareSourceName)
    Assert-True ($classifyTriggerMethod.Invoke($null, @($hardwareSource)) -eq $hardwareTrigger) "触发源$hardwareSourceName没有归类为硬触发。"
}

# 用纯C#线程执行并发边界，避免PowerShell后台线程缺少Runspace影响测试结论。
Add-Type -TypeDefinition @'
using System;
using System.Diagnostics;
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

    public static bool RaceCancellationAndPublish(
        object registry,
        object ticket,
        string cameraKey,
        object image,
        CancellationTokenSource cancellation,
        bool favorPublish)
    {
        bool published = false;
        Exception publishFailure = null;
        using (Barrier barrier = new Barrier(2))
        {
            Task publishTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                if (!favorPublish)
                    Thread.Sleep(2);
                try
                {
                    published = (bool)registry.GetType().GetMethod("TryPublishFrame").Invoke(
                        registry,
                        new object[] { cameraKey, 1L, (uint)1, Stopwatch.GetTimestamp(), image, null });
                }
                catch (TargetInvocationException exception)
                {
                    publishFailure = exception.InnerException ?? exception;
                }
                catch (Exception exception)
                {
                    publishFailure = exception;
                }
            });
            Task cancelTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                if (favorPublish)
                    Thread.Sleep(2);
                cancellation.Cancel();
                ((IDisposable)ticket).Dispose();
            });
            Task.WaitAll(publishTask, cancelTask);
        }
        if (!published)
            ((IDisposable)image).Dispose();
        if (publishFailure != null)
            throw publishFailure;
        return published;
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
$throwProductionCameraWaitFailureMethod = $nodeImageSourceType.GetMethod(
    'ThrowProductionCameraWaitFailure',
    [Reflection.BindingFlags]'Static,NonPublic')
$softwareTriggerDispatchStateType = $nodeImageSourceType.GetNestedType(
    'SoftwareTriggerDispatchState',
    [Reflection.BindingFlags]'NonPublic')
$none = [Threading.CancellationToken]::None
$noOpTrigger = [Action] { }

# 线程池排队与SDK命令开始必须原子裁决，超时获胜后排队任务不得再进入真实SDK。
$queuedDispatchState = [Activator]::CreateInstance($softwareTriggerDispatchStateType, $true)
$tryCancelBeforeSdkMethod = $softwareTriggerDispatchStateType.GetMethod('TryCancelBeforeSdkCommand')
$tryBeginSdkMethod = $softwareTriggerDispatchStateType.GetMethod('TryBeginSdkCommand')
Assert-True ([bool]$tryCancelBeforeSdkMethod.Invoke($queuedDispatchState, @())) '线程池尚未进入SDK时，超时必须能够取消真实SDK命令。'
Assert-True (-not [bool]$tryBeginSdkMethod.Invoke($queuedDispatchState, @())) '超时已经获胜后，排队任务仍取得了SDK执行权。'
$startedDispatchState = [Activator]::CreateInstance($softwareTriggerDispatchStateType, $true)
Assert-True ([bool]$tryBeginSdkMethod.Invoke($startedDispatchState, @())) '正常触发任务没有取得SDK执行权。'
Assert-True (-not [bool]$tryCancelBeforeSdkMethod.Invoke($startedDispatchState, @())) 'SDK命令已经开始后，超时不应伪装成触发前安全取消。'

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

# 连续采集不发送软件命令、不缓存空闲帧，并允许两轮节点运行间主动跳帧。
$earlyContinuousTimestamp = [Diagnostics.Stopwatch]::GetTimestamp()
$continuousTicket = $registry.RegisterContinuousWait('CAMERA-CONTINUOUS', (New-Context '连续采集'), [DateTime]::UtcNow.AddSeconds(10), [long]256, [long]4096)
Assert-True ($continuousTicket.TriggerKind.ToString() -eq 'Continuous') '连续采集票据类型错误。'
$continuousCommandRejected = $false
try { $continuousTicket.IssueSoftwareTrigger($noOpTrigger) } catch { $continuousCommandRejected = $true }
Assert-True $continuousCommandRejected '连续采集不得发送软件触发命令。'
$oldContinuousFrame = New-TestMat
Assert-True (-not $registry.TryPublishFrame('CAMERA-CONTINUOUS', [long]1, [uint32]1, $earlyContinuousTimestamp, $oldContinuousFrame, $null)) '连续采集错误接管等待前的旧回调。'
$oldContinuousFrame.Dispose()
Assert-True (-not $continuousTicket.Completion.IsCompleted) '丢弃旧回调不应结束当前等待。'
$continuousFrame = New-TestMat
Assert-True ($registry.TryPublishFrame('CAMERA-CONTINUOUS', [long]2, [uint32]2, [Diagnostics.Stopwatch]::GetTimestamp(), $continuousFrame, $null)) '连续采集未接管新帧。'
$continuousTicket.Completion.GetAwaiter().GetResult().Dispose()
$continuousTicket.Dispose()
Assert-True (-not $registry.ShouldCaptureBufferedHardwareFrame('CAMERA-CONTINUOUS')) '连续采集不得开启硬触发积压队列。'
$idleContinuousFrame = New-TestMat
Assert-True (-not $registry.TryPublishFrame('CAMERA-CONTINUOUS', [long]3, [uint32]3, [Diagnostics.Stopwatch]::GetTimestamp(), $idleContinuousFrame, $null)) '空闲连续帧不应进入缓存。'
$idleContinuousFrame.Dispose()
$continuousTicket2 = $registry.RegisterContinuousWait('CAMERA-CONTINUOUS', (New-Context '连续采集第二轮'), [DateTime]::UtcNow.AddSeconds(10), [long]256, [long]4096)
$continuousFrame2 = New-TestMat
Assert-True ($registry.TryPublishFrame('CAMERA-CONTINUOUS', [long]20, [uint32]20, [Diagnostics.Stopwatch]::GetTimestamp(), $continuousFrame2, $null)) '连续采集应允许跳过空闲帧。'
$continuousTicket2.Completion.GetAwaiter().GetResult().Dispose()
$continuousTicket2.Dispose()
Assert-True ($budget.GetSnapshot().ReservedBytes -eq 0) '连续帧释放后预算必须归零。'
$continuousTimeoutTicket = $registry.RegisterContinuousWait('CAMERA-CONTINUOUS-TIMEOUT', (New-Context '连续采集超时'), [DateTime]::UtcNow.AddSeconds(10), [long]256, [long]4096)
$continuousTimeoutTask = $waitForProductionFrameMethod.Invoke($null, @($registry, $continuousTimeoutTicket, [Threading.Tasks.Task]::CompletedTask, [int]20, $none))
$continuousTimedOut = $false
try { $null = $continuousTimeoutTask.GetAwaiter().GetResult() } catch {
    $continuousTimedOut = (Get-InnermostException $_.Exception) -is [TimeoutException]
}
Assert-True $continuousTimedOut '连续采集没有遵循节点采图超时。'
$continuousTimeoutTicket.Dispose()
Confirm-CameraStreamReset 'CAMERA-CONTINUOUS-TIMEOUT'
$continuousStopTicket = $registry.RegisterContinuousWait('CAMERA-CONTINUOUS-STOP', (New-Context '连续采集停止'), [DateTime]::UtcNow.AddSeconds(10), [long]256, [long]4096)
$continuousCancellation = [Threading.CancellationTokenSource]::new()
$continuousStopTask = $waitForProductionFrameMethod.Invoke($null, @($registry, $continuousStopTicket, [Threading.Tasks.Task]::CompletedTask, [int]5000, $continuousCancellation.Token))
$continuousCancellation.Cancel()
$continuousCancelled = $false
try { $null = $continuousStopTask.GetAwaiter().GetResult() } catch {
    $continuousCancelled = (Get-InnermostException $_.Exception) -is [OperationCanceledException]
}
Assert-True $continuousCancelled '停止未解除连续采集等待。'
$continuousStopTicket.Dispose()
$continuousCancellation.Dispose()
Confirm-CameraStreamReset 'CAMERA-CONTINUOUS-STOP'
Assert-True ($budget.GetSnapshot().ReservedBytes -eq 0) '连续采集超时或停止后预算未释放。'

# 线路硬触发票据必须在一个相机临界区内完成登记与武装，并只等待物理帧。
$hardwareContext = New-Context '产线硬触发'
$hardwareTicket = $registry.RegisterHardwareWait(
    'CAMERA-HARDWARE',
    $hardwareContext,
    $deadline,
    [long]256,
    [long]4096)
Assert-True ($hardwareTicket.TriggerKind -eq $hardwareTrigger) '硬触发登记入口创建了错误的票据类型。'
Assert-True ($null -ne $hardwareTicket.TriggerIssuedAtUtc) '硬触发票据返回前没有完成武装。'
$hardwareSoftwareCommandCount = 0
$hardwareSoftwareCommandRejected = $false
try {
    $hardwareTicket.IssueSoftwareTrigger([Action] { $script:hardwareSoftwareCommandCount++ })
}
catch {
    $hardwareSoftwareCommandRejected = $true
}
Assert-True ($hardwareSoftwareCommandRejected -and $hardwareSoftwareCommandCount -eq 0) '硬触发票据错误调用了软件触发命令。'
$hardwareFrame = New-TestMat
Assert-True ($registry.TryPublishFrame(
    'CAMERA-HARDWARE',
    [long]1,
    [uint32]1,
    [Diagnostics.Stopwatch]::GetTimestamp(),
    $hardwareFrame,
    $null)) '硬触发物理帧没有被已武装票据接管。'
$hardwareEnvelope = $hardwareTicket.Completion.GetAwaiter().GetResult()
Assert-True ($hardwareEnvelope.Identity.Equals($hardwareContext.Identity)) '硬触发物理帧绑定到了错误工件。'
$hardwareEnvelope.Dispose()
Assert-True ($budget.GetSnapshot().ReservedBytes -eq 0) '硬触发帧释放后图像预算没有归零。'

# 完整流程忙碌期间到达的下一硬触发帧必须进入FIFO，后续工件登记后立即继续执行。
$bufferedHardwareFrame = New-TestMat
Assert-True ($registry.ShouldCaptureBufferedHardwareFrame('CAMERA-HARDWARE')) '首个硬触发工件完成后没有保持运行会话级帧接管。'
Assert-True ($registry.TryPublishFrame(
    'CAMERA-HARDWARE',
    [long]2,
    [uint32]2,
    [Diagnostics.Stopwatch]::GetTimestamp(),
    $bufferedHardwareFrame,
    $null)) '算法忙碌期间到达的第二帧没有进入待执行FIFO。'
Assert-True ($registry.GetSnapshot().BufferedHardwareFrameCount -eq 1) '第二帧进入FIFO后的等待数量不正确。'
$bufferedHardwareContext = New-Context '产线硬触发第二工件'
$bufferedHardwareTicket = $registry.RegisterHardwareWait(
    'CAMERA-HARDWARE',
    $bufferedHardwareContext,
    [DateTime]::SpecifyKind([DateTime]::MaxValue, [DateTimeKind]::Utc),
    [long]256,
    [long]4096)
$bufferedHardwareEnvelope = $bufferedHardwareTicket.Completion.GetAwaiter().GetResult()
Assert-True ($bufferedHardwareEnvelope.Identity.Equals($bufferedHardwareContext.Identity)) '缓存的第二帧没有绑定到下一工件。'
Assert-True ($bufferedHardwareEnvelope.SdkFrameNumber -eq 2) '下一工件没有按FIFO取得SDK第二帧。'
Assert-True ($registry.GetSnapshot().BufferedHardwareFrameCount -eq 0) '第二帧交付后待执行FIFO没有归零。'
$bufferedHardwareEnvelope.Dispose()
$bufferedHardwareTicket.Dispose()
Assert-True ($budget.GetSnapshot().ReservedBytes -eq 0) '缓存硬触发帧执行完毕后图像预算没有归零。'

# 硬触发由外部线路独立产生：帧可先进入SDK回调、票据随后建立，转换完成后仍应按连续帧号交给下一工件。
$earlyHardwareCallbackTimestamp = [Diagnostics.Stopwatch]::GetTimestamp()
[Threading.Thread]::Sleep(2)
$earlyHardwareContext = New-Context '产线硬触发回调先到'
$earlyHardwareTicket = $registry.RegisterHardwareWait(
    'CAMERA-HARDWARE',
    $earlyHardwareContext,
    [DateTime]::SpecifyKind([DateTime]::MaxValue, [DateTimeKind]::Utc),
    [long]256,
    [long]4096)
$earlyHardwareFrame = New-TestMat
Assert-True ($registry.TryPublishFrame(
    'CAMERA-HARDWARE',
    [long]3,
    [uint32]3,
    $earlyHardwareCallbackTimestamp,
    $earlyHardwareFrame,
    $null)) '回调先于下一票据的连续硬触发帧没有被生产管线接管。'
$earlyHardwareEnvelope = $earlyHardwareTicket.Completion.GetAwaiter().GetResult()
Assert-True ($earlyHardwareEnvelope.Identity.Equals($earlyHardwareContext.Identity)) '提前进入回调的连续硬触发帧绑定到了错误工件。'
Assert-True ($earlyHardwareEnvelope.SdkFrameNumber -eq 3) '提前进入回调的硬触发帧没有保持SDK帧号顺序。'
Assert-True ($registry.GetSnapshot().CameraCountRequiringStreamReset -eq 0) '合法的硬触发回调与票据竞态错误建立了停流重启门禁。'
$earlyHardwareEnvelope.Dispose()
$earlyHardwareTicket.Dispose()
Assert-True ($budget.GetSnapshot().ReservedBytes -eq 0) '硬触发回调与票据竞态完成后图像预算没有归零。'

$previewDuringHardwareRejected = $false
try {
    $unexpectedHardwarePreviewLease = $registry.EnterSoftwareTriggerCommand('CAMERA-HARDWARE')
    $unexpectedHardwarePreviewLease.Dispose()
}
catch {
    $previewDuringHardwareRejected = $true
}
Assert-True $previewDuringHardwareRejected '硬触发持续接管期间没有拒绝预览或手动软件触发。'
$softwareDuringHardwareRejected = $false
try {
    $unexpectedSoftwareTicket = $registry.Register(
        'CAMERA-HARDWARE',
        (New-Context '产线硬触发误入软件触发'),
        $softwareTrigger,
        $deadline,
        [long]256,
        [long]4096)
    $unexpectedSoftwareTicket.Dispose()
}
catch {
    $softwareDuringHardwareRejected = $true
}
Assert-True $softwareDuringHardwareRejected '硬触发持续接管期间没有拒绝生产软件触发票据。'
Assert-True ($budget.GetSnapshot().ReservedBytes -eq 0) '拒绝硬软触发混流后没有归还临时预算。'

# 线路硬触发不得使用图像源采图超时；超过传入超时后仍等待，物理帧或停止令牌才能结束。
$hardwarePersistentContext = New-Context '产线硬触发常驻等待'
$hardwarePersistentTicket = $registry.RegisterHardwareWait(
    'CAMERA-HARDWARE-PERSISTENT',
    $hardwarePersistentContext,
    [DateTime]::SpecifyKind([DateTime]::MaxValue, [DateTimeKind]::Utc),
    [long]256,
    [long]4096)
$hardwarePersistentWaitTask = $waitForProductionFrameMethod.Invoke(
    $null,
    @($registry, $hardwarePersistentTicket, [Threading.Tasks.Task]::CompletedTask, [int]20, $none))
[Threading.Thread]::Sleep(100)
Assert-True (-not $hardwarePersistentWaitTask.IsCompleted) '硬触发错误使用了图像源采图超时，外部帧尚未到达时不应结束等待。'
$hardwarePersistentFrame = New-TestMat
Assert-True ($registry.TryPublishFrame(
    'CAMERA-HARDWARE-PERSISTENT',
    [long]1,
    [uint32]1,
    [Diagnostics.Stopwatch]::GetTimestamp(),
    $hardwarePersistentFrame,
    $null)) '超过原采图超时后到达的硬触发帧没有被接管。'
$hardwarePersistentEnvelope = $hardwarePersistentWaitTask.GetAwaiter().GetResult()
Assert-True ($hardwarePersistentEnvelope.Identity.Equals($hardwarePersistentContext.Identity)) '常驻等待取得的硬触发帧绑定到了错误工件。'
$hardwarePersistentEnvelope.Dispose()

$hardwareStopContext = New-Context '产线硬触发停止'
$hardwareStopTicket = $registry.RegisterHardwareWait(
    'CAMERA-HARDWARE-STOP',
    $hardwareStopContext,
    [DateTime]::SpecifyKind([DateTime]::MaxValue, [DateTimeKind]::Utc),
    [long]256,
    [long]4096)
$hardwareStopCancellation = [Threading.CancellationTokenSource]::new()
$hardwareStopWaitTask = $waitForProductionFrameMethod.Invoke(
    $null,
    @($registry, $hardwareStopTicket, [Threading.Tasks.Task]::CompletedTask, [int]20, $hardwareStopCancellation.Token))
[Threading.Thread]::Sleep(100)
Assert-True (-not $hardwareStopWaitTask.IsCompleted) '硬触发在停止前不应自行结束等待。'
$hardwareStopCancellation.Cancel()
$hardwareStopCancelled = $false
try { $null = $hardwareStopWaitTask.GetAwaiter().GetResult() } catch {
    $hardwareStopCancelled = (Get-InnermostException $_.Exception) -is [OperationCanceledException]
}
Assert-True $hardwareStopCancelled '点击停止对应的取消令牌没有解除硬触发等待。'
$hardwareStopTicket.Dispose()
$hardwareStopCancellation.Dispose()
Assert-True ($budget.GetSnapshot().ReservedBytes -eq 0) '硬触发停止后没有归还票据图像预算。'

# 高频覆盖物理帧与停止同时发生的竞态，两种先后顺序都必须结束等待并归还预算。
$racePublishedCount = 0
$raceCancelledBeforePublishCount = 0
for ($raceIndex = 1; $raceIndex -le 100; $raceIndex++) {
    $raceCameraKey = "CAMERA-HARDWARE-RACE-$raceIndex"
    $raceTicket = $registry.RegisterHardwareWait(
        $raceCameraKey,
        (New-Context '产线硬触发停止竞态'),
        [DateTime]::SpecifyKind([DateTime]::MaxValue, [DateTimeKind]::Utc),
        [long]256,
        [long]4096)
    $raceCancellation = [Threading.CancellationTokenSource]::new()
    $raceWaitTask = $waitForProductionFrameMethod.Invoke(
        $null,
        @($registry, $raceTicket, [Threading.Tasks.Task]::CompletedTask, [int]20, $raceCancellation.Token))
    $raceFrame = New-TestMat
    $racePublished = [CameraTriggerConcurrencyProbe]::RaceCancellationAndPublish(
        $registry,
        $raceTicket,
        $raceCameraKey,
        $raceFrame,
        $raceCancellation,
        (($raceIndex % 2) -eq 1))
    if ($racePublished) {
        $racePublishedCount++
    }
    else {
        $raceCancelledBeforePublishCount++
    }
    try {
        $raceEnvelope = $raceWaitTask.GetAwaiter().GetResult()
        $raceEnvelope.Dispose()
    }
    catch {
        Assert-True ((Get-InnermostException $_.Exception) -is [OperationCanceledException]) '硬触发停止与到帧竞态产生了非取消异常。'
    }
    $raceTicket.Dispose()
    $raceCancellation.Dispose()
}
Assert-True ($racePublishedCount -gt 0) '硬触发停止与到帧竞态没有实际覆盖物理帧先完成。'
Assert-True ($raceCancelledBeforePublishCount -gt 0) '硬触发停止与到帧竞态没有实际覆盖票据取消先完成。'
Assert-True ($budget.GetSnapshot().ReservedBytes -eq 0) '硬触发停止与到帧100轮竞态后仍有图像预算残留。'

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

# 直接执行节点等待方法，确认软触发成功发出但没有返回帧时仍按配置超时。
$timeoutFaultSnapshotBefore = $registry.GetSnapshot()
$independentCameraTicket = $registry.Register(
    'CAMERA-SOFTWARE-TIMEOUT-INDEPENDENT',
    (New-Context '产线软触发超时隔离'),
    $softwareTrigger,
    [DateTime]::UtcNow.AddSeconds(10),
    [long]256,
    [long]4096)
$independentCameraTicket.IssueSoftwareTrigger($noOpTrigger)
$softwareWaitTimeoutTicket = $registry.Register(
    'CAMERA-SOFTWARE-WAIT-TIMEOUT',
    (New-Context '产线软触发等待超时'),
    $softwareTrigger,
    [DateTime]::UtcNow.AddSeconds(10),
    [long]256,
    [long]4096)
$softwareWaitTimeoutTicket.IssueSoftwareTrigger($noOpTrigger)
$softwareWaitTimeoutTask = $waitForProductionFrameMethod.Invoke(
    $null,
    @($registry, $softwareWaitTimeoutTicket, [Threading.Tasks.Task]::CompletedTask, [int]20, $none))
$softwareWaitTimedOut = $false
try { $null = $softwareWaitTimeoutTask.GetAwaiter().GetResult() } catch {
    $softwareWaitTimedOut = (Get-InnermostException $_.Exception) -is [TimeoutException]
}
Assert-True $softwareWaitTimedOut '软触发成功发出但没有返回帧时，节点没有按配置抛出TimeoutException。'
$timeoutFaultSnapshotAfter = $registry.GetSnapshot()
Assert-True ($timeoutFaultSnapshotAfter.RecoverableProductionFaultCount -eq ($timeoutFaultSnapshotBefore.RecoverableProductionFaultCount + 1)) '普通软件取帧超时必须只累计一条可恢复告警。'
Assert-True ($timeoutFaultSnapshotAfter.StoppingProductionFaultCount -eq $timeoutFaultSnapshotBefore.StoppingProductionFaultCount) '普通软件取帧超时错误升级成了停方案故障。'
Assert-True (-not $independentCameraTicket.Completion.IsCompleted) '一台相机取帧超时不得清场另一台相机的工件票据。'
Assert-True ($budget.GetSnapshot().ActiveLeaseCount -eq 1) '软触发节点等待超时后必须只保留另一台相机的活动预算。'

$lateTimedOutFrame = New-TestMat
$lateTimedOutFrameAccepted = $registry.TryPublishFrame(
    'CAMERA-SOFTWARE-WAIT-TIMEOUT',
    [long]1,
    [uint32]1,
    [Diagnostics.Stopwatch]::GetTimestamp(),
    $lateTimedOutFrame,
    $null)
Assert-True (-not $lateTimedOutFrameAccepted) '超时后的迟到帧不得绑定给下一工件。'
$lateTimedOutFrame.Dispose()

# 可恢复告警即使尚在后台派发，也不得阻止该相机完成物理停流后的两阶段流重置。
$recoverableTimeoutGeneration = $prepareStreamResetMethod.Invoke(
    $registry,
    @('CAMERA-SOFTWARE-WAIT-TIMEOUT'))
$null = $confirmStreamResetMethod.Invoke(
    $registry,
    @('CAMERA-SOFTWARE-WAIT-TIMEOUT', [long]$recoverableTimeoutGeneration))

$recoveredCameraTicket = $registry.Register(
    'CAMERA-SOFTWARE-WAIT-TIMEOUT',
    (New-Context '产线软触发超时恢复'),
    $softwareTrigger,
    [DateTime]::UtcNow.AddSeconds(10),
    [long]256,
    [long]4096)
$recoveredCameraTicket.IssueSoftwareTrigger($noOpTrigger)
$recoveredCameraFrame = New-TestMat
Assert-True ($registry.TryPublishFrame(
    'CAMERA-SOFTWARE-WAIT-TIMEOUT',
    [long]1,
    [uint32]1,
    [Diagnostics.Stopwatch]::GetTimestamp(),
    $recoveredCameraFrame,
    $null)) '超时相机完成清流重启后，下一轮新帧必须能绑定新工件。'
$recoveredEnvelope = $recoveredCameraTicket.Completion.GetAwaiter().GetResult()
$recoveredEnvelope.Dispose()

$independentCameraFrame = New-TestMat
Assert-True ($registry.TryPublishFrame(
    'CAMERA-SOFTWARE-TIMEOUT-INDEPENDENT',
    [long]1,
    [uint32]1,
    [Diagnostics.Stopwatch]::GetTimestamp(),
    $independentCameraFrame,
    $null)) '另一台相机必须在首台相机超时恢复后继续完成自己的原票据。'
$independentCameraEnvelope = $independentCameraTicket.Completion.GetAwaiter().GetResult()
$independentCameraEnvelope.Dispose()
Assert-True ($budget.GetSnapshot().ActiveLeaseCount -eq 0) '超时恢复和独立相机都完成后，图像预算必须全部归零。'
Wait-ProductionFaultDrain

# 超时计时器和成功帧同时完成时，已经由注册表提交成功的帧必须胜出，不能再反向改判超时。
$timeoutTieTicket = $registry.Register(
    'CAMERA-SOFTWARE-TIMEOUT-TIE',
    (New-Context '产线软触发超时同时完成'),
    $softwareTrigger,
    [DateTime]::UtcNow.AddSeconds(10),
    [long]256,
    [long]4096)
$timeoutTieTicket.IssueSoftwareTrigger($noOpTrigger)
$timeoutTieFrame = New-TestMat
Assert-True ($registry.TryPublishFrame(
    'CAMERA-SOFTWARE-TIMEOUT-TIE',
    [long]1,
    [uint32]1,
    [Diagnostics.Stopwatch]::GetTimestamp(),
    $timeoutTieFrame,
    $null)) '超时同时完成测试必须先由注册表成功提交帧。'
$timeoutTieFaultCountBefore = $registry.GetSnapshot().RecoverableProductionFaultCount
$null = $throwProductionCameraWaitFailureMethod.Invoke(
    $null,
    @(
        $registry,
        $timeoutTieTicket,
        [Threading.Tasks.Task]::CompletedTask,
        $timeoutTieTicket.Completion,
        [int]20,
        $none))
Assert-True ($registry.GetSnapshot().RecoverableProductionFaultCount -eq $timeoutTieFaultCountBefore) '已经成功提交的帧被迟到超时反向改判为失败。'
$timeoutTieEnvelope = $timeoutTieTicket.Completion.GetAwaiter().GetResult()
$timeoutTieEnvelope.Dispose()
Assert-True ($budget.GetSnapshot().ActiveLeaseCount -eq 0) '超时同时完成裁决后图像预算没有归零。'

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
try { $null = $stuckWaitTask.GetAwaiter().GetResult() } catch {
    $stuckTriggerException = Get-InnermostException $_.Exception
    $stuckTriggerTimedOut = $stuckTriggerException -is [TimeoutException]
}
Assert-True $stuckTriggerTimedOut '帧已到但SDK触发命令未返回时，节点共同超时必须失败，不能返回成功图像。'
Assert-True ($stuckTriggerException.GetType().Name -eq 'SoftwareTriggerCommandTimeoutException') 'SDK触发命令卡死必须使用专用异常，禁止被外层当普通无帧超时执行并发重启。'
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

# 硬触发待执行FIFO不设拒绝上限；超过原8张边界后必须继续按序接收并累计非停机报警。
$overflowBudget = [TDJS_Vision.ResourceManagement.WorkpieceFrameMemoryBudgetManager]::new()
$overflowRegistry = [TDJS_Vision.ResourceManagement.CameraTriggerTicketRegistry]::new($overflowBudget)
$overflowContext = New-Context '产线硬触发积压'
$overflowTicket = $overflowRegistry.RegisterHardwareWait(
    'CAMERA-HARDWARE-OVERFLOW',
    $overflowContext,
    [DateTime]::SpecifyKind([DateTime]::MaxValue, [DateTimeKind]::Utc),
    [long]256,
    [long]4096)
$overflowFirstFrame = New-TestMat
Assert-True ($overflowRegistry.TryPublishFrame(
    'CAMERA-HARDWARE-OVERFLOW', [long]1, [uint32]1,
    [Diagnostics.Stopwatch]::GetTimestamp(), $overflowFirstFrame, $null)) '满载测试首帧没有完成硬触发模式武装。'
$overflowFirstEnvelope = $overflowTicket.Completion.GetAwaiter().GetResult()
$overflowFirstEnvelope.Dispose()
$overflowTicket.Dispose()
for ($bufferedIndex = 2; $bufferedIndex -le 18; $bufferedIndex++) {
    $queuedFrame = New-TestMat
    Assert-True ($overflowRegistry.TryPublishFrame(
        'CAMERA-HARDWARE-OVERFLOW', [long]$bufferedIndex, [uint32]$bufferedIndex,
        [Diagnostics.Stopwatch]::GetTimestamp(), $queuedFrame, $null)) "第$bufferedIndex 帧没有进入硬触发待执行FIFO。"
}
$overflowSnapshot = $overflowRegistry.GetSnapshot()
Assert-True ($overflowSnapshot.BufferedHardwareFrameCount -eq 17) '硬触发待执行FIFO超过原8张边界后没有继续接收。'
Assert-True ($overflowSnapshot.BufferedHardwareFramePressureWarningCount -eq 2) '积压到8张和16张时没有各记录一次报警。'
Assert-True ($overflowSnapshot.CameraCountRequiringStreamReset -eq 0) '待执行帧积压报警错误建立了物理停流重启门禁。'
Assert-True ($overflowRegistry.ShouldCaptureBufferedHardwareFrame('CAMERA-HARDWARE-OVERFLOW')) '待执行帧积压报警后没有继续接收硬触发帧。'
$overflowNextTicket = $overflowRegistry.RegisterHardwareWait(
    'CAMERA-HARDWARE-OVERFLOW',
    (New-Context '产线硬触发积压后继续'),
    [DateTime]::SpecifyKind([DateTime]::MaxValue, [DateTimeKind]::Utc),
    [long]256,
    [long]4096)
$overflowNextEnvelope = $overflowNextTicket.Completion.GetAwaiter().GetResult()
Assert-True ($overflowNextEnvelope.SdkFrameNumber -eq 2) '超过报警阈值后没有按FIFO交付最早待执行帧。'
$overflowNextEnvelope.Dispose()
$overflowNextTicket.Dispose()
Assert-True ($overflowRegistry.GetSnapshot().BufferedHardwareFrameCount -eq 16) '积压后消费一帧没有正确减少FIFO数量。'
for ($drainIndex = 3; $drainIndex -le 11; $drainIndex++) {
    $drainTicket = $overflowRegistry.RegisterHardwareWait(
        'CAMERA-HARDWARE-OVERFLOW',
        (New-Context "产线硬触发回落$drainIndex"),
        [DateTime]::SpecifyKind([DateTime]::MaxValue, [DateTimeKind]::Utc),
        [long]256,
        [long]4096)
    $drainEnvelope = $drainTicket.Completion.GetAwaiter().GetResult()
    Assert-True ($drainEnvelope.SdkFrameNumber -eq $drainIndex) "积压回落期间没有按FIFO交付SDK第$drainIndex 帧。"
    $drainEnvelope.Dispose()
    $drainTicket.Dispose()
}
Assert-True ($overflowRegistry.GetSnapshot().BufferedHardwareFrameCount -eq 7) '积压回落到报警阈值以下时FIFO数量不正确。'
$warningResetFrame = New-TestMat
Assert-True ($overflowRegistry.TryPublishFrame(
    'CAMERA-HARDWARE-OVERFLOW', [long]19, [uint32]19,
    [Diagnostics.Stopwatch]::GetTimestamp(), $warningResetFrame, $null)) '积压回落后再次到8张时没有继续接收。'
Assert-True ($overflowRegistry.GetSnapshot().BufferedHardwareFramePressureWarningCount -eq 3) '积压回落到8张以下后再次到8张没有重新报警。'
$overflowRegistry.Dispose()
Assert-True ($overflowRegistry.GetSnapshot().BufferedHardwareFrameCount -eq 0) '无上限FIFO释放后没有清空全部缓存Mat。'

# 没有当前票据但硬触发持续接管时，转换失败必须立即故障，不能等到下一帧断层才发现。
$idleFailureBudget = [TDJS_Vision.ResourceManagement.WorkpieceFrameMemoryBudgetManager]::new()
$idleFailureRegistry = [TDJS_Vision.ResourceManagement.CameraTriggerTicketRegistry]::new($idleFailureBudget)
$idleFailureTicket = $idleFailureRegistry.RegisterHardwareWait(
    'CAMERA-HARDWARE-IDLE-FAILURE',
    (New-Context '产线硬触发空窗转换失败'),
    [DateTime]::SpecifyKind([DateTime]::MaxValue, [DateTimeKind]::Utc),
    [long]256,
    [long]4096)
$idleFailureFirstFrame = New-TestMat
Assert-True ($idleFailureRegistry.TryPublishFrame(
    'CAMERA-HARDWARE-IDLE-FAILURE', [long]1, [uint32]1,
    [Diagnostics.Stopwatch]::GetTimestamp(), $idleFailureFirstFrame, $null)) '空窗失败测试首帧没有正常完成。'
$idleFailureEnvelope = $idleFailureTicket.Completion.GetAwaiter().GetResult()
$idleFailureEnvelope.Dispose()
$idleFailureTicket.Dispose()
Assert-True ($idleFailureRegistry.TryFailFrame(
    'CAMERA-HARDWARE-IDLE-FAILURE', [long]2,
    [Diagnostics.Stopwatch]::GetTimestamp(),
    [InvalidOperationException]::new('模拟算法忙碌期间Mat转换失败'))) '无票据期间转换失败没有被持续硬触发管线接管。'
Assert-True ($idleFailureRegistry.GetSnapshot().CameraCountRequiringStreamReset -eq 1) '无票据期间转换失败没有建立停流重启门禁。'
Assert-True (-not $idleFailureRegistry.ShouldCaptureBufferedHardwareFrame('CAMERA-HARDWARE-IDLE-FAILURE')) '无票据转换失败后仍在继续接收硬触发帧。'
$idleFailureRegistry.Dispose()

# 待执行Mat超过参考字节预算后仍必须接收，字节计账仅用于日志追查和资源观测。
$byteBudget = [TDJS_Vision.ResourceManagement.WorkpieceFrameMemoryBudgetManager]::new()
$byteRegistry = [TDJS_Vision.ResourceManagement.CameraTriggerTicketRegistry]::new($byteBudget, [long]500)
foreach ($byteCameraKey in @('CAMERA-BYTE-A', 'CAMERA-BYTE-B')) {
    $byteTicket = $byteRegistry.RegisterHardwareWait(
        $byteCameraKey,
        (New-Context "产线$byteCameraKey"),
        [DateTime]::SpecifyKind([DateTime]::MaxValue, [DateTimeKind]::Utc),
        [long]256,
        [long]4096)
    $byteFirstFrame = New-TestMat
    Assert-True ($byteRegistry.TryPublishFrame(
        $byteCameraKey, [long]1, [uint32]1,
        [Diagnostics.Stopwatch]::GetTimestamp(), $byteFirstFrame, $null)) "相机$byteCameraKey 首帧没有正常完成。"
    $byteEnvelope = $byteTicket.Completion.GetAwaiter().GetResult()
    $byteEnvelope.Dispose()
    $byteTicket.Dispose()
}
$byteQueuedFrame = New-TestMat
Assert-True ($byteRegistry.TryPublishFrame(
    'CAMERA-BYTE-A', [long]2, [uint32]2,
    [Diagnostics.Stopwatch]::GetTimestamp(), $byteQueuedFrame, $null)) '第一台相机没有取得全局待执行Mat预算。'
Assert-True ($byteRegistry.GetSnapshot().BufferedHardwareFrameBytes -eq 300) '第一张待执行Mat实际字节记账不正确。'
$byteOverBudgetFrame = New-TestMat
Assert-True ($byteRegistry.TryPublishFrame(
    'CAMERA-BYTE-B', [long]2, [uint32]2,
    [Diagnostics.Stopwatch]::GetTimestamp(), $byteOverBudgetFrame, $null)) '第二台相机超过参考字节预算后没有继续接收。'
Assert-True ($byteRegistry.GetSnapshot().BufferedHardwareFrameBytes -eq 600) '超过参考字节预算后待执行Mat字节计账不正确。'
Assert-True ($byteRegistry.GetSnapshot().BufferedHardwareFrameBudgetBytes -eq 500) '全局待执行Mat字节预算快照不正确。'
Assert-True ($byteRegistry.GetSnapshot().CameraCountRequiringStreamReset -eq 0) '超过参考字节预算错误建立了物理停流重启门禁。'
$byteRegistry.Dispose()
Assert-True ($byteRegistry.GetSnapshot().BufferedHardwareFrameBytes -eq 0) '注册表释放后全局待执行Mat字节记账没有归零。'

# 最后一个运行会话结束时必须关闭旧注册表入口；已复制但尚未转换的迟到帧不能重新写入旧FIFO。
$stopBudget = [TDJS_Vision.ResourceManagement.WorkpieceFrameMemoryBudgetManager]::new()
$stopRegistry = [TDJS_Vision.ResourceManagement.CameraTriggerTicketRegistry]::new($stopBudget)
$stopTicket = $stopRegistry.RegisterHardwareWait(
    'CAMERA-STOP-RACE',
    (New-Context '产线停止迟到帧'),
    [DateTime]::SpecifyKind([DateTime]::MaxValue, [DateTimeKind]::Utc),
    [long]256,
    [long]4096)
$stopFirstFrame = New-TestMat
Assert-True ($stopRegistry.TryPublishFrame(
    'CAMERA-STOP-RACE', [long]1, [uint32]1,
    [Diagnostics.Stopwatch]::GetTimestamp(), $stopFirstFrame, $null)) '停止竞态测试首帧没有正常完成。'
$stopEnvelope = $stopTicket.Completion.GetAwaiter().GetResult()
$stopEnvelope.Dispose()
$stopTicket.Dispose()
$stopQueuedFrame = New-TestMat
Assert-True ($stopRegistry.TryPublishFrame(
    'CAMERA-STOP-RACE', [long]2, [uint32]2,
    [Diagnostics.Stopwatch]::GetTimestamp(), $stopQueuedFrame, $null)) '停止前到达的第2帧没有进入待执行FIFO。'
Assert-True ($stopRegistry.GetSnapshot().BufferedHardwareFrameCount -eq 1) '停止前待执行FIFO数量不正确。'
$stopAcceptingMethod = $registryType.GetMethod(
    'StopAcceptingProductionFrames',
    [Reflection.BindingFlags]'Instance,NonPublic')
Assert-True ($null -ne $stopAcceptingMethod) '没有找到生产会话停止入口。'
$null = $stopAcceptingMethod.Invoke($stopRegistry, @())
Assert-True ($stopRegistry.GetSnapshot().BufferedHardwareFrameCount -eq 0) '生产会话停止后没有释放待执行Mat。'
Assert-True ($stopRegistry.GetSnapshot().BufferedHardwareFrameBytes -eq 0) '生产会话停止后待执行Mat字节没有归零。'
Assert-True (-not $stopRegistry.ShouldCaptureBufferedHardwareFrame('CAMERA-STOP-RACE')) '生产会话停止后仍允许SDK回调持续接管。'
$previewFailureMethod = $registryType.GetMethod(
    'TryAcknowledgePreviewFrameFailure',
    [Reflection.BindingFlags]'Instance,NonPublic')
Assert-True ($null -ne $previewFailureMethod) '没有找到预览帧失败计数归还入口。'
# 分别模拟原始入口失败和Mat转换失败，两次失败都必须归还预览待返回计数。
foreach ($previewFailureKind in @('原始入口失败', 'Mat转换失败')) {
    $failedPreviewLease = $stopRegistry.EnterSoftwareTriggerCommand('CAMERA-STOP-PREVIEW')
    $failedPreviewLease.MarkCommandStarted()
    $failedPreviewLease.MarkCommandSucceeded()
    $failedPreviewLease.Dispose()
    Assert-True ([bool]$previewFailureMethod.Invoke(
        $stopRegistry,
        @('CAMERA-STOP-PREVIEW'))) "$previewFailureKind 没有归还预览待返回计数。"
}
# 生产门闩关闭后仍允许手动预览归还待返回计数；连续超过64次不能因计数泄漏被拒绝。
for ($previewIndex = 1; $previewIndex -le 100; $previewIndex++) {
    $previewLease = $stopRegistry.EnterSoftwareTriggerCommand('CAMERA-STOP-PREVIEW')
    $previewLease.MarkCommandStarted()
    $previewLease.MarkCommandSucceeded()
    $previewLease.Dispose()
    $previewFrame = New-TestMat
    $previewProductionAccepted = $stopRegistry.TryPublishFrame(
        'CAMERA-STOP-PREVIEW', [long]$previewIndex, [uint32]$previewIndex,
        [Diagnostics.Stopwatch]::GetTimestamp(), $previewFrame, $null)
    if (-not $previewProductionAccepted) { $previewFrame.Dispose() }
    Assert-True (-not $previewProductionAccepted) '停止后的预览帧错误进入了生产管线。'
}
$staleFrame = New-TestMat
$staleAccepted = $stopRegistry.TryPublishFrame(
    'CAMERA-STOP-RACE', [long]3, [uint32]3,
    [Diagnostics.Stopwatch]::GetTimestamp(), $staleFrame, $null)
if (-not $staleAccepted) { $staleFrame.Dispose() }
Assert-True (-not $staleAccepted) '生产会话停止后，旧原始包仍被写入生产FIFO。'
$stopRegisterRejected = $false
try {
    $null = $stopRegistry.RegisterHardwareWait(
        'CAMERA-STOP-RACE',
        (New-Context '产线停止后登记'),
        [DateTime]::SpecifyKind([DateTime]::MaxValue, [DateTimeKind]::Utc),
        [long]256,
        [long]4096)
}
catch {
    $stopRegisterRejected = $true
}
Assert-True $stopRegisterRejected '生产会话停止后仍允许登记新的相机票据。'
$tryRouteFrameMethod = $registryType.GetMethod(
    'TryRouteFrame',
    [Reflection.BindingFlags]'Instance,NonPublic')
Assert-True ($null -ne $tryRouteFrameMethod) '没有找到相机转换线程无异常帧路由入口。'
$stopRegistry.Dispose()
$disposedLateFrame = New-TestMat
$disposedRouteAccepted = [bool]$tryRouteFrameMethod.Invoke(
    $stopRegistry,
    @(
        'CAMERA-STOP-RACE',
        [long]4,
        [uint32]4,
        [Diagnostics.Stopwatch]::GetTimestamp(),
        $disposedLateFrame,
        $null))
if (-not $disposedRouteAccepted) { $disposedLateFrame.Dispose() }
Assert-True (-not $disposedRouteAccepted) '已经释放的旧注册表没有无异常拒绝迟到帧。'

# 单相机断线不关闭生产会话：旧票据失败、旧帧拒绝、其他相机继续收帧，物理重启确认后恢复。
$reconnectBudget = [Activator]::CreateInstance($budgetType)
$reconnectRegistry = [Activator]::CreateInstance($registryType, @($reconnectBudget))
$suspendReconnectMethod = $registryType.GetMethod('SuspendCameraForReconnect', [Reflection.BindingFlags]'Instance,NonPublic')
$failTriggeredMethod = $registryType.GetMethod('FailTriggeredTicket', [Reflection.BindingFlags]'Instance,NonPublic')
$reconnectDeadline = [DateTime]::UtcNow.AddMinutes(1)
try {
    $offlineTicket = $reconnectRegistry.RegisterHardwareWait('CAMERA-OFFLINE', (New-Context '重连旧工件'), $reconnectDeadline, [long]256, [long]4096)
    $onlineTicket = $reconnectRegistry.RegisterHardwareWait('CAMERA-ONLINE', (New-Context '重连其他相机'), $reconnectDeadline, [long]256, [long]4096)
    $null = $suspendReconnectMethod.Invoke($reconnectRegistry, @('CAMERA-OFFLINE'))
    Assert-True $offlineTicket.Completion.IsFaulted '断线时旧工件必须结束，不能继续等待新连接的帧。'
    $offlineTicket.Dispose()
    # 旧票据清场后的取消和SDK迟到异常均不能产生全方案停止请求。
    $null = $failTriggeredMethod.Invoke($reconnectRegistry, @($offlineTicket, [InvalidOperationException]::new('旧SDK调用失败')))
    Assert-True (-not $onlineTicket.Completion.IsCompleted) '断线清场误伤了其他相机的等待。'
    $lateReconnectFrame = New-TestMat
    $lateReconnectAccepted = $reconnectRegistry.TryPublishFrame('CAMERA-OFFLINE', [long]1, [uint32]1, [Diagnostics.Stopwatch]::GetTimestamp(), $lateReconnectFrame, $null)
    if (-not $lateReconnectAccepted) { $lateReconnectFrame.Dispose() }
    Assert-True (-not $lateReconnectAccepted) '断线后的旧帧不应进入生产管线。'
    $reconnectRegisterRejected = $false
    try { $null = $reconnectRegistry.RegisterHardwareWait('CAMERA-OFFLINE', (New-Context '重连尚未完成'), $reconnectDeadline, [long]256, [long]4096) }
    catch { $reconnectRegisterRejected = $true }
    Assert-True $reconnectRegisterRejected '流恢复确认前不得登记新工件。'
    $manualResetRejected = $false
    try { $reconnectRegistry.ResetCamera('CAMERA-OFFLINE') } catch { $manualResetRejected = $true }
    Assert-True $manualResetRejected '断线恢复不能跳过物理停流及重新取流确认。'
    $onlineFrame = New-TestMat
    Assert-True ($reconnectRegistry.TryPublishFrame('CAMERA-ONLINE', [long]1, [uint32]1, [Diagnostics.Stopwatch]::GetTimestamp(), $onlineFrame, $null)) '断线相机不应阻止其他相机正常交付。'
    $onlineTicket.Completion.GetAwaiter().GetResult().Dispose()
    $onlineTicket.Dispose()
    $reconnectGeneration = $prepareStreamResetMethod.Invoke($reconnectRegistry, @('CAMERA-OFFLINE'))
    $null = $suspendReconnectMethod.Invoke($reconnectRegistry, @('CAMERA-OFFLINE'))
    $staleReconnectRejected = $false
    try { $null = $confirmStreamResetMethod.Invoke($reconnectRegistry, @('CAMERA-OFFLINE', [long]$reconnectGeneration)) }
    catch { $staleReconnectRejected = $true }
    Assert-True $staleReconnectRejected '重新发生断线后，不得使用旧代数解除门禁。'
    $reconnectGeneration = $prepareStreamResetMethod.Invoke($reconnectRegistry, @('CAMERA-OFFLINE'))
    $null = $confirmStreamResetMethod.Invoke($reconnectRegistry, @('CAMERA-OFFLINE', [long]$reconnectGeneration))
    $restoredTicket = $reconnectRegistry.RegisterHardwareWait('CAMERA-OFFLINE', (New-Context '重连新工件'), $reconnectDeadline, [long]256, [long]4096)
    $null = $failTriggeredMethod.Invoke($reconnectRegistry, @($offlineTicket, [InvalidOperationException]::new('重连成功后才处理的旧异常')))
    Assert-True (-not $restoredTicket.Completion.IsCompleted) '旧连接的迟到异常误伤了新连接票据。'
    $restoredFrame = New-TestMat
    Assert-True ($reconnectRegistry.TryPublishFrame('CAMERA-OFFLINE', [long]2, [uint32]1, [Diagnostics.Stopwatch]::GetTimestamp(), $restoredFrame, $null)) '重连确认后应允许SDK帧号重新起步并交付新工件。'
    $restoredTicket.Completion.GetAwaiter().GetResult().Dispose()
    $restoredTicket.Dispose()
    # 流程忙碌期间缓存的帧也必须在断线时释放，不能跨连接继续使用。
    $bufferedReconnectFrame = New-TestMat
    Assert-True ($reconnectRegistry.TryPublishFrame('CAMERA-OFFLINE', [long]3, [uint32]2, [Diagnostics.Stopwatch]::GetTimestamp(), $bufferedReconnectFrame, $null)) '测试缓存帧未成功进入FIFO。'
    $null = $suspendReconnectMethod.Invoke($reconnectRegistry, @('CAMERA-OFFLINE'))
    Assert-True ($reconnectRegistry.GetSnapshot().BufferedHardwareFrameCount -eq 0) '断线时没有清空旧连接缓存帧。'
    Assert-True ($reconnectRegistry.GetSnapshot().StoppingProductionFaultCount -eq 0) '单相机重连产生了全方案停止故障。'
    Assert-True ($reconnectBudget.GetSnapshot().ActiveLeaseCount -eq 0) '单相机重连泄漏了工件图像预算。'

    # 真实SDK调用尚未返回时允许隔离，但仍禁止跨越物理重启确认。
    $reconnectProbe = [CameraTriggerConcurrencyProbe]::new()
    try {
        $busyReconnectTicket = $reconnectRegistry.Register('CAMERA-RECONNECT-BUSY', (New-Context '重连在途触发'), $softwareTrigger, $reconnectDeadline, [long]256, [long]4096)
        $busyReconnectTask = $reconnectProbe.IssueBlockingAsync($busyReconnectTicket)
        Assert-True ($reconnectProbe.WaitUntilEntered(2000)) '重连并发测试SDK调用没有开始。'
        $null = $suspendReconnectMethod.Invoke($reconnectRegistry, @('CAMERA-RECONNECT-BUSY'))
        Assert-True $busyReconnectTicket.Completion.IsFaulted 'SDK在途期间断线未结束旧票据。'
        $busyReconnectResetRejected = $false
        try { $null = $prepareStreamResetMethod.Invoke($reconnectRegistry, @('CAMERA-RECONNECT-BUSY')) }
        catch { $busyReconnectResetRejected = $true }
        Assert-True $busyReconnectResetRejected '旧SDK调用仍在执行时不得重新取流确认。'
        $reconnectProbe.Release()
        $busyReconnectTask.GetAwaiter().GetResult()
        $busyReconnectTicket.Dispose()
        $busyReconnectGeneration = $prepareStreamResetMethod.Invoke($reconnectRegistry, @('CAMERA-RECONNECT-BUSY'))
        $null = $confirmStreamResetMethod.Invoke($reconnectRegistry, @('CAMERA-RECONNECT-BUSY', [long]$busyReconnectGeneration))
        Assert-True ($reconnectRegistry.GetSnapshot().StoppingProductionFaultCount -eq 0) '重连SDK在途边界不应生成全方案停机故障。'
        Assert-True ($reconnectBudget.GetSnapshot().ActiveLeaseCount -eq 0) 'SDK在途断线清场泄漏图像预算。'
    }
    finally { $reconnectProbe.Release(); $reconnectProbe.Dispose() }
}
finally { $reconnectRegistry.Dispose() }

# 低内存档案必须严格使用流程图像预算的25%，不能用固定32MB下限突破总预算。
$solutionType = $assembly.GetType('TDJS_Vision.Solution', $true)
$bufferBudgetMethod = $solutionType.GetMethod(
    'GetBufferedHardwareFrameBudgetBytes',
    [Reflection.BindingFlags]'Static,NonPublic')
Assert-True ($null -ne $bufferBudgetMethod) '没有找到硬触发FIFO动态字节预算计算方法。'
$profileType = $bufferBudgetMethod.GetParameters()[0].ParameterType
$lowMemoryProfile = [Activator]::CreateInstance($profileType)
$profileType.GetProperty('FlowImageMemoryBudgetMb').SetValue($lowMemoryProfile, [int]64)
$lowMemoryBufferedBudget = [long]$bufferBudgetMethod.Invoke($null, @($lowMemoryProfile))
Assert-True ($lowMemoryBufferedBudget -eq 16L * 1024L * 1024L) '64MB流程图像预算没有严格分配16MB给硬触发FIFO。'

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
