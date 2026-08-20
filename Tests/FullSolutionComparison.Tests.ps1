param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath,

    [Parameter(Mandatory = $true)]
    [string]$SchemePath,

    [Parameter(Mandatory = $true)]
    [string]$VersionLabel,

    [ValidateRange(1, 100000)]
    [int]$Rounds = 5000,

    [ValidateRange(0, 100)]
    [int]$WarmupRounds = 1,

    [ValidateRange(0, [int]::MaxValue)]
    [int]$ExistingProcessId = 0,

    [ValidateRange(1, 1000)]
    [int]$ProgressInterval = 25,

    [ValidateRange(1, 1000)]
    [int]$ResourceSampleInterval = 5,

    [ValidateRange(10, 3600)]
    [int]$RoundTimeoutSeconds = 600,

    [ValidateRange(1, 1000)]
    [int]$UiPollMilliseconds = 50,

    [ValidateSet("SerialRounds", "FixedIntervalTriggers")]
    [string]$RunMode = "SerialRounds",

    [ValidateRange(1, 60000)]
    [int]$TriggerIntervalMilliseconds = 50,

    [ValidateRange(10, 30000)]
    [int]$TriggerMessageTimeoutMilliseconds = 5000,

    [string]$OutputDirectory = (Join-Path $PSScriptRoot "Results\FullSolutionComparison"),

    [string]$ImmutableSchemeSourcePath = "",

    [string]$RuntimeReferenceDirectory = "",

    [ValidateRange(1, 16)]
    [int]$ExpectedVisibleImageWindows = 6,

    [ValidateRange(1, 16)]
    [int]$ExpectedProcessTreeCount = 3,

    [ValidateSet("Normal", "Minimized")]
    [string]$WindowMode = "Minimized",

    [ValidateSet("Normal", "BelowNormal")]
    [string]$ProcessPriority = "BelowNormal",

    [switch]$KeepProcess
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -TypeDefinition @"
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

public static class FullSolutionGuiResourceProbe
{
    /// <summary>Windows为单个进程累计的I/O操作和传输字节。</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ProcessIoCounters
    {
        /// <summary>累计读操作次数。</summary>
        public ulong ReadOperationCount;

        /// <summary>累计写操作次数。</summary>
        public ulong WriteOperationCount;

        /// <summary>累计其他I/O操作次数。</summary>
        public ulong OtherOperationCount;

        /// <summary>累计读取传输字节。</summary>
        public ulong ReadTransferCount;

        /// <summary>累计写入传输字节。</summary>
        public ulong WriteTransferCount;

        /// <summary>累计其他I/O传输字节。</summary>
        public ulong OtherTransferCount;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessIoCounters(IntPtr processHandle, out ProcessIoCounters ioCounters);

    /// <summary>读取指定进程句柄的累计I/O计数，失败时保留Windows错误码。</summary>
    public static ProcessIoCounters QueryProcessIoCounters(IntPtr processHandle)
    {
        ProcessIoCounters counters;
        if (!GetProcessIoCounters(processHandle, out counters))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return counters;
    }

    [DllImport("user32.dll")]
    public static extern int GetGuiResources(IntPtr processHandle, int resourceType);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr windowHandle, uint message, IntPtr wordParameter, IntPtr longParameter);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(
        IntPtr windowHandle,
        uint message,
        IntPtr wordParameter,
        IntPtr longParameter,
        uint flags,
        uint timeoutMilliseconds,
        out IntPtr messageResult);

    [DllImport("kernel32.dll")]
    public static extern uint SetThreadExecutionState(uint executionState);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindowAsync(IntPtr windowHandle, int command);
}
"@ -Language CSharp

function Get-Percentile {
    param(
        [double[]]$Values,
        [ValidateRange(0, 100)]
        [double]$Percentile
    )

    if ($null -eq $Values -or $Values.Count -eq 0) {
        return 0.0
    }

    $sorted = @($Values | Sort-Object)
    $rank = ($Percentile / 100.0) * ($sorted.Count - 1)
    $lower = [int][Math]::Floor($rank)
    $upper = [int][Math]::Ceiling($rank)
    if ($lower -eq $upper) {
        return [double]$sorted[$lower]
    }

    $weight = $rank - $lower
    return ([double]$sorted[$lower] * (1.0 - $weight)) + ([double]$sorted[$upper] * $weight)
}

function Get-UnsignedCounterDelta {
    param(
        [UInt64]$Current,
        [UInt64]$Previous
    )

    if ($Current -ge $Previous) {
        return [UInt64]($Current - $Previous)
    }

    # 正式同比会额外校验进程ID稳定；此回退仅避免计数器复位时发生无符号下溢。
    return $Current
}

function Test-StableProcessIds {
    param(
        [int[]]$InitialProcessIds,
        [int[]]$FinalProcessIds
    )

    $initial = @($InitialProcessIds | Sort-Object)
    $final = @($FinalProcessIds | Sort-Object)
    return (($initial -join ',') -eq ($final -join ','))
}

function Restore-ImmutableScheme {
    param(
        [string]$SourcePath,
        [string]$TargetPath,
        [string]$ExpectedSha256
    )

    if ([string]::IsNullOrWhiteSpace($SourcePath)) {
        return
    }

    Copy-Item -LiteralPath $SourcePath -Destination $TargetPath -Force
    $actualSha256 = (Get-FileHash -LiteralPath $TargetPath -Algorithm SHA256).Hash
    if (-not [string]::Equals($actualSha256, $ExpectedSha256, [StringComparison]::OrdinalIgnoreCase)) {
        throw "恢复固定方案母本后SHA256校验失败。"
    }
}

function Sync-TestRuntimeConfiguration {
    param(
        [string]$WorkingDirectory,
        [string]$ReferenceDirectory,
        [int]$VisibleImageWindows
    )

    $requiredFiles = @('device.license', 'DockPanel.config', 'DockPanelImageWindows.config')
    $resolvedReferenceDirectory = ""
    if (-not [string]::IsNullOrWhiteSpace($ReferenceDirectory)) {
        $resolvedReferenceDirectory = (Resolve-Path -LiteralPath $ReferenceDirectory).Path
        foreach ($fileName in $requiredFiles) {
            $sourcePath = Join-Path $resolvedReferenceDirectory $fileName
            if (-not (Test-Path -LiteralPath $sourcePath)) {
                throw "参考运行目录缺少$fileName。"
            }
            Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $WorkingDirectory $fileName) -Force
        }
    }

    $hashes = [ordered]@{}
    foreach ($fileName in $requiredFiles) {
        $targetPath = Join-Path $WorkingDirectory $fileName
        if (-not (Test-Path -LiteralPath $targetPath)) {
            throw "测试运行目录缺少$fileName。"
        }
        $hashes[$fileName] = (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash
        if (-not [string]::IsNullOrWhiteSpace($ReferenceDirectory)) {
            $referenceHash = (Get-FileHash -LiteralPath (Join-Path $resolvedReferenceDirectory $fileName) -Algorithm SHA256).Hash
            if (-not [string]::Equals($hashes[$fileName], $referenceHash, [StringComparison]::OrdinalIgnoreCase)) {
                throw "$fileName复制后SHA256与参考运行目录不一致。"
            }
        }
    }

    $dockPanel = [xml](Get-Content -LiteralPath (Join-Path $WorkingDirectory 'DockPanel.config') -Raw)
    $mainContents = @($dockPanel.DockPanel.Contents.Content)
    $imageView = @($mainContents | Where-Object { $_.PersistString -eq 'TDJS_Vision.Forms.ImageViewer.FrmImageViewer' })
    $resultView = @($mainContents | Where-Object { $_.PersistString -eq 'TDJS_Vision.Forms.ResultView.FrmResultView' })
    $logoView = @($mainContents | Where-Object { $_.PersistString -eq 'TDJS_Vision.Forms.LogoView.FrmLogo' })
    if ($imageView.Count -ne 1 -or [string]$imageView[0].IsHidden -ne 'False') {
        throw '主布局必须显示图像视图。'
    }
    if ($resultView.Count -ne 1 -or [string]$resultView[0].IsHidden -ne 'True') {
        throw '主布局必须隐藏检测结果视图。'
    }
    if ($logoView.Count -ne 1 -or [string]$logoView[0].IsHidden -ne 'True') {
        throw '主布局必须隐藏Logo视图。'
    }
    $loggerView = @($mainContents | Where-Object { $_.PersistString -eq 'Logger.FrmLogger' })
    $autoHideLoggerPanes = @(
        $dockPanel.DockPanel.Panes.Pane |
            Where-Object {
                [string]$_.DockState -eq 'DockRightAutoHide' -and
                @($_.Contents.Content | Where-Object { [string]$_.RefID -eq [string]$loggerView[0].ID }).Count -gt 0
            })
    if ($loggerView.Count -ne 1 -or $autoHideLoggerPanes.Count -eq 0) {
        throw '运行日志必须保持旧版右侧自动折叠布局。'
    }

    $imageDockPanel = [xml](Get-Content -LiteralPath (Join-Path $WorkingDirectory 'DockPanelImageWindows.config') -Raw)
    $visibleCount = @($imageDockPanel.DockPanel.Contents.Content | Where-Object { [string]$_.IsHidden -eq 'False' }).Count
    if ($visibleCount -ne $VisibleImageWindows) {
        throw "图像窗口布局必须恰好显示${VisibleImageWindows}个窗体，实际为${visibleCount}个。"
    }

    return $hashes
}

