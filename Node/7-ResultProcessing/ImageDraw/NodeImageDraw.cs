using Logger;
using OpenCvSharp.Extensions;
//using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._3_Detection.TDAI;
using System.Drawing;
using System.Drawing.Drawing2D;
using Sunny.UI.Win32;
//using System.Windows.Media;
using System.Drawing;

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

                // 绘制在不同位置
                switch (parm.TextLoc)
                {
                    case TextLoc.LeftUp:
                        DrawOnLeftUp(g, detectResults, parm);
                        break;
                    case TextLoc.RightDown:
                        DrawBottomRightBox(g, detectResults, parm, cllonedBitmap.Size);
                        break;
                    case TextLoc.FollowBox:
                        DrawFollowBox(g, detectResults, parm);
                        break;
                    default:
                        break;
                }
            }
            return cllonedBitmap;
        }
        /// <summary>
        /// 绘制到左上角
        /// </summary>
        /// <param name="g"></param>
        /// <param name="detectResults"></param>
        /// <param name="parm"></param>
        //private static void DrawFollowBox(Graphics g, List<DetectItemResult> detectResults, NodeParamImageDraw parm)
        //{
        //    foreach (var item in detectResults)
        //    {
        //        foreach (var result in item.Results)
        //        {
        //            var colorRect = result.IsOk ? Color.Green : Color.Red;

        //            foreach (var rect in result.Rects)
        //            {
        //                // 绘制矩形框
        //                using (Pen pen = new Pen(colorRect, parm.LineWidth))
        //                {
        //                    g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
        //                }

        //            }
        //        }

        //        // 使用 Graphics 绘制中文文本
        //        var colorText = item.IsOk ? Color.Green : Color.Red;
        //        using (Font font = new Font("Microsoft YaHei", parm.FontSize))
        //        using (Brush brush = new SolidBrush(colorText))
        //        {
        //            string text = $"{item.Name}: " + (item.IsOk ? "OK" : "NG");
        //            SizeF textSize = g.MeasureString(text, font);

        //            // 计算文本位置，避免超出图片边界
        //            g.DrawString(text, font, brush, textPosition);
        //        }
        //    }
        //}
        private static void DrawOnLeftUp(Graphics g, List<DetectItemResult> detectResults, NodeParamImageDraw parm)
        {
            // 定义初始Y坐标和行高（这里假设为字体高度加上一定间隔）
            float initialY = 30; // 起始Y坐标
            int lineSpacing = 10; // 行间距

            foreach (var item in detectResults)
            {
                foreach (var result in item.Results)
                {
                    var colorRect = result.IsOk ? Color.Green : Color.Red;

                    foreach (var rect in result.Rects)
                    {
                        // 绘制矩形框
                        using (Pen pen = new Pen(colorRect, parm.LineWidth))
                        {
                            g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                        }
                    }
                }

                // 使用 Graphics 绘制中文文本
                var colorText = (item.IsOk && item.IsCountOk) ? Color.Green : Color.Red;
                using (Font font = new Font("Microsoft YaHei", parm.FontSize))
                using (Brush brush = new SolidBrush(colorText))
                {
                    string text = $"{item.Name}: " + ((item.IsOk && item.IsCountOk) ? "OK" : "NG");
                    SizeF textSize = g.MeasureString(text, font);

                    // 计算文本位置，避免超出图片边界
                    PointF textPosition = new PointF(30, initialY); // X坐标固定，Y坐标从initialY开始
                    g.DrawString(text, font, brush, textPosition);

                    // 更新initialY，为下一个文本项预留空间
                    initialY += textSize.Height + lineSpacing; // 更新Y坐标，加上文本高度和行间距
                }
            }
        }
        /// <summary>
        /// 绘制到右下角
        /// </summary>
        /// <param name="g"></param>
        /// <param name="detectResults"></param>
        /// <param name="parm"></param>
        private static void DrawBottomRightBox(Graphics g, List<DetectItemResult> detectResults, NodeParamImageDraw parm, Size imageSize)
        {
            // 定义初始Y坐标（从底部开始）和行高（这里假设为字体高度加上一定间隔）
            float initialY = imageSize.Height - 120; // 起始Y坐标为图片高度减去一个小偏移量，避免超出边界
            int lineSpacing = 10; // 行间距

            // 反转列表以便从最后一个元素开始绘制
            var reversedDetectResults = new Stack<DetectItemResult>(detectResults);

            foreach (var item in reversedDetectResults)
            {
                foreach (var result in item.Results)
                {
                    var colorRect = result.IsOk ? Color.Green : Color.Red;

                    foreach (var rect in result.Rects)
                    {
                        // 绘制矩形框（如果需要调整位置，请参考之前的逻辑）
                        using (Pen pen = new Pen(colorRect, parm.LineWidth))
                        {
                            g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                        }
                    }
                }

                // 使用 Graphics 绘制中文文本
                var colorText = (item.IsOk && item.IsCountOk) ? Color.Green : Color.Red;
                using (Font font = new Font("Microsoft YaHei", parm.FontSize))
                using (Brush brush = new SolidBrush(colorText))
                {
                    string text = $"{item.Name}: " + ((item.IsOk && item.IsCountOk) ? "OK" : "NG");
                    SizeF textSize = g.MeasureString(text, font);

                    // 计算文本位置，避免超出图片边界
                    PointF textPosition = new PointF(imageSize.Width - textSize.Width - 120, initialY); // X坐标从右侧开始预留空间
                    g.DrawString(text, font, brush, textPosition);

                    // 更新initialY，为下一个文本项预留空间（向上移动）
                    initialY -= textSize.Height + lineSpacing; // 更新Y坐标，减去文本高度和行间距
                }
            }
        }
        /// <summary>
        /// 绘制跟随矩形框
        /// </summary>
        /// <param name="g"></param>
        /// <param name="detectResults"></param>
        /// <param name="parm"></param>
        private static void DrawFollowBox(Graphics g, List<DetectItemResult> detectResults, NodeParamImageDraw parm)
        {
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
    }
}
