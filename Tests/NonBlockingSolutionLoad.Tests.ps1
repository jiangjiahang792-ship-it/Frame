$ErrorActionPreference = 'Stop'

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Needle,
        [string]$Message
    )

    if (-not $Text.Contains($Needle)) {
        throw $Message
    }
}

function Assert-NotContains {
    param(
        [string]$Text,
        [string]$Needle,
        [string]$Message
    )

    if ($Text.Contains($Needle)) {
        throw $Message
    }
}

$testRoot = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    Join-Path (Get-Location) 'Tests'
}
else {
    $PSScriptRoot
}

$projectRoot = Split-Path -Parent $testRoot
$formMainPath = Join-Path $projectRoot 'FormMain.cs'
$progressContextPath = Join-Path $projectRoot 'Startup\StartupProgressContext.cs'
$applicationContextPath = Join-Path $projectRoot 'Startup\StartupApplicationContext.cs'
$solutionPath = Join-Path $projectRoot 'Solution.cs'
$processEditPath = Join-Path $projectRoot 'Forms\ProcessNew\ProcessEditPanel.cs'

$formMainSource = Get-Content -LiteralPath $formMainPath -Raw -Encoding UTF8
$progressContextSource = Get-Content -LiteralPath $progressContextPath -Raw -Encoding UTF8
$applicationContextSource = Get-Content -LiteralPath $applicationContextPath -Raw -Encoding UTF8
$solutionSource = Get-Content -LiteralPath $solutionPath -Raw -Encoding UTF8
$processEditSource = Get-Content -LiteralPath $processEditPath -Raw -Encoding UTF8

Assert-Contains $progressContextSource 'DrainFailures()' '启动进度上下文必须提供读取并清空数据恢复失败的方法。'
$throwMethodIndex = $progressContextSource.IndexOf('public static void ThrowIfFailures()')
$throwDrainIndex = $progressContextSource.IndexOf('DrainFailures();', $throwMethodIndex)
Assert-True ($throwDrainIndex -gt $throwMethodIndex) '原有致命失败检查必须复用统一的失败清空方法。'

$loadMethodStart = $formMainSource.IndexOf('private void LoadStartupSolution()')
$loadMethodEnd = $formMainSource.IndexOf('private void StartNonBlockingWarmups()', $loadMethodStart)
Assert-True ($loadMethodStart -ge 0 -and $loadMethodEnd -gt $loadMethodStart) '无法定位启动方案加载方法。'
$loadMethodSource = $formMainSource.Substring($loadMethodStart, $loadMethodEnd - $loadMethodStart)
Assert-Contains $loadMethodSource 'try' '启动方案加载必须捕获数据恢复异常。'
Assert-Contains $loadMethodSource 'catch (Exception ex)' '启动方案加载必须捕获文件、解析和恢复异常。'
$loadTryIndex = $loadMethodSource.IndexOf('try')
$settingsReadIndex = $loadMethodSource.IndexOf('Settings.Default.IsAutoLoad')
$pathNameIndex = $loadMethodSource.IndexOf('Path.GetFileName')
Assert-True ($loadTryIndex -ge 0 -and $loadTryIndex -lt $settingsReadIndex) '自动加载配置读取必须位于非阻断 try 范围内。'
Assert-True ($loadTryIndex -lt $pathNameIndex) '方案路径解析必须位于非阻断 try 范围内。'
Assert-Contains $loadMethodSource 'StartupProgressContext.DrainFailures();' '启动方案加载结束后必须读取并清空节点或设备恢复失败。'
Assert-Contains $loadMethodSource '_shouldStartAutoRun = false;' '方案加载不完整时禁止自动运行。'
Assert-Contains $loadMethodSource '方案数据加载异常，已忽略' '非阻断数据异常必须写入运行日志。'
Assert-Contains $loadMethodSource '继续进入主页面' '加载异常时启动动画应说明将继续进入主页面。'
Assert-NotContains $loadMethodSource 'StartupProgressContext.ThrowIfFailures();' '启动方案数据异常不应继续抛到启动失败页面。'

$manualOpenStart = $formMainSource.IndexOf('private void 打开方案ToolStripMenuItem_Click')
$manualOpenEnd = $formMainSource.IndexOf('/// 新建方案', $manualOpenStart)
Assert-True ($manualOpenStart -ge 0 -and $manualOpenEnd -gt $manualOpenStart) '无法定位手动打开方案方法。'
$manualOpenSource = $formMainSource.Substring($manualOpenStart, $manualOpenEnd - $manualOpenStart)
Assert-Contains $manualOpenSource '手动打开方案失败，已忽略' '手动打开方案异常必须保留完整日志。'
Assert-NotContains $manualOpenSource 'MessageBoxTD.Show(' '手动打开方案数据失败后不得弹出错误窗口。'

$solutionLoadStart = $solutionSource.IndexOf('public bool Load(string configFile, bool flag)')
$solutionLoadEnd = $solutionSource.IndexOf('/// 方案保存', $solutionLoadStart)
Assert-True ($solutionLoadStart -ge 0 -and $solutionLoadEnd -gt $solutionLoadStart) '方案加载接口必须返回是否完整加载成功。'
$solutionLoadSource = $solutionSource.Substring($solutionLoadStart, $solutionLoadEnd - $solutionLoadStart)
Assert-Contains $solutionLoadSource 'StartupProgressContext.Begin(new NullStartupProgressReporter())' '旧方案加载入口必须建立无弹窗的数据恢复作用域。'
Assert-Contains $solutionLoadSource 'StartupProgressContext.DrainFailures();' '旧方案加载入口必须消费设备和节点恢复失败。'
Assert-NotContains $solutionLoadSource 'MessageBoxTD.Show(' '旧方案加载入口不得显示反序列化错误弹窗。'

$nodeFailureStart = $processEditSource.IndexOf('var nodeLoadException = new InvalidOperationException(')
$nodeFailureEnd = $processEditSource.IndexOf('continue;', $nodeFailureStart)
Assert-True ($nodeFailureStart -ge 0 -and $nodeFailureEnd -gt $nodeFailureStart) '无法定位流程节点恢复失败处理。'
$nodeFailureSource = $processEditSource.Substring($nodeFailureStart, $nodeFailureEnd - $nodeFailureStart)
Assert-NotContains $nodeFailureSource 'MessageBoxTD.Show(' '流程节点恢复失败不得弹出错误窗口。'
Assert-Contains $nodeFailureSource 'LogHelper.AddLog' '流程节点恢复失败必须始终写入运行日志。'

Assert-Contains $applicationContextSource 'ShowFailureAsync' '真正的主界面初始化致命异常仍应保留失败处理。'

Write-Host '方案数据加载异常非阻断检查通过。'
