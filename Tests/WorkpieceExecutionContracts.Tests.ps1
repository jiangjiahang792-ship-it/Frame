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

$epoch = [Guid]::NewGuid()
$identity1 = [Activator]::CreateInstance($identityType, @($epoch, [long]1))
$identity2 = [Activator]::CreateInstance($identityType, @($epoch, [long]2))
Assert-True ($identity1.CompareTo($identity2) -lt 0) '同一生产会话内的工件序号必须保持先后顺序。'
Assert-True ($identity1.ToString().EndsWith('/1')) '工件身份日志文本必须包含工件序号。'

$crossSessionRejected = $false
try {
    $otherIdentity = [Activator]::CreateInstance($identityType, @([Guid]::NewGuid(), [long]1))
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
$releaseMethod = $contextType.GetMethod(
    'ReleaseSignalIntent',
    [Reflection.BindingFlags]'Instance,NonPublic')
Assert-True ($null -ne $reserveMethod -and $null -ne $releaseMethod) '测试需要找到框架内部的意图预约与释放协议。'

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
Assert-True ($context.TryComplete($succeeded)) '活动工件第一次请求封口必须成功。'
Assert-True $context.IsCompletionRequested '封口请求必须立即禁止产生新的信号意图。'
Assert-True ($context.TerminalState.ToString() -eq 'Active') '存在在途信号时不能提前发布终态并放行下一工件。'
Assert-True (-not $context.Completion.IsCompleted) '存在在途信号时异步完成任务不能提前结束。'
Assert-True (-not $context.TryComplete($faulted)) '工件封口请求必须唯一，重复请求不得覆盖第一次结果。'

foreach ($intent in $intents) {
    $releaseMethod.Invoke($context, @($intent.IntentId))
}
$completedState = $context.Completion.GetAwaiter().GetResult()
Assert-True ($completedState.ToString() -eq 'Succeeded') '最后一个在途信号结束后必须发布第一次请求的终态。'
Assert-True ($context.TerminalState.ToString() -eq 'Succeeded') '重复封口请求后必须保留第一个目标终态。'

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

Assert-True ($intentType.GetConstructors().Count -eq 0) '外部调用方不能伪造信号序号或直接构造信号意图。'
Assert-True ($null -eq $intentType.GetProperty('SendAsync')) '信号意图不能公开发送委托绕过协调器。'
Assert-True ($null -eq $intentType.GetMethod('ExecuteAsync')) '信号意图不能公开设备动作执行入口。'
$sampleIntent = $intents[0]
Assert-True ($sampleIntent.Context.Identity.Equals($identity1)) '信号意图必须固定关联预约时的工件身份。'
Assert-True ($sampleIntent.NodePath -like '主流程/PLC写入*') '信号意图必须冻结节点路径元数据。'

$whitelist = @([Enum]::GetNames($nodeKindType))
Assert-True ($whitelist.Count -eq 4) '有序外发白名单只能包含当前确认的四类节点。'
Assert-True (($whitelist -join ',') -eq 'ComSend,CameraIO,PLCWrite,ModbusWrite') '有序外发白名单类别与设计不一致。'

Write-Host '工件执行契约检查通过：身份隔离、原子意图预约、在途封口、不可伪造序号、协调器强制入口、确认等级和四类白名单均符合预期。'
