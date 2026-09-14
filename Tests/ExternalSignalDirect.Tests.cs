using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HslCommunication;
using TDJS_Vision;
using TDJS_Vision.Device;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.Device.COM;
using TDJS_Vision.Device.PLC;
using TDJS_Vision.Node;
using TDJS_Vision.Node._5_EquipmentCommunication.CameraIO;
using TDJS_Vision.Node._5_EquipmentCommunication.ComSend;
using TDJS_Vision.Node._5_EquipmentCommunication.PlcWirte;
using TDJS_Vision.Node._5_EquipmentCommunication.AIResultSend;
using TDJS_Vision.Node._5_EquipmentCommunication.TcpClient;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.ResourceManagement;

/// <summary>在真实节点入口验证直接外发，不连接现场设备。</summary>
internal static class ExternalSignalDirectTests
{
    /// <summary>断言行为符合预期。</summary>
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    /// <summary>断言调用明确失败。</summary>
    private static void Reject(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch { rejected = true; }
        Check(rejected, message);
    }
    /// <summary>用消息循环等待WinForms节点完成，不依赖UI同步上下文阻塞等待。</summary>
    private static void Pump(Task task)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(1); }
        Check(task.IsCompleted, "节点等待超时，可能仍在排队。");
        task.GetAwaiter().GetResult();
    }
    /// <summary>释放节点与设计器参数窗体。</summary>
    private static void DisposeNode(NodeBase node) { (node.ParamForm as Form)?.Dispose(); node.Dispose(); }

    /// <summary>为大设备接口提供仅实现当前测试所需成员的透明代理。</summary>
    private sealed class DeviceProxy<T> : RealProxy where T : class
    {
        /// <summary>设备方法的可替换测试行为。</summary>
        private readonly Func<MethodInfo, object[], object> _handler;
        /// <summary>创建接口代理。</summary>
        public DeviceProxy(Func<MethodInfo, object[], object> handler) : base(typeof(T)) { _handler = handler; }
        /// <summary>获取节点使用的接口对象。</summary>
        public T Device => (T)GetTransparentProxy();
        /// <summary>保留设备抛出的异常，避免代理隐藏真实调用失败。</summary>
        public override IMessage Invoke(IMessage message)
        {
            var call = (IMethodCallMessage)message;
            try { return new ReturnMessage(_handler((MethodInfo)call.MethodBase, call.Args), null, 0, call.LogicalCallContext, call); }
            catch (Exception exception) { return new ReturnMessage(exception, call); }
        }
    }

    /// <summary>验证PLC值快照、取消、响应失败、并发直接写入及无工件上下文。</summary>
    private static void VerifyPlc()
    {
        var calls = new List<Tuple<string, string, object>>();
        var gate = new TaskCompletionSource<OperateResult>();
        bool connected = true;
        OperateResult response = OperateResult.CreateSuccessResult();
        var proxy = new DeviceProxy<IPlc>((method, args) =>
        {
            if (method.Name == "get_IsConnect") return connected;
            if (method.Name == "get_PLCParms") return new PLCParms { PlcConType = PlcConType.ETHERNET, EthernetParms = new EthernetParms("127.0.0.1", 12345) };
            if (method.Name == "get_Brand") return DeviceBrand.Keyence;
            if (method.Name.StartsWith("Write"))
            {
                calls.Add(Tuple.Create(method.Name, (string)args[0], args[1] is Array array ? array.Clone() : args[1]));
                return calls.Count == 1 ? gate.Task : Task.FromResult(response);
            }
            return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
        });
        var first = new NodePlcWrite(1, "PLC直接写入1", null, NodeType.PLCWrite) { Active = true };
        var second = new NodePlcWrite(2, "PLC直接写入2", null, NodeType.PLCWrite) { Active = true };
        try
        {
            first.ParamForm.Params = new NodeParamPlcWrite { Plc = proxy.Device, Address = "DM100", DataType = "Int32", Value = "1,-2" };
            second.ParamForm.Params = new NodeParamPlcWrite { Plc = proxy.Device, Address = "DM101", DataType = "Int32", Value = "3" };
            Task firstRun = first.Run(CancellationToken.None, false);
            Check(calls.Count == 1 && !firstRun.IsCompleted, "PLC没有立即调用设备或提前报告成功。");
            ((NodeParamPlcWrite)first.ParamForm.Params).Address = "DM999";
            ((NodeParamPlcWrite)first.ParamForm.Params).Value = "999";
            Pump(second.Run(CancellationToken.None, false));
            Check(calls.Count == 2 && !firstRun.IsCompleted, "第二个PLC节点仍等待第一个节点的发送权。");
            Check(calls[0].Item2 == "DM100" && ((int[])calls[0].Item3).SequenceEqual(new[] { 1, -2 }), "PLC写入快照被后续参数修改污染。");
            gate.SetResult(OperateResult.CreateSuccessResult()); Pump(firstRun);
            var parameter = (NodeParamPlcWrite)second.ParamForm.Params;
            foreach (var item in new[] { Tuple.Create("Boolean", "True,0"), Tuple.Create("Single", "-1.5"), Tuple.Create("Single", "1.5,-2.5"), Tuple.Create("String", "ABC") })
            { parameter.DataType = item.Item1; parameter.Value = item.Item2; Pump(second.Run(CancellationToken.None, false)); }
            Check(calls[2].Item1 == "WriteBoolAsync" && ((bool[])calls[2].Item3).SequenceEqual(new[] { true, false }) &&
                calls[3].Item3 is float && calls[4].Item3 is float[] && (string)calls[5].Item3 == "ABC", "PLC类型或数组值错误。");
            int before = calls.Count;
            using (var cancelled = new CancellationTokenSource())
            { cancelled.Cancel(); Reject(() => Pump(second.Run(cancelled.Token, false)), "已取消PLC节点仍成功。"); }
            Check(calls.Count == before, "PLC取消后仍写入。");
            connected = false;
            Reject(() => Pump(second.Run(CancellationToken.None, false)), "断开PLC仍成功。");
            Check(calls.Count == before, "断开PLC仍调用写入。"); connected = true;
            response = new OperateResult("模拟失败");
            Reject(() => Pump(second.Run(CancellationToken.None, false)), "PLC失败响应被吞掉。");
            Check(calls.Count == before + 1, "PLC失败自动重试。");
            response = null;
            Reject(() => Pump(second.Run(CancellationToken.None, false)), "PLC空响应被当成成功。");
            response = OperateResult.CreateSuccessResult();
            VerifyPendingPredecessor(() => Pump(second.Run(CancellationToken.None, false)));
            parameter.DataType = "Single"; parameter.Value = "NaN";
            before = calls.Count;
            Reject(() => Pump(second.Run(CancellationToken.None, false)), "非法PLC值没有预检。");
            Check(calls.Count == before, "非法值已经产生写入。");
        }
        finally { gate.TrySetResult(OperateResult.CreateSuccessResult()); DisposeNode(first); DisposeNode(second); }
    }

    /// <summary>同组前序未完成及随后故障均不能阻止到达即发。</summary>
    private static void VerifyPendingPredecessor(Action send)
    {
        var epoch = Guid.NewGuid();
        string domain = "DIRECT-ALL-" + epoch.ToString("N");
        var first = new WorkpieceExecutionContext(new WorkpieceIdentity(epoch, domain, 1), 1, domain, "测试", DateTime.UtcNow, CancellationToken.None);
        var second = new WorkpieceExecutionContext(new WorkpieceIdentity(epoch, domain, 2), 1, domain, "测试", DateTime.UtcNow, CancellationToken.None);
        var coordinator = Solution.Instance.OrderedSignalCoordinator;
        coordinator.RegisterWorkpiece(first); coordinator.RegisterWorkpiece(second);
        using (Solution.Instance.WorkpieceContextAccessor.Push(second))
        {
            send();
            Pump(coordinator.CompleteWorkpieceAsync(first, WorkpieceTerminalState.Faulted));
            send();
        }
        second.TryComplete(WorkpieceTerminalState.Succeeded);
    }

    /// <summary>验证相机脉冲立即拉高、同步等待、异步继续、复位及不占有端点。</summary>
    private static void VerifyCamera()
    {
        var calls = new ConcurrentQueue<string>();
        bool throwAfterHigh = false;
        bool failNextReset = false;
        bool highStarted = false;
        var proxy = new DeviceProxy<ICamera>((method, args) =>
        {
            if (method.Name == "get_IsOpen") return true;
            if (method.Name == "get_SN") return "DIRECT-CAMERA";
            if (method.Name == "SetLineSelector" || method.Name == "SetLineMode" || method.Name == "SetLineInverter")
            {
                calls.Enqueue(method.Name + ":" + args[0]);
                if (method.Name == "SetLineInverter" && (bool)args[0] && throwAfterHigh) throw new InvalidOperationException("模拟拉高后异常");
                if (method.Name == "SetLineInverter")
                {
                    if (!(bool)args[0] && highStarted && failNextReset)
                    { failNextReset = false; throw new InvalidOperationException("模拟异步复位异常"); }
                    highStarted = (bool)args[0];
                }
                return null;
            }
            return method.ReturnType == typeof(void) ? null : method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
        });
        var first = new NodeCameraIO(3, "相机IO直接1", null, NodeType.CameraIO) { Active = true };
        var second = new NodeCameraIO(4, "相机IO直接2", null, NodeType.CameraIO) { Active = true };
        try
        {
            var parameter = new NodeParamCameraIO { Camera = proxy.Device, LineSelector = "Line1", LineMode = "输出", HoldTime = 400000, IsAsay = false };
            first.ParamForm.Params = parameter;
            second.ParamForm.Params = new NodeParamCameraIO { Camera = proxy.Device, LineSelector = "Line2", LineMode = "输出", HoldTime = 0 };
            var task = first.Run(CancellationToken.None, false);
            Check(calls.Contains("SetLineInverter:True") && !task.IsCompleted, "同步相机IO未立即拉高或没有等待保持结束。");
            parameter.LineSelector = "LineChanged";
            Pump(second.Run(CancellationToken.None, false));
            Check(!task.IsCompleted && calls.Contains("SetLineSelector:Line2"), "相机IO仍按物理端点排队。");
            Pump(task);
            Check(calls.ToArray().Reverse().Take(2).SequenceEqual(new[] { "SetLineInverter:False", "SetLineSelector:Line1" }), "复位没有使用本轮线路快照。");
            parameter.LineSelector = "Line1"; parameter.IsAsay = true;
            var context = new WorkpieceExecutionContext(new WorkpieceIdentity(Guid.NewGuid(), "DIRECT-ASYNC", 1), 1, "DIRECT-ASYNC", "测试", DateTime.UtcNow, CancellationToken.None);
            using (Solution.Instance.WorkpieceContextAccessor.Push(context))
            {
                Pump(first.Run(CancellationToken.None, false));
                context.TryComplete(WorkpieceTerminalState.Succeeded);
                Check(!context.Completion.IsCompleted, "异步脉冲还没结束就提前完成工件生命周期。");
                Pump(context.Completion);
                Check(context.Completion.Result == WorkpieceTerminalState.Succeeded, "异步脉冲成功未归还生命周期。");
            }
            using (var cancel = new CancellationTokenSource())
            {
                int before = calls.Count; cancel.Cancel();
                Reject(() => Pump(first.Run(cancel.Token, false)), "取消相机IO仍然成功。");
                Check(calls.Count == before, "取消后相机IO仍然拉高。");
            }
            parameter.IsAsay = false; parameter.HoldTime = 20000;
            using (var cancel = new CancellationTokenSource())
            {
                var pulse = first.Run(cancel.Token, false); cancel.Cancel(); Pump(pulse);
                Check(calls.Last() == "SetLineInverter:False", "脉冲开始后取消导致未复位。");
            }
            throwAfterHigh = true;
            Reject(() => Pump(first.Run(CancellationToken.None, false)), "相机SDK异常被吞掉。");
            Check(calls.Last() == "SetLineInverter:False", "相机拉高失败没有尝试复位。");
            throwAfterHigh = false; parameter.HoldTime = 0;
            VerifyPendingPredecessor(() => Pump(first.Run(CancellationToken.None, false)));
            parameter.IsAsay = true; parameter.HoldTime = 50000; failNextReset = true;
            var failedContext = new WorkpieceExecutionContext(new WorkpieceIdentity(Guid.NewGuid(), "DIRECT-ASYNC-FAIL", 1), 1, "DIRECT-ASYNC-FAIL", "测试", DateTime.UtcNow, CancellationToken.None);
            using (Solution.Instance.WorkpieceContextAccessor.Push(failedContext))
            {
                Pump(first.Run(CancellationToken.None, false));
                failedContext.TryComplete(WorkpieceTerminalState.Succeeded);
                Pump(failedContext.Completion);
                Check(failedContext.Completion.Result == WorkpieceTerminalState.Faulted && !highStarted,
                    "异步复位失败未传播工件失败或未尝试恢复低电平。");
            }
            parameter.HoldTime = -1;
            int count = calls.Count;
            Reject(() => Pump(first.Run(CancellationToken.None, false)), "无效保持时间未拒绝。");
            Check(calls.Count == count, "无效配置已经输出脉冲。");
        }
        finally { DisposeNode(first); DisposeNode(second); }
    }

    /// <summary>串口没有测试物理端口，验证无上下文入口、取消、预检和实际驱动异常传播。</summary>
    private static void VerifyCom()
    {
        var node = new NodeComSend(5, "串口直接", null, NodeType.ComSend) { Active = true };
        try
        {
            var device = new ComDevice { DevName = "COM-TEST", ComParams = new SerialPortConfig { PortName = "COM-TEST" }, IsOpen = true };
            node.ParamForm.Params = new NodeParamComSend { Dev = device, Cmd = "TEST", Encoding = "ASCII" };
            try { Pump(node.Run(CancellationToken.None, false)); throw new InvalidOperationException("未打开物理串口却成功。"); }
            catch (Exception exception) { Check(exception.Message.Contains("串口未打开"), "未到达串口驱动写入，或设备异常被隐藏。"); }
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel(); var task = node.Run(cancel.Token, false);
                Reject(() => Pump(task), "串口取消仍成功。"); Check(task.IsCanceled, "串口未响应取消。");
            }
            ((NodeParamComSend)node.ParamForm.Params).Cmd = "";
            Reject(() => node.ExecuteDirectSendAsync(CancellationToken.None), "串口空命令未预检。");
        }
        finally { DisposeNode(node); }
    }

    /// <summary>本机TCP服务延迟响应，验证真实节点不会在配置的响应完成前返回成功。</summary>
    private static void VerifyTcp()
    {
        var server = new TcpListener(IPAddress.Loopback, 0);
        server.Start();
        int port = ((IPEndPoint)server.LocalEndpoint).Port;
        var device = new TDJS_Vision.Device.TCP.TCPClient(new TDJS_Vision.Device.TCP.TcpParam(DevType.TcpClient, "127.0.0.1", port, "本机TCP", "本机TCP"));
        var node = new NodeTCPClient(6, "TCP直接发送", null, NodeType.TCPClientRequest) { Active = true };
        TcpClient peer = null;
        try
        {
            var accepted = server.AcceptTcpClientAsync(); device.Connect(); Pump(accepted); peer = accepted.Result;
            node.ParamForm.Params = new NodeParamTCPClient { Device = device, NoConditionContent = "PING", IsWaitingForResponse = true };
            var run = node.Run(CancellationToken.None, false);
            var buffer = new byte[4]; int received = 0;
            while (received < buffer.Length)
            {
                var read = peer.GetStream().ReadAsync(buffer, received, buffer.Length - received); Pump(read);
                Check(read.Result > 0, "本机TCP未收到请求。"); received += read.Result;
            }
            Check(System.Text.Encoding.UTF8.GetString(buffer) == "PING" && !run.IsCompleted, "TCP未发送正确数据或响应前已报告成功。");
            byte[] response = System.Text.Encoding.UTF8.GetBytes("PONG"); peer.GetStream().Write(response, 0, response.Length);
            Pump(run);
            Check(string.Equals(((NodeResultTCPClient)node.Result).ResponseData as string, "PONG", StringComparison.Ordinal), "TCP节点未等待实际响应结果。");
            device.Disconnect();
            Reject(() => Pump(node.Run(CancellationToken.None, false)), "TCP断开仍报告成功。");
        }
        finally { device.Disconnect(); peer?.Close(); server.Stop(); DisposeNode(node); }
    }

    /// <summary>旧AI信号发送的设备适配路径必须等待PLC响应并检查发送和复位失败。</summary>
    private static void VerifyLegacyAiSend()
    {
        var gate = new TaskCompletionSource<OperateResult>();
        int calls = 0;
        OperateResult response = OperateResult.CreateSuccessResult();
        var proxy = new DeviceProxy<IPlc>((method, args) =>
        {
            if (method.Name == "get_UserDefinedName") return "AI假PLC";
            if (method.Name == "get_IsConnect") return true;
            if (method.Name.StartsWith("Write")) { calls++; return calls == 1 ? gate.Task : Task.FromResult(response); }
            return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
        });
        IPlc device = proxy.Device;
        Solution.Instance.AllDevices.Add(device);
        var node = new NodeSignalSend(7, "旧AI直接发送", null, NodeType.UNKNOWN);
        try
        {
            var algorithm = new AlgorithmResult();
            algorithm.DetectResults["长度"] = new List<SingleDetectResult> { new SingleDetectResult("长度", "1", true) };
            var map = new Dictionary<string, DetectItemAddress> { { "长度", new DetectItemAddress { DeviceName = "AI假PLC", SignalAddress = "DM100", OkValue = 1, NgValue = 2 } } };
            MethodInfo method = typeof(NodeSignalSend).GetMethod("SendToModbus", BindingFlags.Instance | BindingFlags.NonPublic);
            Func<bool, Task> send = reset => (Task)method.Invoke(node, new object[] { algorithm, map, 0d, reset, CancellationToken.None, true, false });
            var pending = send(false);
            Check(calls == 1 && !pending.IsCompleted, "旧AI发送没有立即调用PLC或提前完成。");
            gate.SetResult(OperateResult.CreateSuccessResult()); Pump(pending);
            Pump(send(true)); Check(calls == 3, "旧AI自动复位未执行。");
            response = new OperateResult("AI写入失败");
            Reject(() => Pump(send(false)), "旧AI忽略PLC失败响应。");
            Check(calls == 4, "旧AI失败发生自动重试。");
        }
        finally { gate.TrySetResult(OperateResult.CreateSuccessResult()); Solution.Instance.AllDevices.Remove(device); DisposeNode(node); }
    }

    /// <summary>确保所有节点退出有序接口，嵌套和并行外发由编译器专项补充。</summary>
    private static void VerifyCatalog()
    {
        foreach (NodeType type in Enum.GetValues(typeof(NodeType)))
        { OrderedSignalNodeKind kind; Check(!OrderedSignalNodeCatalog.TryGetSignalKind(type, out kind), "仍有节点登记到有序目录：" + type); }
        foreach (Type type in new[] { typeof(NodePlcWrite), typeof(NodeCameraIO), typeof(NodeComSend) })
            Check(!typeof(IOrderedExternalSignalNode).IsAssignableFrom(type), "节点仍实现有序接口：" + type.Name);
    }
    /// <summary>运行验证并用退出码反馈结果。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            LanguageManager.SetLanguage("zh-CN", false);
            VerifyCatalog(); VerifyPlc(); VerifyCamera(); VerifyCom(); VerifyTcp(); VerifyLegacyAiSend();
            Console.WriteLine("全部外发直接执行验证通过：PLC类型/快照/实际响应、相机同步及异步/复位/取消、无端点排队、前序未结束及故障不阻塞、串口驱动失败传播、本机TCP等待响应、旧AI发送及复位。");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
