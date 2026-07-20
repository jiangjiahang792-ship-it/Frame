namespace TDJS_Vision.Startup
{
    /// <summary>
    /// 表示用户在启动失败界面选择的后续操作。
    /// </summary>
    internal enum StartupFailureAction
    {
        /// <summary>
        /// 重新执行可重试的启动初始化。
        /// </summary>
        Retry,

        /// <summary>
        /// 退出当前软件进程。
        /// </summary>
        Exit
    }
}
