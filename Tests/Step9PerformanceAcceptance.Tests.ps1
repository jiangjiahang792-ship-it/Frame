$ErrorActionPreference = "Stop"

# 生产程序集基于.NET Framework 4.8，运行时行为检查统一交给Windows PowerShell。
if ($PSVersionTable.PSEdition -eq 'Core') {
    $windowsPowerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    & $windowsPowerShell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath
    exit $LASTEXITCODE
}

function Assert-Contains {
    param([string]$Text, [string]$Expected, [string]$Message)

    if (-not $Text.Contains($Expected)) {
        throw $Message
    }
}

function Assert-NotMatches {
    param([string]$Text, [string]$Pattern, [string]$Message)

    if ($Text -match $Pattern) {
        throw $Message
    }
}

function Assert-MatchCountAtLeast {
    param([string]$Text, [string]$Pattern, [int]$MinimumCount, [string]$Message)

    if ([regex]::Matches($Text, $Pattern).Count -lt $MinimumCount) {
        throw $Message
    }
}

function Assert-Matches {
    param([string]$Text, [string]$Pattern, [string]$Message)

    if ($Text -notmatch $Pattern) {
        throw $Message
    }
}

function Get-FunctionText {
    param(
        [Management.Automation.Language.ScriptBlockAst]$ScriptAst,
        [string]$FunctionName
    )

    $functionAst = $ScriptAst.Find(
        {
            param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
                $node.Name -eq $FunctionName
        },
        $true)
    if ($null -eq $functionAst) {
        throw "未找到测试函数：$FunctionName。"
    }

    return $functionAst.Extent.Text
}

function Get-StartupCreateParams {
    param([Reflection.Assembly]$ApplicationAssembly)

    $startupFormType = $ApplicationAssembly.GetType('TDJS_Vision.Forms.Startup.FormStartup', $true)
    $startupFormInstance = $null
    try {
        $startupFormInstance = [Activator]::CreateInstance($startupFormType, $true)
        $createParamsProperty = $startupFormType.GetProperty(
            'CreateParams',
            [Reflection.BindingFlags]'Instance,NonPublic')
        if ($null -eq $createParamsProperty) {
            throw "未找到启动页窗口样式属性。"
        }

        return [Windows.Forms.CreateParams]$createParamsProperty.GetValue($startupFormInstance, $null)
    }
    finally {
        if ($null -ne $startupFormInstance) {
            $startupFormInstance.Dispose()
        }
    }
}

