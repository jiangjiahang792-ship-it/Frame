$ErrorActionPreference = 'Stop'

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

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

function Assert-NotContains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if ($Text.Contains($Pattern)) {
        throw $Message
    }
}

$root = Split-Path -Parent $PSScriptRoot
$outputImageSource = Get-Content -LiteralPath (Join-Path $root 'Node\1-Acquisition\ImageSource\NodeResultImageSource.cs') -Raw -Encoding UTF8
$resourceManagerSource = Get-Content -LiteralPath (Join-Path $root 'Node\NodeResultResourceManager.cs') -Raw -Encoding UTF8
$nodeBaseSource = Get-Content -LiteralPath (Join-Path $root 'Node\NodeBase.cs') -Raw -Encoding UTF8
$nodeBaseDesignerSource = Get-Content -LiteralPath (Join-Path $root 'Node\NodeBase.Designer.cs') -Raw -Encoding UTF8
$solutionSource = Get-Content -LiteralPath (Join-Path $root 'Solution.cs') -Raw -Encoding UTF8
$imageSourceNode = Get-Content -LiteralPath (Join-Path $root 'Node\1-Acquisition\ImageSource\NodeImageSource.cs') -Raw -Encoding UTF8
$cropNode = Get-Content -LiteralPath (Join-Path $root 'Node\2-ImagePreprocessing\ImageCrop\NodeImageCrop.cs') -Raw -Encoding UTF8
$splitNode = Get-Content -LiteralPath (Join-Path $root 'Node\2-ImagePreprocessing\ImageSplit\NodeImageSplit.cs') -Raw -Encoding UTF8
$rotateNode = Get-Content -LiteralPath (Join-Path $root 'Node\2-ImagePreprocessing\ImageRotate\NodeImageRotate.cs') -Raw -Encoding UTF8
$saveNode = Get-Content -LiteralPath (Join-Path $root 'Node\7-ResultProcessing\ImageSave\NodeSaveImage.cs') -Raw -Encoding UTF8
$batteryEarNode = Get-Content -LiteralPath (Join-Path $root 'Node\3-Detection\BatteryEar\NodeBatteryEar.cs') -Raw -Encoding UTF8
$binaryAnalysisNode = Get-Content -LiteralPath (Join-Path $root 'Node\3-Detection\BinaryAnalysis\NodeBinaryAnalysis.cs') -Raw -Encoding UTF8
$findLineNode = Get-Content -LiteralPath (Join-Path $root 'Node\3-Detection\FindLine\NodeFIndLine.cs') -Raw -Encoding UTF8
$findCircleNode = Get-Content -LiteralPath (Join-Path $root 'Node\3-Detection\FindCircle\NodeFIndCircle.cs') -Raw -Encoding UTF8
$batteryEarForm = Get-Content -LiteralPath (Join-Path $root 'Node\3-Detection\BatteryEar\NodeParamFormBatteryEar.cs') -Raw -Encoding UTF8
$binaryAnalysisForm = Get-Content -LiteralPath (Join-Path $root 'Node\3-Detection\BinaryAnalysis\NodeParamFormBinaryAnalysis.cs') -Raw -Encoding UTF8
$findLineForm = Get-Content -LiteralPath (Join-Path $root 'Node\3-Detection\FindLine\NodeParamFormFindLine.cs') -Raw -Encoding UTF8
$findCircleForm = Get-Content -LiteralPath (Join-Path $root 'Node\3-Detection\FindCircle\NodeParamFormFindCircle.cs') -Raw -Encoding UTF8
$tdaiNode = Get-Content -LiteralPath (Join-Path $root 'Node\3-Detection\TDAI\NodeTDAI.cs') -Raw -Encoding UTF8
$blobNode = Get-Content -LiteralPath (Join-Path $root 'Node\4-Measurement\BlobAnalysis\NodeBlobAnalysis.cs') -Raw -Encoding UTF8
$blobForm = Get-Content -LiteralPath (Join-Path $root 'Node\4-Measurement\BlobAnalysis\NodeParamFormBlobAnalysis.cs') -Raw -Encoding UTF8
$imageDrawNode = Get-Content -LiteralPath (Join-Path $root 'Node\7-ResultProcessing\ImageDraw\NodeImageDraw.cs') -Raw -Encoding UTF8
$imageDrawForm = Get-Content -LiteralPath (Join-Path $root 'Node\7-ResultProcessing\ImageDraw\NodeParamFormImageDraw.cs') -Raw -Encoding UTF8
$overlayDrawNode = Get-Content -LiteralPath (Join-Path $root 'Node\7-ResultProcessing\ResultOverlayDraw\NodeResultOverlayDraw.cs') -Raw -Encoding UTF8
$overlayDraw2Node = Get-Content -LiteralPath (Join-Path $root 'Node\7-ResultProcessing\ResultOverlayDraw2\NodeResultOverlayDraw2.cs') -Raw -Encoding UTF8
$lineMergeNode = Get-Content -LiteralPath (Join-Path $root 'Node\8-GeometryCreation\LineMergeFit\NodeLineMergeFit.cs') -Raw -Encoding UTF8
$lineMergeForm = Get-Content -LiteralPath (Join-Path $root 'Node\8-GeometryCreation\LineMergeFit\NodeParamFormLineMergeFit.cs') -Raw -Encoding UTF8
$matchTemplateNode = Get-Content -LiteralPath (Join-Path $root 'Node\3-Detection\MatchTemplate\NodeMatchTemplate.cs') -Raw -Encoding UTF8
$nccMatchTemplateNode = Get-Content -LiteralPath (Join-Path $root 'Node\3-Detection\MatchTemplate\NodeNccMatchTemplate.cs') -Raw -Encoding UTF8
$matchTemplateForm = Get-Content -LiteralPath (Join-Path $root 'Node\3-Detection\MatchTemplate\NodeParamFormMatchTemplate.cs') -Raw -Encoding UTF8

