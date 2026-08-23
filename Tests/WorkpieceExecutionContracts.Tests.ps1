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
$source = Get-Item -LiteralPath (Join-Path $projectRoot 'ResourceManagement\WorkpieceExecutionContracts.cs')
Assert-True ($null -ne $application) '工件执行契约测试需要最新Debug程序。'
Assert-True ($application.LastWriteTimeUtc -ge $source.LastWriteTimeUtc) 'Debug程序早于工件执行契约源码，请先重新编译。'

[Environment]::CurrentDirectory = $debugDirectory
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$identityType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceIdentity', $true)
$contextType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceExecutionContext', $true)
$terminalType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceTerminalState', $true)
$intentType = $assembly.GetType('TDJS_Vision.ResourceManagement.OrderedSignalIntent', $true)
$nodeKindType = $assembly.GetType('TDJS_Vision.ResourceManagement.OrderedSignalNodeKind', $true)
$sendResultType = $assembly.GetType('TDJS_Vision.ResourceManagement.OrderedSignalSendResult', $true)
$sendStatusType = $assembly.GetType('TDJS_Vision.ResourceManagement.OrderedSignalSendStatus', $true)
$coordinatorType = $assembly.GetType('TDJS_Vision.ResourceManagement.IOrderedSignalCoordinator', $true)

Add-Type -TypeDefinition @'
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

public class WorkpieceExecutionCoordinatorProbe : DispatchProxy
{
    public object Result { get; set; }

    public Exception Exception { get; set; }

    public bool ReturnNull { get; set; }

    protected override object Invoke(MethodInfo targetMethod, object[] args)
    {
        if (targetMethod.Name == "RegisterWorkpiece")
            return null;

        if (targetMethod.Name == "CompleteWorkpieceAsync")
        {
            object context = args[0];
            context.GetType().GetMethod("TryComplete").Invoke(context, new[] { args[1] });
            return context.GetType().GetProperty("Completion").GetValue(context, null);
        }

        if (targetMethod.Name != "SubmitAsync")
            throw new NotSupportedException(targetMethod.Name);

        Type resultType = targetMethod.ReturnType.GetGenericArguments()[0];
        if (Exception != null)
        {
            MethodInfo fromException = FindGenericTaskFactory("FromException");
            return fromException.MakeGenericMethod(resultType).Invoke(null, new object[] { Exception });
        }

        MethodInfo fromResult = FindGenericTaskFactory("FromResult");
        return fromResult.MakeGenericMethod(resultType).Invoke(
            null,
            new[] { ReturnNull ? null : Result });
    }

    private static MethodInfo FindGenericTaskFactory(string methodName)
    {
        foreach (MethodInfo method in typeof(Task).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.Name == methodName &&
                method.IsGenericMethodDefinition &&
                method.GetParameters().Length == 1)
            {
                return method;
            }
        }

        throw new MissingMethodException(typeof(Task).FullName, methodName);
    }
}
'@

function New-CoordinatorProbe {
    param([object]$Result, [Exception]$Exception, [bool]$ReturnNull)
    $proxy = [System.Reflection.DispatchProxy]::Create(
        $coordinatorType,
        [WorkpieceExecutionCoordinatorProbe])
    $proxy.Result = $Result
    $proxy.Exception = $Exception
    $proxy.ReturnNull = $ReturnNull
    return $proxy
}

$epoch = [Guid]::NewGuid()
$identity1 = [Activator]::CreateInstance($identityType, @($epoch, 'LINE-A', [long]1))
$identity2 = [Activator]::CreateInstance($identityType, @($epoch, 'line-a', [long]2))
Assert-True ($identity1.CompareTo($identity2) -lt 0) '同一生产会话内的工件序号必须保持先后顺序。'
Assert-True ($identity1.ToString().EndsWith('/1')) '工件身份日志文本必须包含工件序号。'

$crossSessionRejected = $false
try {
    $otherIdentity = [Activator]::CreateInstance($identityType, @([Guid]::NewGuid(), 'LINE-A', [long]1))
    $identity1.CompareTo($otherIdentity)
}
catch [System.Management.Automation.MethodInvocationException] {
    $crossSessionRejected = $_.Exception.InnerException -is [InvalidOperationException]
}
Assert-True $crossSessionRejected '不同生产会话的工件不能直接比较先后顺序。'