$root = Split-Path -Parent $PSScriptRoot
$comparisonPath = Join-Path $root "Tests\FullSolutionComparison.Tests.ps1"
$logSettingsPath = Join-Path $root "Forms\Logger\LogLevelSettings.cs"
$startupDisplayModePath = Join-Path $root "Startup\StartupDisplayMode.cs"
$startupApplicationPath = Join-Path $root "Startup\StartupApplicationContext.cs"
$backgroundActivationGuardPath = Join-Path $root "Startup\BackgroundWindowActivationGuard.cs"
$programPath = Join-Path $root "Program.cs"
$projectPath = Join-Path $root "TDJS-Vision.csproj"
$formMainPath = Join-Path $root "FormMain.cs"
$startupFormPath = Join-Path $root "Forms\Startup\FormStartup.cs"
$messageBoxPath = Join-Path $root "Forms\YTMessageBox\MessageBoxTD.cs"
$imageSaveComparisonPath = Join-Path $root "Tests\FullSolutionImageSaveComparison.Tests.ps1"
$workerStressPath = Join-Path $root "Tests\ImageSaveWorkerPoolStress.Tests.ps1"
$fourWindowStressPath = Join-Path $root "Tests\FourWindowImageDisplayStress.Tests.ps1"
$uiOwnershipPath = Join-Path $root "Tests\UiFrameRateAndImageOwnership.Tests.ps1"
$cpuProtectionPath = Join-Path $root "Tests\CpuResourceProtectionStress.Tests.ps1"
$comparison = Get-Content -LiteralPath $comparisonPath -Encoding UTF8 -Raw
$logSettings = Get-Content -LiteralPath $logSettingsPath -Encoding UTF8 -Raw
$startupDisplayMode = Get-Content -LiteralPath $startupDisplayModePath -Encoding UTF8 -Raw
$startupApplication = Get-Content -LiteralPath $startupApplicationPath -Encoding UTF8 -Raw
$backgroundActivationGuard = Get-Content -LiteralPath $backgroundActivationGuardPath -Encoding UTF8 -Raw
$program = Get-Content -LiteralPath $programPath -Encoding UTF8 -Raw
$formMain = Get-Content -LiteralPath $formMainPath -Encoding UTF8 -Raw
$startupForm = Get-Content -LiteralPath $startupFormPath -Encoding UTF8 -Raw
$messageBox = Get-Content -LiteralPath $messageBoxPath -Encoding UTF8 -Raw
$imageSaveComparison = Get-Content -LiteralPath $imageSaveComparisonPath -Encoding UTF8 -Raw
$workerStress = Get-Content -LiteralPath $workerStressPath -Encoding UTF8 -Raw
$fourWindowStress = Get-Content -LiteralPath $fourWindowStressPath -Encoding UTF8 -Raw
$uiOwnership = Get-Content -LiteralPath $uiOwnershipPath -Encoding UTF8 -Raw
$cpuProtection = Get-Content -LiteralPath $cpuProtectionPath -Encoding UTF8 -Raw
$comparisonTokens = $null
$comparisonParseErrors = $null
$comparisonAst = [Management.Automation.Language.Parser]::ParseFile(
    $comparisonPath,
    [ref]$comparisonTokens,
    [ref]$comparisonParseErrors)
if ($comparisonParseErrors.Count -gt 0) {
    throw "完整方案测试工具存在PowerShell语法错误。"
}
$startupWaitFunction = Get-FunctionText -ScriptAst $comparisonAst -FunctionName 'Find-RunOnceButton'
$roundFunction = Get-FunctionText -ScriptAst $comparisonAst -FunctionName 'Invoke-SolutionRound'
$closeFunction = Get-FunctionText -ScriptAst $comparisonAst -FunctionName 'Close-TestProcess'

