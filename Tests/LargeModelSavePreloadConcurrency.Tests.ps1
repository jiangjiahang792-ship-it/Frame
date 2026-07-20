$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$nodePath = Join-Path $root "Node\3-Detection\LargeModel\NodeLargeModelDetection.cs"
$nodeSource = Get-Content -LiteralPath $nodePath -Encoding UTF8 -Raw
$marker = "internal sealed class LargeModelRuntimeConfigurationGate"
$classStart = $nodeSource.IndexOf($marker, [System.StringComparison]::Ordinal)
if ($classStart -lt 0) {
    throw "未找到大模型运行配置门生产实现。"
}

$braceStart = $nodeSource.IndexOf("{", $classStart)
if ($braceStart -lt 0) {
    throw "大模型运行配置门缺少类体。"
}

$depth = 0
$classEnd = -1
for ($index = $braceStart; $index -lt $nodeSource.Length; $index++) {
    $character = $nodeSource[$index]
    if ($character -eq "{") {
        $depth++
    }
    elseif ($character -eq "}") {
        $depth--
        if ($depth -eq 0) {
            $classEnd = $index
            break
        }
    }
}

if ($classEnd -lt 0) {
    throw "无法定位大模型运行配置门完整类体。"
}

$gateSource = $nodeSource.Substring($classStart, $classEnd - $classStart + 1)
# Windows PowerShell 5.1 的 Add-Type 使用旧编译器，测试编译时仅把 nameof 机械展开为同名字符串。
$gateSource = $gateSource.Replace('nameof(action)', '"action"')
$gateSource = $gateSource.Replace('nameof(preloadAction)', '"preloadAction"')
$gateSource = $gateSource.Replace('nameof(commitAction)', '"commitAction"')
$behaviorSource = @"
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TDJS_Vision.Node._3_Detection.LargeModel
{
$gateSource

    public static class LargeModelRuntimeConfigurationGateBehaviorHarness
    {
        public static void Run()
        {
            AssertSuccessfulPreloadAndConcurrentRunAreAtomic();
            AssertFailedPreloadDoesNotCommitParameters();
        }

        private static void AssertSuccessfulPreloadAndConcurrentRunAreAtomic()
        {
            var gate = new LargeModelRuntimeConfigurationGate();
            var loadStarted = new ManualResetEventSlim(false);
            var allowLoadToFinish = new ManualResetEventSlim(false);
            var runAttemptingToEnterGate = new ManualResetEventSlim(false);
            string currentParameter = "旧参数";
            string loadedModel = "旧模型";

            Task preloadTask = Task.Run(() => gate.PreloadAndCommit(
                () =>
                {
                    loadStarted.Set();
                    if (!allowLoadToFinish.Wait(5000))
                        throw new TimeoutException("等待候选模型加载放行超时。");
                    loadedModel = "新模型";
                },
                () => currentParameter = "新参数"));

            if (!loadStarted.Wait(5000))
                throw new TimeoutException("候选模型加载没有按预期开始。");

            Task<string> runTask = Task.Run(() =>
            {
                runAttemptingToEnterGate.Set();
                return gate.Execute(() => currentParameter + "|" + loadedModel);
            });
            if (!runAttemptingToEnterGate.Wait(5000))
                throw new TimeoutException("并发运行没有开始尝试进入配置门。");
            Thread.Sleep(100);
            if (runTask.IsCompleted)
                throw new InvalidOperationException("并发运行越过了尚未提交完成的预加载配置门。");

            allowLoadToFinish.Set();
            Task.WaitAll(preloadTask, runTask);
            if (!string.Equals(runTask.Result, "新参数|新模型", StringComparison.Ordinal))
                throw new InvalidOperationException("并发运行读取到了跨版本的参数和模型组合：" + runTask.Result);
        }

        private static void AssertFailedPreloadDoesNotCommitParameters()
        {
            var gate = new LargeModelRuntimeConfigurationGate();
            string currentParameter = "旧参数";
            bool failedAsExpected = false;
            try
            {
                gate.PreloadAndCommit(
                    () => { throw new InvalidOperationException("模拟预加载失败"); },
                    () => currentParameter = "新参数");
            }
            catch (InvalidOperationException)
            {
                failedAsExpected = true;
            }

            if (!failedAsExpected)
                throw new InvalidOperationException("预加载失败没有向调用方报告异常。");
            if (!string.Equals(currentParameter, "旧参数", StringComparison.Ordinal))
                throw new InvalidOperationException("预加载失败后仍然提交了新参数。");
        }
    }
}
"@

$compiledTypes = Add-Type -TypeDefinition $behaviorSource -Language CSharp -PassThru
$harnessType = $compiledTypes | Where-Object {
    $_.FullName -eq "TDJS_Vision.Node._3_Detection.LargeModel.LargeModelRuntimeConfigurationGateBehaviorHarness"
} | Select-Object -First 1
if ($null -eq $harnessType) {
    throw "未找到已编译的大模型配置门行为测试入口。"
}
$harnessType.GetMethod("Run").Invoke($null, @())

Write-Host "大模型保存预加载并发行为检查通过。"
