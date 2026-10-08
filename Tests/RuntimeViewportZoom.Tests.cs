using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>验证滚轮宿主和运行窗口默认状态，只显示屏幕外的独立编辑器测试窗口。</summary>
internal static class RuntimeViewportZoomTests
{
    /// <summary>成功断言数量。</summary>
    private static int checks;

    /// <summary>允许分别运行窗口或缩放用例，便于观察各自的失败证据。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            if (args.Length == 0 || args[0] == "windows") VerifyRuntimeWindows();
            if (args.Length == 0 || args[0] == "zoom")
            {
                VerifyManualViewport();
                VerifyWheelAnchor();
                VerifyZoomedDragFocus();
                VerifyZoomedResizeAndCopy();
            }
            Console.WriteLine("PASS RuntimeViewportZoom: " + checks);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    /// <summary>从工具栏返回已滚动的画布，鼠标按下不能改变视口或拖动起点。</summary>
    private static void VerifyZoomedDragFocus()
    {
        foreach (string scale in new[] { "50%", "75%", "100%", "125%", "150%", "200%" })
        using (var editor = OpenEditor())
        {
            var canvas = Field<RuntimeDesignCanvas>(editor, "canvas");
            var workspace = Field<Panel>(editor, "workspace");
            var combo = Field<ToolStripComboBox>(editor, "zoomCombo");
            var session = Field<RuntimeLayoutSession>(editor, "session");
            var element = session.Add(RuntimeWidgetKind.Text, new Point(600, 520));
            canvas.SnapToGrid = false;
            combo.SelectedItem = scale;
            combo.ComboBox.Focus();
            workspace.AutoScrollPosition = new Point((int)(500 * canvas.Zoom), (int)(420 * canvas.Zoom));
            Application.DoEvents();
            Point scroll = workspace.AutoScrollPosition;
            Point location = Point.Round(canvas.CanvasPoint(new PointF(element.X + 25, element.Y + 25)));
            Point screen = canvas.PointToScreen(location);
            Console.WriteLine("缩放=" + scale + "，按下前滚动=" + scroll + "，画布=" + canvas.Location + "，焦点=" + canvas.Focused);
            canvas.BeginPointerInteraction(new MouseEventArgs(MouseButtons.Left, 1, location.X, location.Y, 0), Keys.None);
            Console.WriteLine("按下后滚动=" + workspace.AutoScrollPosition + "，画布=" + canvas.Location);
            Check(workspace.AutoScrollPosition == scroll, "缩放后点击画布不能自动滚回原点：" + scale);
            Point moved = canvas.PointToClient(new Point(screen.X + 30, screen.Y + 30));
            typeof(RuntimeDesignCanvas).GetMethod("OnMouseMove", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(canvas, new object[] { new MouseEventArgs(MouseButtons.Left, 0, moved.X, moved.Y, 0) });
            Rectangle preview = (Rectangle)typeof(RuntimeDesignCanvas).GetMethod("GetDesignBounds", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(canvas, new object[] { element });
            int delta = (int)Math.Round(30 / canvas.Zoom);
            Check(Math.Abs(preview.X - element.X - delta) <= 1 && Math.Abs(preview.Y - element.Y - delta) <= 1,
                "拖动增量必须从原控件位置按倍率换算：" + scale);
            typeof(RuntimeDesignCanvas).GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(canvas, new object[] { new MouseEventArgs(MouseButtons.Left, 1, moved.X, moved.Y, 0) });
            Check(session.Find(element.Id).Bounds == preview, "松开后位置应与拖动预览一致：" + scale);
            Check(workspace.AutoScrollPosition == scroll, "提交拖动不重置滚动：" + scale);
            session.Undo();
            session.Redo();
            Check(workspace.AutoScrollPosition == scroll && session.Find(element.Id).Bounds == preview, "撤销重做保持视口及提交位置：" + scale);
            Field<PropertyGrid>(editor, "propertyGrid").Focus();
            location = Point.Round(canvas.CanvasPoint(new PointF(preview.X + 25, preview.Y + 25)));
            canvas.BeginPointerInteraction(new MouseEventArgs(MouseButtons.Left, 1, location.X, location.Y, 0), Keys.None);
            Check(workspace.AutoScrollPosition == scroll, "属性面板返回画布仍保持原位：" + scale);
            PointerEvent(canvas, "OnMouseUp", location);
            Check(session.Find(element.Id).Bounds == preview, "原地点击不能移动组件：" + scale);
        }
    }

    /// <summary>缩放后的调整尺寸与Ctrl复制使用同一稳定视口，不改变原件或固定边。</summary>
    private static void VerifyZoomedResizeAndCopy()
    {
        foreach (string scale in new[] { "50%", "200%" })
        using (var editor = OpenEditor())
        {
            var canvas = Field<RuntimeDesignCanvas>(editor, "canvas");
            var workspace = Field<Panel>(editor, "workspace");
            var combo = Field<ToolStripComboBox>(editor, "zoomCombo");
            var session = Field<RuntimeLayoutSession>(editor, "session");
            var element = session.Add(RuntimeWidgetKind.Signal, new Point(600, 520));
            canvas.SnapToGrid = false;
            combo.SelectedItem = scale;
            canvas.SelectElement(element.Id);
            combo.ComboBox.Focus();
            workspace.AutoScrollPosition = new Point((int)(500 * canvas.Zoom), (int)(420 * canvas.Zoom));
            Point scroll = workspace.AutoScrollPosition;
            Rectangle original = element.Bounds;
            Point location = Point.Round(canvas.CanvasPoint(new PointF(original.Right, original.Bottom)));
            canvas.BeginPointerInteraction(new MouseEventArgs(MouseButtons.Left, 1, location.X, location.Y, 0), Keys.None);
            Point moved = new Point(location.X + 30, location.Y + 30);
            PointerEvent(canvas, "OnMouseMove", moved);
            Rectangle preview = (Rectangle)typeof(RuntimeDesignCanvas).GetMethod("GetDesignBounds", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(canvas, new object[] { element });
            int delta = (int)Math.Round(30 / canvas.Zoom);
            Check(preview.Location == original.Location && preview.Size == new Size(original.Width + delta, original.Height + delta), "缩放手柄只改变活动边：" + scale);
            PointerEvent(canvas, "OnMouseUp", moved);
            Check(workspace.AutoScrollPosition == scroll && session.Find(element.Id).Bounds == preview, "手柄拖动保持视口和最终位置：" + scale);

            combo.ComboBox.Focus();
            location = Point.Round(canvas.CanvasPoint(new PointF(preview.X + 25, preview.Y + 25)));
            canvas.BeginPointerInteraction(new MouseEventArgs(MouseButtons.Left, 1, location.X, location.Y, 0), Keys.Control);
            moved = new Point(location.X + 30, location.Y + 30);
            PointerEvent(canvas, "OnMouseMove", moved);
            PointerEvent(canvas, "OnMouseUp", moved);
            Check(session.Document.Elements.Count == 2 && session.Find(element.Id).Bounds == preview, "Ctrl拖动只生成副本并保留原件：" + scale);
            Check(session.Find(canvas.SelectedId).Bounds.Location == new Point(preview.X + delta, preview.Y + delta), "副本按当前缩放换算位移：" + scale);
            Check(workspace.AutoScrollPosition == scroll, "复制提交保持滚动位置：" + scale);
        }
    }

    /// <summary>向屏幕外控件触发局部鼠标事件，不移动用户鼠标。</summary>
    private static void PointerEvent(RuntimeDesignCanvas canvas, string method, Point location) =>
        typeof(RuntimeDesignCanvas).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(canvas, new object[] { new MouseEventArgs(MouseButtons.Left, 1, location.X, location.Y, 0) });

    /// <summary>单窗口和多窗口都默认最大化，但不显示这些运行预览窗体。</summary>
    private static void VerifyRuntimeWindows()
    {
        foreach (int count in new[] { 1, 3 })
        using (var first = new RuntimeInterfacePreviewForm(new RuntimeLayoutDocument()))
        using (var second = new RuntimeInterfacePreviewForm(new RuntimeLayoutDocument()))
        {
            first.ConfigureRuntimeWindow("工位一", 0, count);
            second.ConfigureRuntimeWindow("工位二", 1, count);
            Check(first.WindowState == FormWindowState.Maximized && second.WindowState == FormWindowState.Maximized,
                "count=" + count + " 的运行窗口必须默认最大化");
            Check(first.Text.StartsWith("工位一") && second.Text.StartsWith("工位二"), "最大化保留独立标题");
            first.WindowState = second.WindowState = FormWindowState.Normal;
            Rectangle original = first.Bounds;
            Check(original.Width >= first.MinimumSize.Width && original.Height >= first.MinimumSize.Height, "还原尺寸有效");
            if (count > 1) Check(first.Location != second.Location, "多窗口仍保留独立错开的还原位置");
            for (int index = 0; index < 3; index++)
            {
                first.WindowState = FormWindowState.Maximized;
                first.WindowState = FormWindowState.Normal;
                Check(first.Bounds == original, "最大化还原不漂移");
            }
            first.Dispose();
            Check(!second.IsDisposed && second.Text.StartsWith("工位二"), "关闭一个窗口不影响另一个");
        }
    }

    /// <summary>真实滚动区域在手动模式下保留位置，自适应仍随窗口变化。</summary>
    private static void VerifyManualViewport()
    {
        using (var editor = OpenEditor())
        {
            var combo = Field<ToolStripComboBox>(editor, "zoomCombo");
            var workspace = Field<Panel>(editor, "workspace");
            var canvas = Field<RuntimeDesignCanvas>(editor, "canvas");
            combo.SelectedItem = "100%";
            workspace.AutoScrollPosition = new Point(500, 420);
            Application.DoEvents();
            Point original = workspace.AutoScrollPosition;
            Check(original.X < 0 && original.Y < 0, "测试建立双向真实滚动位置");
            editor.Size = new Size(editor.Width + 50, editor.Height + 30);
            Application.DoEvents();
            Check(workspace.AutoScrollPosition == original, "手动倍率窗口Resize不能重置滚动位置");
            Check(Math.Abs(Zoom(canvas) - 1f) < 0.0001f, "手动倍率Resize保持100%");
            var session = Field<RuntimeLayoutSession>(editor, "session");
            session.Edit(document => document.Name = "修改页面名称");
            Application.DoEvents();
            Check(workspace.AutoScrollPosition == original, "普通编辑刷新不重置位置");
            Size canvasSize = canvas.Size;
            for (int index = 0; index < 5; index++) Invoke(editor, "ApplyZoom");
            Check(canvas.Size == canvasSize && workspace.AutoScrollPosition == original, "重复应用倍率不累积滚动条尺寸或偏移");
            Field<ToolStripButton>(editor, "fitButton").PerformClick();
            Check(combo.SelectedIndex == 0 && workspace.AutoScrollPosition == Point.Empty, "自适应按钮重置滚动并保持模式");
            float firstFit = Zoom(canvas);
            editor.Size = new Size(editor.Width - 100, editor.Height - 60);
            Application.DoEvents();
            Check(Zoom(canvas) < firstFit && combo.SelectedIndex == 0, "自适应随窗口变小重新计算倍率");
            Check(!workspace.HorizontalScroll.Visible && !workspace.VerticalScroll.Visible, "自适应不残留滚动条");
        }
    }

    /// <summary>模拟画布已分流的缩放请求，锚点在窗口内保持不动且倍率受限。</summary>
    private static void VerifyWheelAnchor()
    {
        using (var editor = OpenEditor())
        {
            var combo = Field<ToolStripComboBox>(editor, "zoomCombo");
            var workspace = Field<Panel>(editor, "workspace");
            var canvas = Field<RuntimeDesignCanvas>(editor, "canvas");
            combo.SelectedItem = "100%";
            workspace.AutoScrollPosition = new Point(500, 420);
            var mouse = new Point(250, 180);
            Point location = new Point(mouse.X - canvas.Left, mouse.Y - canvas.Top);
            PointF document = DocumentPoint(canvas, location);
            RaiseZoom(canvas, 120, location);
            Application.DoEvents();
            Check(Zoom(canvas) > 1f && combo.SelectedIndex > 0, "滚轮增加倍率并退出自适应");
            CheckAnchor(canvas, document, mouse, "放大保持鼠标锚点");
            CheckCombo(combo, canvas);
            location = new Point(mouse.X - canvas.Left, mouse.Y - canvas.Top);
            RaiseZoom(canvas, -120, location);
            Application.DoEvents();
            CheckAnchor(canvas, document, mouse, "缩小保持鼠标锚点");
            Check(Math.Abs(Zoom(canvas) - 1f) < 0.001f, "相反滚轮方向恢复原倍率");
            float previous = Zoom(canvas);
            RaiseZoom(canvas, 0, location);
            Check(Zoom(canvas) == previous, "零增量不改变倍率");
            for (int index = 0; index < 80; index++) RaiseZoom(canvas, 120, new Point(600, 500));
            Check(Math.Abs(Zoom(canvas) - 2f) < 0.0001f, "放大上限200%");
            for (int index = 0; index < 100; index++) RaiseZoom(canvas, -120, new Point(600, 500));
            Check(Math.Abs(Zoom(canvas) - 0.05f) < 0.0001f, "缩小下限5%");
            CheckCombo(combo, canvas);
            Check(combo.Items.Count <= 12, "滚轮不会无限累积倍率下拉项");
            combo.SelectedItem = "150%";
            Check(Math.Abs(Zoom(canvas) - 1.5f) < 0.0001f, "滚轮后仍可用工具栏选择倍率");
            workspace.AutoScrollPosition = new Point(400, 350);
            location = new Point(mouse.X - canvas.Left, mouse.Y - canvas.Top);
            document = DocumentPoint(canvas, location);
            var wheel = new HandledMouseEventArgs(MouseButtons.None, 0, location.X, location.Y, 120);
            typeof(RuntimeDesignCanvas).GetMethod("OnMouseWheel", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(canvas, new object[] { wheel });
            Application.DoEvents();
            Check(wheel.Handled && Zoom(canvas) > 1.5f, "空白滚轮经真实事件消费，不再传给父滚动条");
            CheckAnchor(canvas, document, mouse, "空白滚轮不叠加父滚动偏移");
            Field<ToolStripButton>(editor, "fitButton").PerformClick();
            RaiseZoom(canvas, 120, new Point(canvas.Width / 2, canvas.Height / 2));
            Check(combo.SelectedIndex > 0 && workspace.AutoScroll, "从自适应滚轮切到可滚动的手动模式");
            float manual = Zoom(canvas);
            editor.Size = new Size(editor.Width + 40, editor.Height + 20);
            Application.DoEvents();
            Check(Math.Abs(Zoom(canvas) - manual) < 0.0001f, "自定义滚轮倍率在Resize后不变");
        }
    }

    /// <summary>仅编辑器窗口在屏幕外显示，不激活且不进入任务栏。</summary>
    private static OffscreenEditor OpenEditor()
    {
        var editor = new OffscreenEditor();
        editor.StartPosition = FormStartPosition.Manual;
        editor.Location = new Point(-30000, -30000);
        editor.ShowInTaskbar = false;
        editor.Size = new Size(1400, 850);
        editor.Show();
        Application.DoEvents();
        return editor;
    }

    /// <summary>屏幕外测试窗口不获得系统激活状态。</summary>
    private sealed class OffscreenEditor : RuntimeInterfaceEditorForm
    {
        /// <summary>使用较大文档建立稳定的双向滚动范围。</summary>
        public OffscreenEditor() : base(new RuntimeLayoutDocument { Width = 4000, Height = 2800 }, "缩放测试", null) { }
        /// <summary>不干扰当前前台窗口。</summary>
        protected override bool ShowWithoutActivation => true;
    }

    /// <summary>经过真实画布滚轮分流入口，不发送全局鼠标或键盘输入。</summary>
    private static void RaiseZoom(RuntimeDesignCanvas canvas, int delta, Point location)
    {
        canvas.HandleImageMouseWheel(new MouseEventArgs(MouseButtons.None, 0, location.X, location.Y, delta), Keys.Control);
    }

    /// <summary>读取画布公开倍率契约。</summary>
    private static float Zoom(RuntimeDesignCanvas canvas) => (float)typeof(RuntimeDesignCanvas).GetProperty("Zoom").GetValue(canvas);
    /// <summary>读取文档锚点，保持浮点精度。</summary>
    private static PointF DocumentPoint(RuntimeDesignCanvas canvas, Point point) =>
        (PointF)typeof(RuntimeDesignCanvas).GetMethod("DocumentPoint").Invoke(canvas, new object[] { point });
    /// <summary>锚点误差只允许滚动位置整数舍入的一像素。</summary>
    private static void CheckAnchor(RuntimeDesignCanvas canvas, PointF document, Point expected, string message)
    {
        var point = (PointF)typeof(RuntimeDesignCanvas).GetMethod("CanvasPoint").Invoke(canvas, new object[] { document });
        Check(Math.Abs(point.X + canvas.Left - expected.X) <= 1.1f && Math.Abs(point.Y + canvas.Top - expected.Y) <= 1.1f, message);
    }
    /// <summary>倍率显示与画布实际倍率同步到百分数两位小数。</summary>
    private static void CheckCombo(ToolStripComboBox combo, RuntimeDesignCanvas canvas) =>
        Check(Math.Abs(float.Parse(combo.Text.TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture) / 100f - Zoom(canvas)) < 0.0002f, "倍率下拉与画布同步");
    /// <summary>读取编辑器本类声明的固定控件和会话。</summary>
    private static T Field<T>(RuntimeInterfaceEditorForm editor, string name) =>
        (T)typeof(RuntimeInterfaceEditorForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
    /// <summary>调用既有缩放入口检查幂等性。</summary>
    private static void Invoke(RuntimeInterfaceEditorForm editor, string name) =>
        typeof(RuntimeInterfaceEditorForm).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, null);
    /// <summary>统计行为断言，错误包含明确原因。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }
}
