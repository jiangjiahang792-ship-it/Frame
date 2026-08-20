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

function Assert-Equal {
    param($Actual, $Expected, [string]$Message)

    if ($Actual -ne $Expected) {
        throw "${Message} 实际=${Actual}，期望=${Expected}。"
    }
}

function Assert-True {
    param([bool]$Condition, [string]$Message)

    if (-not $Condition) {
        throw $Message
    }
}

$settingsSource = Get-ProjectSource 'ResourceManagement\PerformanceResourceSettings.cs'
$providerSource = Get-ProjectSource 'ResourceManagement\RuntimeResourceProfileProvider.cs'
$schedulerSource = Get-ProjectSource 'ResourceManagement\CpuWorkScheduler.cs'
$projectSource = Get-ProjectSource 'TDJS-Vision.csproj'

Assert-Contains $settingsSource 'Environment.SpecialFolder.LocalApplicationData' '机器性能配置必须保存在本机目录，不能进入方案文件。'
Assert-Contains $settingsSource 'File.Replace(temporaryPath, _filePath, null, true)' '机器性能配置覆盖保存必须采用无备份原子替换。'
Assert-Contains $settingsSource 'Interlocked.CompareExchange(ref _settings, null, null)' '正常运行时读取机器配置不得等待配置写盘锁。'
Assert-Contains $settingsSource 'ResourceProfileOverrideResolver' '自动值与手动值必须通过独立合并器生成最终档案。'
Assert-Contains $providerSource 'PerformanceResourceSettingsProvider.Default' '生产资源档案提供器必须接入机器设置快照。'
Assert-Contains $providerSource 'BuildProfileResult' '资源档案提供器必须同时公开自动值和最终值。'
Assert-Contains $schedulerSource 'DynamicAdjustmentEnabled' '自适应开关必须真实进入CPU动态调度。'
Assert-Contains $projectSource '<Compile Include="ResourceManagement\PerformanceResourceSettings.cs" />' '项目必须编译机器性能设置实现。'

$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$application = Get-Item -LiteralPath (Join-Path $debugDirectory '机器视觉AI检测系统V1.0.exe') -ErrorAction SilentlyContinue
if ($null -eq $application) {
    throw '找不到Debug|x64编译产物，不能跳过机器性能配置运行校验。'
}

[Environment]::CurrentDirectory = $debugDirectory
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$hardwareType = $assembly.GetType('TDJS_Vision.ResourceManagement.HardwareResourceSnapshot', $true)
$workloadType = $assembly.GetType('TDJS_Vision.ResourceManagement.ResourceWorkloadSnapshot', $true)
$optionsType = $assembly.GetType('TDJS_Vision.ResourceManagement.ResourceProfileCalculationOptions', $true)
$calculatorType = $assembly.GetType('TDJS_Vision.ResourceManagement.ResourceProfileCalculator', $true)
$settingsType = $assembly.GetType('TDJS_Vision.ResourceManagement.MachinePerformanceResourceSettings', $true)
$resolverType = $assembly.GetType('TDJS_Vision.ResourceManagement.ResourceProfileOverrideResolver', $true)
$storeType = $assembly.GetType('TDJS_Vision.ResourceManagement.JsonPerformanceResourceSettingsStore', $true)
$settingsProviderType = $assembly.GetType('TDJS_Vision.ResourceManagement.PerformanceResourceSettingsProvider', $true)
$storeInterfaceType = $assembly.GetType('TDJS_Vision.ResourceManagement.IPerformanceResourceSettingsStore', $true)
$schedulerOptionsType = $assembly.GetType('TDJS_Vision.ResourceManagement.CpuWorkSchedulerOptions', $true)
$policyType = $assembly.GetType('TDJS_Vision.ResourceManagement.CpuConcurrencyAdjustmentPolicy', $true)

