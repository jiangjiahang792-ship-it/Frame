using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using OpenCvSharp;
using Newtonsoft.Json;
using TDJS_Vision.Node._4_Measurement.BlobAnalysis;
using TDJS_Vision.Node._4_Measurement.Common;

/// <summary>验证Blob绘制坐标与运行坐标一致，以及相对位姿跟随和方案持久化。</summary>
internal static class BlobRoiReferenceTests
{
    /// <summary>调用真实ROI构建逻辑，避免依赖现场相机或模型。</summary>
    private static List<PointF> Points(NodeParamBlobAnalysis param, PositionCorrectionInfo correction)
    {
        return (List<PointF>)typeof(NodeBlobAnalysis).GetMethod("BuildRegionPoints",
            BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { param, correction });
    }

    /// <summary>比较顶点坐标，容许浮点转换误差。</summary>
    private static void Equal(PointF actual, PointF expected)
    {
        if (Math.Abs(actual.X - expected.X) > 0.001 || Math.Abs(actual.Y - expected.Y) > 0.001)
            throw new Exception("ROI坐标不一致：" + actual + "，期望：" + expected);
    }

    /// <summary>执行具有非零平移、旋转及非等比尺度的实际ROI回归。</summary>
    [STAThread]
    private static int Main()
    {
        var reference = new PositionCorrectionInfo { IsValid = true, BaseX = 20, BaseY = 30,
            CurrentX = 120, CurrentY = 90, BaseAngle = 5, CurrentAngle = 35,
            CurrentScaleX = 1.5, CurrentScaleY = 0.8 };
        var param = new NodeParamBlobAnalysis { RegionColumn = 200, RegionRow = 160,
            RegionLength1 = 40, RegionLength2 = 15, RegionPhi = 0.3f, RegionCorrectionReference = reference };
        var drawn = Points(param, null);
        var same = Points(param, reference);
        for (int i = 0; i < drawn.Count; i++) Equal(same[i], drawn[i]);
        var restored = JsonConvert.DeserializeObject<NodeParamBlobAnalysis>(JsonConvert.SerializeObject(param));
        var current = JsonConvert.DeserializeObject<PositionCorrectionInfo>(JsonConvert.SerializeObject(reference));
        current.CurrentX += 25; current.CurrentY -= 10;
        var moved = Points(restored, current);
        for (int i = 0; i < drawn.Count; i++) Equal(moved[i], new PointF(drawn[i].X + 25, drawn[i].Y - 10));
        // 相同原始位姿到90度旋转、等比放大的独立几何期望。
        reference = new PositionCorrectionInfo { IsValid = true, CurrentX = 100, CurrentY = 100 };
        param.RegionCorrectionReference = reference;
        current = new PositionCorrectionInfo { IsValid = true, CurrentX = 150, CurrentY = 120,
            CurrentAngle = 90, CurrentScaleX = 2, CurrentScaleY = 2 };
        moved = Points(param, current);
        for (int i = 0; i < drawn.Count; i++)
            Equal(moved[i], new PointF(150 - 2 * (drawn[i].Y - 100), 120 + 2 * (drawn[i].X - 100)));
        param.RegionCorrectionReference = null;
        moved = Points(param, reference);
        for (int i = 0; i < drawn.Count; i++) Equal(moved[i], new PointF(drawn[i].X + 100, drawn[i].Y + 100));
        Console.WriteLine("Blob ROI基准回归通过：同位姿、相对平移、旋转缩放、持久化和旧方案兼容，共16个顶点校验。");
        VerifyMultipleRegions();
        VerifyRegionEditor();
        return 0;
    }

