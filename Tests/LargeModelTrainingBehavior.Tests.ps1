param(
    [ValidateSet("All", "Ordering", "Publishing", "Cancellation", "Validity", "Concurrency", "Cleanup", "ProcessLog")]
    [string]$Case = "All"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$pipelinePath = Join-Path $root "Forms\AiTrainForm\LargeModelTrainingPipeline.cs"

if (-not (Test-Path -LiteralPath $pipelinePath)) {
    throw "Missing executable large-model training pipeline coordinator."
}

$pipelineSource = Get-Content -LiteralPath $pipelinePath -Encoding UTF8 -Raw
$testHarness = @'
namespace LargeModelTrainingBehaviorTests
{
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Forms.AiTrainForm;

public static class LargeModelTrainingBehaviorHarness
{
    public static void RunOrdering()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            LargeModelTrainingCoordinator coordinator = new LargeModelTrainingCoordinator();
            RecordingTrainingStages cpuStages = new RecordingTrainingStages(root);

            string firstCpuModel = coordinator.Execute(root, false, cpuStages, null, CancellationToken.None);
            string secondCpuModel = coordinator.Execute(root, false, cpuStages, null, CancellationToken.None);

            AssertEqual(2, cpuStages.OnnxCalls, "Two consecutive CPU runs must export ONNX twice.");
            AssertEqual(0, cpuStages.EngineCalls, "CPU runs must not build TensorRT engines.");
            AssertEqual(2, cpuStages.BankCalls, "Two consecutive CPU runs must train two banks.");
            AssertEqual(2, cpuStages.PackageCalls, "Two consecutive CPU runs must package twice.");
            AssertSequence(
                cpuStages.Events,
                new[] { "dataset", "onnx", "bank", "package", "dataset", "onnx", "bank", "package" },
                "CPU stages must execute in the required order on every run.");
            AssertTrue(firstCpuModel.EndsWith("run1.onnx", StringComparison.Ordinal), "The first CPU run must consume its newly exported ONNX.");
            AssertTrue(secondCpuModel.EndsWith("run2.onnx", StringComparison.Ordinal), "The second CPU run must consume its newly exported ONNX.");
            AssertEqual(firstCpuModel, cpuStages.BankInputPaths[0], "The first CPU Bank must receive the first run's ONNX.");
            AssertEqual(secondCpuModel, cpuStages.BankInputPaths[1], "The second CPU Bank must receive the second run's ONNX.");
            AssertEqual(firstCpuModel, cpuStages.PackageInputPaths[0], "The first CPU package must receive the first run's ONNX.");
            AssertEqual(secondCpuModel, cpuStages.PackageInputPaths[1], "The second CPU package must receive the second run's ONNX.");

            RecordingTrainingStages gpuStages = new RecordingTrainingStages(root);
            string gpuModel = coordinator.Execute(root, true, gpuStages, null, CancellationToken.None);

            AssertSequence(
                gpuStages.Events,
                new[] { "dataset", "onnx", "engine", "bank", "package" },
                "GPU stages must execute ONNX, Engine, Bank and package in order.");
            AssertEqual(1, gpuStages.OnnxCalls, "A GPU run must export ONNX.");
            AssertEqual(1, gpuStages.EngineCalls, "A GPU run must build a TensorRT engine.");
            AssertEqual(1, gpuStages.BankCalls, "A GPU run must train a bank.");
            AssertTrue(gpuModel.EndsWith("run1.engine", StringComparison.Ordinal), "The GPU run must consume its newly built Engine.");
            AssertTrue(gpuStages.EngineInputPaths[0].EndsWith("run1.onnx", StringComparison.Ordinal), "GPU Engine must receive this run's ONNX.");
            AssertEqual(gpuModel, gpuStages.BankInputPaths[0], "GPU Bank must receive this run's Engine.");
            AssertEqual(gpuModel, gpuStages.PackageInputPaths[0], "GPU package must receive this run's Engine.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    public static void RunPublishing()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string targetPath = Path.Combine(root, "dinov2_448x224.onnx");
            byte[] oldBytes = CreateBytes(80, 11);
            byte[] newBytes = CreateBytes(128, 23);
            File.WriteAllBytes(targetPath, oldBytes);

            LargeModelArtifactPublisher publisher = new LargeModelArtifactPublisher();
            RecordingArtifactValidator validator = new RecordingArtifactValidator();
            string publishedPath = publisher.Publish(
                targetPath,
                "DINOv2 ONNX",
                64,
                new WritingArtifactGenerator(newBytes),
                validator,
                null,
                CancellationToken.None);

            AssertTrue(string.Equals(targetPath, publishedPath, StringComparison.OrdinalIgnoreCase), "Publishing must return the formal model path.");
            AssertBytes(newBytes, File.ReadAllBytes(targetPath), "A valid artifact must replace the pre-existing formal model.");
            AssertEqual(1, validator.Calls, "A generated artifact must be validated before publication.");
            AssertTrue(validator.ValidatedPath.IndexOf(".building.onnx", StringComparison.OrdinalIgnoreCase) >= 0, "Validation must target the temporary ONNX.");

            File.WriteAllBytes(targetPath, oldBytes);
            bool generationFailed = false;
            try
            {
                publisher.Publish(
                    targetPath,
                    "DINOv2 ONNX",
                    64,
                    new ThrowingArtifactGenerator(),
                    validator,
                    null,
                    CancellationToken.None);
            }
            catch (InvalidOperationException ex)
            {
                generationFailed = string.Equals(ex.Message, "generation failed", StringComparison.Ordinal);
            }

            AssertTrue(generationFailed, "The original generation exception must escape publication.");
            AssertBytes(oldBytes, File.ReadAllBytes(targetPath), "Generation failure must preserve the old formal model bytes.");

            File.WriteAllBytes(targetPath, oldBytes);
            FailingArtifactTrainingStages failingStages = new FailingArtifactTrainingStages(publisher, targetPath);
            bool coordinatedGenerationFailed = false;
            try
            {
                new LargeModelTrainingCoordinator().Execute(root, false, failingStages, null, CancellationToken.None);
            }
            catch (InvalidOperationException ex)
            {
                coordinatedGenerationFailed = string.Equals(ex.Message, "generation failed", StringComparison.Ordinal);
            }

            AssertTrue(coordinatedGenerationFailed, "A coordinated run must stop on exporter failure.");
            AssertEqual(0, failingStages.BankCalls, "Bank training must not start after exporter failure.");
            AssertBytes(oldBytes, File.ReadAllBytes(targetPath), "Coordinated exporter failure must preserve the old formal model.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    public static void RunCancellation()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string targetPath = Path.Combine(root, "dinov2_448x224.onnx");
            byte[] oldBytes = CreateBytes(80, 31);
            byte[] newBytes = CreateBytes(128, 47);
            LargeModelArtifactPublisher publisher = new LargeModelArtifactPublisher();
            RecordingArtifactValidator validator = new RecordingArtifactValidator();

            File.WriteAllBytes(targetPath, oldBytes);
            CancellationTokenSource beforeStartCancellation = new CancellationTokenSource();
            beforeStartCancellation.Cancel();
            CountingArtifactGenerator notStartedGenerator = new CountingArtifactGenerator(newBytes);
            AssertCancellation(
                delegate
                {
                    publisher.Publish(
                        targetPath,
                        "DINOv2 ONNX",
                        64,
                        notStartedGenerator,
                        validator,
                        null,
                        beforeStartCancellation.Token);
                },
                "Cancellation before generation must stop publication.");
            AssertEqual(0, notStartedGenerator.Calls, "A cancelled request must not start the exporter.");
            AssertBytes(oldBytes, File.ReadAllBytes(targetPath), "Cancellation before generation must preserve the old formal model.");

            File.WriteAllBytes(targetPath, oldBytes);
            CancellationTokenSource generationBoundaryCancellation = new CancellationTokenSource();
            AssertCancellation(
                delegate
                {
                    publisher.Publish(
                        targetPath,
                        "DINOv2 ONNX",
                        64,
                        new CancelingArtifactGenerator(newBytes, generationBoundaryCancellation),
                        validator,
                        null,
                        generationBoundaryCancellation.Token);
                },
                "Cancellation observed when generation returns must stop publication.");
            AssertBytes(oldBytes, File.ReadAllBytes(targetPath), "Cancellation at the generation boundary must preserve the old formal model.");

            LargeModelTrainingCoordinator coordinator = new LargeModelTrainingCoordinator();
            CancellationTokenSource cpuCancellation = new CancellationTokenSource();
            CancelAfterModelStages cpuStages = new CancelAfterModelStages(root, cpuCancellation, false);
            AssertCancellation(
                delegate { coordinator.Execute(root, false, cpuStages, null, cpuCancellation.Token); },
                "Cancellation after ONNX rebuild must stop before CPU Bank training.");
            AssertEqual(0, cpuStages.BankCalls, "CPU Bank training must not start after model rebuild cancellation.");
            AssertEqual(0, cpuStages.PackageCalls, "CPU packaging must not start after cancellation.");

            CancellationTokenSource gpuCancellation = new CancellationTokenSource();
            CancelAfterModelStages gpuStages = new CancelAfterModelStages(root, gpuCancellation, true);
            AssertCancellation(
                delegate { coordinator.Execute(root, true, gpuStages, null, gpuCancellation.Token); },
                "Cancellation after Engine rebuild must stop before GPU Bank training.");
            AssertEqual(0, gpuStages.BankCalls, "GPU Bank training must not start after model rebuild cancellation.");
            AssertEqual(0, gpuStages.PackageCalls, "GPU packaging must not start after cancellation.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    public static void RunValidity()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            byte[] oldBytes = CreateBytes(96, 61);
            LargeModelArtifactPublisher publisher = new LargeModelArtifactPublisher();

            string onnxTarget = Path.Combine(root, "dinov2_448x224.onnx");
            File.WriteAllBytes(onnxTarget, oldBytes);
            InvalidArtifactTrainingStages zeroLengthStages = new InvalidArtifactTrainingStages(
                publisher,
                onnxTarget,
                new byte[0],
                new RecordingArtifactValidator());
            LargeModelTrainingCoordinator coordinator = new LargeModelTrainingCoordinator();
            AssertInvalidData(
                delegate { coordinator.Execute(root, false, zeroLengthStages, null, CancellationToken.None); },
                "DINOv2 ONNX",
                "A zero-byte ONNX must fail before Bank training.");
            AssertBytes(oldBytes, File.ReadAllBytes(onnxTarget), "A zero-byte ONNX must not replace the old formal ONNX.");
            AssertEqual(0, zeroLengthStages.BankCalls, "Bank training must not start after zero-byte ONNX generation.");

            File.WriteAllBytes(onnxTarget, oldBytes);
            InvalidArtifactTrainingStages shortStages = new InvalidArtifactTrainingStages(
                publisher,
                onnxTarget,
                CreateBytes(63, 67),
                new RecordingArtifactValidator());
            AssertInvalidData(
                delegate { coordinator.Execute(root, false, shortStages, null, CancellationToken.None); },
                "DINOv2 ONNX",
                "An ONNX shorter than the explicit minimum must fail.");
            AssertBytes(oldBytes, File.ReadAllBytes(onnxTarget), "A short ONNX must preserve the old formal ONNX.");
            AssertEqual(0, shortStages.BankCalls, "Bank training must not start after short ONNX generation.");

            string engineTarget = Path.Combine(root, "dinov2_448x224_fp16.engine");
            File.WriteAllBytes(engineTarget, oldBytes);
            bool loadValidationFailed = false;
            try
            {
                publisher.Publish(
                    engineTarget,
                    "TensorRT Engine",
                    64,
                    new WritingArtifactGenerator(CreateBytes(128, 73)),
                    new RejectingArtifactValidator(),
                    null,
                    CancellationToken.None);
            }
            catch (InvalidDataException ex)
            {
                loadValidationFailed = ex.Message.IndexOf("TensorRT Engine", StringComparison.Ordinal) >= 0
                    && ex.InnerException != null;
            }

            AssertTrue(loadValidationFailed, "A native-rejected Engine must report the failing stage and preserve the validation cause.");
            AssertBytes(oldBytes, File.ReadAllBytes(engineTarget), "A native-rejected Engine must not replace the old formal Engine.");

            File.WriteAllBytes(engineTarget, oldBytes);
            InvalidEngineTrainingStages invalidEngineStages = new InvalidEngineTrainingStages(publisher, root, engineTarget);
            AssertInvalidData(
                delegate { coordinator.Execute(root, true, invalidEngineStages, null, CancellationToken.None); },
                "TensorRT Engine",
                "A native-rejected Engine must stop the coordinated GPU run.");
            AssertEqual(0, invalidEngineStages.BankCalls, "GPU Bank training must not start after Engine load validation failure.");
            AssertBytes(oldBytes, File.ReadAllBytes(engineTarget), "A coordinated invalid Engine must preserve the old formal Engine.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    public static void RunConcurrency()
    {
        string root = CreateTemporaryDirectory();
        ManualResetEvent firstEntered = new ManualResetEvent(false);
        ManualResetEvent releaseFirst = new ManualResetEvent(false);
        ManualResetEvent secondEntered = new ManualResetEvent(false);
        ManualResetEvent secondAttemptStarted = new ManualResetEvent(false);
        try
        {
            LargeModelTrainingCoordinator firstCoordinator = new LargeModelTrainingCoordinator();
            LargeModelTrainingCoordinator secondCoordinator = new LargeModelTrainingCoordinator();
            BlockingTrainingStages firstStages = new BlockingTrainingStages(root, firstEntered, releaseFirst);
            SignalingTrainingStages secondStages = new SignalingTrainingStages(root, secondEntered);

            Task firstTask = Task.Factory.StartNew(
                delegate { firstCoordinator.Execute(root, false, firstStages, null, CancellationToken.None); });
            AssertTrue(firstEntered.WaitOne(2000), "The first training call must enter the protected chain.");

            Task secondTask = Task.Factory.StartNew(
                delegate
                {
                    secondAttemptStarted.Set();
                    secondCoordinator.Execute(root, false, secondStages, null, CancellationToken.None);
                });
            AssertTrue(secondAttemptStarted.WaitOne(2000), "The second training call must start attempting to enter the protected chain.");
            bool secondEnteredBeforeRelease = secondEntered.WaitOne(300);
            releaseFirst.Set();
            AssertTrue(Task.WaitAll(new[] { firstTask, secondTask }, 5000), "Both serialized training calls must finish after release.");

            AssertTrue(!secondEnteredBeforeRelease, "A second concurrent call must not enter the protected chain while the first call owns it.");
            AssertTrue(secondEntered.WaitOne(0), "The second call must enter after the first call releases the chain.");

            firstEntered.Reset();
            releaseFirst.Reset();
            ManualResetEvent cancelledWaiterEntered = new ManualResetEvent(false);
            ManualResetEvent cancelledWaiterAttemptStarted = new ManualResetEvent(false);
            CancellationTokenSource waitingCancellation = new CancellationTokenSource();
            BlockingTrainingStages lockHolderStages = new BlockingTrainingStages(root, firstEntered, releaseFirst);
            SignalingTrainingStages cancelledWaiterStages = new SignalingTrainingStages(root, cancelledWaiterEntered);
            Task lockHolderTask = Task.Factory.StartNew(
                delegate { firstCoordinator.Execute(root, false, lockHolderStages, null, CancellationToken.None); });
            AssertTrue(firstEntered.WaitOne(2000), "The lock holder must enter before testing cancellable waiting.");
            Task cancelledWaiterTask = Task.Factory.StartNew(
                delegate
                {
                    cancelledWaiterAttemptStarted.Set();
                    secondCoordinator.Execute(root, false, cancelledWaiterStages, null, waitingCancellation.Token);
                });

            AssertTrue(cancelledWaiterAttemptStarted.WaitOne(2000), "The cancellable waiter must start attempting to acquire the held lock.");
            Thread.Sleep(150);
            waitingCancellation.Cancel();
            bool cancelledWhileWaiting = WaitForCancellation(cancelledWaiterTask, 3000);
            releaseFirst.Set();
            AssertTrue(lockHolderTask.Wait(5000), "The lock holder must finish after release.");

            AssertTrue(cancelledWhileWaiting, "A caller waiting for the cross-process lock must observe cancellation.");
            AssertTrue(!cancelledWaiterEntered.WaitOne(0), "A cancelled waiter must never enter dataset preparation.");
            cancelledWaiterAttemptStarted.Dispose();
            cancelledWaiterEntered.Dispose();
            waitingCancellation.Dispose();

            VerifyIndependentProcessLock(root);
        }
        finally
        {
            releaseFirst.Set();
            firstEntered.Dispose();
            releaseFirst.Dispose();
            secondEntered.Dispose();
            secondAttemptStarted.Dispose();
            DeleteDirectory(root);
        }
    }

    private static void VerifyIndependentProcessLock(string root)
    {
        string helperScriptPath = Path.Combine(root, "hold-lock.ps1");
        string readyPath = Path.Combine(root, "holder-ready.flag");
        string releasePath = Path.Combine(root, "holder-release.flag");
        string lockPath = Path.Combine(root, ".large-model-training.lock");
        string helperScript =
            "$stream = [System.IO.File]::Open($args[0], [System.IO.FileMode]::OpenOrCreate, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)\r\n"
            + "[System.IO.File]::WriteAllText($args[1], 'ready')\r\n"
            + "try { while (-not [System.IO.File]::Exists($args[2])) { Start-Sleep -Milliseconds 25 } } finally { $stream.Dispose() }\r\n";
        File.WriteAllText(helperScriptPath, helperScript, Encoding.ASCII);

        Process holder = new Process();
        ManualResetEvent contenderAttemptStarted = new ManualResetEvent(false);
        ManualResetEvent contenderEntered = new ManualResetEvent(false);
        Task contenderTask = null;
        try
        {
            string powershellPath = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
            holder.StartInfo = new ProcessStartInfo
            {
                FileName = powershellPath,
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + QuoteArgument(helperScriptPath)
                    + " " + QuoteArgument(lockPath)
                    + " " + QuoteArgument(readyPath)
                    + " " + QuoteArgument(releasePath),
                UseShellExecute = false,
                CreateNoWindow = true
            };
            holder.Start();
            AssertTrue(WaitForFile(readyPath, 3000), "The independent helper process must acquire the lock file.");

            LargeModelTrainingCoordinator coordinator = new LargeModelTrainingCoordinator();
            SignalingTrainingStages contenderStages = new SignalingTrainingStages(root, contenderEntered);
            contenderTask = Task.Factory.StartNew(
                delegate
                {
                    contenderAttemptStarted.Set();
                    coordinator.Execute(root, false, contenderStages, null, CancellationToken.None);
                });
            AssertTrue(contenderAttemptStarted.WaitOne(2000), "The in-process contender must start while the helper process owns the lock.");
            AssertTrue(!contenderEntered.WaitOne(300), "A different process holding the lock must block dataset preparation.");

            File.WriteAllText(releasePath, "release");
            AssertTrue(holder.WaitForExit(3000), "The independent lock holder must exit after release.");
            AssertTrue(contenderTask.Wait(5000), "The contender must finish after the independent process releases the lock.");
            AssertTrue(contenderEntered.WaitOne(0), "The contender must enter only after cross-process lock release.");
        }
        finally
        {
            File.WriteAllText(releasePath, "release");
            if (!holder.HasExited)
            {
                holder.Kill();
                holder.WaitForExit(3000);
            }

            if (contenderTask != null && !contenderTask.IsCompleted)
                contenderTask.Wait(3000);
            holder.Dispose();
            contenderAttemptStarted.Dispose();
            contenderEntered.Dispose();
        }
    }

    public static void RunCleanup()
    {
        string root = CreateTemporaryDirectory();
        LockedFailingArtifactGenerator generator = null;
        try
        {
            string targetPath = Path.Combine(root, "dinov2_448x224.onnx");
            File.WriteAllBytes(targetPath, CreateBytes(96, 79));
            generator = new LockedFailingArtifactGenerator();
            CollectingProgress log = new CollectingProgress();
            LargeModelArtifactPublisher publisher = new LargeModelArtifactPublisher();
            Exception escaped = null;

            try
            {
                publisher.Publish(
                    targetPath,
                    "DINOv2 ONNX",
                    64,
                    generator,
                    new RecordingArtifactValidator(),
                    log,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                escaped = ex;
            }

            AssertTrue(object.ReferenceEquals(generator.GenerationException, escaped), "Cleanup failure must not replace the original generation exception.");
            AssertTrue(log.Contains("\u4E34\u65F6\u6A21\u578B\u6587\u4EF6\u6E05\u7406\u5931\u8D25"), "A failed temporary-file cleanup must emit a Simplified Chinese log.");
            AssertTrue(log.Contains(".building.onnx"), "The cleanup failure log must identify the temporary artifact.");
        }
        finally
        {
            if (generator != null)
                generator.Dispose();
            DeleteDirectory(root);
        }
    }

    public static void RunProcessLog()
    {
        LargeModelProcessOutputBuffer buffer = new LargeModelProcessOutputBuffer();
        const int writerCount = 8;
        const int linesPerWriter = 500;
        Task[] writers = new Task[writerCount];
        for (int writerIndex = 0; writerIndex < writerCount; writerIndex++)
        {
            int capturedWriter = writerIndex;
            writers[writerIndex] = Task.Factory.StartNew(
                delegate
                {
                    for (int lineIndex = 0; lineIndex < linesPerWriter; lineIndex++)
                        buffer.AppendLine(capturedWriter + ":" + lineIndex);
                });
        }

        AssertTrue(Task.WaitAll(writers, 5000), "Concurrent process-log writers must finish.");
        string[] lines = buffer.GetText().Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        AssertEqual(writerCount * linesPerWriter, lines.Length, "Concurrent stdout/stderr collection must not lose or corrupt lines.");

        HashSet<string> uniqueLines = new HashSet<string>(lines, StringComparer.Ordinal);
        AssertEqual(writerCount * linesPerWriter, uniqueLines.Count, "Concurrent process-log collection must preserve every distinct line once.");
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "tdjs-large-model-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
    }

    private static void AssertSequence(IList<string> actual, string[] expected, string message)
    {
        if (actual.Count != expected.Length)
            throw new InvalidOperationException(message + " Actual count: " + actual.Count + ".");

        for (int index = 0; index < expected.Length; index++)
        {
            if (!string.Equals(actual[index], expected[index], StringComparison.Ordinal))
                throw new InvalidOperationException(message + " Difference at index " + index + ".");
        }
    }

    private static void AssertEqual(int expected, int actual, string message)
    {
        if (expected != actual)
            throw new InvalidOperationException(message + " Expected " + expected + ", actual " + actual + ".");
    }

    private static void AssertEqual(string expected, string actual, string message)
    {
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(message + " Expected=" + expected + ", Actual=" + actual + ".");
    }

    private static byte[] CreateBytes(int length, byte value)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index++)
            bytes[index] = value;
        return bytes;
    }

    private static void AssertBytes(byte[] expected, byte[] actual, string message)
    {
        if (expected.Length != actual.Length)
            throw new InvalidOperationException(message + " Length mismatch.");

        for (int index = 0; index < expected.Length; index++)
        {
            if (expected[index] != actual[index])
                throw new InvalidOperationException(message + " Difference at index " + index + ".");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void AssertCancellation(Action action, string message)
    {
        bool cancelled = false;
        try
        {
            action();
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        if (!cancelled)
            throw new InvalidOperationException(message);
    }

    private static void AssertInvalidData(Action action, string stageName, string message)
    {
        bool failedAsExpected = false;
        try
        {
            action();
        }
        catch (InvalidDataException ex)
        {
            failedAsExpected = ex.Message.IndexOf(stageName, StringComparison.Ordinal) >= 0;
        }

        if (!failedAsExpected)
            throw new InvalidOperationException(message);
    }

    private static bool WaitForCancellation(Task task, int timeoutMilliseconds)
    {
        try
        {
            if (!task.Wait(timeoutMilliseconds))
                return false;
        }
        catch (AggregateException ex)
        {
            AggregateException flattened = ex.Flatten();
            return flattened.InnerExceptions.Count == 1
                && flattened.InnerExceptions[0] is OperationCanceledException;
        }

        return task.IsCanceled;
    }

    private static bool WaitForFile(string path, int timeoutMilliseconds)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
        {
            if (File.Exists(path))
                return true;
            Thread.Sleep(20);
        }

        return File.Exists(path);
    }

    private static string QuoteArgument(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private sealed class RecordingTrainingStages : ILargeModelTrainingStages
    {
        private readonly string _root;

        public RecordingTrainingStages(string root)
        {
            _root = root;
            Events = new List<string>();
            EngineInputPaths = new List<string>();
            BankInputPaths = new List<string>();
            PackageInputPaths = new List<string>();
        }

        public List<string> Events { get; private set; }

        public List<string> EngineInputPaths { get; private set; }

        public List<string> BankInputPaths { get; private set; }

        public List<string> PackageInputPaths { get; private set; }

        public int OnnxCalls { get; private set; }

        public int EngineCalls { get; private set; }

        public int BankCalls { get; private set; }

        public int PackageCalls { get; private set; }

        public void PrepareDataset(CancellationToken token)
        {
            Events.Add("dataset");
        }

        public string RebuildOnnxModel(CancellationToken token)
        {
            OnnxCalls++;
            Events.Add("onnx");
            return Path.Combine(_root, "run" + OnnxCalls + ".onnx");
        }

        public string RebuildTensorRtEngine(string onnxPath, CancellationToken token)
        {
            EngineCalls++;
            Events.Add("engine");
            EngineInputPaths.Add(onnxPath);
            return Path.Combine(_root, "run" + EngineCalls + ".engine");
        }

        public void TrainMemoryBank(string modelPath, CancellationToken token)
        {
            BankCalls++;
            Events.Add("bank");
            BankInputPaths.Add(modelPath);
        }

        public void PackageTemplate(string modelPath, CancellationToken token)
        {
            PackageCalls++;
            Events.Add("package");
            PackageInputPaths.Add(modelPath);
        }
    }

    private sealed class WritingArtifactGenerator : ILargeModelArtifactGenerator
    {
        private readonly byte[] _bytes;

        public WritingArtifactGenerator(byte[] bytes)
        {
            _bytes = bytes;
        }

        public void Generate(string temporaryPath, CancellationToken token)
        {
            File.WriteAllBytes(temporaryPath, _bytes);
        }
    }

    private sealed class ThrowingArtifactGenerator : ILargeModelArtifactGenerator
    {
        public void Generate(string temporaryPath, CancellationToken token)
        {
            throw new InvalidOperationException("generation failed");
        }
    }

    private sealed class RecordingArtifactValidator : ILargeModelArtifactValidator
    {
        public int Calls { get; private set; }

        public string ValidatedPath { get; private set; }

        public void Validate(string artifactPath)
        {
            Calls++;
            ValidatedPath = artifactPath;
        }
    }

    private sealed class CountingArtifactGenerator : ILargeModelArtifactGenerator
    {
        private readonly byte[] _bytes;

        public CountingArtifactGenerator(byte[] bytes)
        {
            _bytes = bytes;
        }

        public int Calls { get; private set; }

        public void Generate(string temporaryPath, CancellationToken token)
        {
            Calls++;
            File.WriteAllBytes(temporaryPath, _bytes);
        }
    }

    private sealed class CancelingArtifactGenerator : ILargeModelArtifactGenerator
    {
        private readonly byte[] _bytes;
        private readonly CancellationTokenSource _cancellation;

        public CancelingArtifactGenerator(byte[] bytes, CancellationTokenSource cancellation)
        {
            _bytes = bytes;
            _cancellation = cancellation;
        }

        public void Generate(string temporaryPath, CancellationToken token)
        {
            File.WriteAllBytes(temporaryPath, _bytes);
            _cancellation.Cancel();
        }
    }

    private sealed class CancelAfterModelStages : ILargeModelTrainingStages
    {
        private readonly string _root;
        private readonly CancellationTokenSource _cancellation;
        private readonly bool _cancelAfterEngine;

        public CancelAfterModelStages(string root, CancellationTokenSource cancellation, bool cancelAfterEngine)
        {
            _root = root;
            _cancellation = cancellation;
            _cancelAfterEngine = cancelAfterEngine;
        }

        public int BankCalls { get; private set; }

        public int PackageCalls { get; private set; }

        public void PrepareDataset(CancellationToken token)
        {
        }

        public string RebuildOnnxModel(CancellationToken token)
        {
            if (!_cancelAfterEngine)
                _cancellation.Cancel();
            return Path.Combine(_root, "current.onnx");
        }

        public string RebuildTensorRtEngine(string onnxPath, CancellationToken token)
        {
            _cancellation.Cancel();
            return Path.Combine(_root, "current.engine");
        }

        public void TrainMemoryBank(string modelPath, CancellationToken token)
        {
            BankCalls++;
        }

        public void PackageTemplate(string modelPath, CancellationToken token)
        {
            PackageCalls++;
        }
    }

    private sealed class RejectingArtifactValidator : ILargeModelArtifactValidator
    {
        public void Validate(string artifactPath)
        {
            throw new InvalidDataException("native rejected artifact");
        }
    }

    private sealed class InvalidArtifactTrainingStages : ILargeModelTrainingStages
    {
        private readonly LargeModelArtifactPublisher _publisher;
        private readonly string _targetPath;
        private readonly byte[] _generatedBytes;
        private readonly ILargeModelArtifactValidator _validator;

        public InvalidArtifactTrainingStages(
            LargeModelArtifactPublisher publisher,
            string targetPath,
            byte[] generatedBytes,
            ILargeModelArtifactValidator validator)
        {
            _publisher = publisher;
            _targetPath = targetPath;
            _generatedBytes = generatedBytes;
            _validator = validator;
        }

        public int BankCalls { get; private set; }

        public void PrepareDataset(CancellationToken token)
        {
        }

        public string RebuildOnnxModel(CancellationToken token)
        {
            return _publisher.Publish(
                _targetPath,
                "DINOv2 ONNX",
                64,
                new WritingArtifactGenerator(_generatedBytes),
                _validator,
                null,
                token);
        }

        public string RebuildTensorRtEngine(string onnxPath, CancellationToken token)
        {
            throw new InvalidOperationException("GPU stage is not expected in this test.");
        }

        public void TrainMemoryBank(string modelPath, CancellationToken token)
        {
            BankCalls++;
        }

        public void PackageTemplate(string modelPath, CancellationToken token)
        {
        }
    }

    private sealed class FailingArtifactTrainingStages : ILargeModelTrainingStages
    {
        private readonly LargeModelArtifactPublisher _publisher;
        private readonly string _targetPath;

        public FailingArtifactTrainingStages(LargeModelArtifactPublisher publisher, string targetPath)
        {
            _publisher = publisher;
            _targetPath = targetPath;
        }

        public int BankCalls { get; private set; }

        public void PrepareDataset(CancellationToken token)
        {
        }

        public string RebuildOnnxModel(CancellationToken token)
        {
            return _publisher.Publish(
                _targetPath,
                "DINOv2 ONNX",
                64,
                new ThrowingArtifactGenerator(),
                new RecordingArtifactValidator(),
                null,
                token);
        }

        public string RebuildTensorRtEngine(string onnxPath, CancellationToken token)
        {
            throw new InvalidOperationException("GPU stage is not expected in this test.");
        }

        public void TrainMemoryBank(string modelPath, CancellationToken token)
        {
            BankCalls++;
        }

        public void PackageTemplate(string modelPath, CancellationToken token)
        {
        }
    }

    private sealed class InvalidEngineTrainingStages : ILargeModelTrainingStages
    {
        private readonly LargeModelArtifactPublisher _publisher;
        private readonly string _root;
        private readonly string _engineTarget;

        public InvalidEngineTrainingStages(LargeModelArtifactPublisher publisher, string root, string engineTarget)
        {
            _publisher = publisher;
            _root = root;
            _engineTarget = engineTarget;
        }

        public int BankCalls { get; private set; }

        public void PrepareDataset(CancellationToken token)
        {
        }

        public string RebuildOnnxModel(CancellationToken token)
        {
            string onnxPath = Path.Combine(_root, "current.onnx");
            File.WriteAllBytes(onnxPath, CreateBytes(128, 83));
            return onnxPath;
        }

        public string RebuildTensorRtEngine(string onnxPath, CancellationToken token)
        {
            return _publisher.Publish(
                _engineTarget,
                "TensorRT Engine",
                64,
                new WritingArtifactGenerator(CreateBytes(128, 89)),
                new RejectingArtifactValidator(),
                null,
                token);
        }

        public void TrainMemoryBank(string modelPath, CancellationToken token)
        {
            BankCalls++;
        }

        public void PackageTemplate(string modelPath, CancellationToken token)
        {
        }
    }

    private sealed class BlockingTrainingStages : ILargeModelTrainingStages
    {
        private readonly string _root;
        private readonly ManualResetEvent _entered;
        private readonly ManualResetEvent _release;

        public BlockingTrainingStages(string root, ManualResetEvent entered, ManualResetEvent release)
        {
            _root = root;
            _entered = entered;
            _release = release;
        }

        public void PrepareDataset(CancellationToken token)
        {
            _entered.Set();
            _release.WaitOne();
        }

        public string RebuildOnnxModel(CancellationToken token)
        {
            return Path.Combine(_root, "blocking.onnx");
        }

        public string RebuildTensorRtEngine(string onnxPath, CancellationToken token)
        {
            return Path.Combine(_root, "blocking.engine");
        }

        public void TrainMemoryBank(string modelPath, CancellationToken token)
        {
        }

        public void PackageTemplate(string modelPath, CancellationToken token)
        {
        }
    }

    private sealed class SignalingTrainingStages : ILargeModelTrainingStages
    {
        private readonly string _root;
        private readonly ManualResetEvent _entered;

        public SignalingTrainingStages(string root, ManualResetEvent entered)
        {
            _root = root;
            _entered = entered;
        }

        public void PrepareDataset(CancellationToken token)
        {
            _entered.Set();
        }

        public string RebuildOnnxModel(CancellationToken token)
        {
            return Path.Combine(_root, "signaling.onnx");
        }

        public string RebuildTensorRtEngine(string onnxPath, CancellationToken token)
        {
            return Path.Combine(_root, "signaling.engine");
        }

        public void TrainMemoryBank(string modelPath, CancellationToken token)
        {
        }

        public void PackageTemplate(string modelPath, CancellationToken token)
        {
        }
    }

    private sealed class LockedFailingArtifactGenerator : ILargeModelArtifactGenerator, IDisposable
    {
        private FileStream _lockedStream;

        public LockedFailingArtifactGenerator()
        {
            GenerationException = new InvalidOperationException("generation failed while temporary file is locked");
        }

        public Exception GenerationException { get; private set; }

        public void Generate(string temporaryPath, CancellationToken token)
        {
            _lockedStream = new FileStream(temporaryPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            _lockedStream.WriteByte(1);
            _lockedStream.Flush();
            throw GenerationException;
        }

        public void Dispose()
        {
            if (_lockedStream != null)
            {
                _lockedStream.Dispose();
                _lockedStream = null;
            }
        }
    }

    private sealed class CollectingProgress : IProgress<string>
    {
        private readonly List<string> _messages = new List<string>();

        public void Report(string value)
        {
            lock (_messages)
            {
                _messages.Add(value ?? string.Empty);
            }
        }

        public bool Contains(string value)
        {
            lock (_messages)
            {
                foreach (string message in _messages)
                {
                    if (message.IndexOf(value, StringComparison.Ordinal) >= 0)
                        return true;
                }
            }

            return false;
        }
    }
}
}
'@

Add-Type -TypeDefinition ($pipelineSource + [Environment]::NewLine + $testHarness) -ReferencedAssemblies @("System.dll", "System.Core.dll")

switch ($Case) {
    "Ordering" { [LargeModelTrainingBehaviorTests.LargeModelTrainingBehaviorHarness]::RunOrdering() }
    "Publishing" { [LargeModelTrainingBehaviorTests.LargeModelTrainingBehaviorHarness]::RunPublishing() }
    "Cancellation" { [LargeModelTrainingBehaviorTests.LargeModelTrainingBehaviorHarness]::RunCancellation() }
    "Validity" { [LargeModelTrainingBehaviorTests.LargeModelTrainingBehaviorHarness]::RunValidity() }
    "Concurrency" { [LargeModelTrainingBehaviorTests.LargeModelTrainingBehaviorHarness]::RunConcurrency() }
    "Cleanup" { [LargeModelTrainingBehaviorTests.LargeModelTrainingBehaviorHarness]::RunCleanup() }
    "ProcessLog" { [LargeModelTrainingBehaviorTests.LargeModelTrainingBehaviorHarness]::RunProcessLog() }
    "All" {
        [LargeModelTrainingBehaviorTests.LargeModelTrainingBehaviorHarness]::RunOrdering()
        [LargeModelTrainingBehaviorTests.LargeModelTrainingBehaviorHarness]::RunPublishing()
        [LargeModelTrainingBehaviorTests.LargeModelTrainingBehaviorHarness]::RunCancellation()
        [LargeModelTrainingBehaviorTests.LargeModelTrainingBehaviorHarness]::RunValidity()
        [LargeModelTrainingBehaviorTests.LargeModelTrainingBehaviorHarness]::RunConcurrency()
        [LargeModelTrainingBehaviorTests.LargeModelTrainingBehaviorHarness]::RunCleanup()
        [LargeModelTrainingBehaviorTests.LargeModelTrainingBehaviorHarness]::RunProcessLog()
    }
}

Write-Host "Large-model executable training behavior checks passed: $Case."