$hardware = [Activator]::CreateInstance($hardwareType)
$hardware.PhysicalCoreCount = 16
$hardware.LogicalProcessorCount = 24
$hardware.AvailableLogicalProcessorCount = 24
$hardware.TotalMemoryMb = 16384
$hardware.AvailableMemoryMb = 10000

$workload = [Activator]::CreateInstance($workloadType)
$workload.EnabledProcessCount = 4
$workload.CameraCount = 4
$workload.AverageImageBytes = 12MB

$options = [Activator]::CreateInstance($optionsType)
$calculator = [Activator]::CreateInstance($calculatorType)
$resolver = [Activator]::CreateInstance($resolverType, @($calculator))
$settings = [Activator]::CreateInstance($settingsType)
$settings.AdaptiveResourceManagementEnabled = $false
$settings.ManualOverrides['CpuReserveCoreCount'] = '2'
$settings.ManualOverrides['CpuHeavyMaxConcurrency'] = '6'
$settings.ManualOverrides['ImageConvertMaxConcurrency'] = '8'
$settings.ManualOverrides['ImageSaveWorkerCount'] = '3'
$settings.ManualOverrides['CpuHighWatermarkPercent'] = '60'
$settings.ManualOverrides['CpuRecoveryWatermarkPercent'] = '99'
$settings.ManualOverrides['FlowImageMemoryBudgetPercent'] = '10'
$settings.ManualOverrides['SaveQueueMemoryBudgetPercent'] = '2'
$settings.ManualOverrides['MemoryLowWatermarkPercent'] = '10'
$settings.ManualOverrides['MemoryCriticalWatermarkPercent'] = '30'
$settings.ManualOverrides['SaveQueueInitialCapacity'] = '50'
$settings.ManualOverrides['SaveQueueMaximumCapacity'] = '3'
$settings.ManualOverrides['SaveDiskLowSpaceMb'] = '256'
$settings.ManualOverrides['SaveDiskCriticalSpaceMb'] = '999'

$buildResult = $resolverType.GetMethod('Build').Invoke($resolver, @($hardware, $workload, $options, $settings))
$automatic = $buildResult.AutomaticProfile
$effective = $buildResult.EffectiveProfile

Assert-Equal $automatic.CpuReserveCoreCount 4 '自动档案不得被手动值污染。'
Assert-Equal $automatic.CpuHeavyMaxConcurrency 4 '自动CPU并发必须保持硬件公式结果。'
Assert-Equal $effective.CpuReserveCoreCount 2 '最终档案必须应用直接保留核心覆盖。'
Assert-Equal $effective.CpuHeavyMaxConcurrency 6 '最终档案必须应用CPU重任务并发覆盖。'
Assert-Equal $effective.ImageConvertMaxConcurrency 6 '图像转换并发不得超过共享CPU重任务额度。'
Assert-Equal $effective.ImageSaveWorkerCount 3 '最终档案必须应用保存工作线程覆盖。'
Assert-Equal $effective.CpuHighWatermarkPercent 60 'CPU高水位必须应用手动值。'
Assert-Equal $effective.CpuRecoveryWatermarkPercent 55 'CPU恢复水位必须至少低于高水位5个百分点。'
Assert-Equal $effective.FlowImageMemoryBudgetMb 1638 '流程图像预算百分比必须参与最终预算计算。'
Assert-Equal $effective.SaveQueueMemoryBudgetMb 327 '保存队列预算百分比必须参与最终预算计算。'
Assert-Equal $effective.MemoryLowWatermarkMb 1639 '内存低水位百分比必须参与最终档案计算。'
Assert-Equal $effective.MemoryCriticalWatermarkMb 1311 '严重水位百分比必须受低水位减2个百分点约束。'
Assert-Equal $effective.SaveQueueCalculatedCapacity 3 '初始容量和最大容量必须先执行关联边界再计算。'
Assert-Equal $effective.SaveDiskLowSpaceMb 256 '磁盘低水位必须应用安全下限。'
Assert-Equal $effective.SaveDiskCriticalSpaceMb 255 '磁盘严重水位必须严格低于低水位。'
Assert-Equal $effective.AdaptiveResourceManagementEnabled $false '自适应关闭必须进入最终档案。'
Assert-Equal $buildResult.AutomaticParameterValues['CpuHeavyMaxConcurrency'] '4' '界面自动参数字典必须使用稳定配置键。'
Assert-Equal $buildResult.EffectiveParameterValues['CpuHeavyMaxConcurrency'] '6' '界面最终参数字典必须反映安全收敛后的值。'
Assert-Equal $buildResult.EffectiveParameterValues['MemoryCriticalWatermarkPercent'] '8' '界面最终参数值必须反映关联百分比收敛。'

