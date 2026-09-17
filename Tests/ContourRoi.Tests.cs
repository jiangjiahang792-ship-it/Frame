using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using OpenCvSharp;
using TDJS_Vision.Node._3_Detection.ContourMatch;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using Rectangle = System.Drawing.Rectangle;

/// <summary>组合区域交互、原生掩膜与持久化回归，不连接现场设备。</summary>
internal static class ContourRoiTests
{
    /// <summary>反射访问真实控件入口。</summary>
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    /// <summary>已通过的检查数。</summary>
    private static int _checks;
    /// <summary>失败即停止，禁止忽略交互或算法不一致。</summary>
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); _checks++; }
    /// <summary>读取字段。</summary>
    private static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, Flags).GetValue(obj);
    /// <summary>调用入口。</summary>
    private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Flags).Invoke(obj, args);
    /// <summary>原图转换为画布坐标。</summary>
    private static Point Pixel(ImageCanvas canvas, double x, double y) => Point.Round((PointF)Call(canvas, "ClientPoint", x, y));
    /// <summary>通过真实鼠标事件入口验证控件行为。</summary>
    private static void Mouse(ImageCanvas canvas, string name, Point point, MouseButtons button = MouseButtons.Left, int clicks = 1)
    { Call(canvas, name, new MouseEventArgs(button, clicks, point.X, point.Y, 0)); }
    /// <summary>绘制一个拖动类型区域。</summary>
    private static void Draw(ImageCanvas canvas, TemplateRegionKind kind, double x1, double y1, double x2, double y2)
    {
        canvas.SetRegionTool(kind);
        Mouse(canvas, "OnMouseDown", Pixel(canvas, x1, y1));
        Mouse(canvas, "OnMouseMove", Pixel(canvas, x2, y2));
        Mouse(canvas, "OnMouseUp", Pixel(canvas, x2, y2));
    }
    /// <summary>等待编辑器异步调用，正常派发界面事件。</summary>
    private static void Wait(ContourTemplateEditor form)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        do { Application.DoEvents(); Thread.Sleep(5); if (clock.Elapsed.TotalSeconds > 40) throw new TimeoutException("编辑器操作超时。"); }
        while (Field<bool>(form, "_busy"));
    }
    /// <summary>发起实际组合建模并检查任务完成。</summary>
    private static void Build(ContourTemplateEditor form)
    {
        var task = (Task)Call(form, "ConfirmCreateAsync"); Wait(form); task.GetAwaiter().GetResult();
        Check(!Field<bool>(form, "_templateDirty") && Field<IShapeMatcher>(form, "_matcher").HasModel,
            "建模失败：" + Field<Label>(form, "statusLabel").Text);
    }
    /// <summary>生成三个分离对象，中间对象用于检查未选区域不能混入轮廓。</summary>
    private static Mat Scene()
    {
        var image = new Mat(240, 320, MatType.CV_8UC3, Scalar.All(20));
        Cv2.Rectangle(image, new Rect(32, 40, 58, 55), Scalar.All(235), 4);
        Cv2.Line(image, new OpenCvSharp.Point(40, 50), new OpenCvSharp.Point(75, 80), Scalar.All(170), 3);
        Cv2.Circle(image, new OpenCvSharp.Point(250, 140), 29, Scalar.All(225), 4);
        Cv2.Line(image, new OpenCvSharp.Point(236, 130), new OpenCvSharp.Point(263, 150), Scalar.All(190), 3);
        Cv2.Rectangle(image, new Rect(134, 65, 43, 91), Scalar.All(250), 4);
        return image;
    }
    /// <summary>保存用于人工检查布局的真实窗体截图。</summary>
    private static void Screenshot(Form form, string directory, string name)
    {
        form.PerformLayout(); Application.DoEvents();
        using (var bitmap = new Bitmap(form.Width, form.Height))
        { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(System.IO.Path.Combine(directory, name + ".png")); }
    }
    /// <summary>验证绘制、选择、移动、缩放、旋转、历史和所有区域工具。</summary>
    private static void Interaction(ContourTemplateEditor form, ImageCanvas canvas, string directory)
    {
        Draw(canvas, TemplateRegionKind.矩形, 20, 25, 105, 110);
        Check(canvas.RegionCount == 1 && canvas.RegionTool == TemplateRegionKind.选择 && !canvas.HasRegionGesture, "矩形松开没有生成并返回选择。");
        Check(!Field<IShapeMatcher>(form, "_matcher").HasModel, "区域绘制错误地执行了算法。");
        Draw(canvas, TemplateRegionKind.矩形, 209, 99, 291, 181);
        Check(canvas.RegionCount == 2 && Field<ListBox>(form, "regionList").Items.Count == 2, "第二个区域覆盖了第一个区域或列表未同步。");
        var original = canvas.CopyRegions();
        Mouse(canvas, "OnMouseDown", Pixel(canvas, 60, 65)); Mouse(canvas, "OnMouseUp", Pixel(canvas, 60, 65));
        Check(canvas.SelectedRegionIndex == 0, "画布点击不能重新选择旧区域。");
        Mouse(canvas, "OnMouseDown", Pixel(canvas, 60, 65)); Mouse(canvas, "OnMouseUp", Pixel(canvas, 70, 73));
        Check(Math.Abs(canvas.SelectedRegion.CenterX - original[0].CenterX - 10) < 2, "选区移动错误。");
        canvas.UndoRegion(false);
        Check(Math.Abs(canvas.CopyRegions()[0].CenterX - original[0].CenterX) < .01, "移动撤销没有还原。");
        canvas.SelectRegion(0);
        var corner = Point.Round((PointF)Call(canvas, "RegionHandlePoint", canvas.SelectedRegion, 4));
        Mouse(canvas, "OnMouseDown", corner); Mouse(canvas, "OnMouseUp", new Point(corner.X + 24, corner.Y + 15));
        Check(canvas.SelectedRegion.Width > original[0].Width + 8 && canvas.SelectedRegion.Height > original[0].Height + 4, "缩放手柄没有改变尺寸。");
        canvas.UndoRegion(false); canvas.SelectRegion(0);
        Point rotate = Point.Round((PointF)Call(canvas, "RegionHandlePoint", canvas.SelectedRegion, 8));
        var selected = canvas.SelectedRegion;
        Mouse(canvas, "OnMouseDown", rotate); Mouse(canvas, "OnMouseUp", Pixel(canvas, selected.CenterX + 50, selected.CenterY));
        Check(Math.Abs(canvas.SelectedRegion.Angle) > 45, "旋转手柄没有改变角度。");
        canvas.UndoRegion(false); canvas.SelectRegion(0);
        Field<NumericUpDown>(form, "regionAngleNumeric").Value = 25;
        Check(Math.Abs(canvas.SelectedRegion.Angle - 25) < .01, "右侧角度输入没有改变区域。");
        canvas.UndoRegion(false);
        canvas.ZoomAt(Pixel(canvas, 65, 65), 2);
        Draw(canvas, TemplateRegionKind.椭圆, 116, 175, 174, 223);
        Check(canvas.RegionCount == 3 && Math.Abs(canvas.SelectedRegion.Width - 58) <= 2, "缩放后绘制没有使用原图坐标。");
        canvas.ResetView();
        Draw(canvas, TemplateRegionKind.扇形, 110, 10, 190, 90);
        Field<NumericUpDown>(form, "sectorStartNumeric").Value = 15;
        Field<NumericUpDown>(form, "sectorSweepNumeric").Value = 140;
        Check(canvas.SelectedRegion.Kind == TemplateRegionKind.扇形 && canvas.SelectedRegion.SweepAngle == 140 && canvas.SelectedRegion.StartAngle == 15, "扇形角度未生效。");
        canvas.SetRegionTool(TemplateRegionKind.多边形);
        foreach (Point p in new[] { Pixel(canvas, 120, 105), Pixel(canvas, 185, 111), Pixel(canvas, 150, 165) })
        { Mouse(canvas, "OnMouseDown", p); Mouse(canvas, "OnMouseUp", p); }
        Check(canvas.HasRegionGesture && !Field<Button>(form, "saveButton").Enabled, "未闭合多边形允许保存。");
        Mouse(canvas, "OnMouseDown", Pixel(canvas, 150, 165), MouseButtons.Right);
        Check(canvas.RegionCount == 5 && canvas.SelectedRegion.Points.Count == 3, "多边形未正确闭合。");
        canvas.BrushSize = 18;
        Draw(canvas, TemplateRegionKind.画笔, 33, 173, 83, 198);
        Check(canvas.RegionCount == 6 && canvas.SelectedRegion.Kind == TemplateRegionKind.画笔, "画笔松开未形成独立区域。");
        Screenshot(form, directory, "组合区域-全部工具");
        canvas.DeleteSelectedRegion(); Check(canvas.RegionCount == 5, "删除影响其它区域。");
        canvas.UndoRegion(false); Check(canvas.RegionCount == 6, "删除撤销失败。");
        canvas.UndoRegion(true); Check(canvas.RegionCount == 5, "重做失败。");
        canvas.ClearRegions(); Check(canvas.RegionCount == 0, "清空失败。");
        canvas.UndoRegion(false); Check(canvas.RegionCount == 5, "清空撤销失败。");
        var allShapes = JsonConvert.DeserializeObject<List<TemplateRegion>>(JsonConvert.SerializeObject(canvas.CopyRegions()));
        Check(allShapes.Select(r => r.Kind).Distinct().Count() == 4 && allShapes[4].Points.Count == 3, "不同形状序列化后顶点或类型丢失。");
        canvas.SetRegionTool(TemplateRegionKind.矩形);
        Mouse(canvas, "OnMouseDown", Pixel(canvas, 10, 10)); Mouse(canvas, "OnMouseMove", Pixel(canvas, 30, 30));
        canvas.ZoomAt(Pixel(canvas, 60, 60), 1.25);
        Check(!canvas.HasRegionGesture && Field<Button>(form, "saveButton").Enabled, "绘制中缩放后交互按钮未恢复。");
        canvas.ResetView();
        canvas.SetRegionTool(TemplateRegionKind.多边形);
        Mouse(canvas, "OnMouseDown", Pixel(canvas, 20, 20));
        Call(canvas, "ProcessCmdKey", new Message(), Keys.Escape);
        Check(!canvas.HasRegionGesture && canvas.RegionCount == 5, "Esc取消未闭合区域影响了已完成区域。");
        canvas.SetRegions(original); Call(form, "ImageCanvas_RegionsChanged", null, EventArgs.Empty);
    }

    /// <summary>验证真实原生轮廓仅来自区域并集，序列化后匹配及编辑一致。</summary>
    private static void Model(ContourTemplateEditor form, ImageCanvas canvas, Mat scene, string directory)
    {
        Field<NumericUpDown>(form, "modelLevelsNumeric").Value = 1;
        Field<NumericUpDown>(form, "featureCountNumeric").Value = 0;
        Build(form);
        var matcher = Field<IShapeMatcher>(form, "_matcher");
        var roi = Field<Rectangle>(form, "_modelRoi");
        var features = matcher.GetModelFeatures();
        Check(features.Any(p => p.X + roi.X < 110) && features.Any(p => p.X + roi.X > 200), "两个不相连的区域没有共同参与模型。");
        Check(features.All(p => p.X + roi.X < 110 || p.X + roi.X > 200), "两区域之间未选中的对象混入模型。");
        var matches = matcher.Find(scene, new FindOptions { PyramidLevels = 1, MaximumMatches = 1, MinimumScore = .6 });
        Console.WriteLine("组合匹配诊断：ROI={0}，特征={1}，结果={2}", roi, features.Count,
            string.Join("；", matches.Select(m => string.Format("分数={0:F4}，中心=({1:F2},{2:F2})，角度={3:F2}", m.Score, m.CenterX, m.CenterY, m.AngleDegrees))));
        if (matches.Count == 0)
            Console.WriteLine("宽松诊断：" + string.Join("；", matcher.Find(scene, new FindOptions { PyramidLevels = 1, MaximumMatches = 1, MinimumScore = 0, AngleStartDegrees = 0, AngleEndDegrees = 0, Mode = ShapeMatchMode.高精度 }).Select(m => string.Format("{0:F4}/{1:F2}/{2:F2}", m.Score, m.CenterX, m.CenterY))));
        Check(matches.Count == 1 && matches[0].Score > .9, "组合模型在原图不能正确匹配。");
        int count = features.Count;
        Call(form, "SaveParameters");
        var saved = form.Params;
        Check(saved.ModelRegions.Count == 2 && saved.EraseMasks.Count == 0, "保存未携带独立区域数据。");
        var isolated = saved.Copy(); isolated.ModelRegions[0].CenterX = 0;
        Check(saved.ModelRegions[0].CenterX != 0, "区域复制泄漏可修改引用。");
        var package = new ContourTemplatePackage { Templates = new List<ContourTemplateDefinition> { new ContourTemplateDefinition { Name = "组合测试", Model = saved } } };
        var imported = ContourTemplatePackage.Decode(package.Encode());
        Check(imported.Templates[0].Model.ModelRegions.Count == 2, "交换文件丢失组合区域。");
        Field<NumericUpDown>(form, "contrastNumeric").Value = 40;
        Check(Field<bool>(form, "_templateDirty"), "参数改变未要求重新建模。");
        bool staleRejected = false; try { Call(form, "SaveParameters"); } catch (TargetInvocationException) { staleRejected = true; }
        Check(staleRejected, "改变创建参数后静默保存了过期模型。");
        form.Params = JsonConvert.DeserializeObject<NodeParamContourMatch>(JsonConvert.SerializeObject(saved));
        var task = (Task)Call(form, "RestoreEditorAsync"); Wait(form); task.GetAwaiter().GetResult();
        Check(canvas.RegionCount == 2 && Field<IShapeMatcher>(form, "_matcher").GetModelFeatures().Count == count, "重新打开后组合轮廓不一致。");
        canvas.SelectRegion(0);
        Field<CheckBox>(form, "regionEnabledCheckBox").Checked = false;
        Check(Field<bool>(form, "_templateDirty") && !Field<ToolStripButton>(form, "eraseRegionButton").Enabled, "区域改变后允许编辑过期轮廓。");
        Build(form); roi = Field<Rectangle>(form, "_modelRoi");
        Check(roi.Left > 200, "停用区域仍参与建模。");
        canvas.UndoRegion(false); Build(form);
        Screenshot(form, directory, "组合区域-轮廓预览");
        Field<TabControl>(form, "editorParameterTabs").SelectedIndex = 1;
        Screenshot(form, directory, "组合区域-创建参数");
        form.Size = new Size(900, 600); Application.DoEvents();
        Screenshot(form, directory, "组合区域-最小窗口");
        Check(canvas.Width > 450 && canvas.Height > 300 && Field<Button>(form, "saveButton").Visible, "最小窗口图像或确认操作被裁切。");
        Field<TabControl>(form, "editorParameterTabs").SelectedIndex = 0;
        Screenshot(form, directory, "组合区域-最小窗口区域参数");
        canvas.ClearRegions();
        bool rejected = false; try { Call(form, "SaveParameters"); } catch (TargetInvocationException) { rejected = true; }
        Check(rejected && !Field<Button>(form, "saveButton").Enabled, "空区域错误保存为旧模板或全图模板。");
    }

    /// <summary>不同几何类型的掩膜必须准确覆盖形状，不能退化为外接矩形。</summary>
    private static void MaskGeometry()
    {
        var regions = new List<TemplateRegion> {
            new TemplateRegion { Kind = TemplateRegionKind.矩形, CenterX = 60, CenterY = 60, Width = 60, Height = 20, Angle = 45 },
            new TemplateRegion { Kind = TemplateRegionKind.椭圆, CenterX = 160, CenterY = 60, Width = 60, Height = 40 },
            new TemplateRegion { Kind = TemplateRegionKind.扇形, CenterX = 60, CenterY = 160, Width = 80, Height = 60, StartAngle = 0, SweepAngle = 90 },
            new TemplateRegion { Kind = TemplateRegionKind.多边形, CenterX = 160, CenterY = 160, Width = 60, Height = 60,
                Points = new List<PointF> { new PointF(-.5F,-.5F), new PointF(.5F,-.5F), new PointF(0,.5F) } },
            new TemplateRegion { Kind = TemplateRegionKind.画笔, CenterX = 110, CenterY = 110, Width = 50, Height = 10,
                StrokeRatio = 1, Points = new List<PointF> { new PointF(-.4F,0),new PointF(.4F,0) } }
        };
        var type = typeof(ImageCanvas).Assembly.GetType("TDJS_Vision.Node._3_Detection.ContourMatch.TemplateRegionMask");
        var method = type.GetMethod("Rasterize", BindingFlags.NonPublic | BindingFlags.Static);
        var mask = (byte[])method.Invoke(null, new object[] { regions, 220, 220 });
        Check(mask[60 * 220 + 60] != 0 && mask[42 * 220 + 80] == 0, "旋转矩形掩膜退化成外接矩形。");
        Check(mask[60 * 220 + 160] != 0 && mask[42 * 220 + 133] == 0, "椭圆掩膜覆盖了角落。");
        Check(mask[170 * 220 + 70] != 0 && mask[150 * 220 + 50] == 0, "扇形起始角或张角不正确。");
        Check(mask[155 * 220 + 160] != 0 && mask[180 * 220 + 180] == 0, "多边形掩膜覆盖了外部区域。");
        Check(mask[110 * 220 + 110] != 0 && mask[117 * 220 + 110] == 0, "画笔直径或区域覆盖不正确。");
        var restored = JsonConvert.DeserializeObject<List<TemplateRegion>>(JsonConvert.SerializeObject(regions));
        var restoredMask = (byte[])method.Invoke(null, new object[] { restored, 220, 220 });
        Check(mask.SequenceEqual(restoredMask), "形状保存恢复改变了实际掩膜像素。");
        regions[0].Enabled = false;
        mask = (byte[])method.Invoke(null, new object[] { regions, 220, 220 });
        Check(mask[60 * 220 + 60] == 0, "停用的区域仍写入掩膜。");
    }

    /// <summary>独立测试及可见演示入口。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            MaskGeometry();
            string directory = args.Length > 0 ? args[0] : "."; System.IO.Directory.CreateDirectory(directory);
            using (var scene = Scene())
            using (var form = new ContourTemplateEditor())
            {
                Call(form, "Configure", null, scene);
                if (args.Contains("--demo") || System.IO.Path.GetFileNameWithoutExtension(Application.ExecutablePath).EndsWith(".Preview", StringComparison.OrdinalIgnoreCase))
                { Application.Run(form); return 0; }
                form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-3000, -3000); form.Show(); Application.DoEvents();
                var canvas = Field<ImageCanvas>(form, "imageCanvas");
                Interaction(form, canvas, directory); Model(form, canvas, scene, directory);
                form.Close();
            }
            Console.WriteLine("组合 ROI 验证通过，断言数：" + _checks); return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
