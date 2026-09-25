$ErrorActionPreference = 'Stop'

function Assert-Equal {
    param($Actual, $Expected, [string]$Message)
    if ($Actual -ne $Expected) {
        throw "$Message Expected=$Expected Actual=$Actual"
    }
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

$root = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $root 'ResourceManagement/CameraRawFrameMemoryBudgetManager.cs'
$concurrencyTestSource = @'
namespace TDJS_Vision.ResourceManagement.Tests
{
    public static class CameraBudgetConcurrencyRunner
    {
        /// <summary>竞争部分归还与整体释放，验证不会重复扣减租约预算。</summary>
        public static void RaceTrimAndDispose(ICameraRawFrameMemoryLease lease)
        {
            using (var start = new System.Threading.ManualResetEventSlim(false))
            {
                var trim = System.Threading.Tasks.Task.Run(() =>
                {
                    start.Wait();
                    try { lease.ReleaseBuffers(6); }
                    catch (System.ObjectDisposedException) { }
                });
                var close = System.Threading.Tasks.Task.Run(() => { start.Wait(); lease.Dispose(); });
                start.Set();
                System.Threading.Tasks.Task.WaitAll(trim, close);
            }
        }

        public static ICameraRawFrameMemoryLease[] ReserveAtOnce(
            ICameraRawFrameMemoryBudgetManager manager,
            int cameraCount,
            long budgetBytes,
            int bytesPerBuffer,
            int bufferCount)
        {
            var start = new System.Threading.ManualResetEventSlim(false);
            var tasks = new System.Threading.Tasks.Task<ICameraRawFrameMemoryLease>[cameraCount];
            for (int index = 0; index < cameraCount; index++)
            {
                int cameraIndex = index + 1;
                tasks[index] = System.Threading.Tasks.Task.Run(() =>
                {
                    start.Wait();
                    return manager.Reserve(
                        "Camera-" + cameraIndex,
                        budgetBytes,
                        bytesPerBuffer,
                        bufferCount,
                        bufferCount);
                });
            }

            start.Set();
            System.Threading.Tasks.Task.WaitAll(tasks);
            var leases = new ICameraRawFrameMemoryLease[cameraCount];
            for (int index = 0; index < cameraCount; index++)
                leases[index] = tasks[index].Result;
            start.Dispose();
            return leases;
        }

        public static void DisposeAtOnce(ICameraRawFrameMemoryLease[] leases)
        {
            var start = new System.Threading.ManualResetEventSlim(false);
            var tasks = new System.Threading.Tasks.Task[leases.Length];
            for (int index = 0; index < leases.Length; index++)
            {
                ICameraRawFrameMemoryLease lease = leases[index];
                tasks[index] = System.Threading.Tasks.Task.Run(() =>
                {
                    start.Wait();
                    lease.Dispose();
                    lease.Dispose();
                });
            }

            start.Set();
            System.Threading.Tasks.Task.WaitAll(tasks);
            start.Dispose();
        }
    }
}
'@
Add-Type -TypeDefinition ((Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8) + $concurrencyTestSource) -Language CSharp

$oneMb = 1MB
$budget = 32MB
$manager = [TDJS_Vision.ResourceManagement.CameraRawFrameMemoryBudgetManager]::new()
$leases = [TDJS_Vision.ResourceManagement.Tests.CameraBudgetConcurrencyRunner]::ReserveAtOnce(
    $manager,
    16,
    $budget,
    $oneMb,
    2)

foreach ($lease in $leases) {
    Assert-Equal $lease.BufferCount 2 '十六相机公平配置时每台必须取得两块缓冲。'
}

Assert-Equal $manager.ActiveReservationCount 16 '必须记录全部十六台活动相机。'
Assert-Equal $manager.CurrentReservedBytes $budget '十六台相机预留总量必须精确等于全局预算。'

$overflowRejected = $false
try {
    $manager.Reserve('Camera-17', $budget, $oneMb, 2, 2).Dispose()
}
catch [System.InvalidOperationException] {
    $overflowRejected = $true
}
Assert-True $overflowRejected '第十七台相机不得突破全局预算。'

$leases[0].Dispose()
Assert-Equal $manager.ActiveReservationCount 15 '租约重复释放必须幂等。'
Assert-Equal $manager.CurrentReservedBytes 30MB '释放一台相机后必须归还两块缓冲预算。'

$replacement = $manager.Reserve('Camera-Replacement', $budget, $oneMb, 8, 2)
Assert-Equal $replacement.BufferCount 2 '剩余预算只够两块时必须自动收敛，不得超配。'
$replacement.Dispose()

$smallerBudgetRejected = $false
try {
    $manager.Reserve('Camera-Lower-Budget', 16MB, $oneMb, 2, 2).Dispose()
}
catch [System.InvalidOperationException] {
    $smallerBudgetRejected = $true
}
Assert-True $smallerBudgetRejected '活动相机占用超过新预算时必须拒绝静默降额。'

[TDJS_Vision.ResourceManagement.Tests.CameraBudgetConcurrencyRunner]::DisposeAtOnce($leases)
Assert-Equal $manager.ActiveReservationCount 0 '全部相机停止后活动租约必须归零。'
Assert-Equal $manager.CurrentReservedBytes 0 '全部相机停止后已预留字节必须归零。'
Assert-Equal $manager.CurrentBudgetBytes 0 '全部相机停止后预算代次必须复位。'

Write-Host '相机原始帧全局内存预算测试通过：16线程同时申请严格限制在32MB，溢出拒绝，同时释放后完整归还。'

# 活动池部分归还后新增相机，不应重复归还同一块预算。
$first = $manager.Reserve('Existing', 32MB, 1MB, 8, 2)
$first.ReleaseBuffers(6)
Assert-Equal $first.BufferCount 2 '部分归还后必须保留两块启动缓冲。'
Assert-Equal $manager.CurrentReservedBytes 2MB '部分归还必须立即反映到共享预算。'
Assert-Equal $manager.ActiveReservationCount 1 '部分归还不能减少活动相机数。'
$invalidTrimRejected = $false
try { $first.ReleaseBuffers(1) } catch [System.ArgumentOutOfRangeException] { $invalidTrimRejected = $true }
Assert-True $invalidTrimRejected '禁止归还启动所需的最少缓冲。'
$second = $manager.Reserve('Added', 32MB, 1MB, 8, 2)
$first.Dispose()
$first.Dispose()
Assert-Equal $manager.CurrentReservedBytes 8MB '关闭缩小后的旧池不得扣除其他相机的预算。'
$second.Dispose()
Assert-Equal $manager.CurrentReservedBytes 0 '部分归还后全部关闭仍必须归零。'
Write-Host '相机缓冲部分归还、动态新增、下限保护及幂等释放测试通过。'

# 预览先用保守预算取流，运行资源档案随后批准更大预算时不能继续锁死在旧预算。
$preview = $manager.Reserve('Preview', 16MB, 1MB, 16, 2)
$production = $manager.Reserve('Production', 32MB, 1MB, 8, 2)
Assert-Equal $manager.CurrentBudgetBytes 32MB '新资源档案提高预算后，活动预算必须同步提高。'
Assert-Equal $manager.CurrentReservedBytes 24MB '提高预算不影响已有相机租约。'
$preview.Dispose()
$production.Dispose()
Assert-Equal $manager.CurrentReservedBytes 0 '更新预算后关闭相机仍必须完整归还。'
Write-Host '活动相机采用更新后的资源预算测试通过。'

for ($round = 0; $round -lt 100; $round++) {
    $racing = $manager.Reserve('Racing', 32MB, 1MB, 8, 2)
    $unchanged = $manager.Reserve('Unchanged', 32MB, 1MB, 2, 2)
    [TDJS_Vision.ResourceManagement.Tests.CameraBudgetConcurrencyRunner]::RaceTrimAndDispose($racing)
    Assert-Equal $manager.CurrentReservedBytes 2MB '回收与关闭竞争不能扣减另一台相机的预算。'
    Assert-Equal $manager.ActiveReservationCount 1 '回收与关闭竞争后活动相机数量必须准确。'
    $unchanged.Dispose()
}
Assert-Equal $manager.CurrentReservedBytes 0 '并发回收与关闭结束后所有预算必须归零。'
Write-Host '100轮空闲缓冲归还与相机关闭并发竞争测试通过。'
