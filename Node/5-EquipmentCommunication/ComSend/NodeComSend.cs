using Logger;
using System;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Device.COM;
using TDJS_Vision.ResourceManagement;

namespace TDJS_Vision.Node._5_EquipmentCommunication.ComSend
{
    /// <summary>通过生产会话协调器按工件顺序发送串口命令。</summary>
    public class NodeComSend : NodeBase, IOrderedExternalSignalNode
    {
        /// <summary>创建串口发送节点并初始化参数与结果对象。</summary>
        public NodeComSend(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType) 
        {
            ParamForm = new ParamFormComSend();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultComSend();
        }

        /// <summary>获取有序外发白名单中的串口发送类别。</summary>
        public OrderedSignalNodeKind OrderedSignalKind => OrderedSignalNodeKind.ComSend;

        /// <summary>获取当前配置绑定的物理串口端点键。</summary>
        public string OrderedSignalEndpointKey
        {
            get
            {
                NodeParamComSend param = ParamForm?.Params as NodeParamComSend;
                return CreateEndpointKey(param?.Dev);
            }
        }

        /// <summary>
        /// 节点运行
        /// </summary>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;

            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return new NodeReturn(NodeRunFlag.StopRun);
            }
            if (ParamForm.Params == null)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({NodeName})运行参数未设置或保存！");
            }

            var param = (NodeParamComSend)ParamForm.Params;
            try
            {
                SetStatus(NodeStatus.Unexecuted, "*");
                base.CheckTokenCancel(token);

                //如果没有连接则不运行
                if (!param.Dev.IsOpen)
                    throw new Exception("串口尚未打开！");

                WorkpieceExecutionContext context = Solution.Instance.WorkpieceContextAccessor.Current;
                if (context == null)
                    throw new InvalidOperationException("串口发送节点缺少当前工件上下文，已拒绝无序发送。");
                OrderedSignalSendResult sendResult = await SubmitOrderedSignalAsync(
                    context,
                    $"{Process?.ProcessName ?? "未知流程"}/{ID}.{NodeName}",
                    Solution.Instance.OrderedSignalCoordinator,
                    token);
                if (sendResult.Status == OrderedSignalSendStatus.CancelledBeforeStart)
                    throw new OperationCanceledException(sendResult.Message, token);
                if (sendResult.Status != OrderedSignalSendStatus.DeviceAcknowledged &&
                    sendResult.Status != OrderedSignalSendStatus.LocalCallCompleted)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(sendResult.Message)
                            ? $"串口有序发送返回{sendResult.Status}。"
                            : sendResult.Message);
                }

                var time = SetRunResult(startTime, NodeStatus.Successful);
                Result.RunTime = time;
                if(showLog)
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);
                return new NodeReturn(NodeRunFlag.ContinueRun);
            }
            catch (OperationCanceledException)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                SetRunResult(startTime, NodeStatus.Unexecuted);
                throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行失败！原因:{ex.Message}");
            }
        }

        /// <summary>复制本轮动态值并通过当前生产会话协调器提交唯一串口动作。</summary>
        public Task<OrderedSignalSendResult> SubmitOrderedSignalAsync(
            WorkpieceExecutionContext context,
            string nodePath,
            IOrderedSignalCoordinator coordinator,
            CancellationToken waitingCancellationToken)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (coordinator == null)
                throw new ArgumentNullException(nameof(coordinator));
            NodeParamComSend param = ParamForm?.Params as NodeParamComSend;
            if (param == null)
                throw new InvalidOperationException("串口发送参数尚未设置。");

            ComDevice deviceSnapshot = param.Dev;
            if (deviceSnapshot == null)
                throw new InvalidOperationException("串口发送设备尚未绑定。");
            if (!deviceSnapshot.IsOpen)
                throw new InvalidOperationException("串口尚未打开。");
            string commandSnapshot = param.Cmd;
            string encodingSnapshot = param.Encoding;
            if (string.IsNullOrEmpty(commandSnapshot))
                throw new InvalidOperationException("串口发送命令不能为空。");
            string endpointKeySnapshot = CreateEndpointKey(deviceSnapshot);
            return context.SubmitOrderedSignalAsync(
                coordinator,
                nodePath,
                OrderedSignalKind,
                endpointKeySnapshot,
                () => SendSnapshotAsync(deviceSnapshot, commandSnapshot, encodingSnapshot),
                waitingCancellationToken);
        }

        /// <summary>使用已经冻结的串口对象和参数执行一次真实写入。</summary>
        private static Task<OrderedSignalSendResult> SendSnapshotAsync(
            ComDevice device,
            string command,
            string encoding)
        {
            if (!device.IsOpen)
            {
                return Task.FromResult(new OrderedSignalSendResult(
                    OrderedSignalSendStatus.Rejected,
                    "串口在取得发送权后已经关闭。"));
            }

            device.Send(command, encoding);
            return Task.FromResult(new OrderedSignalSendResult(
                OrderedSignalSendStatus.LocalCallCompleted,
                "串口写入API已正常返回。"));
        }

        /// <summary>按真实物理端口名生成跨节点共享的稳定端点键。</summary>
        private static string CreateEndpointKey(ComDevice device)
        {
            if (device == null)
                throw new InvalidOperationException("串口发送设备尚未绑定。");
            string portName = device.ComParams?.PortName;
            if (string.IsNullOrWhiteSpace(portName))
                portName = device.DevName;
            if (string.IsNullOrWhiteSpace(portName))
                throw new InvalidOperationException("串口物理端口名不能为空。");
            return WorkpieceExecutionContext.NormalizeKey(
                $"COM:{portName}",
                nameof(device));
        }
    }
}
