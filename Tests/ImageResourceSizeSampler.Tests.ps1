$ErrorActionPreference = 'Stop'

function Assert-Equal {
    param($Expected, $Actual, [string]$Message)
    if ($Expected -ne $Actual) {
        throw "$Message 期望值：$Expected；实际值：$Actual。"
    }
}

$sourcePath = Join-Path $PSScriptRoot '..\ResourceManagement\ImageResourceSizeSampler.cs'
$source = Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8
$harness = @'
namespace TDJS_Vision.Tests
{
    using System;
    using System.Threading.Tasks;
    using TDJS_Vision.ResourceManagement;

    public static class ImageResourceSizeSamplerStressHarness
    {
        public static void RunConcurrent(IImageResourceSizeSampler sampler, int producerCount, int samplesPerProducer)
        {
            Task[] producers = new Task[producerCount];
            for (int producer = 0; producer < producerCount; producer++)
            {
                producers[producer] = Task.Run(() =>
                {
                    for (int index = 0; index < samplesPerProducer; index++)
                        sampler.Record(1024);
                });
            }

            Task.WaitAll(producers);
        }
    }
}
'@
Add-Type -TypeDefinition ($source + [Environment]::NewLine + $harness) -Language CSharp

$sampler = New-Object TDJS_Vision.ResourceManagement.RollingImageResourceSizeSampler 3
$sampler.Record(10)
$sampler.Record(20)
$sampler.Record(30)
Assert-Equal 3 $sampler.SampleCount '达到容量后样本数量不正确。'
Assert-Equal 20 $sampler.AverageBytes '前三个样本平均值不正确。'
$sampler.Record(100)
Assert-Equal 3 $sampler.SampleCount '滚动后样本数量不得超过容量。'
Assert-Equal 50 $sampler.AverageBytes '滚动窗口必须移除最早样本。'
$sampler.Record(0)
$sampler.Record(-1)
Assert-Equal 50 $sampler.AverageBytes '无效样本不得污染平均值。'

$concurrentSampler = New-Object TDJS_Vision.ResourceManagement.RollingImageResourceSizeSampler 20
[TDJS_Vision.Tests.ImageResourceSizeSamplerStressHarness]::RunConcurrent($concurrentSampler, 4, 25000)
Assert-Equal 20 $concurrentSampler.SampleCount '四线程采样后必须只保留最近20个样本。'
Assert-Equal 1024 $concurrentSampler.AverageBytes '四线程固定样本平均值不正确。'
$concurrentSampler.Reset()
Assert-Equal 0 $concurrentSampler.SampleCount '重置后样本数量必须归零。'
Assert-Equal 0 $concurrentSampler.AverageBytes '重置后平均值必须归零。'

Write-Host '图像资源滚动采样通过：四线程共100000次记录；窗口=20；平均=1024字节；重置归零。'
