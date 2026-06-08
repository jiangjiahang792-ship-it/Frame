using System.Collections.Generic;
using System.Linq;

namespace TDJS_Vision.Forms.SolRunParam
{
    /// <summary>
    /// 运行参数显示范围，用于让图像窗口手动调参只显示当前ROI绘制链路关联的上下限参数。
    /// </summary>
    public class RunParamDisplayFilter
    {
        /// <summary>
        /// 当前过滤范围所属的图像窗口名称。
        /// </summary>
        public string WindowName { get; set; }

        /// <summary>
        /// 是否限制为指定来源节点；为true时来源集合为空也表示不显示任何来源参数。
        /// </summary>
        public bool IsSourceNodeLimited { get; private set; }

        /// <summary>
        /// 触发当前过滤范围的图像显示节点ID集合。
        /// </summary>
        public HashSet<int> ImageShowNodeIds { get; private set; } = new HashSet<int>();

        /// <summary>
        /// 当前图像显示订阅到的ROI绘制节点ID集合。
        /// </summary>
        public HashSet<int> OverlayDrawNodeIds { get; private set; } = new HashSet<int>();

        /// <summary>
        /// ROI绘制项实际订阅的来源节点ID集合。
        /// </summary>
        public HashSet<int> SourceNodeIds { get; private set; } = new HashSet<int>();

        /// <summary>
        /// 标记当前范围已经由ROI绘制链路接管。
        /// </summary>
        public void EnableSourceNodeLimit()
        {
            IsSourceNodeLimited = true;
        }

        /// <summary>
        /// 添加图像显示节点ID。
        /// </summary>
        /// <param name="nodeId">图像显示节点ID。</param>
        public void AddImageShowNodeId(int nodeId)
        {
            AddNodeId(ImageShowNodeIds, nodeId);
        }

        /// <summary>
        /// 添加ROI绘制节点ID，并启用来源节点限制。
        /// </summary>
        /// <param name="nodeId">ROI绘制节点ID。</param>
        public void AddOverlayDrawNodeId(int nodeId)
        {
            AddNodeId(OverlayDrawNodeIds, nodeId);
            EnableSourceNodeLimit();
        }

        /// <summary>
        /// 添加ROI绘制项订阅的来源节点ID。
        /// </summary>
        /// <param name="nodeId">来源节点ID。</param>
        public void AddSourceNodeId(int nodeId)
        {
            AddNodeId(SourceNodeIds, nodeId);
        }

        /// <summary>
        /// 判断指定来源节点是否允许显示运行参数。
        /// </summary>
        /// <param name="nodeId">来源节点ID。</param>
        /// <returns>未启用限制时返回true；启用限制后只允许集合内节点。</returns>
        public bool ContainsSourceNode(int nodeId)
        {
            if (!IsSourceNodeLimited)
                return true;

            return SourceNodeIds.Contains(nodeId);
        }

        /// <summary>
        /// 生成诊断日志文本，便于现场确认过滤范围。
        /// </summary>
        /// <returns>过滤范围摘要。</returns>
        public string ToLogText()
        {
            return $"窗口={WindowName ?? string.Empty}，启用过滤={IsSourceNodeLimited}，图像显示节点={JoinIds(ImageShowNodeIds)}，ROI绘制节点={JoinIds(OverlayDrawNodeIds)}，来源节点={JoinIds(SourceNodeIds)}";
        }

        /// <summary>
        /// 从“ID.节点名”订阅文本中提取节点ID。
        /// </summary>
        /// <param name="nodeText">订阅控件保存的节点文本。</param>
        /// <param name="nodeId">解析出的节点ID。</param>
        /// <returns>是否成功解析。</returns>
        public static bool TryGetNodeId(string nodeText, out int nodeId)
        {
            nodeId = 0;
            if (string.IsNullOrWhiteSpace(nodeText))
                return false;

            int dotIndex = nodeText.IndexOf('.');
            string idText = dotIndex > 0 ? nodeText.Substring(0, dotIndex) : nodeText;
            return int.TryParse(idText, out nodeId) && nodeId > 0;
        }

        /// <summary>
        /// 添加有效节点ID到集合。
        /// </summary>
        /// <param name="nodeIds">目标节点ID集合。</param>
        /// <param name="nodeId">待添加节点ID。</param>
        private static void AddNodeId(HashSet<int> nodeIds, int nodeId)
        {
            if (nodeIds == null || nodeId <= 0)
                return;

            nodeIds.Add(nodeId);
        }

        /// <summary>
        /// 将节点ID集合转换成逗号分隔文本。
        /// </summary>
        /// <param name="nodeIds">节点ID集合。</param>
        /// <returns>逗号分隔文本。</returns>
        private static string JoinIds(HashSet<int> nodeIds)
        {
            if (nodeIds == null || nodeIds.Count == 0)
                return "空";

            return string.Join(",", nodeIds.OrderBy(id => id));
        }
    }
}
