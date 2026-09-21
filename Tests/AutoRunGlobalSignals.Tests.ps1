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

$formMainSource = Get-ProjectSource 'FormMain.cs'
$autoRunBlock = Get-SourceBlock $formMainSource 'private async void RunAutoSolutionInBackgroundAsync()' 'private void InitializeLanguageUI()'

Assert-Contains $autoRunBlock 'await FormGlobalSignal.SendGlobalSignals(this, true);' '软件启动后自动运行必须先发送视觉准备信号。'
Assert-Contains $autoRunBlock 'FormGlobalSignal.StartListenSignals();' '软件启动后自动运行必须启动监听信号。'
Assert-Contains $autoRunBlock 'FormGlobalSignal.StartHeartbeatSignals();' '软件启动后自动运行必须启动心跳信号。'
Assert-Contains $autoRunBlock 'FormGlobalSignal.StopListenSignals();' '自动运行退出时必须停止监听信号。'
Assert-Contains $autoRunBlock 'await FormGlobalSignal.SendGlobalSignals(this, false);' '自动运行退出时必须发送视觉离线信号。'

Write-Host 'Auto run global signal source checks passed.'


