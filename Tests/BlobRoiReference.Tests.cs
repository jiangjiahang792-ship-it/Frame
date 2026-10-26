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
    /// <summary>输出原始异常信息，避免依赖框架异常格式化器。</summary>
    [STAThread]
    private static int Main()
    {
        try { RunTests(); return 0; }
        catch (Exception error)
        {
            for (var current = error; current != null; current = current.InnerException)
                Console.WriteLine(current.GetType().FullName + ": " + current.Message);
            return 1;
        }
    }
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
    private static int RunTests()
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
        VerifyMultipleTargets();
        VerifyRegionEditor();
        VerifyEditingFollow();
        return 0;
    }

    /// <summary>验证各目标独立检测全部ROI、面积过滤、失败占位、旧摘要和订阅分类。</summary>
    private static void VerifyMultipleTargets()
    {
        var p = new NodeParamBlobAnalysis { EnableDetectionRegion = true, MinGray = 200, MaxGray = 255,
            CorrectionText1 = "定位", CorrectionText2 = "位置修正信息列表",
            Regions = new List<BlobDetectionRegion> {
                new BlobDetectionRegion { Column = 20, Row = 20, Length1 = 5, Length2 = 5 },
                new BlobDetectionRegion { Column = 40, Row = 20, Length1 = 5, Length2 = 5 } } };
        var method = typeof(NodeBlobAnalysis).GetMethod("ExecuteAnalysis", BindingFlags.NonPublic | BindingFlags.Static);
        using (var image = new Mat(80, 150, MatType.CV_8UC1, Scalar.All(0)))
        {
            foreach (int x in new[] { 15, 35, 75 }) Cv2.Rectangle(image, new Rect(x, 15, 11, 11), Scalar.White, -1);
            var targets = new List<PositionCorrectionInfo> {
                new PositionCorrectionInfo { IsValid = true, TargetIndex = 7 },
                new PositionCorrectionInfo { IsValid = true, TargetIndex = 9, CurrentX = 60 } };
            var result = (NodeResultBlobAnalysis)method.Invoke(null, new object[] { image, p, targets });
            try
            {
                if (!result.IsOk || result.Items.Count != 2 || result.Items[0].Count != 2 || result.Items[1].Count != 1 || result.Count != 2 || result.Items[1].Area != 121 || result.DetectionRegions.Count != 4)
                    throw new Exception("多目标没有分别检测全部ROI或首目标摘要改变");
                if (Cv2.CountNonZero(result.OutputImage.SrcImg) != 363) throw new Exception("多目标图像未合并");
                if (result.Items[0].TargetIndex != 7 || result.Items[1].TargetIndex != 9) throw new Exception("目标序号丢失");
                var outputs = TDJS_Vision.Node.SubscriptionPortCatalog.GetStaticOutputs(typeof(NodeResultBlobAnalysis));
                bool found = false;
                foreach (var output in outputs) if (output.PropertyPath == "Items") found = output.Category == TDJS_Vision.Node.SubscriptionDataCategory.MeasurementResult;
                if (!found) throw new Exception("多目标结果未进入订阅目录");
                var evaluator = typeof(NodeBlobAnalysis).Assembly.GetType("TDJS_Vision.Node._6_LogicTool.MultiCondition.MultiConditionEvaluator");
                var condition = new TDJS_Vision.Node._6_LogicTool.MultiCondition.MultiConditionItem { PropertyPath = "Count", Value1 = "2" };
                var args = new object[] { result, condition, null };
                var read = evaluator.GetMethod("TryReadMultiTargetConditionValue", BindingFlags.Static | BindingFlags.NonPublic);
                if (!(bool)read.Invoke(null, args) || (bool)args[2].GetType().GetProperty("AreAllMultiTargetValuesMatched").GetValue(args[2]))
                    throw new Exception("多条件只判断首目标数量，漏掉第二目标不合格");
                condition.Operator = TDJS_Vision.Node._6_LogicTool.MultiCondition.MultiConditionOperator.Between;
                condition.Value1 = "1"; condition.Value2 = "2";
                read.Invoke(null, args);
                if (!(bool)args[2].GetType().GetProperty("AreAllMultiTargetValuesMatched").GetValue(args[2])) throw new Exception("多目标数量区间判断错误");
                var builder = typeof(NodeBlobAnalysis).Assembly.GetType("TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw.ResultOverlayDrawBuilder");
                var lines = (List<string>)builder.GetMethod("BuildTextValueLines", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { result, "Count", result.Count });
                if (!string.Join("", lines).Contains("2") || !string.Join("", lines).Contains("1")) throw new Exception("文本未显示所有目标数量");
                if (TDJS_Vision.Node.SubscriptionTypeCompatibility.ResolveCategory(typeof(List<NodeResultBlobAnalysis>)) != TDJS_Vision.Node.SubscriptionDataCategory.MeasurementResult)
                    throw new Exception("组合模块动态输出类型丢失");

            }
            finally { result.ReleaseResources(); result.ReleaseResources(); }
            targets[0].IsValid = false;
            result = (NodeResultBlobAnalysis)method.Invoke(null, new object[] { image, p, targets });
            try
            {
                if (result.IsOk || result.JudgeOk || result.Items.Count != 2 || result.Items[0].IsOk || !result.Items[1].IsOk || result.Items[1].Count != 1 || string.IsNullOrEmpty(result.Items[0].ErrorMessage))
                    throw new Exception("失败目标被跳过或阻断后续目标");
            }
            finally { result.ReleaseResources(); }
            result = (NodeResultBlobAnalysis)method.Invoke(null, new object[] { image, p, new List<PositionCorrectionInfo>() });
            try { if (result.IsOk || result.Count != 0 || result.Items.Count != 0) throw new Exception("未匹配目标时错误返回成功"); }
            finally { result.ReleaseResources(); }
        }
        Console.WriteLine("Blob多目标通过：独立多ROI、首目标兼容、全目标图像、失败占位、空定位、订阅和重复释放。");
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
                    throw new Exception("多区域并集面积或显示错误：" + result.Area + " " + (result.Items.Count > 0 ? result.Items[0].ErrorMessage : "无目标"));
                using (var form = new NodeParamFormBlobAnalysis())
                {
                    form.GetType().GetMethod("ShowMeasuredArea", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(form, new object[] { result.Area });
                    if (Field<Label>(form, "labelMeasuredArea").Text != "识别区域面积：" + area.ToString("F0") + " 像素²")
                        throw new Exception("参数界面未显示实际识别面积。");
                }
            }
            finally { result.ReleaseResources(); }
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

    /// <summary>真实编辑控件跨图重画、拖动保存后再跟随，避免只有运行ROI正确。</summary>
    private static void VerifyEditingFollow()
    {
        using (var form = new NodeParamFormBlobAnalysis())
        {
            var reference = new PositionCorrectionInfo { IsValid = true, CurrentX = 10, CurrentY = 20 };
            form.Params = new NodeParamBlobAnalysis { EnableDetectionRegion = true, RegionColumn = 40, RegionRow = 50,
                RegionLength1 = 8, RegionLength2 = 4, RegionCorrectionReference = reference,
                CorrectionText1 = "1.定位", CorrectionText2 = "位置修正信息列表" };
            form.SetParam2Form();
            var node = (TDJS_Vision.Node.NodeBase)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(TDJS_Vision.Node.NodeBase));
            var position = new TDJS_Vision.Node._4_Measurement.PositionCorrection.NodeResultPositionCorrection();
            position.Items.Add(new PositionCorrectionInfo { IsValid = true, CurrentX = 110, CurrentY = 70 });
            typeof(TDJS_Vision.Node.NodeBase).GetField("_result", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(node, position);
            var subscription = Field<TDJS_Vision.Node.NodeSubscription>(form, "nodeSubscriptionPositionCorrection");
            typeof(TDJS_Vision.Node.NodeSubscription).GetField("_selectedNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(subscription, node);
            var type = form.GetType();
            var refresh = type.GetMethod("RefreshRoiFromFields", BindingFlags.Instance | BindingFlags.NonPublic);
            refresh.Invoke(form, null);
            var display = Field<BlobDetectionRegion>(form, "_displayedRegion");
            Equal(new PointF(display.Column, display.Row), new PointF(140, 100));
            var snapshot = (NodeParamBlobAnalysis)type.GetMethod("BuildParamFromControls", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { false });
            if (snapshot.Regions[0].Column != 40 || snapshot.Regions[0].CorrectionReference.CurrentX != 10) throw new Exception("仅查看ROI改变原始坐标或基准");
            // 真实动态框模拟用户拖动，随后上游又运行；提交必须仍使用绘制时的位姿快照。
            var control = Field<TDJS_Vision.Forms.DispShowImage.ShowImageControl>(form, "showImageControl1");
            control.ClearDynamicRoi();
            control.AddRoiRotatedRect(105, 147, display.Phi, display.Length1, display.Length2, 1.5f, Color.Blue, "测试ROI");
            position.Items[0].CurrentX = 210;
            type.GetMethod("TryReadRoiToFields", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
            snapshot = (NodeParamBlobAnalysis)type.GetMethod("BuildParamFromControls", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { false });
            if (snapshot.Regions[0].CorrectionReference.CurrentX != 110) throw new Exception("拖动保存错误使用后续运行的位姿");
            refresh.Invoke(form, null);
            display = Field<BlobDetectionRegion>(form, "_displayedRegion");
            Equal(new PointF(display.Column, display.Row), new PointF(247, 105));
            position.Items[0].CurrentAngle = 90;
            position.Items[0].CurrentScaleX = 2; position.Items[0].CurrentScaleY = 2;
            refresh.Invoke(form, null);
            display = Field<BlobDetectionRegion>(form, "_displayedRegion");
            Equal(new PointF(display.Column, display.Row), new PointF(140, 144));
            if (Math.Abs(Math.Max(display.Length1, display.Length2) - 16) > 0.001) throw new Exception("编辑框未随当前定位缩放");

            Console.WriteLine("Blob编辑跟随通过：第二张图位置、无修改保存、拖动回写快照、第三张图继续跟随。");
        }
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