Assert-Contains $comparison '[string]$WindowMode = "Minimized"' "步骤9完整方案测试必须默认最小化运行。"
Assert-Contains $comparison '[string]$ProcessPriority = "BelowNormal"' "步骤9完整方案测试必须默认使用低于正常优先级。"
Assert-Contains $comparison '[string]$DebugMode = "Current"' "完整方案测试必须支持明确选择Debug模式。"
Assert-Contains $comparison '[string]$TestEnvironmentMode = "SharedDesktopBackground"' "办公期间测试必须默认标记为共享桌面后台数据。"
Assert-Contains $comparison '[int]$MinimumSharedDesktopAvailableMemoryMb = 4096' "共享办公环境必须预留至少4096MB可用内存。"
Assert-Contains $comparison '$OutputDirectory = Join-Path $PSScriptRoot "Results\FullSolutionComparison"' "默认结果目录必须在参数绑定后解析，兼容Windows PowerShell。"
Assert-Contains $comparison 'RequestedPerformanceConclusionEligibility = ($TestEnvironmentMode -eq "Dedicated")' "专用环境资格必须先作为待退出验证请求记录。"
Assert-Contains $comparison 'PerformanceConclusionEligible = $false' "退出验证前不得把结果标记为最终性能结论。"
Assert-Contains $comparison 'SW_SHOWNA=8' "测试窗口必须使用不激活显示方式建立控件树。"
Assert-Contains $comparison '[Windows.Automation.InvokePattern]$patternObject).Invoke()' "流程触发必须使用控件调用模式，不能模拟键盘鼠标。"
Assert-Contains $comparison '[Environment]::SetEnvironmentVariable("TDJS_VISION_PERFORMANCE_ACCEPTANCE", $previousAcceptanceMode, "Process")' "测试结束必须恢复父进程原有验收环境变量。"
Assert-Contains $comparison '[Environment]::SetEnvironmentVariable("TDJS_VISION_TEST_LOG_DEBUG_ENABLED", $previousDebugOverride, "Process")' "测试结束必须恢复父进程原有Debug覆盖环境变量。"
Assert-Contains $comparison '[Environment]::SetEnvironmentVariable("TDJS_VISION_PERFORMANCE_ACCEPTANCE", "1", "Process")' "所有工具启动的子进程都必须携带性能验收标记，Debug当前值模式也不能失去后台保护。"
Assert-Contains $comparison 'Assert-NoExistingVisionProcess' "启动测试前必须拒绝已有同名视觉程序。"
Assert-Contains $comparison '步骤9安全验收不再支持复用既有视觉进程' "步骤9工具不得修改或驱动用户已有视觉进程。"
Assert-Contains $comparison 'GetDescendantProcessIds' "测试进程树必须按Windows父子进程关系识别。"
Assert-Contains $comparison 'Assert-BackgroundFocusSafe' "共享办公环境必须检测测试进程是否取得前台焦点。"
Assert-Contains $comparison '视觉测试主进程或工作进程取得了Windows前台焦点' "焦点保护必须覆盖主程序和本轮工作进程。"
Assert-Contains $comparison 'GetForegroundWindowInfo()' "焦点保护必须记录一次性的前台窗口现场。"
Assert-Contains $comparison '阶段={0}；前台窗口={1}' "焦点异常必须输出检查阶段、窗口句柄、PID、标题和窗口类现场。"
Assert-Contains $comparison '$testBodyFailure = $_.Exception' "测试工具必须保存测试主体的原始异常。"
Assert-Contains $comparison '测试主体原始失败已保留；退出清理附加异常' "退出清理异常不得覆盖测试主体原始异常。"
Assert-Contains $comparison 'Assert-SharedDesktopResourceHeadroom' "共享办公环境必须在启动完整方案前检查可用内存。"
Assert-MatchCountAtLeast $comparison 'Assert-BackgroundFocusSafe\s+-MainProcess' 5 "焦点保护必须覆盖启动、运行、排空和退出阶段。"
Assert-MatchCountAtLeast $comparison 'Assert-SharedDesktopResourceHeadroom\s+`' 4 "内存保护必须覆盖启动、运行和结果排空阶段。"
Assert-Contains $startupWaitFunction 'Assert-BackgroundFocusSafe' "等待主界面阶段必须持续检查前台焦点。"
Assert-Contains $startupWaitFunction 'Assert-SharedDesktopResourceHeadroom' "等待主界面阶段必须持续检查可用内存。"
Assert-Contains $startupWaitFunction '$null -ne $button -and $button.Current.IsEnabled' "主界面等待必须确认单次运行按钮已经启用。"
Assert-Contains $roundFunction 'Assert-BackgroundFocusSafe' "每轮方案运行必须持续检查前台焦点。"
Assert-Contains $roundFunction 'Assert-SharedDesktopResourceHeadroom' "每轮方案运行必须持续检查可用内存。"
Assert-Contains $closeFunction 'Assert-BackgroundFocusSafe' "关闭排空阶段必须持续检查前台焦点。"
Assert-Contains $closeFunction 'Assert-SharedDesktopResourceHeadroom' "关闭排空阶段必须持续检查可用内存。"
Assert-Matches $comparison 'Send-FixedTriggerMessage[\s\S]{0,700}Assert-BackgroundFocusSafe' "固定触发投递阶段必须检查前台焦点。"
Assert-Matches $comparison 'summaryDeadline[\s\S]{0,700}Assert-SharedDesktopResourceHeadroom' "固定触发结果排空阶段必须检查可用内存。"
Assert-Contains $comparison 'TDJS_VISION_BACKGROUND_ACCEPTANCE' "共享办公环境必须向子进程传递不激活启动标记。"
Assert-Contains $comparison 'if (-not $KeepProcess -and $startedByScript -and $null -ne $mainProcess)' "测试工具只能自动关闭自己启动的视觉进程。"
Assert-Contains $comparison 'Stop-TestProcessTree' "正常关闭失败后必须清理本工具拥有的测试进程树。"
Assert-Contains $comparison 'Set-TestResultExitValidation' "结果必须经过退出验证后再更新完成状态。"
Assert-Contains $comparison 'AwaitingExitValidation = $true' "退出前检查点必须保持待验证状态。"
Assert-Contains $comparison 'CpuSampled = ($null -ne $cpuPercent)' "固定节拍未采样行必须明确标记，不能把CPU伪造为0。"
Assert-Contains $comparison '固定触发期间进程树发生重启' "固定节拍结果必须校验进程PID稳定。"
Assert-Contains $comparison '$ioEndSnapshot.TotalCpuMilliseconds - $initial.TotalCpuMilliseconds' "串行平均CPU必须使用正式墙钟结束时的CPU快照。"
Assert-Contains $comparison '$Rounds / [Math]::Max(0.001, $formalWallMilliseconds / 1000.0)' "正式吞吐量必须按实际墙钟计算。"
Assert-NotMatches $comparison '(?i)SendKeys|AppActivate|SetForegroundWindow|keybd_event|mouse_event|SendInput' "步骤9测试禁止模拟键盘鼠标或抢占前台焦点。"

