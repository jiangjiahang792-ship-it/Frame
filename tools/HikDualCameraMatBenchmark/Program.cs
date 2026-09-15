using System;
using System.Windows.Forms;
using MvCamCtrl.NET;

namespace HikDualCameraMatBenchmark
{
    /// <summary>
    /// 应用程序入口，负责海康SDK的全局初始化和反初始化。
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// 启动双相机基准测试工具。
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            int initializeResult = MyCamera.MV_CC_Initialize_NET();
            if (initializeResult != MyCamera.MV_OK)
            {
                MessageBox.Show(
                    string.Format("海康SDK初始化失败，错误码：0x{0:X8}", initializeResult),
                    "启动失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            try
            {
                Application.Run(new MainForm());
            }
            finally
            {
                MyCamera.MV_CC_Finalize_NET();
            }
        }
    }
}
