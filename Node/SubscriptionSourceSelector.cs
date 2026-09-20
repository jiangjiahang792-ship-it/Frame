using System.Collections.Generic;
using System.Linq;

namespace TDJS_Vision.Node
{
    /// <summary>定义公共订阅的上游排序和兼容输出选择策略，供新增节点直接复用。</summary>
    public interface ISubscriptionSourceSelector
    {
        /// <summary>按连线距离由近到远返回上游，同层按节点编号稳定排序。</summary>
        IReadOnlyList<NodeBase> GetUpstreamNodes(NodeBase target);

        /// <summary>选择符合输入契约的首个公开输出，不选择缺失或仅供旧方案使用的输出。</summary>
        SubscriptionOutputDescriptor SelectOutput(NodeBase source, SubscriptionInputContract contract, bool includeAdvanced);
    }

    /// <summary>按反向连线广度优先查找来源；仅在配置变化时执行，不参与逐帧算法。</summary>
    public sealed class NearestSubscriptionSourceSelector : ISubscriptionSourceSelector
    {
        /// <summary>公共无状态实例，所有标准订阅控件默认使用同一规则。</summary>
        public static readonly NearestSubscriptionSourceSelector Instance = new NearestSubscriptionSourceSelector();

        /// <summary>构建反向邻接表并逐层遍历；访问集合防止环路与分支汇合重复。</summary>
        public IReadOnlyList<NodeBase> GetUpstreamNodes(NodeBase target)
        {
            var result = new List<NodeBase>();
            if (target?.Process == null) return result;
            var nodes = target.Process.Nodes.ToDictionary(node => node.ID);
            var parents = target.Process.Connections.ToLookup(edge => edge.ToNodeId, edge => edge.FromNodeId);
            var visited = new HashSet<int> { target.ID };
            var level = new List<int> { target.ID };
            while (level.Count > 0)
            {
                var next = new List<int>();
                foreach (int id in level)
                    foreach (int parent in parents[id])
                        if (nodes.ContainsKey(parent) && visited.Add(parent)) next.Add(parent);
                next.Sort();
                foreach (int id in next) result.Add(nodes[id]);
                level = next;
            }
            return result;
        }

        /// <summary>复用端口目录的类型、数量和可见性规则，不要求上游已生成实际结果值。</summary>
        public SubscriptionOutputDescriptor SelectOutput(NodeBase source, SubscriptionInputContract contract, bool includeAdvanced)
        {
            return SubscriptionPortCatalog.GetOutputs(source, contract, includeAdvanced, null)
                .FirstOrDefault(output => !output.IsMissing && !output.IsLegacySelection &&
                    output.Visibility != SubscriptionOutputVisibility.Hidden);
        }
    }
}
