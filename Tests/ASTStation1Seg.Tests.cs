using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Newtonsoft.Json;
using OpenCvSharp;
using TDJS_Vision;
using TDJS_Vision.Node;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._3_Detection.TDAI.Parse;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

/// <summary>AST分割模型真实掩膜、解析、学习、配置及界面离线回归。</summary>
internal static class ASTStation1SegTests
{
    /// <summary>累计通过的断言数量。</summary>
    private static int _checks;
    /// <summary>测试检测项，与SEG测试程序的两项测量一致。</summary>
    private static readonly string[] Names = { "绝缘胶顶部最低点到屏蔽网顶部最低点的距离", "绝缘胶顶部到铜环底部的距离", "飞丝" };
    /// <summary>失败时明确报告验证场景。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }
    /// <summary>创建有倾斜上边缘和侧壁干扰的真实二值掩膜。</summary>
    private static SegResult Segment(int id, int top, int bottom, float score = 0.9f)
    {
        using (var mask = new Mat(120, 80, MatType.CV_8UC1, Scalar.All(0)))
        {
            for (int x = 10; x < 30; x++)
                for (int y = top + (x < 14 || x >= 26 ? 10 : x % 3); y <= bottom; y++)
                    mask.Set<byte>(y, x, 255);
            return new SegResult { ClassId = id, Score = score, Rect = new Rect(0, 0, 80, 120),
                MaskGeometry = SegMaskGeometry.FromMask(mask, 100, 200) };
        }
    }
    /// <summary>独立参数与检测集合，不修改现场配置。</summary>
    private static NodeParamTDAI Parameters(string name)
    {
        Solution.Instance.DetectItemDic[name] = Names.Select(n => new DetectItemInfo {
            Name = n, Enable = true, MinValue = n == "飞丝" ? "0" : "1",
            MaxValue = n == "飞丝" ? "0" : "1000", IsCountItem = n == "飞丝", CurValue = "0" }).ToList();
        return new NodeParamTDAI { CurDetectItemName = name, DetectItemName1 = name,
            ModelName = ModelName.AST工位1分割模型, StudyNum = 2, StudyPercentage = 10, Scale = 1, NodeName = name };
    }
    /// <summary>运行真实解析器。</summary>
    private static NodeResultTDAI Parse(NodeParamTDAI p, List<SegResult> segments)
    {
        var result = new NodeResultTDAI();
        ASTStation1SegParse.Parse(segments, segments == null ? 0 : segments.Count, p, new TDJS_Vision.Process("SEG离线验证"), ref result);
        return result;
    }
    /// <summary>验证掩膜取点、像素换算、缺失、数量及禁用。</summary>
    private static void VerifyParsing()
    {
        var p = Parameters("SEG解析");
        var segments = new List<SegResult> { Segment(0, 10, 35), Segment(1, 50, 70), Segment(2, 80, 100), Segment(3, 105, 119) };
        Check(segments[0].MaskGeometry.Bounds.Top == 210 && segments[0].MaskGeometry.Bounds.Bottom == 236, "掩膜边界或ROI偏移错误。");
        Check(segments[0].MaskGeometry.UpperEdgeLowestY == 212, "顶部最低点没有排除两侧20%。");
        segments.Add(Segment(0, 0, 25, 0.1f));
        var result = Parse(p, segments);
        Check(result.IsOk && result.DetectItemCount == 3, "分割判定或飞丝零值错误。");
        Check(result.AlgorithmResult.DetectResults[Names[0]][0].Value == "40.00", "最高置信度或中央顶部测量错误。");
        Check(result.AlgorithmResult.DetectResults[Names[1]][0].Value == "90.00", "铜环应使用最后有效像素行，不是排他Bottom。");
        Check(result.AlgorithmResult.Lines.Count == 10 && result.AlgorithmResult.Lines.All(l => l.Color == Color.Yellow), "黄色箭头数量错误。");
        Check(result.AlgorithmResult.Lines[0].P1.Y == 212 && result.AlgorithmResult.Lines[0].P2.Y == 252, "箭头取点与第一项测量不一致。");
        Check(result.AlgorithmResult.Lines[5].P1.Y == 210 && result.AlgorithmResult.Lines[5].P2.Y == 300, "箭头取点与第二项测量不一致。");
        Check(result.AlgorithmResult.Rects.Count == 5 && result.AlgorithmResult.Contours.Count == 0,
            "SEG显示层应只输出检测矩形框，不输出轮廓。");
        Check(result.AlgorithmResult.Rects[0].RotatedRect.size.width == segments[0].Rect.Width &&
            result.AlgorithmResult.Rects[0].RotatedRect.size.height == segments[0].Rect.Height,
            "显示矩形框应使用模型检测框尺寸。");
        Check(!result.AlgorithmResult.Texts.Any(t => t.Color == Color.Yellow) &&
            result.AlgorithmResult.Texts.Any(t => t.Text.Contains("40.00mm")) &&
            !result.AlgorithmResult.Texts.Any(t => t.Text.Contains("像素")),
            "黄色数字未移除，或非数量型检测项没有统一显示mm。");
        string flyingWireDisplayName = ParseCommon.GetDisplayName(Names[2]);
        Check(result.AlgorithmResult.Texts.Any(t => t.Text.Contains(flyingWireDisplayName) && t.Text.EndsWith(": 0")),
            "数量型检测项不应追加mm单位。");
        p.NeedConvert = true; p.Scale = 0.1f;
        result = Parse(p, segments);
        Check(result.AlgorithmResult.DetectResults[Names[1]][0].Value == "9.00" && result.AlgorithmResult.Texts.Any(t => t.Text.Contains("9.00mm")), "毫米换算错误。");
        segments.Add(Segment(4, 40, 60));
        result = Parse(p, segments);
        Check(!result.IsOk && result.AlgorithmResult.DetectResults["DetectItem.FlyingWire"][0].Value == "1", "飞丝类别没有按标签4判定。");
        Check(result.AlgorithmResult.Rects.Last().Color == Color.Red,
            "飞丝NG目标的矩形框颜色错误。");
        // 铜环掩膜底边为301：完全位于其下方的飞丝忽略，跨越底边的飞丝仍计数报警。
        var belowCopper = Segment(4, 105, 119);
        result = Parse(p, new List<SegResult> { Segment(2, 80, 100), belowCopper });
        Check(result.AlgorithmResult.DetectResults["DetectItem.FlyingWire"][0].IsOk &&
            result.AlgorithmResult.DetectResults["DetectItem.FlyingWire"][0].Value == "0" &&
            result.AlgorithmResult.Rects.All(r => r.RotatedRect.center.y < belowCopper.MaskGeometry.Bounds.Top),
            "铜环底边以下的飞丝没有被筛除或仍被绘制。");
        var copperWithTrailingNoise = Segment(2, 80, 100);
        copperWithTrailingNoise.MaskGeometry.Bounds = new Rect(
            copperWithTrailingNoise.MaskGeometry.Bounds.X,
            copperWithTrailingNoise.MaskGeometry.Bounds.Y,
            copperWithTrailingNoise.MaskGeometry.Bounds.Width,
            100);
        result = Parse(p, new List<SegResult> { copperWithTrailingNoise, belowCopper });
        Check(result.AlgorithmResult.DetectResults["DetectItem.FlyingWire"][0].IsOk &&
            result.AlgorithmResult.DetectResults["DetectItem.FlyingWire"][0].Value == "0",
            "铜环掩膜拖尾把有效底边拉低后，底部飞丝仍被误报。");
        var crossingCopperBottom = Segment(4, 95, 110);
        result = Parse(p, new List<SegResult> { Segment(2, 80, 100), crossingCopperBottom });
        Check(!result.IsOk && result.AlgorithmResult.DetectResults["DetectItem.FlyingWire"][0].Value == "1",
            "跨越铜环底边的飞丝被错误屏蔽。");
        result = Parse(p, new List<SegResult> { belowCopper });
        Check(!result.IsOk && result.AlgorithmResult.DetectResults["DetectItem.FlyingWire"][0].Value == "1",
            "未检出铜环时不应屏蔽飞丝。");
        segments.RemoveAll(r => r.ClassId == 1);
        result = Parse(p, segments);
        Check(!result.IsOk && result.AlgorithmResult.Lines.Count == 5 &&
            result.AlgorithmResult.Texts.Any(t => t.Text.EndsWith(": 0mm")) &&
            !result.AlgorithmResult.Texts.Any(t => t.Text.Contains("无法测量")),
            "缺少屏蔽网时未显示0mm，或影响其它测量。");
        result = Parse(p, new List<SegResult> { new SegResult { ClassId = 0, Rect = new Rect(1, 2, 40, 50) } });
        Check(!result.IsOk && result.AlgorithmResult.Lines.Count == 0 && result.AlgorithmResult.Rects.Count == 0,
            "没有掩膜时错误地使用检测框测量或显示无效目标。");
        result = Parse(p, new List<SegResult> { new SegResult { ClassId = 4, Rect = new Rect(1, 2, 3, 4) } });
        Check(!result.AlgorithmResult.DetectResults["DetectItem.FlyingWire"][0].IsOk, "飞丝缺少掩膜不应伪判为零个OK。");
        Solution.Instance.DetectItemDic[p.CurDetectItemName][0].Enable = false;
        result = Parse(p, segments);
        Check(!result.AlgorithmResult.DetectResults.ContainsKey(Names[0]), "禁用项仍执行检测。");
        using (var empty = new Mat(5, 5, MatType.CV_8UC1, Scalar.All(0)))
            Check(SegMaskGeometry.FromMask(empty) == null, "空掩膜产生伪几何。");
        Check(SegMaskGeometry.FromMask(null) == null, "空图像产生伪几何。");
        var all = Parameters("SEG全部检测项");
        var template = JsonConvert.DeserializeObject<List<DetectItemInfo>>(File.ReadAllText("Template/TDAI/AST工位1分割检测项.items"));
        foreach (var item in template) item.Enable = true;
        Solution.Instance.DetectItemDic[all.CurDetectItemName] = template;
        result = Parse(all, new List<SegResult> { Segment(0, 10, 35), Segment(1, 50, 70), Segment(2, 80, 100), Segment(3, 105, 119), Segment(5, 20, 60) });
        Check(result.IsOk && result.DetectItemCount == 15 && result.AlgorithmResult.Lines.Count == 70, "全部检测项或铝箔箭头缺失。");
        Check(result.AlgorithmResult.DetectResults["护套剥离长度"][0].Value == "95.00", "护套剥离长度未按胶皮顶部最低点到绝缘体顶部最低点计算。");
    }
    /// <summary>验证铝箔掩膜尺寸、最高置信度选择、补偿、缺失和学习。</summary>
    private static void VerifyFoilMeasurements()
    {
        var p = Parameters("铝箔尺寸");
        var length = new DetectItemInfo { Name = "铝箔长度", Enable = true, MinValue = "1", MaxValue = "100" };
        var width = new DetectItemInfo { Name = "铝箔宽度", Enable = true, MinValue = "1", MaxValue = "100" };
        Solution.Instance.DetectItemDic[p.CurDetectItemName] = new List<DetectItemInfo> { length, width };
        var segments = new List<SegResult> { Segment(5, 20, 60), Segment(5, 10, 90, 0.1f), Segment(0, 0, 100) };
        var result = Parse(p, segments);
        Check(result.IsOk && result.DetectItemCount == 2, "铝箔类别5未参与检测。");
        Check(length.CurValue == "41.00" && width.CurValue == "20.00", "铝箔未取最高置信度掩膜宽高，或误用了检测框。");
        Check(result.AlgorithmResult.Lines.Count == 10, "铝箔长宽箭头缺失。");
        p.NeedConvert = true; p.Scale = 0.1f; length.Compensation = 0.5f;
        Parse(p, segments);
        Check(length.CurValue == "4.60" && width.CurValue == "2.00", "铝箔换算或补偿顺序错误。");
        width.MaxValue = "1";
        result = Parse(p, segments);
        Check(!result.IsOk && result.AlgorithmResult.Rects[0].Color == Color.Red, "铝箔宽度超限未判NG或标红。");
        width.Enable = false;
        Check(!Parse(p, segments).AlgorithmResult.DetectResults.ContainsKey("铝箔宽度"), "禁用铝箔项仍输出。");
        width.Enable = true; width.MaxValue = "100";
        result = Parse(p, new List<SegResult> { new SegResult { ClassId = 5, Rect = new Rect(0, 0, 80, 120) } });
        Check(!result.IsOk && length.CurValue == "0" && width.CurValue == "0" && result.AlgorithmResult.Lines.Count == 0, "无掩膜时错误使用矩形或补偿伪造铝箔尺寸。");
        length.MinValue = width.MinValue = "0";
        result = Parse(p, new List<SegResult> { new SegResult { ClassId = 5, Rect = new Rect(0, 0, 80, 120) } });
        Check(result.IsOk && length.CurValue == "0" && width.CurValue == "0" && result.AlgorithmResult.Lines.Count == 0,
            "无有效掩膜且范围包含0时应判OK，缺失值不应补偿或绘制箭头。");
        Check(Parse(p, null).IsOk, "未检出铝箔且范围包含0时应判OK。");
        p.IsAutoStudy = true; p.StudyNum = 1;
        Check(Parse(p, null).IsOk && length.MaxValue == "100", "缺失零值应按范围判定且不污染学习。");
        length.MinValue = width.MinValue = "1";
        p.IsAutoStudy = true; p.StudyNum = 1;
        result = Parse(p, null);
        Check(!result.IsOk && length.MaxValue == "100", "缺失铝箔未判NG或污染学习。");
        p.NeedConvert = false; length.Compensation = 0; p.IsAutoStudy = true;
        Parse(p, segments);
        Check(length.MinValue == "36.90" && width.MinValue == "18.00", "铝箔自动学习范围错误。");
        Check(ASTStation1SegParse.ClassNames.Count == 6 && ASTStation1SegParse.ClassNames[5] == "aluminum_foil", "铝箔训练标签下标错误。");
    }

