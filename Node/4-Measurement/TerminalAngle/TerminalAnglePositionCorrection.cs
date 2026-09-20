using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using OpenCvSharp;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.TerminalAngle
{
    /// <summary>持久化绘制帧的仿射位姿，避免新画区域被绝对定位角度再次旋转。</summary>
    public sealed class TerminalAnglePoseSnapshot
    {
        /// <summary>水平坐标对基准水平坐标的系数。</summary>
        public double A { get; set; } = 1;
        /// <summary>水平坐标对基准垂直坐标的系数。</summary>
        public double B { get; set; }
        /// <summary>垂直坐标对基准水平坐标的系数。</summary>
        public double C { get; set; }
        /// <summary>垂直坐标对基准垂直坐标的系数。</summary>
        public double D { get; set; } = 1;
        /// <summary>水平平移。</summary>
        public double X { get; set; }
        /// <summary>垂直平移。</summary>
        public double Y { get; set; }
        /// <summary>生成独立副本。</summary>
        public TerminalAnglePoseSnapshot Copy() { return (TerminalAnglePoseSnapshot)MemberwiseClone(); }
    }
    /// <summary>不可变的位置修正快照，采用框架的先缩放再旋转和平移约定。</summary>
    public sealed class TerminalAngleTransform
    {
        /// <summary>水平方向尺度。</summary>
        public double ScaleX { get; private set; }
        /// <summary>垂直方向尺度。</summary>
        public double ScaleY { get; private set; }
        /// <summary>当前搜索框相对基准搜索框的顺时针角度。</summary>
        public double AngleDegrees { get; private set; }
        /// <summary>变换是否严格为恒等，以避免不必要的局部重采样。</summary>
        public bool IsIdentity { get { return _a == 1 && _b == 0 && _c == 0 && _d == 1 && _tx == 0 && _ty == 0; } }
        /// <summary>仿射矩阵第一行第一列。</summary>
        private readonly double _a;
        /// <summary>仿射矩阵第一行第二列。</summary>
        private readonly double _b;
        /// <summary>仿射矩阵第二行第一列。</summary>
        private readonly double _c;
        /// <summary>仿射矩阵第二行第二列。</summary>
        private readonly double _d;
        /// <summary>水平方向仿射偏移。</summary>
        private readonly double _tx;
        /// <summary>垂直方向仿射偏移。</summary>
        private readonly double _ty;
        /// <summary>从完整仿射矩阵构造相对位姿，支持非等比尺度变化产生的剪切。</summary>
        private TerminalAngleTransform(double a, double b, double c, double d, double tx, double ty)
        {
            double determinant = a * d - b * c;
            if (new[] { a, b, c, d, tx, ty, determinant }.Any(value => !TerminalAngleRoi.Finite(value)) || determinant <= 0)
                throw new ArgumentException("区域绘制位姿无效或不可逆。");
            _a = a; _b = b; _c = c; _d = d; _tx = tx; _ty = ty;
            ScaleX = Math.Sqrt(a * a + c * c); ScaleY = Math.Sqrt(b * b + d * d);
            AngleDegrees = Math.Atan2(c, a) * 180 / Math.PI;
        }
        /// <summary>复制当前位姿以保存到区域，避免引用可变的上游结果。</summary>
        public TerminalAnglePoseSnapshot Snapshot()
        {
            return new TerminalAnglePoseSnapshot { A = _a, B = _b, C = _c, D = _d, X = _tx, Y = _ty };
        }
        /// <summary>当前定位乘以绘制时定位的逆变换，使同帧新绘制区域保持严格水平。</summary>
        public TerminalAngleTransform RelativeTo(TerminalAnglePoseSnapshot drawing)
        {
            if (drawing == null) return this;
            var validated = new TerminalAngleTransform(drawing.A, drawing.B, drawing.C, drawing.D, drawing.X, drawing.Y);
            if (_a == drawing.A && _b == drawing.B && _c == drawing.C && _d == drawing.D && _tx == drawing.X && _ty == drawing.Y)
                return new TerminalAngleTransform(null);
            double det = validated._a * validated._d - validated._b * validated._c;
            double a = (_a * drawing.D - _b * drawing.C) / det;
            double b = (-_a * drawing.B + _b * drawing.A) / det;
            double c = (_c * drawing.D - _d * drawing.C) / det;
            double d = (-_c * drawing.B + _d * drawing.A) / det;
            return new TerminalAngleTransform(a, b, c, d, _tx - a * drawing.X - b * drawing.Y, _ty - c * drawing.X - d * drawing.Y);
        }
        /// <summary>未启用修正直接使用绘制坐标，启用时只应用从绘制帧到当前帧的相对变换。</summary>
        public TerminalAngleTransform ForRoi(NodeParamTerminalAngle settings, TerminalAngleRoi roi)
        {
            return settings.UsePositionCorrection ? RelativeTo(roi?.DrawingPose) : new TerminalAngleTransform(null);
        }
        /// <summary>复制并校验位姿数值；空值仅表示未启用修正。</summary>
        public TerminalAngleTransform(PositionCorrectionInfo correction)
        {
            ScaleX = ScaleY = 1;
            if (correction == null) { _a = _d = 1; return; }
            PositionCorrectionHelper.EnsureValid(correction);
            var values = new[] { correction.BaseX, correction.BaseY, correction.CurrentX, correction.CurrentY,
                correction.BaseAngle, correction.CurrentAngle, correction.BaseScaleX, correction.BaseScaleY,
                correction.CurrentScaleX, correction.CurrentScaleY };
            if (values.Any(value => !TerminalAngleRoi.Finite(value)) || correction.BaseScaleX <= 0 || correction.BaseScaleY <= 0 ||
                correction.CurrentScaleX <= 0 || correction.CurrentScaleY <= 0)
                throw new ArgumentException("位置修正的中心、角度或尺度无效。");
            // 先取余再相减，避免异常大角度引起循环归一化耗时或差值溢出。
            AngleDegrees = (correction.CurrentAngle % 360 - correction.BaseAngle % 360) % 360;
            ScaleX = correction.CurrentScaleX / correction.BaseScaleX;
            ScaleY = correction.CurrentScaleY / correction.BaseScaleY;
            double radians = AngleDegrees * Math.PI / 180, cos = Math.Cos(radians), sin = Math.Sin(radians);
            _a = cos * ScaleX; _b = -sin * ScaleY; _c = sin * ScaleX; _d = cos * ScaleY;
            _tx = correction.CurrentX - _a * correction.BaseX - _b * correction.BaseY;
            _ty = correction.CurrentY - _c * correction.BaseX - _d * correction.BaseY;
            double determinant = _a * _d - _b * _c;
            if (new[] { _a, _b, _c, _d, _tx, _ty, determinant }.Any(value => !TerminalAngleRoi.Finite(value)) || determinant <= 0)
                throw new ArgumentException("位置修正变换不可逆或超出数值范围。");
        }
        /// <summary>把基准坐标变换为当前图像坐标。</summary>
        public PointF Forward(double x, double y)
        {
            double mappedX = _a * x + _b * y + _tx, mappedY = _c * x + _d * y + _ty;
            if (!TerminalAngleRoi.Finite(mappedX) || !TerminalAngleRoi.Finite(mappedY) || Math.Abs(mappedX) > float.MaxValue || Math.Abs(mappedY) > float.MaxValue)
                throw new ArgumentException("修正后的区域坐标超出数值范围。");
            return new PointF((float)mappedX, (float)mappedY);
        }
        /// <summary>把当前图像坐标逆变换为基准坐标，供区域编辑保存。</summary>
        public PointF Inverse(double x, double y)
        {
            double determinant = _a * _d - _b * _c;
            return new PointF((float)((_d * (x - _tx) - _b * (y - _ty)) / determinant),
                (float)((-_c * (x - _tx) + _a * (y - _ty)) / determinant));
        }
        /// <summary>返回修正后矩形的四个原图顶点。</summary>
        public PointF[] Corners(TerminalAngleRoi roi)
        {
            return new[] { Forward(roi.Left, roi.Top), Forward(roi.Right, roi.Top), Forward(roi.Right, roi.Bottom), Forward(roi.Left, roi.Bottom) };
        }
        /// <summary>验证基准参数和修正后两个区域均落在当前输入图像内。</summary>
        public void Validate(NodeParamTerminalAngle settings, int width, int height)
        {
            settings.Validate(int.MaxValue, int.MaxValue);
            foreach (var roi in new[] { settings.TerminalRoi, settings.BaseRoi })
                if (ForRoi(settings, roi).Corners(roi).Any(point => point.X < 0 || point.Y < 0 || point.X > width - 1 || point.Y > height - 1))
                    throw new ArgumentException("位置修正后的区域超出当前图像，请核对订阅图像、定位坐标系和基准区域。");
        }
        /// <summary>按基准端子局部坐标采样当前图像，仅读取需要测量的区域。</summary>
        public Mat Sample(Mat image, Rect reference)
        {
            // OpenCV 仿射变换的尺寸限制；避免错误方案导致超大局部图分配。
            if (reference.Width >= 32767 || reference.Height >= 32767 ||
                (long)reference.Width * reference.Height > (long)image.Width * image.Height * 4)
                throw new ArgumentException("端子基准区域过大，请核对位置修正尺度和区域尺寸。");
            using (var matrix = new Mat(2, 3, MatType.CV_64FC1))
            {
                matrix.Set(0, 0, _a); matrix.Set(0, 1, _b); matrix.Set(0, 2, _a * reference.X + _b * reference.Y + _tx);
                matrix.Set(1, 0, _c); matrix.Set(1, 1, _d); matrix.Set(1, 2, _c * reference.X + _d * reference.Y + _ty);
                var sampled = new Mat();
                try
                {
                    Cv2.WarpAffine(image, sampled, matrix, new OpenCvSharp.Size(reference.Width, reference.Height),
                        InterpolationFlags.Linear | InterpolationFlags.WarpInverseMap, BorderTypes.Replicate);
                    return sampled;
                }
                catch { sampled.Dispose(); throw; }
            }
        }
    }

    /// <summary>位置修正与现有算法之间的适配层；生产节点和预览共用同一条测量路径。</summary>
    public static class TerminalAnglePositionCorrection
    {
        /// <summary>沿用卡尺逐目标执行器，失败目标保留顺序，取消继续向上传播。</summary>
        public static List<TerminalAngleTargetResult> MeasureAll(ITerminalAngleMeasurer measurer, Mat image,
            NodeParamTerminalAngle settings, IReadOnlyList<PositionCorrectionInfo> corrections, CancellationToken token)
        {
            return MultiTargetMeasurementRunner.Run(corrections, token, correction =>
            {
                PositionCorrectionHelper.EnsureValid(correction);
                var transform = new TerminalAngleTransform(correction);
                var measurement = Measure(measurer, image, settings, transform, token);
                return new TerminalAngleTargetResult
                {
                    IsOk = measurement.Success, Angle = measurement.Angle, ErrorMessage = measurement.Message,
                    AlgorithmMilliseconds = measurement.AlgorithmMilliseconds,
                    DisplayResult = TerminalAngleDisplay.Build(settings, measurement, transform)
                };
            }, (correction, error) => new TerminalAngleTargetResult { IsOk = false, ErrorMessage = error.Message });
        }
        /// <summary>在端子局部区域测量，并将几何和角度转换回当前图像坐标。</summary>
        public static TerminalAngleMeasurement Measure(ITerminalAngleMeasurer measurer, Mat image,
            NodeParamTerminalAngle settings, TerminalAngleTransform transform, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (image == null || image.Empty()) throw new ArgumentException("输入图像为空。");
            if (transform == null) throw new ArgumentNullException("transform");
            transform.Validate(settings, image.Width, image.Height);
            var terminalTransform = transform.ForRoi(settings, settings.TerminalRoi);
            if (terminalTransform.IsIdentity)
            {
                var unchanged = settings.Copy();
                unchanged.BaseRoi = unchanged.TerminalRoi.Copy();
                return measurer.Measure(image, unchanged, token);
            }
            var timer = Stopwatch.StartNew();
            Rect reference = settings.TerminalRoi.ToRect(int.MaxValue, int.MaxValue);
            // 取整后实际采样范围也必须处于图像内，不能依赖填充像素形成端子边缘。
            var sampling = settings.Copy();
            sampling.TerminalRoi = new TerminalAngleRoi { Left = reference.Left, Top = reference.Top, Right = reference.Right - 1, Bottom = reference.Bottom - 1, DrawingPose = settings.TerminalRoi.DrawingPose };
            transform.Validate(sampling, image.Width, image.Height);
            using (var patch = terminalTransform.Sample(image, reference))
            {
                var local = settings.Copy();
                local.TerminalRoi = new TerminalAngleRoi { Left = 0, Top = 0, Right = patch.Width - 1, Bottom = patch.Height - 1 };
                local.BaseRoi = local.TerminalRoi.Copy(); // 基座仅显示，算法本身不采样基座。
                var measurement = measurer.Measure(patch, local, token);
                token.ThrowIfCancellationRequested();
                if (measurement.Success)
                {
                    measurement.Corners = measurement.Corners.Select(point => terminalTransform.Forward(reference.X + point.X, reference.Y + point.Y)).ToArray();
                    measurement.Top = terminalTransform.Forward(reference.X + measurement.Top.X, reference.Y + measurement.Top.Y);
                    measurement.Bottom = terminalTransform.Forward(reference.X + measurement.Bottom.X, reference.Y + measurement.Bottom.Y);
                    if (measurement.Top.Y > measurement.Bottom.Y)
                    {
                        var swap = measurement.Top; measurement.Top = measurement.Bottom; measurement.Bottom = swap;
                    }
                    measurement.Angle = Math.Atan2(measurement.Top.X - measurement.Bottom.X,
                        measurement.Bottom.Y - measurement.Top.Y) * 180 / Math.PI;
                }
                measurement.AlgorithmMilliseconds = timer.Elapsed.TotalMilliseconds;
                return measurement;
            }
        }
    }
}
