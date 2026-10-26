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

                NodeResultBlobAnalysis nodeResult = ExecuteTargets(gray, param, form.GetPositionCorrections(), token);
                Result = nodeResult;

                int time = SetRunResult(startTime, NodeStatus.Successful);
                nodeResult.RunTime = time;
                Result = nodeResult;

                if (showLog)
                {
                    LogHelper.AddLog(
                        MsgLevel.Info,
                        $"节点({ID}.{NodeName})Blob分析完成！耗时：{time} ms，区域数量：{nodeResult.Count}，面积：{nodeResult.Area:F0}，宽：{nodeResult.Width:F0}，高：{nodeResult.Height:F0}",
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
        /// 执行灰度区间提取和面积统计。
        /// </summary>
        /// <param name="gray">输入灰度图。</param>
        /// <param name="param">Blob分析参数。</param>
        /// <param name="corrections">按目标顺序排列的位置修正列表。</param>
        /// <returns>Blob分析结果。</returns>
        internal static NodeResultBlobAnalysis ExecuteAnalysis(Mat gray, NodeParamBlobAnalysis param, List<PositionCorrectionInfo> corrections)
        {
            return ExecuteTargets(gray, param, corrections, CancellationToken.None);
        }

        /// <summary>按定位列表逐目标检测全部ROI，摘要保留首目标，图像与绘制结果合并所有目标。</summary>
        private static NodeResultBlobAnalysis ExecuteTargets(Mat gray, NodeParamBlobAnalysis param, List<PositionCorrectionInfo> corrections, CancellationToken token)
        {
            bool follow = param.EnableDetectionRegion && !string.IsNullOrWhiteSpace(param.CorrectionText1) && !string.IsNullOrWhiteSpace(param.CorrectionText2);
            var targets = follow ? (corrections ?? new List<PositionCorrectionInfo>()) : new List<PositionCorrectionInfo> { null };
            var owned = new List<NodeResultBlobAnalysis>();
            Mat segmented = null;
            try
            {
                segmented = new Mat();
                Cv2.InRange(gray, new Scalar(param.MinGray), new Scalar(param.MaxGray), segmented);
                var items = MultiTargetMeasurementRunner.Run(targets, token, correction =>
                {
                    if (follow) PositionCorrectionHelper.EnsureValid(correction);
                    var item = ExecuteSingle(segmented, param, correction);
                    owned.Add(item);
                    return item;
                }, (correction, error) => new NodeResultBlobAnalysis { IsOk = false, JudgeOk = false, ErrorMessage = error.Message });
                var first = items.FirstOrDefault() ?? new NodeResultBlobAnalysis();
                var result = new NodeResultBlobAnalysis
                {
                    Items = items, IsOk = items.Count > 0 && items.All(item => item.IsOk),
                    Count = first.Count, Area = first.Area, Width = first.Width, Height = first.Height,
                    DetectionRegions = items.SelectMany(item => item.DetectionRegions ?? new List<ColorRotatedRect>()).ToList()
                };
                try
                {
                    result.JudgeOk = result.IsOk;
                    result.ErrorMessage = items.Count == 0 ? "未找到定位目标。" : string.Join("；", items.Where(item => !item.IsOk).Select(item => "目标" + item.TargetIndex + "：" + item.ErrorMessage));
                    if (!result.IsOk) result.Result.Texts.Add(new ColorText(result.ErrorMessage, Color.Red));
                    result.DetectionRegion = result.DetectionRegions.FirstOrDefault();
                    using (var merged = new Mat(gray.Rows, gray.Cols, MatType.CV_8UC1, Scalar.All(0)))
                    {
                        foreach (var item in items)
                        {
                            token.ThrowIfCancellationRequested();
                            if (OutputImage.HasValidImage(item.OutputImage?.SrcImg)) Cv2.BitwiseOr(merged, item.OutputImage.SrcImg, merged);
                            MeasurementNodeHelper.AppendAlgorithmResult(result.Result, item.Result);
                        }
                        var pixels = merged.Clone();
                        result.OutputImage = OutputImage.FromOwnedSingleImage(pixels, pixels);
                    }
                    result.Result.IsAllOk = result.IsOk;
                    result.OutputImage.DisplayResult = result.Result;
                    return result;
                }
                catch { result.ReleaseResources(); throw; }
            }
            catch
            {
                foreach (var item in owned) item.ReleaseResources();
                throw;
            }
            finally { segmented?.Dispose(); }
        }

        /// <summary>复用原单目标算法；各目标独立做面积过滤，多ROI仍合并为当前目标的检测掩膜。</summary>
        private static NodeResultBlobAnalysis ExecuteSingle(Mat gray, NodeParamBlobAnalysis param, PositionCorrectionInfo correction)
        {
            using (Mat binary = gray.Clone())
            {
                var regionPolygons = new List<List<PointF>>();

                if (param.EnableDetectionRegion)
                {
                    if (param.Regions != null && param.Regions.Count > 0)
                    {
                        foreach (BlobDetectionRegion region in param.Regions)
                        {
                            if (region == null || region.Length1 <= 0 || region.Length2 <= 0)
                                throw new Exception("检测区域无效，请重新确认ROI。");
                            regionPolygons.Add(BuildRegionPoints(region.ToParameters(), correction));
                        }
                    }
                    else regionPolygons.Add(BuildRegionPoints(param, correction));
                    ApplyDetectionRegionMask(binary, regionPolygons);
                }
                int count = FilterConnectedRegions(binary, param);

                double area = Cv2.CountNonZero(binary);
                Rect foregroundBounds = CalculateForegroundBounds(binary);
                Mat outputBinary = binary.Clone();
                OutputImage outputImage = OutputImage.FromOwnedSingleImage(outputBinary, outputBinary);
                try
                {
                    var detectionRegions = regionPolygons.Select(BuildDetectionRegionRect).ToList();
                    ColorRotatedRect detectionRegion = detectionRegions.FirstOrDefault();
                    AlgorithmResult displayResult = BuildDisplayResult(area, foregroundBounds.Width, foregroundBounds.Height, param, detectionRegion);
                    displayResult.Rects.AddRange(detectionRegions.Skip(1));
                    outputImage.DisplayResult = displayResult;

                    return new NodeResultBlobAnalysis
                    {
                        IsOk = true,
                        JudgeOk = true,
                        Area = area,
                        Count = count,
                        Width = foregroundBounds.Width,
                        Height = foregroundBounds.Height,
                        DetectionRegion = detectionRegion,
                        DetectionRegions = detectionRegions,
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
        internal static List<PointF> BuildRegionPoints(NodeParamBlobAnalysis param, PositionCorrectionInfo correction)
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
                {
                    // ROI保存的是绘制图像坐标，先还原到公共基准，再变换到本次目标，避免重复叠加绘制时的偏移。
                    if (param.RegionCorrectionReference != null)
                        point = param.RegionCorrectionReference.InverseTransformPoint(point.X, point.Y);
                    point = correction.TransformPoint(point.X, point.Y);
                }
                points.Add(point);
            }

            return points;
        }

        /// <summary>
        /// 按指定多边形检测区域裁剪二值结果。
        /// </summary>
        /// <param name="binary">待裁剪的二值图。</param>
        /// <param name="regions">多个区域顶点，取并集，重叠像素只计算一次。</param>
        private static void ApplyDetectionRegionMask(Mat binary, List<List<PointF>> regions)
        {
            if (regions == null || regions.Count == 0)
                throw new Exception("检测区域无效，请先绘制并确认ROI。");

            using (Mat mask = new Mat(binary.Size(), MatType.CV_8UC1, Scalar.Black))
            {
                // 所有ROI共用一张掩膜，避免逐区域复制图像，也避免重叠区域重复计数。
                foreach (var regionPoints in regions)
                {
                    if (regionPoints == null || regionPoints.Count < 3)
                        throw new Exception("检测区域无效，请先绘制并确认ROI。");
                    OpenCvSharp.Point[] points = regionPoints
                        .Select(item => new OpenCvSharp.Point((int)Math.Round(item.X), (int)Math.Round(item.Y)))
                        .ToArray();
                    Cv2.FillConvexPoly(mask, points, Scalar.White);
                }
                Cv2.BitwiseAnd(binary, mask, binary);
            }
        }

        /// <summary>按八连通前景像素面积过滤和选择区域，空洞不计面积，仅进行一次连通域标记。</summary>
        /// <param name="binary">原位更新的二值图。</param>
        /// <param name="param">面积范围和输出模式。</param>
        /// <returns>最终保留的区域数量。</returns>
        private static int FilterConnectedRegions(Mat binary, NodeParamBlobAnalysis param)
        {
            if (param.EnableAreaFilter && (param.MinArea < 0 || param.MaxArea < param.MinArea))
                throw new ArgumentException("最小面积应非负且不能大于最大面积。");
            using (var labels = new Mat())
            using (var stats = new Mat())
            using (var centers = new Mat())
            {
                int total = Cv2.ConnectedComponentsWithStats(binary, labels, stats, centers, PixelConnectivity.Connectivity8);
                var keep = new bool[total];
                int count = 0, selected = 0, selectedArea = 0;
                for (int label = 1; label < total; label++)
                {
                    int area = stats.At<int>(label, (int)ConnectedComponentsTypes.Area);
                    if (param.EnableAreaFilter && (area < param.MinArea || area > param.MaxArea)) continue;
                    if (param.OutputRegionMode == BlobOutputRegionMode.All) { keep[label] = true; count++; }
                    else if (selected == 0 ||
                        (param.OutputRegionMode == BlobOutputRegionMode.Max && area > selectedArea) ||
                        (param.OutputRegionMode == BlobOutputRegionMode.Min && area < selectedArea))
                    { selected = label; selectedArea = area; }
                }
                if (selected > 0) { keep[selected] = true; count = 1; }
                // 全部保留时直接复用二值图；过滤时按行重建，避免为每个区域扫描整图。
                if (count == total - 1) return count;
                var labelRow = new int[binary.Cols];
                var outputRow = new byte[binary.Cols];
                for (int y = 0; y < binary.Rows; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(labels.Ptr(y), labelRow, 0, labelRow.Length);
                    for (int x = 0; x < outputRow.Length; x++) outputRow[x] = keep[labelRow[x]] ? (byte)255 : (byte)0;
                    System.Runtime.InteropServices.Marshal.Copy(outputRow, 0, binary.Ptr(y), outputRow.Length);
                }
                return count;
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