    /// <summary>屏蔽网高度使用胶皮和屏蔽网中央上边缘最低点，不使用自身高度或相邻间距。</summary>
    private static void VerifyShieldingHeight()
    {
        const string name = "L2_编组切割长度";
        var p = Parameters("屏蔽网高度取点");
        var item = new DetectItemInfo { Name = name, Enable = true, MinValue = "0", MaxValue = "1000" };
        Solution.Instance.DetectItemDic[p.CurDetectItemName] = new List<DetectItemInfo> { item };
        var mesh = Segment(1, 50, 70);
        var rubber = Segment(3, 105, 119);
        var segments = new List<SegResult> { mesh, rubber, Segment(3, 80, 100, 0.1f) };
        var result = Parse(p, segments);
        Check(result.IsOk && result.AlgorithmResult.DetectResults[name][0].Value == "55.00",
            "屏蔽网高度未采用最高置信度胶皮到屏蔽网顶部最低点距离。");
        Check(result.AlgorithmResult.Lines.Count == 5 && result.AlgorithmResult.Lines[0].P1.Y == 252 &&
            result.AlgorithmResult.Lines[0].P2.Y == 307, "屏蔽网高度箭头与最低点不一致。");
        p.NeedConvert = true; p.Scale = 0.1f;
        Check(Parse(p, segments).AlgorithmResult.DetectResults[name][0].Value == "5.50", "屏蔽网高度比例换算错误。");
        item.MaxValue = "5";
        result = Parse(p, segments);
        Check(!result.IsOk && result.AlgorithmResult.Rects.All(r => r.Color == Color.Red), "距离超限未将胶皮和屏蔽网标红。");
        p.NeedConvert = false; item.MaxValue = "1000";
        segments = new List<SegResult> { mesh, Segment(3, 40, 65) };
        Check(Parse(p, segments).AlgorithmResult.DetectResults[name][0].Value == "10.00", "重叠或反向时应继续计算最低点差绝对值。");
        segments = new List<SegResult> { mesh, Segment(3, 50, 65) };
        Check(Parse(p, segments).IsOk && item.CurValue == "0", "顶部最低点同高应为有效零值。");
        foreach (var missing in new[] { new List<SegResult> { mesh }, new List<SegResult> { rubber } })
        {
            result = Parse(p, missing);
            Check(result.IsOk && item.CurValue == "0" && result.AlgorithmResult.Lines.Count == 0,
                "缺少胶皮或屏蔽网且范围包含0时应判OK，不绘制箭头。");
        }
        rubber.MaskGeometry.UpperEdgeLowestY = -1;
        Check(Parse(p, new List<SegResult> { mesh, rubber }).IsOk, "缺少有效顶部取点时应按零值范围判OK。");
        item.Name = "屏蔽网高度";
        Check(Parse(p, new List<SegResult> { mesh, Segment(3, 105, 119) })
            .AlgorithmResult.DetectResults[item.Name][0].Value == "55.00", "旧名称的测量逻辑不兼容。");
    }
    /// <summary>验证套筒压接位置固定使用铜环顶部最高点和胶皮顶部最低点，不使用相邻底边。</summary>
    private static void VerifyCopperTopDistance()
    {
        var p = Parameters("铜环顶部距离");
        var item = new DetectItemInfo { Name = "套筒压接位置", Enable = true, MinValue = "0", MaxValue = "1000" };
        Solution.Instance.DetectItemDic[p.CurDetectItemName] = new List<DetectItemInfo> { item };
        var copper = Segment(2, 50, 70);
        var rubber = Segment(3, 105, 119);
        var segments = new List<SegResult> { copper, rubber, Segment(2, 10, 30, 0.1f) };
        var result = Parse(p, segments);
        Check(result.IsOk && item.CurValue == "57.00", "套筒压接位置未使用最高置信度铜环与胶皮顶部最低点。");
        Check(result.AlgorithmResult.Lines.Count == 5 && result.AlgorithmResult.Lines[0].P1.Y == 250 &&
            result.AlgorithmResult.Lines[0].P2.Y == 307, "套筒压接位置箭头端点与顶部最低点不一致。");
        p.NeedConvert = true; p.Scale = 0.1f; item.Compensation = 0.5f;
        Parse(p, segments);
        Check(item.CurValue == "6.20", "套筒压接位置换算或补偿错误。");
        p.NeedConvert = false; item.Compensation = 0;
        Parse(p, new List<SegResult> { copper, Segment(3, 40, 65) });
        Check(item.CurValue == "8.00", "套筒压接位置重叠和反向时应保持指定顶部取点Y差绝对值。");
        Parse(p, new List<SegResult> { copper, Segment(3, 48, 65) });
        Check(item.CurValue == "0", "套筒压接位置顶部同高应为0。");
        foreach (var missing in new[] { new List<SegResult> { copper }, new List<SegResult> { rubber } })
        {
            result = Parse(p, missing);
            Check(result.IsOk && item.CurValue == "0" && result.AlgorithmResult.Lines.Count == 0,
                "套筒压接位置缺失目标应按零值判定且不绘制箭头。");
        }
        // 铜环最低点数据不再参与该项测量；真实掩膜最高点仍有效。
        copper.MaskGeometry.UpperEdgeLowestY = -1;
        Check(Parse(p, segments).IsOk && item.CurValue == "57.00", "铜环最低点不应影响最高点测量。");
        rubber.MaskGeometry.UpperEdgeLowestY = -1;
        Check(Parse(p, new List<SegResult> { copper, rubber }).IsOk && item.CurValue == "0", "套筒压接位置无效取点应输出0。");
        item.MinValue = "1";
        Check(!Parse(p, new List<SegResult> { copper, rubber }).IsOk, "套筒压接位置缺失零值应受下限约束。");
        item.Name = "胶皮到铜环的距离";
        Parse(p, new List<SegResult> { copper, Segment(3, 105, 119) });
        Check(item.CurValue == "57.00", "套筒压接位置旧名称未采用新测量口径。");
    }

