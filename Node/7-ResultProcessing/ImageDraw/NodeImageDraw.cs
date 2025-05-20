using Logger;
using OpenCvSharp.Extensions;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._7_ResultProcessing.DataShow;
using System.Drawing;
using MvCameraControl;
using TDJS_Vision.Node._3_Detection.FindCircle;
using Sunny.UI.Win32;
using System.Windows.Media;

namespace TDJS_Vision.Node._7_ResultProcessing.ImageDraw
{
    internal class NodeImageDraw : NodeBase
    {
        public static event EventHandler<DatashowData> DataShow;

        public NodeImageDraw(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new NodeParamFormImageDraw();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultImageDraw();
        }

        /// <summary>
        /// 节点运行
        /// </summary>
        public override async Task Run(CancellationToken token)
        {
            DateTime startTime = DateTime.Now;

            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return;
            }

            if (ParamForm.Params == null)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({NodeName})运行参数未设置或保存！");
            }

            var param = (NodeParamImageDraw)ParamForm.Params;
            if (ParamForm is NodeParamFormImageDraw form)
            {
                try
                {
                    // 初始化运行状态
                    SetStatus(NodeStatus.Unexecuted, "*");
                    base.CheckTokenCancel(token);

                    //执行绘制
                    var image = form.GetImage();
                    var aiResult = form.GetAIResults();
                    image = ImageDrawer.DrawDetectionRectangles(image, aiResult);

                    // 输出绘制后的图像
                    ((NodeResultImageDraw)Result).RenderImage = image;

                    long time = SetRunResult(startTime, NodeStatus.Successful);
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);
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
                    throw new Exception($"节点({ID}.{NodeName})运行失败！原因:{ex.Message}");
                }
            }
        }
    }
    /// <summary>
    /// 图像绘制类
    /// </summary>
    internal static class ImageDrawer
    {
        /// <summary>
        /// 在图像上根据检测结果绘制矩形框
        /// </summary>
        /// <param name="sourceBitmap">原始图像</param>
        /// <param name="detectResults">检测结果列表</param>
        /// <returns>绘制后的图像</returns>
        public static Bitmap DrawDetectionRectangles(Bitmap sourceBitmap, List<DetectItemResult> detectResults)
        {
            // 将 Bitmap 转换为 OpenCV 的 Mat 格式
            using (Mat mat = BitmapConverter.ToMat(sourceBitmap))
            {
                bool isGrayscale = sourceBitmap.PixelFormat == System.Drawing.Imaging.PixelFormat.Format8bppIndexed;
                if (isGrayscale)
                    Cv2.CvtColor(mat, mat, ColorConversionCodes.GRAY2BGR);
                foreach (var item in detectResults)
                {
                    foreach (var result in item.Results)
                    {
                        var color = result.IsOk ? Scalar.FromRgb(0, 255, 0) : Scalar.Red;

                        foreach (var rect in result.Rects)
                        {
                            // 绘制矩形框
                            Cv2.Rectangle(mat, rect, color, 2, LineTypes.Link8);
                        }
                    }
                }
                // 将绘制后的 Mat 转回 Bitmap 返回
                return BitmapConverter.ToBitmap(mat);
            }
        }
    }
}
