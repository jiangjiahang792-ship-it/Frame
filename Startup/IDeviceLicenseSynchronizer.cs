using System.Collections.Generic;

namespace TDJS_Vision.Startup
{
    /// <summary>
    /// 定义设备许可证同步边界，便于替换文件系统实现或扩展新的运行环境。
    /// </summary>
    internal interface IDeviceLicenseSynchronizer
    {
        /// <summary>
        /// 把程序根目录中的设备许可证同步到已经部署的 AI 运行目录。
        /// </summary>
        /// <param name="applicationDirectory">主程序运行目录。</param>
        /// <returns>本次同步产生的逐目标结果。</returns>
        IReadOnlyList<DeviceLicenseSynchronizationResult> Synchronize(string applicationDirectory);
    }
}
