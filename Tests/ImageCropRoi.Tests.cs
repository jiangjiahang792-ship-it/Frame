using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Newtonsoft.Json;
using OpenCvSharp;
using TDJS_Vision.Forms.DispShowImage;
using TDJS_Vision.Forms.ShapeDraw;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._2_ImagePreprocessing.ImageCrop;

/// <summary>真实显示控件交互、裁剪参数及像素输出的离线回归。</summary>
internal static class ImageCropRoiTests
{
    /// <summary>非公开实例成员标志。</summary>
    private const BindingFlags InstanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;
    /// <summary>断言测试条件。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    /// <summary>获取参数窗体中的设计器控件。</summary>
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, InstanceFlags).GetValue(target);
    /// <summary>执行窗体内部操作。</summary>
    private static void Click(object target, string name) => target.GetType().GetMethod(name, InstanceFlags).Invoke(target, new object[] { null, EventArgs.Empty });
    /// <summary>向实际控件的鼠标入口发送事件。</summary>
    private static void Mouse(ShowImageControl viewer, string name, int x, int y, MouseButtons button = MouseButtons.Left) =>
        typeof(ShowImageControl).GetMethod(name, InstanceFlags).Invoke(viewer, new object[] { new MouseEventArgs(button, 1, x, y, 0) });
    /// <summary>把图像坐标转换为当前视口坐标。</summary>
    private static System.Drawing.Point Screen(ShowImageControl viewer, float x, float y)
    {
        float scale = Field<float>(viewer, "_scale");
        PointF offset = Field<PointF>(viewer, "_offset");
        return new System.Drawing.Point((int)Math.Round(x * scale + offset.X), (int)Math.Round(y * scale + offset.Y));
    }
    /// <summary>按图像坐标拖动。</summary>
    private static void Drag(ShowImageControl viewer, PointF start, PointF end)
    {
        var a = Screen(viewer, start.X, start.Y); var b = Screen(viewer, end.X, end.Y);
        Mouse(viewer, "OnMouseDown", a.X, a.Y); Mouse(viewer, "OnMouseMove", b.X, b.Y); Mouse(viewer, "OnMouseUp", b.X, b.Y);
    }
    /// <summary>断言无效区域被拒绝。</summary>
    private static void Reject(ImageCropRoiRegion roi)
    {
        try { roi.GetImageRect(400, 300); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("无效区域没有被拒绝。");
    }
    /// <summary>验证合法旧 ROI 空值不会产生被转换器内部捕获的 JSON 异常。</summary>
    private static void NullRoiSerialization()
    {
        int jsonErrors = 0;
        EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> handler = (sender, e) =>
        {
            if (e.Exception is JsonException) jsonErrors++;
        };
        AppDomain.CurrentDomain.FirstChanceException += handler;
        try
        {
            var param = JsonConvert.DeserializeObject<NodeParamImageCrop>(
                "{\"ROIs\":null,\"ImageRois\":[{\"CenterX\":794.169434,\"CenterY\":793.305054,\"Width\":1500.84741,\"Height\":1432.2373,\"Angle\":0}],\"RoiEnable\":true}");
            Check(param.ROIs == null && param.ImageRois.Count == 1 && param.RoiEnable, "新版裁剪参数还原失败。");
            var restored = JsonConvert.DeserializeObject<NodeParamImageCrop>(JsonConvert.SerializeObject(param));
            Check(restored.ROIs == null && restored.ImageRois[0].Width == param.ImageRois[0].Width &&
                restored.ImageRois[0].CenterX == param.ImageRois[0].CenterX, "保存再读取改变了新版裁剪区域。");
            var empty = JsonConvert.DeserializeObject<NodeParamImageCrop>("{\"ROIs\":{},\"ImageRois\":null}");
            Check(empty.ROIs != null && empty.ROIs.Count == 0 && empty.ImageRois == null, "旧版空对象语义改变。");
            var missing = JsonConvert.DeserializeObject<NodeParamImageCrop>("{}");
            Check(missing.ROIs == null && missing.ImageRois == null, "缺失字段默认值改变。");
            Check(jsonErrors == 0, "合法空值在读取过程中仍触发了 JSON 异常。");
        }
        finally { AppDomain.CurrentDomain.FirstChanceException -= handler; }
    }

    /// <summary>运行真实控件和裁剪验证，不连接任何设备。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            NullRoiSerialization();
            Geometry();
            Rectification();
            OutOfBounds();
            if (args.Length > 0)
            {
                // 用户提供的是带界面的截图；按截图内绿色ROI位置验证实际裁剪形状。
                using (var reference = Cv2.ImRead(args[0]))
                using (var crop = new ImageCropRoiRegion
                {
                    CenterX = 319, CenterY = 394, Width = 72, Height = 210, Angle = 45
                }.CropRectified(reference))
                    Cv2.ImWrite("ImageCrop-reference-rectified.png", crop);
            }
            using (var form = new NodeParamFormImageCrop(null, null))
            using (var source = new Mat(300, 400, MatType.CV_8UC3, new Scalar(11, 27, 63)))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new System.Drawing.Point(-3000, -3000);
                form.Show(); Application.DoEvents();
                var viewer = Field<ShowImageControl>(form, "showImageControl1");
                Check(viewer.EnableRectangleRoiDrawing, "未默认开启左键绘制。");
                viewer.SetImage(new Bitmap(400, 300)); viewer.ShowFit();
                // 快速绘制只发送按下和松开，确保最终点不会丢失。
                var start = Screen(viewer, 80, 80); var end = Screen(viewer, 180, 150);
                Mouse(viewer, "OnMouseDown", start.X, start.Y);
                Mouse(viewer, "OnMouseUp", end.X, end.Y);
                Check(viewer.DynamicRoiCount == 1, "快速绘制没有生成矩形。");
                var roi = viewer.GetAllDynamicRects()[0];
                Check(roi.IsSelected && Math.Abs(roi.W - 100) < 2, "松开后矩形未选中或终点错误。");
                float oldX = roi.CX;
                Drag(viewer, new PointF(roi.CX, roi.CY), new PointF(roi.CX + 20, roi.CY + 15));
                Check(Math.Abs(roi.CX - oldX - 20) < 2, "矩形内部不能拖动。");
                float scale = Field<float>(viewer, "_scale");
                var rotateStart = new PointF(roi.CX + roi.W / 2 + 10 / scale, roi.CY - roi.H / 2 - 10 / scale);
                float dx = rotateStart.X - roi.CX, dy = rotateStart.Y - roi.CY;
                const double radians = Math.PI / 6;
                Drag(viewer, rotateStart, new PointF(roi.CX + (float)(dx * Math.Cos(radians) - dy * Math.Sin(radians)),
                    roi.CY + (float)(dx * Math.Sin(radians) + dy * Math.Cos(radians))));
                Check(Math.Abs(roi.Phi - 30) < 2, "旋转手柄不能旋转矩形。");
                double angle = roi.Phi * Math.PI / 180;
                PointF corner = new PointF(roi.CX + (float)(roi.W / 2 * Math.Cos(angle) - roi.H / 2 * Math.Sin(angle)),
                    roi.CY + (float)(roi.W / 2 * Math.Sin(angle) + roi.H / 2 * Math.Cos(angle)));
                float oldWidth = roi.W;
                Drag(viewer, corner, new PointF(corner.X + (float)(8 * Math.Cos(angle)), corner.Y + (float)(8 * Math.Sin(angle))));
                Check(roi.W > oldWidth + 10, "旋转后角点不能缩放。");
                Field<Sunny.UI.UISwitch>(form, "uiSwitch1").Active = true;
                Click(form, "button2_Click");
                var saved = (NodeParamImageCrop)form.Params;
                Check(saved.ImageRois.Count == 1 && Math.Abs(saved.ImageRois[0].Angle - 30) < 2, "角度未保存。");
                Rect savedRect = form.GetImageROIRects(source)[0];
                roi.CX += 20;
                Check(form.GetImageROIRects(source)[0] == savedRect, "未保存编辑改变了运行区域。");
                form.Params = JsonConvert.DeserializeObject<NodeParamImageCrop>(JsonConvert.SerializeObject(saved));
                form.SetParam2Form();
                Check(Math.Abs(viewer.GetAllDynamicRects()[0].Phi - saved.ImageRois[0].Angle) < 0.01, "序列化后角度丢失。");
                form.Show(); Application.DoEvents();
                var help = Field<Label>(form, "labelRoiHelp");
                Check(help.GetPreferredSize(new System.Drawing.Size(help.Width, 0)).Height <= help.Height, "默认窗体操作说明被截断。");
                using (var defaultScreenshot = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(defaultScreenshot, new Rectangle(0, 0, form.Width, form.Height));
                    defaultScreenshot.Save("ImageCrop-parameters-default.png");
                }
                form.ClientSize = new System.Drawing.Size(1200, 650); viewer.ShowFit();
                Check(form.GetImageROIRects(source)[0] == savedRect, "窗口缩放导致ROI跑位。");
                VerifyCrop(source, savedRect);
                form.Show(); Application.DoEvents();
                using (var screenshot = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(screenshot, new Rectangle(0, 0, screenshot.Width, screenshot.Height));
                    screenshot.Save("ImageCrop-parameters.png");
                }
                // 删除按键及清空按钮，不应恢复旧参数中的区域。
                var restored = viewer.GetAllDynamicRects()[0]; restored.IsSelected = true;
                typeof(ShowImageControl).GetMethod("OnKeyDown", InstanceFlags).Invoke(viewer, new object[] { new KeyEventArgs(Keys.Delete) });
                Check(viewer.DynamicRoiCount == 0, "Delete不能删除选中框。");
                Drag(viewer, new PointF(200, 200), new PointF(260, 250));
                Click(form, "buttonClearRois_Click"); Click(form, "button2_Click");
                Check(form.GetImageROIRects(source)[0] == new Rect(0, 0, 400, 300), "清空后没有使用全图。");
                // 误点和右键释放不应生成小框或提前结束左键拖动。
                var p = Screen(viewer, 30, 30);
                Mouse(viewer, "OnMouseDown", p.X, p.Y); Mouse(viewer, "OnMouseUp", p.X, p.Y);
                Check(viewer.DynamicRoiCount == 0, "误点产生了小框。");
                Mouse(viewer, "OnMouseDown", p.X, p.Y); Mouse(viewer, "OnMouseUp", p.X, p.Y, MouseButtons.Right);
                Check(Field<bool>(viewer, "_isDrawingRectangleRoi"), "右键释放中断了左键绘制。");
                var q = Screen(viewer, 60, 70); Mouse(viewer, "OnMouseUp", q.X, q.Y);
                Check(viewer.DynamicRoiCount == 1, "混合按键后绘制失败。");
            }
            Legacy();
            Console.WriteLine("通过：ROI交互/保存恢复/旧参数兼容/0度逐像素一致/90与180度方向/任意角度无黑边/亚像素采样/灰度对齐/单图及多图坐标/资源释放/越界补边。");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    /// <summary>验证旋转外接矩形及异常参数边界。</summary>
    private static void Geometry()
    {
        var roi = new ImageCropRoiRegion { CenterX = 200, CenterY = 150, Width = 100, Height = 40 };
        Check(roi.GetImageRect(400, 300) == new Rect(150, 130, 100, 40), "0度裁剪尺寸变化。");
        roi.Angle = 90; Check(roi.GetImageRect(400, 300) == new Rect(180, 100, 40, 100), "90度宽高错误。");
        roi.Angle = 45; Check(roi.GetImageRect(400, 300) == new Rect(150, 100, 100, 100), "45度外接框错误。");
        roi.Angle = -45; Check(roi.GetImageRect(400, 300) == new Rect(150, 100, 100, 100), "负角度错误。");
        roi.CenterX = 0; Check(roi.GetImageRect(400, 300).X < 0, "越界ROI被截断或改变位置。"); roi.CenterX = float.NaN; Reject(roi);
        roi.CenterX = 200; roi.Width = 0; Reject(roi);
    }
    /// <summary>验证实际执行节点的独立Mat与灰度快速路径。</summary>
    private static void VerifyCrop(Mat source, Rect rect)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        var rects = new List<Rect> { rect };
        var images = (List<Mat>)typeof(NodeImageCrop).GetMethod("CropImages", flags).Invoke(null, new object[] { source, rects });
        using (var input = new OutputImage())
        using (var gray = new Mat(source.Rows, source.Cols, MatType.CV_8UC1, new Scalar(91)))
        {
            input.GrayImg = gray;
            try
            {
                Check(images[0].Width == rect.Width && images[0].Height == rect.Height, "裁剪图尺寸与偏移不一致。");
                Check(images[0].At<Vec3b>(0, 0).Item1 == 27, "裁剪像素错误。");
                images[0].Set(0, 0, new Vec3b(0, 0, 0));
                Check(source.At<Vec3b>(rect.Y, rect.X).Item1 == 27, "裁剪修改影响源图。");
                using (var result = (Mat)typeof(NodeImageCrop).GetMethod("BuildFirstGrayCrop", flags).Invoke(null, new object[] { input, rects, images }))
                    Check(result.Width == rect.Width && result.At<byte>(0, 0) == 91, "上游灰度裁剪未复用。");
            }
            finally { foreach (var image in images) image.Dispose(); }
        }
    }
    /// <summary>验证纠正后的像素方向、浮点精度、边界及下游输出坐标。</summary>
    private static void Rectification()
    {
        using (var ramp = new Mat(300, 400, MatType.CV_32FC1))
        using (var color = new Mat(300, 400, MatType.CV_8UC3, new Scalar(80, 140, 200)))
        using (var gray = new Mat(300, 400, MatType.CV_8UC1, new Scalar(123)))
        using (var input = new OutputImage { SrcImg = color, Bitmaps = new List<Mat> { color }, GrayImg = gray })
        {
            // 使用解析可求值的平面，避免用同一仿射实现生成期望图而掩盖错误。
            for (int y = 0; y < ramp.Height; y++)
                for (int x = 0; x < ramp.Width; x++)
                    ramp.Set(y, x, (float)(x + 3 * y));
            var roi = new ImageCropRoiRegion { CenterX = 120, CenterY = 100, Width = 40, Height = 80 };
            using (var direct = new Mat(ramp, new Rect(100, 60, 40, 80)))
            using (var result = roi.CropRectified(ramp))
                Check(Cv2.Norm(direct, result, NormTypes.INF) == 0, "0度整数ROI发生了重采样或偏移。");
            foreach (float angle in new[] { 90F, 180F, 270F })
            {
                roi.Angle = angle;
                using (var bounds = new Mat(ramp, roi.GetImageRect(ramp.Width, ramp.Height)))
                using (var expected = new Mat())
                using (var result = roi.CropRectified(ramp))
                {
                    Cv2.Rotate(bounds, expected, angle == 90 ? RotateFlags.Rotate90Counterclockwise :
                        angle == 180 ? RotateFlags.Rotate180 : RotateFlags.Rotate90Clockwise);
                    Check(Cv2.Norm(expected, result, NormTypes.INF) == 0, "直角纠正方向或半像素对齐错误：" + angle);
                }
            }
            roi.CenterX = 180.25F; roi.CenterY = 140.75F; roi.Width = 61.4F; roi.Height = 103.6F;
            foreach (float angle in new[] { -135F, -45F, 0F, 30F, 45F, 89.9F, 135F, 225F, 359F })
            {
                roi.Angle = angle;
                using (var result = roi.CropRectified(ramp))
                using (var solid = roi.CropRectified(color))
                {
                    Check(result.Width == 61 && result.Height == 104, "旋转裁剪使用了外接矩形或错误取整。");
                    double rad = angle * Math.PI / 180;
                    for (int y = 0; y < result.Height; y += 7)
                        for (int x = 0; x < result.Width; x += 5)
                        {
                            double localX = (x + 0.5) * roi.Width / result.Width - roi.Width / 2;
                            double localY = (y + 0.5) * roi.Height / result.Height - roi.Height / 2;
                            double sx = roi.CenterX - 0.5 + Math.Cos(rad) * localX - Math.Sin(rad) * localY;
                            double sy = roi.CenterY - 0.5 + Math.Sin(rad) * localX + Math.Cos(rad) * localY;
                            Check(Math.Abs(result.At<float>(y, x) - (sx + 3 * sy)) < 0.08, "任意角度亚像素采样超差。");
                        }
                    Check(Cv2.Mean(solid).Val0 == 80 && Cv2.Mean(solid).Val1 == 140, "纠正后仍出现黑边或非ROI画布。");
                }
            }
            roi.Angle = 45;
            var regions = new List<ImageCropRoiRegion> { roi };
            using (OutputImage output = BuildOutput(input, color, regions))
            {
                Check(ReferenceEquals(output.SrcImg, output.Bitmaps[0]), "单图基准仍然是纠正前的源图。");
                Check(output.Rectangles[0] == new Rect(0, 0, 61, 104), "单图检测仍叠加旧图坐标偏移。");
                Check(output.GrayImg.Size() == output.Bitmaps[0].Size() && Cv2.Mean(output.GrayImg).Val0 == 123, "灰度未同步纠正。");
                output.Bitmaps[0].Set(0, 0, new Vec3b());
                Check(color.At<Vec3b>(140, 180).Item0 == 80, "纠正后修改污染源图。");
                Check(input.ActiveLeaseCount == 1, "输出未持有依赖租约。");
            }
            Check(input.ActiveLeaseCount == 0, "输出释放后仍持有租约。");
            var exact = new List<ImageCropRoiRegion>
            {
                new ImageCropRoiRegion { CenterX = 120, CenterY = 100, Width = 40, Height = 80 }
            };
            using (OutputImage output = BuildOutput(input, color, exact))
            {
                Check(ReferenceEquals(output.SrcImg, color), "整数0度快路径改变了原图坐标基准。");
                Check(output.Rectangles[0] == new Rect(100, 60, 40, 80), "整数0度快路径偏移错误。");
            }
            exact[0].Width = 39.99998F;
            using (var narrow = exact[0].CropRectified(color))
                Check(narrow.Width == 40, "浮点误差导致ROI丢失一列像素。");
            var edge = new ImageCropRoiRegion { CenterX = 40, CenterY = 20, Width = 40, Height = 80, Angle = 90 };
            using (var edgeCrop = edge.CropRectified(color))
                Check(Cv2.Mean(edgeCrop).Val0 == 80, "贴边有效ROI出现黑边。");
            regions.Add(new ImageCropRoiRegion { CenterX = 280, CenterY = 180, Width = 40, Height = 70, Angle = -30 });
            using (OutputImage output = BuildOutput(input, color, regions))
            {
                Check(output.Bitmaps.Count == 2 && output.SrcImg.Height == 174, "多图预览尺寸错误。");
                Check(output.Rectangles[1] == new Rect(0, 104, 40, 70), "多图检测坐标发生重叠。");
                for (int i = 0; i < 2; i++)
                    using (var tile = new Mat(output.SrcImg, output.Rectangles[i]))
                        Check(Cv2.Norm(tile, output.Bitmaps[i], NormTypes.INF) == 0, "多图预览偏移与检测图不一致。");
            }
            using (var grayInput = new OutputImage { SrcImg = gray, Bitmaps = new List<Mat> { gray } })
            using (OutputImage output = BuildOutput(grayInput, gray, regions))
                Check(ReferenceEquals(output.GrayImg, output.Bitmaps[0]), "单通道ROI未复用灰度图。");
            regions[0] = new ImageCropRoiRegion { CenterX = 100, CenterY = 100, Width = 40, Height = 50 };
            regions[1] = new ImageCropRoiRegion { CenterX = 0, CenterY = 0, Width = 0, Height = 50, Angle = 45 };
            roi.CenterX = float.NaN;
            Reject(roi);
            bool rejected = false;
            try { using (var invalid = BuildOutput(input, color, regions)) { } }
            catch (TargetInvocationException) { rejected = true; }
            Check(rejected && input.ActiveLeaseCount == 0, "失败输出未释放资源。");
        }
    }

    /// <summary>验证四边、四角与完全越界时不报错，有效像素不偏移，缺失像素复制最近边缘。</summary>
    private static void OutOfBounds()
    {
        using (var source = new Mat(60, 80, MatType.CV_32FC1))
        using (var input = new OutputImage { SrcImg = source, Bitmaps = new List<Mat> { source } })
        {
            for (int y = 0; y < source.Height; y++)
                for (int x = 0; x < source.Width; x++)
                    source.Set(y, x, (float)(x + 3 * y + 10));
            var centers = new[] { new PointF(0, 30), new PointF(80, 30), new PointF(40, 0), new PointF(40, 60),
                new PointF(0, 0), new PointF(80, 0), new PointF(0, 60), new PointF(80, 60),
                new PointF(-100, -100), new PointF(180, 160) };
            foreach (PointF center in centers)
                foreach (float angle in new[] { 0F, 45F, -30F, 90F })
                {
                    var region = new ImageCropRoiRegion { CenterX = center.X, CenterY = center.Y, Width = 40, Height = 30, Angle = angle };
                    using (OutputImage output = BuildOutput(input, source, new List<ImageCropRoiRegion> { region }))
                    {
                        Mat image = output.Bitmaps[0];
                        Check(image.Width == 40 && image.Height == 30, "越界后ROI尺寸变化。");
                        Check(ReferenceEquals(output.SrcImg, image) && ReferenceEquals(output.GrayImg, image), "越界输出坐标或灰度基准错误。");
                        Check(output.Rectangles[0] == new Rect(0, 0, 40, 30), "越界输出保留了错误偏移。");
                        double radians = angle * Math.PI / 180;
                        for (int y = 0; y < 30; y++)
                            for (int x = 0; x < 40; x++)
                            {
                                double localX = x - 19.5, localY = y - 14.5;
                                double sx = center.X - 0.5 + Math.Cos(radians) * localX - Math.Sin(radians) * localY;
                                double sy = center.Y - 0.5 + Math.Sin(radians) * localX + Math.Cos(radians) * localY;
                                double expected = Math.Max(0, Math.Min(79, sx)) + 3 * Math.Max(0, Math.Min(59, sy)) + 10;
                                Check(Math.Abs(image.At<float>(y, x) - expected) < 0.08, "越界像素补齐或图内采样发生偏移。");
                            }
                    }
                }
            // 旧方案的轴对齐裁剪路径必须与新ROI一致，不能残留越界异常。
            var rects = new List<Rect> { new Rect(-10, -20, 40, 50) };
            using (var output = (OutputImage)typeof(NodeImageCrop).GetMethod("BuildCroppedOutput", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { input, source, null, rects }))
            {
                Check(output.Bitmaps[0].At<float>(20, 10) == source.At<float>(0, 0), "旧ROI越界后有效像素错位。");
                Check(output.Bitmaps[0].At<float>(0, 0) == 10, "旧ROI未补齐左上角。");
            }
            Check(input.ActiveLeaseCount == 0, "越界裁剪释放后仍持有源图租约。");
        }
        using (var color = new Mat(60, 80, MatType.CV_8UC3, new Scalar(80, 140, 200)))
        using (var gray = new Mat(60, 80, MatType.CV_8UC1, new Scalar(123)))
        using (var input = new OutputImage { SrcImg = color, Bitmaps = new List<Mat> { color }, GrayImg = gray })
        using (var output = BuildOutput(input, color, new List<ImageCropRoiRegion>
        {
            new ImageCropRoiRegion { CenterX = -10, CenterY = 0, Width = 50, Height = 40, Angle = 45 }
        }))
            Check(output.GrayImg.Size() == output.Bitmaps[0].Size() && Cv2.Mean(output.GrayImg).Val0 == 123,
                "越界彩色图与灰度补边不同步。");
        using (var form = new NodeParamFormImageCrop(null, null))
        {
            var viewer = Field<ShowImageControl>(form, "showImageControl1");
            viewer.ImageBitmap = new Bitmap(80, 60);
            viewer.AddRoiRotatedRect(-10, -10, (float)Math.PI / 4, 25, 20);
            Field<Sunny.UI.UISwitch>(form, "uiSwitch1").Active = true;
            Click(form, "button2_Click");
            Check(((NodeParamImageCrop)form.Params).ImageRois.Count == 1, "越界ROI未能保存。");
        }
    }

    /// <summary>调用实际运行节点的完整输出构造入口。</summary>
    private static OutputImage BuildOutput(OutputImage input, Mat source, List<ImageCropRoiRegion> regions)
    {
        var rects = new List<Rect>();
        // 无效项交给输出构造器验证，以覆盖部分ROI已创建后的异常释放。
        foreach (var roi in regions)
        {
            try { rects.Add(roi.GetImageRect(source.Width, source.Height)); }
            catch (InvalidOperationException) { rects.Add(new Rect()); }
        }
        return (OutputImage)typeof(NodeImageCrop).GetMethod("BuildCroppedOutput", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { input, source, regions, rects });
    }

    /// <summary>验证无图保存旧参数及首次图像恢复后的像素坐标迁移。</summary>
    private static void Legacy()
    {
        using (var form = new NodeParamFormImageCrop(null, null))
        using (var source = new Mat(300, 400, MatType.CV_8UC1, Scalar.All(7)))
        {
            var param = new NodeParamImageCrop { RoiEnable = true, ROIs = new List<ROI> { new RectangleROI(new RectangleF(150, 100, 120, 80), 0) } };
            param = JsonConvert.DeserializeObject<NodeParamImageCrop>(JsonConvert.SerializeObject(param));
            form.Params = param; form.SetParam2Form();
            Rect oldRect = form.GetImageROIRects(source)[0];
            Click(form, "button2_Click");
            Check(((NodeParamImageCrop)form.Params).ImageRois == null && ((NodeParamImageCrop)form.Params).ROIs.Count == 1, "无图保存丢失旧ROI。");
            var viewer = Field<ShowImageControl>(form, "showImageControl1");
            viewer.ImageBitmap = new Bitmap(400, 300); form.SetParam2Form();
            Check(viewer.DynamicRoiCount == 1, "旧ROI没有恢复。");
            Click(form, "button2_Click");
            Check(((NodeParamImageCrop)form.Params).ImageRois.Count == 1, "旧ROI没有迁移到像素参数。");
            Check(form.GetImageROIRects(source)[0] == oldRect, "迁移改变了旧裁剪范围。");
        }
    }
}
