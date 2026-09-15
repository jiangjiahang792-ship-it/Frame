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

function Assert-True {
    param([bool]$Condition, [string]$Message)

    if (-not $Condition) {
        throw $Message
    }
}

$formSource = Get-ProjectSource 'Forms\SystemSetting\FrmSystemSetting.cs'
$definitionSource = Get-ProjectSource 'Forms\SystemSetting\PerformanceSettingDefinition.cs'
$designerSource = Get-ProjectSource 'Forms\SystemSetting\FrmSystemSetting.Designer.cs'
$solutionSource = Get-ProjectSource 'Solution.cs'
$formMainSource = Get-ProjectSource 'FormMain.cs'
$imageSaveSource = Get-ProjectSource 'Node\7-ResultProcessing\ImageSave\NodeSaveImage.cs'
$projectSource = Get-ProjectSource 'TDJS-Vision.csproj'

Assert-Contains $designerSource 'this.tabPagePerformance.Text = "性能与资源";' '性能与资源页必须位于设计器文件。'
Assert-Contains $designerSource 'this.dataGridViewPerformance.Columns.AddRange' '性能参数表格必须位于设计器文件。'
Assert-Contains $designerSource 'this.buttonSavePerformanceSettings.Text = "保存并应用";' '设计器必须提供中文保存并应用按钮。'
Assert-Contains $designerSource 'this.buttonRestoreAutomatic.Text = "恢复全部自动";' '设计器必须提供恢复全部自动按钮。'
Assert-Contains $formSource 'ConfigHelper.DeserializationCompletionEvent -= UpdateRunInterval;' '系统设置窗体关闭时必须解除全局事件订阅。'
Assert-Contains $formSource '_isLoadingGeneralSettings' '打开系统设置时不得重复保存常规配置或改写启动项。'
Assert-Contains $formSource '规划值，尚未启用内存硬管控' '未接入运行控制的内存参数必须明确标记为规划值。'
Assert-Contains $definitionSource 'NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint' '数字校验不得把点号或逗号误当成千位分隔符。'
Assert-Contains $formMainSource 'using (FrmSystemSetting systemSettingForm = new FrmSystemSetting())' '系统设置必须点击时创建，不能增加主程序启动耗时或保留过期预览。'
Assert-Contains $solutionSource 'ApplyMachinePerformanceResourceSettings(' '保存后必须通过Solution安全应用运行资源档案。'
Assert-Contains $solutionSource 'ImageSaveProfileApplyState.Pending' '保存工作池繁忙时必须返回待应用状态。'
Assert-Contains $solutionSource 'if (_isResetting)' '方案重置期间必须阻止性能参数热更新。'
Assert-Contains $imageSaveSource '_shutdownTimeoutMs == shutdownTimeoutMs' '停止排空等待变化必须触发保存工作池安全重配。'
Assert-Contains $projectSource '<Compile Include="Forms\SystemSetting\PerformanceSettingDefinition.cs" />' '参数定义必须独立编译，避免破坏WinForms资源绑定。'

$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$application = Get-Item -LiteralPath (Join-Path $debugDirectory '机器视觉AI检测系统V1.0.exe') -ErrorAction SilentlyContinue
if ($null -eq $application) {
    throw '找不到Debug|x64编译产物，不能跳过系统设置界面运行校验。'
}
if ([Threading.Thread]::CurrentThread.ApartmentState -ne [Threading.ApartmentState]::STA) {
    throw '系统设置界面测试必须使用STA模式运行。'
}

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class SystemSettingsNativeWindow
{
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr handle, int command);
}
'@

[Environment]::CurrentDirectory = $debugDirectory
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$resourceNames = $assembly.GetManifestResourceNames()
Assert-True ($resourceNames -contains 'TDJS_Vision.Forms.SystemSetting.FrmSystemSetting.resources') 'FrmSystemSetting资源必须绑定到窗体类型。'
Assert-True (-not ($resourceNames -contains 'TDJS_Vision.Forms.SystemSetting.PerformanceSettingDefinition.resources')) '参数定义不得截获FrmSystemSetting资源。'

