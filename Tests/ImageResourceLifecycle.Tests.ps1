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
Assert-Contains $saveNode 'IImageResourceLease imageLease = outputImage.AcquireLease();' '保存任务入队前必须取得图像租约。'
Assert-Contains $saveNode 'saveImagedata.Dispose();' '保存后台完成或异常后必须释放任务租约。'
Assert-Contains $saveNode 'pendingTask.Dispose();' '保存队列停止排空时必须释放待处理任务租约。'

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
        }

        public int ChannelCount { get; set; }
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
using OpenCvSharp;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
'@
$outputImageBody = [System.Text.RegularExpressions.Regex]::Replace($outputImageSource, '(?m)^using\s+[^;]+;\s*\r?\n', '')
$resourceManagerBody = [System.Text.RegularExpressions.Regex]::Replace($resourceManagerSource, '(?m)^using\s+[^;]+;\s*\r?\n', '')
Add-Type -TypeDefinition ($commonUsings + [Environment]::NewLine + $outputImageBody + [Environment]::NewLine + $resourceManagerBody + [Environment]::NewLine + $stubs) -Language CSharp

$colorSource = New-Object OpenCvSharp.Mat
$ownedOutput = [TDJS_Vision.Node._1_Acquisition.ImageSource.OutputImage]::FromOwnedSingleImage($colorSource)
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
$borrowedWithLocalGray = [TDJS_Vision.Node._1_Acquisition.ImageSource.OutputImage]::FromBorrowedSingleImage($borrowedColor)
$localGray = $borrowedWithLocalGray.GrayImg
Assert-True ($borrowedWithLocalGray.OwnedImageCount -eq 1) '借用彩图按需生成的灰度缓存应由当前输出拥有。'
$borrowedWithLocalGray.Dispose()
Assert-True ($borrowedColor.DisposeCount -eq 0 -and $localGray.DisposeCount -eq 1) '借用彩图不能释放，但自产灰度缓存必须释放。'

$parentMat = New-Object OpenCvSharp.Mat
$parentMat.ChannelCount = 1
$parentOutput = [TDJS_Vision.Node._1_Acquisition.ImageSource.OutputImage]::FromOwnedSingleImage($parentMat, $parentMat)
$childOutput = [TDJS_Vision.Node._1_Acquisition.ImageSource.OutputImage]::FromBorrowedSingleImage($parentOutput, $parentMat, $parentMat)
$childLease = $childOutput.AcquireLease()
Assert-True ($parentOutput.ActiveLeaseCount -eq 1 -and $childOutput.OwnedDependencyCount -eq 1) '借用输出必须持有一份父级资源租约。'
$parentOutput.Dispose()
$childOutput.Dispose()
Assert-True ($parentMat.DisposeCount -eq 0) '下游租约未释放时，父级自产Mat不能提前释放。'
$childLease.Dispose()
Assert-True ($parentMat.DisposeCount -eq 1) '下游最后一个租约释放后，父级租约链必须完成回收。'

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

Write-Host "Image resource lifecycle checks passed. Stress cycles=$cycles, elapsed=$($stopwatch.ElapsedMilliseconds)ms."
