$ErrorActionPreference = 'Stop'

$sourcePath = Join-Path $PSScriptRoot '..\Diagnostics\CameraFrameTraceRegistry.cs'
if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw 'CameraFrameTraceRegistry.cs does not exist.'
}

$registrySource = Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8
$testSource = @'
namespace OpenCvSharp
{
    /// <summary>Minimal Mat wrapper used to exercise registry ownership behavior.</summary>
    public sealed class Mat
    {
    }
}

namespace TDJS_Vision.Diagnostics
{
    /// <summary>Runs deterministic behavior and pressure checks against the production registry source.</summary>
    public static class CameraFrameTraceRegistryStressHarness
    {
        /// <summary>Creates one weakly referenced Mat and attaches trace metadata.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static System.WeakReference CreateWeakFrameReference()
        {
            OpenCvSharp.Mat image = new OpenCvSharp.Mat();
            CameraFrameTraceRegistry.Attach(
                image,
                new CameraFrameTraceInfo("Camera-A", 1, 1, 2, 1280, 1024, "Mono8"));
            return new System.WeakReference(image);
        }

        /// <summary>Runs repeated attach/read operations and verifies weak key collection.</summary>
        public static void Run(int iterations)
        {
            for (int i = 1; i <= iterations; i++)
            {
                OpenCvSharp.Mat image = new OpenCvSharp.Mat();
                CameraFrameTraceInfo expected = new CameraFrameTraceInfo(
                    "Camera-A",
                    i,
                    100,
                    200,
                    1280,
                    1024,
                    "Mono8");
                CameraFrameTraceRegistry.Attach(image, expected);

                CameraFrameTraceInfo actual;
                if (!CameraFrameTraceRegistry.TryGet(image, out actual) ||
                    !object.ReferenceEquals(expected, actual) ||
                    actual.FrameId != i)
                {
                    throw new System.InvalidOperationException("Frame metadata was not returned intact.");
                }
            }

            System.WeakReference weakFrame = CreateWeakFrameReference();
            for (int i = 0; i < 3 && weakFrame.IsAlive; i++)
            {
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
                System.GC.Collect();
            }

            if (weakFrame.IsAlive)
                throw new System.InvalidOperationException("The weak registry retained a Mat key.");
        }
    }
}
'@

Add-Type -TypeDefinition ($registrySource + [Environment]::NewLine + $testSource) -Language CSharp

$iterations = 50000
$watch = [System.Diagnostics.Stopwatch]::StartNew()
[TDJS_Vision.Diagnostics.CameraFrameTraceRegistryStressHarness]::Run($iterations)
$watch.Stop()

Write-Host "Camera frame trace registry stress checks passed: $iterations iterations in $($watch.ElapsedMilliseconds) ms."