Assert-Contains $logSettings 'ResolveInitialDebugEnabled()' "Debug开关必须支持进程级验收覆盖。"
Assert-Contains $logSettings 'TDJS_VISION_PERFORMANCE_ACCEPTANCE' "Debug覆盖必须受性能验收标记保护。"
Assert-Contains $logSettings 'TDJS_VISION_TEST_LOG_DEBUG_ENABLED' "Debug覆盖必须使用独立测试环境变量。"
Assert-Contains $logSettings 'return Settings.Default.LogDebugEnabled;' "无合法覆盖时必须保持用户设置。"
Assert-NotMatches $logSettings 'TDJS_VISION_TEST_LOG_DEBUG_ENABLED[\s\S]{0,500}Settings\.Default\.Save\(' "进程级Debug覆盖不得写入用户设置。"

Assert-Contains $startupDisplayMode 'TDJS_VISION_BACKGROUND_ACCEPTANCE' "生产启动流程必须识别后台验收显示模式。"
Assert-Contains $startupDisplayMode 'TDJS_VISION_PERFORMANCE_ACCEPTANCE' "后台显示模式必须同时受性能验收标记保护。"
Assert-Contains $startupDisplayMode 'return isPerformanceAcceptance && isBackgroundRequested;' "只有两个专用开关同时开启时才允许静默后台运行。"
Assert-Contains $startupApplication 'if (!StartupDisplayMode.IsBackgroundAcceptance)' "后台验收启动不得激活并置前主窗体。"
Assert-Contains $startupApplication 'if (StartupDisplayMode.IsBackgroundAcceptance)' "后台验收启动失败时必须静默退出，不能等待不可见的人工重试。"
Assert-Matches $startupApplication 'if \(!StartupDisplayMode\.IsBackgroundAcceptance\)\s+splashController\.Start\(\);' "后台验收不得创建不可见启动动画线程和WinForms停车窗口。"
Assert-Matches $startupApplication 'if \(!StartupDisplayMode\.IsBackgroundAcceptance\)\s+\{\s+TimeSpan remaining' "后台验收不得等待不可见启动动画的最短展示时长。"
Assert-Contains $program 'if (!StartupDisplayMode.IsBackgroundAcceptance)' "后台验收重复实例不得弹出前台提示框。"
Assert-Contains $program 'BackgroundWindowActivationGuard.CreateForCurrentThread()' "后台验收窗口激活守卫必须在任何主UI窗口创建前安装。"
Assert-Contains $backgroundActivationGuard 'HookTypeComputerBasedTraining = 5' "窗口激活守卫必须使用线程级WH_CBT钩子。"
Assert-Contains $backgroundActivationGuard 'HookCodeActivate = 5' "窗口激活守卫必须拦截HCBT_ACTIVATE事件。"
Assert-Contains $backgroundActivationGuard 'if (!StartupDisplayMode.IsBackgroundAcceptance)' "普通启动不得安装后台验收窗口钩子。"
Assert-Contains $backgroundActivationGuard 'return new IntPtr(1);' "后台验收必须拒绝本线程窗口激活。"
Assert-Contains $backgroundActivationGuard 'UnhookWindowsHookEx(hookHandle)' "主消息循环退出时必须解除窗口激活钩子。"
Assert-Contains $backgroundActivationGuard '后台性能验收窗口激活守卫解除失败' "窗口激活钩子解除失败必须写入诊断日志。"
Assert-Contains $formMain 'StartupDisplayMode.IsBackgroundAcceptance || base.ShowWithoutActivation' "后台验收主窗体必须使用不激活显示。"
Assert-Contains $formMain 'createParams.ExStyle |= ExtendedWindowStyleNoActivate;' "后台验收主窗体必须使用系统级不激活窗口样式。"
Assert-Contains $startupForm 'createParams.ExStyle |= ExtendedWindowStyleNoActivate;' "后台验收启动页必须使用系统级不激活窗口样式。"
Assert-Contains $formMain 'if (!StartupDisplayMode.IsBackgroundAcceptance)' "后台验收关闭时不得显示确认框。"
Assert-Contains $formMain 'Solution.Instance.Stop();' "后台验收在流程运行中退出时必须先请求正常取消，不能只等待强制清理。"
Assert-Contains $startupForm 'WindowState = FormWindowState.Minimized;' "后台验收启动页必须保持最小化。"
Assert-Contains $startupForm 'Opacity = 0D;' "后台验收启动页必须不可见，避免遮挡用户办公窗口。"
Assert-Contains $messageBox 'if (StartupDisplayMode.IsBackgroundAcceptance)' "后台验收中的统一消息框必须静默返回。"
Assert-Contains $messageBox 'return DialogResult.Cancel;' "后台验收中的统一消息框不得创建模态窗口。"
Assert-Contains $imageSaveComparison 'SaveComparisonIoCounters ioSaveEnd = QueryIo(process.Handle);' "保存链路I/O必须在文件验证读取前结束采样。"
Assert-Contains $workerStress "[Guid]::NewGuid().ToString('N')" "保存压力临时目录必须按测试实例隔离。"

