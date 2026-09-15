using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._2_ImagePreprocessing.ImageCrop
{
    /// <summary>裁剪图像节点，旋转ROI直接输出纠正后的独立矩形图像。</summary>
    public class NodeImageCrop : NodeBase
    {

        /// <summary>初始化裁剪节点和参数窗体。</summary>
        public NodeImageCrop(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new NodeParamFormImageCrop(process, this);
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultImageCrop();
        }

        /// <summary>
        /// 节点运行
        /// </summary>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;
            // 参数合法性校验
            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return new NodeReturn(NodeRunFlag.StopRun);
            }
            if (ParamForm.Params == null)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({NodeName})运行参数未设置或保存！");
            }

            if (ParamForm is NodeParamFormImageCrop form)
            {
                if (ParamForm.Params is NodeParamImageCrop param)
                {
                    try
                    {
                        // 初始化状态
                        SetStatus(NodeStatus.Unexecuted, "*");
                        base.CheckTokenCancel(token);

                        OutputImage inputImage = form.GetInputOutputImage();
                        Mat src = form.GetInputMat(inputImage);
                        NodeResultImageCrop nodeResultImageCrop = new NodeResultImageCrop();
                        Result = nodeResultImageCrop;
                        if (param.RoiEnable)
                        {
                            var rects = form.GetImageROIRects(src);
                            nodeResultImageCrop.OutputImage = BuildCroppedOutput(inputImage, src, param.ImageRois, rects);
                        }
                        else
                        {
                            nodeResultImageCrop.OutputImage = OutputImage.FromBorrowedSingleImage(
                                inputImage,
                                src,
                                form.GetInputGrayMat(inputImage));
                            nodeResultImageCrop.OutputImage.Rectangles = new List<Rect>();
                        }
                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        nodeResultImageCrop.RunTime = time;
                        Result = nodeResultImageCrop;
                        if (showLog)
                            LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms）", true);
                        return new NodeReturn(NodeRunFlag.ContinueRun);
                    }
                    catch (OperationCanceledException)
                    {
                        LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                        SetRunResult(startTime, NodeStatus.Unexecuted);
                        Result = new NodeResultImageCrop();
                        throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
                    }
                    catch (Exception ex)
                    {
                        LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                        SetRunResult(startTime, NodeStatus.Failed);
                        Result = new NodeResultImageCrop();
                        throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
                    }
                }
            }

            return new NodeReturn(NodeRunFlag.ContinueRun);
        }

        /// <summary>构造完整裁剪输出，失败时释放全部已创建的图像。</summary>
        internal static OutputImage BuildCroppedOutput(OutputImage inputImage, Mat source,
            List<ImageCropRoiRegion> regions, List<Rect> rects)
        {
            var output = new OutputImage();
            try
            {
                bool hasRegions = regions != null && regions.Count > 0;
                var roiImg = hasRegions ? CropRectifiedImages(source, regions) : CropImages(source, rects);
                output.TakeOwnership(roiImg).TakeDependency(inputImage);
                output.Bitmaps = roiImg;
                bool rectified = false;
                if (hasRegions)
                    foreach (ImageCropRoiRegion region in regions)
                        rectified |= region.RequiresOutputCoordinates(source.Width, source.Height);
                else
                    foreach (Rect rect in rects)
                        rectified |= ImageCropRoiRegion.FromRectangle(rect).RequiresOutputCoordinates(source.Width, source.Height);

                if (rectified)
                {
                    // 旋转或越界补边后使用独立基准，避免下游检测框错位或出现负坐标。
                    // 单ROI直接使用裁图作为基准；多ROI纵向拼接预览，使每张图都有独立坐标区。
                    output.SrcImg = BuildRectifiedPreview(roiImg, out List<Rect> outputRects);
                    output.TakeOwnership(output.SrcImg);
                    output.Rectangles = outputRects;
                }
                else
                {
                    output.SrcImg = source;
                    output.Rectangles = rects;
                }

                Mat firstGrayCrop;
                if (roiImg[0].Channels() == 1)
                    firstGrayCrop = roiImg[0];
                else if (hasRegions && OutputImage.HasValidImage(inputImage?.GrayImg) &&
                    inputImage.GrayImg.Width == source.Width && inputImage.GrayImg.Height == source.Height)
                    firstGrayCrop = regions[0].CropRectified(inputImage.GrayImg);
                else if (!hasRegions)
                    firstGrayCrop = BuildFirstGrayCrop(inputImage, rects, roiImg);
                else
                    firstGrayCrop = OutputImage.BuildGrayImage(roiImg[0]);
                output.TakeOwnership(firstGrayCrop);
                output.GrayImg = firstGrayCrop;
                return output;
            }
            catch
            {
                output.Dispose();
                throw;
            }
        }

        /// <summary>从同一源图直接采样多个纠正ROI，不创建整幅旋转中间图。</summary>
        internal static List<Mat> CropRectifiedImages(Mat source, IEnumerable<ImageCropRoiRegion> regions)
        {
            var images = new List<Mat>();
            try
            {
                foreach (ImageCropRoiRegion region in regions)
                {
                    if (region == null)
                        throw new InvalidOperationException("裁剪ROI参数为空。");
                    images.Add(region.CropRectified(source));
                }
                return images;
            }
            catch
            {
                foreach (Mat image in images)
                    image.Dispose();
                throw;
            }
        }

        /// <summary>为纠正后的多图建立一致的预览坐标；单图直接复用，无额外复制。</summary>
        private static Mat BuildRectifiedPreview(List<Mat> images, out List<Rect> rects)
        {
            rects = new List<Rect>(images.Count);
            int width = 0, height = 0;
            foreach (Mat image in images)
            {
                rects.Add(new Rect(0, height, image.Width, image.Height));
                width = Math.Max(width, image.Width);
                height = checked(height + image.Height);
            }
            if (images.Count == 1)
                return images[0];
            var preview = new Mat(height, width, images[0].Type(), Scalar.All(0));
            try
            {
                for (int i = 0; i < images.Count; i++)
                    using (var target = new Mat(preview, rects[i]))
                        images[i].CopyTo(target);
                return preview;
            }
            catch
            {
                preview.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 按矩形区域裁剪源图像，并返回独立的 ROI 图像，避免下游修改影响源图。
        /// </summary>
        /// <param name="source">源图像。</param>
        /// <param name="rects">ROI 矩形列表。</param>
        /// <returns>裁剪后的图像列表。</returns>
        internal static List<Mat> CropImages(Mat source, List<Rect> rects)
        {
            if (!OutputImage.HasValidImage(source))
                throw new Exception("订阅的图像为null！");
            if (rects == null || rects.Count == 0)
                throw new Exception("裁切的ROI为空！");

            var images = new List<Mat>();
            try
            {
                foreach (Rect rect in rects)
                {
                    images.Add(ImageCropRoiRegion.FromRectangle(rect).CropRectified(source));
                }
            }
            catch
            {
                foreach (Mat image in images)
                    image?.Dispose();

                throw;
            }

            return images;
        }

        /// <summary>
        /// 为第一张裁剪图构建灰度缓存，优先从上游 GrayImg 同步裁剪。
        /// </summary>
        /// <param name="inputImage">上游图像输出。</param>
        /// <param name="rects">ROI 矩形列表。</param>
        /// <param name="croppedImages">已裁剪的彩色图像列表。</param>
        /// <returns>第一张裁剪图对应的灰度缓存。</returns>
        private static Mat BuildFirstGrayCrop(OutputImage inputImage, List<Rect> rects, List<Mat> croppedImages)
        {
            if (rects != null && rects.Count > 0 && OutputImage.HasValidImage(inputImage?.GrayImg))
            {
                return ImageCropRoiRegion.FromRectangle(rects[0]).CropRectified(inputImage.GrayImg);
            }

            if (croppedImages != null && croppedImages.Count > 0)
                return OutputImage.BuildGrayImage(croppedImages[0]);

            return new Mat();
        }

    }
}
