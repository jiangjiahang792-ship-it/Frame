using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Newtonsoft.Json;
using OpenCvSharp;
using TDJS_Vision;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._3_Detection.TDAI.Parse;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

/// <summary>使用真实解析器验证新模型的类别、数量约束、方向及模板。</summary>
internal static class NewTerminal12Tests
{
    /// <summary>通过的断言数。</summary>
    private static int checks;
    /// <summary>验证行为并报告失败原因。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    /// <summary>建立启用的检测项。</summary>
    private static DetectItemInfo Item(string name, string min = "0", string max = "0")
    {
        return new DetectItemInfo { Name = name, MinValue = min, MaxValue = max, Enable = true };
    }
    /// <summary>创建非正方形检测框。</summary>
    private static DetResult Box(int id, int x = 0, int y = 0, int width = 10, int height = 5)
    {
        return new DetResult { ClassId = id, Box = new Rect(x, y, width, height) };
    }
    /// <summary>执行真实解析而不加载设备或模型权重。</summary>
    private static NodeResultTDAI Parse(List<DetectItemInfo> items, List<DetResult> boxes, bool study = false, bool convert = false)
    {
        Solution.Instance.DetectItemDic["新端子离线测试"] = items;
        var param = new NodeParamTDAI { CurDetectItemName = "新端子离线测试", NodeName = "新端子离线测试",
            IsAutoStudy = study, StudyNum = 1, StudyPercentage = 10, NeedConvert = convert, Scale = 0.5f };
        var result = new NodeResultTDAI();
        NewTerminal12Parse.Parse(boxes, boxes.Count, param, new TDJS_Vision.Process("新端子离线测试"), ref result);
        return result;
    }
    /// <summary>读取一项真实结果。</summary>
    private static SingleDetectResult Value(NodeResultTDAI result, string name)
    {
        return result.AlgorithmResult.DetectResults[DetectItemLanguage.NormalizeName(name)][0];
    }
    /// <summary>验证横向线芯方向、独立反向计数、缺侧及一对一配对。</summary>
    private static void Direction()
    {
        var cases = new[] {
            new { Boxes = new List<DetResult> { Box(5, 20), Box(6), Box(8, 40) }, Expected = 0 },
            new { Boxes = new List<DetResult> { Box(5), Box(6, 20), Box(8, -20) }, Expected = 0 },
            new { Boxes = new List<DetResult> { Box(5), Box(6, 20), Box(8, 40) }, Expected = 1 },
            new { Boxes = new List<DetResult> { Box(5, 20), Box(6), Box(8, -20) }, Expected = 1 },
            new { Boxes = new List<DetResult> { Box(5) }, Expected = 1 },
            new { Boxes = new List<DetResult> { Box(6) }, Expected = 1 },
            new { Boxes = new List<DetResult>(), Expected = 1 },
            new { Boxes = new List<DetResult> { Box(5), Box(6), Box(8, 40) }, Expected = 1 },
            new { Boxes = new List<DetResult> { Box(5, 20), Box(6) }, Expected = 1 },
            new { Boxes = new List<DetResult> { Box(5, 40), Box(6), Box(8, 20) }, Expected = 1 },
            new { Boxes = new List<DetResult> { Box(5, 20), Box(6), Box(8, 40, 100) }, Expected = 1 },
            new { Boxes = new List<DetResult> { Box(5), Box(6, 20), Box(8, 40), Box(5, 20, 100), Box(6, 0, 100), Box(8, 40, 100) }, Expected = 1 },
            new { Boxes = new List<DetResult> { Box(5, 20), Box(6), Box(8, 40), Box(5, 20, 100) }, Expected = 1 },
            new { Boxes = new List<DetResult> { Box(5), Box(6, 20), Box(8, 40), Box(5, 0, 100), Box(6, 20, 100), Box(8, 40, 100) }, Expected = 2 },
            new { Boxes = new List<DetResult> { Box(5, 20), Box(6), Box(5, 120), Box(6, 100), Box(8, 140) }, Expected = 1 },
            // 近似用户截图：右侧较窄前段、左侧带褶皱后段，两根线芯均在右侧。
            new { Boxes = new List<DetResult> { Box(5, 354, 183, 144, 176), Box(6, 150, 132, 204, 264), Box(8, 536, 254, 160, 47),
                Box(5, 376, 460, 138, 179), Box(6, 169, 441, 207, 269), Box(8, 560, 511, 154, 52) }, Expected = 0 }
        };
        foreach (var test in cases)
        {
            var items = new List<DetectItemInfo> { Item("防水栓正反"), Item("端子数量", "9", "9") };
            var result = Parse(items, test.Boxes);
            Check(Value(result, "防水栓正反").Value == test.Expected.ToString(), "正反计数错误");
            Check(Value(result, "防水栓正反").IsOk == (test.Expected == 0), "正反受到端子数量影响");
            Check(items[0].IsCountItem, "正反必须为数量型");
            test.Boxes.Reverse();
            Check(Value(Parse(items, test.Boxes), "防水栓正反").Value == test.Expected.ToString(), "输入顺序改变配对结果");
        }
    }
    /// <summary>验证新类别映射及独立计数。</summary>
    private static void Mapping()
    {
        var names = new[] { "前防水栓数量", "后防水栓数量", "胶皮数量", "线芯数量", "飞丝", "飞胶", "防水栓破损" };
        for (int id = 5; id <= 11; id++)
        {
            var items = names.Select(n => Item(n, "1", "1")).ToList();
            items.Add(Item("端子数量", "8", "8"));
            var boxes = new List<DetResult> { Box(id) };
            if (id == 7) { boxes.Add(Box(8, -20)); boxes.Add(Box(5, 20)); }
            var result = Parse(items, boxes);
            for (int index = 0; index < names.Length; index++)
            {
                var value = Value(result, names[index]);
                bool present = index == id - 5 || (id == 7 && (index == 0 || index == 3));
                Check(value.Value == (present ? "1" : "0"), "类别混淆：" + names[index]);
                Check(value.IsOk == present, "独立数量判定错误");
            }
        }
        var dimensions = new[] { new { Name = "前防水栓长度", Id = 5, Value = "10" },
            new { Name = "后防水栓高度", Id = 6, Value = "5" }, new { Name = "穿塞宽度", Id = 7, Value = "10" },
            new { Name = "线芯长度", Id = 8, Value = "10" }, new { Name = "前防水栓面积", Id = 5, Value = "50" } };
        foreach (var test in dimensions)
        {
            var items = new List<DetectItemInfo> { Item(test.Name, test.Value, test.Value), Item("端子数量", "2", "2") };
            var boxes = new List<DetResult> { Box(test.Id) };
            if (test.Id == 7) { boxes.Add(Box(8, -20)); boxes.Add(Box(5, 20)); }
            var result = Parse(items, boxes);
            Check(float.Parse(Value(result, test.Name).Value) == float.Parse(test.Value), "尺寸提取错误");
            Check(Value(result, test.Name).IsOk && !result.IsOk && items[0].CurValue.EndsWith(",0"), "缺件未分离为补零NG");
            items[1].MinValue = items[1].MaxValue = "1";
            Check(Parse(items, boxes).IsOk, "尺寸合格仍NG");
        }
        var defects = new List<DetectItemInfo> { Item("防水栓破损"), Item("防水栓正反") };
        Parse(defects, new List<DetResult> { Box(11), Box(5, 20), Box(6) }, true);
        Check(defects.All(i => i.MinValue == "0" && i.MaxValue == "0"), "学习覆盖缺陷0～0约束");
        var bite = new List<DetectItemInfo> { Item("咬胶长度", "50", "50") };
        Check(Parse(bite, new List<DetResult> { Box(3), Box(1, 30), Box(4) }, false, true).IsOk, "咬胶百分比被物理比例改变");
        Check(!Parse(bite, new List<DetResult> { Box(3), Box(1, 30) }).IsOk, "咬胶缺依赖误判OK");
    }
    /// <summary>缺件由补零结果判NG，实际合格框保持绿色，缺失值不污染学习。</summary>
    private static void MissingMeasurements()
    {
        var items = new List<DetectItemInfo> { Item("端子数量", "2", "2"), Item("穿塞宽度", "1", "2"), Item("穿塞高度", "1", "6") };
        var boxes = new List<DetResult> { Box(8), Box(5, 60), Box(7, 20, 0, 1, 5) };
        var result = Parse(items, boxes);
        var values = result.AlgorithmResult.DetectResults["穿塞宽度"];
        Check(items[1].CurValue == "1.00,0" && items[2].CurValue == "5.00,0", "未按缺件数量补零");
        Check(values.Count == 2 && values[0].IsOk && !values[1].IsOk && !result.IsOk, "缺件放行或实际值误判");
        Check(result.AlgorithmResult.Rects.Count == 1 && result.AlgorithmResult.Rects[0].Color == System.Drawing.Color.Green, "缺件把合格框染红或生成虚假框");
        Check(result.AlgorithmResult.Texts.Any(t => t.Text.Contains("1.00,0") && t.Color == System.Drawing.Color.Red), "缺件检测项文字未标NG");
        result = Parse(items, new List<DetResult>());
        Check(items[1].CurValue == "0,0" && result.AlgorithmResult.Rects.Count == 0, "全部缺失未补齐或出现虚假框");
        items[1].MinValue = "0";
        result = Parse(items, boxes);
        Check(!result.IsOk && !result.AlgorithmResult.DetectResults["穿塞宽度"][1].IsOk, "零在尺寸范围内导致缺件被放行");
        items[0].Enable = false;
        Check(Parse(items, boxes).IsOk && items[1].CurValue == "1.00", "禁用公共数量仍补零");
        items[0].Enable = true; items[0].MinValue = "1"; items[0].MaxValue = "3";
        Check(Parse(items, boxes).IsOk && items[1].CurValue == "1.00", "按公共上限错误补零");
        items[0].MinValue = "2"; items[0].MaxValue = "2";
        items[1].MaxValue = "0.5";
        Check(Parse(items, boxes).AlgorithmResult.Rects[0].Color == System.Drawing.Color.Red, "实际尺寸超限框未标红");
        items[1].MinValue = "1"; items[1].MaxValue = "2";
        Parse(items, boxes, true);
        Check(float.Parse(items[1].MinValue) == 0.9f && float.Parse(items[1].MaxValue) == 1.1f, "缺失零污染学习样本");
        var extra = new List<DetectItemInfo> { Item("端子数量", "1", "1"), Item("线芯长度", "10", "10") };
        Check(!Parse(extra, new List<DetResult> { Box(8), Box(8, 30) }).IsOk, "数量超上限被放行");
    }
    /// <summary>验证位置过滤统一影响尺寸、数量、绘图、公共计数及学习，且不影响其它类别。</summary>
    private static void RubberFiltering()
    {
        var boxes = new List<DetResult> { Box(8, 0), Box(5, 60), Box(7, 20),
            Box(7, -30), Box(7, 80), Box(7, 20, 50), Box(10, 80), Box(3, 80) };
        var items = new List<DetectItemInfo> { Item("胶皮数量", "1", "1"), Item("穿塞宽度", "10", "10"),
            Item("穿塞高度", "5", "5"), Item("端子数量", "1", "1"), Item("飞胶", "1", "1") };
        var result = Parse(items, boxes);
        Check(result.IsOk, "区域外胶皮影响整体判定");
        Check(Value(result, "胶皮数量").Value == "1", "区域外胶皮参与计数");
        Check(Value(result, "端子数量").Value == "1", "区域外胶皮污染公共数量");
        Check(result.AlgorithmResult.DetectResults["穿塞宽度"].Count == 1, "区域外胶皮参与尺寸检测");
        Check(result.AlgorithmResult.Rects.Count == 2, "区域外胶皮仍绘图或其它类别被过滤");
        Check(boxes.Count == 8, "过滤修改了输入列表");
        // 前后定位项目可以不启用，原始模型框仍应作为位置依据。
        boxes.Reverse();
        Check(Parse(items, boxes).IsOk, "筛选受输入顺序影响");
        var gaps = new[] {
            new List<DetResult> { Box(7, 20), Box(8) },
            new List<DetResult> { Box(7, 20), Box(5, 60) },
            new List<DetResult> { Box(7, 20) },
            new List<DetResult> { Box(7, 20), Box(8), Box(5, 5) },
            new List<DetResult> { Box(7, 20), Box(8), Box(5, 10) },
            new List<DetResult> { Box(7, 20), Box(8), Box(5, 60, 50) },
            new List<DetResult> { Box(7, 20), Box(8), Box(6, 60) }
        };
        foreach (var gap in gaps)
        {
            result = Parse(items, gap);
            Check(Value(result, "胶皮数量").Value == "0", "无有效定位仍保留胶皮");
            Check(Value(result, "穿塞宽度").Value == "0" && !Value(result, "穿塞宽度").IsOk, "缺失胶皮未清零或未判NG");
        }
        var rubberOnly = new List<DetectItemInfo> { Item("胶皮数量", "1", "1"), Item("穿塞宽度", "10", "10") };
        Check(Parse(rubberOnly, new List<DetResult> { Box(5), Box(8, 60), Box(7, 20) }).IsOk, "反向摆放的间隔筛选失败");
        Check(Parse(rubberOnly, new List<DetResult> { Box(8), Box(5, 60), Box(7, 5) }).IsOk, "边界中心未保留或原尺寸被裁剪");
        Check(!Parse(rubberOnly, new List<DetResult> { Box(8), Box(5, 60), Box(7, 4) }).IsOk, "中心位于间隔外仍保留");
        var multiple = new List<DetResult> { Box(8), Box(5, 60), Box(7, 20),
            Box(8, 0, 50), Box(5, 60, 50), Box(7, 20, 50), Box(7, 20, 25), Box(7, 100, 50) };
        var counts = new List<DetectItemInfo> { Item("胶皮数量", "2", "2") };
        Check(Parse(counts, multiple).IsOk, "多行胶皮筛选失败");
        // 同行两组不能使用跨组的大区间接纳中间背景胶皮。
        multiple = new List<DetResult> { Box(8), Box(5, 60), Box(7, 20), Box(8, 100), Box(5, 160), Box(7, 120), Box(7, 80) };
        Check(Parse(counts, multiple).IsOk, "同行多组跨组误匹配");
        var learn = new List<DetectItemInfo> { Item("穿塞宽度", "1", "99999") };
        Parse(learn, new List<DetResult> { Box(8), Box(5, 60), Box(7, 20), Box(7, 100, 0, 200) }, true);
        Check(float.Parse(learn[0].MinValue) == 9 && float.Parse(learn[0].MaxValue) == 11, "区域外胶皮污染学习范围");
        // 近似现场截图：左侧正常、右侧防水栓反向，右下尾胶仍必须排除。
        var screenshot = new List<DetResult> {
            Box(8, 195, 250, 55, 160), Box(5, 159, 487, 160, 145), Box(6, 136, 632, 227, 192),
            Box(7, 195, 410, 68, 77), Box(7, 204, 824, 60, 55),
            Box(8, 538, 298, 48, 156), Box(5, 486, 716, 175, 142), Box(6, 438, 526, 260, 191),
            Box(7, 534, 454, 65, 73), Box(7, 540, 858, 70, 18) };
        for (int rotation = 0; rotation < 4; rotation++)
        {
            result = Parse(counts, screenshot);
            Check(Value(result, "胶皮数量").Value == "2" && result.IsOk, "现场布局旋转后保留尾胶或丢失有效胶皮");
            Check(result.AlgorithmResult.Rects.Count == 2, "现场布局旋转后尾胶仍绘制");
            screenshot = screenshot.Select(r => Box(r.ClassId, 1200 - r.Box.Bottom, r.Box.Left, r.Box.Height, r.Box.Width)).ToList();
        }
    }
    /// <summary>验证25项模板、标签及可导入加密配置。</summary>
    private static void Templates()
    {
        var legacy = new List<DetectItemInfo> { Item("胶皮宽度", "10", "10"), Item("胶皮高度", "5", "5") };
        var legacyResult = Parse(legacy, new List<DetResult> { Box(8), Box(5, 60), Box(7, 20) });
        Check(legacyResult.IsOk && legacyResult.AlgorithmResult.DetectResults.ContainsKey("胶皮宽度"), "旧配置订阅键失效");
        Check(legacyResult.AlgorithmResult.Texts.Any(t => t.Text.StartsWith("穿塞宽度:")) &&
            legacyResult.AlgorithmResult.Texts.Any(t => t.Text.StartsWith("穿塞高度:")), "旧配置显示未使用穿塞名称");
        var items = JsonConvert.DeserializeObject<List<DetectItemInfo>>(File.ReadAllText("Template/TDAI/新端子12类检测项.items"));
        Check(items.Any(i => i.Name == "穿塞宽度") && items.Any(i => i.Name == "穿塞高度") &&
            !items.Any(i => i.Name == "胶皮宽度" || i.Name == "胶皮高度"), "模板未更新穿塞尺寸名称");
        Check(items.Count == 27 && items.Select(i => i.Name).Distinct().Count() == 27, "模板项目缺失或重复");
        Check(items.Count(i => i.Name == "前防水栓破损" || i.Name == "后防水栓破损") == 2,
            "缺少前后防水栓颜色检测项");
        var result = Parse(items, Enumerable.Range(0, 12).Select(id => Box(id, id * 20)).ToList());
        Check(result.DetectItemCount == 25, "模板项目未全部输出");
        Check(!items.Any(i => i.Name.Contains("正插") || i.Name.Contains("反插")), "遗留旧防水栓项目");
        string source = File.ReadAllText("Template/TDAI/新端子12类模型配置.json");
        Check(StringCipher.Decrypt(File.ReadAllText("Template/TDAI/新端子12类模型.ai")) == source, "AI配置加密回读失败");
        var config = JsonConvert.DeserializeObject<AIInputInfo>(source);
        Check(config.ClassNames.Count == 12 && config.ClassNames[5] == "waterproof_plug_front" && config.ClassNames[10] == "fly_rubber", "标签配置错误");
        var param = new NodeParamTDAI { ModelName = ModelName.新端子12类模型 };
        Check(JsonConvert.DeserializeObject<NodeParamTDAI>(JsonConvert.SerializeObject(param)).ModelName == ModelName.新端子12类模型, "模型枚举保存失败");
        using (var form = new ParamFormTDAI())
        {
            var combo = (ComboBox)typeof(ParamFormTDAI).GetField("comboBoxModelName", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            Check(combo.Items.Contains("新端子12类模型"), "设计器未加入新模型入口");
            typeof(ParamFormTDAI).GetMethod("SetComboBoxToEnumValue", BindingFlags.Instance | BindingFlags.NonPublic)
                .MakeGenericMethod(typeof(ModelName)).Invoke(form, new object[] { combo, ModelName.新端子12类模型 });
            Check(combo.Text == "新端子12类模型", "新模型界面恢复失败");
        }
    }
    /// <summary>离线回归入口。</summary>
    [STAThread]
    private static int Main()
    {
        try { Direction(); Mapping(); RubberFiltering(); MissingMeasurements(); Templates(); ColorPerimeters(); Console.WriteLine("新端子12类验证通过，共" + checks + "项断言。"); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    /// <summary>验证真实OpenCV颜色处理、逐区域学习判定、配置保存及裁剪坐标。</summary>
    private static void ColorPerimeters()
    {
        const string front = "前防水栓破损", back = "后防水栓破损";
        var service = new PlugColorPerimeter();
        using (var image = new Mat(150, 400, MatType.CV_8UC3, Scalar.White))
        using (var input = new OutputImage { Bitmaps = new List<Mat> { image } })
        {
            Cv2.Rectangle(image, new Rect(10, 10, 50, 40), new Scalar(0, 0, 255), -1);
            Cv2.Rectangle(image, new Rect(90, 10, 60, 40), new Scalar(0, 0, 255), -1);
            Cv2.Rectangle(image, new Rect(180, 10, 57, 35), new Scalar(0, 0, 255), -1); // 1995，必须过滤。
            Cv2.Rectangle(image, new Rect(290, 10, 50, 60), new Scalar(0, 255, 0), -1);
            var boxes = new List<DetResult> { Box(5, 0, 0, 260, 140), Box(6, 280, 0, 100, 140) };
            var items = new List<DetectItemInfo> { Item(front), Item(back) };
            Solution.Instance.DetectItemDic["颜色测试"] = items;
            var param = new NodeParamTDAI { CurDetectItemName = "颜色测试", NodeName = "颜色测试",
                IsAutoStudy = true, StudyNum = 2, StudyPercentage = 10 };
            var result = new NodeResultTDAI();
            var process = new TDJS_Vision.Process("颜色测试");
            NewTerminal12Parse.Parse(boxes, boxes.Count, param, process, ref result, input);
            Check(result.AlgorithmResult.DetectResults[front].Count == 2, "面积2000未保留或小区域未过滤");
            Check(result.AlgorithmResult.DetectResults[front].Select(r => float.Parse(r.Value)).OrderBy(v => v)
                .SequenceEqual(new[] { 176f, 196f }), "独立轮廓周长错误或被求和");
            Check(items.All(i => i.PlugColor == null), "未完成学习提前覆盖正式颜色");
            NewTerminal12Parse.Parse(boxes, boxes.Count, param, process, ref result, input);
            Check(!param.IsAutoStudy && items.All(i => i.PlugColor != null), "学习完成未保存颜色或未关闭开关");
            Check(float.Parse(items[0].MinValue) == 158.4f && float.Parse(items[0].MaxValue) == 215.6f,
                "学习公差未包含每一个独立区域");
            Check(items[0].PlugColor.Hue != items[1].PlugColor.Hue, "前后颜色混用");
            var clone = (List<DetectItemInfo>)typeof(NodeTDAI).GetMethod("CloneDetectItems",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { items });
            Check(clone[0].PlugColor.Hue == items[0].PlugColor.Hue && !ReferenceEquals(clone[0].PlugColor, items[0].PlugColor),
                "通信复制模板丢失颜色或共享颜色对象");
            var restored = JsonConvert.DeserializeObject<List<DetectItemInfo>>(JsonConvert.SerializeObject(items));
            Solution.Instance.DetectItemDic["颜色测试"] = restored;
            NewTerminal12Parse.Parse(boxes, boxes.Count, param, process, ref result, input);
            Check(result.IsOk && result.AlgorithmResult.Contours.Count == 3, "保存恢复后未正确检测或显示轮廓");
            Check(result.AlgorithmResult.Contours.All(c => c.IsClosed), "防水栓轮廓没有闭合显示标记");
            // 使用只有四个角点的轮廓验证实际绘制：边中点及最后一条闭合边都必须着色。
            using (var bitmap = new System.Drawing.Bitmap(100, 100))
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
            {
                graphics.Clear(System.Drawing.Color.Black);
                var roi = new TDJS_Vision.Forms.DispShowImage.ShowImageControl.RoiContour(
                    new List<System.Drawing.PointF> { new System.Drawing.PointF(10, 10), new System.Drawing.PointF(80, 10),
                        new System.Drawing.PointF(80, 80), new System.Drawing.PointF(10, 80) }, System.Drawing.Color.Lime) { IsClosed = true };
                roi.Draw(graphics, 1, System.Drawing.PointF.Empty);
                Check(bitmap.GetPixel(45, 10).G > 0 && bitmap.GetPixel(10, 45).G > 0, "轮廓仍显示散点或首尾未闭合");
                Check(bitmap.GetPixel(45, 45).G == 0, "轮廓被错误填充");
                graphics.Clear(System.Drawing.Color.Black);
                roi.IsClosed = false;
                roi.Draw(graphics, 1, System.Drawing.PointF.Empty);
                Check(bitmap.GetPixel(45, 10).G == 0, "旧散点显示被改变");
            }
            Cv2.Rectangle(image, new Rect(90, 10, 90, 40), new Scalar(0, 0, 255), -1);
            NewTerminal12Parse.Parse(boxes, boxes.Count, param, process, ref result, input);
            Check(!result.IsOk && result.AlgorithmResult.DetectResults[front].Count(r => !r.IsOk) == 1,
                "一个区域超差未单独NG");
            Check(Value(result, back).IsOk, "前部超差污染后部结果");
            image.SetTo(Scalar.White);
            NewTerminal12Parse.Parse(boxes, boxes.Count, param, process, ref result, input);
            Check(!result.IsOk && Value(result, front).Value == "0.00", "无颜色区域误判OK");
            string before = JsonConvert.SerializeObject(restored);
            param.IsAutoStudy = true;
            bool failed = false;
            try { NewTerminal12Parse.Parse(new List<DetResult>(), 0, param, process, ref result, input); }
            catch (InvalidOperationException) { failed = true; }
            Check(failed && JsonConvert.SerializeObject(restored) == before, "空目标学习覆盖原颜色或公差");
        }
        // 两个不足面积门槛的区域即使闭运算能够合并，也必须先被去除。
        using (var image = new Mat(120, 140, MatType.CV_8UC3, Scalar.White))
        using (var input = new OutputImage { Bitmaps = new List<Mat> { image },
            Rectangles = new List<Rect> { new Rect(300, 400, 140, 120) } })
        {
            var box = new List<DetResult> { Box(5, 300, 400, 140, 120) };
            var red = new PlugColorModel { Hue = 0, HueTolerance = 10, MinS = 100, MaxS = 255, MinV = 100, MaxV = 255 };
            Cv2.Rectangle(image, new Rect(10, 10, 20, 80), new Scalar(0, 0, 255), -1);
            Cv2.Rectangle(image, new Rect(31, 10, 20, 80), new Scalar(0, 0, 255), -1);
            Check(service.Measure(input, box, ref red).Single().Contour.Length == 0, "闭运算在面积过滤前执行");
            image.SetTo(Scalar.White);
            Cv2.Rectangle(image, new Rect(10, 10, 50, 40), new Scalar(0, 0, 255), -1);
            Cv2.Rectangle(image, new Rect(10, 10, 1, 1), Scalar.White, -1);
            Check(service.Measure(input, box, ref red).Single().Contour.Length == 0, "1999像素区域未过滤");
            image.SetTo(Scalar.White);
            Cv2.Rectangle(image, new Rect(10, 10, 30, 80), new Scalar(0, 0, 255), -1);
            Cv2.Rectangle(image, new Rect(41, 10, 30, 80), new Scalar(0, 0, 255), -1);
            var region = service.Measure(input, box, ref red).Single();
            Check(region.Perimeter == 278, "闭运算没有合并一像素间隙或周长不正确");
            Check(region.Bounds.X == 310 && region.Bounds.Y == 410, "裁剪偏移错误");
            // 色相179必须被以0为中心的红色模型接受。
            using (var hsv = new Mat(120, 140, MatType.CV_8UC3, new Scalar(179, 255, 255)))
                Cv2.CvtColor(hsv, image, ColorConversionCodes.HSV2BGR);
            Check(service.Measure(input, box, ref red).Single().Contour.Length > 0, "红色色相跨零点失败");
        }
        using (var gray = new Mat(100, 100, MatType.CV_8UC1, Scalar.White))
        using (var input = new OutputImage { Bitmaps = new List<Mat> { gray } })
        {
            PlugColorModel color = null;
            bool failed = false;
            try { service.Measure(input, new List<DetResult> { Box(5, 0, 0, 100, 100) }, ref color); }
            catch (InvalidOperationException) { failed = true; }
            Check(failed, "灰度输入被误作彩色学习");
        }
    }
}
