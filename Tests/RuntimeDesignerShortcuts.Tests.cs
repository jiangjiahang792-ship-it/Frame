using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>屏幕外驱动画布消息，验证复制快捷键和拖动事务，不接触系统剪贴板。</summary>
internal static class RuntimeDesignerShortcutsTests
{
    /// <summary>行为断言计数。</summary>
    private static int checks;
    /// <summary>执行画布实际快捷键与鼠标处理。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            TestClipboard();
            TestCopyDrag(1); TestCopyDrag(0.5f);
            TestOrdinaryDrag();
            Console.WriteLine("通过：设计器快捷键，" + checks + " 项断言。");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    /// <summary>普通移动和缩放不生成副本，松开位置无需提前收到移动事件。</summary>
    private static void TestOrdinaryDrag()
    {
        var session = new RuntimeLayoutSession(new RuntimeLayoutDocument());
        var original = session.Add(RuntimeWidgetKind.Text, new Point(100, 100));
        using (var canvas = CreateCanvas(new MemoryClipboard()))
        {
            canvas.Bind(session); canvas.CreateControl();
            Begin(canvas, Position(canvas, new Point(120, 120)), Keys.None);
            Mouse(canvas, "OnMouseUp", Position(canvas, new Point(320, 270)));
            Check(session.Document.Elements.Count == 1 && original.X == 300 && original.Y == 250, "普通移动只修改原件，使用松开时坐标");
            Rectangle before = original.Bounds;
            Begin(canvas, Position(canvas, new Point(before.Right, before.Bottom)), Keys.None);
            Mouse(canvas, "OnMouseUp", Position(canvas, new Point(before.Right + 40, before.Bottom + 40)));
            Check(original.Width == before.Width + 40 && original.Height == before.Height + 40, "普通右下角缩放仍正常");
            before = original.Bounds;
            Begin(canvas, Position(canvas, new Point(original.X + 20, original.Y + 20)), Keys.Control);
            Mouse(canvas, "OnMouseUp", Position(canvas, new Point(original.X + 20, original.Y + 20)));
            Check(session.Document.Elements.Count == 1 && canvas.SelectedId == null && original.Bounds == before, "Ctrl单击已选控件只取消选择，不创建副本");
        }
    }
    /// <summary>覆盖快照、重复粘贴、跨页和预览隔离。</summary>
    private static void TestClipboard()
    {
        var clipboard = new MemoryClipboard();
        var session = new RuntimeLayoutSession(new RuntimeLayoutDocument());
        var original = session.Add(RuntimeWidgetKind.Text, new Point(100, 100));
        using (var canvas = CreateCanvas(clipboard))
        {
            canvas.Bind(session); canvas.SelectElement(original.Id);
            Check(Key(canvas, Keys.Control | Keys.C), "Ctrl+C 未作为控件复制处理");
            Check(session.Document.Elements.Count == 1 && clipboard.Payload != null, "复制只写快照，不新增组件");
            original.Text = "复制之后改变原文";
            Check(Key(canvas, Keys.Control | Keys.V), "Ctrl+V 未作为控件粘贴处理");
            var first = session.Find(canvas.SelectedId);
            Check(session.Document.Elements.Count == 2 && first.Id != original.Id && first.Text != original.Text, "粘贴新标识与复制时快照");
            Check(first.X == 124 && first.Y == 124, "首次粘贴错开原控件");
            Key(canvas, Keys.Control | Keys.V);
            var second = session.Find(canvas.SelectedId);
            Check(second.Id != first.Id && second.X == 148 && second.Y == 148, "重复粘贴生成不同副本并递增偏移");
            Key(canvas, Keys.Control | Keys.Z);
            Check(session.Document.Elements.Count == 2, "一次撤销只移除一个粘贴控件");
            Key(canvas, Keys.Control | Keys.Y);
            Check(session.Document.Elements.Count == 3, "粘贴可重做");
            var nextPage = new RuntimeLayoutSession(new RuntimeLayoutDocument { Width = 320, Height = 240 });
            canvas.Bind(nextPage);
            Key(canvas, Keys.Control | Keys.V);
            Check(nextPage.Document.Elements.Count == 1 && nextPage.Document.Elements[0].Bounds.Right <= 320, "跨页粘贴受目标边界约束");
            string error = null; canvas.EditError += value => error = value;
            clipboard.Payload = "{损坏的组件}";
            Key(canvas, Keys.Control | Keys.V);
            Check(error != null && nextPage.Document.Elements.Count == 1, "损坏剪贴板不破坏文档");
            clipboard.Payload = null;
            Key(canvas, Keys.Control | Keys.V);
            Check(nextPage.Document.Elements.Count == 1, "普通文字或空剪贴板不粘贴控件");
            error = null; clipboard.Fail = true;
            Key(canvas, Keys.Control | Keys.C);
            Check(error != null && nextPage.Document.Elements.Count == 1, "剪贴板写入失败不改布局");
            error = null; Key(canvas, Keys.Control | Keys.V);
            Check(error != null && nextPage.Document.Elements.Count == 1, "剪贴板读取失败不改布局");
            clipboard.Fail = false;
            canvas.PreviewMode = true;
            Check(!Key(canvas, Keys.Control | Keys.C) && !Key(canvas, Keys.Control | Keys.V), "运行预览不截获控件快捷键");
        }
    }
    /// <summary>验证副本只在左键释放时生成、保留原件并支持取消。</summary>
    private static void TestCopyDrag(float zoom)
    {
        var session = new RuntimeLayoutSession(new RuntimeLayoutDocument());
        var original = session.Add(RuntimeWidgetKind.Text, new Point(100, 100));
        using (var canvas = CreateCanvas(new MemoryClipboard()))
        {
            canvas.Bind(session); canvas.SetZoom(zoom); canvas.CreateControl();
            Point start = Position(canvas, new Point(120, 120));
            Point finish = Position(canvas, new Point(320, 270));
            Begin(canvas, start, Keys.Control);
            Mouse(canvas, "OnMouseMove", finish);
            Check(session.Document.Elements.Count == 1 && original.Bounds.Location == new Point(100, 100), "Ctrl拖动仅预览且不移动原件");
            using (var bitmap = new Bitmap(canvas.Width, canvas.Height))
            {
                canvas.DrawToBitmap(bitmap, canvas.ClientRectangle);
                Point border = Position(canvas, new Point(340, 250));
                Color pixel = bitmap.GetPixel(border.X, border.Y);
                Check(pixel.R > 200 && pixel.G > 70 && pixel.G < 180 && pixel.B < 60, "副本在目标位置显示橙色选择边框");
                bitmap.Save(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, zoom == 1 ? "复制拖动-100.png" : "复制拖动-50.png"));
            }
            Mouse(canvas, "OnMouseUp", finish, MouseButtons.Right);
            Check(session.Document.Elements.Count == 1, "右键释放不得提交左键复制");
            Mouse(canvas, "OnMouseUp", finish);
            var copy = session.Find(canvas.SelectedId);
            Check(session.Document.Elements.Count == 2 && copy.Id != original.Id && copy.X == 300 && copy.Y == 250, "松开左键按缩放坐标放置副本");
            Check(original.X == 100 && original.Y == 100, "复制提交后原控件仍不动");
            session.Undo();
            Check(session.Document.Elements.Count == 1, "拖动复制仅一个撤销事务");
            session.Redo();
            Check(session.Document.Elements.Count == 2, "拖动复制可重做");
            Begin(canvas, start, Keys.Control); Mouse(canvas, "OnMouseMove", finish);
            Key(canvas, Keys.Escape); Mouse(canvas, "OnMouseUp", finish);
            Check(session.Document.Elements.Count == 2, "Esc取消复制预览");
            Begin(canvas, start, Keys.Control); Mouse(canvas, "OnMouseMove", finish);
            canvas.Capture = false; Mouse(canvas, "OnMouseUp", finish);
            Check(session.Document.Elements.Count == 2, "捕获丢失取消复制");
            Begin(canvas, start, Keys.Control);
            Mouse(canvas, "OnMouseUp", new Point(0, 0));
            Check(session.Document.Elements.Count == 2, "页面外释放取消副本");
            Begin(canvas, start, Keys.Control); Mouse(canvas, "OnMouseMove", finish);
            var next = new RuntimeLayoutSession(new RuntimeLayoutDocument());
            canvas.Bind(next); Mouse(canvas, "OnMouseUp", finish);
            Check(next.Document.Elements.Count == 0 && session.Document.Elements.Count == 2, "切换页面不能将副本提交给另一个页面");
        }
    }
    /// <summary>通过注入剪贴板避开用户真实系统数据；旧代码只用于RED阶段。</summary>
    private static RuntimeDesignCanvas CreateCanvas(MemoryClipboard clipboard)
    {
        var constructor = typeof(RuntimeDesignCanvas).GetConstructor(new[] { typeof(IRuntimeWidgetClipboard) });
        return constructor == null ? new RuntimeDesignCanvas() : (RuntimeDesignCanvas)constructor.Invoke(new object[] { clipboard });
    }
    /// <summary>执行确定的修饰键按下入口，真实鼠标入口读取同一修饰键后转发。</summary>
    private static void Begin(RuntimeDesignCanvas canvas, Point point, Keys modifiers)
    {
        var method = typeof(RuntimeDesignCanvas).GetMethod("BeginPointerInteraction", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(method != null, "缺少Ctrl拖动复制入口");
        method.Invoke(canvas, new object[] { new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0), modifiers });
    }
    /// <summary>取得指定逻辑点的真实画布坐标。</summary>
    private static Point Position(RuntimeDesignCanvas canvas, Point logical)
    {
        var rectangle = (RectangleF)typeof(RuntimeDesignCanvas).GetMethod("ToScreen", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(canvas, new object[] { new Rectangle(logical, new Size(1, 1)) });
        return Point.Round(rectangle.Location);
    }
    /// <summary>驱动现有WinForms鼠标回调。</summary>
    private static void Mouse(RuntimeDesignCanvas canvas, string method, Point point, MouseButtons button = MouseButtons.Left)
    { typeof(RuntimeDesignCanvas).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas, new object[] { new MouseEventArgs(button, 1, point.X, point.Y, 0) }); }
    /// <summary>执行画布命令键。</summary>
    private static bool Key(RuntimeDesignCanvas canvas, Keys key)
    { return (bool)typeof(RuntimeDesignCanvas).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas, new object[] { new Message(), key }); }
    /// <summary>断言失败提供明确交互原因。</summary>
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new InvalidOperationException(message); }
    /// <summary>只保存本测试的组件快照。</summary>
    private sealed class MemoryClipboard : IRuntimeWidgetClipboard
    {
        /// <summary>独立于系统剪贴板的内容。</summary>
        internal string Payload;
        /// <summary>模拟系统剪贴板被占用。</summary>
        internal bool Fail;
        /// <summary>读取内存内容。</summary>
        public string Read() { if (Fail) throw new InvalidOperationException("剪贴板被占用"); return Payload; }
        /// <summary>覆盖内存内容。</summary>
        public void Write(string payload) { if (Fail) throw new InvalidOperationException("剪贴板被占用"); Payload = payload; }
    }
}
#if CLIPBOARD_CONTRACT_STUB
namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>仅在功能尚未创建时帮助执行真实旧画布的失败测试。</summary>
    public interface IRuntimeWidgetClipboard
    {
        /// <summary>读取组件内容。</summary>
        string Read();
        /// <summary>写入组件内容。</summary>
        void Write(string payload);
    }
}
#endif
