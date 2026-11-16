using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using TDJS_Vision;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._3_Detection.TDAI.Parse;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

/// <summary>宏晟端子12类模型真实解析器离线回归测试。</summary>
internal static class HongShengTerminal12Tests
{
    /// <summary>成功断言数量。</summary>
    private static int _checks;

    /// <summary>验证条件，失败时抛出明确错误。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }

    /// <summary>创建检测项。</summary>
    private static DetectItemInfo Item(string name, string min, string max, bool count = false)
    {
        return new DetectItemInfo { Name = name, MinValue = min, MaxValue = max,
            Enable = true, IsCountItem = count, CurValue = "0" };
    }

    /// <summary>创建模型框。</summary>
    private static DetResult Box(int id, int x, int width, int height)
    {
        return new DetResult { ClassId = id, Box = new Rect(x, 10, width, height) };
    }

    /// <summary>执行真实解析器。</summary>
    private static NodeResultTDAI Parse(List<DetectItemInfo> items, List<DetResult> boxes, bool study = false)
    {
        Solution.Instance.DetectItemDic["宏晟测试"] = items;
        var parameter = new NodeParamTDAI { ModelName = ModelName.宏晟端子12类模型,
            CurDetectItemName = "宏晟测试", NodeName = "宏晟测试", IsAutoStudy = study,
            StudyNum = 1, StudyPercentage = 10 };
        var result = new NodeResultTDAI();
        HongShengTerminal12Parse.Parse(boxes, boxes.Count, parameter,
            new TDJS_Vision.Process("宏晟测试"), ref result);
        return result;
    }

    /// <summary>验证全部类别映射、尺寸、面积、数量及咬胶X距离。</summary>
    private static void VerifyMappings()
    {
        var items = new List<DetectItemInfo>
        {
            Item("前铆露线芯高度", "6", "6"), Item("前铆露线芯长度", "20", "20"),
            Item("前铆露线芯面积", "120", "120"), Item("后铆露线芯高度", "8", "8"),
            Item("后铆露线芯长度", "30", "30"), Item("后铆露线芯面积", "240", "240"),
            Item("飞丝", "1", "1", true), Item("前铆脚面积", "480", "480"),
            Item("后铆脚面积", "1080", "1080"), Item("咬胶长度", "25", "25"),
            Item("防水栓反装", "1", "1", true), Item("正向防水栓面积", "1920", "1920"),
            Item("防水栓破损", "1", "1", true)
        };
        var boxes = new List<DetResult>
        {
            Box(0, 300, 20, 6), Box(1, 100, 40, 12), Box(2, 185, 30, 8),
            Box(4, 100, 60, 18), Box(5, 400, 80, 24), Box(8, 500, 3, 4),
            Box(10, 510, 3, 4), Box(11, 520, 3, 4)
        };
        NodeResultTDAI result = Parse(items, boxes);
        Check(result.IsOk, "完整正常结果未判OK：" + string.Join("；", result.AlgorithmResult.DetectResults
            .Where(pair => pair.Value.Any(value => !value.IsOk))
            .Select(pair => pair.Key + "=" + string.Join(",", pair.Value.Select(value => value.Value)))));
        Check(result.AlgorithmResult.DetectResults[DetectItemLanguage.NormalizeName("咬胶长度")][0].Value == "25.00", "咬胶未按X距离计算。");
        Check(result.AlgorithmResult.DetectResults[DetectItemLanguage.NormalizeName("前铆露线芯面积")][0].Value == "120.00", "前铆露线芯面积错误。");
        Check(result.AlgorithmResult.DetectResults[DetectItemLanguage.NormalizeName("后铆露线芯面积")][0].Value == "240.00", "后铆露线芯面积错误。");
        Check(result.AlgorithmResult.DetectResults[DetectItemLanguage.NormalizeName("飞丝")][0].Value == "1", "飞丝类别错误。");
        Check(result.AlgorithmResult.DetectResults[DetectItemLanguage.NormalizeName("防水栓反装")][0].Value == "1", "反装类别错误。");
        Check(result.AlgorithmResult.DetectResults[DetectItemLanguage.NormalizeName("防水栓破损")][0].Value == "1", "破损类别错误。");
    }

    /// <summary>验证通信首次学习无防水栓时关闭全部防水栓检测项。</summary>
    private static void VerifyNoPlugInitialStudy()
    {
        var items = new List<DetectItemInfo>
        {
            Item("前铆脚面积", "1", "99999"), Item("防水栓反装", "0", "0", true),
            Item("正向防水栓面积", "1", "99999"), Item("防水栓破损", "0", "0", true)
        };
        items[0].NeedsInitialAutoStudy = true;
        NodeResultTDAI result = Parse(items, new List<DetResult> { Box(1, 10, 20, 10) }, true);
        Check(items.Skip(1).All(item => !item.Enable), "无防水栓时未停用全部防水栓相关项。");
        Check(result.AlgorithmResult.DetectResults.Count == 1, "停用的防水栓项目仍参与解析。");
    }

    /// <summary>验证通信首次学习检出防水栓时关闭后铆露线芯、后铆脚面积和咬胶。</summary>
    private static void VerifyPlugInitialStudy()
    {
        var items = new List<DetectItemInfo>
        {
            Item("前铆脚面积", "1", "99999"), Item("后铆露线芯高度", "1", "99999"),
            Item("后铆露线芯长度", "1", "99999"), Item("后铆露线芯面积", "1", "99999"),
            Item("咬胶长度", "1", "99999"), Item("后铆脚面积", "1", "99999"),
            Item("正向防水栓面积", "1", "99999")
        };
        items[0].NeedsInitialAutoStudy = true;
        Parse(items, new List<DetResult>
        {
            Box(1, 10, 20, 10), Box(2, 80, 10, 5), Box(4, 50, 20, 10), Box(5, 100, 30, 20)
        }, true);
        Check(items.Skip(1).Take(4).All(item => !item.Enable),
            "检出防水栓时未关闭后铆露线芯或咬胶检测项。");
        Check(!items[5].Enable && items[6].Enable,
            "检出防水栓时未关闭后铆脚面积，或错误关闭了防水栓检测项。");
    }

    /// <summary>执行全部测试。</summary>
    private static int Main()
    {
        try
        {
            VerifyMappings(); VerifyNoPlugInitialStudy(); VerifyPlugInitialStudy();
            Console.WriteLine("宏晟端子12类模型验证通过，共" + _checks + "项断言。");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
