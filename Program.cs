using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Logger;
using TDJS_Vision.Diagnostics;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Startup;

namespace TDJS_Vision
{

    internal static class Program
    {
        /// <summary>
        /// 视觉检测运行期默认线程池的最低工作线程和IO线程数量。
        /// </summary>
        private const int MinimumThreadPoolThreads = 64;

        /// <summary>
        /// 当前软件在同一 Windows 桌面会话内的单实例互斥锁名称。
        /// </summary>
        private const string SingleInstanceMutexName = @"Local\TDJS_Vision_SingleInstance_20260713";

        /// <summary>
        /// 设备许可证启动同步器，负责把根目录许可证分发到已部署的 AI 运行环境。
        /// </summary>
        private static readonly IDeviceLicenseSynchronizer DeviceLicenseSynchronizer = new DeviceLicenseSynchronizer();

        /// <summary>
        /// 当前进程正在运行的主窗体实例。
        /// </summary>
        public static FormMain MainForm { get; internal set; } = null;

        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (Mutex singleInstanceMutex = new Mutex(false, SingleInstanceMutexName))
            {
                bool hasSingleInstance = TryEnterSingleInstance(singleInstanceMutex);
                if (!hasSingleInstance)
                {
                    MessageBoxTD.Show("软件已经运行，请勿重复启动！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    RunApplication(args);
                }
                finally
                {
                    singleInstanceMutex.ReleaseMutex();
                }
            }
        }

        /// <summary>
        /// 尝试获得当前桌面会话内的软件单实例运行权。
        /// </summary>
        /// <param name="singleInstanceMutex">用于限制重复启动的命名互斥锁。</param>
        /// <returns>获得运行权返回 true；已有进程持有运行权返回 false。</returns>
        private static bool TryEnterSingleInstance(Mutex singleInstanceMutex)
        {
            try
            {
                return singleInstanceMutex.WaitOne(0, false);
            }
            catch (AbandonedMutexException)
            {
                // 上一次进程异常退出时系统会标记互斥锁废弃，当前进程已经接管所有权，可以继续启动。
                return true;
            }
        }

        /// <summary>
        /// 执行主程序正常启动流程，必须在单实例互斥锁获取成功后调用。
        /// </summary>
        /// <param name="args">启动命令行参数。</param>
        private static void RunApplication(string[] args)
        {
            ConfigureThreadPoolMinimums();
            LogHelper.AddLog(MsgLevel.Warn, $"【启动诊断-程序身份】{PerformanceSpikeDiagnostics.GetExecutableIdentityText()}；命令行={Environment.CommandLine}", true);
            SynchronizeDeviceLicense();

            // HslCommunication通信库授权
            if (!HslCommunication.Authorization.SetAuthorizationCode("d8868ab9-4494-4056-98c6-b669e2434e25"))
            {
                MessageBoxTD.Show("HslCommunication通信库授权失败！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            string solutionPath = null;

            if (args.Length > 0 && args[0].EndsWith(".Sol", StringComparison.OrdinalIgnoreCase))
                solutionPath = args[0];

            using (StartupApplicationContext startupContext = new StartupApplicationContext(solutionPath))
            {
                Application.Run(startupContext);
            }
        }

        /// <summary>
        /// 同步设备许可证并写入逐目标日志，任何异常都不得阻止主程序继续启动。
        /// </summary>
        private static void SynchronizeDeviceLicense()
        {
            try
            {
                foreach (DeviceLicenseSynchronizationResult result in DeviceLicenseSynchronizer.Synchronize(AppDomain.CurrentDomain.BaseDirectory))
                {
                    MsgLevel logLevel = result.Status == DeviceLicenseSynchronizationStatus.Failed
                        ? MsgLevel.Warn
                        : MsgLevel.Info;
                    LogHelper.AddLog(logLevel, result.Message, true);
                }
            }
            catch (Exception ex)
            {
                // 同步器最外层兜底也只记录日志，许可证问题不能中断软件其它功能。
                LogHelper.AddLog(MsgLevel.Exception, $"设备许可证自动同步发生未处理异常：{ex}", true);
            }
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
