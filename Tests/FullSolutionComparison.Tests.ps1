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

    [string]$OutputDirectory = (Join-Path $PSScriptRoot "Results\FullSolutionComparison"),

    [string]$ImmutableSchemeSourcePath = "",

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
using System.Runtime.InteropServices;

public static class FullSolutionGuiResourceProbe
{
    [DllImport("user32.dll")]
    public static extern int GetGuiResources(IntPtr processHandle, int resourceType);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr windowHandle, uint message, IntPtr wordParameter, IntPtr longParameter);

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
        if ($MainProcess.MainWindowHandle -ne [IntPtr]::Zero) {
            try {
                $root = [Windows.Automation.AutomationElement]::FromHandle($MainProcess.MainWindowHandle)
                $condition = New-Object Windows.Automation.PropertyCondition(
                    [Windows.Automation.AutomationElement]::NameProperty,
                    "确定")
                $confirmButton = $root.FindFirst([Windows.Automation.TreeScope]::Descendants, $condition)
                if ($null -ne $confirmButton -and $confirmButton.Current.NativeWindowHandle -ne 0) {
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
    $mainProcess = Start-Process @startProcessArguments
    $startedByScript = $true
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

    $logOffsets = Get-LogOffsets -LogDirectory $logDirectory
    $mainProcess.Refresh()
    $mainStartTime = $mainProcess.StartTime
    $logicalProcessorCount = [Math]::Max(1, [Environment]::ProcessorCount)
    $initial = Get-ResourceSnapshot -ResolvedExecutablePath $resolvedExecutablePath -MainProcessStartTime $mainStartTime -MainProcessId $mainProcess.Id -IncludeManagedHeap $true
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
        [Math]::Max(1.0, $formalStopwatch.Elapsed.TotalMilliseconds)) * (100.0 / $logicalProcessorCount)
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
            HarnessWallMilliseconds = [Math]::Round($formalStopwatch.Elapsed.TotalMilliseconds, 3)
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
