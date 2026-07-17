param(
    [ValidateSet("All", "NativeLifecycle", "PythonIsolation", "PathBoundary", "TemplatePublishing", "ProductionEntry")]
    [string]$Case = "All"
)

$ErrorActionPreference = "Stop"

# 沙箱可能同时注入 Path 与 PATH；先统一键名，避免 ProcessStartInfo 构造环境块时因重复键失败。
$inheritedProcessPath = [Environment]::GetEnvironmentVariable("PATH", "Process")
[Environment]::SetEnvironmentVariable("Path", $null, "Process")
[Environment]::SetEnvironmentVariable("PATH", $inheritedProcessPath, "Process")

$root = Split-Path -Parent $PSScriptRoot
$detectorPath = Join-Path $root "Forms\AiTrainForm\LargeModelDinov2Detector.cs"
$pipelinePath = Join-Path $root "Forms\AiTrainForm\LargeModelTrainingPipeline.cs"
$templatePackagePath = Join-Path $root "Forms\AiTrainForm\LargeModelTemplatePackage.cs"
$trainingModelsPath = Join-Path $root "Forms\AiTrainForm\LargeModelTrainingModels.cs"
$runtimeBootstrapperPath = Join-Path $root "Forms\AiTrainForm\LargeModelRuntimeBootstrapper.cs"
$trainingServicePath = Join-Path $root "Forms\AiTrainForm\LargeModelTrainingService.cs"
$openCvPath = Join-Path $root "packages\OpenCvSharp4.4.10.0.20240616\lib\net48\OpenCvSharp.dll"
$newtonsoftPath = Join-Path $root "packages\Newtonsoft.Json.13.0.3\lib\net45\Newtonsoft.Json.dll"
$cscPath = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe"

foreach ($requiredPath in @(
    $detectorPath,
    $pipelinePath,
    $templatePackagePath,
    $trainingModelsPath,
    $runtimeBootstrapperPath,
    $trainingServicePath,
    $openCvPath,
    $newtonsoftPath,
    $cscPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Missing production behavior test dependency: $requiredPath"
    }
}

