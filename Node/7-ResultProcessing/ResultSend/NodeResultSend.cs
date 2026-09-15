using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Logger;
using TDJS_Vision.ResourceManagement;

namespace TDJS_Vision.Node._7_ResultProcessing.ResultSend
{
    /// <summary>整批预检多个上游基础结果，到达节点后直接逐行发送，不参与工件有序排队。</summary>
    public sealed class NodeResultSend : NodeBase, INodeSubscriptionDependencyProvider
    {
        /// <summary>创建结果发送节点与设计器参数窗体。</summary>
        public NodeResultSend(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            Result = new NodeResultResultSend();
            ParamForm = new ParamFormResultSend();
            ParamForm.SetNodeBelong(this);
        }

        /// <summary>为图调度器声明全部启用订阅依赖。</summary>
        public IEnumerable<int> GetSubscriptionDependencyNodeIds() =>
            (ParamForm.Params as NodeParamResultSend)?.Rows?.Where(row => row != null && row.Enabled && row.Source != null && row.Source.NodeId > 0 && row.Source.NodeId != ID)
                .Select(row => row.Source.NodeId).Distinct().ToArray() ?? new int[0];

        /// <summary>按设备名称解析唯一现有设备，不自行创建额外连接。</summary>
        public static IResultSendWriter GetWriter(NodeParamResultSend param)
        {
            if (param == null || string.IsNullOrWhiteSpace(param.DeviceName)) throw new InvalidOperationException("请选择通信设备并保存参数。");
            var devices = Solution.Instance.AllDevices.Where(device => device.UserDefinedName == param.DeviceName).ToArray();
            if (devices.Length != 1) throw new InvalidOperationException("通信设备不存在或名称不唯一：" + param.DeviceName);
            return ResultSendWriterFactory.Create(devices[0]);
        }

        /// <summary>读取并转换所有启用行；任何一行失败时尚未进行设备写入。</summary>
        public List<ResultSendPreparedRow> PrepareRows(NodeParamResultSend param, IResultSendWriter writer, bool requireCurrentRun)
        {
            if (param?.Rows == null || !param.Rows.Any(row => row != null && row.Enabled)) throw new InvalidOperationException("至少启用一个发送项。");
            var prepared = new List<ResultSendPreparedRow>();
            // 每批仅遍历一次上游图，发送项多时避免重复扫描连线。
            var upstream = Process.GetUpstreamNodes(this).ToDictionary(node => node.ID);
            for (int index = 0; index < param.Rows.Count; index++)
            {
                var row = param.Rows[index];
                if (row == null || !row.Enabled) continue;
                try
                {
                    ResultSendValueConverter.ValidateConfiguration(row);
                    var values = ResultSendSourceReader.Read(this, row.Source, requireCurrentRun, upstream);
                    var item = ResultSendValueConverter.Prepare(row, values, index + 1);
                    writer.Validate(row.Address, row.DataType, item.Values);
                    if (row.Mode == ResultSendMode.All) writer.Validate(row.CountAddress, ResultSendType.UInt16, new ushort[] { item.Count });
                    prepared.Add(item);
                }
                catch (Exception exception)
                {
                    throw new ResultSendRowException(index + 1, exception);
                }
            }
            ResultSendAddressValidator.Validate(prepared, writer);
            return prepared;
        }

        /// <summary>冻结并预检本轮结果后直接发送，不等待工件顺序或获取有序端点租约。</summary>
        public Task<OrderedSignalSendResult> ExecuteDirectSendAsync(CancellationToken token)
        {
            var result = new NodeResultResultSend();
            Result = result;
            try
            {
                token.ThrowIfCancellationRequested();
                var param = (ParamForm.Params as NodeParamResultSend)?.Copy();
                var writer = GetWriter(param);
                // 保留适配器端点配置校验，但不使用端点键申请发送权。
                _ = writer.EndpointKey;
                var rows = PrepareRows(param, writer, true);
                token.ThrowIfCancellationRequested();
                return SendPreparedAsync(writer, rows, result);
            }
            catch (Exception exception)
            {
                result.Error = exception.Message;
                result.FailedRow = (exception as ResultSendRowException)?.RowNumber ?? 0;
                throw;
            }
        }

