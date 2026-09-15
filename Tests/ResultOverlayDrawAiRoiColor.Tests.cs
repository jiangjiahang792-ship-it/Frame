using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using TDJS_Vision;
using TDJS_Vision.Node;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._3_Detection.TDAI.Parse;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;
using TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw;
using TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw2;

/// <summary>通过实际解析器和显示构建器验证混合OK/NG颜色，不加载设备或运行模型。</summary>
internal static class ResultOverlayDrawAiRoiColorTests
{
    /// <summary>定位内部显示构建器使用的程序集。</summary>
    private static readonly Assembly ApplicationAssembly = typeof(NodeBase).Assembly;

    /// <summary>验证失败时给出明确的业务场景。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>调用真实内部构建方法，避免复制被测颜色判断逻辑。</summary>
    private static object Invoke(string builder, string method, params object[] arguments)
    {
        Type type = ApplicationAssembly.GetType("TDJS_Vision.Node._7_ResultProcessing." + builder, true);
        MethodInfo target = type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == method && candidate.GetParameters().Length == arguments.Length);
        return target.Invoke(null, arguments);
    }

    /// <summary>设置上游结果，不启动节点、相机或通信。</summary>
    private static void SetResult(NodeBase node, INodeResult result)
    {
        typeof(NodeBase).GetProperty("Result").SetValue(node, result);
    }

    /// <summary>创建与用户方案相同的文本和自动ROI订阅。</summary>
    private static NodeParamResultOverlayDraw CreateParameter()
    {
        return new NodeParamResultOverlayDraw
        {
            Items = new List<ResultOverlayDrawItem>
            {
                new ResultOverlayDrawItem { ItemType = ResultOverlayDrawItemType.Text, SourceText1 = "19.目标分类", SourceText2 = "AI输出结果", UseManualText = false, UseJudgeColor = false },
                new ResultOverlayDrawItem { ItemType = ResultOverlayDrawItemType.Roi, SourceText1 = "19.目标分类", SourceText2 = "AI输出结果", UseJudgeColor = false, LineWidth = 3 }
            }
        };
    }

    /// <summary>通过完整显示构建器读取实际节点订阅。</summary>
    private static AlgorithmResult Draw(NodeBase owner, NodeParamResultOverlayDraw parameter)
    {
        return (AlgorithmResult)Invoke("ResultOverlayDraw.ResultOverlayDrawBuilder", "BuildDisplayResult", owner, parameter, null);
    }

    /// <summary>创建解析测试用上下限配置。</summary>
    private static DetectItemInfo Item(string name, string min, string max)
    {
        return new DetectItemInfo { Name = name, MinValue = min, MaxValue = max, Enable = true };
    }

    /// <summary>检查各矩形的颜色和坐标完整保留，且没有修改来源对象。</summary>
    private static void CheckGeometry(AlgorithmResult source, AlgorithmResult target)
    {
        Check(source.Rects.Count == target.Rects.Count, "绘制后矩形数量改变。");
        for (int index = 0; index < source.Rects.Count; index++)
        {
            Check(source.Rects[index].Color.ToArgb() == target.Rects[index].Color.ToArgb(),
                "混合判定中第" + index + "个ROI颜色被整体判定覆盖。");
            Check(!ReferenceEquals(source.Rects[index], target.Rects[index]), "绘制层不能复用可修改的来源矩形对象。");
            Check(source.Rects[index].RotatedRect.center.x == target.Rects[index].RotatedRect.center.x, "矩形位置改变。");
        }
    }

    /// <summary>检查原解析器保留单项颜色、整体NG、文本颜色和直角旋转颜色。</summary>
    private static void VerifyParserAndDrawing(NodeBase owner, NodeBase source, NodeParamResultOverlayDraw parameter,
        List<DetectItemInfo> items, NodeParamTDAI aiParameter)
    {
        Solution.Instance.DetectItemDic[aiParameter.CurDetectItemName] = items;
        var detections = new List<DetResult>
        {
            new DetResult { ClassId = 0, Box = new Rect(10, 20, 70, 10), Score = 1 },
            new DetResult { ClassId = 2, Box = new Rect(100, 20, 41, 10), Score = 1 },
            new DetResult { ClassId = 1, Box = new Rect(170, 20, 140, 150), Score = 1 },
            new DetResult { ClassId = 4, Box = new Rect(20, 100, 80, 150), Score = 1 },
            new DetResult { ClassId = 3, Box = new Rect(330, 20, 45, 10), Score = 1 }
        };
        var result = new NodeResultTDAI();
        RL12Parse.Parse(detections, detections.Count, aiParameter, owner.Process, ref result);
        SetResult(source, result);
        Check(!result.AlgorithmResult.IsAllOk, "后铆脚长度41超出20到35应判NG。");
        Check(result.AlgorithmResult.Rects.Any(rect => rect.Color == Color.Green) &&
            result.AlgorithmResult.Rects.Any(rect => rect.Color == Color.Red), "解析器必须同时输出OK和NG框。");
        AlgorithmResult display = Draw(owner, parameter);
        CheckGeometry(result.AlgorithmResult, display);
        Check(!display.IsAllOk, "保留单框颜色不能把整体NG改成OK。");
        Check(display.Texts.Any(text => text.Color == Color.Green) && display.Texts.Any(text => text.Color == Color.Red), "文本必须保留单项颜色。");
        foreach (int angle in new[] { 90, 180, 270 })
        {
            var rotated = (AlgorithmResult)Invoke("ResultOverlayDraw.ResultOverlayDrawBuilder", "RotateDisplayResult", display, 800, 600, angle);
            Check(rotated.Rects.Select(rect => rect.Color.ToArgb()).SequenceEqual(display.Rects.Select(rect => rect.Color.ToArgb())), "旋转后颜色改变。");
            Check(!rotated.IsAllOk, "旋转后整体NG改变。");
        }
        Console.WriteLine("RL12解析、方案订阅、混合框/文本颜色及三种旋转通过。");
    }

    /// <summary>验证公共加框工具只合并同框条件，且不同模型的统一输出均可保留颜色。</summary>
    private static void VerifySharedGeometry(NodeBase owner, NodeBase source)
    {
        var result = new NodeResultTDAI();
        var keys = new HashSet<string>();
        ParseCommon.AddRect(keys, new Rect(10, 20, 50, 10), true, ref result);
        ParseCommon.AddRect(keys, new Rect(10, 20, 50, 10), false, ref result);
        ParseCommon.AddRect(keys, new Rect(100, 20, 50, 10), true, ref result);
        result.AlgorithmResult.DetectResults["共用框"] = new List<SingleDetectResult> { new SingleDetectResult("共用框", "1", false) };
        Check(result.AlgorithmResult.Rects.Count == 2 && result.AlgorithmResult.Rects[0].Color == Color.Red &&
            result.AlgorithmResult.Rects[1].Color == Color.Green, "同框NG不能影响其他框。");
        result.AlgorithmResult.Lines.Add(new ColorLine(new PointF(0, 0), new PointF(10, 10), Color.Green));
        SetResult(source, result);
        foreach (bool legacyFlag in new[] { false, true })
        {
            var parameter = CreateParameter();
            parameter.Items = parameter.Items.Where(item => item.ItemType == ResultOverlayDrawItemType.Roi).ToList();
            parameter.Items[0].UseJudgeColor = legacyFlag;
            foreach (string path in new[] { "AI输出结果", "AlgorithmResult" })
            {
                parameter.Items[0].SourceText2 = path;
                var display = Draw(owner, parameter);
                CheckGeometry(result.AlgorithmResult, display);
                Check(display.Texts.Count == 0 && display.Lines[0].Color == Color.Green, "自动ROI只复制几何并保留元素颜色。");
                Check(display.Rects.All(rect => rect.LineWidth == 3), "用户线宽未生效。");
            }
        }
        var ordinary = new NodeResultTDAI();
        ordinary.AlgorithmResult.Rects.Add(new ColorRotatedRect(new Rect(0, 0, 10, 10), Color.Blue));
        SetResult(source, ordinary);
        foreach (bool judge in new[] { false, true })
        {
            ordinary.JudgeOk = judge;
            var parameter = CreateParameter();
            parameter.OkColorArgb = Color.Yellow.ToArgb();
            parameter.NgColorArgb = Color.Magenta.ToArgb();
            Check(Draw(owner, parameter).Rects[0].Color.ToArgb() == (judge ? Color.Yellow : Color.Magenta).ToArgb(), "无检测明细的普通几何应保留自动判定着色。");
        }
        SetResult(source, result);
        Console.WriteLine("公共加框、同框合并、遗留字段、订阅形式、线宽及普通几何颜色通过。");
    }

    /// <summary>验证绘制2默认保色、旧规则兼容，以及显式布尔统一着色。</summary>
    private static void VerifyDraw2(NodeBase owner, NodeBase source)
    {
        var parameter = new NodeParamResultOverlayDraw2
        {
            Items = new List<ResultOverlayDraw2Item>
            {
                new ResultOverlayDraw2Item { ItemType = ResultOverlayDraw2ItemType.Roi, SourceText1 = "19.目标分类", SourceText2 = "AI输出结果" }
            }
        };
        var result = (NodeResultTDAI)source.Result;
        foreach (int mode in new[] { 0, 1, 2 })
        {
            if (mode == 1) parameter.ColorRules.Add(new ResultOverlayDraw2ColorRule { Enabled = true, SourceText1 = "19.目标分类", SourceText2 = "JudgeOk" });
            if (mode == 2) { parameter.JudgeText1 = "19.目标分类"; parameter.JudgeText2 = "JudgeOk"; }
            object state = Invoke("ResultOverlayDraw2.ResultOverlayDraw2Builder", "ResolveColorState", owner, parameter);
            var display = (AlgorithmResult)Invoke("ResultOverlayDraw2.ResultOverlayDraw2Builder", "BuildDisplayResult", owner, parameter, state);
            if (mode < 2) CheckGeometry(result.AlgorithmResult, display);
            else Check(display.Rects.All(rect => rect.Color.ToArgb() == Color.Red.ToArgb()), "显式布尔NG颜色订阅应统一着色。");
        }
        Console.WriteLine("绘制2默认保色、旧规则兼容和显式布尔覆盖通过。");
    }

    /// <summary>运行离线回归；可选传入方案路径，只读取检测项和绘制参数。</summary>
    [STAThread]
    private static int Main(string[] arguments)
    {
        try
        {
            var process = new TDJS_Vision.Process("ROI颜色离线回归");
            var source = new NodeBase(19, "目标分类", process, NodeType.AITD);
            var owner = new NodeBase(25, "ROI结果绘制", process, NodeType.ResultOverlayDraw);
            process.Nodes.AddRange(new[] { source, owner });
            process.Connections.Add(new ProcessConnection { FromNodeId = 19, ToNodeId = 25 });
            var parameter = CreateParameter();
            var aiParameter = new NodeParamTDAI { CurDetectItemName = "颜色回归", NeedConvert = false };
            var items = new List<DetectItemInfo>
            {
                Item("DetectItem.FrontRivetCoreLength", "70", "80"),
                Item("DetectItem.BackRivetCoreLength", "20", "35")
            };
            VerifyParserAndDrawing(owner, source, parameter, items, aiParameter);
            if (arguments.Length > 0)
            {
                JObject solution = JObject.Parse(File.ReadAllText(arguments[0]));
                parameter = solution.Descendants().OfType<JProperty>()
                    .Single(property => property.Name == typeof(NodeParamResultOverlayDraw).FullName).Value.ToObject<NodeParamResultOverlayDraw>();
                aiParameter = solution.Descendants().OfType<JProperty>()
                    .Single(property => property.Name == typeof(NodeParamTDAI).FullName).Value.ToObject<NodeParamTDAI>();
                items = solution["DetectItemDic"][aiParameter.CurDetectItemName].ToObject<List<DetectItemInfo>>();
                VerifyParserAndDrawing(owner, source, parameter, items, aiParameter);
            }
            VerifySharedGeometry(owner, source);
            VerifyDraw2(owner, source);
            Console.WriteLine("AI矩形颜色专项全部通过。");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