$context = [Activator]::CreateInstance(
    $contextType,
    @($identity1, [long]7, 'line-a', '软件触发', [DateTime]::UtcNow, [Threading.CancellationToken]::None))
Assert-True ($context.OrderDomain -eq 'LINE-A') '生产顺序域必须规范为大小写不敏感的稳定键。'

$acknowledged = [Enum]::Parse($sendStatusType, 'DeviceAcknowledged')
$localCompleted = [Enum]::Parse($sendStatusType, 'LocalCallCompleted')
$missingEvidenceRejected = $false
try {
    [Activator]::CreateInstance($sendResultType, @($acknowledged, '已确认', $null))
}
catch {
    $currentException = $_.Exception
    while ($null -ne $currentException.InnerException) {
        $currentException = $currentException.InnerException
    }
    $missingEvidenceRejected = $currentException -is [ArgumentException]
}
Assert-True $missingEvidenceRejected '设备确认状态必须携带可记录的协议确认凭据。'

$localResult = [Activator]::CreateInstance($sendResultType, @($localCompleted, '本地调用完成', $null))
$sendDelegate = [Func[Threading.Tasks.Task[TDJS_Vision.ResourceManagement.OrderedSignalSendResult]]] {
    return [Threading.Tasks.Task]::FromResult($localResult)
}
$reserveMethod = $contextType.GetMethod(
    'ReserveSignalIntent',
    [Reflection.BindingFlags]'Instance,NonPublic')
$finalizeMethod = $contextType.GetMethod(
    'FinalizeSignalIntent',
    [Reflection.BindingFlags]'Instance,NonPublic')
Assert-True ($null -ne $reserveMethod -and $null -ne $finalizeMethod) '测试需要找到框架内部的意图预约与终结协议。'

Add-Type -TypeDefinition @'
using System;
using System.Reflection;
using System.Threading.Tasks;

public static class WorkpieceExecutionConcurrencyProbe
{
    public static object[] ReserveSignalIntents(
        object context,
        MethodInfo reserveMethod,
        int count,
        object signalKind,
        object sendDelegate)
    {
        object[] values = new object[count];
        Parallel.For(0, count, index =>
        {
            values[index] = reserveMethod.Invoke(context, new object[]
            {
                "主流程/PLC写入" + index,
                signalKind,
                "plc:line-a",
                sendDelegate
            });
        });
        return values;
    }
}
'@

$plcWrite = [Enum]::Parse($nodeKindType, 'PLCWrite')
$intents = @([WorkpieceExecutionConcurrencyProbe]::ReserveSignalIntents(
    $context,
    $reserveMethod,
    2000,
    $plcWrite,
    $sendDelegate))
$sequences = @($intents | ForEach-Object { $_.SignalSequence } | Sort-Object)
Assert-True ($sequences.Count -eq 2000) '并发信号序号测试必须完成全部任务。'
Assert-True (($sequences | Select-Object -Unique).Count -eq 2000) '同一工件的并发信号序号不能重复。'
Assert-True ($sequences[0] -eq 1 -and $sequences[-1] -eq 2000) '同一工件的信号序号必须从1连续递增。'
Assert-True (($intents | ForEach-Object { $_.IntentId } | Select-Object -Unique).Count -eq 2000) '并发预约的信号意图ID不能重复。'
Assert-True (($intents | Where-Object { $_.EndpointKey -ne 'PLC:LINE-A' }).Count -eq 0) '同一物理设备键必须规范为稳定的大小写不敏感值。'

$succeeded = [Enum]::Parse($terminalType, 'Succeeded')
$faulted = [Enum]::Parse($terminalType, 'Faulted')
$rejected = [Enum]::Parse($sendStatusType, 'Rejected')
$unknown = [Enum]::Parse($sendStatusType, 'Unknown')
Assert-True ($context.TryComplete($succeeded)) '活动工件第一次请求封口必须成功。'
Assert-True $context.IsCompletionRequested '封口请求必须立即禁止产生新的信号意图。'
Assert-True ($context.TerminalState.ToString() -eq 'Active') '存在在途信号时不能提前发布终态并放行下一工件。'
Assert-True (-not $context.Completion.IsCompleted) '存在在途信号时异步完成任务不能提前结束。'
Assert-True (-not $context.TryComplete($faulted)) '工件封口请求必须唯一，重复请求不得覆盖第一次结果。'

