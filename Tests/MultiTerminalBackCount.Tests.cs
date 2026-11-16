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
        try
        {
            RunAll();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"{exception.GetType().FullName}: {exception.Message}");
            if (exception.InnerException != null)
                Console.Error.WriteLine($"内部异常：{exception.InnerException.GetType().FullName}: {exception.InnerException.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>运行全部多端子解析回归场景。</summary>
    private static void RunAll()
    {
        VerifyVerticallyOrderedBiteGlue();
        Console.WriteLine("多端子咬胶纵向配对：2项验证通过。");

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
        Check(count.IsCountItem && result.AlgorithmResult.DetectResults["DetectItem.BackRivetCount"][0].Value == "2",
            "后铆脚未固定按数量统计");
        Check(!result.AlgorithmResult.DetectResults.ContainsKey("DetectItem.TerminalCount"), "旧端子数量仍输出结果");
        boxes.RemoveAt(0);
        Check(!Parse(param, boxes).AlgorithmResult.DetectResults["DetectItem.BackRivetCount"][0].IsOk, "数量不足未判NG");
        boxes.RemoveAt(0);
        Check(Parse(param, boxes).AlgorithmResult.DetectResults["DetectItem.BackRivetCount"][0].Value == "0", "缺失未输出0");
        count.MinValue = count.MaxValue = "0";
        Check(Parse(param, boxes).IsOk, "零数量上下限错误");
        count.Enable = false;
        Check(!Parse(param, boxes).AlgorithmResult.DetectResults.ContainsKey("DetectItem.BackRivetCount"), "禁用项仍执行");
        Console.WriteLine("多端子后铆脚数量：7项验证通过。");
    }

    /// <summary>验证多个竖向物料按中心Y配对，并且咬胶只使用X方向宽度和间距计算。</summary>
    private static void VerifyVerticallyOrderedBiteGlue()
    {
        Solution.Instance.DetectItemDic["多物料咬胶验证"] = new List<DetectItemInfo>
        {
            new DetectItemInfo { Name = "咬胶长度", Enable = true, MinValue = "20", MaxValue = "50" }
        };
        var param = new NodeParamTDAI { CurDetectItemName = "多物料咬胶验证", NeedConvert = false };

        // 故意打乱模型输出顺序：上方物料应为15/30*100=50，下方物料应为10/50*100=20。
        var boxes = new List<DetResult>
        {
            new DetResult { ClassId = 1, Box = new Rect(200, 200, 20, 30) },
            new DetResult { ClassId = 3, Box = new Rect(160, 210, 10, 80) },
            new DetResult { ClassId = 4, Box = new Rect(50, 10, 20, 30) },
            new DetResult { ClassId = 3, Box = new Rect(75, 20, 15, 120) },
            new DetResult { ClassId = 4, Box = new Rect(120, 200, 30, 30) },
            new DetResult { ClassId = 1, Box = new Rect(100, 10, 20, 30) }
        };

        NodeResultTDAI result = Parse(param, boxes);
        List<SingleDetectResult> biteResults = result.AlgorithmResult.DetectResults["DetectItem.BiteGlueLength"];
        Check(biteResults.Count == 2 && biteResults[0].Value == "50.00" && biteResults[1].Value == "20.00",
            "多个竖向物料未按中心Y逐一配对，或咬胶仍混入了高度面积");
        Check(biteResults[0].IsOk && biteResults[1].IsOk, "咬胶纵向配对后的上下限判定错误");
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