function Get-TestProcesses {
    param(
        [string]$ResolvedExecutablePath,
        [datetime]$MainProcessStartTime
    )

    $processName = [IO.Path]::GetFileNameWithoutExtension($ResolvedExecutablePath)
    $matches = New-Object System.Collections.Generic.List[System.Diagnostics.Process]
    foreach ($process in @(Get-Process -Name $processName -ErrorAction SilentlyContinue)) {
        try {
            if ([string]::Equals($process.Path, $ResolvedExecutablePath, [StringComparison]::OrdinalIgnoreCase) -and
                $process.StartTime -ge $MainProcessStartTime.AddSeconds(-2)) {
                $matches.Add($process)
            }
        }
        catch {
            # 进程可能恰好退出；下一次采样会重新发现当前进程树。
        }
    }

    return @($matches)
}

function Set-TestProcessPriority {
    param(
        [string]$ResolvedExecutablePath,
        [datetime]$MainProcessStartTime,
        [string]$PriorityName
    )

    $priorityClass = [Diagnostics.ProcessPriorityClass]::$PriorityName
    foreach ($process in @(Get-TestProcesses -ResolvedExecutablePath $ResolvedExecutablePath -MainProcessStartTime $MainProcessStartTime)) {
        try {
            if ($process.PriorityClass -ne $priorityClass) {
                $process.PriorityClass = $priorityClass
            }
        }
        catch {
            # AI工作进程若恰好重启，下一次进度采样会再次应用测试优先级。
        }
    }
}

function Set-TestWindowMode {
    param(
        [System.Diagnostics.Process]$MainProcess,
        [string]$Mode
    )

    if ($Mode -ne "Minimized") {
        return
    }

    $MainProcess.Refresh()
    if ($MainProcess.MainWindowHandle -ne [IntPtr]::Zero) {
        # SW_MINIMIZE=6；保留真实窗口和UI线程，只避免测试窗口抢占用户桌面。
        [void][FullSolutionGuiResourceProbe]::ShowWindowAsync($MainProcess.MainWindowHandle, 6)
    }
}

function Get-ManagedHeapBytes {
    param([int[]]$ProcessIds)

    if ($null -eq $ProcessIds -or $ProcessIds.Count -eq 0) {
        return 0L
    }

    try {
        $category = New-Object System.Diagnostics.PerformanceCounterCategory(".NET CLR Memory")
        $total = 0L
        $matchedInstanceCount = 0
        foreach ($instance in $category.GetInstanceNames()) {
            $pidCounter = $null
            $heapCounter = $null
            try {
                $pidCounter = New-Object System.Diagnostics.PerformanceCounter(".NET CLR Memory", "Process ID", $instance, $true)
                $instanceProcessId = [int]$pidCounter.NextValue()
                if ($ProcessIds -notcontains $instanceProcessId) {
                    continue
                }

                $heapCounter = New-Object System.Diagnostics.PerformanceCounter(".NET CLR Memory", "# Bytes in all Heaps", $instance, $true)
                $total += [long]$heapCounter.NextValue()
                $matchedInstanceCount++
            }
            finally {
                if ($null -ne $pidCounter) { $pidCounter.Dispose() }
                if ($null -ne $heapCounter) { $heapCounter.Dispose() }
            }
        }

        return $(if ($matchedInstanceCount -gt 0) { $total } else { -1L })
    }
    catch {
        return -1L
    }
}

function Get-ResourceSnapshot {
    param(
        [string]$ResolvedExecutablePath,
        [datetime]$MainProcessStartTime,
        [int]$MainProcessId,
        [bool]$IncludeManagedHeap
    )

    $processes = @(Get-TestProcesses -ResolvedExecutablePath $ResolvedExecutablePath -MainProcessStartTime $MainProcessStartTime)
    if ($processes.Count -eq 0) {
        throw "未找到正在测试的程序进程树。"
    }

    [long]$privateBytes = 0
    [long]$mainPrivateBytes = 0
    [long]$totalCpuMilliseconds = 0
    [int]$gdiObjects = 0
    [int]$userObjects = 0
    [int]$handles = 0
    [int]$ioCounterProcessCount = 0
    [UInt64]$readOperationCount = 0
    [UInt64]$writeOperationCount = 0
    [UInt64]$otherOperationCount = 0
    [UInt64]$readTransferBytes = 0
    [UInt64]$writeTransferBytes = 0
    [UInt64]$otherTransferBytes = 0
    foreach ($process in $processes) {
        try {
            $process.Refresh()
            $privateBytes += $process.PrivateMemorySize64
            $totalCpuMilliseconds += [long]$process.TotalProcessorTime.TotalMilliseconds
            $handles += $process.HandleCount
            $gdiObjects += [FullSolutionGuiResourceProbe]::GetGuiResources($process.Handle, 0)
            $userObjects += [FullSolutionGuiResourceProbe]::GetGuiResources($process.Handle, 1)
            if ($process.Id -eq $MainProcessId) {
                $mainPrivateBytes = $process.PrivateMemorySize64
            }
            try {
                $ioCounters = [FullSolutionGuiResourceProbe]::QueryProcessIoCounters($process.Handle)
                $readOperationCount += $ioCounters.ReadOperationCount
                $writeOperationCount += $ioCounters.WriteOperationCount
                $otherOperationCount += $ioCounters.OtherOperationCount
                $readTransferBytes += $ioCounters.ReadTransferCount
                $writeTransferBytes += $ioCounters.WriteTransferCount
                $otherTransferBytes += $ioCounters.OtherTransferCount
                $ioCounterProcessCount++
            }
            catch {
                # 资源快照仍然有效；结果会用计数器覆盖进程数明确标记I/O采集是否完整。
            }
        }
        catch {
            # AI工作进程若在采样瞬间重启，本轮忽略该已退出实例并在下次重新发现。
        }
    }

    $managedHeapBytes = if ($IncludeManagedHeap) {
        Get-ManagedHeapBytes -ProcessIds @($processes.Id)
    }
    else {
        -1L
    }

    return [pscustomobject]@{
        TimestampUtc = [DateTime]::UtcNow
        ProcessCount = $processes.Count
        ProcessIds = @($processes.Id)
        PrivateBytes = $privateBytes
        MainPrivateBytes = $mainPrivateBytes
        ManagedHeapBytes = $managedHeapBytes
        GdiObjects = $gdiObjects
        UserObjects = $userObjects
        Handles = $handles
        TotalCpuMilliseconds = $totalCpuMilliseconds
        IoCounterProcessCount = $ioCounterProcessCount
        ReadOperationCount = $readOperationCount
        WriteOperationCount = $writeOperationCount
        OtherOperationCount = $otherOperationCount
        ReadTransferBytes = $readTransferBytes
        WriteTransferBytes = $writeTransferBytes
        OtherTransferBytes = $otherTransferBytes
    }
}

function Find-RunOnceButton {
    param(
        [System.Diagnostics.Process]$MainProcess,
        [string]$WindowMode
    )

    $shownWithoutActivation = $false
    for ($attempt = 0; $attempt -lt 90; $attempt++) {
        $MainProcess.Refresh()
        if ($MainProcess.HasExited) {
            throw "程序在主界面就绪前退出。"
        }

        if ($WindowMode -eq "Minimized" -and
            -not $shownWithoutActivation -and
            $MainProcess.MainWindowHandle -ne [IntPtr]::Zero -and
            $MainProcess.MainWindowTitle -notlike "*正在启动*") {
            # SW_SHOWNA=8：让WinForms创建可访问控件树，但不激活窗口、不抢键盘焦点。
            [void][FullSolutionGuiResourceProbe]::ShowWindowAsync($MainProcess.MainWindowHandle, 8)
            $shownWithoutActivation = $true
            Start-Sleep -Milliseconds 200
        }

        if ($MainProcess.MainWindowHandle -ne [IntPtr]::Zero -and
            $MainProcess.MainWindowTitle -notlike "*正在启动*") {
            $root = [Windows.Automation.AutomationElement]::FromHandle($MainProcess.MainWindowHandle)
            $condition = New-Object Windows.Automation.PropertyCondition(
                [Windows.Automation.AutomationElement]::NameProperty,
                "单次运行")
            $button = $root.FindFirst([Windows.Automation.TreeScope]::Descendants, $condition)
            if ($null -ne $button) {
                return $button
            }
        }

        Start-Sleep -Seconds 1
    }

    throw "等待主界面和单次运行按钮超时。"
}

