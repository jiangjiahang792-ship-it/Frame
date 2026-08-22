$ErrorActionPreference = 'Stop'

$projectRoot = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    (Get-Location).Path
}
else {
    Split-Path -Parent $PSScriptRoot
}

function Get-ProjectSource {
    param([string]$RelativePath)

    return Get-Content -LiteralPath (Join-Path $projectRoot $RelativePath) -Raw -Encoding UTF8
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

function Assert-Equal {
    param($Actual, $Expected, [string]$Message)

    if ($Actual -ne $Expected) {
        throw "${Message} 实际=${Actual}，期望=${Expected}。"
    }
}

$models = Get-ProjectSource 'ResourceManagement\ResourceProfileModels.cs'
$interfaces = Get-ProjectSource 'ResourceManagement\IResourceProfileServices.cs'
$probe = Get-ProjectSource 'ResourceManagement\WindowsHardwareResourceProbe.cs'
$calculator = Get-ProjectSource 'ResourceManagement\ResourceProfileCalculator.cs'
$provider = Get-ProjectSource 'ResourceManagement\RuntimeResourceProfileProvider.cs'
$solution = Get-ProjectSource 'Solution.cs'
$project = Get-ProjectSource 'TDJS-Vision.csproj'
$performanceSettings = Get-ProjectSource 'ResourceManagement\PerformanceResourceSettings.cs'

Assert-Contains $interfaces 'interface IHardwareResourceProbe' '硬件读取必须通过可替换接口。'
Assert-Contains $interfaces 'interface IResourceProfileCalculator' '资源公式必须通过可替换接口。'
Assert-Contains $interfaces 'interface IRuntimeResourceProfileProvider' '运行档案必须通过可替换接口。'
Assert-Contains $probe 'Win32_Processor' 'Windows探针必须读取物理核心。'
Assert-Contains $probe 'ProcessorAffinity' 'Windows探针必须读取进程可用逻辑处理器。'
Assert-Contains $probe 'Win32_OperatingSystem' 'Windows探针必须读取总内存和可用内存。'
Assert-Contains $probe 'Win32_VideoController' 'Windows探针必须读取GPU诊断信息。'
Assert-Contains $probe 'AvailableFreeSpace' 'Windows探针必须读取程序磁盘空间。'
Assert-Contains $provider 'HardwareResourceSnapshot.CreateFallback' '硬件探针失败不能阻止软件启动。'
Assert-Contains $provider 'PerformanceResourceSettingsProvider.Default' '生产资源档案必须读取本机性能覆盖值。'
Assert-Contains $performanceSettings 'ResourceProfileOverrideResolver' '自动档案和最终档案必须由独立覆盖合并器隔离。'
Assert-Contains $calculator 'AiConcurrencyPolicy = "FollowEditableFlow"' '自动资源档案不能增加全局AI并发限制。'
Assert-Contains $calculator 'UiMaxRefreshFps = Clamp(' 'UI最大刷新帧率必须经过安全范围限制。'
Assert-Contains $calculator 'SaveDiskLowSpaceMb = saveDiskLowSpaceMb' '资源档案必须输出普通图片磁盘低水位。'
Assert-Contains $calculator 'SaveDiskCriticalSpaceMb = saveDiskCriticalSpaceMb' '资源档案必须输出全部图片磁盘严重水位。'
Assert-Contains $calculator 'SaveSlowThresholdMs = Clamp(' '资源档案必须限制慢写阈值范围。'
Assert-Contains $solution 'RefreshRuntimeResourceProfile(true);' '首个方案运行会话必须在节点执行前刷新资源档案。'
Assert-Contains $solution 'AverageImageBytes = _imageSaveSizeSampler.AverageBytes' '运行资源档案必须读取真实滚动图像字节样本。'
Assert-Contains $solution '_imageSaveSizeSampler.Reset();' '切换方案时必须清空旧方案图像大小样本。'
Assert-Contains $solution '不修改线程池、CPU亲和性或AI并发' '保存工作池接入后仍不能修改线程池、CPU亲和性或AI并发。'
Assert-Contains $solution 'new CachedImageSaveStorageGuard(' '真实共享保存工作池必须接入磁盘保护器。'
Assert-Contains $solution 'GetImageSaveQueueDiagnosticsSnapshot()' 'Solution必须公开保存工作池只读诊断快照。'
Assert-Contains $project '<Compile Include="ResourceManagement\ResourceProfileCalculator.cs" />' '项目必须编译资源档案计算器。'
Assert-NotContains $solution 'ThreadPool.Set' '步骤3不能修改全局线程池。'
Assert-NotContains $probe 'ProcessorAffinity =' '步骤3只能读取CPU亲和性，不能修改CPU亲和性。'
Assert-NotContains $calculator 'SemaphoreSlim' '步骤3不能给AI或节点增加并发信号量。'

$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$application = Get-Item -LiteralPath (Join-Path $debugDirectory '机器视觉AI检测系统V1.0.exe') -ErrorAction SilentlyContinue
if ($null -eq $application) {
    throw '找不到Debug|x64编译产物，不能跳过硬件资源档案运行校验。'
}

[Environment]::CurrentDirectory = $debugDirectory
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$hardwareType = $assembly.GetType('TDJS_Vision.ResourceManagement.HardwareResourceSnapshot', $true)
$workloadType = $assembly.GetType('TDJS_Vision.ResourceManagement.ResourceWorkloadSnapshot', $true)
$optionsType = $assembly.GetType('TDJS_Vision.ResourceManagement.ResourceProfileCalculationOptions', $true)
$calculatorType = $assembly.GetType('TDJS_Vision.ResourceManagement.ResourceProfileCalculator', $true)
$probeType = $assembly.GetType('TDJS_Vision.ResourceManagement.WindowsHardwareResourceProbe', $true)

function New-Profile {
    param(
        [int]$PhysicalCores,
        [int]$LogicalProcessors,
        [int]$AvailableLogicalProcessors,
        [long]$TotalMemoryMb,
        [long]$AvailableMemoryMb,
        [int]$EnabledProcesses,
        [int]$CameraCount,
        [long]$AverageImageBytes
    )

    $hardware = [Activator]::CreateInstance($hardwareType)
    $hardware.PhysicalCoreCount = $PhysicalCores
    $hardware.LogicalProcessorCount = $LogicalProcessors
    $hardware.AvailableLogicalProcessorCount = $AvailableLogicalProcessors
    $hardware.TotalMemoryMb = $TotalMemoryMb
    $hardware.AvailableMemoryMb = $AvailableMemoryMb
    $workload = [Activator]::CreateInstance($workloadType)
    $workload.EnabledProcessCount = $EnabledProcesses
    $workload.CameraCount = $CameraCount
    $workload.AverageImageBytes = $AverageImageBytes
    $options = [Activator]::CreateInstance($optionsType)
    $calculatorInstance = [Activator]::CreateInstance($calculatorType)
    return $calculatorType.GetMethod('Calculate').Invoke($calculatorInstance, @($hardware, $workload, $options))
}

$standard = New-Profile 16 24 24 16384 10000 4 4 (12MB)
Assert-Equal $standard.CpuReserveCoreCount 4 '16核电脑应保留4个物理核心。'
Assert-Equal $standard.CpuHeavyMaxConcurrency 4 '四流程CPU重任务并发应为4。'
Assert-Equal $standard.CpuHighWatermarkPercent 85 'CPU高水位默认必须为85%。'
Assert-Equal $standard.CpuRecoveryWatermarkPercent 65 'CPU恢复水位默认必须为65%。'
Assert-Equal $standard.CpuHighDurationMs 5000 'CPU高水位默认必须持续5000ms。'
Assert-Equal $standard.CpuRecoveryDurationMs 10000 'CPU恢复水位默认必须持续10000ms。'
Assert-Equal $standard.ResourceAdjustmentIntervalMs 5000 'CPU资源评估默认周期必须为5000ms。'
Assert-Equal $standard.ResourceAdjustmentCooldownMs 30000 'CPU并发调整默认必须冷却30000ms。'
Assert-Equal $standard.ConcurrencyAdjustmentStep 1 'CPU并发每次默认只能调整1。'
Assert-Equal $standard.ImageConvertMaxConcurrency 4 '四相机图像转换并发应为4。'
Assert-Equal $standard.ImageSaveWorkerCount 2 '16核电脑保存线程建议应为2。'
Assert-Equal $standard.FlowImageMemoryBudgetMb 2048 '流程图像预算应受2048MB上限约束。'
Assert-Equal $standard.SaveQueueMemoryBudgetMb 512 '保存队列预算应受512MB上限约束。'
Assert-Equal $standard.SaveQueueCalculatedCapacity 42 '12MB平均任务对应512MB预算时队列容量应为42。'
Assert-Equal $standard.AiConcurrencyPolicy 'FollowEditableFlow' 'AI策略必须跟随可编辑流程。'
Assert-Equal $standard.UiMaxRefreshFps 30 '每个图像窗口默认最大刷新帧率应为30。'
Assert-Equal $standard.ShutdownResourceDrainTimeoutMs 5000 '后台资源停止排空默认必须等待5000ms。'
Assert-Equal $standard.ImageSizeSampleCount 20 '图像资源滚动采样默认必须保留20个样本。'
Assert-Equal $standard.SaveDiskLowSpaceMb 2048 '普通图片磁盘低水位默认必须为2048MB。'
Assert-Equal $standard.SaveDiskCriticalSpaceMb 512 '全部图片磁盘严重水位默认必须为512MB。'
Assert-Equal $standard.SaveSlowThresholdMs 500 '单路径慢写阈值默认必须为500ms。'
Assert-Equal $standard.SaveDiskProbeIntervalMs 1000 '磁盘空间探测默认必须缓存1000ms。'

$manyCameras = New-Profile 16 24 24 16384 10000 16 16 (12MB)
Assert-Equal $manyCameras.CpuHeavyMaxConcurrency 8 '十六流程不得把CPU重任务并发扩张到硬件上限之外。'
Assert-Equal $manyCameras.ImageConvertMaxConcurrency 4 '十六台相机必须按物理核心和转换上限收敛到4路转换。'

$manyCamerasSmallCpu = New-Profile 4 8 8 8192 4096 16 16 (12MB)
Assert-Equal $manyCamerasSmallCpu.ImageConvertMaxConcurrency 1 '低核心电脑连接十六台相机时必须保持单路转换。'

$singleProcess = New-Profile 16 24 24 16384 10000 1 4 (12MB)
Assert-Equal $singleProcess.CpuHeavyMaxConcurrency 1 '单流程CPU重任务并发应为1。'
Assert-Equal $singleProcess.ImageConvertMaxConcurrency 1 '图像转换自动并发不得超过共享CPU总并发。'

$small = New-Profile 2 2 2 4096 1024 4 4 (50MB)
Assert-Equal $small.CpuReserveCoreCount 1 '双核电脑最多只能保留1个核心。'
Assert-Equal $small.CpuHeavyMaxConcurrency 1 '双核电脑CPU重任务并发应降为1。'
Assert-Equal $small.ImageConvertMaxConcurrency 1 '双核电脑图像转换并发应降为1。'
Assert-Equal $small.ImageSaveWorkerCount 1 '双核电脑保存线程应为1。'
Assert-Equal $small.FlowImageMemoryBudgetMb 256 '低可用内存应把流程图像预算压到256MB。'
Assert-Equal $small.SaveQueueMemoryBudgetMb 102 '低可用内存应把保存预算压到102MB。'
Assert-Equal $small.SaveQueueCalculatedCapacity 4 '保存队列容量不得低于计划下限4。'

$unsampled = New-Profile 8 16 16 8192 4096 0 0 0
Assert-Equal $unsampled.CpuHeavyMaxConcurrency 1 '没有启用流程时建议并发仍必须至少为1。'
Assert-Equal $unsampled.ImageConvertMaxConcurrency 1 '没有相机时建议转换并发仍必须至少为1。'
Assert-Equal $unsampled.SaveQueueCalculatedCapacity 8 '未采样图像时必须使用初始队列容量8。'
Assert-Equal $unsampled.UsesInitialSaveQueueCapacity $true '未采样图像时必须标记初始容量。'

$affinityLimited = New-Profile 16 24 2 16384 10000 4 4 (12MB)
Assert-Equal $affinityLimited.CpuHeavyMaxConcurrency 2 '进程亲和性受限时CPU重任务并发不能超过可用处理器。'
Assert-Equal $affinityLimited.ImageConvertMaxConcurrency 1 '进程亲和性受限时图像转换并发必须降级。'
Assert-Equal $affinityLimited.ImageSaveWorkerCount 1 '进程亲和性受限时保存线程必须降级。'

$probeInstance = [Activator]::CreateInstance($probeType)
$actualHardware = $probeType.GetMethod('Capture').Invoke($probeInstance, @())
if ($actualHardware.PhysicalCoreCount -lt 1 -or $actualHardware.LogicalProcessorCount -lt 1 -or $actualHardware.TotalMemoryMb -lt 1) {
    throw '本机硬件探针返回了无效核心数或内存。'
}
if ($actualHardware.ProbeWarnings.Count -gt 0) {
    throw ('本机硬件探针发生降级：' + [string]::Join('；', [string[]]$actualHardware.ProbeWarnings))
}

$actualWorkload = [Activator]::CreateInstance($workloadType)
$actualWorkload.EnabledProcessCount = 4
$actualWorkload.CameraCount = 4
$actualWorkload.AverageImageBytes = 0
$actualOptions = [Activator]::CreateInstance($optionsType)
$actualCalculator = [Activator]::CreateInstance($calculatorType)
$actualProfile = $calculatorType.GetMethod('Calculate').Invoke($actualCalculator, @($actualHardware, $actualWorkload, $actualOptions))

Write-Host ('硬件资源档案检查通过：' + $actualHardware.ToLogText())
Write-Host ('本机四流程四相机自动建议：' + $actualProfile.ToLogText())