Assert-Contains $nodeBaseSource 'Interlocked.Exchange(ref _result, value)' 'NodeBase.Result必须通过原子替换发布新结果。'
Assert-Contains $nodeBaseSource 'NodeResultResourceManager.Release(previousResult);' 'NodeBase.Result替换后必须释放旧结果资源。'
Assert-Contains $nodeBaseDesignerSource 'ReleaseResultResources();' '节点Dispose必须释放最后一次结果资源。'
Assert-Contains $solutionSource 'node.ReleaseResultResources();' '方案删除流程或重置时必须释放节点结果资源。'
Assert-Contains $imageSourceNode 'OutputImage.FromBorrowedSingleImage(mat)' '共享变量Mat必须按借用方式包装。'
Assert-Contains $imageSourceNode 'OutputImage.FromOwnedSingleImage(bitmap.ToMat())' '共享变量Bitmap转Mat后必须登记自产所有权。'
Assert-Contains $cropNode '.TakeOwnership(roiImg)' '裁切节点必须拥有自产ROI。'
Assert-Contains $cropNode '.TakeOwnership(firstGrayCrop)' '裁切节点必须拥有自产灰度缓存。'
Assert-Contains $cropNode '.TakeDependency(inputImage)' '裁切节点必须把借用源图的租约传回上游。'
Assert-Contains $splitNode '.TakeOwnership(imgs)' '分割节点必须拥有自产子图。'
Assert-Contains $splitNode '.TakeOwnership(firstGraySplit)' '分割节点必须拥有自产灰度缓存。'
Assert-Contains $splitNode '.TakeDependency(inputImage)' '分割节点必须把借用源图的租约传回上游。'
Assert-Contains $rotateNode 'TakeOwnership(rotatedImage)' '旋转节点必须在创建彩图后立即登记所有权。'
Assert-Contains $saveNode 'outputImage.GetRetainedImageBytesEstimateForAdmission()' '保存任务必须使用完整图像依赖链的缓存字节估算。'
Assert-NotContains $saveNode 'IImageResourceLease candidateLease = outputImage.AcquireLease();' '保存节点不能在队列确认接收前取得Mat租约。'
Assert-Contains $saveNode '() => outputImage.AcquireSaveSnapshot()' '队列确认接收后必须原子取得后台Mat与租约快照。'
Assert-Contains $outputImageSource 'GetRetainedImageBytesEstimateForAdmission()' '保存预判必须使用输出构建阶段缓存的字节估算。'
Assert-Contains $outputImageSource 'OutputImageSaveSnapshot AcquireSaveSnapshot()' 'OutputImage必须在同一资源锁内取得保存源图和租约。'
Assert-Contains $outputImageSource 'CollectRetainedImageBytes(this, visitedOutputs, visitedImages)' '原子保存快照必须在租约保护下重算实际资源字节。'
Assert-Contains $outputImageSource 'HasDependencyPath(owner, this)' '登记父级租约前必须拒绝直接或传递循环依赖。'
Assert-Contains $outputImageSource 'Interlocked.Read(ref _retainedImageBytesEstimate)' '读取父级缓存不能在子级锁内继续取得父级资源锁。'
Assert-Contains $saveNode 'DisposeSaveTaskNoThrow(saveImagedata, "保存工作任务")' '保存后台完成或异常后必须隔离异常并释放任务租约。'
Assert-Contains $saveNode '_imageQueue.CompleteAndDisposePending();' '保存队列停止超时后必须释放全部待处理任务租约。'
Assert-Contains $batteryEarNode 'pendingOutputImage.TakeOwnership(generatedImage);' '电池耳节点必须接管自产结果图。'
Assert-Contains $batteryEarNode 'Result = nodeResult;' '电池耳节点必须发布新的完整结果对象。'
Assert-Contains $binaryAnalysisNode 'pendingOutputImage.TakeOwnership(outputMat);' '二值分析节点必须接管Bitmap转出的Mat。'
Assert-Contains $binaryAnalysisNode 'analysisBitmap?.Dispose();' '二值分析返回的临时Bitmap必须在所有路径释放。'
Assert-Contains $findLineNode 'pendingOutputImage.TakeDependency(outputImage);' '找线节点借用上游Mat时必须持有父级租约。'
Assert-Contains $findCircleNode 'pendingOutputImage.TakeDependency(outputImage);' '找圆节点借用上游Mat时必须持有父级租约。'
Assert-True (-not $findLineNode.Contains('outputImage.Bitmaps[0].Clone()')) '找线节点不得继续为显示结果克隆整张上游图。'
Assert-True (-not $findCircleNode.Contains('outputImage.Bitmaps[0].Clone()')) '找圆节点不得继续为显示结果克隆整张上游图。'
Assert-Contains $batteryEarForm 'foreach (Mat roiImage in roiImagesToDispose)' '电池耳算法必须释放ROI控件返回的全部临时Mat。'
Assert-Contains $batteryEarForm 'using (Mat result = new Mat())' '电池耳模板匹配结果Mat必须按方法作用域释放。'
Assert-Contains $binaryAnalysisForm 'bitmap?.Dispose();' '二值分析的执行与添加区域预览必须释放返回Bitmap。'
Assert-Contains $findLineForm 'foreach (Mat roiImage in roiImages)' '找线参数窗体必须释放全部临时ROI Mat。'
Assert-Contains $findCircleForm 'foreach (Mat roiImage in roiImages)' '找圆参数窗体必须释放全部临时ROI Mat。'
Assert-Contains $findLineForm 'ReplacePictureBoxImage(pictureBoxCanny, BitmapConverter.ToBitmap(edges));' '找线预览替换时必须释放旧GDI图像。'
Assert-Contains $findCircleForm 'ReplacePictureBoxImage(pictureBoxCanny, BitmapConverter.ToBitmap(edges));' '找圆预览替换时必须释放旧GDI图像。'
Assert-Contains $findLineForm 'public (LineSegmentPoint line, bool succeeded) DetectLine()' '找线检测必须使用轻量成功标志，不能为未使用返回值生成Bitmap。'
Assert-Contains $findCircleForm 'public (CircleSegment circle, bool succeeded) DetectCircle()' '找圆检测必须使用轻量成功标志，不能为未使用返回值生成Bitmap。'
Assert-True (-not $findLineForm.Contains('BitmapConverter.ToBitmap(cleanOutput)')) '找线检测不得恢复未使用的Mat转Bitmap。'
Assert-True (-not $findCircleForm.Contains('BitmapConverter.ToBitmap(cleanOutput)')) '找圆检测不得恢复未使用的Mat转Bitmap。'
Assert-Contains $tdaiNode 'roiOutput.TakeOwnership(roiImages);' 'AI多ROI临时输出必须接管全部裁剪Mat。'
Assert-Contains $tdaiNode 'roiOutput.TakeDependency(inputImage);' 'AI多ROI临时输出必须持有上游父级租约。'
Assert-Contains $tdaiNode '!ReferenceEquals(inputImage, subscribedInputImage)' 'AI推理退出时只能释放本轮创建的临时ROI输出。'
Assert-Contains $tdaiNode 'using (Mat img = inputImage.Bitmaps[i].Clone())' 'OBB推理克隆图必须在单张推理结束后释放。'
Assert-Contains $blobNode 'GetGrayImage(OutputImage inputImage, out bool ownsImage)' 'Blob取灰度图必须明确返回释放责任。'
Assert-Contains $blobNode 'temporaryGray?.Dispose();' 'Blob正式运行必须释放本轮临时灰度图。'
Assert-Contains $blobForm 'NodeResultResourceManager.Release(result);' 'Blob参数预览必须回收未发布的结果图。'
Assert-Contains $imageDrawForm 'GetImage(out OutputImage owner)' '图像绘制取上游Mat时必须同时返回资源拥有者。'
Assert-Contains $imageDrawNode 'inputLease = inputOwner.AcquireLease();' '图像绘制使用上游Mat期间必须持有短租约。'
Assert-Contains $imageDrawNode 'pendingOutputImage.TakeOwnership(outputMat);' '图像绘制生成的输出Mat必须登记自产所有权。'
Assert-Contains $imageDrawNode 'Result = nodeResult;' '图像绘制必须以新结果对象原子发布本轮输出。'
Assert-Contains $overlayDrawNode 'borrowedOutput.TakeDependency(inputImage);' '结果绘制借用上游Mat时必须持有父级租约。'
Assert-Contains $overlayDraw2Node 'borrowedOutput.TakeDependency(inputImage);' '结果绘制2借用上游Mat时必须持有父级租约。'
Assert-Contains $overlayDrawNode 'var nodeResult = new NodeResultResultOverlayDraw' '结果绘制每轮必须发布新的完整结果对象。'
Assert-Contains $overlayDraw2Node 'NodeResultResultOverlayDraw2 nodeResult = new NodeResultResultOverlayDraw2' '结果绘制2每轮必须发布新的完整结果对象。'
Assert-Contains $lineMergeForm 'output = needPreviewImage ? CloneFirstAvailableOutputImage() : null;' '线组合拟合正式运行不得创建无意义的空Mat。'
Assert-Contains $lineMergeForm 'output?.Dispose();' '线组合拟合预览输出必须在异常路径释放。'
Assert-Contains $lineMergeNode 'image.TakeOwnership(output);' '线组合拟合存在预览输出时必须登记Mat所有权。'
Assert-Contains $lineMergeNode 'NodeResultResourceManager.Release(pendingResult);' '线组合拟合发布失败时必须回收待发布结果。'
Assert-Contains $matchTemplateNode 'borrowedOutput.TakeDependency(borrowedOwner);' 'NCC模板匹配借用上游Mat时必须持有父级租约。'
Assert-Contains $matchTemplateNode 'ownedOutput.TakeOwnership(convertedMat);' '普通模板匹配Bitmap转Mat后必须登记自产所有权。'
Assert-Contains $matchTemplateNode 'matchResult?.OutputBitmap?.Dispose();' '模板匹配正式运行必须释放算法返回的临时Bitmap。'
Assert-Contains $matchTemplateNode 'NodeResultResourceManager.Release(pendingResult);' '模板匹配发布失败时必须回收待发布结果。'
Assert-Contains $matchTemplateForm 'outputLease = outputOwner.AcquireLease();' 'NCC计算读取上游Mat期间必须先取得短租约。'
Assert-Contains $matchTemplateForm 'outputLease?.Dispose();' 'NCC取图失败时必须释放已经取得的短租约。'
Assert-Contains $nccMatchTemplateNode 'inputLease?.Dispose();' 'NCC结果接管父级依赖后必须释放计算阶段短租约。'
Assert-Contains $nccMatchTemplateNode 'outputMat, outputGrayMat, outputOwner' 'NCC构建结果时必须传递真实上游资源拥有者。'