    /// <summary>学习会话按节点隔离，无效缺失值不进入样本。</summary>
    private static void VerifyLearning()
    {
        var a = Parameters("SEG学习A"); var b = Parameters("SEG学习B");
        a.IsAutoStudy = true; b.IsAutoStudy = true;
        var segments = new List<SegResult> { Segment(0, 10, 35), Segment(1, 50, 70), Segment(2, 80, 100) };
        Parse(a, segments); Parse(b, segments);
        Check(a.IsAutoStudy && b.IsAutoStudy, "四AI节点学习进度串扰。");
        Parse(a, segments);
        Check(!a.IsAutoStudy && b.IsAutoStudy, "节点学习会话没有隔离。");
        Check(Solution.Instance.DetectItemDic[a.CurDetectItemName][0].MinValue == "36.00", "有效掩膜距离未参与学习。");
        Parse(b, null);
        Check(Solution.Instance.DetectItemDic[b.CurDetectItemName][0].MinValue == "36.00", "缺失零值污染学习公差。");
    }
    /// <summary>验证真正worker响应序列化保留掩膜几何，不仅测试普通对象。</summary>
    private static void VerifyTransport()
    {
        var assembly = typeof(NodeParamTDAI).Assembly;
        var worker = assembly.GetType("TDJS_Vision.Node._3_Detection.TDAI.Yolo8.YoloIsolatedWorkerProgram");
        var method = worker.GetMethod("SuccessWithSeg", BindingFlags.Static | BindingFlags.NonPublic);
        var response = method.Invoke(null, new object[] { new List<SegResult> { Segment(0, 10, 35) } });
        var restored = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(response), response.GetType());
        object dto = ((System.Collections.IEnumerable)restored.GetType().GetProperty("SegResults").GetValue(restored)).Cast<object>().Single();
        var geometry = (SegMaskGeometry)dto.GetType().GetProperty("MaskGeometry").GetValue(dto);
        Check(geometry.Bounds == new Rect(110, 210, 20, 26) && geometry.UpperEdgeLowestY == 212 && geometry.Contours[0].Length > 0,
            "worker传输丢失掩膜取点或轮廓。");
    }
    /// <summary>配置及实际参数窗体可选择、保存与恢复新枚举。</summary>
    private static void VerifyConfiguration()
    {
        string source = File.ReadAllText("Template/TDAI/AST工位1分割模型配置.json");
        string decoded = StringCipher.Decrypt(File.ReadAllText("Template/TDAI/AST工位1分割模型.ai"));
        Check(source == decoded, "导入配置与可读配置不一致。");
        var config = JsonConvert.DeserializeObject<AIInputInfo>(source);
        Check(config.ModelInfo.ModelType == ModelType.SEG && config.ModelSize == 640 && config.ClassNames.SequenceEqual(ASTStation1SegParse.ClassNames), "配置类型或标签顺序错误。");
        using (var form = new ParamFormTDAI())
        {
            form.SetNodeBelong(new NodeBase(1, "SEG验证", new TDJS_Vision.Process("验证"), NodeType.AITD));
            var combo = (ComboBox)typeof(ParamFormTDAI).GetField("comboBoxModelName", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            Check(combo.Items.Contains("AST工位1分割模型"), "Designer缺少新模型选项。");
            form.Params = new NodeParamTDAI { ModelName = ModelName.AST工位1分割模型 };
            var gate = typeof(NodeParamTDAI).Assembly.GetType("TDJS_Vision.Startup.StartupAiRuntimeLoadGate");
            using ((IDisposable)gate.GetMethod("BeginTDAIDeferral").Invoke(null, null)) form.SetParam2Form();
            Check(combo.Text == "AST工位1分割模型", "模型名称恢复错误。");
            var p = Parameters("SEG界面保存");
            p.IsFixed = true;
            form.Params = p;
            using ((IDisposable)gate.GetMethod("BeginTDAIDeferral").Invoke(null, null)) form.SetParam2Form();
            combo.SelectedItem = "AST工位1分割模型";
            typeof(ParamFormTDAI).GetField("aIInputInfo", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(form,
                new AIInputInfo { ModelSize = 640, ClassNames = ASTStation1SegParse.ClassNames.ToList(),
                    ModelInfo = new ModelInfo { ModelType = ModelType.SEG, ModelPath = "不存在的SEG验证模型" } });
            ((TextBox)typeof(ParamFormTDAI).GetField("textBoxConfigPath", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form)).Text = "SEG验证配置";
            var subscription = typeof(ParamFormTDAI).GetField("nodeSubscription1", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            subscription.GetType().GetMethod("SetText").Invoke(subscription, new object[] { "1.图像源", "图像" });
            Check((bool)typeof(ParamFormTDAI).GetMethod("SaveParams", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null), "新模型界面保存失败。");
            var saved = (NodeParamTDAI)form.Params;
            saved.ModelLoadTask.Wait();
            Check(saved.ModelName == ModelName.AST工位1分割模型 && saved.AIInputInfo.ModelInfo.ModelType == ModelType.SEG, "保存时丢失新模型枚举或SEG类型。");
            var restored = JsonConvert.DeserializeObject<NodeParamTDAI>(JsonConvert.SerializeObject(saved));
            Check(restored.ModelName == ModelName.AST工位1分割模型, "方案序列化丢失新模型名称。");
        }
    }
    /// <summary>生成加密配置或运行离线验证；推理模式加载真实CPU模型但不连接设备。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            // 测试宿主也必须接管worker入口，避免子进程把测试输出误当JSON协议。
            if (args.Contains("--tdjs-yolo-worker"))
            {
                var worker = typeof(NodeParamTDAI).Assembly.GetType("TDJS_Vision.Node._3_Detection.TDAI.Yolo8.YoloIsolatedWorkerProgram");
                worker.GetMethod("TryRun").Invoke(null, new object[] { args });
                return 0;
            }
            if (args.Length > 0 && args[0] == "--prepare")
            {
                File.WriteAllText(args[2], StringCipher.Encrypt(File.ReadAllText(args[1])));
                return 0;
            }
            if (args.Length > 0 && args[0] == "--infer")
            {
                var config = JsonConvert.DeserializeObject<AIInputInfo>(File.ReadAllText("Template/TDAI/AST工位1分割模型配置.json"));
                var engine = new Yolo8Seg();
                try
                {
                    engine.Init(config.ModelInfo.ModelPath, DeviceType.CPU, config.ClassNames.ToArray(), 640, 0.25f, 0.45f);
                    using (var image = args.Length > 1 ? Cv2.ImRead(args[1]) : new Mat(480, 800, MatType.CV_8UC3, Scalar.All(127)))
                    {
                        var results = engine.Detect(image);
                        Console.WriteLine("真实CPU SEG模型加载和推理通过，目标数量：" + results.Count + "；有效掩膜：" + results.Count(r => r.MaskGeometry != null));
                    }
                }
                finally { engine.Destroy(); }
                return 0;
            }
            VerifyParsing(); VerifyFoilMeasurements(); VerifyShieldingHeight(); VerifyCopperTopDistance(); VerifyLearning(); VerifyTransport();
            Console.WriteLine("SEG解析、取点、学习及传输验证通过，共" + _checks + "项断言；继续验证模型配置。");
            VerifyConfiguration();
            Console.WriteLine("AST工位1分割离线验证通过，共" + _checks + "项断言。");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
