using System.Threading.Tasks;

namespace TDJS_Vision.Node
{
    /// <summary>
    /// 定义方案恢复后可从已保存参数预加载运行时资源的节点。
    /// </summary>
    public interface INodeRuntimePreloader
    {
        /// <summary>
        /// 获取当前节点是否存在可用于预加载的已保存运行参数。
        /// </summary>
        bool HasSavedRuntimeConfiguration { get; }

        /// <summary>
        /// 从已恢复的节点参数预加载并预热运行时资源。
        /// </summary>
        /// <returns>预加载任务。</returns>
        Task PreloadSavedRuntimeAsync();
    }
}
