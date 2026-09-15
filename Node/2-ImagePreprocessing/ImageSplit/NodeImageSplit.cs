using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._2_ImagePreprocessing.ImageSplit
{
    public class NodeImageSplit : NodeBase
    {
        public NodeImageSplit(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new ParamFormImageSplit(process, this);
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultImageSplit();
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
            if (ParamForm is ParamFormImageSplit paramForm)
            {
                if (ParamForm.Params is NodeParamImageSplit param)
                {
                    try
                    {
                        // 初始化状态
                        SetStatus(NodeStatus.Unexecuted, "*");
                        base.CheckTokenCancel(token);

                        NodeResultImageSplit res = new NodeResultImageSplit();
                        Result = res;
                        OutputImage inputImage = paramForm.GetInputOutputImage();
                        Mat img = paramForm.GetInputMat(inputImage);
                        var imgs = SplitImage(img, param.Rows, param.Cols, out List<Rect> rects);
                        res.OutputImage = new OutputImage
                        {
                            SrcImg = img,
                            Bitmaps = new List<Mat>(imgs),
                            Rectangles = rects
                        };
                        res.OutputImage
                            .TakeOwnership(imgs)
                            .TakeDependency(inputImage);
                        Mat firstGraySplit = BuildFirstSplitGray(inputImage, rects, imgs);
                        res.OutputImage.GrayImg = firstGraySplit;
                        res.OutputImage.TakeOwnership(firstGraySplit);
                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        res.RunTime = time;
                        Result = res;
                        if (showLog)
                            LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);
                        return new NodeReturn(NodeRunFlag.ContinueRun);
                    }
                    catch (OperationCanceledException)
                    {
                        LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                        SetRunResult(startTime, NodeStatus.Unexecuted);
                        Result = new NodeResultImageSplit();
                        throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
                    }
                    catch (Exception ex)
                    {
                        LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                        SetRunResult(startTime, NodeStatus.Failed);
                        Result = new NodeResultImageSplit();
                        throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
                    }
                }
            }

            return new NodeReturn(NodeRunFlag.StopRun);
        }

        /// <summary>
        /// 将一个 Mat 图像分成 m 行 n 列的小图像，丢弃剩余的行和列像素。
        /// </summary>
        /// <param name="originalImage">原始图像。</param>
        /// <param name="rows">行数。</param>
        /// <param name="cols">列数。</param>
        /// <param name="rectangles">每张小图相对源图的矩形位置。</param>
        /// <returns>包含分割后图像的列表。</returns>
        public static List<Mat> SplitImage(Mat originalImage, int rows, int cols, out List<Rect> rectangles)
        {
            rectangles = new List<Rect>();
            if (!OutputImage.HasValidImage(originalImage))
                throw new ArgumentNullException(nameof(originalImage), "原始图像不能为空。");

            if (rows <= 0 || cols <= 0)
                throw new ArgumentException("行数和列数必须是正整数。");

            // 计算每个子图像的最大整除宽度和高度。
            int baseHeight = originalImage.Height / rows * rows;
            int baseWidth = originalImage.Width / cols * cols;

            // 每个子图像的实际宽度和高度。
            int subHeight = baseHeight / rows;
            int subWidth = baseWidth / cols;
            if (subHeight <= 0 || subWidth <= 0)
                throw new ArgumentException("分割后的图像尺寸必须大于0。");

            List<Mat> subImages = new List<Mat>();

            try
            {
                // 遍历行和列来提取子图像。
                for (int row = 0; row < rows; row++)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        int y = row * subHeight;
                        int x = col * subWidth;
                        Rect roi = new Rect(x, y, subWidth, subHeight);

                        // 输出独立小图，避免下游写入影响源图。
                        using (Mat subMat = new Mat(originalImage, roi))
                        {
                            subImages.Add(subMat.Clone());
                        }
                        rectangles.Add(roi);
                    }
                }
            }
            catch
            {
                foreach (Mat subImage in subImages)
                    subImage?.Dispose();

                rectangles.Clear();
                throw;
            }

            return subImages;
        }

        /// <summary>
        /// 为第一张分割图构建灰度缓存，优先从上游 GrayImg 同步裁剪。
        /// </summary>
        /// <param name="inputImage">上游图像输出。</param>
        /// <param name="rectangles">分割区域列表。</param>
        /// <param name="splitImages">已分割的图像列表。</param>
        /// <returns>第一张分割图对应的灰度缓存。</returns>
        private static Mat BuildFirstSplitGray(OutputImage inputImage, List<Rect> rectangles, List<Mat> splitImages)
        {
            if (rectangles != null && rectangles.Count > 0 && OutputImage.HasValidImage(inputImage?.GrayImg))
            {
                using (Mat grayRoi = new Mat(inputImage.GrayImg, rectangles[0]))
                {
                    return grayRoi.Clone();
                }
            }

            if (splitImages != null && splitImages.Count > 0)
                return OutputImage.BuildGrayImage(splitImages[0]);

            return new Mat();
        }
    }
}
