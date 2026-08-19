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

$processSource = Get-ProjectSource 'Process.cs'
$diagnosticsSource = Get-ProjectSource 'Diagnostics\PerformanceSpikeDiagnostics.cs'
$imageDrawSource = Get-ProjectSource 'Node\7-ResultProcessing\ImageDraw\NodeImageDraw.cs'
$overlay2Source = Get-ProjectSource 'Node\7-ResultProcessing\ResultOverlayDraw2\NodeResultOverlayDraw2.cs'
$findPointNodeSource = Get-ProjectSource 'Node\4-Measurement\FindPoint\NodeFindPoint.cs'
$findPointFormSource = Get-ProjectSource 'Node\4-Measurement\FindPoint\NodeParamFormFindPoint.cs'
$findPointAlgorithmSource = Get-ProjectSource 'Node\4-Measurement\FindPoint\FindPointAlgorithm.cs'

Assert-Contains $diagnosticsSource 'DiagnosticBuildMark = "2026-08-19-完整链路诊断V5"' '当前现场诊断必须带第五轮可辨认构建标记。'
Assert-Contains $processSource 'Stopwatch fastPathWatch = PerformanceSpikeDiagnostics.StartStopwatchIfEnabled(MsgLevel.Debug);' '相机回调快速路径必须只在Debug开启时创建诊断计时器。'
Assert-Contains $processSource 'Stopwatch prepareWatch = PerformanceSpikeDiagnostics.StartStopwatchIfEnabled(MsgLevel.Debug);' '跳过节点结果准备必须只在Debug开启时创建诊断计时器。'
Assert-Contains $processSource 'Stopwatch stopwatch = Stopwatch.StartNew();' '并行图运行时用于计算RunTime的业务计时器必须保留。'

Assert-Contains $imageDrawSource 'PerformanceSpikeDiagnostics.StartStopwatchIfEnabled(MsgLevel.Debug)' 'AI结果绘制必须按Debug开关创建诊断计时器。'
Assert-Contains $imageDrawSource 'PerformanceSpikeDiagnostics.LogIfEnabled(' 'AI结果绘制必须使用惰性诊断日志。'
Assert-Contains $imageDrawSource '() => $"【性能诊断-AI结果绘制】' 'AI结果绘制性能日志必须惰性生成诊断文本。'

Assert-Contains $overlay2Source 'performanceDiagnostics = stopwatch == null ? null : new ResultOverlayDraw2PerformanceDiagnostics();' 'ROI结果绘制2在Debug关闭时不能创建性能明细对象。'
Assert-Contains $overlay2Source 'Stopwatch itemWatch = performanceDiagnostics == null ? null : Stopwatch.StartNew();' 'ROI结果绘制2在Debug关闭时不能创建逐项计时器。'
Assert-Contains $overlay2Source '() => $"【性能诊断-ROI结果绘制2】' 'ROI结果绘制2性能日志必须惰性生成诊断文本。'

Assert-Contains $findPointNodeSource 'Stopwatch nodeRunWatch = PerformanceSpikeDiagnostics.StartStopwatchIfEnabled(MsgLevel.Debug);' '找点节点分段诊断必须按Debug开关创建计时器。'
Assert-Contains $findPointFormSource 'FindPointTimingInfo timing = PerformanceSpikeDiagnostics.IsDiagnosticLogEnabled(MsgLevel.Debug)' '找点参数窗体仅在Debug开启时创建分段诊断对象。'
Assert-Contains $findPointFormSource 'Stopwatch runtimeParamWatch = timing == null ? null : Stopwatch.StartNew();' '找点运行参数计时器必须受诊断对象控制。'
Assert-Contains $findPointFormSource 'if (timing == null)' '找点图像信息采集必须允许Debug关闭时跳过。'
Assert-Contains $findPointAlgorithmSource 'Stopwatch stopwatch = Stopwatch.StartNew();' '找点算法结果公开AlgorithmMs，业务计时器必须保留。'
Assert-Contains $findPointAlgorithmSource 'result.AlgorithmMs = stopwatch.Elapsed.TotalMilliseconds;' '找点算法耗时必须继续写入业务结果。'

Write-Host '剩余性能诊断开销管控检查通过。'
