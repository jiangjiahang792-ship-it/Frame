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
using TDJS_Vision;
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.ContourMatch;
using TDJS_Vision.Node._3_Detection.MatchTemplate;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;

/// <summary>真实DLL、节点运行、方案恢复和Designer交互验证，不连接现场设备。</summary>
internal static class ContourMatchTests
{
    /// <summary>用于访问独立验证入口的实例反射标志。</summary>
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    /// <summary>累计通过的断言数量。</summary>
    private static int _checks;

    /// <summary>验证条件，不满足时立即以非零退出。</summary>
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); _checks++; }
    /// <summary>读取真实窗体字段。</summary>
    private static T Field<T>(object value, string name) { return (T)value.GetType().GetField(name, Flags).GetValue(value); }
    /// <summary>通过真实结果属性替换入口模拟上游运行并保留资源释放语义。</summary>
    private static void SetResult(NodeBase node, INodeResult result) { typeof(NodeBase).GetProperty("Result").SetValue(node,result); }
    /// <summary>调用真实实例方法。</summary>
    private static object Call(object value, string name, params object[] args) { return value.GetType().GetMethod(name, Flags).Invoke(value, args); }
    /// <summary>执行界面事件入口。</summary>
    private static void Click(object value, string name) { Call(value, name, null, EventArgs.Empty); }

    /// <summary>创建与Demo合成样本相同的非对称轮廓。</summary>
    private static Mat Template()
    {
        var image = new Mat(88, 96, MatType.CV_8UC3, Scalar.All(20));
        int[,] rectangles = { {14,14,62,5,230}, {14,14,5,58,230}, {14,67,45,5,230}, {54,41,5,31,230},
            {54,37,25,5,230}, {74,37,5,18,230}, {30,30,16,12,150}, {34,46,8,13,245} };
        for (int i = 0; i < rectangles.GetLength(0); i++)
            Cv2.Rectangle(image, new Rect(rectangles[i,0],rectangles[i,1],rectangles[i,2],rectangles[i,3]), Scalar.All(rectangles[i,4]), -1);
        return image;
    }

    /// <summary>在已知位置放置模板，得到可解析验证的搜索图。</summary>
    private static Mat Search(Mat template)
    {
        var image = new Mat(190, 260, MatType.CV_8UC3, Scalar.All(20));
        using (var patch = new Mat(image, new Rect(104, 58, template.Width, template.Height))) template.CopyTo(patch);
        return image;
    }

    /// <summary>原生算法测试，覆盖所有模式、灰度/彩图、角度及非连续输入。</summary>
    private static NodeParamContourMatch NativeChecks(Mat template, Mat search)
    {
        var create = new CreateModelOptions { AutoContrast = true };
        var find = new FindOptions { MaximumMatches = 5 };
        using (var matcher = new NativeShapeMatcher())
        {
            Check(!matcher.HasModel, "未建模实例状态错误。");
            matcher.CreateModel(template, new DrawingRectangle(0, 0, 96, 88), create);
            Check(matcher.HasModel && matcher.ModelContrast > 0, "自动阈值建模失败。");
            Check(matcher.GetModelFeatures().Count >= 8 && matcher.GetModelContours().Count > 0, "真实特征或完整轮廓未接通。");
            foreach (ShapeMatchMode mode in Enum.GetValues(typeof(ShapeMatchMode)))
            {
                find.Mode = mode;
                var result = matcher.Find(search, find);
                Check(result.Count == 1, "Demo模式未准确找到单目标：" + mode);
                Check(Math.Abs(result[0].CenterX - 152) < 1.5 && Math.Abs(result[0].CenterY - 102) < 1.5, "中心坐标偏差过大。");
                Check(result[0].Score >= find.MinimumScore && result[0].Score <= 1 && Math.Abs(result[0].AngleDegrees) < 2,
                    string.Format("分数或角度语义改变：模式={0}，分数={1:F4}，角度={2:F3}。", mode, result[0].Score, result[0].AngleDegrees));
            }
            using (var gray = new Mat())
            using (var bgra = new Mat())
            using (var parent = new Mat(230, 340, MatType.CV_8UC3, Scalar.All(20)))
            using (var view = new Mat(parent, new Rect(11, 9, search.Width, search.Height)))
            {
                Cv2.CvtColor(search, gray, ColorConversionCodes.BGR2GRAY);
                Cv2.CvtColor(search, bgra, ColorConversionCodes.BGR2BGRA);
                Check(matcher.Find(gray, find).Count == 1 && matcher.Find(bgra, find).Count == 1, "灰度或四通道输入失败。");
                search.CopyTo(view);
                Check(!view.IsContinuous() && matcher.Find(view, find).Count == 1, "未按Mat真实步长处理ROI图像。");
            }
            using (var blank = new Mat(search.Size(), MatType.CV_8UC3, Scalar.All(20)))
                Check(matcher.Find(blank, find).Count == 0, "无目标图产生错误匹配。");
            using (var multi = new Mat(220, 540, MatType.CV_8UC3, Scalar.All(20)))
            {
                using (var first = new Mat(multi, new Rect(104,58,96,88))) template.CopyTo(first);
                using (var second = new Mat(multi, new Rect(350,90,96,88))) template.CopyTo(second);
                Check(matcher.Find(multi, find).Count == 2, "多目标数量不正确。");
            }
            using (var rotation = Cv2.GetRotationMatrix2D(new Point2f(152, 102), 15, 1))
            using (var rotated = new Mat())
            {
                Cv2.WarpAffine(search, rotated, rotation, search.Size(), InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(20));
                var result = matcher.Find(rotated, find);
                Check(result.Count == 1 && Math.Abs(result[0].AngleDegrees + 15) < 2, "旋转搜索或顺时针角度约定不一致。");
            }
            int before = matcher.GetModelFeatures().Count;
            byte[] erase = new byte[96 * 88];
            for (int y = 24; y < 61; y++) for (int x = 26; x < 49; x++) erase[y * 96 + x] = 255;
            matcher.EraseModelFeatures(erase, 96, 88);
            int after = matcher.GetModelFeatures().Count;
            Check(after >= 8 && after < before, "涂抹没有实际删除特征。");
            Check(matcher.Find(search, find).Count == 1, "删除轮廓后模型不可用。");
            bool rejected = false;
            try { matcher.EraseModelFeatures(Enumerable.Repeat((byte)255, 96 * 88).ToArray(), 96, 88); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected && matcher.GetModelFeatures().Count == after, "全删除失败未保留原模型。");
            create.Contrast = matcher.ModelContrast;
            return new NodeParamContourMatch { Text1 = "1.输入图像", Text2 = "输出图像", SourceNodeId = 1,
                ModelImageBytes = template.ToBytes(".png"), ModelRoi = new DrawingRectangle(0,0,96,88),
                ModelOptions = create, CreateOptions = new CreateModelOptions { AutoContrast = false, FeatureCount = 10000, MinimumContrast = 0 },
                FindOptions = new FindOptions { Mode = ShapeMatchMode.高精度 }, EraseMasks = new List<byte[]> { erase } };
        }
    }

    /// <summary>验证手动阈值、忽略极性和ROI原图偏移。</summary>
    private static void ManualModel(Mat template, Mat search)
    {
        using (var matcher = new NativeShapeMatcher())
        using (var parent = new Mat(160, 180, MatType.CV_8UC3, Scalar.All(20)))
        {
            using (var roi = new Mat(parent, new Rect(30,25,96,88))) template.CopyTo(roi);
            var options = new CreateModelOptions { AutoContrast = false, Contrast = 20, MinimumContrast = 0, Metric = ShapeMetric.忽略极性, FeatureCount = 10000, PyramidLevels = 1 };
            matcher.CreateModel(parent, new DrawingRectangle(30,25,96,88), options);
            Check(Math.Abs(matcher.ModelContrast - 20) < 0.001, "手动阈值未保留。");
            var found = matcher.Find(search, new FindOptions { PyramidLevels = 1, SubPixel = false });
            Check(found.Count == 1 && Math.Abs(found[0].CenterX - 152) < 2, "来源ROI偏移污染了搜索坐标。");
        }
    }

    /// <summary>验证方案持久化、节点工厂、租约、多目标订阅及模型复用。</summary>
    private static void NodeChecks(NodeParamContourMatch parameters, Mat search)
    {
        var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
        string json = JsonConvert.SerializeObject(new INodeParam[] { parameters }, settings);
        var restored = (NodeParamContourMatch)JsonConvert.DeserializeObject<INodeParam[]>(json, settings)[0];
        Check(restored.ModelOptions.AutoContrast && !restored.CreateOptions.AutoContrast && restored.EraseMasks.Count == 1, "方案未区分已建模参数和下次创建参数。");
        var process = new TDJS_Vision.Process("轮廓匹配离线验证");
        var source = new NodeBase(1, "输入图像", process, NodeType.ImageSource);
        var contour = (NodeContourMatch)NodeFactory.CreateNode(2, "轮廓模板匹配", process, NodeType.ContourMatch);
        var form = (NodeParamFormContourMatch)contour.ParamForm;
        TDJS_Vision.ResourceManagement.CpuWorkloadKind workload;
        Check(new TDJS_Vision.ResourceManagement.DefaultCpuWorkloadClassifier().TryClassify(contour, out workload), "轮廓匹配未进入CPU算法调度。");
        var pasted = restored.Copy();
        var editorType = typeof(NodeBase).Assembly.GetTypes().Single(type => type.Name == "ProcessEditPanel");
        editorType.GetMethod("RemapInternalNodeReferences", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
            new object[] { pasted, new Dictionary<string,string> { { "1.输入图像", "11.复制图像" } }, new Dictionary<int,int> { { 1, 11 } }, null, null, 0 });
        Check(pasted.SourceNodeId == 11 && pasted.Text1 == "11.复制图像", "复制粘贴节点后图像订阅未随节点编号重映射。");
        var owned = search.Clone();
        var owner = new OutputImage { SrcImg = owned, Bitmaps = new List<Mat> { owned } }; owner.TakeOwnership(owned);
        SetResult(source, new NodeResultImageSource { OutputImage = owner });
        process.Nodes.Add(source); process.Nodes.Add(contour);
        process.Connections.Add(new ProcessConnection { FromNodeId = 1, ToNodeId = 2 });
        form.SetNodeBelong(contour); form.Params = restored; form.SetParam2Form();
        try
        {
            restored.FindOptions.MinimumScore = 1;
            Check(((NodeParamContourMatch)form.Params).FindOptions.MinimumScore == 0.65, "保存后参数被调用方引用修改。");
            var exported = (NodeParamContourMatch)form.Params; exported.ModelImageBytes[0] = 0;
            Check(((NodeParamContourMatch)form.Params).ModelImageBytes[0] != 0, "参数获取泄漏内部图像数组。");
            // 不保存方案、不关闭窗口，修改运行参数及图像订阅必须直接生效。
            Field<NumericUpDown>(form, "minimumScoreNumeric").Value = 0.66M;
            Check(((NodeParamContourMatch)form.Params).FindOptions.MinimumScore == 0.66, "运行参数未即时生效。");
            var subscription = Field<NodeSubscription>(form, "imageSubscription");
            subscription.ClearText();
            bool missingInput = false;
            try { contour.Run(CancellationToken.None, false).GetAwaiter().GetResult(); } catch (InvalidOperationException) { missingInput = true; }
            Check(missingInput && ((NodeParamContourMatch)form.Params).SourceNodeId == -1, "清空订阅后仍使用旧输入。");
            subscription.SetText("1.输入图像", parameters.Text2);
            Check(((NodeParamContourMatch)form.Params).SourceNodeId == 1, "重新选择图像订阅未即时生效。");
            contour.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            var result = (NodeResultContourMatch)contour.Result;
            Check(result.IsOk && result.MatchCount == 1 && result.Score >= 0.65 && result.Score <= 1, "真实节点运行结果不正确。");
            Check(result.OutputImage.Rectangles.Count == result.MatchCount && result.OutputImage.Rectangles[0].Contains(152, 102), "下游裁剪矩形未包含匹配中心。");
            Check(result.Poses.Count == 1 && result.Result.Contours.Count > 0 && ReferenceEquals(result.OutputImage.SrcImg, owned), "位姿、轮廓或零复制输出未接通。");
            object coordinate;
            Check(result.TryGetDynamicVariable("目标1中心X", out coordinate) && Math.Abs((double)coordinate - 152) < 1.5, "动态坐标订阅不可用。");
            var session = Field<object>(form, "_runtimeSession"); var matcher = Field<object>(session, "_models");
            contour.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            Check(ReferenceEquals(matcher, Field<object>(session, "_models")), "每帧重复创建了原生模型。");
            var correctionNode = new NodeBase(3,"定位",process,NodeType.PositionCorrection);
            process.Nodes.Add(correctionNode); process.Connections.Add(new ProcessConnection { FromNodeId=3,ToNodeId=2 });
            var originalParameters=(NodeParamContourMatch)form.Params;
            try
            {
                var info=new TDJS_Vision.Node._4_Measurement.Common.PositionCorrectionInfo { IsValid=true, CurrentX=84, CurrentY=38 };
                SetResult(correctionNode,new TDJS_Vision.Node._4_Measurement.PositionCorrection.NodeResultPositionCorrection { CorrectionInfo=info });
                var corrected=originalParameters.Copy(); corrected.AllSearch=false; corrected.SearchRegion=new DrawingRectangle(0,0,130,125);
                corrected.UsePositionCorrection=true; corrected.CorrectionText1="3.定位"; corrected.CorrectionText2="位置修正信息";
                form.Params=corrected; form.SetParam2Form();
                var correctionSubscription=Field<NodeSubscription>(form,"correctionSubscription");
                correctionSubscription.ClearText();
                Check(string.IsNullOrEmpty(((NodeParamContourMatch)form.Params).CorrectionText1),"位置修正订阅清空后未即时生效。");
                correctionSubscription.SetText("3.定位","位置修正信息");
                Check(((NodeParamContourMatch)form.Params).CorrectionText1=="3.定位","位置修正订阅选择未即时生效。");
                contour.Run(CancellationToken.None,false).GetAwaiter().GetResult();
                Check(((NodeResultContourMatch)contour.Result).MatchCount==1,"节点未从上游订阅读取位置修正。");
                info.IsValid=false; bool failedCorrection=false;
                try { contour.Run(CancellationToken.None,false).GetAwaiter().GetResult(); } catch(InvalidOperationException) { failedCorrection=true; }
                Check(failedCorrection && ((NodeResultContourMatch)contour.Result).MatchCount==0,"修正输入失效后仍返回历史结果。");
            }
            finally { form.Params=originalParameters; correctionNode.Dispose(); }
            var token = new CancellationToken(true); bool cancelled = false;
            try { contour.Run(token, false).GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled && ((NodeResultContourMatch)contour.Result).MatchCount == 0, "取消后仍保留旧匹配结果。");
            contour.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            var leasedResult = (NodeResultContourMatch)contour.Result;
            owner.Dispose();
            Check(!owned.IsDisposed && leasedResult.OutputImage.SrcImg.At<Vec3b>(0,0).Item0 == 20, "上游释放时没有保护下游租约。");
            SetResult(contour, new NodeResultContourMatch());
            Check(owned.IsDisposed, "下游释放后上游图像租约未回收。");
            SetResult(source, new NodeResultImageSource());
            bool failed = false; try { contour.Run(CancellationToken.None, false).GetAwaiter().GetResult(); } catch (InvalidOperationException) { failed = true; }
            Check(failed && ((NodeResultContourMatch)contour.Result).MatchCount == 0, "输入失败后错误复用旧结果。");
        }
        finally { form.Dispose(); contour.Dispose(); SetResult(source, new NodeResultImageSource()); source.Dispose(); owner.Dispose(); }
    }

    /// <summary>等待真实WinForms后台操作完成，保留消息泵并设置超时。</summary>
    private static void WaitEditor(ContourTemplateEditor form)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        do { Application.DoEvents(); Thread.Sleep(5); if (timeout.Elapsed.TotalSeconds > 30) throw new TimeoutException("编辑操作超时。"); }
        while (Field<bool>(form, "_busy"));
    }

    /// <summary>将图像坐标映射到真实画布客户区。</summary>
    private static DrawingPoint CanvasPoint(ImageCanvas canvas, double x, double y) { return DrawingPoint.Round((PointF)Call(canvas, "ClientPoint", x, y)); }
    /// <summary>投递鼠标事件给真实画布，验证左键和右键语义。</summary>
    private static void Mouse(ImageCanvas canvas, string method, DrawingPoint point, MouseButtons button)
    {
        Call(canvas, method, new MouseEventArgs(button, 1, point.X, point.Y, 0));
    }

    /// <summary>验证缩放锚点、平移与涂抹坐标，并检查导航不会连出意外删除笔画。</summary>
    private static void CanvasNavigationChecks(Mat template)
    {
        using (var frame = ImageFrame.CopyFrom(template))
        using (var host = new Form { Size = new System.Drawing.Size(500, 430), StartPosition = FormStartPosition.Manual, Location = new DrawingPoint(-3000, -3000) })
        using (var canvas = new ImageCanvas { Dock = DockStyle.Fill, BrushSize = 4 })
        {
            host.Controls.Add(canvas); host.Show(); Application.DoEvents(); canvas.SetImage(frame);
            canvas.SetModel(new DrawingRectangle(10, 10, 70, 65), new[] { new PointF(20, 20), new PointF(45, 30) });
            canvas.BeginErase();
            var first = CanvasPoint(canvas, 30.5, 30.5);
            Mouse(canvas, "OnMouseDown", first, MouseButtons.Left); Mouse(canvas, "OnMouseUp", first, MouseButtons.Left);
            byte[] original = canvas.CopyEraseMask();
            Check(original[20 * 70 + 20] != 0 && canvas.PendingFeatureCount == 1, "基准笔画未落在ROI局部像素。");
            var before = (RectangleF)Call(canvas, "ImageBounds");
            double x = (first.X - before.X) * frame.Width / before.Width;
            double y = (first.Y - before.Y) * frame.Height / before.Height;
            Call(canvas, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, first.X, first.Y, 240));
            var anchorAfter = (PointF)Call(canvas, "ClientPoint", x, y);
            Check(Math.Abs(anchorAfter.X - first.X) < 0.01 && Math.Abs(anchorAfter.Y - first.Y) < 0.01, "滚轮缩放没有固定鼠标下的图像点。");
            Check(canvas.ImageScale > before.Width / frame.Width && original.SequenceEqual(canvas.CopyEraseMask()), "缩放误改了原图涂抹遮罩。");
            var oldClient = CanvasPoint(canvas, 30.5, 30.5);
            Mouse(canvas, "OnMouseDown", first, MouseButtons.Middle);
            Mouse(canvas, "OnMouseMove", new DrawingPoint(first.X + 36, first.Y + 22), MouseButtons.Middle);
            Mouse(canvas, "OnMouseUp", new DrawingPoint(first.X + 36, first.Y + 22), MouseButtons.Middle);
            var movedClient = CanvasPoint(canvas, 30.5, 30.5);
            Check(movedClient.X - oldClient.X == 36 && movedClient.Y - oldClient.Y == 22 && original.SequenceEqual(canvas.CopyEraseMask()), "中键平移误涂或移动距离不正确。");
            Mouse(canvas, "OnMouseDown", movedClient, MouseButtons.Left); Mouse(canvas, "OnMouseUp", movedClient, MouseButtons.Left);
            Check(original.SequenceEqual(canvas.CopyEraseMask()), "放大平移后同一笔画的直径或位置改变。");
            Mouse(canvas, "OnMouseDown", movedClient, MouseButtons.Left);
            Call(canvas, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, movedClient.X, movedClient.Y, 120));
            Mouse(canvas, "OnMouseMove", CanvasPoint(canvas, 55.5, 40.5), MouseButtons.Left);
            Mouse(canvas, "OnMouseUp", CanvasPoint(canvas, 55.5, 40.5), MouseButtons.Left);
            Check(original.SequenceEqual(canvas.CopyEraseMask()), "笔画中缩放后产生跨视口删除线。");
            canvas.ResetView();
            Check(canvas.EditorMode == CanvasEditorMode.涂抹 && original.SequenceEqual(canvas.CopyEraseMask()), "适应画布清除了未确认的涂抹。");
            Mouse(canvas, "OnMouseDown", CanvasPoint(canvas, 55.5, 40.5), MouseButtons.Left);
            Mouse(canvas, "OnMouseUp", CanvasPoint(canvas, 55.5, 40.5), MouseButtons.Left);
            Check(canvas.PendingFeatureCount == 2, "恢复适应后不能继续涂抹。");
            canvas.ZoomAt(first, 1E100); canvas.ZoomAt(first, 1E-100);
            Check(canvas.ImageScale > 0 && !double.IsInfinity(canvas.ImageScale), "缩放极值没有限制。");
            canvas.SetImage(null); canvas.ResetView();
            Check(canvas.ImageScale == 0 && canvas.CopyEraseMask().Length == 0, "清空图像未清理视图和临时遮罩。");
            host.Close();
        }
    }

    /// <summary>验证Designer参数、框选/涂抹、保存隔离、重新打开及窗口缩放。</summary>
    private static void EditorChecks(Mat template, string artifactDirectory)
    {
        using (var form = new ContourTemplateEditor())
        {
            form.StartPosition = FormStartPosition.Manual; form.Location = new DrawingPoint(-3000,-3000);
            form.Show(); Application.DoEvents();
            var image = ImageFrame.CopyFrom(template); form.GetType().GetField("_image",Flags).SetValue(form,image);
            var canvas = Field<ImageCanvas>(form, "imageCanvas"); canvas.SetImage(image);
            Click(form, "CreateModelButton_Click");
            Mouse(canvas,"OnMouseDown",CanvasPoint(canvas,0.5,0.5),MouseButtons.Left);
            Mouse(canvas,"OnMouseUp",CanvasPoint(canvas,95,87),MouseButtons.Left);
            Check(!Field<IShapeMatcher>(form,"_matcher").HasModel, "左键松开错误地自动提交建模。");
            Check(canvas.RegionCount == 1 && Field<Button>(form,"saveButton").Enabled, "松开后区域未生成或不能确定组合建模。");
            Click(form,"CreateModelButton_Click"); WaitEditor(form);
            var matcher = Field<IShapeMatcher>(form,"_matcher"); Check(matcher.HasModel, "创建按钮未建立真实模板：" + Field<Label>(form,"statusLabel").Text);
            int before = matcher.GetModelFeatures().Count;
            PointF feature = matcher.GetModelFeatures().First(point => point.X > 27 && point.X < 48 && point.Y > 25 && point.Y < 59);
            var zoomAnchor = CanvasPoint(canvas, feature.X, feature.Y);
            Call(canvas,"OnMouseWheel",new MouseEventArgs(MouseButtons.None,0,zoomAnchor.X,zoomAnchor.Y,240));
            double enlargedScale = canvas.ImageScale;
            Click(form, "EraseButton_Click");
            Check(Math.Abs(canvas.ImageScale - enlargedScale) < 0.001, "进入涂抹时丢失放大位置。");
            Check(canvas.Enabled && Field<Button>(form,"zoomInButton").Enabled && Field<Button>(form,"fitImageButton").Enabled, "涂抹期间画布或缩放按钮不可用。");
            Mouse(canvas,"OnMouseDown",CanvasPoint(canvas,feature.X,feature.Y),MouseButtons.Left);
            Mouse(canvas,"OnMouseUp",CanvasPoint(canvas,feature.X,feature.Y),MouseButtons.Left);
            Check(canvas.PendingFeatureCount > 0 && matcher.GetModelFeatures().Count == before, "涂抹没有预览或未确认就修改模型。");
            using (var bitmap = new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new DrawingRectangle(0,0,bitmap.Width,bitmap.Height)); bitmap.Save(System.IO.Path.Combine(artifactDirectory,"轮廓模板匹配-放大涂抹.png")); }
            Mouse(canvas,"OnMouseDown",CanvasPoint(canvas,feature.X,feature.Y),MouseButtons.Right); WaitEditor(form);
            Check(matcher.GetModelFeatures().Count < before, "右键没有提交特征删除。");
            Check(Math.Abs(canvas.ImageScale - enlargedScale) < 0.001, "确认删除后丢失放大位置。");
            Click(form,"FitImageButton_Click");
            Check(canvas.ImageScale < enlargedScale, "适应按钮没有恢复整图显示。");
            Call(form,"SaveParameters");
            var saved = (NodeParamContourMatch)form.Params;
            Check(saved.CreateOptions.AngleStartDegrees == -30 && saved.ModelOptions.AngleStartDegrees == -30 && saved.ModelRegions.Count == 1, "已确认模型参数或可编辑区域未正确保存。");
            Check(saved.EraseMasks.Count == 1 && saved.ModelImageBytes.Length > 0, "模型及删除记录未保存。");
            var isolated = (NodeParamContourMatch)form.Params; isolated.FindOptions.MinimumScore = 0.9;
            Check(((NodeParamContourMatch)form.Params).FindOptions.MinimumScore == 0.65, "UI参数副本不独立。");
            int savedFeatureCount = matcher.GetModelFeatures().Count;
            form.SetParam2Form();
            var restore = (Task)Call(form,"RestoreEditorAsync"); WaitEditor(form); restore.GetAwaiter().GetResult();
            Check(Field<IShapeMatcher>(form,"_matcher").GetModelFeatures().Count == savedFeatureCount, "重新打开模型后涂抹特征不一致。");
            // 模板参数控件均保留在Designer，且保持Demo原始范围。
            Check(Field<NumericUpDown>(form,"featureCountNumeric").Maximum == 10000, "Demo参数范围被改变。");
            Check(Field<NumericUpDown>(form,"minimumContrastNumeric").Minimum == 0, "创建参数不完整。");
            form.Size = new System.Drawing.Size(860,560); form.PerformLayout(); Application.DoEvents(); Thread.Sleep(60); Application.DoEvents();
            using (var bitmap = new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new DrawingRectangle(0,0,bitmap.Width,bitmap.Height)); bitmap.Save(System.IO.Path.Combine(artifactDirectory,"轮廓模板匹配-参数.png")); }
            form.Size = new System.Drawing.Size(800,540); form.PerformLayout(); Application.DoEvents(); Thread.Sleep(60); Application.DoEvents(); Field<TableLayoutPanel>(form,"rootLayout").PerformLayout();
            Console.WriteLine("布局检查：窗体={0}，根布局={1}，画布={2}", form.ClientSize, Field<TableLayoutPanel>(form,"rootLayout").Bounds, canvas.Bounds);
            Check(Field<TableLayoutPanel>(form,"rootLayout").Right <= form.ClientSize.Width && Field<TableLayoutPanel>(form,"rootLayout").Bottom <= form.ClientSize.Height, "窗口缩放后根布局超出客户区。");
            Check(canvas.ClientSize.Width > 400 && canvas.ClientSize.Height > 350 && canvas.Right <= Field<TableLayoutPanel>(form,"rootLayout").Width, "窗口缩放后画布布局异常。");
            using (var bitmap = new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new DrawingRectangle(0,0,bitmap.Width,bitmap.Height)); bitmap.Save(System.IO.Path.Combine(artifactDirectory,"轮廓模板匹配-小窗口.png")); }
            form.Close();
        }
    }

    /// <summary>生成不携带节点订阅的独立模板定义。</summary>
    private static ContourTemplateDefinition Definition(NodeParamContourMatch model, string name)
    {
        var copy = model.Copy(); copy.Text1 = null; copy.Text2 = null; copy.SourceNodeId = -1; copy.Templates = null;
        return new ContourTemplateDefinition { Name = name, Model = copy };
    }
    /// <summary>从内部生产会话取得真实匹配结果。</summary>
    private static IReadOnlyList<ShapeMatchResult> SearchSession(object session, Mat image, NodeParamContourMatch parameters, IReadOnlyList<TDJS_Vision.Node._4_Measurement.Common.PositionCorrectionInfo> corrections = null)
    {
        object result = Call(session, "Execute", image, parameters, CancellationToken.None, corrections);
        return (IReadOnlyList<ShapeMatchResult>)result.GetType().GetProperty("Matches", Flags).GetValue(result);
    }
    /// <summary>验证多模板启停、搜索区域、位置修正、多目标去重及交换文件。</summary>
    private static void MultiTemplateChecks(NodeParamContourMatch model, Mat template, string artifactDirectory)
    {
        using (var other = new Mat(88, 96, MatType.CV_8UC3, Scalar.All(20)))
        using (var scene = new Mat(240, 420, MatType.CV_8UC3, Scalar.All(20)))
        {
            Cv2.Circle(other, new OpenCvSharp.Point(45, 42), 29, Scalar.All(235), 5);
            Cv2.Line(other, new OpenCvSharp.Point(18,42), new OpenCvSharp.Point(70,42), Scalar.All(235), 5);
            Cv2.Rectangle(other, new Rect(55,17,12,18), Scalar.All(180), -1);
            using (var target = new Mat(scene,new Rect(50,70,96,88))) template.CopyTo(target);
            using (var target = new Mat(scene,new Rect(260,80,96,88))) other.CopyTo(target);
            var second = model.Copy(); second.ModelImageBytes = other.ToBytes(".png"); second.EraseMasks.Clear();
            var parameters = new NodeParamContourMatch { Templates = new List<ContourTemplateDefinition> { Definition(model,"折线模板"), Definition(second,"圆形模板") }, FindOptions = new FindOptions { MaximumMatches = 5 } };
            var sessionType = typeof(NodeContourMatch).Assembly.GetType("TDJS_Vision.Node._3_Detection.ContourMatch.ContourMatchSession");
            using (var session = (IDisposable)Activator.CreateInstance(sessionType, Flags, null, new object[] { null }, null))
            {
                var matches = SearchSession(session,scene,parameters);
                Check(matches.Count == 2 && matches.Select(item=>item.TemplateName).Distinct().Count() == 2, "多模板未返回各自目标及名称。");
                Check(matches.Any(item=>item.TemplateName=="圆形模板" && Math.Abs(item.CenterX-308)<2), "第二模板的全图坐标错误。");
                var disabled = parameters.Copy(); disabled.Templates[1].Enabled = false;
                Check(SearchSession(session,scene,disabled).All(item=>item.TemplateName=="折线模板"), "停用模板仍然参与搜索。");
                var region = parameters.Copy(); region.AllSearch = false; region.SearchRegion = new DrawingRectangle(30,50,130,125);
                matches = SearchSession(session,scene,region);
                Check(matches.Count == 1 && matches[0].TemplateName=="折线模板" && Math.Abs(matches[0].CenterX-98)<2, "搜索区域限制或ROI坐标回填错误。");
                region = region.Copy(); region.UsePositionCorrection = true;
                var correction = new TDJS_Vision.Node._4_Measurement.Common.PositionCorrectionInfo { IsValid=true, CurrentX=210, CurrentY=10 };
                matches = SearchSession(session,scene,region,new[]{correction});
                Check(matches.Count == 1 && matches[0].TemplateName=="圆形模板", "位置修正没有移动实际搜索区域。");
                bool rejected = false;
                try { SearchSession(session,scene,region,new[]{new TDJS_Vision.Node._4_Measurement.Common.PositionCorrectionInfo()}); } catch(TargetInvocationException) { rejected=true; }
                Check(rejected, "无效位置修正被当作有效区域执行。");
                var limit = parameters.Copy(); limit.FindOptions.MaximumMatches=1;
                Check(SearchSession(session,scene,limit).Count==1,"最大结果数没有在模板合并后统一生效。");
                var duplicate = parameters.Copy(); duplicate.Templates.Add(Definition(model,"重复折线"));
                Check(SearchSession(session,scene,duplicate).Count==2,"不同模板对同一目标的结果未按重叠率去重。");
                var cleared = parameters.Copy(); cleared.Templates.Clear(); cleared.ModelImageBytes=model.ModelImageBytes; cleared.ModelOptions=model.ModelOptions;
                rejected=false; try { SearchSession(session,scene,cleared); } catch(TargetInvocationException) { rejected=true; }
                Check(rejected,"删除全部模板后旧单模板字段错误复活。");
                var transform = new TDJS_Vision.Node._4_Measurement.Common.PositionCorrectionInfo { IsValid=true, CurrentAngle=90, CurrentX=240 };
                var polygons=(List<Point2f[]>)sessionType.GetMethod("GetSearchRegions", BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{scene,region,new[]{transform}});
                Check(Math.Abs(polygons[0][0].X-190)<0.01 && Math.Abs(polygons[0][0].Y-30)<0.01,"位置修正旋转未作用于搜索区域角点。");
            }
            var package = new ContourTemplatePackage { Templates=parameters.Templates };
            var decoded=ContourTemplatePackage.Decode(package.Encode());
            Check(decoded.Templates.Count==2 && decoded.Templates[0].Id!=parameters.Templates[0].Id && decoded.Templates[0].Model.EraseMasks.Count==model.EraseMasks.Count,"模板交换未恢复模型或未分配独立标识。");
            var persisted=JsonConvert.DeserializeObject<NodeParamContourMatch>(JsonConvert.SerializeObject(parameters));
            Check(persisted.Templates.Count==2 && persisted.Templates[0].Model.ModelImageBytes.Length>0,"多模板方案未完整持久化。");
            ManagerChecks(parameters,scene,artifactDirectory);
        }
    }
    /// <summary>检查真实三页UI、模板启用、区域确认、即时生效和模板删除。</summary>
    private static void ManagerChecks(NodeParamContourMatch parameters, Mat scene, string artifactDirectory)
    {
        using (var form=new NodeParamFormContourMatch())
        {
            form.Params=parameters; form.StartPosition=FormStartPosition.Manual; form.Location=new DrawingPoint(-3000,-3000);
            form.Show(); Application.DoEvents(); Call(form,"SetImage",ImageFrame.CopyFrom(scene));
            var tabs=Field<TabControl>(form,"parameterTabs");
            Check(tabs.TabPages.Cast<TabPage>().Select(page=>page.Text).SequenceEqual(new[]{"基础参数","特征模板","运行参数"}),"页面未正确拆分。");
            Check(Field<FlowLayoutPanel>(form,"footerActions").Controls.Cast<Control>().Select(control=>control.Text).SequenceEqual(new[]{"确定","执行"}), "底部未仅保留执行和确定。");
            Field<NumericUpDown>(form,"maximumMatchesNumeric").Value=3;
            Field<NumericUpDown>(form,"maximumOverlapNumeric").Value=0.2M;
            Field<NumericUpDown>(form,"findLevelsNumeric").Value=1;
            Field<ComboBox>(form,"modeComboBox").SelectedIndex=0;
            Field<CheckBox>(form,"subPixelCheckBox").Checked=false;
            var live=(NodeParamContourMatch)form.Params;
            Check(live.FindOptions.MaximumMatches==3 && live.FindOptions.MaximumOverlap==0.2 && live.FindOptions.PyramidLevels==1 && (int)live.FindOptions.Mode==0 && !live.FindOptions.SubPixel,"运行控件修改未全部即时生效。");
            form.Params=parameters; form.SetParam2Form();
            var grid=Field<DataGridView>(form,"templatesGrid"); Check(grid.Rows.Count==2,"模板列表没有恢复两个模板。");
            grid.Rows[0].Cells[1].Value="即时重命名";
            Check(((NodeParamContourMatch)form.Params).Templates[0].Name=="即时重命名", "模板重命名未即时生效。");
            grid.Rows[1].Cells[0].Value=false; Application.DoEvents();
            var draft=(NodeParamContourMatch)Call(form,"ReadDraft");
            Check(!draft.Templates[1].Enabled && !((NodeParamContourMatch)form.Params).Templates[1].Enabled,"模板启用修改未即时生效。");
            tabs.SelectedIndex=0; Click(form,"DrawRegionButton_Click"); var canvas=Field<ImageCanvas>(form,"imageCanvas");
            Mouse(canvas,"OnMouseDown",CanvasPoint(canvas,30,50),MouseButtons.Left); Mouse(canvas,"OnMouseUp",CanvasPoint(canvas,160,175),MouseButtons.Left);
            Check(!Field<Button>(form,"executeButton").Enabled,"区域尚未确认时允许执行。");
            Mouse(canvas,"OnMouseDown",CanvasPoint(canvas,90,90),MouseButtons.Right);
            draft=(NodeParamContourMatch)Call(form,"ReadDraft");
            Check(!draft.AllSearch && Math.Abs(draft.SearchRegion.X-30)<=1,"基础页右键未确认搜索区域。");
            Check(((NodeParamContourMatch)form.Params).Templates.Count==2 && !((NodeParamContourMatch)form.Params).AllSearch,"确认搜索区域后参数未即时生效。");
            Click(form,"ExecuteButton_Click");
            var previewTimeout=System.Diagnostics.Stopwatch.StartNew();
            while(Field<bool>(form,"_busy")) { Application.DoEvents(); Thread.Sleep(5); if(previewTimeout.Elapsed.TotalSeconds>30) throw new TimeoutException("预览搜索超时"); }
            Check(Field<DataGridView>(form,"resultsGrid").Rows.Count==1,"执行按钮未使用启用模板及搜索区域产生结果。");
            Check(Field<Button>(form,"executeButton").Enabled && Field<Button>(form,"closeButton").Enabled,"预览结束后操作按钮未恢复。");
            for(int index=0;index<tabs.TabCount;index++)
            {
                tabs.SelectedIndex=index; form.PerformLayout(); Application.DoEvents(); Thread.Sleep(40); Application.DoEvents();
                if (index == 1)
                {
                    Check(!Field<DataGridView>(form,"resultsGrid").Visible && Field<ImageCanvas>(form,"templateCanvas").Visible,"模板页仍显示底部结果或未切换模板图。");
                    Check(Field<ImageFrame>(form,"_templateImage").Width==96 && Field<PointF[][]>(Field<ImageCanvas>(form,"templateCanvas"),"_modelPaths").Length>0,"右侧未显示裁剪模板和实际轮廓。");
                }
                using(var bitmap=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new DrawingRectangle(0,0,bitmap.Width,bitmap.Height)); bitmap.Save(System.IO.Path.Combine(artifactDirectory,"轮廓匹配-"+tabs.TabPages[index].Text+".png")); }
            }
            Click(form,"ClearTemplatesButton_Click");
            var saved=(NodeParamContourMatch)form.Params;
            Check(saved.Templates.Count==0 && saved.ModelImageBytes==null,"删除全部后空模板列表未即时生效。");
            Field<NumericUpDown>(form,"findAngleStartNumeric").Value=100;
            Check(((NodeParamContourMatch)form.Params).FindOptions.AngleStartDegrees==100,"暂时无效的角度范围被静默替换成旧值。");
            form.SetParam2Form();
            Check(Field<NumericUpDown>(form,"findAngleStartNumeric").Value==100,"重新打开未恢复当前填写值。");
            Click(form,"CloseButton_Click");
            Check(!form.Visible && ((NodeParamContourMatch)form.Params).FindOptions.AngleStartDegrees==100,"确定未直接关闭或意外撤销参数。");
        }
    }

    /// <summary>真实程序集与原生DLL测试入口。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            string artifacts = args.Length > 0 ? args[0] : "."; System.IO.Directory.CreateDirectory(artifacts);
            using (Mat template = Template()) using (Mat search = Search(template))
            {
                NodeParamContourMatch parameters = NativeChecks(template,search); Console.WriteLine("原生参数、模式、多目标、旋转及涂抹通过。");
                ManualModel(template,search); NodeChecks(parameters,search); Console.WriteLine("节点执行、方案恢复、参数隔离、缓存与图像租约通过。");
                MultiTemplateChecks(parameters,template,artifacts); Console.WriteLine("三页、多模板、搜索区域、位置修正和模板交换通过。");
                CanvasNavigationChecks(template); Console.WriteLine("图像缩放锚点、中键平移与原图涂抹坐标通过。");
                EditorChecks(template,artifacts); Console.WriteLine("真实Designer窗体、区域生成、建模、擦除与保存恢复通过。");
            }
            Console.WriteLine("轮廓模板匹配验证通过，断言数：" + _checks); return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
