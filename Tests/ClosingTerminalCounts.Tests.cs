using System;
using System.Collections.Generic;
using OpenCvSharp;
using TDJS_Vision;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._3_Detection.TDAI.Parse;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

/// <summary>合压模型数量检测的离线行为验证，不加载模型或相机。</summary>
internal static class ClosingTerminalCountsTests
{
    /// <summary>验证数量不受框尺寸和换算影响，以及零检出、上下限和禁用行为。</summary>
    [STAThread]
    private static void Main()
    {
        var items = new List<DetectItemInfo>
        {
            new DetectItemInfo { Name = "胶皮数量", Enable = true, MinValue = "2", MaxValue = "2" },
            new DetectItemInfo { Name = "露线芯数量", Enable = true, MinValue = "1", MaxValue = "1" }
        };
        Solution.Instance.DetectItemDic["合压数量离线验证"] = items;
        var param = new NodeParamTDAI { CurDetectItemName = "合压数量离线验证", NeedConvert = true, Scale = 10 };
        var detections = new List<DetResult>
        {
            new DetResult { ClassId = 0, Box = new Rect(0, 0, 20, 30) },
            new DetResult { ClassId = 0, Box = new Rect(50, 0, 40, 10) },
            new DetResult { ClassId = 1, Box = new Rect(100, 0, 30, 40) },
            new DetResult { ClassId = 3, Box = new Rect(150, 0, 2, 3) }
        };
        var result = Parse(param, detections);
        Check(result.AlgorithmResult.DetectResults["胶皮数量"][0].Value == "2", "胶皮数量错误");
        Check(result.AlgorithmResult.DetectResults["露线芯数量"][0].Value == "1", "露线芯数量错误");
        Check(result.IsOk && items.TrueForAll(i => i.IsCountItem), "数量型或上下限判定错误");
        result = Parse(param, new List<DetResult>());
        Check(!result.IsOk && result.AlgorithmResult.DetectResults["胶皮数量"][0].Value == "0", "漏检未按零判NG");
        items.ForEach(i => { i.MinValue = "0"; i.MaxValue = "0"; });
        Check(Parse(param, new List<DetResult>()).IsOk, "零上下限未判OK");
        Check(!Parse(param, detections).IsOk, "超出数量上限未判NG");
        items[1].Enable = false;
        Check(!Parse(param, detections).AlgorithmResult.DetectResults.ContainsKey("露线芯数量"), "禁用项仍输出");
        Console.WriteLine("合压数量检测：7项验证通过。");
    }

    /// <summary>调用实际解析器获取结果。</summary>
    private static NodeResultTDAI Parse(NodeParamTDAI param, List<DetResult> detections)
    {
        var result = new NodeResultTDAI();
        ClosingTerminals_encrypted_Parse.Parse(detections, detections.Count, param,
            new TDJS_Vision.Process("合压数量离线验证"), ref result);
        return result;
    }

    /// <summary>验证失败时中止并输出具体原因。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
