using Logger;
using System;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Device.COM;
using TDJS_Vision.ResourceManagement;

namespace TDJS_Vision.Node._5_EquipmentCommunication.ComSend
{
    /// <summary>到达节点后直接发送串口命令，不参与工件顺序排队。</summary>
    public class NodeComSend : NodeBase
    {
        /// <summary>创建串口发送节点并初始化参数与结果对象。</summary>
        public NodeComSend(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType) 
        {
            ParamForm = new ParamFormComSend();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultComSend();
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

            try
            {
                SetStatus(NodeStatus.Unexecuted, "*");
                await base.CheckTokenCancel(token);
                OrderedSignalSendResult sendResult = await ExecuteDirectSendAsync(token);
                if (sendResult.Status == OrderedSignalSendStatus.CancelledBeforeStart)
                    throw new OperationCanceledException(sendResult.Message, token);
                if (sendResult.Status != OrderedSignalSendStatus.DeviceAcknowledged &&
                    sendResult.Status != OrderedSignalSendStatus.LocalCallCompleted)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(sendResult.Message)
                            ? $"串口发送返回{sendResult.Status}。"
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

        /// <summary>冻结并预检本轮命令后直接发送，不等待工件或端点发送权。</summary>
        public Task<OrderedSignalSendResult> ExecuteDirectSendAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
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
            _ = CreateEndpointKey(deviceSnapshot);
            token.ThrowIfCancellationRequested();
            return SendSnapshotAsync(deviceSnapshot, commandSnapshot, encodingSnapshot);
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
                    "串口在实际发送前已经关闭。"));
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
