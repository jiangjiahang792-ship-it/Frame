$ErrorActionPreference = 'Stop'

function Get-ProjectSource {
    param([string]$RelativePath)
    $sourcePath = Join-Path $PSScriptRoot "..\$RelativePath"
    return Get-Content -Path $sourcePath -Raw -Encoding UTF8
}

function Assert-Contains {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if (-not $Text.Contains($Pattern)) { throw $Message }
}

function Get-SourceBlock {
    param([string]$Text, [string]$StartMarker, [string]$EndMarker)
    $startIndex = $Text.IndexOf($StartMarker)
    if ($startIndex -lt 0) { throw "未找到代码块开始标记：$StartMarker" }
    $endIndex = $Text.IndexOf($EndMarker, $startIndex)
    if ($endIndex -lt 0) { throw "未找到代码块结束标记：$EndMarker" }
    return $Text.Substring($startIndex, $endIndex - $startIndex)
}

# 委托抽取后仍验证调用先后顺序，不能只在任意文件中找到孤立的信号调用。
function Assert-InOrder {
    param([string]$Text, [string[]]$Patterns, [string]$Message)
    $position = 0
    foreach ($pattern in $Patterns) {
        $index = $Text.IndexOf($pattern, $position, [StringComparison]::Ordinal)
        if ($index -lt 0) { throw "$Message 缺失或顺序错误：$pattern" }
        $position = $index + $pattern.Length
    }
}

$formMainSource = Get-ProjectSource 'FormMain.cs'
$runtimeSource = Get-ProjectSource 'FormMain.RuntimeInterface.cs'
$controllerSource = Get-ProjectSource 'Forms/RuntimeDesigner/RuntimeFrameworkRunController.cs'
$autoRunBlock = Get-SourceBlock $formMainSource 'private async void RunAutoSolutionInBackgroundAsync()' 'private void InitializeLanguageUI()'
$hostBinding = Get-SourceBlock $runtimeSource 'private RuntimeFrameworkRunController RuntimeRunController =>' 'private IReadOnlyList<RuntimeProcessChoice> GetRuntimeProcesses()'
$hostRun = Get-SourceBlock $runtimeSource 'private async Task RunRuntimeFrameworkAsync(' 'private async Task StopRuntimeFrameworkAsync()'
$controllerRun = Get-SourceBlock $controllerSource 'internal Task RunAsync(int? processId, bool continuous, bool automatic)' 'internal bool TryRunAsync('
$controllerAdmission = Get-SourceBlock $controllerSource 'internal bool TryRunAsync(' 'private async Task ExecuteAsync('
$controllerExecute = Get-SourceBlock $controllerSource 'private async Task ExecuteAsync(' 'private Task StartHostRun('

Assert-Contains $autoRunBlock 'await RuntimeRunController.RunAsync(null, true, automatic: true);' '自动启动必须委托共享控制器，保持全流程、循环和自动模式。'
Assert-Contains $hostBinding 'GetRuntimeProcesses, RunRuntimeFrameworkAsync, StopRuntimeFrameworkAsync,' '共享控制器必须绑定正式宿主的执行和停止链路。'
Assert-Contains $controllerRun 'TryRunAsync(processId, continuous, automatic, out Task task)' '控制器必须原样传递自动运行模式到准入入口。'
Assert-Contains $controllerAdmission 'task = ExecuteAsync(processId, continuous, automatic, lease);' '准入成功后必须继续执行原模式，不能丢失自动启动标志。'
Assert-Contains $controllerExecute 'await run(processId, continuous, automatic, StartHostRun);' '控制器必须等待正式宿主执行与收尾全部结束。'

Assert-InOrder $hostRun @(
    'await FormGlobalSignal.SendGlobalSignals(this, true);',
    'online = true;',
    'await start(() =>',
    'FormGlobalSignal.StartListenSignals();',
    'if (automatic) FormGlobalSignal.StartHeartbeatSignals();',
    'return solution.RunProcessesAsync(selected, continuous);',
    'finally',
    'FormGlobalSignal.StopListenSignals();',
    'if ((!continuous && !automatic) || (automatic && online) || (online && !started))',
    'await FormGlobalSignal.SendGlobalSignals(this, false);'
) '自动运行必须先准备、再监听和心跳、再执行；最终停止监听，并在准备成功后发送离线信号。'

Write-Host 'Auto run global signal source checks passed.'