Assert-Contains $fourWindowStress 'public static extern bool ShowWindow(IntPtr windowHandle, int command);' "四窗口压力必须使用不激活显示。"
Assert-Contains $fourWindowStress 'P99ConversionMilliseconds' "四窗口压力必须记录Mat转Bitmap的P99。"
Assert-Contains $fourWindowStress 'P99ConversionMilliseconds[$windowIndex] -lt 500' "四窗口组件压力必须执行P99小于500ms门槛。"
Assert-Contains $fourWindowStress '该证据不包含完整节点执行链和生产CPU调度链' "四窗口组件压力必须声明证据边界。"
Assert-NotMatches $fourWindowStress '\$(warmupWindow|window)\.Show\(\)' "四窗口压力不得调用会激活窗体的Show方法。"
Assert-Contains $uiOwnership 'public static extern bool ShowWindow(IntPtr windowHandle, int command);' "图像所有权测试必须使用不激活显示。"
Assert-NotMatches $uiOwnership '\$shownWindow\.Show\(\)' "图像所有权测试不得调用会激活窗体的Show方法。"
Assert-Contains $cpuProtection '该探针指标不代表真实UI消息循环、相机回调或PLC通信' "CPU压力必须说明线程池计时探针不代表生产控制链。"

$exitValidationFunction = Get-FunctionText -ScriptAst $comparisonAst -FunctionName 'Set-TestResultExitValidation'
. ([scriptblock]::Create($exitValidationFunction))
$exitValidationDirectory = Join-Path ([IO.Path]::GetTempPath()) ('TDJS-Step9-' + [Guid]::NewGuid().ToString('N'))
$exitValidationJson = Join-Path $exitValidationDirectory 'result.json'
$exitValidationCheckpoint = Join-Path $exitValidationDirectory 'checkpoint.json'
try {
    New-Item -ItemType Directory -Path $exitValidationDirectory -Force | Out-Null
    [ordered]@{ PerformanceConclusionEligible = $false } |
        ConvertTo-Json |
        Set-Content -LiteralPath $exitValidationJson -Encoding UTF8
    [ordered]@{ Completed = $false } |
        ConvertTo-Json |
        Set-Content -LiteralPath $exitValidationCheckpoint -Encoding UTF8

    Set-TestResultExitValidation `
        -JsonPath $exitValidationJson `
        -CheckpointPath $exitValidationCheckpoint `
        -ValidationCompleted $true `
        -ValidationPassed $false `
        -PerformanceConclusionEligible $false `
        -FailureReason '退出失败验证'
    $failedResult = Get-Content -LiteralPath $exitValidationJson -Raw -Encoding UTF8 | ConvertFrom-Json
    $failedCheckpoint = Get-Content -LiteralPath $exitValidationCheckpoint -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($failedResult.PerformanceConclusionEligible -or
        $failedResult.ExitValidationPassed -or
        $failedCheckpoint.Completed -or
        $failedCheckpoint.AwaitingExitValidation) {
        throw "退出失败时结果不得被标记为完成或可用于性能结论。"
    }

    Set-TestResultExitValidation `
        -JsonPath $exitValidationJson `
        -CheckpointPath $exitValidationCheckpoint `
        -ValidationCompleted $true `
        -ValidationPassed $true `
        -PerformanceConclusionEligible $true `
        -FailureReason ''
    $passedResult = Get-Content -LiteralPath $exitValidationJson -Raw -Encoding UTF8 | ConvertFrom-Json
    $passedCheckpoint = Get-Content -LiteralPath $exitValidationCheckpoint -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $passedResult.ExitValidationPassed -or
        -not $passedResult.PerformanceConclusionEligible -or
        -not $passedCheckpoint.Completed -or
        $passedCheckpoint.AwaitingExitValidation) {
        throw "退出成功后结果完成状态没有正确发布。"
    }
}
finally {
    Remove-Item -LiteralPath $exitValidationJson -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $exitValidationCheckpoint -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $exitValidationDirectory -Force -ErrorAction SilentlyContinue
}