$formType = $assembly.GetType('TDJS_Vision.Forms.SystemSetting.FrmSystemSetting', $true)
$bindingFlags = [Reflection.BindingFlags]'Instance,NonPublic'
$settingsType = $assembly.GetType('TDJS_Vision.ResourceManagement.MachinePerformanceResourceSettings', $true)
$storeType = $assembly.GetType('TDJS_Vision.ResourceManagement.JsonPerformanceResourceSettingsStore', $true)
$providerType = $assembly.GetType('TDJS_Vision.ResourceManagement.PerformanceResourceSettingsProvider', $true)
$storeInterfaceType = $assembly.GetType('TDJS_Vision.ResourceManagement.IPerformanceResourceSettingsStore', $true)
$hardwareProbeType = $assembly.GetType('TDJS_Vision.ResourceManagement.WindowsHardwareResourceProbe', $true)
$hardwareProbe = [Activator]::CreateInstance($hardwareProbeType)
$hardware = $hardwareProbeType.GetMethod('Capture').Invoke($hardwareProbe, @())
$seedReserveCoreCount = [Math]::Max(1, [Math]::Min(4, $hardware.PhysicalCoreCount - 1))
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ('TDJS-Vision-SystemSettings-' + [Guid]::NewGuid().ToString('N'))
$settingsPath = Join-Path $temporaryDirectory 'performance-resource-settings.json'
$store = $storeType.GetConstructor([Type[]]@([string])).Invoke([object[]]@([string]$settingsPath))
$seedSettings = [Activator]::CreateInstance($settingsType)
$seedSettings.ManualOverrides['CpuReserveCoreCount'] = $seedReserveCoreCount.ToString([Globalization.CultureInfo]::InvariantCulture)
$storeType.GetMethod('Save').Invoke($store, @($seedSettings))
$providerConstructor = $providerType.GetConstructor([Type[]]@($storeInterfaceType))
$provider = $providerConstructor.Invoke([object[]]@($store))
$formConstructor = $formType.GetConstructor(
    [Reflection.BindingFlags]'Instance,NonPublic',
    $null,
    [Type[]]@($providerType),
    $null)
