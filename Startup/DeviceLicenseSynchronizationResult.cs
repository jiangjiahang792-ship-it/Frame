namespace TDJS_Vision.Startup
{
    /// <summary>
    /// 设备许可证同步状态。
    /// </summary>
    internal enum DeviceLicenseSynchronizationStatus
    {
        /// <summary>
        /// 目标许可证已经新增或覆盖。
        /// </summary>
        Copied = 0,

        /// <summary>
        /// 目标许可证与源文件一致，无需重复写入。
        /// </summary>
        Unchanged = 1,

        /// <summary>
        /// 目标运行环境未部署，本次已跳过。
        /// </summary>
        Skipped = 2,

        /// <summary>
        /// 源文件无效或目标同步失败。
        /// </summary>
        Failed = 3
    }

    /// <summary>
    /// 保存单个设备许可证目标的同步结果。
    /// </summary>
    internal sealed class DeviceLicenseSynchronizationResult
    {
        /// <summary>
        /// 同步状态。
        /// </summary>
        private readonly DeviceLicenseSynchronizationStatus _status;

        /// <summary>
        /// 本条结果对应的源文件或目标文件路径。
        /// </summary>
        private readonly string _targetPath;

        /// <summary>
        /// 用于写入日志的简体中文消息。
        /// </summary>
        private readonly string _message;

        /// <summary>
        /// 初始化设备许可证同步结果。
        /// </summary>
        /// <param name="status">同步状态。</param>
        /// <param name="targetPath">源文件或目标文件路径。</param>
        /// <param name="message">用于写入日志的简体中文消息。</param>
        public DeviceLicenseSynchronizationResult(
            DeviceLicenseSynchronizationStatus status,
            string targetPath,
            string message)
        {
            _status = status;
            _targetPath = targetPath ?? string.Empty;
            _message = message ?? string.Empty;
        }

        /// <summary>
        /// 获取同步状态。
        /// </summary>
        public DeviceLicenseSynchronizationStatus Status
        {
            get { return _status; }
        }

        /// <summary>
        /// 获取本条结果对应的源文件或目标文件路径。
        /// </summary>
        public string TargetPath
        {
            get { return _targetPath; }
        }

        /// <summary>
        /// 获取用于写入日志的简体中文消息。
        /// </summary>
        public string Message
        {
            get { return _message; }
        }
    }
}
