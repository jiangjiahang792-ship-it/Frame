using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using TDJS_Vision;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._3_Detection.TDAI.Parse;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

/// <summary>直接执行真实 RL12 解析器，验证胶皮尺寸和飞胶数量。</summary>
internal static class RL12RubberTests
{
    /// <summary>已通过的行为断言数量。</summary>
    private static int _checks;

    /// <summary>验证结果，失败时报告具体行为。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }

    /// <summary>创建独立的检测项配置。</summary>
    private static DetectItemInfo Item(string name, string min, string max, bool count = false)
    {
        return new DetectItemInfo { Name = name, MinValue = min, MaxValue = max,
            Enable = true, IsCountItem = count, CurValue = "旧值" };
    }

    /// <summary>创建一个具有明确类别和非正方形尺寸的模型结果。</summary>
    private static DetResult Box(int id, int width, int height, int x = 0)
    {
        return new DetResult { ClassId = id, Box = new Rect(x, 10, width, height) };
    }

    /// <summary>执行真实解析流程，不加载模型或连接现场设备。</summary>
    private static NodeResultTDAI Parse(List<DetectItemInfo> items, List<DetResult> boxes,
        bool convert = false, bool study = false)
    {
        Solution.Instance.DetectItemDic["RL12离线测试"] = items;
        var parameter = new NodeParamTDAI { CurDetectItemName = "RL12离线测试",
            NodeName = "RL12离线测试", NeedConvert = convert, Scale = 0.5f,
            IsAutoStudy = study, StudyNum = 1, StudyPercentage = 10 };
        var result = new NodeResultTDAI();
        RL12Parse.Parse(boxes, boxes.Count, parameter, new TDJS_Vision.Process("RL12离线测试"), ref result);
        return result;
    }

    /// <summary>按显示名称读取实际检测结果，同时兼容原有英文内部键。</summary>
    private static SingleDetectResult Value(NodeResultTDAI result, string name)
    {
        string key = DetectItemLanguage.NormalizeName(name);
        Check(result.AlgorithmResult.DetectResults.ContainsKey(key), "未输出检测项：" + name);
        return result.AlgorithmResult.DetectResults[key][0];
    }

    /// <summary>防止宽高互换、误用 rubber 类别、换算丢失和未检出保留旧值。</summary>
    private static void VerifyDimensions()
    {
        var items = new List<DetectItemInfo> { Item("胶皮宽度", "40", "40"), Item("胶皮高度", "12", "12") };
        var boxes = new List<DetResult> { Box(6, 40, 12), Box(3, 99, 88), Box(7, 77, 66) };
        var result = Parse(items, boxes);
        Check(float.Parse(Value(result, "胶皮宽度").Value) == 40 && result.IsOk, "类别6的宽度或边界判断错误。");
        Check(float.Parse(Value(result, "胶皮高度").Value) == 12, "类别6的高度错误。");
        Check(result.AlgorithmResult.Rects.Count == 1, "宽高共用胶皮框未去重或混入其他类别。");
        items[0].MinValue = items[0].MaxValue = "20";
        items[1].MinValue = items[1].MaxValue = "6";
        result = Parse(items, boxes, true);
        Check(float.Parse(Value(result, "胶皮宽度").Value) == 20 && result.IsOk, "宽度物理换算错误。");
        Check(float.Parse(Value(result, "胶皮高度").Value) == 6, "高度物理换算错误。");
        result = Parse(items, new List<DetResult>());
        Check(!result.IsOk && items.All(i => i.CurValue == "0"), "缺失尺寸未判NG或未清空当前值。");
        items.ForEach(i => { i.MinValue = "0"; i.MaxValue = "0"; });
        Check(Parse(items, new List<DetResult>()).IsOk, "缺失尺寸的0~0范围兼容失败。");
        items[0].MaxValue = "40"; items[1].MaxValue = "12";
        boxes.Add(Box(6, 41, 12, 100));
        result = Parse(items, boxes);
        Check(result.AlgorithmResult.DetectResults["胶皮宽度"].Count == 2 && !result.IsOk, "多胶皮目标未逐个判定超限。");
        items.ForEach(i => i.Enable = false);
        Check(Parse(items, boxes).DetectItemCount == 0, "禁用尺寸仍输出结果。");
    }

