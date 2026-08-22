$ErrorActionPreference = 'Stop'

$solutionPath = Join-Path $PSScriptRoot '..\Solution.cs'
$source = Get-Content -Path $solutionPath -Raw -Encoding UTF8
$processSource = Get-Content -Path (Join-Path $PSScriptRoot '..\Process.cs') -Raw -Encoding UTF8
$processEditorSource = Get-Content -Path (Join-Path $PSScriptRoot '..\Forms\ProcessNew\ProcessEditPanel.cs') -Raw -Encoding UTF8
$configSource = Get-Content -Path (Join-Path $PSScriptRoot '..\ConfigHelper.cs') -Raw -Encoding UTF8

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

function Assert-Sequence {
    param(
        [string]$Text,
        [string]$First,
        [string]$Second,
        [string]$Message
    )

    $firstIndex = $Text.IndexOf($First)
    $secondIndex = $Text.IndexOf($Second)
    if ($firstIndex -lt 0 -or $secondIndex -lt 0 -or $firstIndex -ge $secondIndex) {
        throw $Message
    }
}

Assert-Contains $source 'private void ReleaseAllProcessResources()' 'Missing all-process resource release helper.'
Assert-Contains $source 'private void ReleaseProcessResources(Process process)' 'Missing single-process resource release helper.'
Assert-Contains $source 'private void ReleaseNodeResources(NodeBase node)' 'Missing node resource release helper.'
Assert-Contains $source 'private void ReleaseAiNodeModel(NodeBase node)' 'Missing AI node model release helper.'
Assert-Contains $source 'private void DisposeNodeParamForm(NodeBase node)' 'Missing node parameter form dispose helper.'
Assert-Contains $source 'node.ReleaseResultResources();' 'Node results must be released when a process or solution is removed.'
Assert-Contains $source 'ReleaseProcessResources(process);' 'RemoveProcess(Process) must release node resources before removing process.'
Assert-Contains $source 'ReleaseProcessResources(processToRemove);' 'RemoveProcess(string) must release node resources before removing process.'
Assert-Contains $source 'ReleaseAllProcessResources();' 'SolReset must release all process resources before clearing processes.'
Assert-Sequence $source 'ReleaseAllProcessResources();' 'Solution.Instance.AllProcesses.Clear();' 'SolReset must release process resources before clearing process list.'
Assert-Contains $source 'deviceReleaseFailures' '方案重置必须逐设备汇总释放失败。'
Assert-Contains $source 'Solution.Instance.AllDevices.ToArray()' '设备释放遍历必须使用稳定快照并继续处理后续设备。'
Assert-Sequence $source 'if (deviceReleaseFailures.Count > 0)' 'Solution.Instance.AllDevices.Clear();' '存在设备释放失败时必须在清空设备引用前中止重置。'
Assert-Sequence $source 'ReleaseProcessResources(process);' 'AllProcesses.Remove(process);' 'RemoveProcess(Process) must release resources before removing the process.'
Assert-Sequence $source 'ReleaseProcessResources(processToRemove);' 'AllProcesses.Remove(processToRemove);' 'RemoveProcess(string) must release resources before removing the process.'

$solResetIndex = $source.IndexOf('public bool SolReset()')
if ($solResetIndex -lt 0) {
    throw 'SolReset必须返回能否安全完成资源重置。'
}
$solResetSource = $source.Substring($solResetIndex)
Assert-Sequence $solResetSource 'TryStopRunsForReset(drainTimeoutMilliseconds)' 'StopImageSaveQueueProcessor()' '方案重置必须先等待全部运行入口退出，再停止保存池。'
Assert-Sequence $solResetSource 'StopImageSaveQueueProcessor()' 'ReleaseAllProcessResources();' '方案重置必须先完全停止保存池，再释放节点图像资源。'
Assert-Contains $source 'if (!_imageSaveQueueProcessor.IsFullyStopped)' '旧保存池未完全退出时不得创建重叠工作池。'
Assert-Contains $source 'private readonly object _solutionMutationSync = new object();' '并发方案变更必须使用独立互斥锁。'
Assert-Contains $source 'lock (_solutionMutationSync)' '并发调用SolReset必须串行执行。'
Assert-Contains $source 'internal IDisposable EnterSolutionMutationScope()' '方案加载必须持有覆盖整个加载周期的互斥作用域。'
Assert-Contains $source 'internal bool TryBeginExternalRunSession()' '手动流程和参数预览必须接入统一运行会话门禁。'
Assert-Contains $source 'TryBeginRunSession(out isFirstSession)' '首条外部运行会话必须在登记锁内原子确定。'
Assert-Contains $source 'if (isFirstSession)' '第一条运行会话必须负责初始化运行时资源档案。'
Assert-Contains $source 'RefreshRuntimeResourceProfile(true);' '首次运行会话必须显式允许安全应用资源档案。'
Assert-Contains $processSource 'Volatile.Read(ref _isRunning)' '流程运行状态必须跨线程可见。'
Assert-Contains $processSource 'if (!Solution.Instance.TryBeginExternalRunSession())' '公开流程运行入口必须登记运行会话。'
Assert-Contains $processSource 'Solution.Instance.EndExternalRunSession();' '公开流程运行入口退出时必须归还运行会话。'
$externalBeginCount = [regex]::Matches($processSource, [regex]::Escape('if (!Solution.Instance.TryBeginExternalRunSession())')).Count
$externalEndCount = [regex]::Matches($processSource, [regex]::Escape('Solution.Instance.EndExternalRunSession();')).Count
if ($externalBeginCount -lt 4 -or $externalEndCount -lt 4) {
    throw "静态流程、实例流程、跳过图像源流程和参数预览必须全部纳入运行门禁；开始=$externalBeginCount；结束=$externalEndCount。"
}
Assert-Contains $processEditorSource 'if (!Solution.Instance.TryBeginExternalRunSession())' '手动运行必须在启动相机前登记运行会话。'
Assert-Contains $configSource 'if (!Solution.Instance.SolResetForSolutionLoad())' '加载新方案时必须检查旧方案是否已经安全重置并继续保持运行门禁。'
Assert-Contains $configSource 'using (Solution.Instance.EnterSolutionMutationScope())' '方案加载从读取旧配置到恢复新方案必须保持独占。'
Assert-Contains $configSource 'openedGateOwnedByLoad = Solution.Instance.BeginSolutionLoadRunGate();' '读取方案文件前必须关闭新流程运行入口并记录原门禁状态。'
Assert-Contains $configSource 'Solution.Instance.EndSolutionLoadRunGate();' '方案读取失败或恢复结束后必须重新开放运行入口。'
Assert-Contains $source 'return SolResetCore(false);' '方案加载清理旧方案后必须继续保持运行门禁。'

Write-Host 'Solution resource release regression checks passed.'
