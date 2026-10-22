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

namespace TDJS_Vision.Node._3_Detection.CombinationColor
{
    /// <summary>
    /// 组合颜色识别节点，根据彩色区间提取感兴趣区域并输出面积和二值彩色图。
    /// </summary>
    public class NodeCombinationColor : NodeBase
    {
        /// <summary>共享无状态算法实现，避免逐帧创建服务。</summary>
        private static readonly ICombinationColorAlgorithm Algorithm = new CombinationColorEngine();
        /// <summary>
        /// 初始化组合颜色识别节点。
        /// </summary>
        /// <param name="nodeId">节点ID。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="process">所属流程。</param>
        /// <param name="nodeType">节点类型。</param>
        public NodeCombinationColor(int nodeId, string nodeName, Process process, NodeType nodeType)
            : base(nodeId, nodeName, process, nodeType)
        {
            var form = new NodeParamFormCombinationColor();
            form.SetNodeBelong(this);
            ParamForm = form;
            Result = new NodeResultCombinationColor();
        }

        /// <summary>
        /// 执行组合颜色识别，输出彩色区间二值图和面积。
        /// </summary>
        /// <param name="token">流程取消令牌。</param>
        /// <param name="showLog">是否输出运行日志。</param>
        /// <returns>节点运行返回标志。</returns>
        public override Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;
            IImageResourceLease inputLease = null;
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
                token.ThrowIfCancellationRequested();
                Result = new NodeResultCombinationColor();

                NodeParamFormCombinationColor form = ParamForm as NodeParamFormCombinationColor;
                NodeParamCombinationColor param = ParamForm.Params as NodeParamCombinationColor;
                if (form == null || param == null)
                    throw new Exception("组合颜色识别参数异常。");

                var inputImage = form.GetInputImage();
                if (inputImage == null) throw new InvalidOperationException("请订阅彩色输入图像。");
                inputLease = inputImage.AcquireLease();
                // 运行时持有上游租约并只读复用，不为每帧复制整张彩色图。
                Mat gray = MeasurementNodeHelper.GetReadOnlyPreviewMat(inputImage);
                if (!OutputImage.HasValidImage(gray))
                    throw new Exception("输入图像为空，请检查图像订阅。");

                NodeResultCombinationColor nodeResult = ExecuteAnalysis(gray, param, form.GetPositionCorrections());
                Result = nodeResult;

                int time = SetRunResult(startTime, NodeStatus.Successful);
                nodeResult.RunTime = time;
                Result = nodeResult;

                if (showLog)
                {
                    LogHelper.AddLog(
                        MsgLevel.Info,
                        $"节点({ID}.{NodeName})组合颜色识别完成！耗时：{time} ms，面积：{nodeResult.Area:F0}，宽：{nodeResult.Width:F0}，高：{nodeResult.Height:F0}",
                        true);
                }

                return Task.FromResult(new NodeReturn(NodeRunFlag.ContinueRun));
            }
            catch (OperationCanceledException)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                int time = SetRunResult(startTime, NodeStatus.Unexecuted);
                Result = new NodeResultCombinationColor { RunTime = time };
                throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因：{ex.Message}", true);
                int time = SetRunResult(startTime, NodeStatus.Failed);
                Result = new NodeResultCombinationColor { RunTime = time };
                throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
            }
            finally
            {
                inputLease?.Dispose();
            }
        }

        /// <summary>
        /// 获取可复用的彩色图，优先使用上游彩色缓存。
        /// </summary>
        /// <param name="inputImage">上游输出图像。</param>
        /// <param name="ownsImage">返回的彩色图是否由当前调用方负责释放。</param>
        /// <returns>彩色图；没有有效输入时返回null。</returns>
        internal static Mat GetColorImage(OutputImage inputImage, out bool ownsImage)
        {
            ownsImage = true;
            if (inputImage == null) throw new InvalidOperationException("请订阅彩色输入图像。");
            using (var lease = inputImage.AcquireLease())
            {
                var source = MeasurementNodeHelper.GetReadOnlyPreviewMat(inputImage);
                if (source == null || source.Empty() || source.Type() != MatType.CV_8UC3)
                    throw new InvalidOperationException("颜色识别需要三通道 BGR 彩色图像。");
                return source.Clone();
            }
        }
        /// <summary>解析搜索区域的位置跟随结果，失败时不退回未修正区域。</summary>
        private static PositionCorrectionInfo ResolveDetectionCorrection(NodeParamCombinationColor param, List<PositionCorrectionInfo> corrections)
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
        /// 执行彩色区间提取和面积统计。
        /// </summary>
        /// <param name="gray">输入彩色图。</param>
        /// <param name="param">组合颜色识别参数。</param>
        /// <param name="detectionRegion">可选位置修正检测区域。</param>
        /// <returns>组合颜色识别结果。</returns>
        internal static NodeResultCombinationColor ExecuteAnalysis(Mat gray, NodeParamCombinationColor param, List<PositionCorrectionInfo> corrections)
        {
            var polygons = new List<List<PointF>>();
            var correction = ResolveDetectionCorrection(param, corrections);
            if (param.EnableDetectionRegion)
            {
                polygons.Add(BuildRegionPoints(param.GetSearchRegion(), correction));
            }
            using(var binary = Algorithm.Segment(gray,param))
            {
                if(param.EnableDetectionRegion) ApplyDetectionRegionMask(binary,polygons);
                var result = Algorithm.Analyze(binary,param);
                result.DetectionRegions = polygons.Select(points =>
                {
                    var rect = Cv2.MinAreaRect(points.Select(p => new Point2f(p.X,p.Y)));
                    return new ColorRotatedRect(rect.Center.X,rect.Center.Y,rect.Size.Width,rect.Size.Height,rect.Angle,Color.DodgerBlue);
                }).ToList();
                result.DetectionRegion = result.DetectionRegions.FirstOrDefault();
                return result;
            }
        }

        /// <summary>
        /// 根据基准ROI和位置修正信息构建当前图像上的检测区域顶点。
        /// </summary>
        /// <param name="param">组合颜色识别参数。</param>
        /// <param name="correction">可选位置修正信息。</param>
        /// <returns>检测区域顶点。</returns>
        private static List<PointF> BuildRegionPoints(NodeParamCombinationColor param, PositionCorrectionInfo correction)
        {
            if (param == null || param.RegionLength1 <= 0 || param.RegionLength2 <= 0 ||
                float.IsNaN(param.RegionColumn) || float.IsNaN(param.RegionRow) || float.IsNaN(param.RegionPhi) ||
                float.IsInfinity(param.RegionColumn) || float.IsInfinity(param.RegionRow) || float.IsInfinity(param.RegionPhi))
                throw new ArgumentException("搜索区域无效，请重新绘制。");
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

    }
}