        /// <summary>在本次调用内逐行发送；每行数据成功后才写有效数量，失败不自动重试。</summary>
        public static async Task<OrderedSignalSendResult> SendPreparedAsync(IResultSendWriter writer, List<ResultSendPreparedRow> rows, NodeResultResultSend result)
        {
            bool anyWriteCompleted = false;
            foreach (var row in rows)
            {
                try
                {
                    await writer.WriteAsync(row.Configuration.Address, row.Configuration.DataType, row.Values).ConfigureAwait(false);
                    anyWriteCompleted = true;
                    if (row.Configuration.Mode == ResultSendMode.All)
                        await writer.WriteAsync(row.Configuration.CountAddress, ResultSendType.UInt16, new ushort[] { row.Count }).ConfigureAwait(false);
                    result.SentCount++;
                }
                catch (DeviceSendNotStartedException exception) when (!anyWriteCompleted)
                {
                    result.FailedRow = row.RowNumber;
                    result.Error = "第" + row.RowNumber + "行发送前检查失败：" + exception.Message + "本次结果未写入，后续行未发送。";
                    throw new DeviceSendNotStartedException(result.Error);
                }
                catch (Exception exception)
                {
                    result.FailedRow = row.RowNumber;
                    result.Error = "第" + row.RowNumber + "行发送失败：" + exception.Message + "；本行可能已部分写入，后续行未发送。";
                    throw new InvalidOperationException(result.Error, exception);
                }
            }
            result.Success = true;
            return new OrderedSignalSendResult(OrderedSignalSendStatus.DeviceAcknowledged, "全部结果写入成功。", "完成行数=" + result.SentCount);
        }

        /// <summary>节点执行入口，等待实际设备写入完成后才允许后续节点继续。</summary>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            var start = DateTime.Now;
            Result = new NodeResultResultSend();
            if (!Active)
            {
                Result.RunTime = SetRunResult(start, NodeStatus.Unexecuted);
                return new NodeReturn(NodeRunFlag.StopRun);
            }
            try
            {
                token.ThrowIfCancellationRequested();
                SetStatus(NodeStatus.Unexecuted, "*");
                var response = await ExecuteDirectSendAsync(token);
                if (response.Status == OrderedSignalSendStatus.CancelledBeforeStart) throw new OperationCanceledException(response.Message, token);
                if (response.Status != OrderedSignalSendStatus.DeviceAcknowledged && response.Status != OrderedSignalSendStatus.LocalCallCompleted)
                    throw new InvalidOperationException(response.Message);
                Result.RunTime = SetRunResult(start, NodeStatus.Successful);
                if (showLog) LogHelper.AddLog(MsgLevel.Info, "节点(" + ID + "." + NodeName + ")发送成功，完成" + ((NodeResultResultSend)Result).SentCount + "行。", true);
                return new NodeReturn(NodeRunFlag.ContinueRun);
            }
            catch (Exception exception)
            {
                var result = (NodeResultResultSend)Result;
                result.Success = false;
                result.Error = exception.Message;
                result.RunTime = SetRunResult(start, exception is OperationCanceledException ? NodeStatus.Unexecuted : NodeStatus.Failed);
                LogHelper.AddLog(MsgLevel.Warn, "节点(" + ID + "." + NodeName + ")：" + exception.Message, true);
                throw;
            }
        }
    }

    /// <summary>标记预检失败的具体配置行。</summary>
    public sealed class ResultSendRowException : InvalidOperationException
    {
        /// <summary>从1开始的行序号。</summary>
        public int RowNumber { get; }
        /// <summary>保留原始异常并添加行号。</summary>
        public ResultSendRowException(int rowNumber, Exception inner) : base("第" + rowNumber + "行：" + inner.Message, inner) { RowNumber = rowNumber; }
    }
}
