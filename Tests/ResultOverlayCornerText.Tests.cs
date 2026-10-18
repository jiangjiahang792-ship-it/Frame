using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using Newtonsoft.Json;
using TDJS_Vision.Forms.DispShowImage;
using TDJS_Vision.Node;
using TDJS_Vision.Node._6_LogicTool.MultiCondition;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw;

/// <summary>右下角文本框的配置兼容、实际构建和屏幕坐标绘制回归。</summary>
internal static class ResultOverlayCornerTextTests
{
    /// <summary>断言失败时输出具体原因。</summary>
    private static void Check(bool valid, string message)
    {
        if (!valid) throw new Exception(message);
    }

    /// <summary>运行真实程序集测试，不连接设备。</summary>
    [STAThread]
    private static void Main()
    {
        var legacy = JsonConvert.DeserializeObject<NodeParamResultOverlayDraw>("{}");
        Check(!legacy.UseCornerTextBox, "旧配置不能自动启用文本框");
        Check(legacy.OkColorArgb == Color.Green.ToArgb(), "默认OK色必须为截图中的RGB(0,128,0)");
        var item = new NodeParamResultOverlayDraw
        {
            UseCornerTextBox = true,
            TextCoordinateMode = DisplayTextCoordinateMode.Control,
            CornerBackgroundArgb = Color.White.ToArgb(),
            CornerForegroundArgb = Color.Green.ToArgb(),
            CornerFontSize = 24,
            CornerText = "0°",
            Items = null
        };
        var restored = JsonConvert.DeserializeObject<NodeParamResultOverlayDraw>(JsonConvert.SerializeObject(item));
        Check(restored.UseCornerTextBox && restored.CornerForegroundArgb == Color.Green.ToArgb()
            && restored.CornerFontSize == 24 && restored.CornerText == "0°", "保存必须保留独立内容、颜色、启用状态和字号");
        var builder = typeof(NodeResultOverlayDraw).Assembly.GetType(
            "TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw.ResultOverlayDrawBuilder", true);
        var build = builder.GetMethod("BuildDisplayResult", BindingFlags.Static | BindingFlags.NonPublic);
        var result = (AlgorithmResult)build.Invoke(null, new object[] { null, restored, null });
        Check(result.Texts.Count == 1 && result.Texts[0].Text == "0°", "没有绘制项也必须额外显示固定内容");
        var text = result.Texts[0];
        Check(text.Position == DisplayTextPosition.BottomRight && text.CoordinateMode == DisplayTextCoordinateMode.Control
            && text.BackgroundColor.ToArgb() == Color.White.ToArgb() && text.Color.ToArgb() == Color.Green.ToArgb(),
            "右下角文本必须使用独立颜色及控件坐标");
        var clone = (ColorText)builder.GetMethod("CloneColorText", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { text });
        Check(clone.BackgroundColor == text.BackgroundColor, "旋转后必须保留背景色");
        restored.Items = new List<ResultOverlayDrawItem> { new ResultOverlayDrawItem { ManualText = "原有文本", FontSize = 13 } };
        result = (AlgorithmResult)build.Invoke(null, new object[] { null, restored, null });
        Check(result.Texts.Count == 2 && result.Texts[0].Text == "原有文本"
            && result.Texts[0].BackgroundColor.A == 0 && result.Texts[0].FontSize == 13
            && result.Texts[0].Position == DisplayTextPosition.TopLeft && result.Texts[1].Text == "0°",
            "固定内容不能覆盖原有文本及样式");
        restored.UseCornerTextBox = false;
        result = (AlgorithmResult)build.Invoke(null, new object[] { null, restored, null });
        Check(result.Texts.Count == 1 && result.Texts[0].Text == "原有文本", "关闭固定内容不能关闭原有文本");
        restored.UseCornerTextBox = true;
        restored.CornerText = "  ";
        result = (AlgorithmResult)build.Invoke(null, new object[] { null, restored, null });
        Check(result.Texts.Count == 1, "空固定内容不应绘制空白框");

        var roi = new ShowImageControl.RoiTextBlock("", new List<(string, Color)> { ("0°", Color.Green) }, 0, 0, 24)
        {
            Position = DisplayTextPosition.BottomRight,
            UseControlCoordinate = true,
            BackgroundColor = Color.White
        };
        roi.SetControlBounds(320, 240);
        using (var first = Render(roi, 1, PointF.Empty))
        using (var second = Render(roi, 3, new PointF(-200, 75)))
        {
            int white = 0, green = 0;
            for (int y = 0; y < first.Height; y++)
                for (int x = 0; x < first.Width; x++)
                {
                    Color pixel = first.GetPixel(x, y);
                    Check(pixel == second.GetPixel(x, y), "缩放、平移不能移动控件坐标文本");
                    if (pixel.ToArgb() == Color.White.ToArgb()) white++;
                    if (pixel.G > pixel.R && pixel.G > pixel.B) green++;
                }
            Check(white > 100 && green > 10, "必须实际绘制白底和绿色文字");
            first.Save("corner-text-preview.png");
        }
        Console.WriteLine("右下角文本框配置、旋转和缩放绘制测试通过。");
        VerifyBooleanText(build);
        VerifyMixedSizeLayout();
        VerifyItemOrdering();
    }

