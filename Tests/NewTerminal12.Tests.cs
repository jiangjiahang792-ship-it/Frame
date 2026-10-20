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
            new { Name = "后防水栓高度", Id = 6, Value = "5" }, new { Name = "穿塞深度", Id = 7, Value = "10" },
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
        var items = new List<DetectItemInfo> { Item("端子数量", "2", "2"), Item("穿塞深度", "1", "2"), Item("穿塞直径", "1", "6") };
        var boxes = new List<DetResult> { Box(8), Box(5, 60), Box(7, 20, 0, 1, 5) };
        var result = Parse(items, boxes);
        var values = result.AlgorithmResult.DetectResults["穿塞深度"];
        Check(items[1].CurValue == "1.00,0" && items[2].CurValue == "5.00,0", "未按缺件数量补零");
        Check(values.Count == 2 && values[0].IsOk && !values[1].IsOk && !result.IsOk, "缺件放行或实际值误判");
        Check(result.AlgorithmResult.Rects.Count == 1 && result.AlgorithmResult.Rects[0].Color == System.Drawing.Color.Green, "缺件把合格框染红或生成虚假框");
        Check(result.AlgorithmResult.Texts.Any(t => t.Text.Contains("1.00,0") && t.Color == System.Drawing.Color.Red), "缺件检测项文字未标NG");
        result = Parse(items, new List<DetResult>());
        Check(items[1].CurValue == "0,0" && result.AlgorithmResult.Rects.Count == 0, "全部缺失未补齐或出现虚假框");
        items[1].MinValue = "0";
        result = Parse(items, boxes);
        Check(!result.IsOk && !result.AlgorithmResult.DetectResults["穿塞深度"][1].IsOk, "零在尺寸范围内导致缺件被放行");
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
        var items = new List<DetectItemInfo> { Item("胶皮数量", "1", "1"), Item("穿塞深度", "10", "10"),
            Item("穿塞直径", "5", "5"), Item("端子数量", "1", "1"), Item("飞胶", "1", "1") };
        var result = Parse(items, boxes);
        Check(result.IsOk, "区域外胶皮影响整体判定");
        Check(Value(result, "胶皮数量").Value == "1", "区域外胶皮参与计数");
        Check(Value(result, "端子数量").Value == "1", "区域外胶皮污染公共数量");
        Check(result.AlgorithmResult.DetectResults["穿塞深度"].Count == 1, "区域外胶皮参与尺寸检测");
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
            Check(Value(result, "穿塞深度").Value == "0" && !Value(result, "穿塞深度").IsOk, "缺失胶皮未清零或未判NG");
        }
        var rubberOnly = new List<DetectItemInfo> { Item("胶皮数量", "1", "1"), Item("穿塞深度", "10", "10") };
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
        var learn = new List<DetectItemInfo> { Item("穿塞深度", "1", "99999") };
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
        Check(legacyResult.AlgorithmResult.Texts.Any(t => t.Text.StartsWith("穿塞深度:")) &&
            legacyResult.AlgorithmResult.Texts.Any(t => t.Text.StartsWith("穿塞直径:")), "旧配置显示未使用穿塞名称");
        var items = JsonConvert.DeserializeObject<List<DetectItemInfo>>(File.ReadAllText("Template/TDAI/新端子12类检测项.items"));
        Check(items.Any(i => i.Name == "穿塞深度") && items.Any(i => i.Name == "穿塞直径") &&
            !items.Any(i => i.Name == "胶皮宽度" || i.Name == "胶皮高度"), "模板未更新穿塞尺寸名称");
        Check(items.Count == 27 && items.Select(i => i.Name).Distinct().Count() == 27, "模板项目缺失或重复");
        Check(items.Count(i => i.Name == "前防水栓破损" || i.Name == "后防水栓破损") == 2,
            "缺少前后防水栓颜色检测项");
        var result = Parse(items, Enumerable.Range(0, 12).Select(id => Box(id, id * 20)).ToList());
        Check(result.DetectItemCount == 24, "模板项目未全部输出");
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
        try { Direction(); Mapping(); RubberFiltering(); MissingMeasurements(); Templates(); ColorPerimeters(); ShapeMatching(); WholePlugMatching(); CustomerDisplayAndWholeCount(); Console.WriteLine("新端子12类验证通过，共" + checks + "项断言。"); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    /// <summary>验证整体计数、逐只方向显示、新尺寸名称和两个模型隐藏统计，不修改用户公差。</summary>
    private static void CustomerDisplayAndWholeCount()
    {
        var items = new List<DetectItemInfo> { Item("防水栓数量", "2", "2"), Item("防水栓正反") };
        var boxes = new List<DetResult> { Box(6,0,0), Box(5,20,0), Box(8,40,0),
            Box(6,0,30), Box(5,20,30), Box(8,40,30) };
        var result = Parse(items, boxes);
        Check(result.IsOk && Value(result,"防水栓数量").Value == "2", "两只完整防水栓应合并计数为2");
        Check(result.AlgorithmResult.Texts.Any(t => t.Text == "防水栓数量: 2"), "新模型数量仍显示上下限");
        Check(items[1].CurValue == "正,正", "两只正向未逐只显示");
        Check(result.AlgorithmResult.Texts.Any(t => t.Text == "防水栓正反: 正,正"), "正反显示仍包含设定值");
        Check(result.AlgorithmResult.Texts.Count == 3, "新模型仍显示生产统计");
        boxes[3] = Box(6,20,30); boxes[4] = Box(5,0,30);
        result = Parse(items, boxes);
        Check(items[1].CurValue == "正,反" && !result.IsOk, "单只反向未显示正反或未判NG");
        Check(Value(result,"防水栓正反").Value == "1", "正反数字通信结果被显示修改破坏");
        boxes.Reverse(); result = Parse(items, boxes);
        Check(items[1].CurValue == "正,反", "方向显示依赖输入检测顺序");
        boxes.RemoveAll(b => b.ClassId == 6 && b.Box.Y == 30);
        result = Parse(items, boxes);
        Check(Value(result,"防水栓数量").Value == "1" && !Value(result,"防水栓数量").IsOk, "缺后段仍算完整防水栓");
        boxes = new List<DetResult> { Box(6), Box(5,20), Box(6,0,30), Box(5,20,30), Box(5,20,60) };
        result = Parse(items, boxes);
        Check(Value(result,"防水栓数量").Value == "2" && !Value(result,"防水栓数量").IsOk, "多余孤立前段被完整数量掩盖");
        result = Parse(items, new List<DetResult>());
        Check(Value(result,"防水栓数量").Value == "0" && items[1].CurValue == "反", "缺失结果应保守判定");
        Check(items[0].MinValue == "2" && items[0].MaxValue == "2" && items[1].MinValue == "0" && items[1].MaxValue == "0", "运行修改了用户公差");
        var sizes = new List<DetectItemInfo> { Item("线芯直径","5","5"), Item("穿塞深度","10","10"), Item("穿塞直径","5","5") };
        result = Parse(sizes, new List<DetResult> { Box(8), Box(7,20), Box(5,60) });
        Check(result.IsOk && Value(result,"穿塞深度").Value == "10.00" && Value(result,"穿塞直径").Value == "5.00", "尺寸改名改变了原测量轴");
        Check(result.AlgorithmResult.Texts.Any(t => t.Text.Contains("范围值: [")) &&
            !result.AlgorithmResult.Texts.Any(t => t.Text.Contains("公差值")), "新模型未使用范围值文案");
        var legacy = new List<DetectItemInfo> { Item("穿塞宽度","10","10"), Item("穿塞高度","5","5") };
        result = Parse(legacy, new List<DetResult> { Box(8), Box(7,20), Box(5,60) });
        Check(result.IsOk && Value(result,"穿塞宽度").Value == "10.00", "旧宽度高度配置测量方向发生变化");
        const string key = "RL统计显示测试";
        Solution.Instance.DetectItemDic[key] = new List<DetectItemInfo>();
        var process = new TDJS_Vision.Process(key);
        result = new NodeResultTDAI();
        RL12Parse.Parse(new List<DetResult>(), 0, new NodeParamTDAI { CurDetectItemName = key }, process, ref result);
        Check(result.AlgorithmResult.Texts.Count == 1 && process.OKNumber == 1, "RL模型统计仍显示或累计被删除");
        var countItems = new List<DetectItemInfo> { Item("端子数量","1","1"), Item("线芯数量","1","1"),
            Item("胶皮数量","1","1"), Item("防水栓数量","1","1"), Item("线芯长度","10","10") };
        foreach (var countItem in countItems.Take(4)) countItem.IsCountItem = true;
        Solution.Instance.DetectItemDic[key] = countItems;
        RL12Parse.Parse(new List<DetResult> { Box(7), Box(6,20), Box(5,40) }, 3,
            new NodeParamTDAI { CurDetectItemName = key }, process, ref result);
        foreach (string name in new[] { "端子数量", "线芯数量", "胶皮数量", "防水栓数量" })
            Check(result.AlgorithmResult.Texts.Any(t => t.Text == ParseCommon.GetDisplayName(name) + ": 1"), "RL数量仍显示范围：" + name + " 实际：" + string.Join(" | ", result.AlgorithmResult.Texts.Select(t => t.Text)));
        Check(result.AlgorithmResult.Texts.Any(t => t.Text.Contains("范围值: [")), "RL尺寸未使用范围值");
    }

    /// <summary>验证整栓前后配对、合并范围、学习检测一致性及缺失保护。</summary>
    private static void WholePlugMatching()
    {
        const string key = "整栓配对测试", name = "防水栓轮廓破损";
        var item = Item(name);
        var items = new List<DetectItemInfo> { item, Item("防水栓数量","2","2") };
        Solution.Instance.DetectItemDic[key] = items;
        var param = new NodeParamTDAI { CurDetectItemName=key, NodeName=key, IsAutoStudy=true, StudyNum=1, StudyPercentage=30 };
        var process = new TDJS_Vision.Process(key);
        var result = new NodeResultTDAI();
        var boxes = new List<DetResult> { Box(5,100,20,90,80), Box(6,10,150,80,80),
            Box(5,100,150,90,80), Box(6,10,20,80,80) };
        using (var image = new Mat(260,240,MatType.CV_8UC3,Scalar.White))
        using (var input = new OutputImage { Bitmaps = new List<Mat> { image } })
        {
            Cv2.Rectangle(image,new Rect(10,20,180,80),new Scalar(0,255,0),-1);
            Cv2.Rectangle(image,new Rect(10,150,180,80),new Scalar(0,255,0),-1);
            NewTerminal12Parse.Parse(boxes,boxes.Count,param,process,ref result,input);
            Check(item.PlugColor.ShapeTemplates.All(t => t.Points.Max(p=>p.X) == 179), "学习未覆盖整栓及前后框之间区域");
            Check(result.AlgorithmResult.DetectResults[name].Count == 2, "一只整栓输出多个分段结果");
            boxes.Reverse();
            NewTerminal12Parse.Parse(boxes,boxes.Count,param,process,ref result,input);
            Check(result.IsOk && result.AlgorithmResult.DetectResults[name].All(r=>r.Value=="100.00"), "整栓学习和检测区域不一致或受框顺序影响");
            Check(result.AlgorithmResult.Texts.Count(t=>t.Text.StartsWith(name)) == 1, "整栓破损重复显示文本");
            // 上方后段外缘缺口必须影响整只评分；下方整栓仍应匹配正常模板。
            Cv2.Rectangle(image,new Rect(35,20,30,25),Scalar.White,-1);
            NewTerminal12Parse.Parse(boxes,boxes.Count,param,process,ref result,input);
            Check(!result.IsOk && result.AlgorithmResult.DetectResults[name].Count(r=>!r.IsOk)==1, "整栓未检出后段缺口或污染另一只");
            var incomplete = boxes.Where(b=>!(b.ClassId==6 && b.Box.Y==20)).ToList();
            item.MinValue="0";
            NewTerminal12Parse.Parse(incomplete,incomplete.Count,param,process,ref result,input);
            Check(result.AlgorithmResult.DetectResults[name].Any(r=>!r.IsOk && r.Value=="0"), "不完整配对被其他颜色区域抵消");
            string saved = JsonConvert.SerializeObject(item.PlugColor);
            param.IsAutoStudy=true;
            bool rejected=false;
            try { NewTerminal12Parse.Parse(incomplete,incomplete.Count,param,process,ref result,input); }
            catch(InvalidOperationException e) { rejected=e.Message.Contains("配对"); }
            Check(rejected && JsonConvert.SerializeObject(item.PlugColor)==saved, "不完整配对学习未拒绝或污染正式模板");
            // 即使一只整栓被分成两个有效颜色区域，也不能冒充两只完整配对。
            param.IsAutoStudy=false;
            image.SetTo(Scalar.White);
            Cv2.Rectangle(image,new Rect(10,20,180,80),new Scalar(0,255,0),-1);
            Cv2.Rectangle(image,new Rect(95,20,10,80),Scalar.White,-1);
            var singlePair = boxes.Where(b=>b.Box.Y==20).ToList();
            NewTerminal12Parse.Parse(singlePair,singlePair.Count,param,process,ref result,input);
            Check(result.AlgorithmResult.DetectResults[name].Any(r=>!r.IsOk && r.Value=="0"), "一只整栓的两块颜色冒充两组配对");
        }
        var pairMethod = typeof(NewTerminal12Parse).GetMethod("PairWholePlugs",BindingFlags.NonPublic|BindingFlags.Static);
        var vertical = (List<DetResult>)pairMethod.Invoke(null,new object[] {
            new List<DetResult>{Box(5,20,100,80,90),Box(5,150,100,80,90)},
            new List<DetResult>{Box(6,150,10,80,80),Box(6,20,10,80,80)} });
        Check(vertical.Count==2 && vertical.All(b=>b.Box.Width==80 && b.Box.Height==180), "竖直摆放配对合并错误");
    }

    /// <summary>验证真实OpenCV颜色处理、逐区域学习判定、配置保存及裁剪坐标。</summary>
    private static void ColorPerimeters()
    {
        const string front = "前防水栓破损", back = "后防水栓破损";
        var service = new PlugColorPerimeter();
        // 两个模型框内出现三个有效颜色区域，前后分别只保留最大的两个，学习与检测一致。
        using (var image = new Mat(260, 400, MatType.CV_8UC3, Scalar.White))
        using (var input = new OutputImage { Bitmaps = new List<Mat> { image } })
        {
            var boxes = new List<DetResult>();
            foreach (int y in new[] { 0, 130 })
            {
                var color = y == 0 ? new Scalar(0,0,255) : new Scalar(0,255,0);
                Cv2.Rectangle(image,new Rect(10,y+10,80,80),color,-1);
                Cv2.Rectangle(image,new Rect(110,y+10,50,50),color,-1);
                Cv2.Rectangle(image,new Rect(220,y+10,70,70),color,-1);
                int id = y == 0 ? 5 : 6;
                boxes.Add(Box(id,0,y,200,120)); boxes.Add(Box(id,200,y,200,120));
            }
            var items = new List<DetectItemInfo> { Item(front), Item(back), Item("端子数量","2","2") };
            const string key = "颜色区域按数量筛选";
            Solution.Instance.DetectItemDic[key] = items;
            var param = new NodeParamTDAI { CurDetectItemName=key, NodeName=key, IsAutoStudy=true, StudyNum=1, StudyPercentage=10 };
            var process = new TDJS_Vision.Process(key);
            var result = new NodeResultTDAI();
            NewTerminal12Parse.Parse(boxes,boxes.Count,param,process,ref result,input);
            Check(items.Take(2).All(i => i.PlugColor.ShapeTemplates.Count == 2), "前后区域未分别限制为两个学习模板");
            Check(items.Take(2).All(i => i.PlugColor.ShapeTemplates.All(t => t.Points.Max(p => p.X) >= 69)), "小区域进入学习模板");
            NewTerminal12Parse.Parse(boxes,boxes.Count,param,process,ref result,input);
            Check(result.IsOk && new[] { front,back }.All(n => result.AlgorithmResult.DetectResults[n].Count == 2),
                "额外小区域影响前后破损检测或显示数量");
            // 删除公共数量后，前后独立数量控制各自颜色区域，模型数量检测仍独立报告。
            items.RemoveAll(i => i.Name == "端子数量");
            items.Add(Item("前防水栓数量", "2", "2"));
            items.Add(Item("后防水栓数量", "1", "1"));
            items.Add(Item("胶皮数量", "2", "2"));
            items.Add(Item("线芯数量", "2", "2"));
            NewTerminal12Parse.Parse(boxes,boxes.Count,param,process,ref result,input);
            Check(result.AlgorithmResult.DetectResults[front].Count == 2 && result.AlgorithmResult.DetectResults[back].Count == 1,
                "前后颜色筛选未使用各自独立数量上限");
            Check(Value(result,"前防水栓数量").IsOk && !Value(result,"后防水栓数量").IsOk &&
                !Value(result,"胶皮数量").IsOk && !Value(result,"线芯数量").IsOk, "独立数量未分别判定");
            Check(!result.AlgorithmResult.DetectResults.ContainsKey("端子数量"), "删除的端子数量仍在输出判定");
            // 恢复后段两个用于后续缺失回归，使用独立数量配置重新学习。
            var backCount = items.Single(i => i.Name == "后防水栓数量");
            backCount.MinValue = "2"; backCount.MaxValue = "2";
            items.RemoveAll(i => i.Name == "胶皮数量" || i.Name == "线芯数量");
            param.IsAutoStudy = true;
            NewTerminal12Parse.Parse(boxes,boxes.Count,param,process,ref result,input);
            Check(items.Take(2).All(i => i.PlugColor.ShapeTemplates.Count == 2), "独立数量下重新学习模板数量错误");
            // 移除第二大区域和碎片，剩余正常大区域不能抵消缺失。
            Cv2.Rectangle(image,new Rect(100,0,300,120),Scalar.White,-1);
            NewTerminal12Parse.Parse(boxes,boxes.Count,param,process,ref result,input);
            Check(!result.IsOk && result.AlgorithmResult.DetectResults[front].Count == 2 &&
                result.AlgorithmResult.DetectResults[front].Count(r => !r.IsOk) == 1, "颜色区域不足没有补零NG");
            Check(result.AlgorithmResult.DetectResults[back].All(r => r.IsOk), "前段缺失污染后段结果");
        }
        // 框外围红色占多数，中心为绿色；完整框学习应选红色，防止误用中心窗口。
        using (var image = new Mat(360, 420, MatType.CV_8UC3, new Scalar(0, 0, 255)))
        using (var input = new OutputImage { Bitmaps = new List<Mat> { image } })
        {
            Cv2.Rectangle(image, new Rect(150, 120, 100, 100), new Scalar(0, 255, 0), -1);
            Cv2.Rectangle(image, new Rect(70, 80, 50, 50), new Scalar(0, 255, 0), -1);
            PlugColorModel color = null;
            var regions = service.Measure(input, new List<DetResult> { Box(5, 50, 20, 300, 300) }, ref color);
            Check(color.Hue == 0, "颜色学习仍被限制在中心100×100");
            Check(regions.Count == 1 && regions.Single().Bounds == new Rect(50, 20, 300, 300), "完整识别框颜色提取范围错误");
            // 小于100的框不能越界，也不能被放大采样到框外颜色。
            color = null;
            regions = service.Measure(input, new List<DetResult> { Box(5, 70, 80, 50, 50) }, ref color);
            Check(color.Hue == 60 && regions.Single().Bounds.Width == 50, "小识别框学习越界或受框外颜色污染");
        }
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
                .SequenceEqual(new[] { 100f, 100f }), "独立轮廓没有各自匹配模板或被求和");
            Check(items.All(i => i.PlugColor == null), "未完成学习提前覆盖正式颜色");
            NewTerminal12Parse.Parse(boxes, boxes.Count, param, process, ref result, input);
            Check(!param.IsAutoStudy && items.All(i => i.PlugColor != null), "学习完成未保存颜色或未关闭开关");
            Check(float.Parse(items[0].MinValue) == 90f && float.Parse(items[0].MaxValue) == 100f,
                "相似度学习公差不是90～100");
            Check(items[0].PlugColor.ShapeTemplates.Count == 2, "没有分别学习两个独立区域的轮廓");
            Check(items[0].PlugColor.Hue != items[1].PlugColor.Hue, "前后颜色混用");
            var clone = (List<DetectItemInfo>)typeof(NodeTDAI).GetMethod("CloneDetectItems",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { items });
            Check(clone[0].PlugColor.Hue == items[0].PlugColor.Hue && !ReferenceEquals(clone[0].PlugColor, items[0].PlugColor),
                "通信复制模板丢失颜色或共享颜色对象");
            Check(!ReferenceEquals(clone[0].PlugColor.ShapeTemplates[0].Points[0], items[0].PlugColor.ShapeTemplates[0].Points[0]),
                "轮廓模板未深复制");
            var restored = JsonConvert.DeserializeObject<List<DetectItemInfo>>(JsonConvert.SerializeObject(items));
            Solution.Instance.DetectItemDic["颜色测试"] = restored;
            NewTerminal12Parse.Parse(boxes, boxes.Count, param, process, ref result, input);
            Check(result.IsOk && result.AlgorithmResult.Contours.Count == 3, "保存恢复后未正确检测或显示轮廓");
            Check(result.AlgorithmResult.Contours.All(c => c.IsClosed), "防水栓轮廓没有闭合显示标记");
            param.NeedConvert = true; param.Scale = .01f; restored[0].Compensation = 9;
            NewTerminal12Parse.Parse(boxes, boxes.Count, param, process, ref result, input);
            Check(Value(result, front).Value == "100.00", "相似度受到尺寸比例或补偿影响");
            param.NeedConvert = false; restored[0].Compensation = 0;
            var savedTemplates = restored[0].PlugColor.ShapeTemplates;
            restored[0].PlugColor.ShapeTemplates = null;
            bool oldRejected = false;
            try { NewTerminal12Parse.Parse(boxes, boxes.Count, param, process, ref result, input); }
            catch (InvalidOperationException error) { oldRejected = error.Message.Contains("重新一键学习"); }
            Check(oldRejected, "旧周长配置没有提示重新学习");
            restored[0].PlugColor.ShapeTemplates = savedTemplates;
            restored[0].MaxValue = "99999";
            bool limitsRejected = false;
            try { NewTerminal12Parse.Parse(boxes, boxes.Count, param, process, ref result, input); }
            catch (InvalidOperationException error) { limitsRejected = error.Message.Contains("0～100"); }
            Check(limitsRejected, "旧周长公差被误用为百分比");
            restored[0].MaxValue = "100";
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
            var measuredRegions = service.Measure(input, box, ref red);
            Check(measuredRegions.Count == 2, "仍有闭运算，跨一像素间隙合并了独立区域");
            var region = measuredRegions[0];
            Check(region.Perimeter == 216, "未闭运算轮廓周长错误");
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

    /// <summary>验证轮廓模板对平移旋转的容忍、对缺损和尺寸变化的敏感性及空模板拒绝。</summary>
    private static void ShapeMatching()
    {
        var service = new PlugContourSimilarity();
        var points = new[] { new Point(0,0),new Point(140,0),new Point(140,30),
            new Point(90,30),new Point(90,100),new Point(0,100) };
        var templates = service.Learn(new List<PlugPerimeterRegion> { new PlugPerimeterRegion { Contour = points } });
        Check(service.Compare(points.Select(p => new Point(p.X+512,p.Y+320)).ToArray(),templates) == 100,
            "平移影响相似度");
        foreach (double angle in new[] { -7.0, 5.0, 9.0 })
        {
            double radians = angle * Math.PI / 180;
            var rotated = points.Select(p => new Point((int)Math.Round(p.X*Math.Cos(radians)-p.Y*Math.Sin(radians))+200,
                (int)Math.Round(p.X*Math.Sin(radians)+p.Y*Math.Cos(radians))+300)).ToArray();
            double similarity = service.Compare(rotated,templates);
            Check(similarity > 96 && similarity <= 100, "刚性角度对齐失败：" + angle + "，" + similarity);
        }
        var damaged = new[] { new Point(0,0),new Point(40,0),new Point(40,45),new Point(75,45),
            new Point(75,0),new Point(140,0),new Point(140,30),new Point(90,30),new Point(90,100),new Point(0,100) };
        Check(service.Compare(damaged,templates) < 92, "缺口被对齐过程抹平");
        Check(service.Compare(points.Select(p => new Point(p.X*2,p.Y*2)).ToArray(),templates) < 40,
            "尺寸被独立缩放，掩盖目标变化");
        var restored = JsonConvert.DeserializeObject<List<PlugShapeTemplate>>(JsonConvert.SerializeObject(templates));
        Check(service.Compare(points,restored) == 100, "模板保存恢复改变相似度");
        bool rejected = false;
        try { service.Compare(points,null); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "无模板时静默通过");
        // 学习阶段由用户提供正常样本，第二帧应成为独立正常模板；检测阶段不能追加。
        using (var image = new Mat(120,160,MatType.CV_8UC3,Scalar.White))
        using (var input = new OutputImage { Bitmaps = new List<Mat> { image } })
        {
            const string key = "多帧轮廓学习";
            var item = Item("前防水栓破损");
            Solution.Instance.DetectItemDic[key] = new List<DetectItemInfo> { item };
            var param = new NodeParamTDAI { CurDetectItemName = key, NodeName = key,
                IsAutoStudy = true, StudyNum = 2, StudyPercentage = 0 };
            var boxes = new List<DetResult> { Box(5,0,0,160,120) };
            var result = new NodeResultTDAI();
            var process = new TDJS_Vision.Process(key);
            Cv2.Rectangle(image,new Rect(20,20,100,60),new Scalar(0,0,255),-1);
            NewTerminal12Parse.Parse(boxes,1,param,process,ref result,input);
            image.SetTo(Scalar.White);
            Cv2.Rectangle(image,new Rect(20,20,80,60),new Scalar(0,0,255),-1);
            NewTerminal12Parse.Parse(boxes,1,param,process,ref result,input);
            Check(item.MinValue == "100.00" && item.MaxValue == "100.00", "多模板自匹配学习分数错误");
            Check(item.PlugColor.ShapeTemplates.Count == 2, "第二张正常品未加入模板");
            NewTerminal12Parse.Parse(boxes,1,param,process,ref result,input);
            Check(result.IsOk && item.PlugColor.ShapeTemplates.Count == 2, "检测阶段污染正常模板或未匹配第二模板");
        }
        var model = new PlugColorModel { ShapeTemplates = templates };
        var local = service.Evaluate(damaged, model, 90);
        Check(local.HasLocalDefect && local.MissingArea >= 100 && local.MissingDepth >= 6,
            "连续缺口未由局部判定拦截");
        var rectangle = new[] { new Point(0,0), new Point(200,0), new Point(200,100), new Point(0,100) };
        model.ShapeTemplates = service.Learn(new List<PlugPerimeterRegion> { new PlugPerimeterRegion { Contour = rectangle } });
        var shallow = new[] { new Point(0,0), new Point(80,0), new Point(80,2), new Point(120,2),
            new Point(120,0), new Point(200,0), new Point(200,100), new Point(0,100) };
        var noisy = service.Evaluate(shallow, model, 95);
        Check(!noisy.HasLocalDefect && noisy.Similarity > 99, "边缘两像素误差未被容忍");
        var notch = new[] { new Point(0,0), new Point(80,0), new Point(80,20), new Point(95,20),
            new Point(95,0), new Point(200,0), new Point(200,100), new Point(0,100) };
        local = service.Evaluate(notch, model, 95);
        Check(local.OverallSimilarity > 95 && local.Similarity < 50 && local.HasLocalDefect,
            "小缺口未计入综合相似度");
        Check(Math.Abs(local.Similarity - local.OverallSimilarity / (1 + local.LocalSeverity * local.LocalSeverity)) < 0.000001,
            "综合评分与局部扣分不一致");
        Check(Math.Abs(service.Evaluate(notch, model, 0).Similarity - local.Similarity) < 0.000001,
            "改变用户公差影响测量评分");
        service.AddTemplates(model.ShapeTemplates, new List<PlugPerimeterRegion> { new PlugPerimeterRegion { Contour = notch } });
        Check(service.Evaluate(notch, model, 95).Similarity == 100, "未选中匹配的正常模板");
        model.ShapeTemplates.RemoveAt(1);
        // 通过真实解析入口验证：局部差异只影响分数，最终服从用户设置的公差。
        using (var image = new Mat(140,240,MatType.CV_8UC3,Scalar.White))
        using (var input = new OutputImage { Bitmaps = new List<Mat> { image } })
        {
            const string key = "局部综合评分";
            var item = Item("前防水栓破损");
            Solution.Instance.DetectItemDic[key] = new List<DetectItemInfo> { item };
            var param = new NodeParamTDAI { CurDetectItemName = key, NodeName = key,
                IsAutoStudy = true, StudyNum = 1, StudyPercentage = 30 };
            var boxes = new List<DetResult> { Box(5,0,0,240,140) };
            var result = new NodeResultTDAI();
            var process = new TDJS_Vision.Process(key);
            Cv2.FillPoly(image, new[] { rectangle.Select(p => new Point(p.X+10,p.Y+10)).ToArray() }, new Scalar(0,0,255));
            NewTerminal12Parse.Parse(boxes,1,param,process,ref result,input);
            param.IsAutoStudy = false;
            image.SetTo(Scalar.White);
            Cv2.FillPoly(image, new[] { notch.Select(p => new Point(p.X+10,p.Y+10)).ToArray() }, new Scalar(0,0,255));
            NewTerminal12Parse.Parse(boxes,1,param,process,ref result,input);
            Check(!result.IsOk, "综合分数低于70仍通过");
            item.MinValue = "0";
            item.MaxValue = "100";
            NewTerminal12Parse.Parse(boxes,1,param,process,ref result,input);
            Check(result.IsOk, "综合分数在公差内仍被局部缺口单独否决");
        }
        service.AddTemplates(model.ShapeTemplates, new List<PlugPerimeterRegion> { new PlugPerimeterRegion { Contour = rectangle } });
        Check(model.ShapeTemplates.Count == 1, "重复学习相同轮廓未去重");
        var clone = model.Clone();
        Check(clone.EdgeTolerancePixels == 3 && clone.LocalMissingArea == 100 && clone.LocalMissingDepth == 6,
            "局部参数克隆丢失");
        model.LocalMissingDepth = 2;
        rejected = false;
        try { service.Evaluate(rectangle, model, 95); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "无效局部参数未拒绝");
    }
}
