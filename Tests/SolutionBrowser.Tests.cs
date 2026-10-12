using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Drawing;
using TDJS_Vision.Forms.SolutionBrowser;

/// <summary>验证真实目录服务及选择窗体，不加载生产方案和设备。</summary>
internal static class SolutionBrowserTests
{
    /// <summary>已完成断言数。</summary>
    private static int checks;
    /// <summary>检查回归条件。</summary>
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks++; }
    /// <summary>创建临时方案并返回其完整路径。</summary>
    private static string Create(string directory, string name)
    { Directory.CreateDirectory(directory); string path = Path.Combine(directory, name); File.WriteAllText(path, "{}"); return path; }
    /// <summary>读取窗体控件以验证搜索及选择行为。</summary>
    private static T Field<T>(object target, string name)
    { return (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target); }
    /// <summary>执行隔离文件验证和窗体绘制。</summary>
    [STAThread]
    private static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "TDJS-SolutionBrowser-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string first = Create(Path.Combine(root, "甲"), "产品一.Sol");
            string second = Create(Path.Combine(root, "乙"), "产品一.Sol");
            string other = Create(Path.Combine(root, "乙"), "产品二.Sol");
            Create(Path.Combine(root, "乙", "子目录"), "不扫描.Sol");
            Create(Path.Combine(root, "乙"), "空方案.Sol");
            string history = Path.Combine(root, "记录", "历史.txt");
            var catalog = new FileSolutionCatalog(history);
            catalog.Remember(first);
            catalog.Remember(first.ToUpperInvariant());
            Check(File.ReadAllLines(history).Length == 1, "路径应忽略大小写去重");
            catalog.Remember(second);
            var reopened = new FileSolutionCatalog(history);
            var entries = reopened.Read(new string[0]);
            Check(entries.Count == 3, "重启后恢复历史并发现相邻方案，排除模板和子目录");
            Check(entries.Count(p => p.Name == "产品一") == 2, "不同路径的同名方案应保留");
            catalog.Remember(Path.Combine(root, "不存在.Sol"));
            Check(File.ReadAllLines(history).Length == 2, "不存在的方案不加入历史");
            File.Delete(first);
            Check(reopened.Read(new string[0]).Count == 2, "已删除文件不应显示");
            File.AppendAllText(history, "\r\n无效\0路径\r\n");
            Check(reopened.Read(new string[0]).Count == 2, "坏历史条目不影响有效方案");
            using (var form = new FormSolutionBrowser(reopened, new string[0]))
            {
                typeof(FormSolutionBrowser).GetField("entries", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(form, entries);
                Field<TextBox>(form, "searchBox").Text = "产品二";
                var grid = Field<DataGridView>(form, "solutionGrid");
                Check(((System.Collections.IList)grid.DataSource).Count == 1, "名称搜索应只保留对应方案");
                Field<TextBox>(form, "searchBox").Text = "未匹配";
                Check(!Field<Button>(form, "openButton").Enabled, "空列表不能确认打开");
                Field<TextBox>(form, "searchBox").Text = "产品";
                // 隐形创建窗口句柄，确保子控件参与离屏绘制，不打扰正在使用的桌面。
                form.Opacity = 0;
                form.Show();
                Application.DoEvents();
                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "方案列表预览.png"));
                }
                typeof(FormSolutionBrowser).GetMethod("SelectSolution", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, new object[] { other });
                Check(form.SelectedSolutionPath == other && form.DialogResult == DialogResult.OK, "确认后返回完整路径");
            }
            Console.WriteLine("方案列表回归通过：" + checks + " 项断言。");
        }
        finally { Directory.Delete(root, true); }
    }
}
