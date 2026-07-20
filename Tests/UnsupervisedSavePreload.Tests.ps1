$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$formPath = Join-Path $root "Node\3-Detection\Unsupervised\ParamFormUnsupervisedDetection.cs"
$nodePath = Join-Path $root "Node\3-Detection\Unsupervised\NodeUnsupervisedDetection.cs"
$runtimePath = Join-Path $root "Node\3-Detection\Unsupervised\UnsupervisedDetectionRuntime.cs"
$packagePath = Join-Path $root "Forms\AiTrainForm\UnsupervisedTemplatePackage.cs"

function Assert-Contains {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if ($Text.IndexOf($Pattern, [System.StringComparison]::Ordinal) -lt 0) {
        throw $Message
    }
}

function Assert-NotContains {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if ($Text.IndexOf($Pattern, [System.StringComparison]::Ordinal) -ge 0) {
        throw $Message
    }
}

$form = Get-Content -LiteralPath $formPath -Encoding UTF8 -Raw
$node = Get-Content -LiteralPath $nodePath -Encoding UTF8 -Raw
$runtime = Get-Content -LiteralPath $runtimePath -Encoding UTF8 -Raw
$package = Get-Content -LiteralPath $packagePath -Encoding UTF8 -Raw

Assert-Contains $package "public static UnsupervisedTemplateManifest ReadManifest(string templatePath)" "Template package must expose a manifest-only reader."
Assert-Contains $form "private async void buttonSave_Click" "Save must await model loading asynchronously."
Assert-Contains $form "await unsupervisedNode.PreloadRuntimeAsync(param);" "Save must await node preload."
Assert-Contains $form "buttonSave.Text = isLoading" "Save must show a loading state."
Assert-Contains $form "UnsupervisedTemplatePackage.ReadManifest(templatePath)" "Template preview must read only the manifest."
Assert-NotContains $form "_UnsupervisedPreview" "Template preview must not extract the complete model."

Assert-Contains $node "private readonly UnsupervisedRuntimeConfigurationGate _configurationGate" "Node must coordinate model and parameter commits."
Assert-Contains $node "public Task PreloadRuntimeAsync(NodeParamUnsupervisedDetection param)" "Node must expose background preload."
Assert-Contains $node "_configurationGate.PreloadAndCommit" "Preload and parameter commit must be atomic."
Assert-Contains $node "_configurationGate.Execute" "Inference must use the same configuration gate."

Assert-Contains $runtime "public void Preload(NodeParamUnsupervisedDetection param)" "Runtime must support save-time preload."
Assert-Contains $runtime "EnsureModelLoaded(param, true);" "Preload must perform first-inference warmup."
Assert-Contains $runtime "WarmupDetector" "Runtime must provide blank-image warmup."
Assert-Contains $runtime "SwapLoadedDetector" "Candidate model must be published after warmup."
Assert-Contains $runtime "if (!published" "Failed candidate publication must dispose the candidate."

$marker = "internal sealed class UnsupervisedRuntimeConfigurationGate"
$classStart = $node.IndexOf($marker, [System.StringComparison]::Ordinal)
if ($classStart -lt 0) {
    throw "Missing production configuration gate."
}

$braceStart = $node.IndexOf("{", $classStart)
$depth = 0
$classEnd = -1
for ($index = $braceStart; $index -lt $node.Length; $index++) {
    $character = $node[$index]
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
    throw "Could not locate the complete configuration gate class."
}

$gateSource = $node.Substring($classStart, $classEnd - $classStart + 1)
$gateSource = $gateSource.Replace('nameof(action)', '"action"')
$gateSource = $gateSource.Replace('nameof(preloadAction)', '"preloadAction"')
$gateSource = $gateSource.Replace('nameof(commitAction)', '"commitAction"')
$behaviorSource = @"
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TDJS_Vision.Node._3_Detection.Unsupervised
{
$gateSource

    public static class UnsupervisedRuntimeConfigurationGateBehaviorHarness
    {
        public static void Run()
        {
            AssertSuccessfulPreloadAndConcurrentRunAreAtomic();
            AssertFailedPreloadDoesNotCommitParameters();
        }

        private static void AssertSuccessfulPreloadAndConcurrentRunAreAtomic()
        {
            var gate = new UnsupervisedRuntimeConfigurationGate();
            var loadStarted = new ManualResetEventSlim(false);
            var allowLoadToFinish = new ManualResetEventSlim(false);
            string currentParameter = "old-parameter";
            string loadedModel = "old-model";

            Task preloadTask = Task.Run(() => gate.PreloadAndCommit(
                () =>
                {
                    loadStarted.Set();
                    if (!allowLoadToFinish.Wait(5000))
                        throw new TimeoutException("Timed out waiting for preload release.");
                    loadedModel = "new-model";
                },
                () => currentParameter = "new-parameter"));

            if (!loadStarted.Wait(5000))
                throw new TimeoutException("Candidate preload did not start.");

            Task<string> runTask = Task.Run(() => gate.Execute(() => currentParameter + "|" + loadedModel));
            Thread.Sleep(100);
            if (runTask.IsCompleted)
                throw new InvalidOperationException("Concurrent inference crossed an incomplete preload commit.");

            allowLoadToFinish.Set();
            Task.WaitAll(preloadTask, runTask);
            if (!string.Equals(runTask.Result, "new-parameter|new-model", StringComparison.Ordinal))
                throw new InvalidOperationException("Concurrent inference observed a mixed configuration: " + runTask.Result);
        }

        private static void AssertFailedPreloadDoesNotCommitParameters()
        {
            var gate = new UnsupervisedRuntimeConfigurationGate();
            string currentParameter = "old-parameter";
            try
            {
                gate.PreloadAndCommit(
                    () => { throw new InvalidOperationException("simulated preload failure"); },
                    () => currentParameter = "new-parameter");
            }
            catch (InvalidOperationException)
            {
            }

            if (!string.Equals(currentParameter, "old-parameter", StringComparison.Ordinal))
                throw new InvalidOperationException("Failed preload still committed new parameters.");
        }
    }
}
"@

$compiledTypes = Add-Type -TypeDefinition $behaviorSource -Language CSharp -PassThru
$harnessType = $compiledTypes | Where-Object {
    $_.FullName -eq "TDJS_Vision.Node._3_Detection.Unsupervised.UnsupervisedRuntimeConfigurationGateBehaviorHarness"
} | Select-Object -First 1
if ($null -eq $harnessType) {
    throw "Missing configuration gate behavior harness."
}
$harnessType.GetMethod("Run").Invoke($null, @())

Write-Host "Unsupervised save preload checks passed."
