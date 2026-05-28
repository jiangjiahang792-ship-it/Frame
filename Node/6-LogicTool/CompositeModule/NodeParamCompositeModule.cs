using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace TDJS_Vision.Node._6_LogicTool.CompositeModule
{
    /// <summary>
    /// 组合模块节点参数，保存来源流程在导入时刻的节点和连线快照。
    /// </summary>
    public class NodeParamCompositeModule : INodeParam
    {
        /// <summary>
        /// 模块显示名称，默认使用来源流程名称。
        /// </summary>
        public string ModuleName { get; set; }

        /// <summary>
        /// 来源流程 ID，用于界面回显和重新导入时定位。
        /// </summary>
        public int SourceProcessId { get; set; }

        /// <summary>
        /// 来源流程名称，用于来源流程不存在时仍能阅读快照来源。
        /// </summary>
        public string SourceProcessName { get; set; }

        /// <summary>
        /// 快照流程是否使用画布图结构执行。
        /// </summary>
        public bool HasCanvasGraph { get; set; } = true;

        /// <summary>
        /// 快照内的节点配置列表。
        /// </summary>
        public List<NodeConfig> NodeInfos { get; set; } = new List<NodeConfig>();

        /// <summary>
        /// 快照内的流程连线配置列表。
        /// </summary>
        public List<ProcessConnectionConfig> ConnectionInfos { get; set; } = new List<ProcessConnectionConfig>();
    }

    /// <summary>
    /// 组合模块快照构建器，负责从流程提取可序列化的节点和连线配置。
    /// </summary>
    internal static class CompositeModuleSnapshotBuilder
    {
        /// <summary>
        /// 根据指定流程创建组合模块参数快照。
        /// </summary>
        /// <param name="sourceProcess">需要封装为组合模块的来源流程。</param>
        /// <returns>包含节点、参数和连线的组合模块参数。</returns>
        public static NodeParamCompositeModule CreateFromProcess(Process sourceProcess)
        {
            if (sourceProcess == null)
                throw new ArgumentNullException(nameof(sourceProcess));

            NodeParamCompositeModule param = new NodeParamCompositeModule
            {
                ModuleName = sourceProcess.ProcessName,
                SourceProcessId = sourceProcess.ID,
                SourceProcessName = sourceProcess.ProcessName,
                HasCanvasGraph = sourceProcess.HasCanvasGraph,
                NodeInfos = new List<NodeConfig>(),
                ConnectionInfos = new List<ProcessConnectionConfig>()
            };

            foreach (NodeBase node in sourceProcess.Nodes)
            {
                if (node == null)
                    continue;

                param.NodeInfos.Add(new NodeConfig
                {
                    NodeType = node.NodeType,
                    NodeName = node.NodeName,
                    ID = node.ID,
                    Active = node.Active,
                    Selected = false,
                    HasCanvasLayout = true,
                    CanvasX = node.CanvasLocation.X,
                    CanvasY = node.CanvasLocation.Y,
                    CanvasWidth = node.CanvasSize.Width,
                    CanvasHeight = node.CanvasSize.Height,
                    IsStartNode = node.IsStartNode,
                    NodeParam = CloneNodeParam(node.ParamForm == null ? null : node.ParamForm.Params)
                });
            }

            foreach (ProcessConnection connection in sourceProcess.Connections)
            {
                if (connection == null)
                    continue;

                param.ConnectionInfos.Add(new ProcessConnectionConfig
                {
                    ID = connection.ID,
                    FromNodeId = connection.FromNodeId,
                    ToNodeId = connection.ToNodeId,
                    FromAnchor = connection.FromAnchor,
                    ToAnchor = connection.ToAnchor,
                    Branch = connection.Branch
                });
            }

            return param;
        }

        /// <summary>
        /// 深拷贝组合模块参数，避免运行时内部流程修改界面参数对象。
        /// </summary>
        /// <param name="sourceParam">需要拷贝的组合模块参数。</param>
        /// <returns>独立的组合模块参数副本。</returns>
        public static NodeParamCompositeModule Clone(NodeParamCompositeModule sourceParam)
        {
            if (sourceParam == null)
                return null;

            string json = JsonConvert.SerializeObject(sourceParam);
            return JsonConvert.DeserializeObject<NodeParamCompositeModule>(json);
        }

        /// <summary>
        /// 深拷贝普通节点参数，保留具体参数类型和其内部多态成员。
        /// </summary>
        /// <param name="sourceParam">需要拷贝的节点参数。</param>
        /// <returns>独立的节点参数副本。</returns>
        public static INodeParam CloneNodeParam(INodeParam sourceParam)
        {
            if (sourceParam == null)
                return null;

            string json = JsonConvert.SerializeObject(sourceParam);
            return JsonConvert.DeserializeObject(json, sourceParam.GetType()) as INodeParam;
        }
    }
}