    /// <summary>防止飞胶计数混入飞丝、受端子数量约束或污染端子数量。</summary>
    private static void VerifyCounts()
    {
        var items = new List<DetectItemInfo> { Item("胶皮宽度", "1", "100"),
            Item("端子数量", "1", "1", true), Item("飞胶", "0", "0", true), Item("飞丝", "0", "0", true) };
        var boxes = new List<DetResult> { Box(6, 40, 12) };
        var result = Parse(items, boxes);
        Check(Value(result, "飞胶").Value == "0" && result.IsOk, "无飞胶时被端子数量1误判NG。");
        boxes.Add(Box(9, 3, 4, 100)); boxes.Add(Box(9, 5, 6, 200));
        result = Parse(items, boxes, true);
        Check(Value(result, "飞胶").Value == "2" && !Value(result, "飞胶").IsOk, "飞胶未按类别9计数或错误参与尺寸换算。");
        Check(Value(result, "飞丝").Value == "0", "飞胶错误计入飞丝。");
        Check(Value(result, "端子数量").Value == "1", "飞胶数量污染端子数量。");
        items[2].MinValue = items[2].MaxValue = "2";
        Check(Parse(items, boxes).IsOk, "飞胶未使用自身上下限或仍受端子数量限制。");
        boxes.Add(Box(8, 8, 9, 300));
        result = Parse(items, boxes);
        Check(Value(result, "飞丝").Value == "1" && !Value(result, "飞丝").IsOk, "既有飞丝判定发生回归。");
        Check(Value(result, "飞胶").Value == "2" && Value(result, "飞胶").IsOk, "飞丝污染飞胶结果。");
        items[2].Enable = false;
        Check(!Parse(items, boxes).AlgorithmResult.DetectResults.ContainsKey("飞胶"), "禁用飞胶仍输出结果。");
    }

    /// <summary>验证线芯、胶皮和正插防水栓各自按对应模型类别计数。</summary>
    private static void VerifyComponentCounts()
    {
        var items = new List<DetectItemInfo> { Item("线芯长度", "1", "100"),
            Item("端子数量", "2", "2", true), Item("线芯数量", "2", "2", true),
            Item("胶皮数量", "1", "1", true), Item("防水栓数量", "2", "2", true) };
        var boxes = new List<DetResult> { Box(7, 40, 12), Box(7, 42, 13, 100),
            Box(6, 30, 20, 200), Box(5, 60, 25, 300), Box(5, 62, 26, 400) };

        var result = Parse(items, boxes);
        Check(result.IsOk, "目标数量符合2/1/2时应判OK。");
        Check(Value(result, "线芯数量").Value == "2", "线芯未按类别7计数。");
        Check(Value(result, "胶皮数量").Value == "1", "胶皮未按类别6计数。");
        Check(Value(result, "防水栓数量").Value == "2", "防水栓未按类别5计数。");
        Check(Value(result, "端子数量").Value == "2", "新增数量项污染端子数量。");
        Check(items[2].CurValue == "2" && items[3].CurValue == "1" && items[4].CurValue == "2",
            "数量检测项当前值未更新。");

        items[1].Enable = false;
        boxes.RemoveAt(1);
        result = Parse(items, boxes);
        Check(Value(result, "线芯数量").Value == "1" && !Value(result, "线芯数量").IsOk,
            "缺少线芯时未按自身数量下限判NG。");

        boxes.Add(Box(6, 31, 21, 500));
        result = Parse(items, boxes);
        Check(Value(result, "胶皮数量").Value == "2" && !Value(result, "胶皮数量").IsOk,
            "胶皮过多时未按自身数量上限判NG。");

        boxes.RemoveAll(box => box.ClassId == 5);
        result = Parse(items, boxes);
        Check(Value(result, "防水栓数量").Value == "0" && !Value(result, "防水栓数量").IsOk,
            "无正插防水栓时未输出0并判NG。");
    }

