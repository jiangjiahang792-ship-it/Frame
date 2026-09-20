using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.TerminalAngle
{
    /// <summary>由数值结果创建框架已有的线和文字叠加层，不分配原图位图。</summary>
    public static class TerminalAngleDisplay
    {
        /// <summary>所有目标的几何和结果文字直接显示在图像中，不依赖侧栏结果控件。</summary>
        public static AlgorithmResult BuildAll(IReadOnlyList<TerminalAngleTargetResult> items)
        {
            var display = new AlgorithmResult { IsAllOk = items.Count > 0 && items.All(item => item.IsOk) };
            foreach (var item in items)
            {
                MeasurementNodeHelper.AppendAlgorithmResult(display, item.DisplayResult);
            }
            // 汇总文字替代逐目标重复的无序提示，保留上游目标顺序。
            display.Texts.Clear();
            if (items.Count == 0) display.Texts.Add(new ColorText("没有有效定位目标，请先运行上游定位。", Color.Red));
            foreach (var item in items)
                display.Texts.Add(new ColorText(item.IsOk
                    ? string.Format("端子{0}角度：{1:+0.0;-0.0;0.0}°", items.Count == 1 ? "" : item.TargetIndex.ToString(), item.Angle)
                    : string.Format("端子{0}测量失败：{1}", items.Count == 1 ? "" : item.TargetIndex.ToString(), item.ErrorMessage), item.IsOk ? Color.Lime : Color.Red));
            return display;
        }
        /// <summary>结果只绘制端子拟合中线与基座垂直中线，保留图像角度文字。</summary>
        public static AlgorithmResult Build(NodeParamTerminalAngle settings, TerminalAngleMeasurement measurement, TerminalAngleTransform transform = null)
        {
            transform = transform ?? new TerminalAngleTransform(null);
            var display = new AlgorithmResult { IsAllOk = measurement.Success };
            if (measurement.Success && settings != null)
            {
                var terminalTransform = transform.ForRoi(settings, settings.TerminalRoi);
                var baseTransform = transform.ForRoi(settings, settings.BaseRoi);
                if (settings.BaseRoi != null)
                {
                    var roi = settings.BaseRoi;
                    var center = baseTransform.Forward((roi.Left + roi.Right) / 2, (roi.Top + roi.Bottom) / 2);
                    var corners = baseTransform.Corners(roi);
                    // 基座中线只跟随位置，保持当前图像垂直，不抵消真实端子倾角。
                    float top = settings.TerminalRoi == null ? center.Y - 20 : MinY(terminalTransform.Corners(settings.TerminalRoi));
                    display.Lines.Add(new ColorLine(new PointF(center.X, top), new PointF(center.X, MaxY(corners)), Color.Teal) { ShowCenterCross = false });
                }
            }
            if (measurement.Success)
            {
                display.Lines.Add(new ColorLine(measurement.Top, measurement.Bottom, Color.DodgerBlue) { ShowCenterCross = false });
                display.Texts.Add(new ColorText(string.Format("端子角度：{0:+0.0;-0.0;0.0}°　水平180° / 垂线90°", measurement.Angle), Color.Lime));
            }
            else display.Texts.Add(new ColorText("端子角度无效：" + measurement.Message, Color.Red));
            return display;
        }
        /// <summary>仅在编辑阶段显示两个搜索区域，结果阶段不使用此叠加。</summary>
        public static AlgorithmResult BuildEditing(NodeParamTerminalAngle settings, TerminalAngleTransform transform)
        {
            var display = new AlgorithmResult();
            if (settings == null) return display;
            AddRoi(display, settings.TerminalRoi, Color.IndianRed, transform.ForRoi(settings, settings.TerminalRoi));
            AddRoi(display, settings.BaseRoi, Color.Teal, transform.ForRoi(settings, settings.BaseRoi));
            return display;
        }
        /// <summary>用四条线表达原图 ROI，复用框架叠加图形模型。</summary>
        private static void AddRoi(AlgorithmResult display, TerminalAngleRoi roi, Color color, TerminalAngleTransform transform)
        {
            if (roi == null) return;
            var points = transform.Corners(roi);
            var a = points[0]; var b = points[1]; var c = points[2]; var d = points[3];
            display.Lines.Add(new ColorLine(a, b, color)); display.Lines.Add(new ColorLine(b, c, color));
            display.Lines.Add(new ColorLine(c, d, color)); display.Lines.Add(new ColorLine(d, a, color));
        }
        /// <summary>获取四边形上边界。</summary>
        private static float MinY(PointF[] points) { float value = points[0].Y; foreach (var point in points) value = System.Math.Min(value, point.Y); return value; }
        /// <summary>获取四边形下边界。</summary>
        private static float MaxY(PointF[] points) { float value = points[0].Y; foreach (var point in points) value = System.Math.Max(value, point.Y); return value; }
    }
}
