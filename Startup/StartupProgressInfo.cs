using System;

namespace TDJS_Vision.Startup
{
    /// <summary>
    /// 表示启动页需要显示的一次进度快照。
    /// </summary>
    internal sealed class StartupProgressInfo
    {
        /// <summary>
        /// 初始化启动进度快照。
        /// </summary>
        /// <param name="stageName">当前阶段名称。</param>
        /// <param name="itemName">当前对象名称。</param>
        /// <param name="current">当前对象序号。</param>
        /// <param name="total">当前阶段对象总数。</param>
        /// <param name="percentage">整体完成百分比。</param>
        public StartupProgressInfo(string stageName, string itemName, int current, int total, int percentage)
        {
            StageName = stageName ?? string.Empty;
            ItemName = itemName ?? string.Empty;
            Current = Math.Max(0, current);
            Total = Math.Max(0, total);
            Percentage = Math.Max(0, Math.Min(100, percentage));
        }

        /// <summary>
        /// 获取当前启动阶段名称。
        /// </summary>
        public string StageName { get; }

        /// <summary>
        /// 获取当前正在加载的对象名称。
        /// </summary>
        public string ItemName { get; }

        /// <summary>
        /// 获取当前对象序号。
        /// </summary>
        public int Current { get; }

        /// <summary>
        /// 获取当前阶段对象总数。
        /// </summary>
        public int Total { get; }

        /// <summary>
        /// 获取整体完成百分比。
        /// </summary>
        public int Percentage { get; }

        /// <summary>
        /// 生成适合启动页显示的对象明细，不包含完整磁盘路径。
        /// </summary>
        /// <returns>当前对象和序号组成的明细文字。</returns>
        public string GetDetailText()
        {
            if (string.IsNullOrWhiteSpace(ItemName))
                return Total > 0 ? $"{Current}/{Total}" : string.Empty;

            return Total > 0
                ? $"{ItemName}（{Current}/{Total}）"
                : ItemName;
        }
    }
}
