using Logger;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace TDJS_Vision.Node._6_LogicTool.CompositeModule
{
    /// <summary>
    /// 组合模块节点，将已导入的流程快照作为当前节点的内部流程执行。
    /// </summary>
    public class NodeCompositeModule : NodeBase
    {
        /// <summary>
        /// 最近一次构建内部流程所使用的参数对象。
        /// </summary>
        private NodeParamCompositeModule _cachedParam;

        /// <summary>
        /// 根据快照构建的内部流程，重复运行时复用以减少控件和参数对象创建成本。
        /// </summary>
        private Process _runtimeProcess;

        /// <summary>
        /// 创建组合模块节点。
        /// </summary>
        /// <param name="nodeId">节点 ID。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="process">节点所属流程。</param>
        /// <param name="nodeType">节点类型。</param>
        public NodeCompositeModule(int nodeId, string nodeName, Process process, NodeType nodeType)
            : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new NodeParamFormCompositeModule(process);
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultCompositeModule();
        }

        /// <summary>
        /// 运行组合模块内部流程。
        /// </summary>
        /// <param name="token">流程取消令牌。</param>
        /// <param name="showLog">是否输出运行日志。</param>
        /// <returns>节点运行返回值。</returns>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;

            if (!Active)
            {
                int time = SetRunResult(startTime, NodeStatus.Unexecuted);
                Result.RunTime = time;
                return new NodeReturn(NodeRunFlag.StopRun);
            }

            if (!(ParamForm.Params is NodeParamCompositeModule param))
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行参数未设置或保存！", true);
                Result = BuildResult(null, _runtimeProcess, false, "运行参数未设置或保存！");
                int time = SetRunResult(startTime, NodeStatus.Failed);
                Result.RunTime = time;
                throw new Exception($"节点({ID}.{NodeName})运行参数未设置或保存！");
            }

            try
            {
                ValidateParam(param);
                SetStatus(NodeStatus.Unexecuted, "*");
                await base.CheckTokenCancel(token);

                Process runtimeProcess = GetOrCreateRuntimeProcess(param, showLog);
                await runtimeProcess.Run(false);
                await base.CheckTokenCancel(token);

                if (!runtimeProcess.Success)
                    throw new Exception("组合模块内部流程运行失败！");

                NodeResultCompositeModule result = BuildResult(param, runtimeProcess, true, string.Empty);
                int time = SetRunResult(startTime, NodeStatus.Successful);
                result.RunTime = time;
                Result = result;

                if (showLog)
                {
                    LogHelper.AddLog(
                        MsgLevel.Info,
                        $"节点({ID}.{NodeName})运行成功！内部节点：{result.InternalNodeCount}，内部耗时：{result.InternalRunTime} ms，节点耗时：{time} ms",
                        true);
                }

                return new NodeReturn(NodeRunFlag.ContinueRun);
            }
            catch (OperationCanceledException)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                Result = BuildResult(param, _runtimeProcess, false, "运行取消");
                int time = SetRunResult(startTime, NodeStatus.Unexecuted);
                Result.RunTime = time;
                throw;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因：{ex.Message}", true);
                Result = BuildResult(param, _runtimeProcess, false, ex.Message);
                int time = SetRunResult(startTime, NodeStatus.Failed);
                Result.RunTime = time;
                throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
            }
        }

        /// <summary>
        /// 释放内部流程节点，避免组合模块删除后保留运行快照对象。
        /// </summary>
        /// <param name="disposing">是否释放托管对象。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposeRuntimeProcess();

            base.Dispose(disposing);
        }

        /// <summary>
        /// 校验组合模块参数是否包含可运行的快照。
        /// </summary>
        /// <param name="param">组合模块参数。</param>
        private static void ValidateParam(NodeParamCompositeModule param)
        {
            if (param == null)
                throw new Exception("组合模块参数为空！");

            if (param.NodeInfos == null || param.NodeInfos.Count == 0)
                throw new Exception("组合模块未导入任何内部节点！");
        }

        /// <summary>
        /// 获取内部流程，参数对象变化时重新构建。
        /// </summary>
        /// <param name="param">组合模块参数。</param>
        /// <param name="showLog">是否输出内部流程日志。</param>
        /// <returns>可执行的内部流程。</returns>
        private Process GetOrCreateRuntimeProcess(NodeParamCompositeModule param, bool showLog)
        {
            if (_runtimeProcess == null || !ReferenceEquals(_cachedParam, param))
            {
                DisposeRuntimeProcess();
                _runtimeProcess = BuildRuntimeProcess(param);
                _cachedParam = param;
            }

            _runtimeProcess.ShowLog = showLog;
            _runtimeProcess.Enable = true;
            return _runtimeProcess;
        }

        /// <summary>
        /// 根据快照构建内部流程。
        /// </summary>
        /// <param name="param">组合模块参数。</param>
        /// <returns>内部流程实例。</returns>
        private Process BuildRuntimeProcess(NodeParamCompositeModule param)
        {
            Process runtimeProcess = new Process(BuildRuntimeProcessName(param))
            {
                ID = -Math.Abs(ID == 0 ? 1 : ID),
                Enable = true,
                ShowLog = false,
                HasCanvasGraph = param.HasCanvasGraph,
                Group = Process == null ? ProcessGroup.Group1 : Process.Group,
                RunLv = Process == null ? ProcessLvEnum.Lv5 : Process.RunLv
            };

            Dictionary<int, NodeBase> runtimeNodes = new Dictionary<int, NodeBase>();
            foreach (NodeConfig nodeConfig in param.NodeInfos)
            {
                if (nodeConfig == null)
                    continue;

                NodeBase node = NodeFactory.CreateNode(nodeConfig.ID, nodeConfig.NodeName, runtimeProcess, nodeConfig.NodeType);
                node.Active = nodeConfig.Active;
                node.Selected = false;
                node.CanvasLocation = new Point(nodeConfig.CanvasX, nodeConfig.CanvasY);
                if (nodeConfig.CanvasWidth > 0 && nodeConfig.CanvasHeight > 0)
                    node.CanvasSize = new Size(nodeConfig.CanvasWidth, nodeConfig.CanvasHeight);
                node.IsStartNode = nodeConfig.IsStartNode;

                if (node.ParamForm != null && nodeConfig.NodeParam != null)
                {
                    node.ParamForm.Params = CompositeModuleSnapshotBuilder.CloneNodeParam(nodeConfig.NodeParam);
                    node.ParamForm.SetParam2Form();
                }

                runtimeProcess.AddNode(node);
                runtimeNodes[node.ID] = node;
            }

            RestoreConnections(runtimeProcess, param, runtimeNodes);
            return runtimeProcess;
        }

        /// <summary>
        /// 还原内部流程连线，保持来源流程的图执行语义。
        /// </summary>
        /// <param name="runtimeProcess">内部流程。</param>
        /// <param name="param">组合模块参数。</param>
        /// <param name="runtimeNodes">内部节点索引。</param>
        private static void RestoreConnections(
            Process runtimeProcess,
            NodeParamCompositeModule param,
            Dictionary<int, NodeBase> runtimeNodes)
        {
            runtimeProcess.Connections.Clear();

            if (param.ConnectionInfos != null && param.ConnectionInfos.Count > 0)
            {
                foreach (ProcessConnectionConfig connectionInfo in param.ConnectionInfos)
                {
                    if (connectionInfo == null ||
                        !runtimeNodes.ContainsKey(connectionInfo.FromNodeId) ||
                        !runtimeNodes.ContainsKey(connectionInfo.ToNodeId))
                    {
                        continue;
                    }

                    ProcessConnection connection = new ProcessConnection
                    {
                        FromNodeId = connectionInfo.FromNodeId,
                        ToNodeId = connectionInfo.ToNodeId,
                        FromAnchor = string.IsNullOrEmpty(connectionInfo.FromAnchor) ? "Right" : connectionInfo.FromAnchor,
                        ToAnchor = string.IsNullOrEmpty(connectionInfo.ToAnchor) ? "Left" : connectionInfo.ToAnchor,
                        Branch = connectionInfo.Branch
                    };

                    if (!string.IsNullOrEmpty(connectionInfo.ID))
                        connection.ID = connectionInfo.ID;

                    runtimeProcess.NormalizeConnectionBranch(connection);
                    runtimeProcess.Connections.Add(connection);
                }
            }
            else if (!param.HasCanvasGraph)
            {
                CreateLegacyOrderedConnections(runtimeProcess);
            }

            runtimeProcess.NotifyConnectionsChanged();
        }

        /// <summary>
        /// 为旧顺序流程生成兼容连线。
        /// </summary>
        /// <param name="runtimeProcess">内部流程。</param>
        private static void CreateLegacyOrderedConnections(Process runtimeProcess)
        {
            for (int index = 0; index < runtimeProcess.Nodes.Count - 1; index++)
            {
                runtimeProcess.Connections.Add(new ProcessConnection
                {
                    FromNodeId = runtimeProcess.Nodes[index].ID,
                    ToNodeId = runtimeProcess.Nodes[index + 1].ID,
                    FromAnchor = "Legacy",
                    ToAnchor = "Left"
                });
            }
        }

        /// <summary>
        /// 构建内部流程名称，避免和用户流程名称冲突。
        /// </summary>
        /// <param name="param">组合模块参数。</param>
        /// <returns>内部流程名称。</returns>
        private string BuildRuntimeProcessName(NodeParamCompositeModule param)
        {
            string moduleName = string.IsNullOrWhiteSpace(param.ModuleName)
                ? NodeName
                : param.ModuleName.Trim();

            int ownerProcessId = Process == null ? 0 : Process.ID;
            return $"__组合模块_{ownerProcessId}_{ID}_{moduleName}";
        }

        /// <summary>
        /// 构建组合模块运行结果。
        /// </summary>
        /// <param name="param">组合模块参数。</param>
        /// <param name="runtimeProcess">内部流程。</param>
        /// <param name="success">内部流程是否成功。</param>
        /// <param name="message">运行消息。</param>
        /// <returns>组合模块结果。</returns>
        private static NodeResultCompositeModule BuildResult(
            NodeParamCompositeModule param,
            Process runtimeProcess,
            bool success,
            string message)
        {
            return new NodeResultCompositeModule
            {
                ModuleName = param == null ? string.Empty : param.ModuleName,
                InternalRunTime = runtimeProcess == null ? 0 : runtimeProcess.RunTime,
                InternalNodeCount = runtimeProcess == null ? 0 : runtimeProcess.Nodes.Count,
                InternalSuccess = success,
                Message = message ?? string.Empty
            };
        }

        /// <summary>
        /// 释放已缓存的内部流程节点。
        /// </summary>
        private void DisposeRuntimeProcess()
        {
            if (_runtimeProcess != null)
            {
                foreach (NodeBase node in _runtimeProcess.Nodes)
                    node.Dispose();
            }

            _runtimeProcess = null;
            _cachedParam = null;
        }
    }
}
