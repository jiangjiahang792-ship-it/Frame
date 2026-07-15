namespace TDJS_Vision.Startup
{
    /// <summary>
    /// 定义软件启动进度的上报接口，使启动流程不依赖具体窗体实现。
    /// </summary>
    internal interface IStartupProgressReporter
    {
        /// <summary>
        /// 上报一条不可变的启动进度信息。
        /// </summary>
        /// <param name="progress">当前启动进度。</param>
        void Report(StartupProgressInfo progress);
    }

    /// <summary>
    /// 忽略全部进度的空实现，用于不经过独立启动页的兼容入口。
    /// </summary>
    internal sealed class NullStartupProgressReporter : IStartupProgressReporter
    {
        /// <summary>
        /// 接收进度但不执行界面操作。
        /// </summary>
        /// <param name="progress">当前启动进度。</param>
        public void Report(StartupProgressInfo progress)
        {
            // 兼容入口只需要复用启动逻辑，不需要显示进度。
        }
    }
}
