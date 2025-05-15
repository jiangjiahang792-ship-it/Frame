using System;
using System.Reflection;
using System.Windows.Forms;
using Logger;

namespace TDJS_Vision
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                Application.Run(new FormMain());
            }
            catch (Exception e)
            {
                LogHelper.AddLog(MsgLevel.Exception, e.Message, true);
            }
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
