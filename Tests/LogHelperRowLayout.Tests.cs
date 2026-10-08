using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Logger;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]

/// <summary>独立验证实际日志控件的行高、跨屏缩放和绘制边界，不启动主程序或连接设备。</summary>
internal static class LogHelperRowLayoutTests
{
    /// <summary>记录通过的断言数量。</summary>
    private static int checks;

    /// <summary>在本机各显示器验证真实列表，并保存预览截图。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var host = new Form { Text = "日志行高回归测试", StartPosition = FormStartPosition.Manual,
                AutoScaleMode = AutoScaleMode.Font, ClientSize = new Size(1000, 500), ShowInTaskbar = false })
            using (var helper = new LogHelper { Dock = DockStyle.Fill })
            {
                host.Controls.Add(helper);
                host.Location = Screen.PrimaryScreen.WorkingArea.Location + new Size(80, 80);
                host.Show();
                PumpMessages();
                var tabs = (TabControl)helper.Controls[0];
                foreach (TabPage tab in tabs.TabPages)
                {
                    var list = (ListBox)tab.Controls[0];
                    for (int i = 0; i < 40; i++)
                        list.Items.Add("2026-09-28 15:00:00.123456    " + (i % 2 == 0 ? "Warn" : "Info") +
                            "    相机（前端剥皮）自动重连失败：设备不在线。日志中文与 Agjp 字体检查 " + i);
                }

                // 同一个已创建句柄的窗口往返显示器，覆盖 DPI 改变消息及首次显示隐藏分类。
                for (int pass = 0; pass < 2; pass++)
                foreach (Screen screen in Screen.AllScreens)
                {
                    host.Location = screen.WorkingArea.Location + new Size(80, 80);
                    PumpMessages();
                    foreach (TabPage tab in tabs.TabPages)
                    {
                        tabs.SelectedTab = tab;
                        PumpMessages();
                        var list = (ListBox)tab.Controls[0];
                        CheckHeight(list);
                        Font original = list.Font;
                        using (var larger = new Font(original.FontFamily, original.SizeInPoints * 1.5f))
                        {
                            list.Font = larger;
                            CheckHeight(list);
                            CheckPaint(helper, list);
                            list.Font = original;
                        }
                        CheckHeight(list);
                        typeof(Control).GetMethod("RecreateHandle", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(list, null);
                        CheckHeight(list);
                    }
                    tabs.SelectedIndex = 0;
                    PumpMessages();
                    using (var bitmap = new Bitmap(helper.Width, helper.Height))
                    {
                        helper.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                        bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                            "log-rows-" + (screen.Primary ? "primary" : "secondary") + ".png"));
                    }
                    Console.WriteLine("屏幕 {0}：DPI={1}，行高={2}，字体={3}", screen.DeviceName,
                        helper.listBoxAll.DeviceDpi, helper.listBoxAll.ItemHeight, helper.listBoxAll.Font.SizeInPoints);
                }
            }
            Console.WriteLine("通过：日志行高与绘制边界，" + checks + " 项断言。");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    /// <summary>处理窗口创建、布局及跨屏 DPI 消息。</summary>
    private static void PumpMessages()
    {
        for (int i = 0; i < 4; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(20); }
    }

    /// <summary>实际字体应完整放进行高内，且相邻原生条目不重叠。</summary>
    private static void CheckHeight(ListBox list)
    {
        using (Graphics graphics = list.CreateGraphics())
        {
            int textHeight = TextRenderer.MeasureText(graphics, "国Ag", list.Font, Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Height;
            Check(list.ItemHeight >= textHeight + 2, list.Name + " 行高不足：" + list.ItemHeight + "/" + textHeight);
        }
        Check(list.GetItemRectangle(0).Bottom <= list.GetItemRectangle(1).Top, "相邻日志条目重叠");
    }

    /// <summary>直接调用实际绘制事件，以像素检查普通、选中及长文本不能越过当前行。</summary>
    private static void CheckPaint(LogHelper helper, ListBox list)
    {
        object original = list.Items[0];
        list.Items[0] = "Warn    中文日志 Agjp & 测试\r\n第二行不能绘制到下一条日志 " + new string('W', 500);
        foreach (DrawItemState state in new[] { DrawItemState.None, DrawItemState.Selected })
        using (var bitmap = new Bitmap(400, list.ItemHeight + 40))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Magenta);
            var bounds = new Rectangle(10, 20, 380, list.ItemHeight);
            var args = new DrawItemEventArgs(graphics, list.Font, bounds, 0, state);
            typeof(LogHelper).GetMethod("listBox1_DrawItem", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(helper, new object[] { list, args });
            int ink = 0;
            for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                Color pixel = bitmap.GetPixel(x, y);
                if (!bounds.Contains(x, y))
                {
                    if (pixel.ToArgb() != Color.Magenta.ToArgb())
                        throw new InvalidOperationException("日志绘制越过当前行边界");
                }
                else if (pixel.ToArgb() != (state == DrawItemState.Selected ? Color.MediumTurquoise : Color.White).ToArgb())
                    ink++;
            }
            Check(ink > 50, "日志文字没有绘制");
            Check(true, "绘制边界没有越界");
        }
        list.Items[0] = original;
    }

    /// <summary>累计断言或抛出明确失败原因。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }
}
