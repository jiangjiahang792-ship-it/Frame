using Logger;
using OpenCvSharp.Extensions;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._3_Detection.TDAI;
using System.Drawing;
using System.Drawing.Drawing2D;

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
                    image = ImageDrawer.DrawDetectionRectangles(image, aiResult, param);

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
        /// 在图像上根据检测结果绘制矩形框（使用 GDI+）
        /// </summary>
        /// <param name="sourceBitmap"></param>
        /// <param name="detectResults"></param>
        /// <returns></returns>
        public static Bitmap DrawDetectionRectangles(Bitmap sourceBitmap, List<DetectItemResult> detectResults, NodeParamImageDraw parm)
        {
            var cllonedBitmap = (Bitmap)sourceBitmap.Clone();
            // 创建一个 Graphics 对象用于绘制
            using (Graphics g = Graphics.FromImage(cllonedBitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                foreach (var item in detectResults)
                {
                    foreach (var result in item.Results)
                    {
                        var color = result.IsOk ? Color.Green : Color.Red;

                        foreach (var rect in result.Rects)
                        {
                            // 绘制矩形框
                            using (Pen pen = new Pen(color, parm.LineWidth))
                            {
                                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                            }

                            // 使用 Graphics 绘制中文文本
                            using (Font font = new Font("Microsoft YaHei", parm.FontSize)) // 确保选择支持中文的字体
                            using (Brush brush = new SolidBrush(color))
                            {
                                string text = $"{item.Name}: {result.Value}";
                                SizeF textSize = g.MeasureString(text, font);

                                // 计算文本位置，避免超出图片边界
                                PointF textPosition = new PointF(rect.X, rect.Y - textSize.Height > 0 ? rect.Y - textSize.Height - 5 : rect.Y + rect.Height + 5);
                                g.DrawString(text, font, brush, textPosition);
                            }
                        }
                    }
                }
            }
            return cllonedBitmap;
        }
    }
}
