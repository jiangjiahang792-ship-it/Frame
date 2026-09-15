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
Assert-True ($null -ne $application) '方案触发准入测试需要最新Debug程序。'
$source = Get-Item -LiteralPath (Join-Path $projectRoot 'ResourceManagement\SolutionRunAdmissionController.cs')
Assert-True ($application.LastWriteTimeUtc -ge $source.LastWriteTimeUtc) 'Debug程序早于方案触发准入源码，请先重新编译。'

[Environment]::CurrentDirectory = $debugDirectory
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$controllerType = $assembly.GetType('TDJS_Vision.ResourceManagement.SolutionRunAdmissionController', $true)
$controller = [Activator]::CreateInstance($controllerType, @(3))

$firstLease = $null
Assert-True ($controller.TryAcquire([ref]$firstLease)) '第一条触发必须取得方案运行权。'
for ($index = 0; $index -lt 4; $index++) {
    $rejectedLease = $null
    Assert-True (-not $controller.TryAcquire([ref]$rejectedLease)) '活动轮次期间的触发必须立即拒绝。'
    Assert-True ($null -eq $rejectedLease) '忙碌拒绝不能返回运行租约。'
}

$busySnapshot = $controller.GetSnapshot()
Assert-True ($busySnapshot.RequestedCount -eq 5) '请求计数必须包含准入和忙碌拒绝。'
Assert-True ($busySnapshot.AcceptedCount -eq 1 -and $busySnapshot.BusyRejectedCount -eq 4) '准入和忙碌拒绝计数不正确。'
Assert-True ($busySnapshot.IsActive) '首轮租约完成前必须保持活动状态。'
Assert-True (-not $controller.TryReset()) '活动轮次期间禁止清空诊断计数。'

Start-Sleep -Milliseconds 20
$firstLease.Complete($true)
$firstLease.Complete($true)
$firstSnapshot = $controller.GetSnapshot()
Assert-True ($firstSnapshot.CompletedCount -eq 1 -and $firstSnapshot.FailedCount -eq 0) '成功租约重复完成只能记一次。'
Assert-True (-not $firstSnapshot.IsActive -and $firstSnapshot.DurationSampleCount -eq 1) '成功完成后必须释放运行权并留下一个耗时样本。'
Assert-True ($firstSnapshot.MaximumMilliseconds -ge 15) '准入租约必须记录真实墙钟耗时。'

$failedLease = $null
Assert-True ($controller.TryAcquire([ref]$failedLease)) '上一轮完成后下一条触发必须能够准入。'
$failedLease.Dispose()
$failedSnapshot = $controller.GetSnapshot()
Assert-True ($failedSnapshot.FailedCount -eq 1 -and -not $failedSnapshot.IsActive) '未显式成功的租约释放必须记为异常并归还运行权。'

foreach ($delay in @(5, 10, 15)) {
    $lease = $null
    Assert-True ($controller.TryAcquire([ref]$lease)) '连续完成样本必须逐轮准入。'
    Start-Sleep -Milliseconds $delay
    $lease.Complete($true)
}

$boundedSnapshot = $controller.GetSnapshot()
Assert-True ($boundedSnapshot.DurationSampleCount -eq 3) '耗时滚动样本不得突破配置容量3。'
Assert-True ($boundedSnapshot.P50Milliseconds -gt 0 -and $boundedSnapshot.P95Milliseconds -gt 0 -and $boundedSnapshot.P99Milliseconds -gt 0) '完成样本必须生成P50/P95/P99。'
Assert-True ($boundedSnapshot.RequestedCount -eq ($boundedSnapshot.AcceptedCount + $boundedSnapshot.BusyRejectedCount)) '每个触发请求必须有且只有一种准入结果。'

Assert-True ($controller.TryReset()) '空闲时必须允许清除预热计数。'
$resetSnapshot = $controller.GetSnapshot()
Assert-True ($resetSnapshot.RequestedCount -eq 0 -and $resetSnapshot.AcceptedCount -eq 0 -and $resetSnapshot.BusyRejectedCount -eq 0) '重置后触发计数必须归零。'
Assert-True ($resetSnapshot.CompletedCount -eq 0 -and $resetSnapshot.FailedCount -eq 0 -and $resetSnapshot.DurationSampleCount -eq 0) '重置后完成、异常和耗时样本必须归零。'

Write-Host '方案触发准入检查通过：忙碌立即拒绝、计数闭合、租约幂等、异常归还、耗时样本有界和空闲重置均符合预期。'