$stubs = @'
namespace OpenCvSharp
{
    public enum ColorConversionCodes
    {
        BGR2GRAY,
        BGRA2GRAY
    }

    public struct Rect
    {
    }

    public sealed class Mat : IDisposable
    {
        public Mat()
        {
            ChannelCount = 3;
            Rows = 10;
            StepBytes = 30;
        }

        public int ChannelCount { get; set; }
        public int Rows { get; set; }
        public long StepBytes { get; set; }
        public bool IsEmpty { get; set; }
        public int DisposeCount { get; private set; }

        public bool Empty()
        {
            return IsEmpty;
        }

        public int Channels()
        {
            return ChannelCount;
        }

        public long Step()
        {
            return StepBytes;
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    public static class Cv2
    {
        public static void CvtColor(Mat source, Mat destination, ColorConversionCodes conversion)
        {
            if (source == null || destination == null)
                throw new ArgumentNullException();

            destination.ChannelCount = 1;
            destination.Rows = source.Rows;
            destination.StepBytes = Math.Max(1, source.StepBytes / Math.Max(1, source.ChannelCount));
            destination.IsEmpty = false;
        }
    }
}

namespace TDJS_Vision.Node
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class SubscriptionOutputAttribute : Attribute
    {
    }

    public interface INodeResult
    {
        int RunTime { get; set; }
    }
}

namespace TDJS_Vision.Node._3_Detection.TDAI
{
    public sealed class AlgorithmResult
    {
    }
}

namespace TDJS_Vision.Tests
{
    using TDJS_Vision.Node;
    using TDJS_Vision.Node._1_Acquisition.ImageSource;