Add-Type -AssemblyName System.Windows.Forms
$releaseDirectory = Join-Path $root 'bin\x64\Release'
$applicationPath = Join-Path $releaseDirectory '机器视觉AI检测系统V1.0.exe'
$application = Get-Item -LiteralPath $applicationPath -ErrorAction SilentlyContinue
if ($null -eq $application) {
    throw "步骤9运行时行为检查需要最新Release程序。"
}
foreach ($sourcePath in @($startupDisplayModePath, $startupApplicationPath, $backgroundActivationGuardPath, $programPath, $projectPath, $startupFormPath, $formMainPath, $messageBoxPath)) {
    $source = Get-Item -LiteralPath $sourcePath
    if ($application.LastWriteTimeUtc -lt $source.LastWriteTimeUtc) {
        throw "Release程序早于$($source.Name)，请先重新编译。"
    }
}

$previousCurrentDirectory = [Environment]::CurrentDirectory
$previousPerformanceAcceptance = [Environment]::GetEnvironmentVariable('TDJS_VISION_PERFORMANCE_ACCEPTANCE', 'Process')
$previousBackgroundAcceptance = [Environment]::GetEnvironmentVariable('TDJS_VISION_BACKGROUND_ACCEPTANCE', 'Process')
$assemblyResolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $requestedAssembly = New-Object Reflection.AssemblyName($eventArgs.Name)
    $dependencyPath = Join-Path $releaseDirectory ($requestedAssembly.Name + '.dll')
    if (Test-Path -LiteralPath $dependencyPath) {
        return [Reflection.Assembly]::LoadFrom($dependencyPath)
    }
    return $null
}

