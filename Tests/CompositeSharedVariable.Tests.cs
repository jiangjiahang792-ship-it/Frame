using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using TDJS_Vision;
using TDJS_Vision.Node;
using TDJS_Vision.Node._6_LogicTool.ArithmeticOperation;
using TDJS_Vision.Node._6_LogicTool.CompositeModule;
using TDJS_Vision.Node._6_LogicTool.SharedVariable;
using TDJS_Vision.Node._6_LogicTool.MultiCondition;
/// <summary>验证模块内四则运算经共享变量传到模块外的订阅及实际判断。</summary>
internal static class CompositeSharedVariableTests
{
    /// <summary>使用真实模块内部流程，覆盖首次运行前类型、重复编号和运行值。</summary>
    [STAThread]
    private static void Main()
    {
        try { Run(); }
        catch (Exception ex) { Console.Error.WriteLine(ex.GetType().FullName + ": " + ex.Message); Console.Error.WriteLine(ex.StackTrace); Environment.ExitCode = 1; }
    }

    /// <summary>构造并执行隔离的共享变量流程。</summary>
    private static void Run()
    {
        var root = new TDJS_Vision.Process("跨模块共享验证");
        Solution.Instance.AllProcesses.Add(root);
        var module = new NodeCompositeModule(1, "组合模块", root, NodeType.CompositeModule);
        root.Nodes.Add(module);
        var inner = module.GetEditableProcess();
        var nested = new NodeCompositeModule(1, "嵌套模块", inner, NodeType.CompositeModule);
        inner.Nodes.Add(nested);
        var writerProcess = nested.GetEditableProcess();
        var math = new NodeArithmeticOperation(1, "四则运算", writerProcess, NodeType.ArithmeticOperation);
        var writer = new NodeSharedVariable(2, "共享变量1", writerProcess, NodeType.SharedVariable);
        writerProcess.Nodes.AddRange(new NodeBase[] { math, writer });
        writerProcess.Connections.Add(new ProcessConnection { FromNodeId = 1, ToNodeId = 2 });
        math.ParamForm.Params = new NodeParamArithmeticOperation { Rows = {
            new ArithmeticOperationRow { OutputVariableName = "V1",
                Value1 = new ArithmeticOperand { ConstantText = "2" },
                Value2 = new ArithmeticOperand { ConstantText = "3" } } } };
        writer.ParamForm.Params = new NodeParamSharedVariable { IsRead = false,
            ReadName = "[未选择]", WriteName = "跨模块V1", Text1 = "1.四则运算", Text2 = "变量.V1" };
        writer.ParamForm.SetParam2Form();
        var reader = new NodeSharedVariable(3, "共享变量2", root, NodeType.SharedVariable);
        var condition = new NodeMultiCondition(4, "多条件判断", root, NodeType.MultiCondition);
        root.Nodes.AddRange(new NodeBase[] { reader, condition });
        root.Connections.Add(new ProcessConnection { FromNodeId = 3, ToNodeId = 4 });
        reader.ParamForm.Params = new NodeParamSharedVariable { IsRead = true, ReadName = "跨模块V1" };
        reader.ParamForm.SetParam2Form();
        var contract = new SubscriptionInputContract(new[] { SubscriptionDataCategory.Number }, null,
            new[] { SubscriptionValueMultiplicity.Single }, NumericConversionMode.SafeWidening);
        var output = SubscriptionPortCatalog.GetOutputs(reader, contract, false, "").Single();
        if (output.ValueType != typeof(double)) throw new Exception("首次运行前未识别V1数值类型");
        condition.ParamForm.Params = new NodeParamMultiCondition { Conditions = {
            new MultiConditionItem { Name = "V1判断", SourceNodeId = 3, PropertyPath = output.PropertyPath,
                ValueTypeName = typeof(double).FullName, Operator = MultiConditionOperator.Equals, Value1 = "5" } } };
        condition.ParamForm.SetParam2Form();
        var tree = (TreeView)condition.ParamForm.GetType().GetField("treeViewSources",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(condition.ParamForm);
        if (!tree.Nodes.Cast<TreeNode>().Any(n => n.Text.Contains("共享变量2") && n.Nodes.Count > 0))
            throw new Exception("多条件判断订阅树无共享变量");
        SynchronizationContext.SetSynchronizationContext(null);
        math.Run(CancellationToken.None, false).GetAwaiter().GetResult();
        writer.Run(CancellationToken.None, false).GetAwaiter().GetResult();
        reader.Run(CancellationToken.None, false).GetAwaiter().GetResult();
        condition.Run(CancellationToken.None, false).GetAwaiter().GetResult();
        if (!((NodeResultMultiCondition)condition.Result).ConditionResult) throw new Exception("跨模块数值判断失败");
        // 配置改变后不可继续使用之前的double结果，防止未知来源误判。
        ((NodeParamSharedVariable)writer.ParamForm.Params).Text2 = "变量.已删除";
        if (SubscriptionPortCatalog.GetOutputs(reader, contract, false, "").Count != 0)
            throw new Exception("已删除变量仍暴露旧类型");
        Console.WriteLine("通过：嵌套模块V1、重复节点编号、运行前订阅树、实际计算2+3=5及配置失效检查。");
        Solution.Instance.AllProcesses.Remove(root);
        Solution.Instance.SharedVariable.ClearAll();
    }
}

