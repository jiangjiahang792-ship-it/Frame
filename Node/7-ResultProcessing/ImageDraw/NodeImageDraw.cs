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
using System.Numerics;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

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
        /// 获取旋转矩形的4个顶点
        /// </summary>
        /// <param name="rrect"></param>
        /// <returns></returns>
        private static PointF[] GetVertices(RotatedRect rrect)
        {
            // 将角度从度转换为弧度
            double angleRad = rrect.angle * Math.PI / 180.0;

            // 计算旋转矩阵所需的基本三角函数值
            float cosA = (float)Math.Cos(angleRad);
            float sinA = (float)Math.Sin(angleRad);

            // 获取中心点坐标和半宽高
            float cx = rrect.center.x;
            float cy = rrect.center.y;
            float halfWidth = rrect.size.width / 2;
            float halfHeight = rrect.size.height / 2;

            // 初始化四个顶点的位置（未旋转前）
            PointF[] points = new PointF[4]
            {
                new PointF(-halfWidth, -halfHeight),
                new PointF(halfWidth, -halfHeight),
                new PointF(halfWidth, halfHeight),
                new PointF(-halfWidth, halfHeight)
            };

            // 应用旋转和平移到每个点上
            for (int i = 0; i < points.Length; i++)
            {
                float tempX = points[i].X * cosA - points[i].Y * sinA + cx;
                float tempY = points[i].X * sinA + points[i].Y * cosA + cy;
                points[i] = new PointF(tempX, tempY);
            }

            return points;
        }
        /// <summary>
        /// 在图像上根据检测结果绘制矩形框（使用 GDI+）
        /// </summary>
        /// <param name="sourceBitmap"></param>
        /// <param name="detectResults"></param>
        /// <returns></returns>
        public static Bitmap DrawDetectionRectangles(Bitmap sourceBitmap, AiResult detectResults, NodeParamImageDraw parm)
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
        private static void DrawOnLeftUp(Graphics g, AiResult aiResult, NodeParamImageDraw parm)
        {
            float initialY = 30;
            int lineSpacing = 10;

            // 1. 先绘制所有旋转矩形框
            foreach (var colorRect in aiResult.Rects)
            {
                PointF[] vertices = GetVertices(colorRect.RotatedRect);

                using (Pen pen = new Pen(colorRect.Color, parm.LineWidth))
                {
                    g.DrawPolygon(pen, vertices);
                }
            }

            // 2. 再绘制线条
            foreach (var line in aiResult.Lines)
            {
                using (Pen pen = new Pen(line.Color, parm.LineWidth))
                {
                    g.DrawLine(pen, line.P1, line.P2);
                }
            }

            // 3. 最后绘制文本信息（来自 Texts 成员）
            foreach (var textInfo in aiResult.Texts)
            {
                using (Font font = new Font("Microsoft YaHei", parm.FontSize))
                using (Brush brush = new SolidBrush(textInfo.Color))
                {
                    string text = textInfo.Text;
                    SizeF textSize = g.MeasureString(text, font);
                    PointF textPosition = new PointF(30, initialY);

                    g.DrawString(text, font, brush, textPosition);

                    initialY += textSize.Height + lineSpacing;
                }
            }
        }
        /// <summary>
        /// 绘制到右下角
        /// </summary>
        /// <param name="g"></param>
        /// <param name="detectResults"></param>
        /// <param name="parm"></param>

        private static void DrawBottomRightBox(Graphics g, AiResult aiResult, NodeParamImageDraw parm, Size imageSize)
        {
            // 定义初始Y坐标（从底部开始）和行高
            float initialY = imageSize.Height - 30; // 起始Y坐标为图片高度减去一个小偏移量
            int lineSpacing = 10; // 行间距

            // 首先绘制所有旋转矩形框
            foreach (var colorRect in aiResult.Rects)
            {
                PointF[] vertices = GetVertices(colorRect.RotatedRect); // 获取旋转矩形的4个顶点

                using (Pen pen = new Pen(colorRect.Color, parm.LineWidth)) // 使用 ColorRotatedRect 内部的颜色属性
                {
                    g.DrawPolygon(pen, vertices); // 绘制旋转矩形
                }
            }

            // 绘制所有颜色线条
            foreach (var line in aiResult.Lines)
            {
                using (Pen pen = new Pen(line.Color, parm.LineWidth))
                {
                    g.DrawLine(pen, line.P1, line.P2);
                }
            }

            // 然后绘制文本
            aiResult.Texts.Reverse();// 从底部开始逐行向上绘制文本
            foreach (var textInfo in aiResult.Texts) 
            {
                using (Font font = new Font("Microsoft YaHei", parm.FontSize))
                using (Brush brush = new SolidBrush(textInfo.Color))
                {
                    SizeF textSize = g.MeasureString(textInfo.Text, font);

                    // 检查是否会在画布顶部越界
                    if (initialY - textSize.Height < 0)
                        break; // 如果下一行文本将超出上边界，则停止绘制

                    // 设置文本位置在右下角区域
                    PointF textPosition = new PointF(imageSize.Width - textSize.Width - 30, initialY - textSize.Height);

                    g.DrawString(textInfo.Text, font, brush, textPosition);

                    // 更新 Y 坐标，向上移动一行
                    initialY -= textSize.Height + lineSpacing;
                }
            }
        }
    }
}