[void][Reflection.Assembly]::LoadFrom($openCvPath)
[void][Reflection.Assembly]::LoadFrom($newtonsoftPath)
$productionSourcePaths = @(
    $pipelinePath,
    $detectorPath,
    $templatePackagePath,
    $trainingModelsPath,
    $runtimeBootstrapperPath,
    $trainingServicePath
)
$testHarness = @'
namespace LargeModelTrainingProductionBehaviorTests
{
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Forms.AiTrainForm;

public static class LargeModelTrainingProductionBehaviorHarness
{
    public static void RunPythonIsolation()
    {
        string root = CreateTemporaryDirectory();
        string runtimeRoot = Path.Combine(root, "LargeModelDll");
        string pythonRoot = Path.Combine(runtimeRoot, "third_party", "python_native_env");
        string torchCacheRoot = Path.Combine(runtimeRoot, "_torch_cache");
        Directory.CreateDirectory(Path.Combine(pythonRoot, "Library", "bin"));
        Directory.CreateDirectory(Path.Combine(pythonRoot, "Scripts"));
        Directory.CreateDirectory(torchCacheRoot);

        try
        {
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.UseShellExecute = false;
            startInfo.EnvironmentVariables.Clear();
            startInfo.EnvironmentVariables["PATH"] = "C:\\Windows\\System32";
            startInfo.EnvironmentVariables["PYTHONHOME"] = Path.Combine(root, "UnsupervisedDll", "python_env");
            startInfo.EnvironmentVariables["PYTHONPATH"] = Path.Combine(root, "polluted-site-packages");

            System.Reflection.MethodInfo configureMethod = typeof(LargeModelTrainingService).GetMethod(
                "ConfigureOnnxExportEnvironment",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            AssertTrue(configureMethod != null, "The production service must expose the private ONNX environment configurator.");
            configureMethod.Invoke(null, new object[] { startInfo, runtimeRoot, torchCacheRoot });

            AssertEqual(
                pythonRoot,
                startInfo.EnvironmentVariables["PYTHONHOME"],
                "ONNX export must replace the globally polluted PYTHONHOME with its own Python root.");
            AssertEqual(
                string.Empty,
                startInfo.EnvironmentVariables["PYTHONPATH"],
                "ONNX export must clear a globally polluted PYTHONPATH.");
            string expectedPathPrefix = runtimeRoot
                + ";" + pythonRoot
                + ";" + Path.Combine(pythonRoot, "Library", "bin")
                + ";" + Path.Combine(pythonRoot, "Scripts")
                + ";";
            AssertTrue(
                startInfo.EnvironmentVariables["PATH"].StartsWith(expectedPathPrefix, StringComparison.OrdinalIgnoreCase),
                "ONNX export must keep its runtime and native dependency directories at the front of PATH.");
            AssertEqual(
                torchCacheRoot,
                startInfo.EnvironmentVariables["TORCH_HOME"],
                "ONNX export must keep the isolated Torch cache root.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    public static void RunNativeLifecycle()
    {
        string root = CreateTemporaryDirectory();
        string modelPath = Path.Combine(root, "blocking.onnx");
        string bankPath = Path.Combine(root, "memory.bank");
        string okPath = Path.Combine(root, "OK");
        File.WriteAllBytes(modelPath, new byte[] { 1, 2, 3, 4 });
        Directory.CreateDirectory(okPath);

        BlockingNativeApi nativeApi = new BlockingNativeApi();
        CancellationTokenSource cancellation = new CancellationTokenSource();
        LargeModelDinov2Detector detector = new LargeModelDinov2Detector(root, nativeApi);
        try
        {
            Task trainingTask = Task.Factory.StartNew(
                delegate
                {
                    detector.TrainMemoryBank(
                        modelPath,
                        bankPath,
                        2,
                        okPath,
                        string.Empty,
                        cancellation.Token);
                });

            AssertTrue(nativeApi.InitEntered.WaitOne(3000), "The real detector must enter the controllable native Init call.");
            cancellation.Cancel();
            detector.Dispose();
            nativeApi.AllowInitReturn.Set();

            AssertTrue(WaitForCancellation(trainingTask, 3000), "Cancellation during native Init must escape as cancellation.");
            AssertEqual(1, nativeApi.InitCalls, "Native Init must run exactly once.");
            AssertEqual(0, nativeApi.TrainCalls, "Native Train must never start after cancellation during Init.");
            AssertEqual(1, nativeApi.ReleaseCalls, "The local handle returned after Dispose must be released exactly once.");
            AssertEqual(new IntPtr(0x1234), nativeApi.LastReleasedHandle, "The rejected local Init handle must be released.");

            detector.Dispose();
            AssertEqual(1, nativeApi.ReleaseCalls, "Repeated Dispose must not revive or release the rejected handle twice.");
        }
        finally
        {
            nativeApi.AllowInitReturn.Set();
            detector.Dispose();
            cancellation.Dispose();
            nativeApi.Dispose();
            DeleteDirectory(root);
        }
    }

    public static void RunPathBoundary()
    {
        string root = CreateTemporaryDirectory();
        string runtimeRoot = Path.Combine(root, "LargeModelDll");
        string modelRoot = Path.Combine(root, "Model");
        string modelDatasetRoot = Path.Combine(modelRoot, "dataset");
        string modelOutputRoot = Path.Combine(modelRoot, "model");
        string datasetSentinel = Path.Combine(modelDatasetRoot, "dataset-sentinel.txt");
        string modelSentinel = Path.Combine(modelOutputRoot, "model-sentinel.txt");
        string imagePath = Path.Combine(root, "ok.png");
        Directory.CreateDirectory(runtimeRoot);
        Directory.CreateDirectory(modelDatasetRoot);
        Directory.CreateDirectory(modelOutputRoot);
        File.WriteAllText(datasetSentinel, "keep-dataset");
        File.WriteAllText(modelSentinel, "keep-model");
        File.WriteAllBytes(imagePath, new byte[] { 1 });

        try
        {
            LargeModelTrainingRequest request = new LargeModelTrainingRequest
            {
                TemplateName = "..",
                OutputTemplatePath = Path.Combine(modelRoot, "safe.tdlarge"),
                Images = new List<LargeModelImageItem>
                {
                    new LargeModelImageItem
                    {
                        FilePath = imagePath,
                        DisplayName = "ok.png",
                        Category = LargeModelImageCategory.OK
                    }
                },
                ModelType = "DINOv2",
                Precision = "fp16",
                Device = "CPU",
                InputWidth = 448,
                InputHeight = 224,
                AreaThreshold = 0
            };

            LargeModelTrainingService service = new LargeModelTrainingService(
                new LargeModelTrainingCoordinator(),
                new LargeModelArtifactPublisher(),
                new FixedTrainingEnvironment(runtimeRoot, modelRoot),
                null,
                new NeverModelRebuilder(),
                new NeverDetectorFactory(),
                new NeverTemplatePublisher());

            bool rejected = false;
            string rejectionMessage = string.Empty;
            try
            {
                service.Train(request, null, null, CancellationToken.None);
            }
            catch (InvalidOperationException ex)
            {
                rejectionMessage = ex.Message;
                rejected = true;
            }

            AssertTrue(
                rejected,
                "The production service must reject '..' before constructing or cleaning a work directory. Actual=" + rejectionMessage);
            AssertEqual("keep-dataset", File.ReadAllText(datasetSentinel), "Model\\dataset sentinel must remain unchanged.");
            AssertEqual("keep-model", File.ReadAllText(modelSentinel), "Model\\model sentinel must remain unchanged.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    public static void RunTemplatePublishing()
    {
        string root = CreateTemporaryDirectory();
        string templatePath = Path.Combine(root, "customer.tdlarge");
        string modelPath = Path.Combine(root, "current.onnx");
        string bankPath = Path.Combine(root, "memory.bank");
        byte[] oldTemplateBytes = new byte[] { 41, 42, 43, 44 };
        byte[] modelBytes = CreateBytes(512 * 1024, 17);
        byte[] bankBytes = CreateBytes(4096, 29);
        File.WriteAllBytes(modelPath, modelBytes);
        File.WriteAllBytes(bankPath, bankBytes);

        try
        {
            File.WriteAllBytes(templatePath, oldTemplateBytes);
            BlockingTemplateSourceStreamFactory blockingFactory = new BlockingTemplateSourceStreamFactory(modelPath);
            LargeModelTemplatePublisher blockingPublisher = new LargeModelTemplatePublisher(blockingFactory);
            CancellationTokenSource cancellation = new CancellationTokenSource();
            try
            {
                Task publishingTask = Task.Factory.StartNew(
                    delegate
                    {
                        blockingPublisher.Publish(
                            templatePath,
                            CreateManifest("cancelled"),
                            modelPath,
                            bankPath,
                            null,
                            cancellation.Token);
                    });

                AssertTrue(blockingFactory.ReadEntered.WaitOne(3000), "Template publication must enter the controllable model copy.");
                cancellation.Cancel();
                blockingFactory.AllowRead.Set();
                AssertTrue(WaitForCancellation(publishingTask, 3000), "Cancellation during model copy must escape as cancellation.");
                AssertBytes(oldTemplateBytes, File.ReadAllBytes(templatePath), "Cancellation must preserve the old formal template.");
                AssertNoTemplateTemporaryFiles(root, "Cancellation must clean the GUID temporary template.");
            }
            finally
            {
                blockingFactory.AllowRead.Set();
                blockingFactory.Dispose();
                cancellation.Dispose();
            }

            File.WriteAllBytes(templatePath, oldTemplateBytes);
            LargeModelTemplatePublisher failingPublisher = new LargeModelTemplatePublisher(
                new ThrowingTemplateSourceStreamFactory(modelPath));
            bool originalFailureEscaped = false;
            try
            {
                failingPublisher.Publish(
                    templatePath,
                    CreateManifest("failed"),
                    modelPath,
                    bankPath,
                    null,
                    CancellationToken.None);
            }
            catch (IOException ex)
            {
                originalFailureEscaped = string.Equals(ex.Message, "template source failed", StringComparison.Ordinal);
            }

            AssertTrue(originalFailureEscaped, "The original template write failure must escape publication.");
            AssertBytes(oldTemplateBytes, File.ReadAllBytes(templatePath), "A template write failure must preserve the old formal template.");
            AssertNoTemplateTemporaryFiles(root, "A template write failure must clean the GUID temporary template.");

            File.WriteAllBytes(templatePath, oldTemplateBytes);
            LargeModelTemplatePublisher publisher = new LargeModelTemplatePublisher();
            publisher.Publish(
                templatePath,
                CreateManifest("success"),
                modelPath,
                bankPath,
                null,
                CancellationToken.None);

            AssertTrue(!BytesEqual(oldTemplateBytes, File.ReadAllBytes(templatePath)), "Successful publication must atomically replace the old template.");
            using (FileStream stream = new FileStream(templatePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                AssertTrue(archive.GetEntry("manifest.json") != null, "The committed template must contain manifest.json.");
                AssertTrue(archive.GetEntry("current.onnx") != null, "The committed template must contain this run's model.");
                AssertTrue(archive.GetEntry("bank.bin") != null, "The committed template must contain this run's bank.");
            }
            AssertNoTemplateTemporaryFiles(root, "Successful publication must move away the GUID temporary template.");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    public static void RunProductionEntry()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            VerifyProductionPathFlow(Path.Combine(root, "path-flow"));
            VerifyProductionInitCancellation(Path.Combine(root, "init-cancel"));
            VerifyProductionPackageCancellation(Path.Combine(root, "package-cancel"));
            VerifyProductionPackageFailure(Path.Combine(root, "package-failure"));
            VerifyCommittedTemplateIsNotReportedCancelled(Path.Combine(root, "post-commit"));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void VerifyProductionPathFlow(string root)
    {
        string runtimeRoot = Path.Combine(root, "LargeModelDll");
        string modelRoot = Path.Combine(root, "Model");
        string imagePath = CreateTrainingImage(root);

        RecordingModelRebuilder cpuRebuilder = new RecordingModelRebuilder(runtimeRoot, "cpu");
        RecordingNativeApi cpuNative = new RecordingNativeApi();
        RecordingTemplatePublisher cpuPublisher = new RecordingTemplatePublisher(new LargeModelTemplatePublisher());
        LargeModelTrainingService cpuService = CreateProductionService(
            runtimeRoot,
            modelRoot,
            cpuRebuilder,
            new RecordingDetectorFactory(cpuNative),
            cpuPublisher);
        LargeModelTrainingResult cpuResult = cpuService.Train(
            CreateRequest(modelRoot, imagePath, "cpu-template", "CPU"),
            null,
            null,
            CancellationToken.None);

        AssertEqual(1, cpuRebuilder.OnnxCalls, "The production CPU entry must rebuild ONNX once.");
        AssertEqual(0, cpuRebuilder.EngineCalls, "The production CPU entry must not build an Engine.");
        AssertEqual(cpuRebuilder.LastOnnxPath, cpuNative.GetSingleTrainedModelPath(), "CPU Bank must consume this run's ONNX.");
        AssertEqual(cpuRebuilder.LastOnnxPath, cpuPublisher.GetSingleModelPath(), "CPU Package must consume this run's ONNX.");
        AssertTrue(File.Exists(cpuResult.TemplatePath), "The production CPU entry must publish a template.");

        RecordingModelRebuilder gpuRebuilder = new RecordingModelRebuilder(runtimeRoot, "gpu");
        RecordingNativeApi gpuNative = new RecordingNativeApi();
        RecordingTemplatePublisher gpuPublisher = new RecordingTemplatePublisher(new LargeModelTemplatePublisher());
        LargeModelTrainingService gpuService = CreateProductionService(
            runtimeRoot,
            modelRoot,
            gpuRebuilder,
            new RecordingDetectorFactory(gpuNative),
            gpuPublisher);
        LargeModelTrainingResult gpuResult = gpuService.Train(
            CreateRequest(modelRoot, imagePath, "gpu-template", "GPU"),
            null,
            null,
            CancellationToken.None);

        AssertEqual(1, gpuRebuilder.OnnxCalls, "The production GPU entry must rebuild ONNX once.");
        AssertEqual(1, gpuRebuilder.EngineCalls, "The production GPU entry must build one Engine.");
        AssertEqual(gpuRebuilder.LastOnnxPath, gpuRebuilder.GetSingleEngineInput(), "GPU Engine must consume this run's ONNX.");
        AssertEqual(gpuRebuilder.LastEnginePath, gpuNative.GetSingleTrainedModelPath(), "GPU Bank must consume this run's Engine.");
        AssertEqual(gpuRebuilder.LastEnginePath, gpuPublisher.GetSingleModelPath(), "GPU Package must consume this run's Engine.");
        AssertTrue(File.Exists(gpuResult.TemplatePath), "The production GPU entry must publish a template.");
    }

    private static void VerifyProductionInitCancellation(string root)
    {
        string runtimeRoot = Path.Combine(root, "LargeModelDll");
        string modelRoot = Path.Combine(root, "Model");
        string imagePath = CreateTrainingImage(root);
        string templatePath = Path.Combine(modelRoot, "init-cancel.tdlarge");
        byte[] oldTemplateBytes = new byte[] { 71, 72, 73 };
        Directory.CreateDirectory(modelRoot);
        File.WriteAllBytes(templatePath, oldTemplateBytes);

        RecordingModelRebuilder rebuilder = new RecordingModelRebuilder(runtimeRoot, "init-cancel");
        BlockingNativeApi nativeApi = new BlockingNativeApi();
        RecordingTemplatePublisher publisher = new RecordingTemplatePublisher(new LargeModelTemplatePublisher());
        LargeModelTrainingService service = CreateProductionService(
            runtimeRoot,
            modelRoot,
            rebuilder,
            new RecordingDetectorFactory(nativeApi),
            publisher);
        CancellationTokenSource cancellation = new CancellationTokenSource();
        try
        {
            Task trainingTask = Task.Factory.StartNew(
                delegate
                {
                    service.Train(
                        CreateRequest(modelRoot, imagePath, "init-cancel", "CPU"),
                        null,
                        null,
                        cancellation.Token);
                });

            AssertTrue(nativeApi.InitEntered.WaitOne(3000), "The production Bank stage must enter controllable native Init.");
            cancellation.Cancel();
            service.CancelActiveTraining();
            nativeApi.AllowInitReturn.Set();

            AssertTrue(WaitForCancellation(trainingTask, 3000), "The production service must report cancellation from blocked Init.");
            AssertEqual(0, nativeApi.TrainCalls, "The production service must not enter native Train after Init cancellation.");
            AssertEqual(1, nativeApi.ReleaseCalls, "The production service must release the late Init handle once.");
            AssertEqual(0, publisher.Calls, "The production service must not package after Init cancellation.");
            AssertBytes(oldTemplateBytes, File.ReadAllBytes(templatePath), "Init cancellation must preserve the old formal template.");
        }
        finally
        {
            nativeApi.AllowInitReturn.Set();
            service.CancelActiveTraining();
            cancellation.Dispose();
            nativeApi.Dispose();
        }
    }

    private static void VerifyProductionPackageCancellation(string root)
    {
        string runtimeRoot = Path.Combine(root, "LargeModelDll");
        string modelRoot = Path.Combine(root, "Model");
        string imagePath = CreateTrainingImage(root);
        string templatePath = Path.Combine(modelRoot, "package-cancel.tdlarge");
        byte[] oldTemplateBytes = new byte[] { 81, 82, 83 };
        Directory.CreateDirectory(modelRoot);
        File.WriteAllBytes(templatePath, oldTemplateBytes);

        RecordingModelRebuilder rebuilder = new RecordingModelRebuilder(runtimeRoot, "package-cancel");
        RecordingNativeApi nativeApi = new RecordingNativeApi();
        BlockingTemplateSourceStreamFactory sourceFactory = new BlockingTemplateSourceStreamFactory(rebuilder.ExpectedFirstOnnxPath);
        LargeModelTrainingService service = CreateProductionService(
            runtimeRoot,
            modelRoot,
            rebuilder,
            new RecordingDetectorFactory(nativeApi),
            new LargeModelTemplatePublisher(sourceFactory));
        CancellationTokenSource cancellation = new CancellationTokenSource();
        try
        {
            Task trainingTask = Task.Factory.StartNew(
                delegate
                {
                    service.Train(
                        CreateRequest(modelRoot, imagePath, "package-cancel", "CPU"),
                        null,
                        null,
                        cancellation.Token);
                });

            AssertTrue(sourceFactory.ReadEntered.WaitOne(3000), "The production service must enter controllable template copy.");
            cancellation.Cancel();
            sourceFactory.AllowRead.Set();
            AssertTrue(WaitForCancellation(trainingTask, 3000), "The production service must report cancellation during package copy.");
            AssertBytes(oldTemplateBytes, File.ReadAllBytes(templatePath), "Package cancellation must preserve the old formal template.");
            AssertNoTemplateTemporaryFiles(modelRoot, "Package cancellation through the service must clean temporary templates.");
        }
        finally
        {
            sourceFactory.AllowRead.Set();
            sourceFactory.Dispose();
            cancellation.Dispose();
        }
    }

    private static void VerifyProductionPackageFailure(string root)
    {
        string runtimeRoot = Path.Combine(root, "LargeModelDll");
        string modelRoot = Path.Combine(root, "Model");
        string imagePath = CreateTrainingImage(root);
        string templatePath = Path.Combine(modelRoot, "package-failure.tdlarge");
        byte[] oldTemplateBytes = new byte[] { 91, 92, 93 };
        Directory.CreateDirectory(modelRoot);
        File.WriteAllBytes(templatePath, oldTemplateBytes);

        RecordingModelRebuilder rebuilder = new RecordingModelRebuilder(runtimeRoot, "package-failure");
        LargeModelTrainingService service = CreateProductionService(
            runtimeRoot,
            modelRoot,
            rebuilder,
            new RecordingDetectorFactory(new RecordingNativeApi()),
            new LargeModelTemplatePublisher(new ThrowingTemplateSourceStreamFactory(rebuilder.ExpectedFirstOnnxPath)));
        bool failed = false;
        try
        {
            service.Train(
                CreateRequest(modelRoot, imagePath, "package-failure", "CPU"),
                null,
                null,
                CancellationToken.None);
        }
        catch (IOException ex)
        {
            failed = string.Equals(ex.Message, "template source failed", StringComparison.Ordinal);
        }

        AssertTrue(failed, "The production service must preserve the original package failure.");
        AssertBytes(oldTemplateBytes, File.ReadAllBytes(templatePath), "Package failure must preserve the old formal template.");
        AssertNoTemplateTemporaryFiles(modelRoot, "Package failure through the service must clean temporary templates.");
    }

    private static void VerifyCommittedTemplateIsNotReportedCancelled(string root)
    {
        string runtimeRoot = Path.Combine(root, "LargeModelDll");
        string modelRoot = Path.Combine(root, "Model");
        string imagePath = CreateTrainingImage(root);
        RecordingModelRebuilder rebuilder = new RecordingModelRebuilder(runtimeRoot, "post-commit");
        CancellationTokenSource cancellation = new CancellationTokenSource();
        try
        {
            LargeModelTrainingService service = CreateProductionService(
                runtimeRoot,
                modelRoot,
                rebuilder,
                new RecordingDetectorFactory(new RecordingNativeApi()),
                new LargeModelTemplatePublisher());
            LargeModelTrainingResult result = service.Train(
                CreateRequest(modelRoot, imagePath, "post-commit", "CPU"),
                new CancelOnCompletionProgress(cancellation),
                null,
                cancellation.Token);

            AssertTrue(cancellation.IsCancellationRequested, "The test must request cancellation immediately after package commit.");
            AssertTrue(File.Exists(result.TemplatePath), "A committed template must remain a successful service result.");
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private static LargeModelTrainingService CreateProductionService(
        string runtimeRoot,
        string modelRoot,
        ILargeModelTrainingModelRebuilder rebuilder,
        ILargeModelDinov2DetectorFactory detectorFactory,
        ILargeModelTemplatePublisher templatePublisher)
    {
        Directory.CreateDirectory(runtimeRoot);
        Directory.CreateDirectory(modelRoot);
        return new LargeModelTrainingService(
            new LargeModelTrainingCoordinator(),
            new LargeModelArtifactPublisher(),
            new FixedTrainingEnvironment(runtimeRoot, modelRoot),
            null,
            rebuilder,
            detectorFactory,
            templatePublisher);
    }

    private static LargeModelTrainingRequest CreateRequest(
        string modelRoot,
        string imagePath,
        string templateName,
        string device)
    {
        return new LargeModelTrainingRequest
        {
            TemplateName = templateName,
            OutputTemplatePath = Path.Combine(modelRoot, templateName + ".tdlarge"),
            Images = new List<LargeModelImageItem>
            {
                new LargeModelImageItem
                {
                    FilePath = imagePath,
                    DisplayName = Path.GetFileName(imagePath),
                    Category = LargeModelImageCategory.OK
                }
            },
            ModelType = "DINOv2",
            Precision = "fp16",
            Device = device,
            InputWidth = 448,
            InputHeight = 224,
            AreaThreshold = 0
        };
    }

    private static string CreateTrainingImage(string root)
    {
        Directory.CreateDirectory(root);
        string imagePath = Path.Combine(root, "ok.png");
        File.WriteAllBytes(imagePath, new byte[] { 1, 2, 3 });
        return imagePath;
    }

    private static LargeModelTemplateManifest CreateManifest(string name)
    {
        return new LargeModelTemplateManifest
        {
            Version = 1,
            TemplateName = name,
            Device = "CPU",
            DeviceMode = 2,
            InputWidth = 448,
            InputHeight = 224
        };
    }

    private static byte[] CreateBytes(int length, int seed)
    {
        byte[] bytes = new byte[length];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)((seed + i) % 251);
        return bytes;
    }

    private static void AssertNoTemplateTemporaryFiles(string root, string message)
    {
        string[] temporaryFiles = Directory.GetFiles(root, "*.building.tdlarge", SearchOption.TopDirectoryOnly);
        if (temporaryFiles.Length != 0)
            throw new InvalidOperationException(message + " Count=" + temporaryFiles.Length + ".");
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

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "tdjs-native-lifecycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void AssertEqual(int expected, int actual, string message)
    {
        if (expected != actual)
            throw new InvalidOperationException(message + " Expected=" + expected + ", Actual=" + actual + ".");
    }

    private static void AssertEqual(IntPtr expected, IntPtr actual, string message)
    {
        if (expected != actual)
            throw new InvalidOperationException(message + " Expected=" + expected + ", Actual=" + actual + ".");
    }

    private static void AssertEqual(string expected, string actual, string message)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            throw new InvalidOperationException(message + " Expected=" + expected + ", Actual=" + actual + ".");
    }

    private static void AssertBytes(byte[] expected, byte[] actual, string message)
    {
        if (!BytesEqual(expected, actual))
            throw new InvalidOperationException(message);
    }

    private static bool BytesEqual(byte[] left, byte[] right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left == null || right == null || left.Length != right.Length)
            return false;
        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
                return false;
        }
        return true;
    }
}

internal sealed class BlockingTemplateSourceStreamFactory : ILargeModelTemplateSourceStreamFactory, IDisposable
{
    private readonly string _blockedPath;

    public BlockingTemplateSourceStreamFactory(string blockedPath)
    {
        _blockedPath = Path.GetFullPath(blockedPath);
        ReadEntered = new ManualResetEvent(false);
        AllowRead = new ManualResetEvent(false);
    }

    public ManualResetEvent ReadEntered { get; private set; }

    public ManualResetEvent AllowRead { get; private set; }

    public Stream OpenRead(string path)
    {
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (string.Equals(Path.GetFullPath(path), _blockedPath, StringComparison.OrdinalIgnoreCase))
            return new BlockingReadStream(stream, ReadEntered, AllowRead);
        return stream;
    }

    public void Dispose()
    {
        ReadEntered.Dispose();
        AllowRead.Dispose();
    }
}

internal sealed class ThrowingTemplateSourceStreamFactory : ILargeModelTemplateSourceStreamFactory
{
    private readonly string _throwingPath;

    public ThrowingTemplateSourceStreamFactory(string throwingPath)
    {
        _throwingPath = Path.GetFullPath(throwingPath);
    }

    public Stream OpenRead(string path)
    {
        if (string.Equals(Path.GetFullPath(path), _throwingPath, StringComparison.OrdinalIgnoreCase))
            return new ThrowingReadStream();
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }
}

internal sealed class BlockingReadStream : Stream
{
    private readonly Stream _inner;
    private readonly ManualResetEvent _readEntered;
    private readonly ManualResetEvent _allowRead;
    private bool _blocked;

    public BlockingReadStream(Stream inner, ManualResetEvent readEntered, ManualResetEvent allowRead)
    {
        _inner = inner;
        _readEntered = readEntered;
        _allowRead = allowRead;
    }

    public override bool CanRead { get { return _inner.CanRead; } }
    public override bool CanSeek { get { return _inner.CanSeek; } }
    public override bool CanWrite { get { return false; } }
    public override long Length { get { return _inner.Length; } }
    public override long Position { get { return _inner.Position; } set { _inner.Position = value; } }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (!_blocked)
        {
            _blocked = true;
            _readEntered.Set();
            if (!_allowRead.WaitOne(3000))
                throw new TimeoutException("The test did not release the controllable template source read.");
        }
        return _inner.Read(buffer, offset, count);
    }

    public override void Flush() { _inner.Flush(); }
    public override long Seek(long offset, SeekOrigin origin) { return _inner.Seek(offset, origin); }
    public override void SetLength(long value) { throw new NotSupportedException(); }
    public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class ThrowingReadStream : Stream
{
    public override bool CanRead { get { return true; } }
    public override bool CanSeek { get { return false; } }
    public override bool CanWrite { get { return false; } }
    public override long Length { get { return 1; } }
    public override long Position { get { return 0; } set { throw new NotSupportedException(); } }
    public override int Read(byte[] buffer, int offset, int count) { throw new IOException("template source failed"); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
    public override void SetLength(long value) { throw new NotSupportedException(); }
    public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
}

internal sealed class RecordingModelRebuilder : ILargeModelTrainingModelRebuilder
{
    private readonly string _runtimeRoot;
    private readonly string _prefix;
    private readonly List<string> _engineInputs = new List<string>();

    public RecordingModelRebuilder(string runtimeRoot, string prefix)
    {
        _runtimeRoot = runtimeRoot;
        _prefix = prefix;
    }

    public int OnnxCalls { get; private set; }

    public int EngineCalls { get; private set; }

    public string LastOnnxPath { get; private set; }

    public string LastEnginePath { get; private set; }

    public string ExpectedFirstOnnxPath
    {
        get { return Path.Combine(_runtimeRoot, _prefix + "-run1.onnx"); }
    }

    public string RebuildOnnxModel(
        string runtimeRoot,
        LargeModelTrainingRequest request,
        IProgress<string> log,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        OnnxCalls++;
        LastOnnxPath = Path.Combine(runtimeRoot, _prefix + "-run" + OnnxCalls + ".onnx");
        File.WriteAllBytes(LastOnnxPath, new byte[128]);
        return LastOnnxPath;
    }

    public string RebuildTensorRtEngine(
        string runtimeRoot,
        string onnxPath,
        LargeModelTrainingRequest request,
        IProgress<string> log,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        EngineCalls++;
        _engineInputs.Add(onnxPath);
        LastEnginePath = Path.Combine(runtimeRoot, _prefix + "-run" + EngineCalls + ".engine");
        File.WriteAllBytes(LastEnginePath, new byte[128]);
        return LastEnginePath;
    }

    public string GetSingleEngineInput()
    {
        if (_engineInputs.Count != 1)
            throw new InvalidOperationException("Expected exactly one Engine input, Actual=" + _engineInputs.Count + ".");
        return _engineInputs[0];
    }
}

internal sealed class RecordingDetectorFactory : ILargeModelDinov2DetectorFactory
{
    private readonly ILargeModelDinov2NativeApi _nativeApi;

    public RecordingDetectorFactory(ILargeModelDinov2NativeApi nativeApi)
    {
        _nativeApi = nativeApi;
    }

    public LargeModelDinov2Detector Create(string runtimeRoot)
    {
        return new LargeModelDinov2Detector(runtimeRoot, _nativeApi);
    }
}

internal sealed class RecordingNativeApi : ILargeModelDinov2NativeApi
{
    private readonly object _syncRoot = new object();
    private readonly Dictionary<IntPtr, string> _modelPaths = new Dictionary<IntPtr, string>();
    private readonly List<string> _trainedModelPaths = new List<string>();
    private int _nextHandle;

    public void Configure(string runtimeRoot)
    {
    }

    public IntPtr Init(string modelPath, string bankPath, int deviceMode)
    {
        lock (_syncRoot)
        {
            IntPtr handle = new IntPtr(++_nextHandle);
            _modelPaths[handle] = modelPath;
            return handle;
        }
    }

    public int Train(
        IntPtr handler,
        IntPtr okImage,
        string okPath,
        IntPtr ngImage,
        string ngPath,
        string saveBankPath,
        int appendMode)
    {
        lock (_syncRoot)
        {
            _trainedModelPaths.Add(_modelPaths[handler]);
        }
        File.WriteAllBytes(saveBankPath, new byte[] { 5, 6, 7, 8 });
        return 1;
    }

    public int Infer(
        IntPtr handler,
        ref LargeModelOpenCvImage image,
        float imageThreshold,
        int areaThreshold,
        string heatmapSaveDir,
        IntPtr outHeatmap,
        out float outScore,
        LargeModelRectBBox[] outBboxes,
        int maxBboxes,
        out int outNumBboxes)
    {
        outScore = 0;
        outNumBboxes = 0;
        return 0;
    }

    public void Release(IntPtr handler)
    {
        lock (_syncRoot)
        {
            _modelPaths.Remove(handler);
        }
    }

    public string GetSingleTrainedModelPath()
    {
        lock (_syncRoot)
        {
            if (_trainedModelPaths.Count != 1)
                throw new InvalidOperationException("Expected exactly one trained model path, Actual=" + _trainedModelPaths.Count + ".");
            return _trainedModelPaths[0];
        }
    }
}

internal sealed class RecordingTemplatePublisher : ILargeModelTemplatePublisher
{
    private readonly ILargeModelTemplatePublisher _inner;
    private readonly List<string> _modelPaths = new List<string>();

    public RecordingTemplatePublisher(ILargeModelTemplatePublisher inner)
    {
        _inner = inner;
    }

    public int Calls { get { return _modelPaths.Count; } }

    public void Publish(
        string templatePath,
        LargeModelTemplateManifest manifest,
        string modelPath,
        string bankPath,
        IProgress<string> log,
        CancellationToken token)
    {
        _modelPaths.Add(modelPath);
        _inner.Publish(templatePath, manifest, modelPath, bankPath, log, token);
    }

    public string GetSingleModelPath()
    {
        if (_modelPaths.Count != 1)
            throw new InvalidOperationException("Expected exactly one package model path, Actual=" + _modelPaths.Count + ".");
        return _modelPaths[0];
    }
}

internal sealed class CancelOnCompletionProgress : IProgress<int>
{
    private readonly CancellationTokenSource _cancellation;

    public CancelOnCompletionProgress(CancellationTokenSource cancellation)
    {
        _cancellation = cancellation;
    }

    public void Report(int value)
    {
        if (value >= 100)
            _cancellation.Cancel();
    }
}

internal sealed class FixedTrainingEnvironment : ILargeModelTrainingEnvironment
{
    private readonly string _runtimeRoot;
    private readonly string _modelRoot;

    public FixedTrainingEnvironment(string runtimeRoot, string modelRoot)
    {
        _runtimeRoot = runtimeRoot;
        _modelRoot = modelRoot;
    }

    public LargeModelRuntimeStatus ValidateRuntime()
    {
        return new LargeModelRuntimeStatus
        {
            IsReady = true,
            RootPath = _runtimeRoot,
            Message = "ready"
        };
    }

    public string EnsureModelRoot()
    {
        Directory.CreateDirectory(_modelRoot);
        return _modelRoot;
    }

    public string ResolveRuntimeRoot(string runtimeRoot)
    {
        return runtimeRoot;
    }
}

internal sealed class NeverModelRebuilder : ILargeModelTrainingModelRebuilder
{
    public string RebuildOnnxModel(
        string runtimeRoot,
        LargeModelTrainingRequest request,
        IProgress<string> log,
        CancellationToken token)
    {
        throw new InvalidOperationException("The path-boundary test must fail before ONNX rebuild.");
    }

    public string RebuildTensorRtEngine(
        string runtimeRoot,
        string onnxPath,
        LargeModelTrainingRequest request,
        IProgress<string> log,
        CancellationToken token)
    {
        throw new InvalidOperationException("The path-boundary test must fail before Engine rebuild.");
    }
}

internal sealed class NeverDetectorFactory : ILargeModelDinov2DetectorFactory
{
    public LargeModelDinov2Detector Create(string runtimeRoot)
    {
        throw new InvalidOperationException("The path-boundary test must fail before detector creation.");
    }
}

internal sealed class NeverTemplatePublisher : ILargeModelTemplatePublisher
{
    public void Publish(
        string templatePath,
        LargeModelTemplateManifest manifest,
        string modelPath,
        string bankPath,
        IProgress<string> log,
        CancellationToken token)
    {
        throw new InvalidOperationException("The path-boundary test must fail before template publication.");
    }
}

internal sealed class BlockingNativeApi : ILargeModelDinov2NativeApi, IDisposable
{
    private int _initCalls;
    private int _trainCalls;
    private int _releaseCalls;
    private IntPtr _lastReleasedHandle;

    public BlockingNativeApi()
    {
        InitEntered = new ManualResetEvent(false);
        AllowInitReturn = new ManualResetEvent(false);
    }

    public ManualResetEvent InitEntered { get; private set; }

    public ManualResetEvent AllowInitReturn { get; private set; }

    public int InitCalls { get { return _initCalls; } }

    public int TrainCalls { get { return _trainCalls; } }

    public int ReleaseCalls { get { return _releaseCalls; } }

    public IntPtr LastReleasedHandle { get { return _lastReleasedHandle; } }

    public void Configure(string runtimeRoot)
    {
    }

    public IntPtr Init(string modelPath, string bankPath, int deviceMode)
    {
        Interlocked.Increment(ref _initCalls);
        InitEntered.Set();
        if (!AllowInitReturn.WaitOne(3000))
            throw new TimeoutException("The test did not release the controllable native Init call.");
        return new IntPtr(0x1234);
    }

    public int Train(
        IntPtr handler,
        IntPtr okImage,
        string okPath,
        IntPtr ngImage,
        string ngPath,
        string saveBankPath,
        int appendMode)
    {
        Interlocked.Increment(ref _trainCalls);
        File.WriteAllBytes(saveBankPath, new byte[] { 9 });
        return 1;
    }

    public int Infer(
        IntPtr handler,
        ref LargeModelOpenCvImage image,
        float imageThreshold,
        int areaThreshold,
        string heatmapSaveDir,
        IntPtr outHeatmap,
        out float outScore,
        LargeModelRectBBox[] outBboxes,
        int maxBboxes,
        out int outNumBboxes)
    {
        outScore = 0;
        outNumBboxes = 0;
        return 0;
    }

    public void Release(IntPtr handler)
    {
        _lastReleasedHandle = handler;
        Interlocked.Increment(ref _releaseCalls);
    }

    public void Dispose()
    {
        InitEntered.Dispose();
        AllowInitReturn.Dispose();
    }
}
}
'@

$compileRoot = Join-Path ([IO.Path]::GetTempPath()) ("tdjs-production-behavior-compile-" + [Guid]::NewGuid().ToString("N"))
[void][IO.Directory]::CreateDirectory($compileRoot)
$sourcePath = Join-Path $compileRoot "ProductionBehaviorHarness.cs"
$assemblyPath = Join-Path $compileRoot "ProductionBehaviorHarness.dll"
$frameworkRoot = [Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()
[IO.File]::WriteAllText(
    $sourcePath,
    $testHarness,
    (New-Object Text.UTF8Encoding($true)))

$compilerArguments = @(
    "/nologo",
    "/target:library",
    "/langversion:latest",
    "/out:$assemblyPath",
    "/reference:$($frameworkRoot)System.dll",
    "/reference:$($frameworkRoot)System.Core.dll",
    "/reference:$($frameworkRoot)System.Drawing.dll",
    "/reference:$($frameworkRoot)System.IO.Compression.dll",
    "/reference:$($frameworkRoot)System.IO.Compression.FileSystem.dll",
    "/reference:$openCvPath",
    "/reference:$newtonsoftPath"
) + $productionSourcePaths + @($sourcePath)
$compilerOutput = & $cscPath $compilerArguments 2>&1
if ($LASTEXITCODE -ne 0) {
    throw ($compilerOutput -join [Environment]::NewLine)
}

[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes($assemblyPath))
Remove-Item -LiteralPath $compileRoot -Recurse -Force

switch ($Case) {
    "NativeLifecycle" { [LargeModelTrainingProductionBehaviorTests.LargeModelTrainingProductionBehaviorHarness]::RunNativeLifecycle() }
    "PythonIsolation" { [LargeModelTrainingProductionBehaviorTests.LargeModelTrainingProductionBehaviorHarness]::RunPythonIsolation() }
    "PathBoundary" { [LargeModelTrainingProductionBehaviorTests.LargeModelTrainingProductionBehaviorHarness]::RunPathBoundary() }
    "TemplatePublishing" { [LargeModelTrainingProductionBehaviorTests.LargeModelTrainingProductionBehaviorHarness]::RunTemplatePublishing() }
    "ProductionEntry" { [LargeModelTrainingProductionBehaviorTests.LargeModelTrainingProductionBehaviorHarness]::RunProductionEntry() }
    "All" {
        [LargeModelTrainingProductionBehaviorTests.LargeModelTrainingProductionBehaviorHarness]::RunNativeLifecycle()
        [LargeModelTrainingProductionBehaviorTests.LargeModelTrainingProductionBehaviorHarness]::RunPythonIsolation()
        [LargeModelTrainingProductionBehaviorTests.LargeModelTrainingProductionBehaviorHarness]::RunPathBoundary()
        [LargeModelTrainingProductionBehaviorTests.LargeModelTrainingProductionBehaviorHarness]::RunTemplatePublishing()
        [LargeModelTrainingProductionBehaviorTests.LargeModelTrainingProductionBehaviorHarness]::RunProductionEntry()
    }
}

Write-Host "Large-model production behavior checks passed: $Case."