    /// <summary>通过实际ROI构建和像素范围验证不同字号在四角均按输入顺序连续排列。</summary>
    private static void VerifyMixedSizeLayout()
    {
        var method = typeof(ShowImageControl).GetMethod("BuildStaticRois", BindingFlags.NonPublic | BindingFlags.Static,
            null, new[] { typeof(AlgorithmResult), typeof(int), typeof(int) }, null);
        foreach (DisplayTextPosition position in Enum.GetValues(typeof(DisplayTextPosition)))
        foreach (DisplayTextCoordinateMode mode in Enum.GetValues(typeof(DisplayTextCoordinateMode)))
        foreach (bool reverse in new[] { false, true })
        {
            var result = new AlgorithmResult();
            var first = new ColorText("检测尺寸\n第二行", Color.Red) { Title = "", FontSize = 12, Position = position, CoordinateMode = mode };
            var second = new ColorText("合格", Color.Lime) { Title = "", FontSize = 25, Margin = 15, Position = position, CoordinateMode = mode };
            result.Texts.Add(reverse ? second : first);
            result.Texts.Add(reverse ? first : second);
            var rois = (List<ShowImageControl.IRoiShape>)method.Invoke(null, new object[] { result, 320, 240 });
            Check(rois.Count == 1, "同一角落的不同字号、边距不能拆成重叠文本块");
            var block = (ShowImageControl.RoiTextBlock)rois[0];
            block.SetControlBounds(320, 240);
            using (var bitmap = Render(block, 1, PointF.Empty))
            {
                int redMin = 240, redMax = -1, greenMin = 240, greenMax = -1;
                for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    if (pixel.R > 150 && pixel.G < 80) { redMin = Math.Min(redMin, y); redMax = Math.Max(redMax, y); }
                    if (pixel.G > 150 && pixel.R < 80) { greenMin = Math.Min(greenMin, y); greenMax = Math.Max(greenMax, y); }
                }
                Check(redMax >= 0 && greenMax >= 0, "两段文本都必须完整绘制");
                Check(reverse ? greenMax < redMin : redMax < greenMin, "不同字号的文本重叠或显示顺序错误");
                if (position == DisplayTextPosition.TopLeft && mode == DisplayTextCoordinateMode.Control && !reverse)
                    bitmap.Save("mixed-text-layout-preview.png");
            }
        }
        Console.WriteLine("混合字号、多行文本、四角及双坐标系顺序排版回归通过。");
    }

    /// <summary>验证窗体移动操作先保留编辑数据，再调整顺序与选中项，并保护边界。</summary>
    private static void VerifyItemOrdering()
    {
        using (var form = new NodeParamFormResultOverlayDraw())
        {
            Type type = form.GetType();
            var add = type.GetMethod("AddItem", BindingFlags.Instance | BindingFlags.NonPublic);
            var move = type.GetMethod("MoveCurrentItem", BindingFlags.Instance | BindingFlags.NonPublic);
            add.Invoke(form, new object[] { ResultOverlayDrawItemType.Text });
            add.Invoke(form, new object[] { ResultOverlayDrawItemType.BooleanText });
            var input = (System.Windows.Forms.TextBox)type.GetField("textBoxBooleanOk", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            input.Text = "移动后的合格内容";
            move.Invoke(form, new object[] { -1 });
            var items = (List<ResultOverlayDrawItem>)type.GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            Check(items[0].ItemType == ResultOverlayDrawItemType.BooleanText && items[0].BooleanOkText == input.Text, "上移不能丢失当前编辑内容");
            move.Invoke(form, new object[] { -1 });
            Check(items[0].ItemType == ResultOverlayDrawItemType.BooleanText, "首项上移必须保持顺序");
            move.Invoke(form, new object[] { 1 });
            Check(items[1].ItemType == ResultOverlayDrawItemType.BooleanText, "下移应恢复原有顺序");
            var restored = JsonConvert.DeserializeObject<List<ResultOverlayDrawItem>>(JsonConvert.SerializeObject(items));
            Check(restored[1].BooleanOkText == "移动后的合格内容", "移动后的内容和顺序必须可保存");
        }
        Console.WriteLine("绘制项上移、下移、边界和保存顺序回归通过。");
    }

    /// <summary>验证真实AI和多条件输出的真假映射、统一颜色、保存及禁用行为。</summary>
    private static void VerifyBooleanText(MethodInfo build)
    {
        var process = new TDJS_Vision.Process("布尔文本回归");
        var source = new NodeBase(19, "判定来源", process, NodeType.AITD);
        var owner = new NodeBase(25, "ROI结果绘制", process, NodeType.ResultOverlayDraw);
        process.Nodes.AddRange(new[] { source, owner });
        process.Connections.Add(new TDJS_Vision.ProcessConnection { FromNodeId = 19, ToNodeId = 25 });
        var item = new ResultOverlayDrawItem
        {
            ItemType = ResultOverlayDrawItemType.BooleanText,
            SourceText1 = "19.判定来源",
            BooleanOkText = "检测通过",
            BooleanNgText = "请检查产品",
            FontSize = 28,
            TextPosition = DisplayTextPosition.TopRight,
            TextCoordinateMode = DisplayTextCoordinateMode.Control
        };
        var parameter = new NodeParamResultOverlayDraw
        {
            TextCoordinateMode = DisplayTextCoordinateMode.Control,
            OkColor = Color.Blue, NgColor = Color.Orange,
            UseCornerTextBox = true, CornerText = "独立内容",
            Items = new List<ResultOverlayDrawItem> { item.Clone() }
        };
        parameter = JsonConvert.DeserializeObject<NodeParamResultOverlayDraw>(JsonConvert.SerializeObject(parameter));
        item = parameter.Items[0];
        foreach (bool useAi in new[] { true, false })
        foreach (bool isOk in new[] { true, false })
        {
            INodeResult sourceResult = useAi
                ? (INodeResult)new NodeResultTDAI { JudgeOk = isOk }
                : new NodeResultMultiCondition { ConditionResult = isOk };
            typeof(NodeBase).GetProperty("Result").SetValue(source, sourceResult);
            item.SourceText2 = useAi ? "判定OK" : "条件结果";
            var display = (AlgorithmResult)build.Invoke(null, new object[] { owner, parameter, null });
            Check(display.Texts.Count == 2 && display.Texts[1].Text == "独立内容", "布尔文本不能影响独立固定内容");
            var text = display.Texts[0];
            Check(text.Text == (isOk ? "检测通过" : "请检查产品"), "真假应显示对应自定义内容");
            Check(text.Color.ToArgb() == (isOk ? Color.Blue : Color.Orange).ToArgb(), "应使用顶部OK/NG颜色");
            Check(display.IsAllOk == isOk && text.FontSize == 28 && text.Position == DisplayTextPosition.TopRight
                && text.CoordinateMode == DisplayTextCoordinateMode.Control, "布尔判定和文本位置字号必须保留");
        }
        item.Enabled = false;
        var disabled = (AlgorithmResult)build.Invoke(null, new object[] { owner, parameter, null });
        Check(disabled.Texts.Count == 1 && disabled.Texts[0].Text == "独立内容", "禁用布尔项应仅保留独立内容");
        item.Enabled = true;
        parameter.Items.Add(new ResultOverlayDrawItem { ManualText = "普通文本", TextCoordinateMode = DisplayTextCoordinateMode.Image });
        foreach (DisplayTextCoordinateMode mode in new[] { DisplayTextCoordinateMode.Image, DisplayTextCoordinateMode.Control })
        {
            parameter.TextCoordinateMode = mode;
            var unified = (AlgorithmResult)build.Invoke(null, new object[] { owner, parameter, null });
            Check(unified.Texts.Count == 3, "普通、布尔和独立固定内容必须同时保留");
            foreach (ColorText entry in unified.Texts)
                Check(entry.CoordinateMode == mode, "全部文本必须服从节点级统一坐标系");
        }
        parameter.Items.RemoveAt(1);
        item.SourceText2 = "RunTime";
        try
        {
            build.Invoke(null, new object[] { owner, parameter, null });
            throw new Exception("非布尔类型不能被隐式转换成OK/NG");
        }
        catch (TargetInvocationException error)
        {
            Check(error.InnerException is InvalidOperationException, "类型错误必须有明确提示");
        }
        item.SourceText2 = "不存在的结果";
        var missing = (AlgorithmResult)build.Invoke(null, new object[] { owner, parameter, null });
        Check(!missing.IsAllOk && missing.Texts[0].Text != "检测通过", "缺失订阅不能误显示OK");
        Console.WriteLine("AI与多条件布尔文本映射、颜色、保存、禁用和错误类型回归通过。");
    }

    /// <summary>使用真实绘制器生成指定缩放和平移下的显示位图。</summary>
    private static Bitmap Render(ShowImageControl.RoiTextBlock roi, float scale, PointF offset)
    {
        var bitmap = new Bitmap(320, 240);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Navy);
            roi.Draw(graphics, scale, offset);
        }
        return bitmap;
    }
}
