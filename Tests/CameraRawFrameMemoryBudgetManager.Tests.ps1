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
