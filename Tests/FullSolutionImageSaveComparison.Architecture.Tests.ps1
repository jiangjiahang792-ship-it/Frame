$ErrorActionPreference = 'Stop'

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if (-not $Text.Contains($Pattern)) {
        throw $Message
    }
}

$toolPath = Join-Path $PSScriptRoot 'FullSolutionImageSaveComparison.Tests.ps1'
$tool = Get-Content -LiteralPath $toolPath -Raw -Encoding UTF8

Assert-Contains $tool 'new Random(20260820).NextBytes(pixels)' '真实落盘同比必须使用确定性噪声图，不能使用易压缩纯色图。'
Assert-Contains $tool 'ImageQueueProcessor processor = new ImageQueueProcessor(workerCount, "真实落盘同比")' '旧版必须调用真实节点级保存队列。'
Assert-Contains $tool 'ImageQueueProcessor(' '当前版必须调用真实共享保存工作池。'
Assert-Contains $tool 'RequireAllAccepted' '同工作量同比必须能够强制全部500张被接收。'
Assert-Contains $tool 'Image.FromFile(file.FullName)' '每个输出JPEG必须执行真实解码校验。'
Assert-Contains $tool 'ComputeFileHash(file.FullName)' '每个输出JPEG必须执行内容哈希校验。'
Assert-Contains $tool 'GetFiles("*.saving"' '当前版原子发布必须检查临时文件残留。'
Assert-Contains $tool 'GetProcessIoCounters' '真实落盘必须记录目标进程写操作和传输字节。'
Assert-Contains $tool 'SaveComparisonIoCounters ioSaveEnd = QueryIo(process.Handle);' '保存链路I/O必须在JPEG解码和哈希验证前结束采样。'
Assert-Contains $tool 'FilesCompletedWallMilliseconds = filesCompletedWallMilliseconds' '真实落盘必须把文件完成耗时与工作池停止耗时分开。'
Assert-Contains $tool 'ShutdownWallMilliseconds = shutdownStopwatch.ElapsedMilliseconds' '真实落盘必须单独记录工作池停止等待。'
Assert-Contains $tool 'AverageCpuPercent = sampler.AverageCpuPercent' '真实落盘必须记录按逻辑线程归一化的平均CPU占用。'
Assert-Contains $tool 'PeakCpuPercent = sampler.PeakCpuPercent' '真实落盘必须记录按逻辑线程归一化的CPU峰值。'
Assert-Contains $tool 'CpuLogicalProcessorCount = Environment.ProcessorCount' 'CPU占用必须记录归一化使用的逻辑线程数。'
Assert-Contains $tool 'PeakQueueCount -le $QueueCapacity' '新版生产边界必须校验任务容量。'
Assert-Contains $tool 'LeaseDisposeCount -eq $ImageCount' '新版全部尝试任务必须核对租约释放。'
Assert-Contains $tool 'MeasurementBoundary' '结果必须说明系统缓存写入与物理介质刷盘的边界。'

Write-Host '真实图片落盘同比架构检查通过：确定性噪声图、生产保存工作池、完整文件校验、I/O和资源边界均已接通。'
