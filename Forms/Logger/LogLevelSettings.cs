using TDJS_Vision.Properties;

namespace Logger
{
    /// <summary>
    /// 日志等级开关访问器，集中管理系统设置中各等级日志是否允许记录。
    /// </summary>
    public static class LogLevelSettings
    {
        /// <summary>
        /// 调试日志记录缓存开关，避免高频日志入口反复读取配置集合。
        /// </summary>
        private static volatile bool _debugEnabled = Settings.Default.LogDebugEnabled;

        /// <summary>
        /// 消息日志记录缓存开关，避免高频日志入口反复读取配置集合。
        /// </summary>
        private static volatile bool _infoEnabled = Settings.Default.LogInfoEnabled;

        /// <summary>
        /// 警告日志记录缓存开关，避免高频日志入口反复读取配置集合。
        /// </summary>
        private static volatile bool _warnEnabled = Settings.Default.LogWarnEnabled;

        /// <summary>
        /// 异常日志记录缓存开关，避免高频日志入口反复读取配置集合。
        /// </summary>
        private static volatile bool _exceptionEnabled = Settings.Default.LogExceptionEnabled;

        /// <summary>
        /// 致命日志记录缓存开关，避免高频日志入口反复读取配置集合。
        /// </summary>
        private static volatile bool _fatalEnabled = Settings.Default.LogFatalEnabled;

        /// <summary>
        /// 判断指定日志等级当前是否允许记录。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <returns>允许记录返回 true，否则返回 false。</returns>
        public static bool IsEnabled(MsgLevel level)
        {
            switch (level)
            {
                case MsgLevel.Debug:
                    return _debugEnabled;
                case MsgLevel.Info:
                    return _infoEnabled;
                case MsgLevel.Warn:
                    return _warnEnabled;
                case MsgLevel.Exception:
                    return _exceptionEnabled;
                case MsgLevel.Fatal:
                    return _fatalEnabled;
                default:
                    return true;
            }
        }

        /// <summary>
        /// 保存指定日志等级的记录开关。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="enabled">是否允许记录。</param>
        public static void SetEnabled(MsgLevel level, bool enabled)
        {
            switch (level)
            {
                case MsgLevel.Debug:
                    _debugEnabled = enabled;
                    Settings.Default.LogDebugEnabled = enabled;
                    break;
                case MsgLevel.Info:
                    _infoEnabled = enabled;
                    Settings.Default.LogInfoEnabled = enabled;
                    break;
                case MsgLevel.Warn:
                    _warnEnabled = enabled;
                    Settings.Default.LogWarnEnabled = enabled;
                    break;
                case MsgLevel.Exception:
                    _exceptionEnabled = enabled;
                    Settings.Default.LogExceptionEnabled = enabled;
                    break;
                case MsgLevel.Fatal:
                    _fatalEnabled = enabled;
                    Settings.Default.LogFatalEnabled = enabled;
                    break;
                default:
                    return;
            }

            Settings.Default.Save();
        }
    }
}
