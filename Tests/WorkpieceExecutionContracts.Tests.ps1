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
    @($identity1, [long]7, '产线A', '软件触发', [DateTime]::UtcNow, [Threading.CancellationToken]::None))

Add-Type -TypeDefinition @'
using System;
using System.Reflection;
using System.Threading.Tasks;

public static class WorkpieceExecutionConcurrencyProbe
{
    public static long[] AllocateSignalSequences(object context, int count)
    {
        MethodInfo allocateMethod = context.GetType().GetMethod("AllocateSignalSequence");
        long[] values = new long[count];
        Parallel.For(0, count, index =>
        {
            values[index] = (long)allocateMethod.Invoke(context, null);
        });
        return values;
    }
}
'@

$sequences = @([WorkpieceExecutionConcurrencyProbe]::AllocateSignalSequences($context, 2000) | Sort-Object)
Assert-True ($sequences.Count -eq 2000) '并发信号序号测试必须完成全部任务。'
Assert-True (($sequences | Select-Object -Unique).Count -eq 2000) '同一工件的并发信号序号不能重复。'
Assert-True ($sequences[0] -eq 1 -and $sequences[-1] -eq 2000) '同一工件的信号序号必须从1连续递增。'

$succeeded = [Enum]::Parse($terminalType, 'Succeeded')
$faulted = [Enum]::Parse($terminalType, 'Faulted')
Assert-True ($context.TryComplete($succeeded)) '活动工件第一次设置终态必须成功。'
Assert-True (-not $context.TryComplete($faulted)) '工件终态必须唯一，重复封口不得覆盖第一次结果。'
Assert-True ($context.TerminalState.ToString() -eq 'Succeeded') '重复封口后必须保留第一个终态。'

$postCompleteRejected = $false
try {
    $context.AllocateSignalSequence()
}
catch [System.Management.Automation.MethodInvocationException] {
    $postCompleteRejected = $_.Exception.InnerException -is [InvalidOperationException]
}
Assert-True $postCompleteRejected '工件封口后不能继续分配信号序号。'

$context2 = [Activator]::CreateInstance(
    $contextType,
    @($identity2, [long]7, ' 产线A ', ' 软件触发 ', [DateTime]::UtcNow, [Threading.CancellationToken]::None))
$sendDelegate = [Func[Threading.CancellationToken, Threading.Tasks.Task[TDJS_Vision.ResourceManagement.OrderedSignalSendResult]]] {
    param($cancellationToken)
    return [Threading.Tasks.Task]::FromResult(
        [TDJS_Vision.ResourceManagement.OrderedSignalSendResult]::new(
            [TDJS_Vision.ResourceManagement.OrderedSignalSendStatus]::Confirmed,
            '已确认'))
}
$intent = [Activator]::CreateInstance(
    $intentType,
    @($context2, [long]1, '主流程/PLC写入1', [Enum]::Parse($nodeKindType, 'PLCWrite'), 'PLC:1', $sendDelegate))
$sendResult = $intent.SendAsync.Invoke([Threading.CancellationToken]::None).GetAwaiter().GetResult()
Assert-True ($intent.Context.Identity.Equals($identity2)) '信号意图必须固定关联创建时的工件身份。'
Assert-True ($intent.NodePath -eq '主流程/PLC写入1' -and $intent.EndpointKey -eq 'PLC:1') '信号意图必须冻结节点路径和设备键。'
Assert-True ($sendResult.Status.ToString() -eq 'Confirmed') '冻结后的发送动作必须返回明确确认结果。'

$whitelist = @([Enum]::GetNames($nodeKindType))
Assert-True ($whitelist.Count -eq 4) '有序外发白名单只能包含当前确认的四类节点。'
Assert-True (($whitelist -join ',') -eq 'ComSend,CameraIO,PLCWrite,ModbusWrite') '有序外发白名单类别与设计不一致。'

Write-Host '工件执行契约检查通过：身份隔离、并发序号、唯一终态、封口保护、意图冻结和四类白名单均符合预期。'