$emptySettings = [Activator]::CreateInstance($settingsType)
$customOptions = [Activator]::CreateInstance($optionsType)
$customOptions.CpuHeavyConcurrencyMaximum = 2
$pureAutomaticResult = $resolverType.GetMethod('Build').Invoke($resolver, @($hardware, $workload, $customOptions, $emptySettings))
Assert-Equal $pureAutomaticResult.AutomaticProfile.CpuHeavyMaxConcurrency 2 '自定义自动并发上限必须进入自动档案。'
Assert-Equal $pureAutomaticResult.EffectiveProfile.CpuHeavyMaxConcurrency 2 '无手动覆盖时最终档案必须严格保持自动上限。'
Assert-Equal $pureAutomaticResult.EffectiveProfile.ToLogText() $pureAutomaticResult.AutomaticProfile.ToLogText() '无手动覆盖时自动值与最终值必须完全一致。'

$singleProcessWorkload = [Activator]::CreateInstance($workloadType)
$singleProcessWorkload.EnabledProcessCount = 1
$singleProcessWorkload.CameraCount = 4
$singleProcessResult = $resolverType.GetMethod('Build').Invoke($resolver, @($hardware, $singleProcessWorkload, $options, $emptySettings))
Assert-Equal $singleProcessResult.AutomaticProfile.CpuHeavyMaxConcurrency 1 '单流程自动CPU总并发必须为1。'
Assert-Equal $singleProcessResult.AutomaticProfile.ImageConvertMaxConcurrency 1 '自动图像转换并发不得超过共享CPU总并发。'
Assert-Equal $singleProcessResult.EffectiveProfile.ImageConvertMaxConcurrency 1 '纯自动最终转换并发必须等于真实调度上限。'

$cpuOnlySettings = [Activator]::CreateInstance($settingsType)
$cpuOnlySettings.ManualOverrides['CpuHeavyMaxConcurrency'] = '1'
$cpuOnlyResult = $resolverType.GetMethod('Build').Invoke($resolver, @($hardware, $workload, $options, $cpuOnlySettings))
Assert-Equal $cpuOnlyResult.EffectiveProfile.ImageConvertMaxConcurrency 1 '只降低CPU总并发时图像转换并发必须同步收敛。'

$highWatermarkOnlySettings = [Activator]::CreateInstance($settingsType)
$highWatermarkOnlySettings.ManualOverrides['CpuHighWatermarkPercent'] = '50'
$highWatermarkOnlyResult = $resolverType.GetMethod('Build').Invoke($resolver, @($hardware, $workload, $options, $highWatermarkOnlySettings))
Assert-Equal $highWatermarkOnlyResult.EffectiveProfile.CpuRecoveryWatermarkPercent 45 '只降低CPU高水位时恢复水位必须同步收敛。'

$reserveOnlySettings = [Activator]::CreateInstance($settingsType)
$reserveOnlySettings.ManualOverrides['CpuReserveCoreCount'] = '15'
$reserveOnlyResult = $resolverType.GetMethod('Build').Invoke($resolver, @($hardware, $workload, $options, $reserveOnlySettings))
Assert-Equal $reserveOnlyResult.EffectiveProfile.CpuHeavyMaxConcurrency 1 '只手动增加保留核心时，自动CPU并发必须同步降低。'