function Close-TestProcess {
    param([System.Diagnostics.Process]$MainProcess)

    if ($null -eq $MainProcess -or $MainProcess.HasExited) {
        return
    }

    [void]$MainProcess.CloseMainWindow()
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while (-not $MainProcess.HasExited -and [DateTime]::UtcNow -lt $deadline) {
        $MainProcess.Refresh()
        try {
            # 关闭确认框是独立顶层窗体，必须按进程ID扫描桌面窗口树，不能只搜索已隐藏的主窗体。
            $processCondition = New-Object Windows.Automation.PropertyCondition(
                [Windows.Automation.AutomationElement]::ProcessIdProperty,
                $MainProcess.Id)
            $processElements = [Windows.Automation.AutomationElement]::RootElement.FindAll(
                [Windows.Automation.TreeScope]::Descendants,
                $processCondition)
            $confirmButton = @($processElements | Where-Object {
                    $_.Current.ControlType -eq [Windows.Automation.ControlType]::Button -and
                    $_.Current.Name -eq "确定" -and
                    $_.Current.IsEnabled -and
                    -not $_.Current.IsOffscreen -and
                    $_.Current.NativeWindowHandle -ne 0
                }) | Select-Object -First 1
            if ($null -ne $confirmButton) {
                # WinForms自定义确认按钮没有暴露InvokePattern，使用标准BM_CLICK消息执行正常关闭。
                [void][FullSolutionGuiResourceProbe]::SendMessage(
                    [IntPtr]$confirmButton.Current.NativeWindowHandle,
                    0x00F5,
                    [IntPtr]::Zero,
                    [IntPtr]::Zero)
            }
        }
        catch {
            # 关闭过程中窗口树可能随时销毁，继续等待进程正常退出。
        }

        Start-Sleep -Milliseconds 100
    }

    if (-not $MainProcess.HasExited) {
        Write-Warning "程序在点击关闭确认后30秒内仍未正常退出，请检查当前运行轮次或关闭提示。"
        return $false
    }

    return $true
}

function Invoke-SolutionRound {
    param(
        [Windows.Automation.AutomationElement]$RunOnceButton,
        [int]$TimeoutSeconds,
        [int]$PollMilliseconds
    )

    if (-not $RunOnceButton.Current.IsEnabled) {
        throw "单次运行按钮在本轮开始前不可用。"
    }

    $patternObject = $null
    if (-not $RunOnceButton.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern, [ref]$patternObject)) {
        throw "单次运行按钮不支持自动调用。"
    }

    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    ([Windows.Automation.InvokePattern]$patternObject).Invoke()
    $sawDisabled = $false
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ($true) {
        Start-Sleep -Milliseconds $PollMilliseconds
        $enabled = $RunOnceButton.Current.IsEnabled
        if (-not $enabled) {
            $sawDisabled = $true
        }

        if ($sawDisabled -and $enabled) {
            break
        }

        if ([DateTime]::UtcNow -gt $deadline) {
            throw "等待本轮方案结束超过${TimeoutSeconds}秒。"
        }
    }

    $stopwatch.Stop()
    return [double]$stopwatch.Elapsed.TotalMilliseconds
}

function Send-FixedTriggerMessage {
    param(
        [IntPtr]$WindowHandle,
        [uint32]$Message,
        [int]$TimeoutMilliseconds
    )

    $messageResult = [IntPtr]::Zero
    $sendResult = [FullSolutionGuiResourceProbe]::SendMessageTimeout(
        $WindowHandle,
        $Message,
        [IntPtr]::Zero,
        [IntPtr]::Zero,
        2,
        [uint32]$TimeoutMilliseconds,
        [ref]$messageResult)
    return [pscustomobject]@{
        Delivered = $sendResult -ne [IntPtr]::Zero
        Result = $messageResult.ToInt64()
    }
}

function Get-LogOffsets {
    param([string]$LogDirectory)

    $offsets = @{}
    if (-not (Test-Path -LiteralPath $LogDirectory)) {
        return $offsets
    }

    foreach ($file in @(Get-ChildItem -LiteralPath $LogDirectory -File | Where-Object { $_.Name -match '^\d{8}\.log$' })) {
        $offsets[$file.FullName] = $file.Length
    }

    return $offsets
}

function Get-AppendedLogText {
    param(
        [string]$LogDirectory,
        [hashtable]$Offsets
    )

    if (-not (Test-Path -LiteralPath $LogDirectory)) {
        return ""
    }

    $builder = New-Object Text.StringBuilder
    foreach ($file in @(Get-ChildItem -LiteralPath $LogDirectory -File | Where-Object { $_.Name -match '^\d{8}\.log$' } | Sort-Object Name)) {
        [long]$offset = 0
        if ($Offsets.ContainsKey($file.FullName)) {
            $offset = [long]$Offsets[$file.FullName]
        }

        if ($file.Length -le $offset) {
            continue
        }

        $stream = New-Object IO.FileStream($file.FullName, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
        try {
            [void]$stream.Seek($offset, [IO.SeekOrigin]::Begin)
            $reader = New-Object IO.StreamReader($stream, [Text.Encoding]::UTF8, $true)
            try {
                [void]$builder.Append($reader.ReadToEnd())
            }
            finally {
                $reader.Dispose()
            }
        }
        finally {
            $stream.Dispose()
        }
    }

    return $builder.ToString()
}

function Get-ProcessLogSummary {
    param(
        [string]$LogText,
        [string]$ProcessName
    )

    $escapedName = [regex]::Escape($ProcessName)
    $pattern = "【$escapedName】（结束）.*?【耗时】（(?<elapsed>\d+)ms）.*?【状态】（(?<status>[^）]+)）"
    $matches = [regex]::Matches($LogText, $pattern)
    $statusCounts = @{}
    $elapsed = New-Object System.Collections.Generic.List[double]
    foreach ($match in $matches) {
        $status = $match.Groups["status"].Value
        if (-not $statusCounts.ContainsKey($status)) {
            $statusCounts[$status] = 0
        }

        $statusCounts[$status]++
        $elapsed.Add([double]$match.Groups["elapsed"].Value)
    }

    return [pscustomobject]@{
        Name = $ProcessName
        Total = $matches.Count
        Success = if ($statusCounts.ContainsKey("成功")) { $statusCounts["成功"] } else { 0 }
        Failed = $matches.Count - $(if ($statusCounts.ContainsKey("成功")) { $statusCounts["成功"] } else { 0 })
        StatusCounts = $statusCounts
        P50Milliseconds = [Math]::Round((Get-Percentile -Values $elapsed.ToArray() -Percentile 50), 3)
        P95Milliseconds = [Math]::Round((Get-Percentile -Values $elapsed.ToArray() -Percentile 95), 3)
        P99Milliseconds = [Math]::Round((Get-Percentile -Values $elapsed.ToArray() -Percentile 99), 3)
        MaxMilliseconds = if ($elapsed.Count -gt 0) { [Math]::Round(($elapsed | Measure-Object -Maximum).Maximum, 3) } else { 0 }
    }
}

$resolvedExecutablePath = (Resolve-Path -LiteralPath $ExecutablePath).Path
$resolvedSchemePath = (Resolve-Path -LiteralPath $SchemePath).Path
$resolvedImmutableSchemeSourcePath = if ([string]::IsNullOrWhiteSpace($ImmutableSchemeSourcePath)) {
    ""
}
else {
    (Resolve-Path -LiteralPath $ImmutableSchemeSourcePath).Path
}
$immutableSchemeSha256 = if ([string]::IsNullOrWhiteSpace($resolvedImmutableSchemeSourcePath)) {
    ""
}
else {
    (Get-FileHash -LiteralPath $resolvedImmutableSchemeSourcePath -Algorithm SHA256).Hash
}
Restore-ImmutableScheme -SourcePath $resolvedImmutableSchemeSourcePath -TargetPath $resolvedSchemePath -ExpectedSha256 $immutableSchemeSha256
$workingDirectory = Split-Path -Parent $resolvedExecutablePath
if ($RunMode -eq "FixedIntervalTriggers" -and [string]::IsNullOrWhiteSpace($RuntimeReferenceDirectory)) {
    throw "固定节拍触发必须提供RuntimeReferenceDirectory，自动同步旧版许可证和界面布局后才能运行。"
}
$runtimeInputHashes = Sync-TestRuntimeConfiguration `
    -WorkingDirectory $workingDirectory `
    -ReferenceDirectory $RuntimeReferenceDirectory `
    -VisibleImageWindows $ExpectedVisibleImageWindows
$logDirectory = Join-Path $workingDirectory "Logs"
$startedByScript = $false
$executionStateWasSet = $false
$keepSystemAwake = [uint32]2147483649
$continuousOnly = [uint32]2147483648

if ([FullSolutionGuiResourceProbe]::SetThreadExecutionState($keepSystemAwake) -eq 0) {
    throw "设置长测期间系统保持唤醒失败。"
}
$executionStateWasSet = $true

if ($ExistingProcessId -gt 0) {
    if ($RunMode -eq "FixedIntervalTriggers") {
        throw "固定节拍触发必须由本工具启动Debug程序，不能复用未开放测试消息的既有进程。"
    }
    $mainProcess = Get-Process -Id $ExistingProcessId
    if (-not [string]::Equals($mainProcess.Path, $resolvedExecutablePath, [StringComparison]::OrdinalIgnoreCase)) {
        throw "ExistingProcessId对应的程序路径与ExecutablePath不一致。"
    }
}
else {
    $startProcessArguments = @{
        FilePath = $resolvedExecutablePath
        WorkingDirectory = $workingDirectory
        ArgumentList = @("`"$resolvedSchemePath`"")
        PassThru = $true
    }
    if ($WindowMode -eq "Minimized") {
        $startProcessArguments.WindowStyle = "Minimized"
    }
    try {
        if ($RunMode -eq "FixedIntervalTriggers") {
            $env:TDJS_VISION_FIXED_TRIGGER_STRESS = "1"
            $env:TDJS_VISION_FIXED_TRIGGER_EXPECTED_ATTEMPTS = [string]$Rounds
            $env:TDJS_VISION_FIXED_TRIGGER_INTERVAL_MS = [string]$TriggerIntervalMilliseconds
        }
        $mainProcess = Start-Process @startProcessArguments
        $startedByScript = $true
    }
    finally {
        if ($RunMode -eq "FixedIntervalTriggers") {
            Remove-Item Env:TDJS_VISION_FIXED_TRIGGER_STRESS -ErrorAction SilentlyContinue
            Remove-Item Env:TDJS_VISION_FIXED_TRIGGER_EXPECTED_ATTEMPTS -ErrorAction SilentlyContinue
            Remove-Item Env:TDJS_VISION_FIXED_TRIGGER_INTERVAL_MS -ErrorAction SilentlyContinue
        }
    }
}