for ($index = 0; $index -lt ($intents.Count - 1); $index++) {
    $finalizeMethod.Invoke($context, @($intents[$index].IntentId, $localCompleted))
}
$finalizeMethod.Invoke($context, @($intents[0].IntentId, $rejected))
Assert-True ($context.TerminalState.ToString() -eq 'Active') '重复终结同一意图必须幂等，不能提前减少在途数量或覆盖首次结果。'
$finalizeMethod.Invoke($context, @($intents[-1].IntentId, $rejected))
$completedState = $context.Completion.GetAwaiter().GetResult()
Assert-True ($completedState.ToString() -eq 'Faulted') '最后一个信号被明确拒绝时必须把成功请求升级为故障终态。'
Assert-True ($context.TerminalState.ToString() -eq 'Faulted') '信号发送失败不能被流程成功结果覆盖。'

$postCompleteRejected = $false
try {
    $reserveMethod.Invoke($context, @('主流程/迟到信号', $plcWrite, 'PLC:LINE-A', $sendDelegate))
}
catch {
    $currentException = $_.Exception
    while ($null -ne $currentException.InnerException) {
        $currentException = $currentException.InnerException
    }
    $postCompleteRejected = $currentException -is [InvalidOperationException]
}
Assert-True $postCompleteRejected '工件请求封口后不能继续预约信号意图。'

$successContext = [Activator]::CreateInstance(
    $contextType,
    @($identity2, [long]7, 'line-a', '软件触发', [DateTime]::UtcNow, [Threading.CancellationToken]::None))
Assert-True ($successContext.TryComplete($succeeded)) '没有外发信号的工件应能直接请求成功封口。'
Assert-True ($successContext.Completion.GetAwaiter().GetResult().ToString() -eq 'Succeeded') '没有信号故障时必须保留流程请求的成功终态。'

$identity3 = [Activator]::CreateInstance($identityType, @($epoch, 'LINE-A', [long]3))
$unknownContext = [Activator]::CreateInstance(
    $contextType,
    @($identity3, [long]7, 'line-a', '软件触发', [DateTime]::UtcNow, [Threading.CancellationToken]::None))
$nullCoordinator = New-CoordinatorProbe -Result $null -Exception $null -ReturnNull $true
$nullTask = $unknownContext.SubmitOrderedSignalAsync(
    $nullCoordinator,
    '主流程/空结果信号',
    $plcWrite,
    'PLC:LINE-A',
    $sendDelegate,
    [Threading.CancellationToken]::None)
Assert-True ($unknownContext.TryComplete($succeeded)) '空结果测试必须先建立成功封口请求。'
$nullResult = $nullTask.GetAwaiter().GetResult()
Assert-True ($nullResult.Status.ToString() -eq 'Unknown') '协调器空结果必须转成明确的未知状态。'
Assert-True ($unknownContext.Completion.GetAwaiter().GetResult().ToString() -eq 'Stopped') '发送结果未知必须升级为停机终态，禁止后续工件越过。'

$identity4 = [Activator]::CreateInstance($identityType, @($epoch, 'LINE-A', [long]4))
$rejectedContext = [Activator]::CreateInstance(
    $contextType,
    @($identity4, [long]7, 'line-a', '软件触发', [DateTime]::UtcNow, [Threading.CancellationToken]::None))
$rejectedResult = [Activator]::CreateInstance($sendResultType, @($rejected, '设备拒绝', $null))
$rejectedCoordinator = New-CoordinatorProbe -Result $rejectedResult -Exception $null -ReturnNull $false
$rejectedTask = $rejectedContext.SubmitOrderedSignalAsync(
    $rejectedCoordinator,
    '主流程/拒绝信号',
    $plcWrite,
    'PLC:LINE-A',
    $sendDelegate,
    [Threading.CancellationToken]::None)
Assert-True ($rejectedContext.TryComplete($succeeded)) '拒绝结果测试必须先建立成功封口请求。'
$null = $rejectedTask.GetAwaiter().GetResult()
Assert-True ($rejectedContext.Completion.GetAwaiter().GetResult().ToString() -eq 'Faulted') '公开提交入口收到明确拒绝时必须升级为故障终态。'

