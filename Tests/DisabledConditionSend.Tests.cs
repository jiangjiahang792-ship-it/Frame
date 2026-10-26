using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;
using TDJS_Vision;
using TDJS_Vision.Node;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.BlobAnalysis;
using TDJS_Vision.Node._4_Measurement.LineLineAngle;
using TDJS_Vision.Node._6_LogicTool.CompositeModule;
using TDJS_Vision.Node._6_LogicTool.MultiCondition;
using TDJS_Vision.Node._7_ResultProcessing.ResultSend;

/// <summary>用客户方案的真实订阅、条件计算和发送转换验证禁用行为，全程不连接设备。</summary>
internal static class DisabledConditionSendTests
{
    /// <summary>累计断言数量。</summary>
    private static int checks;
    /// <summary>设置无设备夹具状态。</summary>
    private static void Set(object obj, Type type, string name, object value) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
    /// <summary>验证约束。</summary>
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    /// <summary>模拟共享变量及四则运算结果，其他检测使用真实结果类型。</summary>
    private sealed class ScalarResult : INodeResult, IDynamicResultVariables
    {
        /// <summary>运行时间。</summary>
        public int RunTime { get; set; }
        /// <summary>独立输出值。</summary>
        public Dictionary<string, object> Values = new Dictionary<string, object>();
        /// <summary>输出目录。</summary>
        public IEnumerable<string> GetDynamicVariableNames() => Values.Keys;
        /// <summary>读取动态结果。</summary>
        public bool TryGetDynamicVariable(string name, out object value) => Values.TryGetValue(DynamicResultVariableResolver.ExtractVariableName(name), out value);
    }
    /// <summary>标记节点已产生本轮结果。</summary>
    private static void Ready(NodeBase node, INodeResult result)
    {
        Set(node, typeof(NodeBase), "_result", result);
        Set(node, typeof(NodeBase), "_runtimeStatus", NodeStatus.Successful);
        Set(node, typeof(NodeBase), "<LastSuccessfulRunId>k__BackingField", 321);
    }
    /// <summary>初始化当前轮结果，递归覆盖嵌套模块。</summary>
    private static void Initialize(TDJS_Vision.Process scope)
    {
        Set(scope, typeof(TDJS_Vision.Process), "<CurrentRunId>k__BackingField", 321);
        scope.IsRuning = true;
        foreach (var node in scope.Nodes)
        {
            INodeResult result = new ScalarResult();
            if (node.ParamForm.Params is NodeParamTDAI ai)
            {
                var r = new NodeResultTDAI { JudgeOk = true };
                if (Solution.Instance.DetectItemDic.TryGetValue(ai.DetectItemName1 ?? ai.CurDetectItemName, out var definitions))
                    foreach (var d in definitions) r.AlgorithmResult.DetectResults[d.Name] = new List<SingleDetectResult> { new SingleDetectResult { IsOk = true, Value = "0" } };
                result = r;
            }
            else if (node.ParamForm.Params is NodeParamBlobAnalysis) result = new NodeResultBlobAnalysis { IsOk = true, DetectionRegions = new List<ColorRotatedRect>() };
            else if (node.ParamForm.Params.GetType().Name == "NodeParamLineLineAngle") result = new NodeResultLineLineAngle { IsOk = true };
            else if (node is NodeMultiCondition) result = new NodeResultMultiCondition();
            else if (node is NodeCompositeModule module)
            {
                Initialize(module.GetEditableProcess());
                result = new NodeResultCompositeModule { InternalSuccess = true, OutputRunId = 321 };
            }
            Ready(node, result);
        }
    }
    /// <summary>解析条件到真正来源，支持嵌套组合模块。</summary>
    private static NodeBase Leaf(NodeBase judge, MultiConditionItem c, out string path)
    {
        var source = judge.Process.Nodes.Single(n => n.ID == c.SourceNodeId);
        if (source is NodeCompositeModule) return CompositeSubscriptionResolver.Resolve(source, c.PropertyPath, out path);
        path = c.PropertyPath; return source;
    }
    /// <summary>按条件阈值产生明确合格或不合格的检测输入。</summary>
    private static void Input(NodeBase judge, MultiConditionItem c, bool ok)
    {
        var leaf = Leaf(judge, c, out var path);
        double a, b; double.TryParse(c.Value1, out a); double.TryParse(c.Value2, out b);
        object value;
        switch (c.Operator)
        {
            case MultiConditionOperator.IsTrue: value = ok; break;
            case MultiConditionOperator.Between: value = ok ? (a + b) / 2 : Math.Max(a, b) + 1000; break;
            case MultiConditionOperator.LessThan: value = ok ? a - 1 : a + 1000; break;
            case MultiConditionOperator.GreaterThan: value = ok ? a + 1 : a - 1; break;
            default: throw new Exception("测试尚未覆盖操作符：" + c.Operator);
        }
        if (leaf.Result is ScalarResult scalar) scalar.Values[DynamicResultVariableResolver.ExtractVariableName(path)] = value;
        else
        {
            leaf.Result.GetType().GetProperty(path).SetValue(leaf.Result, Convert.ChangeType(value, Nullable.GetUnderlyingType(leaf.Result.GetType().GetProperty(path).PropertyType) ?? leaf.Result.GetType().GetProperty(path).PropertyType));
            if (leaf.Result is NodeResultBlobAnalysis blob)
                blob.Items = new List<NodeResultBlobAnalysis> { new NodeResultBlobAnalysis { IsOk = true, Area = blob.Area, Count = blob.Count, TargetIndex = 1 } };
            if (leaf.Result is NodeResultLineLineAngle angle)
                angle.Items = new List<LineLineAngleTargetResult> { new LineLineAngleTargetResult { IsOk = true, Angle = angle.Angle.Value, TargetIndex = 1 } };
        }
    }
    /// <summary>从叶节点向外发布每层模块的当前结果。</summary>
    private static void Publish(TDJS_Vision.Process scope)
    {
        foreach (var m in scope.Nodes.OfType<NodeCompositeModule>())
        {
            Publish(m.GetEditableProcess());
            typeof(NodeCompositeModule).GetMethod("CollectOutputValues", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                new object[] { m.Result, m.GetEditableProcess(), (NodeParamCompositeModule)m.ParamForm.Params, false });
        }
    }
    /// <summary>执行真实条件引擎，保留禁用单项的放行输出。</summary>
    private static void Evaluate(NodeBase judge)
    {
        Publish(judge.Process);
        var args = new object[] { judge, judge.ParamForm.Params, null };
        var ok = (bool)typeof(NodeBase).Assembly.GetType("TDJS_Vision.Node._6_LogicTool.MultiCondition.MultiConditionEvaluator").GetMethod("Evaluate").Invoke(null, args);
        Ready(judge, new NodeResultMultiCondition { ConditionResult = ok, Details = (List<NodeConditionEvaluation>)args[2] });
    }
    /// <summary>走实际发送读取与转换入口，检查最终写入的整数。</summary>
    private static int Send(NodeBase configured, ResultSendRow row)
    {
        var sender = (NodeResultSend)FormatterServices.GetUninitializedObject(typeof(NodeResultSend));
        sender.Process = configured.Process;
        var upstream = configured.Process.GetUpstreamNodes(configured).ToDictionary(n => n.ID);
        var values = (List<object>)typeof(NodeResultSend).GetMethod("ReadRowValues", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(sender, new object[] { row, true, upstream });
        return Convert.ToInt32(ResultSendValueConverter.Prepare(row, values, 1).Values.GetValue(0));
    }
    /// <summary>检查各配方真实飞丝及总结果，逐个禁用、恢复及其他NG组合。</summary>
    private static void Verify(TDJS_Vision.Process scope, string label)
    {
        foreach (var sender in scope.Nodes.Where(n => n.Active && n.ParamForm.Params is NodeParamResultSend))
        {
            var rows = ((NodeParamResultSend)sender.ParamForm.Params).Rows.Where(r => r.Enabled).ToList();
            var total = rows.Last();
            var judge = scope.Nodes.Single(n => n.ID == total.Source.NodeId);
            var param = (NodeParamMultiCondition)judge.ParamForm.Params;
            foreach (var c in param.Conditions) Input(judge, c, true);
            var fly = rows.FirstOrDefault(r => r.GetEffectiveSources().Any(s => s.ItemName == "DetectItem.FlyingWire"));
            Evaluate(judge);
            Check(Send(sender, total) == 1, label + "初始总结果不是1");
            if (fly != null) Check(Send(sender, fly) == 1, label + "初始飞丝不是1");
            var blobs = param.Conditions.Where(c => Leaf(judge, c, out _)?.Result is NodeResultBlobAnalysis).ToList();
            var originalStates = blobs.Select(c => c.Enabled).ToArray();
            // 所有Blob均超限，穷举各视角开关组合，防止一个禁用项掩盖其他启用项。
            for (int mask = 0; mask < (1 << blobs.Count); mask++)
            {
                for (int i = 0; i < blobs.Count; i++)
                {
                    blobs[i].Enabled = (mask & (1 << i)) != 0;
                    Input(judge, blobs[i], false);
                }
                Evaluate(judge);
                Check(Send(sender, total) == (mask == 0 ? 1 : 2), label + "Blob开关组合总结果错误：" + mask);
                if (fly != null) Check(Send(sender, fly) == (mask == 0 ? 1 : 2), label + "Blob开关组合飞丝错误：" + mask);
            }
            for (int i = 0; i < blobs.Count; i++) { blobs[i].Enabled = originalStates[i]; Input(judge, blobs[i], true); }
            foreach (var c in blobs)
            {
                bool enabled = c.Enabled;
                Input(judge, c, false);
                foreach (bool on in new[] { true, false, true, false })
                {
                    c.Enabled = on; Evaluate(judge);
                    Check(Send(sender, total) == (on ? 2 : 1), label + c.Name + "禁用/恢复总结果错误");
                    if (fly != null) Check(Send(sender, fly) == (on ? 2 : 1), label + c.Name + "禁用/恢复飞丝错误");
                }
                foreach (var other in param.Conditions.Where(o => o != c && o.Enabled))
                {
                    Input(judge, other, false); Evaluate(judge);
                    Check(Send(sender, total) == 2, label + "禁用Blob掩盖其他NG：" + other.Name);
                    Input(judge, other, true);
                }
                Input(judge, c, true); c.Enabled = enabled;
            }
            // AI飞丝与Blob是独立来源，禁用全部Blob也不能掩盖仍启用的AI飞丝NG。
            var states = blobs.Select(c => c.Enabled).ToArray();
            foreach (var c in blobs) c.Enabled = false;
            if (fly != null)
                foreach (var source in fly.GetEffectiveSources().Where(s => s.Kind == ResultSendSourceKind.AiJudgment))
                {
                    var aiNode = scope.Nodes.Single(n => n.ID == source.NodeId);
                    var ai = (NodeResultTDAI)aiNode.Result;
                    var definition = (NodeParamTDAI)aiNode.ParamForm.Params;
                    var d = Solution.Instance.DetectItemDic[definition.DetectItemName1 ?? definition.CurDetectItemName].Single(i => i.Name == source.ItemName);
                    if (!d.Enable) continue;
                    ai.AlgorithmResult.DetectResults[source.ItemName][0].IsOk = false; ai.JudgeOk = false;
                    Evaluate(judge);
                    Check(Send(sender, fly) == 2, label + "禁用Blob掩盖AI飞丝NG");
                    Check(Send(sender, total) == 2, label + "AI飞丝NG总结果不是2");
                    ai.AlgorithmResult.DetectResults[source.ItemName][0].IsOk = true; ai.JudgeOk = true;
                }
            for (int i = 0; i < blobs.Count; i++) blobs[i].Enabled = states[i];
            foreach (var c in param.Conditions.Where(c => c.Enabled))
            {
                Input(judge, c, false); Evaluate(judge);
                Check(Send(sender, total) == 2, label + "启用项NG没有发2：" + c.Name);
                Input(judge, c, true);
            }
            Evaluate(judge); Check(Send(sender, total) == 1, label + "恢复后总结果不为1");
            Console.WriteLine(label + "/发送" + sender.ID + "通过，Blob条件=" + blobs.Count + "，总结果地址=" + total.Address);
        }
        foreach (var m in scope.Nodes.OfType<NodeCompositeModule>()) Verify(m.GetEditableProcess(), label + "/" + m.ID);
    }
    /// <summary>加载客户方案到内存，不写方案、不连接PLC。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            LanguageManager.Initialize();
            var data = JObject.Parse(File.ReadAllText(args[0]));
            Solution.Instance.DetectItemDic = data["DetectItemDic"].ToObject<Dictionary<string, List<DetectItemInfo>>>();
            foreach (JObject p in data["ProcessInfos"])
            {
                var scope = (TDJS_Vision.Process)typeof(Station2ModuleSplitTests).GetMethod("ReadScope", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { p });
                Initialize(scope); Verify(scope, "流程" + p["ID"]);
            }
            Console.WriteLine("全部通过：" + checks + "项断言；方案只读，未连接PLC。"); return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
}