try {
    $runOnceButton = Find-RunOnceButton -MainProcess $mainProcess -WindowMode $WindowMode
    $mainProcess.Refresh()
    Set-TestProcessPriority -ResolvedExecutablePath $resolvedExecutablePath -MainProcessStartTime $mainProcess.StartTime -PriorityName $ProcessPriority
    Set-TestWindowMode -MainProcess $mainProcess -Mode $WindowMode
    for ($warmup = 1; $warmup -le $WarmupRounds; $warmup++) {
        $warmupElapsed = Invoke-SolutionRound -RunOnceButton $runOnceButton -TimeoutSeconds $RoundTimeoutSeconds -PollMilliseconds $UiPollMilliseconds
        Write-Output ("WARMUP {0}/{1} elapsed_ms={2:N3}" -f $warmup, $WarmupRounds, $warmupElapsed)
    }

    if ($RunMode -eq "FixedIntervalTriggers") {
        $fixedTriggerMessage = [uint32]0x8451
        $fixedTriggerResetMessage = [uint32]0x8452
        $mainProcess.Refresh()
        $resetResult = Send-FixedTriggerMessage `
            -WindowHandle $mainProcess.MainWindowHandle `
            -Message $fixedTriggerResetMessage `
            -TimeoutMilliseconds $TriggerMessageTimeoutMilliseconds
        if (-not $resetResult.Delivered -or $resetResult.Result -ne 1) {
            throw "固定触发正式测试前无法清除预热计数，活动方案可能尚未退出。"
        }

        $logOffsets = Get-LogOffsets -LogDirectory $logDirectory
        $mainProcess.Refresh()
        $mainStartTime = $mainProcess.StartTime
        $logicalProcessorCount = [Math]::Max(1, [Environment]::ProcessorCount)
        $initial = Get-ResourceSnapshot -ResolvedExecutablePath $resolvedExecutablePath -MainProcessStartTime $mainStartTime -MainProcessId $mainProcess.Id -IncludeManagedHeap $true
        if ($initial.ProcessCount -ne $ExpectedProcessTreeCount) {
            throw "固定触发压力要求进程树为${ExpectedProcessTreeCount}个进程，实际为$($initial.ProcessCount)个。"
        }

        $peak = $initial.PSObject.Copy()
        $lastSnapshot = $initial
        $peakCpuPercent = 0.0
        $acceptedByHarness = 0
        $busyByHarness = 0
        $messageTimeoutCount = 0
        $csvRows = New-Object System.Collections.Generic.List[object]
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
        $safeLabel = $VersionLabel -replace '[^0-9A-Za-z\u4e00-\u9fa5_-]', '_'
        $jsonPath = Join-Path $OutputDirectory "${timestamp}-${safeLabel}-${Rounds}次固定触发.json"
        $csvPath = Join-Path $OutputDirectory "${timestamp}-${safeLabel}-${Rounds}次固定触发.csv"
        $checkpointPath = Join-Path $OutputDirectory "${timestamp}-${safeLabel}-${Rounds}次固定触发-checkpoint.json"
        $formalStopwatch = [Diagnostics.Stopwatch]::StartNew()

        for ($attempt = 1; $attempt -le $Rounds; $attempt++) {
            $scheduledMilliseconds = [double](($attempt - 1) * $TriggerIntervalMilliseconds)
            while ($formalStopwatch.Elapsed.TotalMilliseconds -lt $scheduledMilliseconds) {
                $remainingMilliseconds = $scheduledMilliseconds - $formalStopwatch.Elapsed.TotalMilliseconds
                if ($remainingMilliseconds -gt 2.0) {
                    Start-Sleep -Milliseconds ([Math]::Max(1, [int][Math]::Floor($remainingMilliseconds - 1.0)))
                }
                else {
                    [Threading.Thread]::SpinWait(64)
                }
            }

            $actualMilliseconds = $formalStopwatch.Elapsed.TotalMilliseconds
            $dispatchStopwatch = [Diagnostics.Stopwatch]::StartNew()
            $triggerResult = Send-FixedTriggerMessage `
                -WindowHandle $mainProcess.MainWindowHandle `
                -Message $fixedTriggerMessage `
                -TimeoutMilliseconds $TriggerMessageTimeoutMilliseconds
            $dispatchStopwatch.Stop()

            if (-not $triggerResult.Delivered) {
                $outcome = "消息超时"
                $messageTimeoutCount++
            }
            elseif ($triggerResult.Result -eq 1) {
                $outcome = "准入"
                $acceptedByHarness++
            }
            else {
                $outcome = "忙碌拒绝"
                $busyByHarness++
            }

            $includeManaged = ($attempt % [Math]::Max(1, $ResourceSampleInterval * 10) -eq 0) -or ($attempt -eq $Rounds)
            if (($attempt % $ResourceSampleInterval -eq 0) -or $attempt -eq 1 -or $attempt -eq $Rounds) {
                $snapshot = Get-ResourceSnapshot -ResolvedExecutablePath $resolvedExecutablePath -MainProcessStartTime $mainStartTime -MainProcessId $mainProcess.Id -IncludeManagedHeap $includeManaged
                $sampleWallMilliseconds = [Math]::Max(1.0, ($snapshot.TimestampUtc - $lastSnapshot.TimestampUtc).TotalMilliseconds)
                $cpuDeltaMilliseconds = [Math]::Max(0.0, $snapshot.TotalCpuMilliseconds - $lastSnapshot.TotalCpuMilliseconds)
                $cpuPercent = ($cpuDeltaMilliseconds / $sampleWallMilliseconds) * (100.0 / $logicalProcessorCount)
                $peakCpuPercent = [Math]::Max($peakCpuPercent, $cpuPercent)
                foreach ($property in @("ProcessCount", "PrivateBytes", "MainPrivateBytes", "GdiObjects", "UserObjects", "Handles")) {
                    if ($snapshot.$property -gt $peak.$property) {
                        $peak.$property = $snapshot.$property
                    }
                }
                if ($snapshot.ManagedHeapBytes -ge 0 -and $snapshot.ManagedHeapBytes -gt $peak.ManagedHeapBytes) {
                    $peak.ManagedHeapBytes = $snapshot.ManagedHeapBytes
                }
                $lastSnapshot = $snapshot
            }
            else {
                $snapshot = $lastSnapshot
                $cpuPercent = 0.0
            }

            $csvRows.Add([pscustomobject]@{
                Attempt = $attempt
                ScheduledMilliseconds = [Math]::Round($scheduledMilliseconds, 3)
                ActualDispatchMilliseconds = [Math]::Round($actualMilliseconds, 3)
                ScheduleDriftMilliseconds = [Math]::Round($actualMilliseconds - $scheduledMilliseconds, 3)
                MessageDispatchMilliseconds = [Math]::Round($dispatchStopwatch.Elapsed.TotalMilliseconds, 3)
                Outcome = $outcome
                ProcessCount = $snapshot.ProcessCount
                PrivateMemoryMegabytes = [Math]::Round($snapshot.PrivateBytes / 1MB, 3)
                ManagedHeapMegabytes = if ($snapshot.ManagedHeapBytes -ge 0) { [Math]::Round($snapshot.ManagedHeapBytes / 1MB, 3) } else { $null }
                GdiObjects = $snapshot.GdiObjects
                Handles = $snapshot.Handles
                CpuPercent = [Math]::Round($cpuPercent, 3)
            })

            if (($attempt % $ProgressInterval -eq 0) -or ($attempt -eq $Rounds)) {
                Set-TestProcessPriority -ResolvedExecutablePath $resolvedExecutablePath -MainProcessStartTime $mainStartTime -PriorityName $ProcessPriority
                Set-TestWindowMode -MainProcess $mainProcess -Mode $WindowMode
                $csvRows | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
                [ordered]@{
                    VersionLabel = $VersionLabel
                    RunMode = $RunMode
                    CompletedAttempts = $attempt
                    TargetAttempts = $Rounds
                    Accepted = $acceptedByHarness
                    BusyRejected = $busyByHarness
                    MessageTimeouts = $messageTimeoutCount
                    LastScheduleDriftMilliseconds = [Math]::Round($actualMilliseconds - $scheduledMilliseconds, 3)
                    UpdatedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
                } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $checkpointPath -Encoding UTF8
                Write-Output ("TRIGGER_PROGRESS {0}/{1} accepted={2} busy={3} timeout={4} drift_ms={5:N3}" -f $attempt, $Rounds, $acceptedByHarness, $busyByHarness, $messageTimeoutCount, ($actualMilliseconds - $scheduledMilliseconds))
            }
        }

        $injectionWallMilliseconds = $formalStopwatch.Elapsed.TotalMilliseconds
        $summaryMatch = $null
        $summaryDeadline = [DateTime]::UtcNow.AddSeconds($RoundTimeoutSeconds)
        while ([DateTime]::UtcNow -lt $summaryDeadline) {
            Start-Sleep -Milliseconds $UiPollMilliseconds
            $appendedLogText = Get-AppendedLogText -LogDirectory $logDirectory -Offsets $logOffsets
            $summaryMatches = [regex]::Matches(
                $appendedLogText,
                "【固定触发汇总】间隔=(?<interval>\d+)ms；请求=(?<requested>\d+)；准入=(?<accepted>\d+)；忙碌拒绝=(?<busy>\d+)；完成=(?<completed>\d+)；异常=(?<failed>\d+)；活动=(?<active>True|False)；耗时样本=(?<samples>\d+)；P50=(?<p50>\d+)ms；P95=(?<p95>\d+)ms；P99=(?<p99>\d+)ms；最大=(?<max>\d+)ms")
            if ($summaryMatches.Count -gt 0) {
                $summaryMatch = $summaryMatches[$summaryMatches.Count - 1]
                break
            }

            $drainSnapshot = Get-ResourceSnapshot -ResolvedExecutablePath $resolvedExecutablePath -MainProcessStartTime $mainStartTime -MainProcessId $mainProcess.Id -IncludeManagedHeap $false
            foreach ($property in @("ProcessCount", "PrivateBytes", "MainPrivateBytes", "GdiObjects", "UserObjects", "Handles")) {
                if ($drainSnapshot.$property -gt $peak.$property) {
                    $peak.$property = $drainSnapshot.$property
                }
            }
        }
        $formalStopwatch.Stop()

        if ($null -eq $summaryMatch) {
            throw "500次固定触发发出后等待最终方案退出和汇总超过${RoundTimeoutSeconds}秒。"
        }

        $controllerRequested = [long]$summaryMatch.Groups['requested'].Value
        $controllerAccepted = [long]$summaryMatch.Groups['accepted'].Value
        $controllerBusy = [long]$summaryMatch.Groups['busy'].Value
        $controllerCompleted = [long]$summaryMatch.Groups['completed'].Value
        $controllerFailed = [long]$summaryMatch.Groups['failed'].Value
        if ($messageTimeoutCount -ne 0) {
            throw "固定触发存在${messageTimeoutCount}次窗口消息投递超时，本轮不能作为有效压力数据。"
        }
        if ($controllerRequested -ne $Rounds -or $controllerAccepted -ne $acceptedByHarness -or $controllerBusy -ne $busyByHarness) {
            throw "外部投递计数与程序准入汇总不一致：外部=$Rounds/$acceptedByHarness/$busyByHarness，程序=$controllerRequested/$controllerAccepted/$controllerBusy。"
        }
        if ($controllerCompleted -ne $controllerAccepted -or $controllerFailed -ne 0) {
            throw "准入轮次没有全部正常退出：准入=$controllerAccepted，完成=$controllerCompleted，异常=$controllerFailed。"
        }

        $final = Get-ResourceSnapshot -ResolvedExecutablePath $resolvedExecutablePath -MainProcessStartTime $mainStartTime -MainProcessId $mainProcess.Id -IncludeManagedHeap $true
        $appendedLogText = Get-AppendedLogText -LogDirectory $logDirectory -Offsets $logOffsets
        $processSummaries = @(
            Get-ProcessLogSummary -LogText $appendedLogText -ProcessName "中心端子正面检测"
            Get-ProcessLogSummary -LogText $appendedLogText -ProcessName "同心度检测"
            Get-ProcessLogSummary -LogText $appendedLogText -ProcessName "中心端子侧面左检测"
            Get-ProcessLogSummary -LogText $appendedLogText -ProcessName "中心端子侧面右检测"
        )
        $invalidProcessSummaries = @($processSummaries | Where-Object { $_.Success -ne $controllerAccepted -or $_.Failed -ne 0 })
        if ($invalidProcessSummaries.Count -ne 0) {
            throw "固定触发准入轮次没有让四个流程各成功一次，请检查结果JSON中的ProcessResults。"
        }

        $exceptionCount = [regex]::Matches($appendedLogText, "(?m)^.*\sException\s.*$").Count
        $modbusSuccessCount = [regex]::Matches($appendedLogText, "Modbus发送信号完毕").Count
        $modbusFailureCount = [regex]::Matches($appendedLogText, "(?mi)^.*Exception.*Modbus.*$").Count +
            [regex]::Matches($appendedLogText, "(?mi)^.*Modbus.*(失败|超时).*$").Count
        if ($exceptionCount -ne 0 -or $modbusFailureCount -ne 0 -or $modbusSuccessCount -ne ($controllerAccepted * 4)) {
            throw "固定触发业务校验失败：异常=$exceptionCount，Modbus成功=$modbusSuccessCount，期望=$($controllerAccepted * 4)，Modbus失败=$modbusFailureCount。"
        }

        $driftValues = @($csvRows | ForEach-Object { [double]$_.ScheduleDriftMilliseconds })
        $dispatchValues = @($csvRows | ForEach-Object { [double]$_.MessageDispatchMilliseconds })
        $averageCpuPercent = (($final.TotalCpuMilliseconds - $initial.TotalCpuMilliseconds) /
            [Math]::Max(1.0, $formalStopwatch.Elapsed.TotalMilliseconds)) * (100.0 / $logicalProcessorCount)
        $result = [ordered]@{
            VersionLabel = $VersionLabel
            RunMode = $RunMode
            ExecutablePath = $resolvedExecutablePath
            ExecutableSha256 = (Get-FileHash -LiteralPath $resolvedExecutablePath -Algorithm SHA256).Hash
            SchemePath = $resolvedSchemePath
            SchemeSha256 = (Get-FileHash -LiteralPath $resolvedSchemePath -Algorithm SHA256).Hash
            RuntimeInputHashes = $runtimeInputHashes
            WindowMode = $WindowMode
            ProcessPriority = $ProcessPriority
            WarmupRounds = $WarmupRounds
            Trigger = [ordered]@{
                Attempted = $Rounds
                IntervalMilliseconds = $TriggerIntervalMilliseconds
                InjectionWallMilliseconds = [Math]::Round($injectionWallMilliseconds, 3)
                Accepted = $controllerAccepted
                BusyRejected = $controllerBusy
                MessageTimeouts = $messageTimeoutCount
                AdmissionPercent = [Math]::Round(($controllerAccepted * 100.0 / $Rounds), 3)
            }
            Scheduling = [ordered]@{
                DriftP50Milliseconds = [Math]::Round((Get-Percentile -Values $driftValues -Percentile 50), 3)
                DriftP95Milliseconds = [Math]::Round((Get-Percentile -Values $driftValues -Percentile 95), 3)
                DriftP99Milliseconds = [Math]::Round((Get-Percentile -Values $driftValues -Percentile 99), 3)
                DriftMaxMilliseconds = [Math]::Round(($driftValues | Measure-Object -Maximum).Maximum, 3)
                DispatchP95Milliseconds = [Math]::Round((Get-Percentile -Values $dispatchValues -Percentile 95), 3)
                DispatchMaxMilliseconds = [Math]::Round(($dispatchValues | Measure-Object -Maximum).Maximum, 3)
            }
            AcceptedRunTiming = [ordered]@{
                Samples = [long]$summaryMatch.Groups['samples'].Value
                P50Milliseconds = [long]$summaryMatch.Groups['p50'].Value
                P95Milliseconds = [long]$summaryMatch.Groups['p95'].Value
                P99Milliseconds = [long]$summaryMatch.Groups['p99'].Value
                MaxMilliseconds = [long]$summaryMatch.Groups['max'].Value
            }
            Cpu = [ordered]@{
                LogicalProcessorCount = $logicalProcessorCount
                AveragePercent = [Math]::Round($averageCpuPercent, 3)
                PeakSamplePercent = [Math]::Round($peakCpuPercent, 3)
            }
            Resources = [ordered]@{
                ProcessTreeCount = [ordered]@{ Start = $initial.ProcessCount; Peak = $peak.ProcessCount; End = $final.ProcessCount }
                MainPrivateMemoryMegabytes = [ordered]@{ Start = [Math]::Round($initial.MainPrivateBytes / 1MB, 3); Peak = [Math]::Round($peak.MainPrivateBytes / 1MB, 3); End = [Math]::Round($final.MainPrivateBytes / 1MB, 3) }
                ProcessTreePrivateMemoryMegabytes = [ordered]@{ Start = [Math]::Round($initial.PrivateBytes / 1MB, 3); Peak = [Math]::Round($peak.PrivateBytes / 1MB, 3); End = [Math]::Round($final.PrivateBytes / 1MB, 3) }
                ProcessTreeManagedHeapMegabytes = [ordered]@{ Start = [Math]::Round($initial.ManagedHeapBytes / 1MB, 3); Peak = [Math]::Round($peak.ManagedHeapBytes / 1MB, 3); End = [Math]::Round($final.ManagedHeapBytes / 1MB, 3) }
                GdiObjects = [ordered]@{ Start = $initial.GdiObjects; Peak = $peak.GdiObjects; End = $final.GdiObjects }
                UserObjects = [ordered]@{ Start = $initial.UserObjects; Peak = $peak.UserObjects; End = $final.UserObjects }
                Handles = [ordered]@{ Start = $initial.Handles; Peak = $peak.Handles; End = $final.Handles }
            }
            ProcessResults = $processSummaries
            ExceptionLogCount = $exceptionCount
            Modbus = [ordered]@{ SuccessLogCount = $modbusSuccessCount; FailureLogCount = $modbusFailureCount }
            ResultFiles = [ordered]@{ Json = $jsonPath; Csv = $csvPath; Checkpoint = $checkpointPath }
        }

        $csvRows | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
        $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
        [ordered]@{
            VersionLabel = $VersionLabel
            RunMode = $RunMode
            CompletedAttempts = $Rounds
            TargetAttempts = $Rounds
            Completed = $true
            ResultJson = $jsonPath
            ResultCsv = $csvPath
            UpdatedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
        } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $checkpointPath -Encoding UTF8
        Write-Output ("RESULT_JSON={0}" -f $jsonPath)
        Write-Output ("RESULT_CSV={0}" -f $csvPath)
        Write-Output ($result | ConvertTo-Json -Depth 10)
        return
    }

    $logOffsets = Get-LogOffsets -LogDirectory $logDirectory
    $mainProcess.Refresh()
    $mainStartTime = $mainProcess.StartTime
    $logicalProcessorCount = [Math]::Max(1, [Environment]::ProcessorCount)
    $initial = Get-ResourceSnapshot -ResolvedExecutablePath $resolvedExecutablePath -MainProcessStartTime $mainStartTime -MainProcessId $mainProcess.Id -IncludeManagedHeap $true
    if ($initial.IoCounterProcessCount -ne $initial.ProcessCount) {
        throw "正式测试前进程I/O计数器采集不完整：进程=$($initial.ProcessCount)，I/O计数器=$($initial.IoCounterProcessCount)。"
    }
    $peak = $initial.PSObject.Copy()
    $lastSnapshot = $initial
    $peakCpuPercent = 0.0
    $durations = New-Object System.Collections.Generic.List[double]
    $csvRows = New-Object System.Collections.Generic.List[object]
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $safeLabel = $VersionLabel -replace '[^0-9A-Za-z\u4e00-\u9fa5_-]', '_'
    $jsonPath = Join-Path $OutputDirectory "${timestamp}-${safeLabel}-${Rounds}轮.json"
    $csvPath = Join-Path $OutputDirectory "${timestamp}-${safeLabel}-${Rounds}轮.csv"
    $checkpointPath = Join-Path $OutputDirectory "${timestamp}-${safeLabel}-${Rounds}轮-checkpoint.json"
    $formalStopwatch = [Diagnostics.Stopwatch]::StartNew()

    for ($round = 1; $round -le $Rounds; $round++) {
        $roundElapsed = Invoke-SolutionRound -RunOnceButton $runOnceButton -TimeoutSeconds $RoundTimeoutSeconds -PollMilliseconds $UiPollMilliseconds
        $durations.Add($roundElapsed)

        $includeManaged = ($round % $ResourceSampleInterval -eq 0) -or ($round -eq $Rounds)
        $snapshot = Get-ResourceSnapshot -ResolvedExecutablePath $resolvedExecutablePath -MainProcessStartTime $mainStartTime -MainProcessId $mainProcess.Id -IncludeManagedHeap $includeManaged
        $sampleWallMilliseconds = [Math]::Max(1.0, ($snapshot.TimestampUtc - $lastSnapshot.TimestampUtc).TotalMilliseconds)
        $cpuDeltaMilliseconds = [Math]::Max(0.0, $snapshot.TotalCpuMilliseconds - $lastSnapshot.TotalCpuMilliseconds)
        $cpuPercent = ($cpuDeltaMilliseconds / $sampleWallMilliseconds) * (100.0 / $logicalProcessorCount)
        $peakCpuPercent = [Math]::Max($peakCpuPercent, $cpuPercent)
        $readOperationDelta = Get-UnsignedCounterDelta -Current $snapshot.ReadOperationCount -Previous $lastSnapshot.ReadOperationCount
        $writeOperationDelta = Get-UnsignedCounterDelta -Current $snapshot.WriteOperationCount -Previous $lastSnapshot.WriteOperationCount
        $otherOperationDelta = Get-UnsignedCounterDelta -Current $snapshot.OtherOperationCount -Previous $lastSnapshot.OtherOperationCount
        $readTransferDelta = Get-UnsignedCounterDelta -Current $snapshot.ReadTransferBytes -Previous $lastSnapshot.ReadTransferBytes
        $writeTransferDelta = Get-UnsignedCounterDelta -Current $snapshot.WriteTransferBytes -Previous $lastSnapshot.WriteTransferBytes
        $otherTransferDelta = Get-UnsignedCounterDelta -Current $snapshot.OtherTransferBytes -Previous $lastSnapshot.OtherTransferBytes

        foreach ($property in @("ProcessCount", "PrivateBytes", "MainPrivateBytes", "GdiObjects", "UserObjects", "Handles")) {
            if ($snapshot.$property -gt $peak.$property) {
                $peak.$property = $snapshot.$property
            }
        }
        if ($snapshot.ManagedHeapBytes -ge 0 -and $snapshot.ManagedHeapBytes -gt $peak.ManagedHeapBytes) {
            $peak.ManagedHeapBytes = $snapshot.ManagedHeapBytes
        }

        $csvRows.Add([pscustomobject]@{
            Round = $round
            ElapsedMilliseconds = [Math]::Round($roundElapsed, 3)
            CpuPercent = [Math]::Round($cpuPercent, 3)
            ProcessCount = $snapshot.ProcessCount
            PrivateMemoryMegabytes = [Math]::Round($snapshot.PrivateBytes / 1MB, 3)
            MainPrivateMemoryMegabytes = [Math]::Round($snapshot.MainPrivateBytes / 1MB, 3)
            ManagedHeapMegabytes = if ($snapshot.ManagedHeapBytes -ge 0) { [Math]::Round($snapshot.ManagedHeapBytes / 1MB, 3) } else { $null }
            GdiObjects = $snapshot.GdiObjects
            UserObjects = $snapshot.UserObjects
            Handles = $snapshot.Handles
            ReadOperations = $readOperationDelta
            WriteOperations = $writeOperationDelta
            OtherIoOperations = $otherOperationDelta
            ReadTransferKilobytes = [Math]::Round($readTransferDelta / 1KB, 3)
            WriteTransferKilobytes = [Math]::Round($writeTransferDelta / 1KB, 3)
            OtherIoTransferKilobytes = [Math]::Round($otherTransferDelta / 1KB, 3)
        })

        $lastSnapshot = $snapshot
        if (($round % $ProgressInterval -eq 0) -or ($round -eq $Rounds)) {
            Set-TestProcessPriority -ResolvedExecutablePath $resolvedExecutablePath -MainProcessStartTime $mainStartTime -PriorityName $ProcessPriority
            Set-TestWindowMode -MainProcess $mainProcess -Mode $WindowMode
            $currentP95 = Get-Percentile -Values $durations.ToArray() -Percentile 95
            $csvRows | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
            [ordered]@{
                VersionLabel = $VersionLabel
                ExecutablePath = $resolvedExecutablePath
                SchemePath = $resolvedSchemePath
                CompletedRounds = $round
                TargetRounds = $Rounds
                LastRoundMilliseconds = [Math]::Round($roundElapsed, 3)
                CurrentP95Milliseconds = [Math]::Round($currentP95, 3)
                ProcessTreePrivateMemoryMegabytes = [Math]::Round($snapshot.PrivateBytes / 1MB, 3)
                ManagedHeapMegabytes = if ($snapshot.ManagedHeapBytes -ge 0) { [Math]::Round($snapshot.ManagedHeapBytes / 1MB, 3) } else { $null }
                GdiObjects = $snapshot.GdiObjects
                Handles = $snapshot.Handles
                UpdatedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
            } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $checkpointPath -Encoding UTF8
            Write-Output ("PROGRESS {0}/{1} last_ms={2:N3} p95_ms={3:N3} private_mb={4:N2} managed_mb={5:N2} gdi={6} handles={7} cpu={8:N2}%" -f
                $round,
                $Rounds,
                $roundElapsed,
                $currentP95,
                ($snapshot.PrivateBytes / 1MB),
                $(if ($snapshot.ManagedHeapBytes -ge 0) { $snapshot.ManagedHeapBytes / 1MB } else { 0 }),
                $snapshot.GdiObjects,
                $snapshot.Handles,
                $cpuPercent)
        }
    }

    $formalStopwatch.Stop()
    $formalWallMilliseconds = $formalStopwatch.Elapsed.TotalMilliseconds
    $ioEndSnapshot = Get-ResourceSnapshot -ResolvedExecutablePath $resolvedExecutablePath -MainProcessStartTime $mainStartTime -MainProcessId $mainProcess.Id -IncludeManagedHeap $false
    $processIdsStable = Test-StableProcessIds -InitialProcessIds $initial.ProcessIds -FinalProcessIds $ioEndSnapshot.ProcessIds
    if (-not $processIdsStable) {
        throw "正式测试期间进程树发生重启，进程I/O累计值不能作为有效同比。"
    }
    if ($ioEndSnapshot.IoCounterProcessCount -ne $ioEndSnapshot.ProcessCount) {
        throw "正式测试结束时进程I/O计数器采集不完整：进程=$($ioEndSnapshot.ProcessCount)，I/O计数器=$($ioEndSnapshot.IoCounterProcessCount)。"
    }
    Start-Sleep -Milliseconds 500
    $final = Get-ResourceSnapshot -ResolvedExecutablePath $resolvedExecutablePath -MainProcessStartTime $mainStartTime -MainProcessId $mainProcess.Id -IncludeManagedHeap $true
    $appendedLogText = Get-AppendedLogText -LogDirectory $logDirectory -Offsets $logOffsets
    $processSummaries = @(
        Get-ProcessLogSummary -LogText $appendedLogText -ProcessName "中心端子正面检测"
        Get-ProcessLogSummary -LogText $appendedLogText -ProcessName "同心度检测"
        Get-ProcessLogSummary -LogText $appendedLogText -ProcessName "中心端子侧面左检测"
        Get-ProcessLogSummary -LogText $appendedLogText -ProcessName "中心端子侧面右检测"
    )

    $allProcessesExactlySuccessful = @($processSummaries | Where-Object { $_.Success -ne $Rounds -or $_.Failed -ne 0 }).Count -eq 0
    $exceptionCount = [regex]::Matches($appendedLogText, "(?m)^.*\sException\s.*$").Count
    $modbusSuccessCount = [regex]::Matches($appendedLogText, "Modbus发送信号完毕").Count
    $modbusFailureCount = [regex]::Matches($appendedLogText, "(?mi)^.*Exception.*Modbus.*$").Count +
        [regex]::Matches($appendedLogText, "(?mi)^.*Modbus.*(失败|超时).*$").Count
    $queueMatches = [regex]::Matches($appendedLogText, "队列=(?<count>\d+)/(?<capacity>\d+)")
    $queuePeak = if ($queueMatches.Count -gt 0) {
        ($queueMatches | ForEach-Object { [int]$_.Groups["count"].Value } | Measure-Object -Maximum).Maximum
    }
    else {
        $null
    }
    $queueRejected = [regex]::Matches($appendedLogText, "保存队列拒绝当前图片").Count
    $queueEvicted = [regex]::Matches($appendedLogText, "保存队列达到边界，为保留NG图已淘汰普通图片").Count

    $durationArray = $durations.ToArray()
    $sumExecutionMilliseconds = ($durationArray | Measure-Object -Sum).Sum
    $averageCpuPercent = (($final.TotalCpuMilliseconds - $initial.TotalCpuMilliseconds) /
        [Math]::Max(1.0, $formalWallMilliseconds)) * (100.0 / $logicalProcessorCount)
    $readOperationCount = Get-UnsignedCounterDelta -Current $ioEndSnapshot.ReadOperationCount -Previous $initial.ReadOperationCount
    $writeOperationCount = Get-UnsignedCounterDelta -Current $ioEndSnapshot.WriteOperationCount -Previous $initial.WriteOperationCount
    $otherOperationCount = Get-UnsignedCounterDelta -Current $ioEndSnapshot.OtherOperationCount -Previous $initial.OtherOperationCount
    $readTransferBytes = Get-UnsignedCounterDelta -Current $ioEndSnapshot.ReadTransferBytes -Previous $initial.ReadTransferBytes
    $writeTransferBytes = Get-UnsignedCounterDelta -Current $ioEndSnapshot.WriteTransferBytes -Previous $initial.WriteTransferBytes
    $otherTransferBytes = Get-UnsignedCounterDelta -Current $ioEndSnapshot.OtherTransferBytes -Previous $initial.OtherTransferBytes
    $readTransferPerRound = [double[]]@($csvRows | ForEach-Object { [double]$_.ReadTransferKilobytes })
    $writeTransferPerRound = [double[]]@($csvRows | ForEach-Object { [double]$_.WriteTransferKilobytes })
    $result = [ordered]@{
        VersionLabel = $VersionLabel
        ExecutablePath = $resolvedExecutablePath
        ExecutableSha256 = (Get-FileHash -LiteralPath $resolvedExecutablePath -Algorithm SHA256).Hash
        SchemePath = $resolvedSchemePath
        SchemeSha256 = (Get-FileHash -LiteralPath $resolvedSchemePath -Algorithm SHA256).Hash
        ImmutableSchemeSourcePath = $resolvedImmutableSchemeSourcePath
        ImmutableSchemeSha256 = $immutableSchemeSha256
        WindowMode = $WindowMode
        ProcessPriority = $ProcessPriority
        UiPollMilliseconds = $UiPollMilliseconds
        DebugSwitch = "使用程序当前用户设置；本轮未通过自动化修改"
        UiMode = "单次运行按钮串行调用，确保四流程每轮各执行一次"
        WarmupRounds = $WarmupRounds
        FormalRounds = $Rounds
        ExactSuccessRounds = if ($allProcessesExactlySuccessful) { $Rounds } else { $null }
        ExactFailedRounds = if ($allProcessesExactlySuccessful) { 0 } else { $null }
        ExceptionLogCount = $exceptionCount
        Timing = [ordered]@{
            ExecutionTotalMilliseconds = [Math]::Round($sumExecutionMilliseconds, 3)
            HarnessWallMilliseconds = [Math]::Round($formalWallMilliseconds, 3)
            ThroughputRoundsPerSecond = [Math]::Round($Rounds / [Math]::Max(0.001, $sumExecutionMilliseconds / 1000.0), 6)
            P50Milliseconds = [Math]::Round((Get-Percentile -Values $durationArray -Percentile 50), 3)
            P95Milliseconds = [Math]::Round((Get-Percentile -Values $durationArray -Percentile 95), 3)
            P99Milliseconds = [Math]::Round((Get-Percentile -Values $durationArray -Percentile 99), 3)
            MaxMilliseconds = [Math]::Round(($durationArray | Measure-Object -Maximum).Maximum, 3)
        }
        Cpu = [ordered]@{
            LogicalProcessorCount = $logicalProcessorCount
            AveragePercent = [Math]::Round($averageCpuPercent, 3)
            PeakSamplePercent = [Math]::Round($peakCpuPercent, 3)
        }
        Resources = [ordered]@{
            ProcessTreeCount = [ordered]@{ Start = $initial.ProcessCount; Peak = $peak.ProcessCount; End = $final.ProcessCount }
            MainPrivateMemoryMegabytes = [ordered]@{ Start = [Math]::Round($initial.MainPrivateBytes / 1MB, 3); Peak = [Math]::Round($peak.MainPrivateBytes / 1MB, 3); End = [Math]::Round($final.MainPrivateBytes / 1MB, 3) }
            ProcessTreePrivateMemoryMegabytes = [ordered]@{ Start = [Math]::Round($initial.PrivateBytes / 1MB, 3); Peak = [Math]::Round($peak.PrivateBytes / 1MB, 3); End = [Math]::Round($final.PrivateBytes / 1MB, 3) }
            ProcessTreeManagedHeapMegabytes = [ordered]@{ Start = [Math]::Round($initial.ManagedHeapBytes / 1MB, 3); Peak = [Math]::Round($peak.ManagedHeapBytes / 1MB, 3); End = [Math]::Round($final.ManagedHeapBytes / 1MB, 3) }
            GdiObjects = [ordered]@{ Start = $initial.GdiObjects; Peak = $peak.GdiObjects; End = $final.GdiObjects }
            UserObjects = [ordered]@{ Start = $initial.UserObjects; Peak = $peak.UserObjects; End = $final.UserObjects }
            Handles = [ordered]@{ Start = $initial.Handles; Peak = $peak.Handles; End = $final.Handles }
        }
        ProcessIo = [ordered]@{
            MeasurementScope = "Windows进程I/O累计值，覆盖主程序和同路径AI工作进程；包含文件、设备和管道等进程I/O，不等同于物理磁盘独占流量"
            AllProcessCountersCaptured = ($initial.IoCounterProcessCount -eq $initial.ProcessCount -and $ioEndSnapshot.IoCounterProcessCount -eq $ioEndSnapshot.ProcessCount)
            ProcessIdsStable = $processIdsStable
            Read = [ordered]@{
                Operations = $readOperationCount
                TransferBytes = $readTransferBytes
                TransferMegabytes = [Math]::Round($readTransferBytes / 1MB, 3)
                MegabytesPerSecond = [Math]::Round(($readTransferBytes / 1MB) / [Math]::Max(0.001, $formalWallMilliseconds / 1000.0), 3)
                KilobytesPerRound = [Math]::Round(($readTransferBytes / 1KB) / $Rounds, 3)
                PerRoundP50Kilobytes = [Math]::Round((Get-Percentile -Values $readTransferPerRound -Percentile 50), 3)
                PerRoundP95Kilobytes = [Math]::Round((Get-Percentile -Values $readTransferPerRound -Percentile 95), 3)
                PerRoundP99Kilobytes = [Math]::Round((Get-Percentile -Values $readTransferPerRound -Percentile 99), 3)
                PerRoundMaxKilobytes = [Math]::Round(($readTransferPerRound | Measure-Object -Maximum).Maximum, 3)
            }
            Write = [ordered]@{
                Operations = $writeOperationCount
                TransferBytes = $writeTransferBytes
                TransferMegabytes = [Math]::Round($writeTransferBytes / 1MB, 3)
                MegabytesPerSecond = [Math]::Round(($writeTransferBytes / 1MB) / [Math]::Max(0.001, $formalWallMilliseconds / 1000.0), 3)
                KilobytesPerRound = [Math]::Round(($writeTransferBytes / 1KB) / $Rounds, 3)
                PerRoundP50Kilobytes = [Math]::Round((Get-Percentile -Values $writeTransferPerRound -Percentile 50), 3)
                PerRoundP95Kilobytes = [Math]::Round((Get-Percentile -Values $writeTransferPerRound -Percentile 95), 3)
                PerRoundP99Kilobytes = [Math]::Round((Get-Percentile -Values $writeTransferPerRound -Percentile 99), 3)
                PerRoundMaxKilobytes = [Math]::Round(($writeTransferPerRound | Measure-Object -Maximum).Maximum, 3)
            }
            Other = [ordered]@{
                Operations = $otherOperationCount
                TransferBytes = $otherTransferBytes
                TransferMegabytes = [Math]::Round($otherTransferBytes / 1MB, 3)
            }
        }
        ProcessResults = $processSummaries
        Modbus = [ordered]@{ SuccessLogCount = $modbusSuccessCount; FailureLogCount = $modbusFailureCount }
        SaveQueue = if ($queueMatches.Count -gt 0 -or $queueRejected -gt 0 -or $queueEvicted -gt 0) {
            [ordered]@{ PeakObservedCount = $queuePeak; RejectedLogCount = $queueRejected; EvictedLogCount = $queueEvicted }
        }
        else {
            "日志未暴露队列采集能力"
        }
        ResultFiles = [ordered]@{ Json = $jsonPath; Csv = $csvPath; Checkpoint = $checkpointPath }
    }

    $csvRows | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
    [ordered]@{
        VersionLabel = $VersionLabel
        CompletedRounds = $Rounds
        TargetRounds = $Rounds
        Completed = $true
        ResultJson = $jsonPath
        ResultCsv = $csvPath
        UpdatedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $checkpointPath -Encoding UTF8
    Write-Output ("RESULT_JSON={0}" -f $jsonPath)
    Write-Output ("RESULT_CSV={0}" -f $csvPath)
    Write-Output ($result | ConvertTo-Json -Depth 10)
}
finally {
    if (-not $KeepProcess -and $null -ne $mainProcess -and -not $mainProcess.HasExited) {
        [void](Close-TestProcess -MainProcess $mainProcess)
    }
    if (-not [string]::IsNullOrWhiteSpace($resolvedImmutableSchemeSourcePath)) {
        if ($null -eq $mainProcess -or $mainProcess.HasExited) {
            Restore-ImmutableScheme -SourcePath $resolvedImmutableSchemeSourcePath -TargetPath $resolvedSchemePath -ExpectedSha256 $immutableSchemeSha256
        }
        else {
            Write-Warning "视觉程序尚未退出，暂不覆盖其正在使用的固定方案文件。"
        }
    }
    if ($executionStateWasSet) {
        [void][FullSolutionGuiResourceProbe]::SetThreadExecutionState($continuousOnly)
    }
}
