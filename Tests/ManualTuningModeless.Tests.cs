using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using TDJS_Vision;
using TDJS_Vision.Forms.SolRunParam;

/// <summary>验证真实调参窗口非模态显示、主窗体可操作和失效窗口关闭，不连接设备。</summary>
internal static class ManualTuningModelessTests
{
    /// <summary>失败时终止测试。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>在STA线程运行实际WinForms生命周期。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            using (var owner = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) })
            using (var button = new Button())
            {
                bool clicked = false;
                button.Click += (sender, args) => clicked = true;
                owner.Controls.Add(button);
                owner.Show();
                bool valid = true;
                using (var tuning = new FormSolRunParam(new Process("非模态离线验证"), null, null))
                {
                    tuning.StartPosition = FormStartPosition.Manual;
                    tuning.Location = new Point(-20000, -20000);
                    tuning.ShowModeless(owner, () => { if (!valid) throw new InvalidOperationException("测试上下文失效"); });
                    Application.DoEvents();
                    Check(tuning.Visible && !tuning.Modal && !tuning.IsDisposed, "Show返回后调参窗体应保持打开且非模态。");
                    Check(owner.Enabled && button.Enabled, "主窗口不应被禁用。");
                    button.PerformClick();
                    Check(clicked, "主窗口按钮未响应。");
                    valid = false;
                    typeof(Form).GetMethod("OnActivated", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(tuning, new object[] { EventArgs.Empty });
                    Application.DoEvents();
                    Check(tuning.IsDisposed, "再次激活时失效的调参窗口未关闭释放。");
                }
                using (var tuning = new FormSolRunParam(new Process("所属窗口关闭验证"), null, null))
                {
                    tuning.StartPosition = FormStartPosition.Manual;
                    tuning.Location = new Point(-20000, -20000);
                    tuning.ShowModeless(owner, () => { });
                    Application.DoEvents();
                    owner.Close();
                    Application.DoEvents();
                    Check(tuning.IsDisposed, "所属窗口关闭后调参窗体未释放。");
                }
            }
            Console.WriteLine("ManualTuningModeless: 5 assertions passed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
