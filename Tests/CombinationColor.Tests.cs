using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Windows.Forms;
using OpenCvSharp;
using Newtonsoft.Json;
using TDJS_Vision.Node;
using TDJS_Vision.Node._3_Detection.CombinationColor;
using TDJS_Vision.Node._4_Measurement.Common;

/// <summary>真实程序集的颜色学习、筛选、旋转跟随和方案持久化回归。</summary>
internal static class CombinationColorTests
{
    /// <summary>累计断言次数。</summary>
    private static int checks;
    /// <summary>调用内部纯算法，不触发设备通信。</summary>
    private static object Call(string type, string method, params object[] args)
    {
        return typeof(NodeCombinationColor).Assembly.GetType("TDJS_Vision.Node._3_Detection.CombinationColor." + type)
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
    }
    /// <summary>断言并记录检查项。</summary>
    private static void Check(bool value, string text)
    {
        checks++;
        if (!value) throw new Exception(text);
    }
    /// <summary>执行完整识别并检查结果资源。</summary>
    private static NodeResultCombinationColor Run(Mat image, NodeParamCombinationColor p, List<PositionCorrectionInfo> corrections = null)
        => (NodeResultCombinationColor)Call("NodeCombinationColor", "ExecuteAnalysis", image, p, corrections);
    /// <summary>合成图覆盖实际用户要求，不依赖现场方案或相机。</summary>
    [STAThread]
    private static void Main()
    {
        var samples = Enumerable.Repeat(new Vec3b(0, 0, 255), 100).ToList();
        var models = (List<GuideColorRangeModel>)Call("CombinationColorLearning", "Learn", samples);
        Check(models.Count == 4, "四种颜色空间学习");
        var p = new NodeParamCombinationColor { Models = models, MinimumArea = 100 };
        using (var image = new Mat(200, 250, MatType.CV_8UC3, Scalar.All(0)))
        {
            Cv2.Rectangle(image, new Rect(20, 20, 20, 10), new Scalar(0, 0, 255), -1);
            Cv2.Rectangle(image, new Rect(80, 20, 10, 10), new Scalar(0, 0, 255), -1);
            Cv2.Rectangle(image, new Rect(120, 20, 5, 5), new Scalar(0, 0, 255), -1);
            foreach (string space in new[] { "HSV", "HSI", "LAB", "RGB" })
            {
                p.ColorSpace = space;
                var r = Run(image, p);
                Check(r.Count == 2 && r.Area == 300, space + "面积过滤包含等于阈值");
                Check(r.Areas.SequenceEqual(new double[] { 200, 100 }), "面积降序且列表对应");
                Check(r.Lengths[0] == 19 && r.Widths[0] == 9, "旋转框使用像素中心距离");
                r.OutputImage.Dispose();
            }
            p.OutputRegionMode = ColorOutputRegionMode.Max;
            var largest = Run(image, p);
            Check(largest.Count == 1 && largest.Area == 200, "最大模式");
            largest.OutputImage.Dispose();
            p.MinimumArea = 201;
            var empty = Run(image, p);
            Check(empty.Count == 0 && empty.Area == 0 && empty.Areas.Count == 0, "过滤后无结果不返回旧值");
            empty.OutputImage.Dispose();
            p.MinimumArea = 0; p.OutputRegionMode = ColorOutputRegionMode.All;
            p.EnableDetectionRegion = true; p.RegionColumn = 30; p.RegionRow = 25; p.RegionLength1 = 14; p.RegionLength2 = 8;
            var roi = Run(image, p);
            Check(roi.Count == 1 && roi.Area == 200, "搜索区域排除其它颜色块"); roi.OutputImage.Dispose();
            p.Regions = new List<ColorDetectionRegion> {
                new ColorDetectionRegion { Column = 30, Row = 25, Length1 = 14, Length2 = 8 },
                new ColorDetectionRegion { Column = 85, Row = 25, Length1 = 10, Length2 = 10 } };
            var single = Run(image, p);
            Check(single.Count == 1 && single.Area == 200, "旧列表也只使用第一个搜索区域");
            single.OutputImage.Dispose();
            p.Regions = null;
            p.CorrectionText1 = "位置修正"; p.CorrectionText2 = "结果";
            var correction = new PositionCorrectionInfo { IsValid = true, BaseX = 30, BaseY = 25, CurrentX = 85, CurrentY = 25, CurrentAngle = 90 };
            var followed = Run(image, p, new List<PositionCorrectionInfo> { correction });
            Check(followed.Count == 1 && followed.Area == 100, "搜索区域平移旋转跟随"); followed.OutputImage.Dispose();
            p.RegionCorrectionReference = correction;
            p.RegionColumn = 85; p.RegionPhi = (float)(Math.PI / 2);
            var snapshot = Run(image, p, new List<PositionCorrectionInfo> { correction });
            Check(snapshot.Area == 100, "绘制基准快照避免重复仿射"); snapshot.OutputImage.Dispose();
            bool failed = false;
            try { Run(image, p); } catch (TargetInvocationException) { failed = true; }
            Check(failed, "位置修正失败必须报错");
            var restored = JsonConvert.DeserializeObject<NodeParamCombinationColor>(JsonConvert.SerializeObject(p));
            var persisted = Run(image, restored, new List<PositionCorrectionInfo> { correction });
            Check(persisted.Area == 100 && restored.Models.Count == 4, "保存加载模型和跟随基准"); persisted.OutputImage.Dispose();
        }
        // 测量独立掩膜，确保孔洞不计入面积、八连通及阈值边界。
        using (var mask = new Mat(100, 100, MatType.CV_8UC1, Scalar.All(0)))
        {
            Cv2.Rectangle(mask, new Rect(10, 10, 20, 20), Scalar.All(255), -1);
            Cv2.Rectangle(mask, new Rect(15, 15, 10, 10), Scalar.All(0), -1);
            var r = (NodeResultCombinationColor)Call("CombinationColorAlgorithm", "Measure", mask, new NodeParamCombinationColor { MinimumArea = 300 });
            Check(r.Area == 300 && Cv2.CountNonZero(r.OutputImage.SrcImg) == 300, "面积和输出掩膜保留孔洞");
            r.OutputImage.Dispose();
        }
        using (var mask = new Mat(120, 120, MatType.CV_8UC1, Scalar.All(0)))
        {
            var rotated = new RotatedRect(new Point2f(60, 60), new Size2f(40, 16), 45);
            Cv2.FillConvexPoly(mask, Array.ConvertAll(rotated.Points(), q => new Point(q.X, q.Y)), Scalar.All(255));
            var r = (NodeResultCombinationColor)Call("CombinationColorAlgorithm", "Measure", mask, new NodeParamCombinationColor());
            Check(r.Count == 1 && Math.Abs(r.Lengths[0] - 40) < 2 && Math.Abs(r.Widths[0] - 16) < 2, "45度旋转区域的长短边");
            r.OutputImage.Dispose();
        }
        bool ambiguous = false;
        try { Call("CombinationColorLearning", "Learn", Enumerable.Repeat(new Vec3b(0, 0, 255), 100).Concat(Enumerable.Repeat(new Vec3b(255, 0, 0), 100)).ToList()); }
        catch (TargetInvocationException) { ambiguous = true; }
        Check(ambiguous, "取色含两种等量主色时拒绝学习");
        // 红色色相跨零时，179 与 0 都应保留。
        using (var hsv = new Mat(20, 40, MatType.CV_8UC3, new Scalar(179, 255, 255)))
        using (var bgr = new Mat())
        {
            Cv2.CvtColor(hsv, bgr, ColorConversionCodes.HSV2BGR);
            var r = Run(bgr, new NodeParamCombinationColor { Models = models });
            Check(r.Area == 800, "红色色相跨零"); r.OutputImage.Dispose();
        }
        using (var form = new NodeParamFormCombinationColor())
        {
            form.Params = new NodeParamCombinationColor { Models = models, MinimumArea = 800, Sensitivity = 72 };
            form.SetParam2Form();
            Check(form.Text == "组合颜色识别", "设计器窗体构造与参数恢复");
            Check(form.Controls.Find("buttonAddRegion", true).Length == 0 &&
                form.Controls.Find("buttonRemoveRegion", true).Length == 0 &&
                form.Controls.Find("comboBoxRegions", true).Length == 0, "单区域界面无增删切换控件");
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var type = form.GetType();
            var viewer = (TDJS_Vision.Forms.DispShowImage.ShowImageControl)type.GetField("showImageControl1", flags).GetValue(form);
            viewer.AddRoiRotatedRect(35, 45, 0.2f, 12, 16, 1.5f, System.Drawing.Color.Orange, "颜色ROI");
            type.GetField("_editingColorRoi", flags).SetValue(form, true);
            type.GetField("_roiVisible", flags).SetValue(form, true);
            type.GetMethod("CaptureColorRoi", flags).Invoke(form, null);
            type.GetMethod("TryReadRoiToFields", flags).Invoke(form, null);
            var colorRegion = (ColorDetectionRegion)type.GetMethod("SnapshotColorRoi", flags).Invoke(form, null);
            Check(colorRegion.Column == 45 && colorRegion.Row == 35, "颜色框独立记录坐标");
            var searchX = (TextBox)type.GetField("textBoxRegionColumn", flags).GetValue(form);
            Check(float.Parse(searchX.Text) == 200, "取色框不覆盖搜索框坐标");
            type.GetMethod("ClearEditingRoi", flags).Invoke(form, null);
            viewer.AddRoiRotatedRect(80, 90, 0, 20, 25, 1.5f, System.Drawing.Color.Blue, "搜索ROI");
            type.GetField("_roiVisible", flags).SetValue(form, true);
            type.GetMethod("TryReadRoiToFields", flags).Invoke(form, null);
            var unchangedColor = (ColorDetectionRegion)type.GetMethod("SnapshotColorRoi", flags).Invoke(form, null);
            Check(float.Parse(searchX.Text) == 90 && unchangedColor.Column == 45, "搜索框调整不覆盖取色框");
            var saved = new NodeParamCombinationColor { Models = models, ColorLearningRegion = unchangedColor, RegionColumn = 90 };
            form.Params = JsonConvert.DeserializeObject<NodeParamCombinationColor>(JsonConvert.SerializeObject(saved));
            form.SetParam2Form();
            var loadedColor = (ColorDetectionRegion)type.GetMethod("SnapshotColorRoi", flags).Invoke(form, null);
            Check(float.Parse(searchX.Text) == 90 && loadedColor.Column == 45, "双ROI方案保存恢复独立");
            Check(form.Controls.Find("buttonDrawColorRoi", true).Length == 1 && form.Controls.Find("buttonLearn", true).Length == 1,
                "独立绘制颜色ROI和学习按钮");
            form.Show(); Application.DoEvents();
            using (var bitmap = new System.Drawing.Bitmap(form.Width, form.Height))
            { form.DrawToBitmap(bitmap, form.ClientRectangle); bitmap.Save("CombinationColor-form.png"); }
            form.Hide();
        }
        Console.WriteLine("组合颜色识别：" + checks + " 项断言通过。");
    }
}
