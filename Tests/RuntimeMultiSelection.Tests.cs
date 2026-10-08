using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>实际画布消息驱动的框选、多选和批量交互回归。</summary>
internal static class RuntimeMultiSelectionTests
{
    /// <summary>已通过断言。</summary>
    private static int checks;
    /// <summary>在STA线程运行隐藏画布，不连接生产设备。</summary>
    [STAThread]
    private static int Main()
    {
        try { TestSelection(1); TestSelection(.5f); TestGroupEdits(); TestFailedCopy(); Console.WriteLine("通过：多选画布，" + checks + " 项断言。"); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    /// <summary>创建三个互不重叠的小控件。</summary>
    private static RuntimeLayoutSession Session()
    {
        var doc = new RuntimeLayoutDocument();
        for (int i = 0; i < 3; i++) doc.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Text, X = 100 + i * 200, Y = 100, Width = 120, Height = 60 });
        return new RuntimeLayoutSession(doc);
    }
    /// <summary>同一交互在两种缩放下应保持一致。</summary>
    private static void TestSelection(float zoom)
    {
        var session = Session();
        using (var canvas = new RuntimeDesignCanvas(new MemoryClipboard()))
        {
            canvas.Bind(session); canvas.SetZoom(zoom); canvas.CreateControl();
            var ids = session.Document.Elements.Select(e => e.Id).ToArray();
            Click(canvas, new Point(120, 120), Keys.None);
            Click(canvas, new Point(320, 120), Keys.Control);
            Check(Selected(canvas).SequenceEqual(ids.Take(2)), "Ctrl单击应添加第二个选中项而不是复制控件");
            Check(session.Document.Elements.Count == 3 && !session.CanUndo, "多选不新增控件和撤销历史");
            Click(canvas, new Point(120, 120), Keys.Control);
            Check(Selected(canvas).SequenceEqual(new[] { ids[1] }), "Ctrl再次单击取消选择");
            Begin(canvas, new Point(80, 80), Keys.None);
            Mouse(canvas, "OnMouseMove", new Point(440, 180));
            Check(Selected(canvas).Length == 2, "框选拖动实时高亮完整包围的控件");
            using (var bitmap = new Bitmap(canvas.Width, canvas.Height))
            {
                canvas.DrawToBitmap(bitmap, canvas.ClientRectangle);
                bitmap.Save(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, zoom == 1 ? "框选-100.png" : "框选-50.png"));
            }
            Mouse(canvas, "OnMouseUp", new Point(440, 180));
            Check(Selected(canvas).SequenceEqual(ids.Take(2)), "释放框选保留两项");
            Begin(canvas, new Point(650, 180), Keys.Control);
            Mouse(canvas, "OnMouseUp", new Point(480, 80));
            Check(Selected(canvas).Length == 3, "Ctrl反向框选追加第三项");
            Begin(canvas, new Point(80, 80), Keys.None);
            Mouse(canvas, "OnMouseMove", new Point(230, 180));
            Key(canvas, Keys.Escape); Mouse(canvas, "OnMouseUp", new Point(230, 180));
            Check(Selected(canvas).Length == 3, "取消框选恢复原选择");
            Begin(canvas, new Point(80, 80), Keys.None);
            Mouse(canvas, "OnMouseUp", new Point(180, 140));
            Check(Selected(canvas).Length == 0, "部分交叉未完整包含的控件不选中");
            Click(canvas, new Point(120, 120), Keys.None);
            Click(canvas, new Point(320, 120), Keys.Control);
            Begin(canvas, new Point(80, 80), Keys.None);
            Mouse(canvas, "OnMouseMove", new Point(230, 180));
            canvas.Capture = false;
            Check(Selected(canvas).Length == 2, "捕获丢失取消框选并恢复选择");
            Begin(canvas, new Point(80, 80), Keys.None);
            var other = Session(); canvas.Bind(other); Mouse(canvas, "OnMouseUp", new Point(650, 180));
            Check(Selected(canvas).Length == 0 && other.Document.Elements.Count == 3, "切页取消选框且不留下旧页选择");
        }
    }
    /// <summary>多选之后的移动、复制、删除保持组内相对位置与事务边界。</summary>
    private static void TestGroupEdits()
    {
        var session = Session();
        using (var canvas = new RuntimeDesignCanvas(new MemoryClipboard()))
        {
            canvas.Bind(session); canvas.CreateControl(); canvas.SnapToGrid = false;
            Click(canvas, new Point(120, 120), Keys.None); Click(canvas, new Point(320, 120), Keys.Control);
            Begin(canvas, new Point(120, 120), Keys.None);
            Mouse(canvas, "OnMouseMove", new Point(170, 170));
            Check(session.Document.Elements[0].X == 100, "组移动仅更新预览");
            Mouse(canvas, "OnMouseUp", new Point(170, 170));
            Check(session.Document.Elements[0].X == 150 && session.Document.Elements[1].X == 350 && session.Document.Elements[2].X == 500, "整体移动选中项而不移动未选项");
            Key(canvas, Keys.Control | Keys.Z);
            Check(session.Document.Elements[0].X == 100 && session.Document.Elements[1].X == 300 && !session.CanUndo, "整体移动一次撤销");
            Begin(canvas, new Point(120, 120), Keys.Control);
            Mouse(canvas, "OnMouseUp", new Point(220, 320));
            Check(session.Document.Elements.Count == 5 && Selected(canvas).Length == 2, "Ctrl拖动复制整个选择组");
            var copies = session.Document.Elements.Skip(3).ToArray();
            Check(copies[1].X - copies[0].X == 200 && copies.All(e => e.Y == 300), "复制组保持内部偏移");
            Check(session.Document.Elements[0].X == 100 && session.Document.Elements[1].X == 300, "组复制不移动原件");
            Key(canvas, Keys.Control | Keys.C); Key(canvas, Keys.Control | Keys.V);
            Check(session.Document.Elements.Count == 7 && Selected(canvas).Length == 2, "CtrlC/V复制所有选中控件");
            Key(canvas, Keys.Delete);
            Check(session.Document.Elements.Count == 5 && Selected(canvas).Length == 0, "Delete删除全部选中控件");
            Key(canvas, Keys.Control | Keys.Z);
            Check(session.Document.Elements.Count == 7, "批量删除一次撤销");
            Key(canvas, Keys.Control | Keys.A);
            Check(Selected(canvas).Length == 7, "CtrlA选中当前页所有控件");
            Key(canvas, Keys.Left);
            Check(session.Document.Elements[0].X == 99 && session.Document.Elements[1].X == 299, "方向键微调整组");
            Begin(canvas, new Point(119, 120), Keys.None);
            Mouse(canvas, "OnMouseUp", new Point(-100, 120));
            Check(session.Document.Elements[0].X == 0 && session.Document.Elements[1].X == 200, "边界约束应用到整个选择组");
        }
    }
    /// <summary>超过组件上限的复制失败应像取消手势一样恢复按下前的选择。</summary>
    private static void TestFailedCopy()
    {
        var document = Session().Document;
        while (document.Elements.Count < 199)
            document.Elements.Add(new RuntimeLayoutElement { X = 700, Y = 200, Width = 48, Height = 32 });
        var session = new RuntimeLayoutSession(document);
        using (var canvas = new RuntimeDesignCanvas(new MemoryClipboard()))
        {
            canvas.Bind(session); canvas.CreateControl();
            Click(canvas, new Point(120, 120), Keys.None);
            string selected = canvas.SelectedId;
            string error = null; canvas.EditError += message => error = message;
            Begin(canvas, new Point(320, 120), Keys.Control);
            Mouse(canvas, "OnMouseUp", new Point(420, 320));
            Check(error != null && session.Document.Elements.Count == 199 && !session.CanUndo, "超限复制失败不写文档或撤销历史");
            Check(Selected(canvas).SequenceEqual(new[] { selected }), "复制失败恢复手势前选择");
        }
    }
    /// <summary>保持RED可编译，验证旧画布真实行为。</summary>
    private static string[] Selected(RuntimeDesignCanvas canvas)
    {
        var property = typeof(RuntimeDesignCanvas).GetProperty("SelectedIds");
        return property == null ? (canvas.SelectedId == null ? new string[0] : new[] { canvas.SelectedId }) : ((IEnumerable<string>)property.GetValue(canvas)).ToArray();
    }
    /// <summary>将文档坐标转换成测试屏幕坐标。</summary>
    private static Point Position(RuntimeDesignCanvas canvas, Point logical)
    { return Point.Round(((RectangleF)typeof(RuntimeDesignCanvas).GetMethod("ToScreen", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas, new object[] { new Rectangle(logical, new Size(1, 1)) })).Location); }
    /// <summary>固定修饰键的按下入口。</summary>
    private static void Begin(RuntimeDesignCanvas canvas, Point logical, Keys keys)
    { Point p = Position(canvas, logical); typeof(RuntimeDesignCanvas).GetMethod("BeginPointerInteraction", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas, new object[] { new MouseEventArgs(MouseButtons.Left, 1, p.X, p.Y, 0), keys }); }
    /// <summary>发送鼠标移动或抬起。</summary>
    private static void Mouse(RuntimeDesignCanvas canvas, string method, Point logical)
    { Point p = Position(canvas, logical); typeof(RuntimeDesignCanvas).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas, new object[] { new MouseEventArgs(MouseButtons.Left, 1, p.X, p.Y, 0) }); }
    /// <summary>单击控件。</summary>
    private static void Click(RuntimeDesignCanvas canvas, Point p, Keys keys) { Begin(canvas, p, keys); Mouse(canvas, "OnMouseUp", p); }
    /// <summary>发送画布命令键。</summary>
    private static void Key(RuntimeDesignCanvas canvas, Keys key) { typeof(RuntimeDesignCanvas).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas, new object[] { new Message(), key }); }
    /// <summary>检查期望行为。</summary>
    private static void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
    /// <summary>不接触系统的剪贴板实现。</summary>
    private sealed class MemoryClipboard : IRuntimeWidgetClipboard
    {
        /// <summary>组件载荷。</summary>
        private string data;
        /// <summary>读取载荷。</summary>
        public string Read() => data;
        /// <summary>覆盖载荷。</summary>
        public void Write(string payload) { data = payload; }
    }
}
