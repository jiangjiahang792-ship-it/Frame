$ErrorActionPreference = 'Stop'

function Get-ProjectSource {
    param([string]$RelativePath)

    $sourcePath = Join-Path $PSScriptRoot "..\$RelativePath"
    return Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8
}

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if (-not $Text.Contains($Pattern)) {
        throw $Message
    }
}

$logHelper = Get-ProjectSource 'Forms\Logger\LogHelper.cs'
$diagnostics = Get-ProjectSource 'Diagnostics\PerformanceSpikeDiagnostics.cs'
$trace = Get-ProjectSource 'Diagnostics\ProcessPerformanceTrace.cs'
$process = Get-ProjectSource 'Process.cs'
$nodeBase = Get-ProjectSource 'Node\NodeBase.cs'
$project = Get-ProjectSource 'TDJS-Vision.csproj'

Assert-Contains $logHelper 'private const int DiagnosticPendingLogLimit = 4096;' 'Debug性能诊断必须有明确的待写队列容量上限。'
Assert-Contains $logHelper 'public static bool TryAddDiagnosticLog(' '日志框架必须提供不阻塞检测流程的有界Debug诊断入口。'
Assert-Contains $logHelper '_logQueue.Count >= DiagnosticPendingLogLimit' 'Debug诊断入队前必须检查待写日志水位。'
Assert-Contains $logHelper 'Interlocked.Increment(ref _droppedDiagnosticLogCount)' '被保护策略丢弃的Debug诊断必须计数。'
Assert-Contains $diagnostics 'LogHelper.TryAddDiagnosticLog(message, isDisplay);' '公共性能诊断必须通过有界Debug入口写入。'

Assert-Contains $trace 'CreateIfEnabled' '流程追踪必须提供Debug开关控制的创建入口。'
Assert-Contains $trace 'PerformanceSpikeDiagnostics.IsDiagnosticLogEnabled(MsgLevel.Debug)' 'Debug关闭时不得创建完整流程追踪。'
Assert-Contains $trace '【完整耗时】TraceId=' '完整耗时日志必须包含跨线程TraceId。'
Assert-Contains $trace '阶段=流程结束' '完整耗时必须输出流程结束阶段。'
Assert-Contains $trace '未闭合节点=' '流程结束时必须暴露未闭合节点数量。'
Assert-Contains $trace '"通信节点开始"' '通信节点必须使用独立的开始阶段标记。'
Assert-Contains $trace '"通信节点结束"' '通信节点必须使用独立的结束阶段标记。'
Assert-Contains $trace '"通信节点异常"' '通信节点必须使用独立的异常阶段标记。'
Assert-Contains $trace '通信实际耗时=' '通信节点结束必须记录实际墙钟耗时。'
Assert-Contains $trace '开始线程=' '通信节点必须记录异步调用开始线程。'
Assert-Contains $trace '结束线程=' '通信节点必须记录异步调用结束线程。'

Assert-Contains $process 'ProcessPerformanceTrace.CreateIfEnabled(' '每轮流程开始必须按Debug开关创建追踪。'
Assert-Contains $process 'node.Process?.TraceNodeStarted(node);' '所有公共节点运行入口必须记录节点开始。'
Assert-Contains $process 'process.CompletePerformanceTrace(statusText);' '流程提前结束、失败和取消必须完成追踪。'
Assert-Contains $process 'CompletePerformanceTrace(updateSucceeded ? "成功" : "失败");' '调参图像刷新流程也必须完成追踪。'
Assert-Contains $nodeBase 'Process?.TraceNodeCompleted(this, status, elapsedMilliseconds);' '节点统一结果入口必须记录节点结束。'
Assert-Contains $project '<Compile Include="Diagnostics\ProcessPerformanceTrace.cs" />' '工程文件必须编译流程完整耗时追踪实现。'

Write-Host 'Performance run trace regression checks passed.'
