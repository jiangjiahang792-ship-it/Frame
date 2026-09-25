namespace TieCornerInspection.WinForms;

/// <summary>
/// 提供 WinForms Demo 的应用程序入口点。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 启动扎带右端缺角检测主窗体。
    /// </summary>
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
