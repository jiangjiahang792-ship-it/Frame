using System.Diagnostics;
using System.IO;

namespace TDJS_Vision
{
    /// <summary>
    /// 定义外部程序启动能力，便于窗体与具体进程启动方式解耦。
    /// </summary>
    internal interface IExternalProgramLauncher
    {
        /// <summary>
        /// 启动指定路径的外部程序。
        /// </summary>
        /// <param name="executablePath">外部程序的绝对路径。</param>
        void Start(string executablePath);
    }

    /// <summary>
    /// 使用系统进程接口启动外部程序的默认实现。
    /// </summary>
    internal sealed class ExternalProgramLauncher : IExternalProgramLauncher
    {
        /// <summary>
        /// 启动指定路径的外部程序，启动后立即返回且不等待外部程序退出。
        /// </summary>
        /// <param name="executablePath">外部程序的绝对路径。</param>
        public void Start(string executablePath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                UseShellExecute = true
            };

            System.Diagnostics.Process.Start(startInfo);
        }
    }
}
