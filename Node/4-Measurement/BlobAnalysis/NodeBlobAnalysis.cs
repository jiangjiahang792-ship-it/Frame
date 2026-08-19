using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.BlobAnalysis
{
    /// <summary>
    /// Blob分析工具节点，根据灰度区间提取感兴趣区域并输出面积和二值灰度图。
    /// </summary>
    public class NodeBlobAnalysis : NodeBase
    {
        /// <summary>
        /// 初始化Blob分析工具节点。
        /// </summary>
        /// <param name="nodeId">节点ID。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="process">所属流程。</param>
        /// <param name="nodeType">节点类型。</param>
        public NodeBlobAnalysis(int nodeId, string nodeName, Process process, NodeType nodeType)
            : base(nodeId, nodeName, process, nodeType)
        {
            var form = new NodeParamFormBlobAnalysis();
            form.SetNodeBelong(this);
            ParamForm = form;
            Result = new NodeResultBlobAnalysis();
        }

        /// <summary>
        /// 执行Blob分析，输出灰度区间二值图和面积。
        /// </summary>
        /// <param name="token">流程取消令牌。</param>
        /// <param name="showLog">是否输出运行日志。</param>
        /// <returns>节点运行返回标志。</returns>
        public override Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;
            Mat temporaryGray = null;
            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return Task.FromResult(new NodeReturn(NodeRunFlag.StopRun));
            }

            if (ParamForm.Params == null)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({NodeName})运行参数未设置或保存！");
            }

            try
            {
                SetStatus(NodeStatus.Unexecuted, "*");
                base.CheckTokenCancel(token);
                Result = new NodeResultBlobAnalysis();

                NodeParamFormBlobAnalysis form = ParamForm as NodeParamFormBlobAnalysis;
                NodeParamBlobAnalysis param = ParamForm.Params as NodeParamBlobAnalysis;
                if (form == null || param == null)
                    throw new Exception("Blob分析工具参数异常。");

                Mat gray = GetGrayImage(form.GetInputImage(), out bool ownsGray);
                if (ownsGray)
                    temporaryGray = gray;
                if (!OutputImage.HasValidImage(gray))
                    throw new Exception("输入图像为空，请检查图像订阅。");

                NodeResultBlobAnalysis nodeResult = ExecuteAnalysis(gray, param, form.GetPositionCorrections());
                Result = nodeResult;

                int time = SetRunResult(startTime, NodeStatus.Successful);
                nodeResult.RunTime = time;
                Result = nodeResult;

                if (showLog)
                {
                    LogHelper.AddLog(
                        MsgLevel.Info,
                        $"节点({ID}.{NodeName})Blob分析完成！耗时：{time} ms，面积：{nodeResult.Area:F0}，宽：{nodeResult.Width:F0}，高：{nodeResult.Height:F0}",
                        true);
                }

                return Task.FromResult(new NodeReturn(NodeRunFlag.ContinueRun));
            }
            catch (OperationCanceledException)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                int time = SetRunResult(startTime, NodeStatus.Unexecuted);
                Result = new NodeResultBlobAnalysis { RunTime = time };
                throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因：{ex.Message}", true);
                int time = SetRunResult(startTime, NodeStatus.Failed);
                Result = new NodeResultBlobAnalysis { RunTime = time };
                throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
            }
            finally
            {
                temporaryGray?.Dispose();
            }
        }

        /// <summary>
        /// 获取可复用的灰度图，优先使用上游灰度缓存。
        /// </summary>
        /// <param name="inputImage">上游输出图像。</param>
        /// <param name="ownsImage">返回的灰度图是否由当前调用方负责释放。</param>
        /// <returns>灰度图；没有有效输入时返回null。</returns>
        internal static Mat GetGrayImage(OutputImage inputImage, out bool ownsImage)
        {
            ownsImage = false;
            if (inputImage == null)
                return null;
            if (OutputImage.HasValidImage(inputImage.GrayImg))
                return inputImage.GrayImg;
            if (OutputImage.HasValidImage(inputImage.SrcImg))
                return BuildGrayImage(inputImage.SrcImg, out ownsImage);
            if (inputImage.Bitmaps != null && inputImage.Bitmaps.Count > 0 && OutputImage.HasValidImage(inputImage.Bitmaps[0]))
                return BuildGrayImage(inputImage.Bitmaps[0], out ownsImage);
            return null;
        }

        /// <summary>
        /// 按需构建灰度图并标明调用方是否取得释放责任。
        /// </summary>
        /// <param name="source">上游只读源图。</param>
        /// <param name="ownsImage">返回值不是源图引用时为true。</param>
        /// <returns>可用于本轮分析的灰度图。</returns>
        private static Mat BuildGrayImage(Mat source, out bool ownsImage)
        {
            Mat gray = OutputImage.BuildGrayImage(source);
            ownsImage = gray != null && !ReferenceEquals(gray, source);
            return gray;
        }

        /// <summary>
        /// 根据参数从位置修正列表中解析当前检测区域。
        /// </summary>
        /// <param name="param">Blob分析参数。</param>
        /// <param name="corrections">位置修正列表。</param>
        /// <returns>检测区域；未启用时返回 null。</returns>
        private static PositionCorrectionInfo ResolveDetectionCorrection(NodeParamBlobAnalysis param, List<PositionCorrectionInfo> corrections)
        {
            if (param == null || !param.EnableDetectionRegion)
                return null;
            if (string.IsNullOrWhiteSpace(param.CorrectionText1) || string.IsNullOrWhiteSpace(param.CorrectionText2))
                return null;

            PositionCorrectionInfo correction = corrections == null
                ? null
                : corrections.FirstOrDefault(item => item != null && item.IsValid);
            PositionCorrectionHelper.EnsureValid(correction);

            return correction;
        }

        /// <summary>
        /// 执行灰度区间提取和面积统计。
        /// </summary>
        /// <param name="gray">输入灰度图。</param>
        /// <param name="param">Blob分析参数。</param>
        /// <param name="detectionRegion">可选位置修正检测区域。</param>
        /// <returns>Blob分析结果。</returns>
        internal static NodeResultBlobAnalysis ExecuteAnalysis(Mat gray, NodeParamBlobAnalysis param, List<PositionCorrectionInfo> corrections)
        {
            using (Mat binary = new Mat())
            {
                Cv2.InRange(gray, new Scalar(param.MinGray), new Scalar(param.MaxGray), binary);
                List<PointF> regionPoints = null;
                PositionCorrectionInfo correction = ResolveDetectionCorrection(param, corrections);
                if (param.EnableDetectionRegion)
                {
                    regionPoints = BuildRegionPoints(param, correction);
                    ApplyDetectionRegionMask(binary, regionPoints);
                }
                ApplyOutputRegionMode(binary, param.OutputRegionMode);

                double area = Cv2.CountNonZero(binary);
                Rect foregroundBounds = CalculateForegroundBounds(binary);
                Mat outputBinary = binary.Clone();
                OutputImage outputImage = OutputImage.FromOwnedSingleImage(outputBinary, outputBinary);
                try
                {
                    ColorRotatedRect detectionRegion = BuildDetectionRegionRect(regionPoints);
                    AlgorithmResult displayResult = BuildDisplayResult(area, foregroundBounds.Width, foregroundBounds.Height, param, detectionRegion);
                    outputImage.DisplayResult = displayResult;

                    return new NodeResultBlobAnalysis
                    {
                        IsOk = true,
                        JudgeOk = true,
                        Area = area,
                        Width = foregroundBounds.Width,
                        Height = foregroundBounds.Height,
                        DetectionRegion = detectionRegion,
                        OutputImage = outputImage,
                        Result = displayResult
                    };
                }
                catch
                {
                    outputImage.Dispose();
                    throw;
                }
            }
        }

        /// <summary>
        /// 根据基准ROI和位置修正信息构建当前图像上的检测区域顶点。
        /// </summary>
        /// <param name="param">Blob分析参数。</param>
        /// <param name="correction">可选位置修正信息。</param>
        /// <returns>检测区域顶点。</returns>
        private static List<PointF> BuildRegionPoints(NodeParamBlobAnalysis param, PositionCorrectionInfo correction)
        {
            OpenCvSharp.RotatedRect rect = new OpenCvSharp.RotatedRect(
                new Point2f(param.RegionColumn, param.RegionRow),
                new Size2f(param.RegionLength1 * 2F, param.RegionLength2 * 2F),
                (float)(param.RegionPhi * 180.0 / Math.PI));
            Point2f[] vertices = rect.Points();
            var points = new List<PointF>(vertices.Length);
            foreach (Point2f vertex in vertices)
            {
                PointF point = new PointF(vertex.X, vertex.Y);
                if (correction != null)
                    point = correction.TransformPoint(point.X, point.Y);
                points.Add(point);
            }

            return points;
        }

        /// <summary>
        /// 按指定多边形检测区域裁剪二值结果。
        /// </summary>
        /// <param name="binary">待裁剪的二值图。</param>
        /// <param name="regionPoints">检测区域顶点。</param>
        private static void ApplyDetectionRegionMask(Mat binary, List<PointF> regionPoints)
        {
            if (regionPoints == null || regionPoints.Count < 3)
                throw new Exception("检测区域无效，请先绘制并确认ROI。");

            using (Mat mask = new Mat(binary.Size(), MatType.CV_8UC1, Scalar.Black))
            {
                OpenCvSharp.Point[] points = regionPoints
                    .Select(item => new OpenCvSharp.Point((int)Math.Round(item.X), (int)Math.Round(item.Y)))
                    .ToArray();
                Cv2.FillConvexPoly(mask, points, Scalar.White);
                Cv2.BitwiseAnd(binary, mask, binary);
            }
        }

        /// <summary>
        /// 根据输出区域模式保留全部、最大或最小连通区域。
        /// </summary>
        /// <param name="binary">待筛选二值图。</param>
        /// <param name="mode">输出区域筛选模式。</param>
        private static void ApplyOutputRegionMode(Mat binary, BlobOutputRegionMode mode)
        {
            if (mode == BlobOutputRegionMode.All)
                return;

            using (Mat contourSource = binary.Clone())
            using (Mat selectedMask = new Mat(binary.Size(), MatType.CV_8UC1, Scalar.Black))
            {
                OpenCvSharp.Point[][] contours;
                HierarchyIndex[] hierarchy;
                Cv2.FindContours(contourSource, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                if (contours == null || contours.Length == 0)
                {
                    binary.SetTo(Scalar.Black);
                    return;
                }

                int selectedIndex = 0;
                double selectedArea = Cv2.ContourArea(contours[0]);
                for (int i = 1; i < contours.Length; i++)
                {
                    double area = Cv2.ContourArea(contours[i]);
                    if ((mode == BlobOutputRegionMode.Max && area > selectedArea) ||
                        (mode == BlobOutputRegionMode.Min && area < selectedArea))
                    {
                        selectedArea = area;
                        selectedIndex = i;
                    }
                }

                Cv2.DrawContours(selectedMask, contours, selectedIndex, Scalar.White, -1);
                Cv2.BitwiseAnd(binary, selectedMask, binary);
            }
        }

        /// <summary>
        /// 计算最终二值结果中所有白色区域的整体外接矩形。
        /// </summary>
        /// <param name="binary">已按输出区域模式筛选后的二值图。</param>
        /// <returns>白色区域外接矩形；没有白色区域时返回空矩形。</returns>
        private static Rect CalculateForegroundBounds(Mat binary)
        {
            using (Mat contourSource = binary.Clone())
            {
                OpenCvSharp.Point[][] contours;
                HierarchyIndex[] hierarchy;
                Cv2.FindContours(contourSource, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                if (contours == null || contours.Length == 0)
                    return new Rect(0, 0, 0, 0);

                int left = int.MaxValue;
                int top = int.MaxValue;
                int right = int.MinValue;
                int bottom = int.MinValue;
                foreach (OpenCvSharp.Point[] contour in contours)
                {
                    if (contour == null || contour.Length == 0)
                        continue;

                    Rect bounds = Cv2.BoundingRect(contour);
                    if (bounds.Width <= 0 || bounds.Height <= 0)
                        continue;

                    left = Math.Min(left, bounds.X);
                    top = Math.Min(top, bounds.Y);
                    right = Math.Max(right, bounds.X + bounds.Width);
                    bottom = Math.Max(bottom, bounds.Y + bounds.Height);
                }

                if (left == int.MaxValue || top == int.MaxValue || right <= left || bottom <= top)
                    return new Rect(0, 0, 0, 0);

                return new Rect(left, top, right - left, bottom - top);
            }
        }

        /// <summary>
        /// 根据检测区域顶点构建可直接绘制的旋转矩形对象。
        /// </summary>
        /// <param name="regionPoints">检测区域顶点。</param>
        /// <returns>检测区域旋转矩形；未启用检测区域时返回 null。</returns>
        private static ColorRotatedRect BuildDetectionRegionRect(List<PointF> regionPoints)
        {
            if (regionPoints == null || regionPoints.Count < 3)
                return null;

            Point2f[] points = regionPoints
                .Select(point => new Point2f(point.X, point.Y))
                .ToArray();
            OpenCvSharp.RotatedRect rect = Cv2.MinAreaRect(points);
            return new ColorRotatedRect(
                rect.Center.X,
                rect.Center.Y,
                rect.Size.Width,
                rect.Size.Height,
                rect.Angle,
                Color.DodgerBlue)
            {
                LineWidth = 1.8F
            };
        }

        /// <summary>
        /// 构建Blob分析结果的显示叠加信息。
        /// </summary>
        /// <param name="area">像素面积。</param>
        /// <param name="width">二值结果外接宽度。</param>
        /// <param name="height">二值结果外接高度。</param>
        /// <param name="param">Blob分析参数。</param>
        /// <param name="detectionRegion">可选检测区域矩形。</param>
        /// <returns>显示叠加结果。</returns>
        private static AlgorithmResult BuildDisplayResult(double area, double width, double height, NodeParamBlobAnalysis param, ColorRotatedRect detectionRegion)
        {
            var result = new AlgorithmResult();
            result.IsAllOk = true;
            result.Texts.Add(new ColorText($"Blob分析 面积:{area:F0} 宽:{width:F0} 高:{height:F0} 灰度:{param.MinGray}-{param.MaxGray} 区域:{GetOutputRegionModeText(param.OutputRegionMode)}", Color.Lime)
            {
                FontSize = 14,
                Position = DisplayTextPosition.TopLeft,
                Title = "BlobAnalysis"
            });

            if (detectionRegion != null)
                result.Rects.Add(detectionRegion);

            return result;
        }

        /// <summary>
        /// 获取输出区域模式的中文显示文本。
        /// </summary>
        /// <param name="mode">输出区域模式。</param>
        /// <returns>中文显示文本。</returns>
        private static string GetOutputRegionModeText(BlobOutputRegionMode mode)
        {
            switch (mode)
            {
                case BlobOutputRegionMode.Max:
                    return "最大";
                case BlobOutputRegionMode.Min:
                    return "最小";
                default:
                    return "所有";
            }
        }
    }
}