    public sealed class DuplicateImageResult : INodeResult
    {
        public int RunTime { get; set; }
        public OutputImage First { get; set; }
        public OutputImage Second { get; set; }
    }

    public sealed class ExplicitResourceResult : INodeResult, INodeResultResourceOwner
    {
        public int RunTime { get; set; }
        public int ReleaseCount { get; private set; }

        public void ReleaseResources()
        {
            ReleaseCount++;
        }
    }

    public sealed class CountingDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    public static class ImageResourceStressRunner
    {
        public static int Run(int cycles)
        {
            int disposeCount = 0;
            for (int i = 0; i < cycles; i++)
            {
                OpenCvSharp.Mat image = new OpenCvSharp.Mat { ChannelCount = 1 };
                OutputImage output = OutputImage.FromOwnedSingleImage(image, image);
                IImageResourceLease lease = output.AcquireLease();
                output.Dispose();
                lease.Dispose();
                output.Dispose();
                disposeCount += image.DisposeCount;
            }

            return disposeCount;
        }

        public static int RunSnapshotDisposeRace(int cycles)
        {
            int disposeCount = 0;
            for (int i = 0; i < cycles; i++)
            {
                OpenCvSharp.Mat image = new OpenCvSharp.Mat { ChannelCount = 1 };
                OutputImage output = OutputImage.FromOwnedSingleImage(image, image);
                ManualResetEventSlim start = new ManualResetEventSlim(false);
                Exception unexpectedException = null;
                Task acquireTask = Task.Run(() =>
                {
                    start.Wait();
                    try
                    {
                        using (OutputImage.OutputImageSaveSnapshot snapshot = output.AcquireSaveSnapshot())
                        {
                            if (!OutputImage.HasValidImage(snapshot.SourceImage))
                                throw new InvalidOperationException("并发快照取得了无效图像。");
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Dispose先取得资源锁时，快照按契约拒绝即可。
                    }
                    catch (Exception ex)
                    {
                        unexpectedException = ex;
                    }
                });
                Task disposeTask = Task.Run(() =>
                {
                    start.Wait();
                    output.Dispose();
                });

                start.Set();
                Task.WaitAll(acquireTask, disposeTask);
                start.Dispose();
                output.Dispose();
                if (unexpectedException != null)
                    throw new InvalidOperationException("快照与释放并发测试失败。", unexpectedException);
                if (output.ActiveLeaseCount != 0 || !output.AreOwnedResourcesReleased || image.DisposeCount != 1)
                    throw new InvalidOperationException("快照与释放竞争后资源未完整归零。");

                disposeCount += image.DisposeCount;
            }

            return disposeCount;
        }

        public static int RunConcurrentReverseDependencyRace(int cycles)
        {
            int completedCycles = 0;
            for (int i = 0; i < cycles; i++)
            {
                OpenCvSharp.Mat firstImage = new OpenCvSharp.Mat { ChannelCount = 1 };
                OpenCvSharp.Mat secondImage = new OpenCvSharp.Mat { ChannelCount = 1 };
                OutputImage firstOutput = OutputImage.FromOwnedSingleImage(firstImage, firstImage);
                OutputImage secondOutput = OutputImage.FromOwnedSingleImage(secondImage, secondImage);
                ManualResetEventSlim start = new ManualResetEventSlim(false);
                int acceptedCount = 0;
                int rejectedCount = 0;
                Exception unexpectedException = null;

                Task firstTask = Task.Run(() =>
                {
                    start.Wait();
                    try
                    {
                        firstOutput.TakeDependency(secondOutput);
                        Interlocked.Increment(ref acceptedCount);
                    }
                    catch (InvalidOperationException)
                    {
                        Interlocked.Increment(ref rejectedCount);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.CompareExchange(ref unexpectedException, ex, null);
                    }
                });
                Task secondTask = Task.Run(() =>
                {
                    start.Wait();
                    try
                    {
                        secondOutput.TakeDependency(firstOutput);
                        Interlocked.Increment(ref acceptedCount);
                    }
                    catch (InvalidOperationException)
                    {
                        Interlocked.Increment(ref rejectedCount);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.CompareExchange(ref unexpectedException, ex, null);
                    }
                });

                start.Set();
                bool finished = Task.WaitAll(new[] { firstTask, secondTask }, 5000);
                start.Dispose();
                if (!finished)
                    throw new TimeoutException("并发反向依赖检查发生死锁。");
                if (unexpectedException != null)
                    throw new InvalidOperationException("并发反向依赖检查发生非预期异常。", unexpectedException);
                if (acceptedCount != 1 || rejectedCount != 1)
                    throw new InvalidOperationException("并发反向依赖必须恰好接收一条边并拒绝一条边。");

                firstOutput.Dispose();
                secondOutput.Dispose();
                if (firstOutput.ActiveLeaseCount != 0 || secondOutput.ActiveLeaseCount != 0 ||
                    firstImage.DisposeCount != 1 || secondImage.DisposeCount != 1)
                {
                    throw new InvalidOperationException("并发反向依赖测试结束后资源未完整归零。");
                }

                completedCycles++;
            }

            return completedCycles;
        }
    }
}
'@

$commonUsings = @'
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
'@
$outputImageBody = [System.Text.RegularExpressions.Regex]::Replace($outputImageSource, '(?m)^using\s+[^;]+;\s*\r?\n', '')
$resourceManagerBody = [System.Text.RegularExpressions.Regex]::Replace($resourceManagerSource, '(?m)^using\s+[^;]+;\s*\r?\n', '')
Add-Type -TypeDefinition ($commonUsings + [Environment]::NewLine + $outputImageBody + [Environment]::NewLine + $resourceManagerBody + [Environment]::NewLine + $stubs) -Language CSharp

$colorSource = New-Object OpenCvSharp.Mat
$ownedOutput = [TDJS_Vision.Node._1_Acquisition.ImageSource.OutputImage]::FromOwnedSingleImage($colorSource, $null)
$generatedGray = $ownedOutput.GrayImg
Assert-True ($ownedOutput.OwnedImageCount -eq 2) '彩色自产图应拥有源图和生成的灰度图。'

$lease = $ownedOutput.AcquireLease()
$ownedOutput.Dispose()
Assert-True ($ownedOutput.IsDisposeRequested) 'Dispose后必须进入释放请求状态。'
Assert-True (-not $ownedOutput.AreOwnedResourcesReleased) '存在租约时不得提前释放自产Mat。'
Assert-True ($colorSource.DisposeCount -eq 0 -and $generatedGray.DisposeCount -eq 0) '租约持有期间源图和灰度图都不能释放。'

$lease.Dispose()
$lease.Dispose()
$ownedOutput.Dispose()
Assert-True ($ownedOutput.AreOwnedResourcesReleased) '最后一个租约释放后必须完成最终回收。'
Assert-True ($colorSource.DisposeCount -eq 1 -and $generatedGray.DisposeCount -eq 1) '每个自产Mat必须且只能释放一次。'

$lifetimeImage = New-Object OpenCvSharp.Mat
$lifetimeImage.ChannelCount = 1
$externalLifetime = New-Object TDJS_Vision.Tests.CountingDisposable
$lifetimeOutput = [TDJS_Vision.Node._1_Acquisition.ImageSource.OutputImage]::FromOwnedSingleImageWithLifetime(
    $lifetimeImage,
    $externalLifetime,
    $lifetimeImage)
$null = $lifetimeOutput.TakeExternalLifetime($externalLifetime)
$lifetimeLease = $lifetimeOutput.AcquireLease()
$lifetimeOutput.Dispose()
Assert-True ($lifetimeOutput.OwnedExternalLifetimeCount -eq 1) '重复登记同一外部生命周期必须按引用去重。'
Assert-True ($externalLifetime.DisposeCount -eq 0) '图像仍有下游租约时不得提前归还相机字节预算。'
$lifetimeLease.Dispose()
Assert-True ($lifetimeImage.DisposeCount -eq 1) '最后一个图像租约释放后必须释放自产Mat。'
Assert-True ($externalLifetime.DisposeCount -eq 1) '最后一个图像租约释放后必须且只能归还一次外部生命周期。'
Assert-True ($lifetimeOutput.OwnedExternalLifetimeCount -eq 0) '最终回收后外部生命周期集合必须归零。'

$graySource = New-Object OpenCvSharp.Mat
$graySource.ChannelCount = 1
$deduplicatedOutput = [TDJS_Vision.Node._1_Acquisition.ImageSource.OutputImage]::FromOwnedSingleImage($graySource, $graySource)
Assert-True ($deduplicatedOutput.OwnedImageCount -eq 1) 'SrcImg、GrayImg和Bitmaps引用同一Mat时必须去重。'
$deduplicatedOutput.Dispose()
Assert-True ($graySource.DisposeCount -eq 1) '同一Mat出现在多个字段时只能释放一次。'

$borrowedSource = New-Object OpenCvSharp.Mat
$borrowedGray = New-Object OpenCvSharp.Mat
$borrowedGray.ChannelCount = 1
$borrowedOutput = [TDJS_Vision.Node._1_Acquisition.ImageSource.OutputImage]::FromBorrowedSingleImage($borrowedSource, $borrowedGray)
Assert-True ($borrowedOutput.OwnedImageCount -eq 0) '借用源图和借用灰度图不能被登记为自产资源。'
$borrowedOutput.Dispose()
Assert-True ($borrowedSource.DisposeCount -eq 0 -and $borrowedGray.DisposeCount -eq 0) '释放借用输出不能释放上游Mat。'

$borrowedColor = New-Object OpenCvSharp.Mat
$borrowedWithLocalGray = [TDJS_Vision.Node._1_Acquisition.ImageSource.OutputImage]::FromBorrowedSingleImage($borrowedColor, $null)
$localGray = $borrowedWithLocalGray.GrayImg
Assert-True ($borrowedWithLocalGray.OwnedImageCount -eq 1) '借用彩图按需生成的灰度缓存应由当前输出拥有。'
$borrowedWithLocalGray.Dispose()
Assert-True ($borrowedColor.DisposeCount -eq 0 -and $localGray.DisposeCount -eq 1) '借用彩图不能释放，但自产灰度缓存必须释放。'

$parentMat = New-Object OpenCvSharp.Mat
$parentMat.ChannelCount = 1
$parentMat.Rows = 4
$parentMat.StepBytes = 10
$parentOutput = [TDJS_Vision.Node._1_Acquisition.ImageSource.OutputImage]::FromOwnedSingleImage($parentMat, $parentMat)
$childOutput = [TDJS_Vision.Node._1_Acquisition.ImageSource.OutputImage]::FromBorrowedSingleImage($parentOutput, $parentMat, $parentMat)
$childLease = $childOutput.AcquireLease()
Assert-True ($parentOutput.ActiveLeaseCount -eq 1 -and $childOutput.OwnedDependencyCount -eq 1) '借用输出必须持有一份父级资源租约。'
Assert-True ($childOutput.EstimateRetainedImageBytes() -eq 40) '完整租约字节估算必须沿父级依赖链统计，并对重复Mat引用去重。'
$parentOutput.Dispose()
$childOutput.Dispose()
Assert-True ($parentMat.DisposeCount -eq 0) '下游租约未释放时，父级自产Mat不能提前释放。'
$childLease.Dispose()
Assert-True ($parentMat.DisposeCount -eq 1) '下游最后一个租约释放后，父级租约链必须完成回收。'

$estimateParentMat = New-Object OpenCvSharp.Mat
$estimateParentMat.ChannelCount = 1
$estimateParentMat.Rows = 4
$estimateParentMat.StepBytes = 10
$estimateChildMat = New-Object OpenCvSharp.Mat
$estimateChildMat.ChannelCount = 1
$estimateChildMat.Rows = 3
$estimateChildMat.StepBytes = 7
$estimateParentOutput = [TDJS_Vision.Node._1_Acquisition.ImageSource.OutputImage]::FromOwnedSingleImage($estimateParentMat, $estimateParentMat)
$estimateChildOutput = [TDJS_Vision.Node._1_Acquisition.ImageSource.OutputImage]::FromOwnedSingleImage($estimateChildMat, $estimateChildMat)
$null = $estimateChildOutput.TakeDependency($estimateParentOutput)
Assert-True ($estimateChildOutput.EstimateRetainedImageBytes() -eq 61) '字节估算必须同时包含当前输出自产Mat与父级依赖Mat。'
$estimateParentOutput.Dispose()
$estimateChildOutput.Dispose()
Assert-True ($estimateParentMat.DisposeCount -eq 1 -and $estimateChildMat.DisposeCount -eq 1) '估算依赖链测试结束后上下游Mat必须各释放一次。'

$mutableOriginal = New-Object OpenCvSharp.Mat
$mutableOriginal.ChannelCount = 1
$mutableOriginal.Rows = 1
$mutableOriginal.StepBytes = 10
$mutableOutput = [TDJS_Vision.Node._1_Acquisition.ImageSource.OutputImage]::FromOwnedSingleImage($mutableOriginal, $mutableOriginal)
$cachedBeforeMutation = $mutableOutput.GetRetainedImageBytesEstimateForAdmission()
$mutableReplacement = New-Object OpenCvSharp.Mat
$mutableReplacement.ChannelCount = 1
$mutableReplacement.Rows = 1000
$mutableReplacement.StepBytes = 10
$mutableOutput.Bitmaps[0] = $mutableReplacement
$mutableSnapshot = $mutableOutput.AcquireSaveSnapshot()
try {
    Assert-True ($cachedBeforeMutation -eq 10) '可变列表测试的初始缓存估算必须为10字节。'
    Assert-True ($mutableSnapshot.EstimatedBytes -eq 10010) '原地替换Bitmaps后，接纳快照必须按租约保护下的实际Mat重新计量。'
}
finally {
    $mutableSnapshot.Dispose()
    $mutableOutput.Dispose()
    $mutableReplacement.Dispose()
}
Assert-True ($mutableOriginal.DisposeCount -eq 1 -and $mutableReplacement.DisposeCount -eq 1) '可变列表预算测试结束后自产与外部Mat必须各释放一次。'

$cycleMatA = New-Object OpenCvSharp.Mat
$cycleMatA.ChannelCount = 1
$cycleMatB = New-Object OpenCvSharp.Mat
$cycleMatB.ChannelCount = 1
$cycleMatC = New-Object OpenCvSharp.Mat
$cycleMatC.ChannelCount = 1
$cycleOutputA = [TDJS_Vision.Node._1_Acquisition.ImageSource.OutputImage]::FromOwnedSingleImage($cycleMatA, $cycleMatA)
$cycleOutputB = [TDJS_Vision.Node._1_Acquisition.ImageSource.OutputImage]::FromOwnedSingleImage($cycleMatB, $cycleMatB)
$cycleOutputC = [TDJS_Vision.Node._1_Acquisition.ImageSource.OutputImage]::FromOwnedSingleImage($cycleMatC, $cycleMatC)
$null = $cycleOutputA.TakeDependency($cycleOutputB)
$null = $cycleOutputB.TakeDependency($cycleOutputC)
$cycleRejected = $false
try {
    $null = $cycleOutputC.TakeDependency($cycleOutputA)
}
catch [System.InvalidOperationException] {
    $cycleRejected = $true
}
Assert-True $cycleRejected '三节点传递循环依赖必须在闭环边登记前被拒绝。'
$cycleOutputC.Dispose()
$cycleOutputB.Dispose()
$cycleOutputA.Dispose()
Assert-True ($cycleOutputA.ActiveLeaseCount -eq 0 -and $cycleOutputB.ActiveLeaseCount -eq 0 -and $cycleOutputC.ActiveLeaseCount -eq 0) '传递循环依赖拒绝后三方租约必须归零。'
Assert-True ($cycleMatA.DisposeCount -eq 1 -and $cycleMatB.DisposeCount -eq 1 -and $cycleMatC.DisposeCount -eq 1) '传递循环依赖拒绝后三方Mat必须各释放一次。'

$managerMat = New-Object OpenCvSharp.Mat
$managerMat.ChannelCount = 1
$managerOutput = [TDJS_Vision.Node._1_Acquisition.ImageSource.OutputImage]::FromOwnedSingleImage($managerMat, $managerMat)
$duplicateResult = New-Object TDJS_Vision.Tests.DuplicateImageResult
$duplicateResult.First = $managerOutput
$duplicateResult.Second = $managerOutput
[TDJS_Vision.Node.NodeResultResourceManager]::Release($duplicateResult)
Assert-True ($managerMat.DisposeCount -eq 1) '结果多个属性引用同一OutputImage时必须只释放一次。'

$explicitResult = New-Object TDJS_Vision.Tests.ExplicitResourceResult
[TDJS_Vision.Node.NodeResultResourceManager]::Release($explicitResult)
Assert-True ($explicitResult.ReleaseCount -eq 1) '特殊结果必须优先调用可插拔资源释放接口。'

$cycles = 50000
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$stressDisposeCount = [TDJS_Vision.Tests.ImageResourceStressRunner]::Run($cycles)
$stopwatch.Stop()
Assert-True ($stressDisposeCount -eq $cycles) '生命周期压力测试中每轮必须恰好释放一次Mat。'

$raceCycles = 1000
$raceDisposeCount = [TDJS_Vision.Tests.ImageResourceStressRunner]::RunSnapshotDisposeRace($raceCycles)
Assert-True ($raceDisposeCount -eq $raceCycles) '快照与Dispose并发争锁后每轮Mat必须恰好释放一次。'

$dependencyRaceCycles = 1000
$dependencyRaceCount = [TDJS_Vision.Tests.ImageResourceStressRunner]::RunConcurrentReverseDependencyRace($dependencyRaceCycles)
Assert-True ($dependencyRaceCount -eq $dependencyRaceCycles) '双线程同时建立反向依赖时每轮必须只接收一条边且资源归零。'

Write-Host "Image resource lifecycle checks passed. Stress cycles=$cycles, snapshot/dispose races=$raceCycles, dependency races=$dependencyRaceCycles, elapsed=$($stopwatch.ElapsedMilliseconds)ms."