    /// <summary>验证检测项文件使用中文名称，解析和界面翻译仍由软件处理。</summary>
    private static void VerifyConfigNames()
    {
        Check(DetectItemLanguage.GetConfigName("DetectItem.CoreLength") == "线芯长度",
            "导出时未将线芯长度内部键还原为中文。");
        Check(DetectItemLanguage.GetConfigName("DetectItem.ForwardWaterproofPlugHeight") == "正插防水栓高度",
            "导出时未将防水栓高度内部键还原为中文。");
        Check(DetectItemLanguage.GetConfigName("胶皮数量") == "胶皮数量",
            "中文检测项名称在导出时发生变化。");
        Check(DetectItemLanguage.NormalizeName("线芯数量") == "DetectItem.CoreCount",
            "中文线芯数量未映射到解析器内部键。");
        LanguageManager.SetLanguage("en-US", false);
        try
        {
            Check(DetectItemLanguage.GetDisplayName("胶皮数量") == "Rubber count",
                "胶皮数量的英文翻译未从软件语言资源读取。");
            Check(DetectItemLanguage.GetDisplayName("防水栓数量") == "Waterproof plug count",
                "防水栓数量的英文翻译未从软件语言资源读取。");
        }
        finally
        {
            LanguageManager.SetLanguage("zh-CN", false);
        }
    }

    /// <summary>验证新增项沿用尺寸区间和数量众数的自动学习流程。</summary>
    private static void VerifyStudy()
    {
        var items = new List<DetectItemInfo> { Item("胶皮宽度", "0", "0"),
            Item("胶皮高度", "0", "0"), Item("飞胶", "0", "0", true) };
        var result = Parse(items, new List<DetResult> { Box(6, 40, 20), Box(9, 3, 4, 100) }, false, true);
        Check(result.IsOk, "自动学习未将新增项标记为OK。");
        Check(float.Parse(items[0].MinValue) == 36 && float.Parse(items[0].MaxValue) == 44, "宽度学习错误。");
        Check(float.Parse(items[1].MinValue) == 18 && float.Parse(items[1].MaxValue) == 22, "高度学习错误。");
        Check(items[2].MinValue == "1" && items[2].MaxValue == "1", "飞胶数量学习错误。");
    }

    /// <summary>旧模型缺件补零，实测合格框保持绿色，缺失项仍NG。</summary>
    private static void VerifyMissingMeasurements()
    {
        var items = new List<DetectItemInfo> { Item("端子数量", "2", "2", true), Item("胶皮宽度", "1", "2") };
        var boxes = new List<DetResult> { Box(6, 1, 5) };
        var result = Parse(items, boxes);
        Check(items[1].CurValue == "1.00,0", "旧模型未输出实测值及缺失零");
        Check(Value(result, "胶皮宽度").IsOk && !result.AlgorithmResult.DetectResults["胶皮宽度"][1].IsOk && !result.IsOk, "旧模型缺件判定未分离");
        Check(result.AlgorithmResult.Rects.Count == 1 && result.AlgorithmResult.Rects[0].Color == System.Drawing.Color.Green, "旧模型缺件把合格框染红");
        result = Parse(items, new List<DetResult>());
        Check(items[1].CurValue == "0,0" && result.AlgorithmResult.Rects.Count == 0, "全缺失补零错误");
        items[1].MinValue = "0";
        Check(!Parse(items, boxes).AlgorithmResult.DetectResults["胶皮宽度"][1].IsOk, "缺件零因尺寸范围含零而放行");
        items[1].MaxValue = "0.5";
        Check(Parse(items, boxes).AlgorithmResult.Rects[0].Color == System.Drawing.Color.Red, "实测超限未标红");
        items[1].MaxValue = "2"; items[0].Enable = false;
        Check(Parse(items, boxes).IsOk && items[1].CurValue == "1.00", "公共数量禁用仍补零");
        items[0].Enable = true; items[0].MinValue = "1"; items[0].MaxValue = "3";
        Check(Parse(items, boxes).IsOk && items[1].CurValue == "1.00", "按数量上限错误补零");
    }

    /// <summary>执行全部行为验证并返回可供脚本判断的退出码。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            LanguageManager.SetLanguage("zh-CN", false);
            VerifyDimensions(); VerifyCounts(); VerifyComponentCounts(); VerifyConfigNames(); VerifyStudy(); VerifyMissingMeasurements();
            Console.WriteLine("RL12胶皮、飞胶与部件数量验证通过，共" + _checks + "项断言。");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
