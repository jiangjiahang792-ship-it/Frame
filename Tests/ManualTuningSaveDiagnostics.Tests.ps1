$ErrorActionPreference = "Stop"

$root = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    (Get-Location).Path
}
else {
    Split-Path -Parent $PSScriptRoot
}
$runParamPath = Join-Path $root "Forms\SolRunParam\SolRunParam.cs"

function Assert-Contains {
    param([string]$Content, [string]$Pattern, [string]$Message)
    if ($Content -notlike "*$Pattern*") {
        throw $Message
    }
}

$runParam = Get-Content -Raw -Encoding UTF8 $runParamPath

Assert-Contains $runParam 'string saveStage = "读取运行参数表格";' '手动调参保存缺少可定位失败位置的阶段标记。'
Assert-Contains $runParam 'saveStage = "写入AI配置文件";' '手动调参保存没有标记AI配置文件写入阶段。'
Assert-Contains $runParam 'saveStage = "保存方案文件";' '手动调参保存没有标记方案文件持久化阶段。'
Assert-Contains $runParam 'LogManualTuningSaveFailure(ex, saveStage);' '手动调参保存异常没有进入统一诊断日志。'
Assert-Contains $runParam '【手动调参保存异常】' '诊断日志缺少可快速检索的中文标识。'
Assert-Contains $runParam '流程={processName}' '诊断日志缺少当前流程名称。'
Assert-Contains $runParam 'AI节点={aiNodeText}' '诊断日志缺少当前AI节点信息。'
Assert-Contains $runParam 'AI配置路径={aiConfigPath}' '诊断日志缺少AI配置文件路径。'
Assert-Contains $runParam '方案路径={solutionPath}' '诊断日志缺少方案文件路径。'
Assert-Contains $runParam '异常详情：{exception}' '诊断日志必须保留异常类型、内部异常和完整堆栈。'
Assert-Contains $runParam 'catch' '诊断日志写入失败时必须隔离，不能覆盖原始保存异常。'

Write-Host "手动调参保存诊断日志检查通过。"
