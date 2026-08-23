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
$source = Get-Item -LiteralPath (Join-Path $projectRoot 'ResourceManagement\WorkpieceRuntimeInfrastructure.cs')
Assert-True ($null -ne $application) '工件运行基础设施测试需要最新Debug程序。'
Assert-True ($application.LastWriteTimeUtc -ge $source.LastWriteTimeUtc) 'Debug程序早于工件运行基础设施源码，请先重新编译。'

[Environment]::CurrentDirectory = $debugDirectory
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$identityType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceIdentity', $true)
$contextType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceExecutionContext', $true)
$accessorType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceContextAccessor', $true)
$generatorType = $assembly.GetType('TDJS_Vision.ResourceManagement.WorkpieceIdentityGenerator', $true)
$gateType = $assembly.GetType('TDJS_Vision.ResourceManagement.ProcessWorkpieceGate', $true)
$queueFullType = $assembly.GetType('TDJS_Vision.ResourceManagement.ProcessWorkpieceQueueFullException', $true)

$generator = [Activator]::CreateInstance($generatorType)
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
for ($index = 1; $index -le 100000; $index++) {
    $identity = $generator.Next(' 产线A ')
    Assert-True ($identity.Sequence -eq $index) '同一顺序域工件序号必须从1严格连续递增。'
    Assert-True ($seen.Add($identity.ToString())) '同一生产会话和顺序域不能生成重复工件身份。'
}
$normalizedIdentity = $generator.Next('产线a')
Assert-True ($normalizedIdentity.Sequence -eq 100001) '顺序域比较必须忽略大小写和首尾空白。'
$otherDomainIdentity = $generator.Next('产线B')
Assert-True ($otherDomainIdentity.Sequence -eq 1) '不同顺序域必须拥有独立连续序号。'
Assert-True (-not $otherDomainIdentity.Equals($generator.Next('产线C'))) '不同顺序域的同序号工件身份不能相等。'
$oldEpoch = $generator.SessionEpoch
$resetMethod = $generatorType.GetMethod('ResetSession', [Reflection.BindingFlags]'Instance,NonPublic')
$newEpoch = $resetMethod.Invoke($generator, @())
Assert-True ($oldEpoch -ne $newEpoch) '新生产会话必须更换会话纪元。'
Assert-True ($generator.Next('产线A').Sequence -eq 1) '确认排空并重建会话后顺序域才允许从1重新开始。'

$accessor = [Activator]::CreateInstance($accessorType)
$context1 = [Activator]::CreateInstance(
    $contextType,
    @($generator.Next('产线A'), [long]1, '产线A', '专项测试', [DateTime]::UtcNow, [Threading.CancellationToken]::None))
$context2 = [Activator]::CreateInstance(
    $contextType,
    @($generator.Next('产线A'), [long]1, '产线A', '专项测试', [DateTime]::UtcNow, [Threading.CancellationToken]::None))
Assert-True ($null -eq $accessor.Current) '未进入工件作用域时当前上下文必须为空。'
$scope1 = $accessor.Push($context1)
Assert-True ([object]::ReferenceEquals($accessor.Current, $context1)) '外层作用域必须公开第一个工件上下文。'
$scope2 = $accessor.Push($context2)
Assert-True ([object]::ReferenceEquals($accessor.Current, $context2)) '内层作用域必须覆盖当前上下文。'
$scope2.Dispose()
$scope2.Dispose()
Assert-True ([object]::ReferenceEquals($accessor.Current, $context1)) '内层作用域重复释放后必须恢复且保持外层上下文。'
$scope1.Dispose()
Assert-True ($null -eq $accessor.Current) '外层作用域释放后必须恢复空上下文。'

