using System;using System.Threading;using System.Threading.Tasks;using System.Reflection;using Newtonsoft.Json;using TDJS_Vision;using TDJS_Vision.Node;using TDJS_Vision.Node._6_LogicTool.CompositeModule;using TDJS_Vision.Node._6_LogicTool.ArithmeticOperation;
/// <summary>实际执行画布/顺序流程，验证模块失败继续及默认隔离策略。</summary>
internal static class CompositeFailureContinueTests
{
    /// <summary>执行所有模式与取消回归，不连接设备。</summary>
    [STAThread]static void Main() { try { Execute(); } catch(Exception ex) { Console.Error.WriteLine(ex.GetType().Name+": "+ex.Message); Console.Error.WriteLine(ex.StackTrace); Environment.ExitCode=1; } }
    /// <summary>执行独立场景。</summary>
    static void Execute()
    {
        Environment.SetEnvironmentVariable("TDJS_VISION_PERFORMANCE_ACCEPTANCE","1");
        Environment.SetEnvironmentVariable("TDJS_VISION_BACKGROUND_ACCEPTANCE","1");
        foreach(bool graph in new[]{true,false})foreach(bool enabled in new[]{false,true})RunCase(graph,enabled);
        if(JsonConvert.DeserializeObject<NodeParamCompositeModule>("{}").ContinueDownstreamOnFailure)throw new Exception("旧方案默认值错误");
        Console.WriteLine("通过：画布/顺序模式开关、内部失败、下游执行、失败状态、快照持久化与取消检查。");
    }
    /// <summary>用模块内部非法运算触发真实失败，外部运算验证是否放行。</summary>
    static void RunCase(bool graph,bool enabled)
    {
        var process=new TDJS_Vision.Process("失败继续测试"){Enable=true,HasCanvasGraph=graph};
        using(var module=new NodeCompositeModule(1,"组合模块",process,NodeType.CompositeModule))
        using(var downstream=new NodeArithmeticOperation(2,"下游",process,NodeType.ArithmeticOperation))
        {
            process.Nodes.Add(module);process.Nodes.Add(downstream);
            if(graph)process.Connections.Add(new ProcessConnection{FromNodeId=1,ToNodeId=2});
            var inner=module.GetEditableProcess();
            var invalid=new NodeArithmeticOperation(3,"非法运算",inner,NodeType.ArithmeticOperation);
            inner.Nodes.Add(invalid);invalid.ParamForm.Params=new NodeParamArithmeticOperation();
            ((NodeParamCompositeModule)module.ParamForm.Params).ContinueDownstreamOnFailure=enabled;
            module.SaveEditableProcess();
            var restored=JsonConvert.DeserializeObject<NodeParamCompositeModule>(JsonConvert.SerializeObject(module.ParamForm.Params));
            if(restored.ContinueDownstreamOnFailure!=enabled)throw new Exception("快照保存丢失开关");
            downstream.ParamForm.Params=new NodeParamArithmeticOperation{Rows={new ArithmeticOperationRow{OutputVariableName="V1",Value1=new ArithmeticOperand{ConstantText="2"},Value2=new ArithmeticOperand{ConstantText="3"}}}};
            SynchronizationContext.SetSynchronizationContext(null);
            var method=typeof(TDJS_Vision.Process).GetMethod("RunWithResultAsync",BindingFlags.Instance|BindingFlags.NonPublic);
            try { ((Task<bool>)method.Invoke(process,new object[]{false,CancellationToken.None})).GetAwaiter().GetResult(); }
            catch(Exception) { if(enabled) throw; }
            if(module.RuntimeStatus!=NodeStatus.Failed)throw new Exception("模块失败状态丢失");
            if((downstream.RuntimeStatus==NodeStatus.Successful)!=enabled)throw new Exception("下游放行不正确 graph="+graph+" enabled="+enabled);
            if(process.Success)throw new Exception("错误地把失败流程改为成功");
            using(var cancel=new CancellationTokenSource())
            {
                cancel.Cancel();bool cancelled=false;
                try{module.Run(cancel.Token,false).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}
                if(!cancelled)throw new Exception("取消被吞掉");
            }
        }
    }
}
