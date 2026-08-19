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

Assert-Contains $interfaces 'interface IHardwareResourceProbe' '硬件读取必须通过可替换接口。'
Assert-Contains $interfaces 'interface IResourceProfileCalculator' '资源公式必须通过可替换接口。'
Assert-Contains $interfaces 'interface IRuntimeResourceProfileProvider' '运行档案必须通过可替换接口。'
Assert-Contains $probe 'Win32_Processor' 'Windows探针必须读取物理核心。'
Assert-Contains $probe 'ProcessorAffinity' 'Windows探针必须读取进程可用逻辑处理器。'
Assert-Contains $probe 'Win32_OperatingSystem' 'Windows探针必须读取总内存和可用内存。'
Assert-Contains $probe 'Win32_VideoController' 'Windows探针必须读取GPU诊断信息。'
Assert-Contains $probe 'AvailableFreeSpace' 'Windows探针必须读取程序磁盘空间。'
Assert-Contains $provider 'HardwareResourceSnapshot.CreateFallback' '硬件探针失败不能阻止软件启动。'
Assert-Contains $calculator 'AiConcurrencyPolicy = "FollowEditableFlow"' '自动资源档案不能增加全局AI并发限制。'
Assert-Contains $solution 'RefreshRuntimeResourceProfile();' '方案运行前必须刷新资源建议档案。'
Assert-Contains $solution 'AverageImageBytes = 0' '尚未采样图像时必须明确使用初始保存队列容量。'
Assert-Contains $solution '尚未修改线程池、CPU亲和性或AI并发' '步骤3不能提前改变运行策略。'
Assert-Contains $project '<Compile Include="ResourceManagement\ResourceProfileCalculator.cs" />' '项目必须编译资源档案计算器。'
Assert-NotContains $solution 'ThreadPool.Set' '步骤3不能修改全局线程池。'
Assert-NotContains $probe 'ProcessorAffinity =' '步骤3只能读取CPU亲和性，不能修改CPU亲和性。'
Assert-NotContains $calculator 'SemaphoreSlim' '步骤3不能给AI或节点增加并发信号量。'

$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$application = Get-Item -LiteralPath (Join-Path $debugDirectory '机器视觉AI检测系统V1.0.exe') -ErrorAction SilentlyContinue
if ($null -eq $application) {
    Write-Host '硬件资源档案源码结构检查通过；编译后再执行公式运行检查。'
    exit 0
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
Assert-Equal $standard.ImageConvertMaxConcurrency 4 '四相机图像转换并发应为4。'
Assert-Equal $standard.ImageSaveWorkerCount 2 '16核电脑保存线程建议应为2。'
Assert-Equal $standard.FlowImageMemoryBudgetMb 2048 '流程图像预算应受2048MB上限约束。'
Assert-Equal $standard.SaveQueueMemoryBudgetMb 512 '保存队列预算应受512MB上限约束。'
Assert-Equal $standard.SaveQueueCalculatedCapacity 42 '12MB平均任务对应512MB预算时队列容量应为42。'
Assert-Equal $standard.AiConcurrencyPolicy 'FollowEditableFlow' 'AI策略必须跟随可编辑流程。'

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
