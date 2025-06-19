using System;
using System.Reflection;
using System.Windows.Forms;
using Logger;

namespace TDJS_Vision
{

    internal static class Program
    {
        public static FormMain MainForm { get; private set; } = null;
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
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
