using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Logger;
using TDJS_Vision.Diagnostics;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision
{

    internal static class Program
    {
        /// <summary>
        /// 视觉检测运行期默认线程池的最低工作线程和IO线程数量。
        /// </summary>
        private const int MinimumThreadPoolThreads = 64;

        public static FormMain MainForm { get; private set; } = null;
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            ConfigureThreadPoolMinimums();
            LogHelper.AddLog(MsgLevel.Warn, $"【启动诊断-程序身份】{PerformanceSpikeDiagnostics.GetExecutableIdentityText()}；命令行={Environment.CommandLine}", true);

            // HslCommunication通信库授权
            if (!HslCommunication.Authorization.SetAuthorizationCode("d8868ab9-4494-4056-98c6-b669e2434e25"))
            {
                MessageBoxTD.Show("HslCommunication通信库授权失败！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string solutionPath = null;

            if (args.Length > 0)
            {
                solutionPath = args[0];
                if (solutionPath.EndsWith(".Sol", StringComparison.OrdinalIgnoreCase))
                {
                    MainForm = new FormMain(solutionPath);
                    // 路径合法，传递给主窗体
                    Application.Run(MainForm);
                    return;
                }
            }
            MainForm = new FormMain(solutionPath);
            Application.Run(MainForm);
        }

        /// <summary>
        /// 配置线程池最小线程数，减少多相机回调、流程并行节点和后台存图同时运行时的排队延迟。
        /// </summary>
        private static void ConfigureThreadPoolMinimums()
        {
            int minWorkerThreads;
            int minCompletionPortThreads;
            ThreadPool.GetMinThreads(out minWorkerThreads, out minCompletionPortThreads);

            int targetWorkerThreads = Math.Max(minWorkerThreads, MinimumThreadPoolThreads);
            int targetCompletionPortThreads = Math.Max(minCompletionPortThreads, MinimumThreadPoolThreads);

            ThreadPool.SetMinThreads(targetWorkerThreads, targetCompletionPortThreads);
        }
    }
}

namespace VersionInfo
{
    class VersionInfo
    {
        public static string GetExeVer()
        {
            // 获取当前程序集的信息
            Assembly assembly = Assembly.GetExecutingAssembly();

            return assembly.GetName().Version.ToString();
        }
    }
}
