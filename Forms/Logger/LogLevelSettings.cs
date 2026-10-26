using TDJS_Vision.Properties;

namespace Logger
{
    /// <summary>
    /// 日志等级开关访问器，集中管理系统设置中各等级日志是否允许记录。
    /// </summary>
    public static class LogLevelSettings
    {
        /// <summary>日志初始化独立兜底，避免配置异常封死整个日志类型并阻断启动。</summary>
        private static bool ReadInitial(System.Func<bool> read, bool fallback)
        {
            try { return read(); }
            catch (System.Exception ex) when (ex is System.Configuration.ConfigurationErrorsException ||
                ex is System.TypeInitializationException || RecoverableSettingsFile.IsFileError(ex))
            { RecoverableSettingsFile.Report("日志开关读取失败，使用默认值：" + ex.Message); return fallback; }
        }

        /// <summary>
        /// 调试日志记录缓存开关，避免高频日志入口反复读取配置集合。
        /// </summary>
        private static volatile bool _debugEnabled = ResolveInitialDebugEnabled();

        /// <summary>
        /// 消息日志记录缓存开关，避免高频日志入口反复读取配置集合。
        /// </summary>
        private static volatile bool _infoEnabled = ReadInitial(() => Settings.Default.LogInfoEnabled, true);

        /// <summary>
        /// 警告日志记录缓存开关，避免高频日志入口反复读取配置集合。
        /// </summary>
        private static volatile bool _warnEnabled = ReadInitial(() => Settings.Default.LogWarnEnabled, true);

        /// <summary>
        /// 异常日志记录缓存开关，避免高频日志入口反复读取配置集合。
        /// </summary>
        private static volatile bool _exceptionEnabled = ReadInitial(() => Settings.Default.LogExceptionEnabled, true);

        /// <summary>
        /// 致命日志记录缓存开关，避免高频日志入口反复读取配置集合。
        /// </summary>
        private static volatile bool _fatalEnabled = ReadInitial(() => Settings.Default.LogFatalEnabled, true);

        /// <summary>
        /// 解析进程启动时的Debug记录开关；正式程序使用用户设置，性能验收子进程允许使用不落盘的环境覆盖值。
        /// </summary>
        /// <returns>当前进程应使用的Debug记录状态。</returns>
        private static bool ResolveInitialDebugEnabled()
        {
            if (!string.Equals(
                    System.Environment.GetEnvironmentVariable("TDJS_VISION_PERFORMANCE_ACCEPTANCE"),
                    "1",
                    System.StringComparison.Ordinal))
            {
                return ReadInitial(() => Settings.Default.LogDebugEnabled, false);
            }

            string overrideValue = System.Environment.GetEnvironmentVariable("TDJS_VISION_TEST_LOG_DEBUG_ENABLED");
            if (string.Equals(overrideValue, "1", System.StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(overrideValue, "0", System.StringComparison.Ordinal))
            {
                return false;
            }

            return ReadInitial(() => Settings.Default.LogDebugEnabled, false);
        }

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
