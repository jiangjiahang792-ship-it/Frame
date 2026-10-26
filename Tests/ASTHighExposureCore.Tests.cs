using System;
using System.Collections.Generic;
using OpenCvSharp;
using TDJS_Vision;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._3_Detection.TDAI.Parse;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

/// <summary>高曝光out_line类别映射、尺寸判定及工位2兼容性验证。</summary>
internal static class ASTHighExposureCoreTests
{
    /// <summary>执行真实解析器的离线检查，不加载模型或连接相机。</summary>
    [STAThread]
    private static void Main()
    {
        var width = new DetectItemInfo { Name = "出线芯宽度", Enable = true, MinValue = "1", MaxValue = "100", IsCountItem = true };
        var length = new DetectItemInfo { Name = "出线芯长度", Enable = true, MinValue = "1", MaxValue = "100" };
        Solution.Instance.DetectItemDic["高曝光验证"] = new List<DetectItemInfo> { width, length };
        var param = new NodeParamTDAI { CurDetectItemName = "高曝光验证", ModelName = ModelName.AST高曝光线芯模型, Scale = 0.5F };
        var boxes = new List<DetResult> {
            new DetResult { ClassId = 0, Box = new Rect(10, 20, 20, 40) },
            new DetResult { ClassId = 1, Box = new Rect(60, 20, 6, 8) }
        };
        var result = Parse(param, boxes);
        Check(Value(result, width.Name) == 20 && Value(result, length.Name) == 40 && result.IsOk, "类别0宽度长度解析错误");
        Check(!width.IsCountItem, "尺寸项未纠正数量类型");
        param.NeedConvert = true;
        result = Parse(param, boxes);
        Check(Value(result, width.Name) == 10 && Value(result, length.Name) == 20, "尺寸换算错误");
        width.MaxValue = "9";
        Check(!Parse(param, boxes).IsOk, "超限未判NG");
        width.MaxValue = "100";
        result = Parse(param, new List<DetResult>());
        Check(Value(result, length.Name) == 0 && !result.IsOk, "漏检零值判定错误");
        width.Enable = false;
        Check(!Parse(param, boxes).AlgorithmResult.DetectResults.ContainsKey(width.Name), "禁用项仍输出");
        width.Enable = true;
        param.ModelName = ModelName.AST_工位2模型;
        param.NeedConvert = false;
        result = Parse(param, boxes);
        Check(Value(result, width.Name) == 6 && Value(result, length.Name) == 8, "原工位2类别1发生改变");
        param.ModelName = ModelName.AST高曝光线芯模型;
        param.IsAutoStudy = true;
        param.StudyNum = 1;
        param.StudyPercentage = 0;
        Parse(param, boxes);
        Check(!param.IsAutoStudy && float.Parse(width.MinValue) == 20 && float.Parse(length.MaxValue) == 40, "自动学习尺寸错误");
        Console.WriteLine("AST高曝光线芯：8项验证通过。");
    }

    /// <summary>解析一帧离线检测框。</summary>
    private static NodeResultTDAI Parse(NodeParamTDAI param, List<DetResult> boxes)
    {
        var result = new NodeResultTDAI();
        ASTStation2Parse.Parse(boxes, boxes.Count, param, new TDJS_Vision.Process("高曝光验证"), ref result);
        return result;
    }

    /// <summary>读取单项数值。</summary>
    private static float Value(NodeResultTDAI result, string name)
    {
        return float.Parse(result.AlgorithmResult.DetectResults[name][0].Value);
    }

    /// <summary>在场景不符合预期时报告原因。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