$outOfOrderScope1 = $accessor.Push($context1)
$outOfOrderScope2 = $accessor.Push($context2)
$outOfOrderRejected = $false
try {
    $outOfOrderScope1.Dispose()
}
catch [InvalidOperationException] {
    $outOfOrderRejected = $true
}
Assert-True $outOfOrderRejected '上下文作用域乱序释放必须明确失败，不能静默恢复错误工件。'
Assert-True ([object]::ReferenceEquals($accessor.Current, $context2)) '外层乱序释放失败后必须保持仍活动的内层上下文。'
$outOfOrderScope2.Dispose()
$outOfOrderScope1.Dispose()
Assert-True ($null -eq $accessor.Current) '按后进先出顺序补充释放后必须恢复空上下文。'

$gate = [Activator]::CreateInstance($gateType, @('流程1', 4))
$lease1 = $gate.EnterAsync([Threading.CancellationToken]::None).GetAwaiter().GetResult()
$wait2 = $gate.EnterAsync([Threading.CancellationToken]::None)
$wait3 = $gate.EnterAsync([Threading.CancellationToken]::None)
$wait4 = $gate.EnterAsync([Threading.CancellationToken]::None)
$snapshot = $gate.GetSnapshot()
Assert-True ($snapshot.HasActiveWorkpiece -and $snapshot.WaitingCount -eq 3) '容量4必须包含1个活动工件和3个等待工件。'
Assert-True ($snapshot.PeakOccupancy -eq 4) '历史峰值必须记录满容量4。'

$fullRejected = $false
try {
    $null = $gate.EnterAsync([Threading.CancellationToken]::None)
}
catch {
    $currentException = $_.Exception
    while ($null -ne $currentException.InnerException) {
        $currentException = $currentException.InnerException
    }
    $fullRejected = $queueFullType.IsInstanceOfType($currentException)
}
Assert-True $fullRejected '队列满载必须在执行权批准前抛出明确准入异常。'
Assert-True ($gate.GetSnapshot().RejectedFullCount -eq 1) '满载拒绝必须累计可诊断计数。'

$lease1.Dispose()
Assert-True $wait2.IsCompleted '释放活动工件后必须首先唤醒FIFO队首。'
Assert-True (-not $wait3.IsCompleted -and -not $wait4.IsCompleted) '队首未释放时后续等待者不能越过。'
$lease2 = $wait2.GetAwaiter().GetResult()
$lease2.Dispose()
$lease2.Dispose()
Assert-True $wait3.IsCompleted '队首租约重复释放不得跳过第二个等待者或增加额外许可。'
Assert-True (-not $wait4.IsCompleted) '第二个等待者未释放前第三个等待者必须继续等待。'
$lease3 = $wait3.GetAwaiter().GetResult()
$lease3.Dispose()
$lease4 = $wait4.GetAwaiter().GetResult()
$lease4.Dispose()
$emptySnapshot = $gate.GetSnapshot()
Assert-True (-not $emptySnapshot.HasActiveWorkpiece -and $emptySnapshot.WaitingCount -eq 0) '全部租约释放后入口必须回到空闲状态。'

$cancelGate = [Activator]::CreateInstance($gateType, @('流程2', 3))
$cancelLease1 = $cancelGate.EnterAsync([Threading.CancellationToken]::None).GetAwaiter().GetResult()
$waitCancellation = [Threading.CancellationTokenSource]::new()
$cancelledWait = $cancelGate.EnterAsync($waitCancellation.Token)
$nextWait = $cancelGate.EnterAsync([Threading.CancellationToken]::None)
$waitCancellation.Cancel()
try {
    $null = $cancelledWait.GetAwaiter().GetResult()
    throw '取消的等待者不能取得执行权。'
}
catch [OperationCanceledException] {
}
$cancelLease1.Dispose()
Assert-True $nextWait.IsCompleted '取消的FIFO队首必须永久移除并允许下一位取得执行权。'
$nextLease = $nextWait.GetAwaiter().GetResult()
$nextLease.Dispose()
$waitCancellation.Dispose()

$gate.Dispose()
$cancelGate.Dispose()
Write-Host '工件运行基础设施检查通过：100000身份、上下文栈、容量满载、严格FIFO、幂等租约和取消移除均符合预期。'
