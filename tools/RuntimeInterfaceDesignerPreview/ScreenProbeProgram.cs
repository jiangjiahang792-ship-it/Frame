using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>复用运行窗口的跨屏诊断入口，不加载方案或连接设备。</summary>
internal static class ScreenProbeProgram
{
    /// <summary>诊断文件仅写入独立体验目录。</summary>
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screen-probe.log");

    /// <summary>创建纯显示窗口，记录屏幕和标题按钮边界以定位跨屏裁剪。</summary>
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        File.WriteAllText(LogPath, string.Join(Environment.NewLine, Screen.AllScreens.Select(screen =>
            screen.DeviceName + " bounds=" + screen.Bounds + " work=" + screen.WorkingArea)) + Environment.NewLine);
        using (var form = new RuntimeInterfacePreviewForm())
        {
            form.Text = "跨屏窗口诊断（不连接设备）";
            form.Shown += (sender, args) => Record(form);
            form.ResizeEnd += (sender, args) => Record(form);
            using (var timer = new Timer { Interval = 1000 })
            {
                string previous = null;
                timer.Tick += (sender, args) =>
                {
                    string current = Describe(form);
                    if (current == previous) return;
                    previous = current;
                    File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss.fff ") + current + Environment.NewLine);
                };
                timer.Start();
                Application.Run(form);
            }
        }
    }

    /// <summary>记录一次窗口生命周期完成后的实际布局。</summary>
    private static void Record(Form form) => File.AppendAllText(LogPath,
        DateTime.Now.ToString("HH:mm:ss.fff ") + Describe(form) + Environment.NewLine);

    /// <summary>输出真实窗体边界及按钮的屏幕坐标，不读取现场运行数据。</summary>
    private static string Describe(Form form)
    {
        var close = (Button)typeof(RuntimeInterfacePreviewForm).GetField("closeButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
        return "state=" + form.WindowState + " bounds=" + form.Bounds + " client=" + form.ClientSize +
            " restore=" + form.RestoreBounds + " dpi=" + form.DeviceDpi + " screen=" + Screen.FromControl(form).DeviceName +
            " close=" + close.RectangleToScreen(close.ClientRectangle) + " title=" + close.Parent.Bounds;
    }
}
