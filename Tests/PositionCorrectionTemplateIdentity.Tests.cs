using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using TDJS_Vision.Node._3_Detection.MatchTemplate;
using TDJS_Vision.Node._3_Detection.ContourMatch;
using TDJS_Vision.Node._4_Measurement.Common;
using TDJS_Vision.Node._4_Measurement.PositionCorrection;

/// <summary>验证不同模板中心、基准保存恢复、旧方案兼容和重建失效，不依赖现场设备。</summary>
internal static class PositionCorrectionTemplateIdentityTests
{
    /// <summary>记录行为断言数量。</summary>
    private static int checks;

    /// <summary>创建具有明确模板身份的测试位姿。</summary>
    private static TemplateMatchPose Pose(string id, double x, double y, double angle, double width)
    {
        return new TemplateMatchPose { TemplateId = id, TemplateRevision = "版本1", UniqueTemplateSize = true,
            CenterX = x, CenterY = y, Angle = angle, Width = width, Height = 100,
            TargetIndex = 1, IsValid = true };
    }

    /// <summary>通过实际生产入口计算位置修正。</summary>
    private static PositionCorrectionInfo Correct(TemplateMatchPose baseline, TemplateMatchPose current)
    {
        var method = typeof(NodePositionCorrection).GetMethod("BuildCorrectionItems", BindingFlags.Static | BindingFlags.NonPublic);
        try
        {
            return ((List<PositionCorrectionInfo>)method.Invoke(null,
                new object[] { baseline, new List<TemplateMatchPose> { current } }))[0];
        }
        catch (TargetInvocationException error) { throw error.InnerException; }
    }

    /// <summary>检查独立给定的世界坐标，避免仅复算实现公式。</summary>
    private static void PointEquals(PositionCorrectionInfo correction, float x, float y, float expectedX, float expectedY)
    {
        var point = correction.TransformPoint(x, y);
        if (Math.Abs(point.X - expectedX) > 0.001 || Math.Abs(point.Y - expectedY) > 0.001)
            throw new Exception("不同模板切换后测量点偏离公共基准。");
        checks++;
    }

    /// <summary>未标定、版本变化或旧基准身份不明时必须明确拒绝。</summary>
    private static void Reject(TemplateMatchPose baseline, TemplateMatchPose current)
    {
        try { Correct(baseline, current); }
        catch (InvalidOperationException) { checks++; return; }
        throw new Exception("不兼容模板仍输出了位置修正。");
    }

    /// <summary>运行独立几何与序列化回归场景。</summary>
    private static int Main()
    {
        try
        {
            var a = Pose("A", 100, 100, 0, 20);
            var b = Pose("B", 130, 160, 15, 30);
            a.ReferencePoses = new List<TemplateMatchPose> { b };
            var saved = JsonConvert.DeserializeObject<TemplateMatchPose>(JsonConvert.SerializeObject(a.Clone()));
            // 同一基准帧先顺时针90度再平移(300,20)，不同模板角度零点亦不同。
            var movedA = Pose("A", 200, 120, 90, 20);
            var movedB = Pose("B", 140, 150, 105, 30);
            PointEquals(Correct(saved, movedB), 110, 120, 180, 130);
            movedB.ReferencePoses = new List<TemplateMatchPose> { movedA };
            PointEquals(Correct(saved, movedB), 110, 120, 180, 130);
            // 无模板B标定时，可以使用同帧保留的模板A，不套用模板B中心。
            var primaryOnly = Pose("A", 100, 100, 0, 20);
            PointEquals(Correct(primaryOnly, movedB), 110, 120, 180, 130);
            movedB.ReferencePoses = null;
            Reject(primaryOnly, movedB);
            movedA.TemplateRevision = "版本2";
            Reject(saved, movedA);
            Reject(saved, Pose("C", 200, 120, 90, 20));
            var legacy = Pose(null, 100, 100, 0, 20);
            PointEquals(Correct(legacy, movedA), 110, 120, 180, 130);
            movedA.UniqueTemplateSize = false;
            Reject(legacy, movedA);
            Reject(legacy, movedB);
            var oldCurrent = Pose(null, 200, 120, 90, 20);
            PointEquals(Correct(legacy, oldCurrent), 110, 120, 180, 130);
            saved.ReferencePoses[0].CenterX = 0;
            if (a.ReferencePoses[0].CenterX != 130) throw new Exception("基准副本共享可变位姿。");
            checks++;
            // 顺着45度长轴移动20像素，两个200×20框应保留90%重叠，反号会误判为分离。
            var session = typeof(NativeShapeMatcher).Assembly.GetType("TDJS_Vision.Node._3_Detection.ContourMatch.ContourMatchSession");
            var overlap = session.GetMethod("Overlap", BindingFlags.Static | BindingFlags.NonPublic);
            var first = new ShapeMatchResult { CenterX = 200, CenterY = 200, Width = 200, Height = 20, AngleDegrees = 45 };
            var second = new ShapeMatchResult { CenterX = 200 + Math.Sqrt(200), CenterY = 200 + Math.Sqrt(200),
                Width = 200, Height = 20, AngleDegrees = 45 };
            double ratio = (double)overlap.Invoke(null, new object[] { first, second });
            if (Math.Abs(ratio - 0.9) > 0.001) throw new Exception("模板候选重叠检测的旋转方向错误。");
            checks++;
            Console.WriteLine("模板基准回归通过，断言数：" + checks);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
