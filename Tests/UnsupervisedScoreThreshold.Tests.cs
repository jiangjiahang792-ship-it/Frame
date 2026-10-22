using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using TDJS_Vision.Forms.AiTrainForm;
/// <summary>无监督总分过滤的真实程序集回归。</summary>
class UnsupervisedScoreThresholdTests
{
    /// <summary>检查分数边界、框数量及原始分数保留。</summary>
    static void Verify(float score, float threshold, int code, bool hasBox, bool expectedOk, int expectedCount)
    {
        var boxes=new List<Rectangle>();
        if(hasBox) boxes.Add(new Rectangle(1,2,30,40));
        var result=new UnsupervisedAnomalibResult(code,score,boxes.ToArray());
        var type=typeof(UnsupervisedAnomalibResult).Assembly.GetType("TDJS_Vision.Node._3_Detection.Unsupervised.UnsupervisedDetectionRuntime");
        var method=type.GetMethod("ApplyScoreThreshold",BindingFlags.NonPublic|BindingFlags.Static);
        bool ok=(bool)method.Invoke(null,new object[]{result,threshold,boxes});
        if(ok!=expectedOk || boxes.Count!=expectedCount || !result.Score.Equals(score)) throw new Exception("分数过滤不符合预期："+score);
    }
    /// <summary>执行用户示例及失败保护回归。</summary>
    static void Main()
    {
        Verify(0.47f,0.5f,0,true,true,0);
        Verify(0.4999f,0.5f,0,true,true,0);
        Verify(0.5f,0.5f,0,true,false,1);
        Verify(0.7f,0.5f,0,true,false,1);
        Verify(0.7f,0.5f,0,false,true,0);
        Verify(0.47f,0.4f,0,true,false,1);
        Verify(0.47f,0.5f,1,true,false,1);
        Verify(0.47f,0.5f,1,false,false,0);
        Verify(float.NaN,0.5f,0,false,false,0);
        Verify(float.PositiveInfinity,0.5f,0,true,false,1);
        Verify(float.NegativeInfinity,0.5f,0,false,false,0);
        Console.WriteLine("无监督分数阈值11组回归通过。");
    }
}