$schedulerOptions = $schedulerOptionsType.GetMethod('FromProfile').Invoke($null, @($effective))
Assert-Equal $schedulerOptions.DynamicAdjustmentEnabled $false 'CPU调度参数必须接收自适应关闭状态。'
$policy = [Activator]::CreateInstance($policyType, @($schedulerOptions, 6))
[void]$policyType.GetMethod('Observe').Invoke($policy, @(100.0, [long]0))
[void]$policyType.GetMethod('Observe').Invoke($policy, @(100.0, [long]60000))
Assert-Equal $policy.CurrentConcurrency 6 '自适应关闭后高CPU不得动态降低固定并发。'

$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ('TDJS-Vision-PerformanceSettings-' + [Guid]::NewGuid().ToString('N'))
$settingsPath = Join-Path $temporaryDirectory 'settings.json'
try {
    $storeConstructor = $storeType.GetConstructor([Type[]]@([string]))
    $store = $storeConstructor.Invoke([object[]]@([string]$settingsPath))
    $storeType.GetMethod('Save').Invoke($store, @($settings))
    $loaded = $storeType.GetMethod('Load').Invoke($store, @())
    Assert-Equal $loaded.AdaptiveResourceManagementEnabled $false 'JSON往返必须保留自适应开关。'
    Assert-Equal $loaded.ManualOverrides['CpuHeavyMaxConcurrency'] '6' 'JSON往返必须保留手动覆盖。'
    Assert-True (-not (Test-Path -LiteralPath ($settingsPath + '.tmp'))) '原子保存完成后不得残留临时文件。'
    Assert-True (-not (Test-Path -LiteralPath ($settingsPath + '.bak'))) '原子保存完成后不得残留备份文件。'

    $replacementSettings = $settings.Clone()
    $replacementSettings.ManualOverrides['CpuHeavyMaxConcurrency'] = '5'
    $storeType.GetMethod('Save').Invoke($store, @($replacementSettings))
    $replaced = $storeType.GetMethod('Load').Invoke($store, @())
    Assert-Equal $replaced.ManualOverrides['CpuHeavyMaxConcurrency'] '5' '第二次保存必须真实覆盖已有配置文件。'
    Assert-True (-not (Test-Path -LiteralPath ($settingsPath + '.tmp'))) '覆盖保存完成后不得残留临时文件。'
    Assert-True (-not (Test-Path -LiteralPath ($settingsPath + '.bak'))) '覆盖保存完成后不得残留备份文件。'

    $providerConstructor = $settingsProviderType.GetConstructor([Type[]]@($storeInterfaceType))
    $snapshotProvider = $providerConstructor.Invoke([object[]]@($store))
    $firstSnapshot = $settingsProviderType.GetMethod('GetSnapshot').Invoke($snapshotProvider, @())
    $firstSnapshot.ManualOverrides['CpuHeavyMaxConcurrency'] = '99'
    $secondSnapshot = $settingsProviderType.GetMethod('GetSnapshot').Invoke($snapshotProvider, @())
    Assert-Equal $secondSnapshot.ManualOverrides['CpuHeavyMaxConcurrency'] '5' '调用方修改快照不得污染提供器内部状态。'

    Set-Content -LiteralPath $settingsPath -Value '{bad json' -Encoding UTF8
    $settingsProvider = $providerConstructor.Invoke([object[]]@($store))
    $fallback = $settingsProviderType.GetMethod('GetSnapshot').Invoke($settingsProvider, @())
    $warning = $settingsProviderType.GetMethod('GetLastLoadWarning').Invoke($settingsProvider, @())
    Assert-Equal $fallback.AdaptiveResourceManagementEnabled $true '损坏配置必须降级到安全自动模式。'
    Assert-True (-not [string]::IsNullOrWhiteSpace($warning)) '损坏配置必须保留可诊断告警。'
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}

Write-Host '机器性能配置检查通过：自动/手动/最终档案分离，成对边界、自适应关闭、JSON原子保存和损坏降级均有效。'
