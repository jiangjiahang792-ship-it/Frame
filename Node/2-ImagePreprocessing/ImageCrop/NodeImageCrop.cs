using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._2_ImagePreprocessing.ImageCrop
{
    public class NodeImageCrop : NodeBase
    {

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
                        if (param.RoiEnable)
                        {
                            var rects = form.GetImageROIRects(src);
                            var roiImg = CropImages(src, rects);
                            if (roiImg == null || roiImg.Count == 0)
                            {
                                throw new Exception("裁切的图像为空，请检查是否超出图像区域！");
                            }

                            nodeResultImageCrop.OutputImage.SrcImg = src;
                            nodeResultImageCrop.OutputImage.Bitmaps = roiImg;
                            nodeResultImageCrop.OutputImage.Rectangles = rects;
                            nodeResultImageCrop.OutputImage.GrayImg = BuildFirstGrayCrop(inputImage, rects, roiImg);
                        }
                        else
                        {
                            nodeResultImageCrop.OutputImage = OutputImage.FromSingleImage(src, form.GetInputGrayMat(inputImage));
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
                        throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
                    }
                    catch (Exception ex)
                    {
                        LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                        SetRunResult(startTime, NodeStatus.Failed);
                        throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
                    }
                }
            }

            return new NodeReturn(NodeRunFlag.ContinueRun);
        }

        /// <summary>
        /// 按矩形区域裁剪源图像，并返回独立的 ROI 图像，避免下游修改影响源图。
        /// </summary>
        /// <param name="source">源图像。</param>
        /// <param name="rects">ROI 矩形列表。</param>
        /// <returns>裁剪后的图像列表。</returns>
        private static List<Mat> CropImages(Mat source, List<Rect> rects)
        {
            if (!OutputImage.HasValidImage(source))
                throw new Exception("订阅的图像为null！");
            if (rects == null || rects.Count == 0)
                throw new Exception("裁切的ROI为空！");

            var images = new List<Mat>();
            foreach (Rect rect in rects)
            {
                EnsureRoiInsideImage(rect, source);
                using (Mat roi = new Mat(source, rect))
                {
                    images.Add(roi.Clone());
                }
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
                EnsureRoiInsideImage(rects[0], inputImage.GrayImg);
                using (Mat grayRoi = new Mat(inputImage.GrayImg, rects[0]))
                {
                    return grayRoi.Clone();
                }
            }

            if (croppedImages != null && croppedImages.Count > 0)
                return OutputImage.BuildGrayImage(croppedImages[0]);

            return new Mat();
        }

        /// <summary>
        /// 校验 ROI 是否完全位于图像范围内。
        /// </summary>
        /// <param name="rect">待校验的 ROI 矩形。</param>
        /// <param name="source">源图像。</param>
        private static void EnsureRoiInsideImage(Rect rect, Mat source)
        {
            if (rect.Width <= 0 || rect.Height <= 0 ||
                rect.X < 0 || rect.Y < 0 ||
                rect.X + rect.Width > source.Width ||
                rect.Y + rect.Height > source.Height)
            {
                throw new Exception("裁切的图像为空，请检查是否超出图像区域！");
            }
        }
    }
}