try {
    [Environment]::CurrentDirectory = $releaseDirectory
    [AppDomain]::CurrentDomain.add_AssemblyResolve($assemblyResolver)
    $applicationAssembly = [Reflection.Assembly]::LoadFrom($application.FullName)
    $startupModeType = $applicationAssembly.GetType('TDJS_Vision.Startup.StartupDisplayMode', $true)
    $backgroundProperty = $startupModeType.GetProperty('IsBackgroundAcceptance', [Reflection.BindingFlags]'Public,Static')
    if ($null -eq $backgroundProperty) {
        throw "未找到后台验收模式属性。"
    }

    [Environment]::SetEnvironmentVariable('TDJS_VISION_PERFORMANCE_ACCEPTANCE', $null, 'Process')
    [Environment]::SetEnvironmentVariable('TDJS_VISION_BACKGROUND_ACCEPTANCE', '1', 'Process')
    if ([bool]$backgroundProperty.GetValue($null, $null)) {
        throw "仅开启后台开关时不得进入后台验收模式。"
    }

    [Environment]::SetEnvironmentVariable('TDJS_VISION_PERFORMANCE_ACCEPTANCE', '1', 'Process')
    if (-not [bool]$backgroundProperty.GetValue($null, $null)) {
        throw "两个专用开关同时开启时应进入后台验收模式。"
    }

    [Environment]::SetEnvironmentVariable('TDJS_VISION_BACKGROUND_ACCEPTANCE', '0', 'Process')
    if ([bool]$backgroundProperty.GetValue($null, $null)) {
        throw "关闭后台开关后不得继续处于后台验收模式。"
    }
    $normalCreateParams = Get-StartupCreateParams -ApplicationAssembly $applicationAssembly
    if (($normalCreateParams.ExStyle -band 0x08000000) -ne 0) {
        throw "普通Release启动页不应携带系统级不激活窗口样式。"
    }

    [Environment]::SetEnvironmentVariable('TDJS_VISION_BACKGROUND_ACCEPTANCE', '1', 'Process')
    $backgroundCreateParams = Get-StartupCreateParams -ApplicationAssembly $applicationAssembly
    if (($backgroundCreateParams.ExStyle -band 0x08000000) -eq 0) {
        throw "Release启动页没有启用系统级不激活窗口样式。"
    }

    $messageBoxType = $applicationAssembly.GetType('TDJS_Vision.Forms.YTMessageBox.MessageBoxTD', $true)
    $showMethod = $messageBoxType.GetMethod(
        'Show',
        [Reflection.BindingFlags]'Public,Static',
        $null,
        [Type[]]@([string], [string], [Windows.Forms.MessageBoxButtons], [Windows.Forms.MessageBoxIcon]),
        $null)
    if ($null -eq $showMethod) {
        throw "未找到统一消息框后台行为验证入口。"
    }
    $dialogResult = $showMethod.Invoke($null, @('后台验收行为检查', '提示', [Windows.Forms.MessageBoxButtons]::OK, [Windows.Forms.MessageBoxIcon]::Information))
    if ($dialogResult -ne [Windows.Forms.DialogResult]::Cancel) {
        throw "后台验收消息框没有静默返回Cancel。"
    }
}
finally {
    [Environment]::SetEnvironmentVariable('TDJS_VISION_PERFORMANCE_ACCEPTANCE', $previousPerformanceAcceptance, 'Process')
    [Environment]::SetEnvironmentVariable('TDJS_VISION_BACKGROUND_ACCEPTANCE', $previousBackgroundAcceptance, 'Process')
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($assemblyResolver)
    [Environment]::CurrentDirectory = $previousCurrentDirectory
}

Write-Output "步骤9Release验收工具检查通过：默认最小化、系统级不激活、低优先级、不使用键盘鼠标、全程焦点与内存门禁、Debug覆盖不落盘，办公环境结果不会冒充最终性能结论。"
