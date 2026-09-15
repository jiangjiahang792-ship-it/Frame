$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSEdition -eq 'Core') {
    $windowsPowerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $env:TDJS_STORAGE_GUARD_TEST_PATH = $PSCommandPath
    & $windowsPowerShell -NoProfile -ExecutionPolicy Bypass -Command '$content = Get-Content -LiteralPath $env:TDJS_STORAGE_GUARD_TEST_PATH -Raw -Encoding UTF8; & ([ScriptBlock]::Create($content))'
    exit $LASTEXITCODE
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

$scriptRoot = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    Split-Path -Parent $env:TDJS_STORAGE_GUARD_TEST_PATH
}
else {
    $PSScriptRoot
}
$projectRoot = Split-Path -Parent $scriptRoot
$debugDirectory = Join-Path $projectRoot 'bin\x64\Debug'
$application = Get-Item -LiteralPath (Join-Path $debugDirectory '机器视觉AI检测系统V1.0.exe') -ErrorAction SilentlyContinue
Assert-True ($null -ne $application) '磁盘保护测试需要最新Debug程序。'
$source = Get-Item -LiteralPath (Join-Path $projectRoot 'ResourceManagement\ImageSaveStorageGuard.cs')
Assert-True ($application.LastWriteTimeUtc -ge $source.LastWriteTimeUtc) 'Debug程序早于磁盘保护源码，请先重新编译。'

[Environment]::CurrentDirectory = $debugDirectory
$assembly = [Reflection.Assembly]::LoadFrom($application.FullName)
$referencePath = Join-Path $debugDirectory 'TDJSVision.StorageGuard.TestHost.dll'
Copy-Item -LiteralPath $application.FullName -Destination $referencePath -Force
$helperSource = @'
using System;
using TDJS_Vision.ResourceManagement;

public sealed class DeterministicDiskSpaceProbe : IImageSaveDiskSpaceProbe
{
    public long AvailableSpaceMb { get; set; }
    public bool ShouldFail { get; set; }
    public int CallCount { get; set; }

    public bool TryGetAvailableSpaceMb(string storageRoot, out long availableSpaceMb, out string errorMessage)
    {
        CallCount++;
        availableSpaceMb = AvailableSpaceMb;
        errorMessage = ShouldFail ? "模拟磁盘探测失败" : string.Empty;
        return !ShouldFail;
    }
}
'@

try {
    Add-Type -TypeDefinition $helperSource -Language CSharp -ReferencedAssemblies @(
        'mscorlib',
        'System',
        'System.Core',
        $referencePath)
}
finally {
    Remove-Item -LiteralPath $referencePath -Force -ErrorAction SilentlyContinue
}

$targetDirectories = New-Object 'System.Collections.Generic.List[string]'
$targetDirectories.Add((Join-Path $projectRoot 'Tests\_storage_guard_probe'))

$normalProbe = [DeterministicDiskSpaceProbe]::new()
$normalProbe.AvailableSpaceMb = 4096
$normalGuard = [TDJS_Vision.ResourceManagement.CachedImageSaveStorageGuard]::new($normalProbe, 2048, 512, 500, 10000)
$firstNormal = $normalGuard.Evaluate($targetDirectories, $false)
$secondNormal = $normalGuard.Evaluate($targetDirectories, $false)
Assert-True ($firstNormal.Status -eq [TDJS_Vision.ResourceManagement.ImageSaveStorageStatus]::Allowed) '空间充足时必须允许普通图片保存。'
Assert-True ($secondNormal.Status -eq [TDJS_Vision.ResourceManagement.ImageSaveStorageStatus]::Allowed) '有效缓存期间重复准入结果必须保持正常。'
Assert-True ($normalProbe.CallCount -eq 1) '同一磁盘在缓存周期内只能执行一次真实空间探测。'

$lowProbe = [DeterministicDiskSpaceProbe]::new()
$lowProbe.AvailableSpaceMb = 1024
$lowGuard = [TDJS_Vision.ResourceManagement.CachedImageSaveStorageGuard]::new($lowProbe, 2048, 512, 500, 1000)
$lowNormal = $lowGuard.Evaluate($targetDirectories, $false)
$lowNg = $lowGuard.Evaluate($targetDirectories, $true)
Assert-True ($lowNormal.Status -eq [TDJS_Vision.ResourceManagement.ImageSaveStorageStatus]::SkippedLowSpace -and $lowNormal.ShouldSkip) '低空间普通图片必须停止保存。'
Assert-True ($lowNg.Status -eq [TDJS_Vision.ResourceManagement.ImageSaveStorageStatus]::AllowedHighPriorityUnderLowSpace -and -not $lowNg.ShouldSkip) '低空间NG图片必须继续保存。'

$criticalProbe = [DeterministicDiskSpaceProbe]::new()
$criticalProbe.AvailableSpaceMb = 128
$criticalGuard = [TDJS_Vision.ResourceManagement.CachedImageSaveStorageGuard]::new($criticalProbe, 2048, 512, 500, 1000)
$criticalNg = $criticalGuard.Evaluate($targetDirectories, $true)
Assert-True ($criticalNg.Status -eq [TDJS_Vision.ResourceManagement.ImageSaveStorageStatus]::SkippedCriticalSpace -and $criticalNg.ShouldSkip) '严重低空间必须停止NG图片保存。'

$failureProbe = [DeterministicDiskSpaceProbe]::new()
$failureProbe.ShouldFail = $true
$failureGuard = [TDJS_Vision.ResourceManagement.CachedImageSaveStorageGuard]::new($failureProbe, 2048, 512, 500, 1000)
$failureDecision = $failureGuard.Evaluate($targetDirectories, $false)
Assert-True ($failureDecision.Status -eq [TDJS_Vision.ResourceManagement.ImageSaveStorageStatus]::ProbeFailedAllowed -and -not $failureDecision.ShouldSkip) '探测故障必须告警放行，不能把监控故障变成停产。'

Assert-True (-not $normalGuard.IsSlowWrite(499)) '低于慢写阈值的写入不能误报警。'
Assert-True ($normalGuard.IsSlowWrite(500)) '达到慢写阈值的写入必须报警。'
$normalizedGuard = [TDJS_Vision.ResourceManagement.CachedImageSaveStorageGuard]::new($normalProbe, 1, 99, 0, -1)
Assert-True ($normalizedGuard.LowSpaceThresholdMb -eq 2 -and $normalizedGuard.CriticalSpaceThresholdMb -eq 1) '磁盘水位必须规范为严重水位低于普通低水位。'
Assert-True ($normalizedGuard.SlowWriteThresholdMs -eq 1 -and $normalizedGuard.ProbeIntervalMs -eq 0) '慢写和采样参数必须限制为合法值。'

Write-Host '保存磁盘保护检查通过：正常放行、缓存、低空间普通图降级、NG优先、严重水位、探测失败放行和慢写阈值均符合预期。'
