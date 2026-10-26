using System;
using System.Collections.Generic;
using OpenCvSharp;
using TDJS_Vision;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._3_Detection.TDAI.Parse;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

/// <summary>验证多端子后铆脚独立计数和旧公共数量限制取消。</summary>
internal static class MultiTerminalBackCountTests
{
    /// <summary>运行无相机和模型依赖的实际解析验证。</summary>
    [STAThread]
    private static void Main()
    {
        var count = new DetectItemInfo { Name = "后铆脚数量", Enable = true, MinValue = "2", MaxValue = "2" };
        Solution.Instance.DetectItemDic["后铆脚验证"] = new List<DetectItemInfo> {
            count,
            new DetectItemInfo { Name = "端子数量", Enable = true, IsCountItem = true, MinValue = "99", MaxValue = "99" },
            new DetectItemInfo { Name = "前铆脚面积", Enable = true, MinValue = "4000", MaxValue = "4000" }
        };
        var param = new NodeParamTDAI { CurDetectItemName = "后铆脚验证", NeedConvert = true, Scale = 10 };
        var boxes = new List<DetResult> {
            new DetResult { ClassId = 4, Box = new Rect(0, 0, 20, 30) },
            new DetResult { ClassId = 4, Box = new Rect(50, 0, 30, 40) },
            new DetResult { ClassId = 1, Box = new Rect(100, 0, 20, 20) }
        };
        var result = Parse(param, boxes);
        Check(result.IsOk, "不同类别数量不同或旧端子数量仍影响结果");
        Check(count.IsCountItem && result.AlgorithmResult.DetectResults["后铆脚数量"][0].Value == "2", "后铆脚未固定按数量统计");
        Check(!result.AlgorithmResult.DetectResults.ContainsKey("DetectItem.TerminalCount"), "旧端子数量仍输出结果");
        boxes.RemoveAt(0);
        Check(!Parse(param, boxes).AlgorithmResult.DetectResults["后铆脚数量"][0].IsOk, "数量不足未判NG");
        boxes.RemoveAt(0);
        Check(Parse(param, boxes).AlgorithmResult.DetectResults["后铆脚数量"][0].Value == "0", "缺失未输出0");
        count.MinValue = count.MaxValue = "0";
        Check(Parse(param, boxes).IsOk, "零数量上下限错误");
        count.Enable = false;
        Check(!Parse(param, boxes).AlgorithmResult.DetectResults.ContainsKey("后铆脚数量"), "禁用项仍执行");
        Console.WriteLine("多端子后铆脚数量：7项验证通过。");
    }

    /// <summary>直接调用生产解析器。</summary>
    private static NodeResultTDAI Parse(NodeParamTDAI param, List<DetResult> boxes)
    {
        var result = new NodeResultTDAI();
        MultiTerminalParse.Parse(boxes, boxes.Count, param, new TDJS_Vision.Process("离线验证"), ref result);
        return result;
    }

    /// <summary>发现错误立即报告场景。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
