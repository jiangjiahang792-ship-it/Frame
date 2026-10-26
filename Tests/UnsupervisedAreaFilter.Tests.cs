using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using TDJS_Vision.Forms.AiTrainForm;
using TDJS_Vision.Node._3_Detection.Unsupervised;

/// <summary>无监督面积、分数及发布结果一致性回归，不加载模型或连接设备。</summary>
internal static class UnsupervisedAreaFilterTests
{
    /// <summary>实际运行处理类型。</summary>
    private static readonly Type Runtime = typeof(NodeUnsupervisedDetection).Assembly.GetType("TDJS_Vision.Node._3_Detection.Unsupervised.UnsupervisedDetectionRuntime");
    /// <summary>累计断言。</summary>
    private static int checks;
    /// <summary>检查输出行为。</summary>
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    /// <summary>调用实际后处理方法。</summary>
    private static object Call(string method, params object[] args) => Runtime.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
    /// <summary>按生产顺序执行面积过滤、坐标映射、分数过滤和发布。</summary>
    private static void Verify(Rectangle[] raw, float area, float score, int code, int expectedCount, bool expectedOk, NodeResultUnsupervisedDetection published)
    {
        var native = new UnsupervisedAnomalibResult(code, score, raw);
        var filtered = (Rectangle[])Call("FilterBoxesByMinimumArea", raw, area);
        var boxes = (List<Rectangle>)Call("OffsetBoxesToSourceImage", filtered, null, 1000, 1000, 1000, 1000, 100, 100);
        bool ok = (bool)Call("ApplyScoreThreshold", native, 0.5f, boxes);
        var runType = Runtime.Assembly.GetType("TDJS_Vision.Node._3_Detection.Unsupervised.UnsupervisedDetectionRunResult");
        var run = Activator.CreateInstance(runType, new object[] { native, null, null, boxes, ok, "回归", "CPU", "", 1000, 1000, 0L, 0L, 0L });
        typeof(NodeUnsupervisedDetection).GetMethod("PublishResult", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { published, run });
        Check(published.BoxCount == expectedCount, "数量必须使用面积和分数过滤后的框");
        Check(published.AlgorithmResult.Rects.Count == expectedCount, "绘制框与数量一致");
        Check(published.IsOk == expectedOk && published.AlgorithmResult.IsAllOk == expectedOk, "节点和绘制判定一致");
        Check((published.AlgorithmResult.RectsNgMap.ContainsKey("无监督异常") ? published.AlgorithmResult.RectsNgMap["无监督异常"].Count : 0) == expectedCount, "NG框缓存不残留");
        Check(published.Score.Equals(score) && native.Boxes.Length == raw.Length, "原始分数和原始框保持不变");
    }

    /// <summary>检查阈值边界、缩放、失败保护及跨帧清理。</summary>
    private static void Main()
    {
        var mixed = new[] { new Rectangle(1,1,9,10), new Rectangle(20,1,10,10), new Rectangle(40,1,11,10) };
        var result = new NodeResultUnsupervisedDetection();
        Verify(mixed,100,0.8f,0,2,false,result);
        Verify(mixed,111,0.8f,0,0,true,result);
        Verify(mixed,100,0.47f,0,0,true,result);
        Verify(mixed,100,0.5f,0,2,false,result);
        Verify(mixed,100.5f,0.8f,0,1,false,result);
        Verify(mixed,0,0.8f,0,3,false,result);
        Verify(mixed,111,0.8f,1,0,false,result);
        Verify(mixed,111,float.NaN,0,0,false,result);
        Verify(mixed,111,float.PositiveInfinity,0,0,false,result);
        Verify(new Rectangle[0],100,0.8f,0,0,true,result);
        var invalid = new[] { new Rectangle(0,0,0,10), new Rectangle(0,0,-2,-3), new Rectangle(0,0,1,1) };
        Check(((Rectangle[])Call("FilterBoxesByMinimumArea",invalid,0f)).Length==1,"无效尺寸不能通过零阈值");
        Check(((Rectangle[])Call("FilterBoxesByMinimumArea",new[]{new Rectangle(0,0,50000,50000)},2000000000f)).Length==1,"面积计算不发生32位溢出");
        Check(((Rectangle[])Call("FilterBoxesByMinimumArea",null,100f)).Length==0,"空返回安全处理");
        Console.WriteLine("无监督面积复筛："+checks+"项检查通过。");
    }
}