    /// <summary>调用完整分析并验证并集像素数量和ROI输出，及时释放输出图像。</summary>
    private static void Analyze(NodeParamBlobAnalysis param, double area, int regionCount, List<PositionCorrectionInfo> corrections = null)
    {
        using (var gray = new Mat(100, 100, MatType.CV_8UC1, Scalar.White))
        {
            var result = (NodeResultBlobAnalysis)typeof(NodeBlobAnalysis).GetMethod("ExecuteAnalysis",
                BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { gray, param, corrections });
            try
            {
                if (result.Area != area || result.DetectionRegions.Count != regionCount || result.Result.Rects.Count != regionCount)
                    throw new Exception("多区域并集面积或显示错误：" + result.Area);
                using (var form = new NodeParamFormBlobAnalysis())
                {
                    form.GetType().GetMethod("ShowMeasuredArea", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(form, new object[] { result.Area });
                    if (Field<Label>(form, "labelMeasuredArea").Text != "识别区域面积：" + area.ToString("F0") + " 像素²")
                        throw new Exception("参数界面未显示实际识别面积。");
                }
            }
            finally { result.OutputImage.Dispose(); }
        }
    }

    /// <summary>验证分离、重叠、重复、关闭ROI及每个区域独立修正基准。</summary>
    private static void VerifyMultipleRegions()
    {
        var p = new NodeParamBlobAnalysis { EnableDetectionRegion = true, MinGray = 200, MaxGray = 255,
            Regions = new List<BlobDetectionRegion> {
                new BlobDetectionRegion { Column = 20, Row = 20, Length1 = 5, Length2 = 5 },
                new BlobDetectionRegion { Column = 50, Row = 20, Length1 = 5, Length2 = 5 } } };
        Analyze(p, 242, 2);
        p.Regions[1].Column = 25;
        Analyze(p, 176, 2);
        p.Regions[1].Column = 20;
        Analyze(p, 121, 2);
        p.EnableDetectionRegion = false;
        Analyze(p, 10000, 0);
        p.EnableDetectionRegion = true;
        p.Regions[1].Column = 50;
        p.Regions[0].CorrectionReference = new PositionCorrectionInfo { IsValid = true, CurrentX = 10 };
        p.Regions[1].CorrectionReference = new PositionCorrectionInfo { IsValid = true, CurrentX = 20 };
        p.CorrectionText1 = "位置修正"; p.CorrectionText2 = "修正结果";
        var restored = JsonConvert.DeserializeObject<NodeParamBlobAnalysis>(JsonConvert.SerializeObject(p));
        Analyze(restored, 242, 2, new List<PositionCorrectionInfo> { new PositionCorrectionInfo { IsValid = true, CurrentX = 30 } });
        p.CorrectionText1 = p.CorrectionText2 = null;
        p.Regions[1].Length1 = p.Regions[1].Length2 = 3;
        p.OutputRegionMode = BlobOutputRegionMode.Max;
        Analyze(p, 121, 2);
        p.OutputRegionMode = BlobOutputRegionMode.Min;
        Analyze(p, 49, 2);
        p.MinGray = p.MaxGray = 0;
        Analyze(p, 0, 2);
        Console.WriteLine("Blob多区域分析及面积显示回归通过，共8组完整图像分析，包含无前景面积0。");
    }

    /// <summary>读取Designer控件，验证区域切换与保存恢复。</summary>
    private static T Field<T>(object instance, string name)
    {
        return (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
    }

    /// <summary>真实窗体添加、切换和删除区域，不连接生产设备。</summary>
    private static void VerifyRegionEditor()
    {
        using (var form = new NodeParamFormBlobAnalysis())
        {
            form.Params = new NodeParamBlobAnalysis { EnableDetectionRegion = true, RegionColumn = 30, RegionRow = 40 };
            form.SetParam2Form();
            var type = form.GetType();
            type.GetMethod("buttonAddRegion_Click", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { null, EventArgs.Empty });
            var picker = Field<ComboBox>(form, "comboBoxRegions");
            if (picker.Items.Count != 2 || picker.SelectedIndex != 1) throw new Exception("区域添加失败。");
            Field<TextBox>(form, "textBoxRegionColumn").Text = "70";
            picker.SelectedIndex = 0;
            if (Field<TextBox>(form, "textBoxRegionColumn").Text != "30.00") throw new Exception("切换区域丢失原坐标。");
            var saved = (NodeParamBlobAnalysis)type.GetMethod("BuildParamFromControls", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { false });
            if (saved.Regions.Count != 2 || saved.Regions[1].Column != 70) throw new Exception("多区域保存失败。");
            form.Params = JsonConvert.DeserializeObject<NodeParamBlobAnalysis>(JsonConvert.SerializeObject(saved));
            form.SetParam2Form();
            picker.SelectedIndex = 1;
            if (Field<TextBox>(form, "textBoxRegionColumn").Text != "70.00") throw new Exception("多区域恢复失败。");
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new System.Drawing.Point(-20000, -20000);
            form.Show();
            Application.DoEvents();
            using (var image = new Bitmap(form.Width, form.Height))
            { form.DrawToBitmap(image, form.ClientRectangle); image.Save("BlobMultiRegion.preview.png"); }
            type.GetMethod("buttonRemoveRegion_Click", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { null, EventArgs.Empty });
            if (picker.Items.Count != 1 || Field<Button>(form, "buttonRemoveRegion").Enabled) throw new Exception("区域删除或最后区域保护失败。");
            if (saved.Regions.Count != 2) throw new Exception("编辑修改了保存快照。");
        }
        Console.WriteLine("Blob区域编辑器添加、切换、保存恢复、删除及快照隔离验证通过。");
    }
}