$form = $formConstructor.Invoke([object[]]@($provider))
try {
    $settingsHashBeforeInteraction = (Get-FileHash -LiteralPath $settingsPath -Algorithm SHA256).Hash
    Assert-True (-not $formType.GetField('_hasUnsavedPerformanceChanges', $bindingFlags).GetValue($form)) '加载合法的保留核心值4不得误判为首次预览错误。'
    Assert-True ($null -ne $formType.GetField('_lastPerformancePreview', $bindingFlags).GetValue($form)) '合法的保留核心值4必须成功生成首次预览。'

    $form.StartPosition = [Windows.Forms.FormStartPosition]::Manual
    $form.Location = New-Object Drawing.Point(-32000, -32000)
    $form.ShowInTaskbar = $false
    $form.Size = New-Object Drawing.Size(1280, 760)
    $formType.GetMethod('OnLoad', $bindingFlags).Invoke($form, @([EventArgs]::Empty))
    Assert-True $formType.GetField('_isRunIntervalEventSubscribed', $bindingFlags).GetValue($form) 'OnLoad必须订阅方案反序列化事件。'
    [void][SystemSettingsNativeWindow]::ShowWindow($form.Handle, 4)
    [Windows.Forms.Application]::DoEvents()

    $tabControl = $formType.GetField('tabControlSettings', $bindingFlags).GetValue($form)
    $performancePage = $formType.GetField('tabPagePerformance', $bindingFlags).GetValue($form)
    $grid = $formType.GetField('dataGridViewPerformance', $bindingFlags).GetValue($form)
    $hardwareGroup = $formType.GetField('groupBoxPerformanceHardware', $bindingFlags).GetValue($form)
    $actions = $formType.GetField('flowLayoutPanelPerformanceActions', $bindingFlags).GetValue($form)
    $statusLabel = $formType.GetField('labelPerformanceStatus', $bindingFlags).GetValue($form)
    $hardwareLabel = $formType.GetField('labelHardwareSummary', $bindingFlags).GetValue($form)

    $tabControl.SelectedTab = $performancePage
    [Windows.Forms.Application]::DoEvents()

    Assert-True ($tabControl.TabPages.Count -eq 2) '系统设置必须保留常规设置和性能与资源两个页签。'
    Assert-True ($grid.Columns.Count -eq 8) '性能参数表格必须完整显示8列。'
    Assert-True ($grid.Rows.Count -eq 26) '性能参数表格必须显示26项可配置参数。'
    Assert-True ($grid.ClientSize.Height -gt 250) '性能参数表格必须保留足够的滚动查看高度。'
    Assert-True ($hardwareGroup.Bottom -le $actions.Top) '硬件摘要与操作栏不得重叠。'
    Assert-True ($actions.Bottom -le $grid.Top) '操作栏与参数表格不得重叠。'
    Assert-True ($grid.Bottom -le $statusLabel.Top) '参数表格与底部状态不得重叠。'
    Assert-True (-not [string]::IsNullOrWhiteSpace($hardwareLabel.Text)) '硬件摘要不得为空。'
    Assert-True (-not [string]::IsNullOrWhiteSpace($statusLabel.Text)) '性能设置状态不得为空。'
    Assert-True ($grid.Rows[0].Cells['columnManualValue'].Value -eq $seedSettings.ManualOverrides['CpuReserveCoreCount']) '合法的机器保留核心值必须在首次打开时完整恢复。'
    Assert-True ((Get-FileHash -LiteralPath $settingsPath -Algorithm SHA256).Hash -eq $settingsHashBeforeInteraction) '构造、OnLoad和草稿预览不得写入机器配置。'

    $grid.Rows[4].Cells['columnManualEnabled'].Value = $true
    $grid.Rows[4].Cells['columnManualValue'].Value = '80'
    [Windows.Forms.Application]::DoEvents()
    Assert-True ((Get-FileHash -LiteralPath $settingsPath -Algorithm SHA256).Hash -eq $settingsHashBeforeInteraction) '编辑草稿不得提前写入机器配置。'
    $formType.GetMethod('buttonSavePerformanceSettings_Click', $bindingFlags).Invoke($form, @($null, [EventArgs]::Empty))
    $savedSettings = $storeType.GetMethod('Load').Invoke($store, @())
    Assert-True ($savedSettings.ManualOverrides['CpuReserveCoreCount'] -eq $seedSettings.ManualOverrides['CpuReserveCoreCount']) '保存并应用不得改写未编辑的保留核心值。'
    Assert-True ($savedSettings.ManualOverrides['CpuHighWatermarkPercent'] -eq '80') '保存并应用按钮必须真实写入当前机器配置。'
    Assert-True ((Get-FileHash -LiteralPath $settingsPath -Algorithm SHA256).Hash -ne $settingsHashBeforeInteraction) '保存并应用后机器配置文件必须发生变化。'

    $solutionType = $assembly.GetType('TDJS_Vision.Solution', $true)
    $settings = [Activator]::CreateInstance($settingsType)
    $settings.AdaptiveResourceManagementEnabled = $false
    $settings.ManualOverrides['CpuHeavyMaxConcurrency'] = '1'
    $solution = $solutionType.GetProperty('Instance').GetValue($null, $null)
    $applyResult = $solutionType.GetMethod('ApplyMachinePerformanceResourceSettings').Invoke($solution, @($settings))
    Assert-True $applyResult.Success '机器设置草稿必须能够应用到运行资源档案。'
    Assert-True $applyResult.CpuAndDisplayApplied 'CPU和显示参数必须返回已应用状态。'
    Assert-True ($applyResult.EffectiveProfile.CpuHeavyMaxConcurrency -eq 1) '运行资源档案必须接收界面草稿中的CPU并发。'
    Assert-True (-not $applyResult.EffectiveProfile.AdaptiveResourceManagementEnabled) 'CPU动态并发开关必须进入当前运行档案。'
    Assert-True ([int]$applyResult.ImageSaveState -eq 0) '保存工作池尚未创建时必须明确返回创建时使用最新档案。'

    $isResettingField = $solutionType.GetField('_isResetting', $bindingFlags)
    $isResettingField.SetValue($solution, $true)
    try {
        $resetApplyResult = $solutionType.GetMethod('ApplyMachinePerformanceResourceSettings').Invoke($solution, @($settings))
        Assert-True $resetApplyResult.Success '重置期间保存成功应返回可等待状态，而不是丢失设置。'
        Assert-True (-not $resetApplyResult.CpuAndDisplayApplied) '方案重置期间不得热更新CPU和显示组件。'
        Assert-True ([int]$resetApplyResult.ImageSaveState -eq 2) '方案重置期间保存参数必须等待下一轮应用。'
    }
    finally {
        $isResettingField.SetValue($solution, $false)
    }

    $activeRunCountField = $solutionType.GetField('_activeRunSessionCount', $bindingFlags)
    $activeRunCountField.SetValue($solution, 1)
    try {
        $activeApplyResult = $solutionType.GetMethod('ApplyMachinePerformanceResourceSettings').Invoke($solution, @($settings))
        Assert-True ([int]$activeApplyResult.ImageSaveState -eq 2) '存在活动流程时保存工作池参数必须明确返回待下一轮应用。'
        $formType.GetMethod('UpdatePerformanceApplyStates', $bindingFlags).Invoke($form, @($activeApplyResult))
        $shutdownRow = $grid.Rows | Where-Object { $_.Tag.Key -eq 'ShutdownResourceDrainTimeoutMs' } | Select-Object -First 1
        Assert-True ($shutdownRow.Cells['columnApplyState'].Value -like '*保存池待下一轮*') '停止排空超时必须区分运行停止已应用与保存池待应用。'
    }
    finally {
        $activeRunCountField.SetValue($solution, 0)
    }

    $bitmap = New-Object Drawing.Bitmap($form.Width, $form.Height)
    $screenshotPath = Join-Path ([IO.Path]::GetTempPath()) ('TDJS-Vision-SystemSettings-' + [Guid]::NewGuid().ToString('N') + '.png')
    try {
        $form.DrawToBitmap($bitmap, (New-Object Drawing.Rectangle(0, 0, $form.Width, $form.Height)))
        $bitmap.Save($screenshotPath, [Drawing.Imaging.ImageFormat]::Png)
        Assert-True ((Get-Item -LiteralPath $screenshotPath).Length -gt 10000) '离屏界面截图内容异常。'
    }
    finally {
        $bitmap.Dispose()
        if (Test-Path -LiteralPath $screenshotPath) {
            Remove-Item -LiteralPath $screenshotPath -Force
        }
    }
}
finally {
    $form.Dispose()
    Assert-True (-not $formType.GetField('_isRunIntervalEventSubscribed', $bindingFlags).GetValue($form)) '直接Dispose必须解除方案反序列化事件订阅。'
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}

Write-Host '系统设置性能界面检查通过：懒加载、首次合法值、草稿不落盘、真实保存应用、重置/运行中待应用、事件释放、布局和离屏渲染均有效。'