$identity5 = [Activator]::CreateInstance($identityType, @($epoch, 'LINE-A', [long]5))
$cancelContext = [Activator]::CreateInstance(
    $contextType,
    @($identity5, [long]7, 'line-a', '软件触发', [DateTime]::UtcNow, [Threading.CancellationToken]::None))
$cancelCoordinator = New-CoordinatorProbe `
    -Result $null `
    -Exception ([OperationCanceledException]::new('模拟设备适配器错误抛出取消异常')) `
    -ReturnNull $false
$cancelTask = $cancelContext.SubmitOrderedSignalAsync(
    $cancelCoordinator,
    '主流程/取消异常信号',
    $plcWrite,
    'PLC:LINE-A',
    $sendDelegate,
    [Threading.CancellationToken]::None)
Assert-True ($cancelContext.TryComplete($succeeded)) '取消异常测试必须先建立成功封口请求。'
try {
    $null = $cancelTask.GetAwaiter().GetResult()
    throw '协调器取消异常必须继续传播给节点调用方。'
}
catch [OperationCanceledException] {
}
Assert-True ($cancelContext.Completion.GetAwaiter().GetResult().ToString() -eq 'Stopped') '无法证明发送前取消的异常必须按未知结果停机。'

$identity6 = [Activator]::CreateInstance($identityType, @($epoch, 'LINE-A', [long]6))
$faultContext = [Activator]::CreateInstance(
    $contextType,
    @($identity6, [long]7, 'line-a', '软件触发', [DateTime]::UtcNow, [Threading.CancellationToken]::None))
$faultCoordinator = New-CoordinatorProbe `
    -Result $null `
    -Exception ([InvalidOperationException]::new('模拟协调器异常')) `
    -ReturnNull $false
$faultTask = $faultContext.SubmitOrderedSignalAsync(
    $faultCoordinator,
    '主流程/异常信号',
    $plcWrite,
    'PLC:LINE-A',
    $sendDelegate,
    [Threading.CancellationToken]::None)
Assert-True ($faultContext.TryComplete($succeeded)) '协调器异常测试必须先建立成功封口请求。'
try {
    $null = $faultTask.GetAwaiter().GetResult()
    throw '协调器普通异常必须继续传播给节点调用方。'
}
catch [InvalidOperationException] {
}
Assert-True ($faultContext.Completion.GetAwaiter().GetResult().ToString() -eq 'Stopped') '协调器异常必须按未知结果停机。'

Assert-True ($intentType.GetConstructors().Count -eq 0) '外部调用方不能伪造信号序号或直接构造信号意图。'
Assert-True ($null -eq $intentType.GetProperty('SendAsync')) '信号意图不能公开发送委托绕过协调器。'
Assert-True ($null -eq $intentType.GetMethod('ExecuteAsync')) '信号意图不能公开设备动作执行入口。'
$endpointLeaseType = $assembly.GetType('TDJS_Vision.ResourceManagement.ISignalEndpointLease', $true)
Assert-True ($endpointLeaseType.GetProperty('EndpointKey').PropertyType -eq [string]) '端点租约必须公开所属设备键供释放校验。'
Assert-True ($endpointLeaseType.GetProperty('IsReleased').PropertyType -eq [bool]) '端点租约必须公开幂等释放状态。'
$sampleIntent = $intents[0]
Assert-True ($sampleIntent.Context.Identity.Equals($identity1)) '信号意图必须固定关联预约时的工件身份。'
Assert-True ($sampleIntent.NodePath -like '主流程/PLC写入*') '信号意图必须冻结节点路径元数据。'

$whitelist = @([Enum]::GetNames($nodeKindType))
Assert-True ($whitelist.Count -eq 4) '有序外发白名单只能包含当前确认的四类节点。'
Assert-True (($whitelist -join ',') -eq 'ComSend,CameraIO,PLCWrite,ModbusWrite') '有序外发白名单类别与设计不一致。'

Write-Host '工件执行契约检查通过：原子意图预约、公开提交入口、结果合并封口、拒绝转故障、空结果与异常转停机、幂等终结和端点租约均符合预期。'
