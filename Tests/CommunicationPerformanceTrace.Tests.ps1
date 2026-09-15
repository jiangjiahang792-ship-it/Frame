$ErrorActionPreference = 'Stop'

function Get-ProjectSource {
    param([string]$RelativePath)

    return Get-Content -LiteralPath (Join-Path $PSScriptRoot "..\$RelativePath") -Raw -Encoding UTF8
}

function Assert-Contains {
    param([string]$Content, [string]$Expected, [string]$Message)

    if (-not $Content.Contains($Expected)) {
        throw $Message
    }
}

function Assert-NotContains {
    param([string]$Content, [string]$Unexpected, [string]$Message)

    if ($Content.Contains($Unexpected)) {
        throw $Message
    }
}

$trace = Get-ProjectSource 'Diagnostics\ProcessPerformanceTrace.cs'
$process = Get-ProjectSource 'Process.cs'
$nodeBase = Get-ProjectSource 'Node\NodeBase.cs'
$diagnostics = Get-ProjectSource 'Diagnostics\PerformanceSpikeDiagnostics.cs'

$communicationNodeTypes = @(
    'LightSourceControl',
    'WaitSoftTrigger',
    'CameraShot',
    'PLCRead',
    'PLCWrite',
    'ModbusRead',
    'ModbusWrite',
    'TCPClientRequest',
    'TCPServerResponse',
    'ModbusSoftTrigger',
    'AIResultSend',
    'CameraIO',
    'ComSend',
    'ERUIIO',
    'CameraIOManual',
    'ReadFlag',
    'CameraExposureGain'
)

foreach ($nodeType in $communicationNodeTypes) {
    Assert-Contains $trace "case NodeType.${nodeType}:" "通信追踪分类缺少节点类型：${nodeType}。"
}

Assert-Contains $diagnostics 'DiagnosticBuildMark = "2026-08-19-完整链路诊断V5"' '第五轮现场诊断必须带可辨认的构建标记。'
Assert-Contains $process 'node.Process?.TraceNodeStarted(node);' '通信追踪必须复用所有节点共有的运行入口。'
Assert-Contains $nodeBase 'Process?.TraceNodeCompleted(this, status, elapsedMilliseconds);' '通信追踪必须复用所有节点共有的结果入口。'
Assert-Contains $trace 'IsCommunicationNodeType(node.NodeType)' '通信阶段必须按节点类型自动识别。'
Assert-Contains $trace 'GetCommunicationTypeText(node.NodeType)' '通信阶段必须输出简体中文通信类型。'
Assert-Contains $trace 'actualElapsed >= PerformanceSpikeDiagnostics.CommonSlowMs' '通信慢耗时现场必须复用统一阈值。'
Assert-Contains $trace 'PerformanceSpikeDiagnostics.GetRuntimeText()' '慢通信必须记录线程池、内存和GC现场。'
Assert-NotContains $trace 'GetNodeByLegacyIndex' '通信追踪不能依赖节点在旧顺序流程中的位置。'
Assert-NotContains $trace 'NextIndex' '通信追踪不能假设通信节点后面存在固定节点。'

$debugDirectory = Join-Path $PSScriptRoot '..\bin\x64\Debug'
$application = Get-Item -LiteralPath (Join-Path $debugDirectory '机器视觉AI检测系统V1.0.exe') -ErrorAction SilentlyContinue
if ($null -eq $application) {
    throw '找不到Debug程序，无法验证编译产物中的通信节点分类。'
}

[Environment]::CurrentDirectory = $debugDirectory
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$traceType = $assembly.GetType('TDJS_Vision.Diagnostics.ProcessPerformanceTrace', $true)
$nodeType = $assembly.GetType('TDJS_Vision.Node.NodeType', $true)
$classifier = $traceType.GetMethod('IsCommunicationNodeType', [Reflection.BindingFlags]'NonPublic,Static')
if ($null -eq $classifier) {
    throw '编译产物缺少通信节点分类入口。'
}

foreach ($nodeTypeName in $communicationNodeTypes) {
    $enumValue = [Enum]::Parse($nodeType, $nodeTypeName)
    $isCommunication = [bool]$classifier.Invoke($null, @($enumValue))
    if (-not $isCommunication) {
        throw "编译产物未把 ${nodeTypeName} 识别为通信节点。"
    }
}

foreach ($nodeTypeName in @('AITD', 'ImageSave', 'MultiCondition', 'ResultOverlayDraw')) {
    $enumValue = [Enum]::Parse($nodeType, $nodeTypeName)
    $isCommunication = [bool]$classifier.Invoke($null, @($enumValue))
    if ($isCommunication) {
        throw "编译产物把非通信节点 ${nodeTypeName} 误判为通信节点。"
    }
}

Write-Host '通信节点完整耗时关联检查通过。'
