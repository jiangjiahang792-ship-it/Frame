param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath,

    [int]$Cycles = 1000
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
    throw "Release executable does not exist: $ExecutablePath"
}
if ($Cycles -le 0) {
    throw "Cycles must be greater than zero."
}

$releaseRoot = Split-Path -Parent $ExecutablePath
$openCvPath = Join-Path $releaseRoot "OpenCvSharp.dll"
if (-not (Test-Path -LiteralPath $openCvPath -PathType Leaf)) {
    throw "OpenCvSharp assembly does not exist: $openCvPath"
}

$source = @'
using OpenCvSharp;
using System;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

public static class YoloSharedMemoryTransportProbe
{
    public static double Run(string executablePath, int cycles)
    {
        Assembly assembly = Assembly.LoadFrom(executablePath);
        Type clientType = assembly.GetType(
            "TDJS_Vision.Node._3_Detection.TDAI.Yolo8.YoloIsolatedWorkerClient",
            true);
        object client = Activator.CreateInstance(clientType, true);
        object secondClient = Activator.CreateInstance(clientType, true);
        MethodInfo ensureCapacity = clientType.GetMethod(
            "EnsureSharedImageCapacity",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo copyImage = clientType.GetMethod(
            "CopyImageToSharedMemory",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo releaseMap = clientType.GetMethod(
            "ReleaseSharedImageMap",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo viewField = clientType.GetField(
            "sharedImageView",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo mapNameField = clientType.GetField(
            "sharedImageMapName",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo mapCapacityField = clientType.GetField(
            "sharedImageMapCapacity",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo syncRootField = clientType.GetField(
            "syncRoot",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Type transportType = assembly.GetType(
            "TDJS_Vision.Node._3_Detection.TDAI.Yolo8.YoloSharedImageTransport",
            true);
        long growthBytes = (long)transportType.GetField("CapacityGrowthBytes").GetRawConstantValue();
        long maximumBytes = (long)transportType.GetField("MaximumImageBytes").GetRawConstantValue();

        byte[] sourceBytes = new byte[4 * 6 * 3];
        for (int index = 0; index < sourceBytes.Length; index++)
            sourceBytes[index] = checked((byte)(index + 1));

        byte[] expected = new byte[2 * 2 * 3];
        for (int row = 0; row < 2; row++)
            Buffer.BlockCopy(sourceBytes, ((row + 1) * 18) + 6, expected, row * 6, 6);

        try
        {
            ensureCapacity.Invoke(client, new object[] { 12L });
            string initialMapName = (string)mapNameField.GetValue(client);
            if ((long)mapCapacityField.GetValue(client) != growthBytes)
                throw new InvalidOperationException("Initial shared map capacity was not rounded to one growth block.");

            ensureCapacity.Invoke(client, new object[] { growthBytes + 1L });
            string expandedMapName = (string)mapNameField.GetValue(client);
            if (string.Equals(initialMapName, expandedMapName, StringComparison.Ordinal) ||
                (long)mapCapacityField.GetValue(client) != growthBytes * 2L)
                throw new InvalidOperationException("Shared map did not expand to the next growth block.");

            ensureCapacity.Invoke(client, new object[] { growthBytes + (growthBytes / 2L) });
            if (!string.Equals(expandedMapName, (string)mapNameField.GetValue(client), StringComparison.Ordinal))
                throw new InvalidOperationException("Shared map was recreated even though existing capacity was sufficient.");

            ensureCapacity.Invoke(secondClient, new object[] { 12L });
            if (string.Equals(
                (string)mapNameField.GetValue(client),
                (string)mapNameField.GetValue(secondClient),
                StringComparison.Ordinal))
                throw new InvalidOperationException("Separate YOLO clients must not share the same map name.");

            bool maximumRejected = false;
            try
            {
                ensureCapacity.Invoke(client, new object[] { maximumBytes + 1L });
            }
            catch (TargetInvocationException exception)
            {
                maximumRejected = exception.InnerException is InvalidOperationException;
            }
            if (!maximumRejected)
                throw new InvalidOperationException("Shared map maximum capacity was not enforced.");

            MemoryMappedViewAccessor view = (MemoryMappedViewAccessor)viewField.GetValue(client);
            double elapsedMilliseconds;
            using (Mat parent = Mat.FromPixelData(4, 6, MatType.CV_8UC3, sourceBytes))
            using (Mat roi = new Mat(parent, new Rect(2, 1, 2, 2)))
            {
                if (roi.IsContinuous())
                    throw new InvalidOperationException("Test ROI must be non-contiguous.");

                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int cycle = 0; cycle < cycles; cycle++)
                {
                    copyImage.Invoke(client, new object[] { roi, 6L, 12L });
                    byte[] actual = new byte[12];
                    view.ReadArray(0L, actual, 0, actual.Length);
                    for (int index = 0; index < actual.Length; index++)
                    {
                        if (actual[index] != expected[index])
                        {
                            throw new InvalidOperationException(
                                "Non-contiguous ROI copy mismatch at cycle=" + cycle +
                                ", index=" + index +
                                ", expected=" + expected[index] +
                                ", actual=" + actual[index] + ".");
                        }
                    }
                }

                watch.Stop();
                elapsedMilliseconds = watch.Elapsed.TotalMilliseconds;
            }

            object syncRoot = syncRootField.GetValue(client);
            var lockEntered = new ManualResetEventSlim(false);
            var releaseLock = new ManualResetEventSlim(false);
            bool disposeCompleted = false;
            Task lockHolder = Task.Run(() =>
            {
                lock (syncRoot)
                {
                    lockEntered.Set();
                    releaseLock.Wait();
                }
            });
            lockEntered.Wait();
            Task disposer = Task.Run(() =>
            {
                ((IDisposable)client).Dispose();
                Volatile.Write(ref disposeCompleted, true);
            });
            Thread.Sleep(50);
            if (Volatile.Read(ref disposeCompleted))
                throw new InvalidOperationException("Dispose did not wait for the active request lock.");
            releaseLock.Set();
            Task.WaitAll(lockHolder, disposer);
            lockEntered.Dispose();
            releaseLock.Dispose();
            return elapsedMilliseconds;
        }
        finally
        {
            releaseMap.Invoke(client, null);
            releaseMap.Invoke(secondClient, null);
        }
    }
}
'@

$previousCurrentDirectory = [Environment]::CurrentDirectory
try {
    [Environment]::CurrentDirectory = $releaseRoot
    [void][Reflection.Assembly]::LoadFrom($openCvPath)
    Add-Type -TypeDefinition $source -Language CSharp -ReferencedAssemblies @(
        $openCvPath,
        "System.Core.dll"
    )
    $elapsedMilliseconds = [YoloSharedMemoryTransportProbe]::Run($ExecutablePath, $Cycles)
}
finally {
    [Environment]::CurrentDirectory = $previousCurrentDirectory
}

$averageMicroseconds = ($elapsedMilliseconds * 1000.0) / $Cycles
Write-Host ("YOLO shared-memory non-contiguous ROI checks passed. cycles={0}, total={1:F3}ms, average={2:F3}us" -f `
    $Cycles, $elapsedMilliseconds, $averageMicroseconds)
