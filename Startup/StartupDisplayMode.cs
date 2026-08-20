using System;

namespace TDJS_Vision.Startup
{
    /// <summary>
    /// 提供进程级启动显示策略；正式运行保持原界面行为，后台性能验收时禁止窗口取得前台焦点。
    /// </summary>
    internal static class StartupDisplayMode
    {
        /// <summary>后台性能验收模式使用的进程环境变量。</summary>
        private const string BackgroundAcceptanceEnvironmentVariable =
            "TDJS_VISION_BACKGROUND_ACCEPTANCE";

        /// <summary>
        /// 获取当前进程是否处于后台性能验收模式。
        /// </summary>
        public static bool IsBackgroundAcceptance
        {
            get
            {
                bool isPerformanceAcceptance = string.Equals(
                    Environment.GetEnvironmentVariable("TDJS_VISION_PERFORMANCE_ACCEPTANCE"),
                    "1",
                    StringComparison.Ordinal);
                bool isBackgroundRequested = string.Equals(
                    Environment.GetEnvironmentVariable(BackgroundAcceptanceEnvironmentVariable),
                    "1",
                    StringComparison.Ordinal);
                return isPerformanceAcceptance && isBackgroundRequested;
            }
        }
    }
}
