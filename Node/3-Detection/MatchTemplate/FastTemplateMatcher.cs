using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using CvRect = OpenCvSharp.Rect;

namespace TDJS_Vision.Node._3_Detection.MatchTemplate
{
    /// <summary>
    /// 模板匹配静态工具，负责图像格式转换、模板学习、native 匹配调用和结果绘制。
    /// </summary>
    internal static class FastTemplateMatcher
    {
        /// <summary>
        /// 新模板匹配 native API 版本，1.1 表示支持稳定错误码、错误文本和模板学习结果持久化。
        /// </summary>
        private const uint RequiredNativeApiVersion = 0x00010001U;

        /// <summary>
        /// 写入模板匹配专用诊断日志，native 崩溃时通过最后一行定位执行阶段。
        /// </summary>
        public static void WriteDiagnosticLog(string message)
        {
            MatchTemplateDiagnosticLog.Write(message);
        }

        public static Bitmap CreateTemplateImage(Bitmap source, float centerX, float centerY, float width, float height, float angle)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            int templateWidth = Math.Max(1, (int)Math.Round(width));
            int templateHeight = Math.Max(1, (int)Math.Round(height));
            var template = new Bitmap(templateWidth, templateHeight, PixelFormat.Format24bppRgb);
            template.SetResolution(96, 96);

            using (var g = Graphics.FromImage(template))
            {
                g.Clear(Color.Black);
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.TranslateTransform(templateWidth / 2f, templateHeight / 2f);
                g.RotateTransform(-angle);
                g.TranslateTransform(-centerX, -centerY);
                g.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
            }

            return template;
        }

        public static byte[] BitmapToPngBytes(Bitmap bitmap)
        {
            if (bitmap == null)
                return null;

            try
            {
                using (var safeBitmap = To24Bpp(bitmap))
                using (var ms = new MemoryStream())
                {
                    safeBitmap.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
            catch
            {
                using (var mat = BitmapConverter.ToMat(bitmap))
                {
                    byte[] bytes;
                    if (!Cv2.ImEncode(".png", mat, out bytes))
                        throw new InvalidOperationException("模板图像数据无法编码。");
                    return bytes;
                }
            }
        }

        public static Bitmap PngBytesToBitmap(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return null;

            using (var ms = new MemoryStream(bytes))
            {
                try
                {
                    using (var raw = new Bitmap(ms))
                        return To24Bpp(raw);
                }
                catch
                {
                    return DecodeImageBytesWithOpenCv(bytes);
                }
            }
        }

        private static Bitmap DecodeImageBytesWithOpenCv(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return null;

            using (var mat = Cv2.ImDecode(bytes, ImreadModes.Color))
            {
                if (mat == null || mat.Empty())
                    throw new InvalidOperationException("模板图像数据无法解码。");

                using (var bitmap = BitmapConverter.ToBitmap(mat))
                    return To24Bpp(bitmap);
            }
        }

        public static Bitmap CreateEmptyMask(System.Drawing.Size size)
        {
            if (size.Width <= 0 || size.Height <= 0)
                return null;

            var mask = new Bitmap(size.Width, size.Height, PixelFormat.Format24bppRgb);
            mask.SetResolution(96, 96);
            using (var g = Graphics.FromImage(mask))
                g.Clear(Color.Black);
            return mask;
        }

        public static bool HasMaskPixels(Bitmap mask)
        {
            if (mask == null)
                return false;

            using (var binary = CreateBinaryMask(mask, mask.Width, mask.Height))
                return binary != null && Cv2.CountNonZero(binary) > 0;
        }

        public static Bitmap ApplyTemplateEraseMask(Bitmap template, Bitmap eraseMask)
        {
            if (template == null)
                return null;
            if (!HasMaskPixels(eraseMask))
                return To24Bpp(template);

            using (var template24 = To24Bpp(template))
            using (var source = BitmapConverter.ToMat(template24))
            using (var mask = CreateBinaryMask(eraseMask, template.Width, template.Height))
            using (var result = new Mat())
            {
                Cv2.Inpaint(source, mask, result, 3.0, InpaintMethod.Telea);
                using (var bitmap = BitmapConverter.ToBitmap(result))
                    return To24Bpp(bitmap);
            }
        }

        public static Bitmap DrawTemplateContourPreview(Bitmap template)
        {
            return DrawTemplateContourPreview(template, (List<PointF>)null);
        }

        public static Bitmap DrawTemplateContourPreview(Bitmap template, Bitmap ignoreMask)
        {
            return DrawTemplateContourPreview(template, BuildTemplateContour(template, ignoreMask));
        }

        public static Bitmap DrawTemplateContourPreview(Bitmap template, List<PointF> contour)
        {
            if (template == null)
                return null;

            var preview = To24Bpp(template);
            if (contour == null)
                contour = BuildTemplateContour(preview);
            using (var g = Graphics.FromImage(preview))
            using (var brush = new SolidBrush(Color.Cyan))
            {
                float cx = preview.Width / 2f;
                float cy = preview.Height / 2f;
                foreach (var point in contour)
                    g.FillRectangle(brush, point.X + cx, point.Y + cy, 1, 1);
            }

            return preview;
        }

        public static FastTemplateMatchResult Match(NodeParamMatchTemplate param, Bitmap sourceImage, Bitmap templateImage)
        {
            using (var session = new MatchSession())
                return session.Match(param, sourceImage, templateImage);
        }

        public static Bitmap DrawPreview(Bitmap sourceImage, FastTemplateMatchResult result, bool showMatchBox, bool showOutline)
        {
            var bitmap = To24Bpp(sourceImage);
            if (result == null)
                return bitmap;

            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (showMatchBox)
                {
                    using (var pen = new Pen(result.IsOk ? Color.Lime : Color.Red, 2))
                    {
                        foreach (var match in result.Matches)
                            g.DrawPolygon(pen, match.Box.GetCorners());
                    }
                }

                if (showOutline)
                {
                    using (var brush = new SolidBrush(Color.Cyan))
                    {
                        foreach (var outline in result.Outlines)
                        {
                            foreach (var point in outline.Points)
                                g.FillRectangle(brush, point.X, point.Y, 1, 1);
                        }
                    }
                }

                using (var brush = new SolidBrush(result.IsOk ? Color.Lime : Color.Red))
                using (var font = new Font("Arial", 12f))
                {
                    for (int i = 0; i < result.Matches.Count; i++)
                    {
                        var corner = result.Matches[i].Box.GetCorners()[0];
                        g.DrawString(string.Format("#{0} {1:F2}", i + 1, result.Matches[i].Score), font, brush, corner);
                    }
                }
            }

            return bitmap;
        }

        public static Bitmap To24Bpp(Bitmap source)
        {
            if (source == null)
                return null;
            if (source.Width <= 0 || source.Height <= 0)
                throw new ArgumentException("图像尺寸无效。", nameof(source));

            if (source.PixelFormat == PixelFormat.Format24bppRgb)
                return Copy24Bpp(source);

            try
            {
                var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
                bitmap.SetResolution(96, 96);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.DrawImage(source, new Rectangle(0, 0, bitmap.Width, bitmap.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
                }

                return bitmap;
            }
            catch
            {
                return To24BppWithOpenCv(source);
            }
        }

        private static Bitmap Copy24Bpp(Bitmap source)
        {
            var rect = new Rectangle(0, 0, source.Width, source.Height);
            var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
            bitmap.SetResolution(96, 96);

            BitmapData srcData = null;
            BitmapData dstData = null;
            bool useFallback = false;
            try
            {
                srcData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                dstData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

                int rowBytes = Math.Min(Math.Abs(srcData.Stride), Math.Abs(dstData.Stride));
                byte[] buffer = new byte[rowBytes];
                for (int y = 0; y < source.Height; y++)
                {
                    IntPtr srcRow = IntPtr.Add(srcData.Scan0, y * srcData.Stride);
                    IntPtr dstRow = IntPtr.Add(dstData.Scan0, y * dstData.Stride);
                    Marshal.Copy(srcRow, buffer, 0, rowBytes);
                    Marshal.Copy(buffer, 0, dstRow, rowBytes);
                }

                return bitmap;
            }
            catch
            {
                useFallback = true;
            }
            finally
            {
                if (dstData != null)
                    bitmap.UnlockBits(dstData);
                if (srcData != null)
                    source.UnlockBits(srcData);
            }

            if (useFallback)
            {
                bitmap.Dispose();
                return To24BppWithOpenCv(source);
            }

            return bitmap;
        }

        private static Bitmap To24BppWithOpenCv(Bitmap source)
        {
            using (var mat = BitmapConverter.ToMat(source))
            using (var bgr = new Mat())
            {
                if (mat.Channels() == 1)
                    Cv2.CvtColor(mat, bgr, ColorConversionCodes.GRAY2BGR);
                else if (mat.Channels() == 4)
                    Cv2.CvtColor(mat, bgr, ColorConversionCodes.BGRA2BGR);
                else
                    mat.CopyTo(bgr);

                using (var bitmap = BitmapConverter.ToBitmap(bgr))
                    return Copy24Bpp(bitmap);
            }
        }

        private static double NormalizeScore(double score)
        {
            double value = score > 1 ? score / 100.0 : score;
            return Math.Max(0, Math.Min(1, value));
        }

        private static double NormalizeRatio(double ratio)
        {
            double value = ratio > 1 ? ratio / 100.0 : ratio;
            return Math.Max(0, Math.Min(1, value));
        }

        public static List<PointF> BuildTemplateContour(Bitmap template)
        {
            return BuildTemplateContour(template, null);
        }

        public static List<PointF> BuildTemplateContour(Bitmap template, Bitmap ignoreMask)
        {
            var points = new List<PointF>();
            using (var mat = BitmapConverter.ToMat(template))
            using (var gray = new Mat())
            using (var edges = new Mat())
            using (var binaryMask = CreateBinaryMask(ignoreMask, template.Width, template.Height))
            using (var expandedMask = new Mat())
            {
                if (mat.Channels() >= 3)
                    Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
                else
                    mat.CopyTo(gray);

                Cv2.Canny(gray, edges, 80, 160);
                if (binaryMask != null)
                {
                    using (var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(7, 7)))
                        Cv2.Dilate(binaryMask, expandedMask, kernel);
                }

                float cx = template.Width / 2f;
                float cy = template.Height / 2f;
                for (int y = 0; y < edges.Rows; y++)
                {
                    for (int x = 0; x < edges.Cols; x++)
                    {
                        if (edges.At<byte>(y, x) != 0 && (expandedMask.Empty() || expandedMask.At<byte>(y, x) == 0))
                            points.Add(new PointF(x - cx, y - cy));
                    }
                }
            }

            return points;
        }

        private static Mat CreateBinaryMask(Bitmap mask, int width, int height)
        {
            if (mask == null || width <= 0 || height <= 0)
                return null;

            using (var maskMat = BitmapConverter.ToMat(mask))
            using (var gray = new Mat())
            {
                if (maskMat.Channels() >= 3)
                    Cv2.CvtColor(maskMat, gray, ColorConversionCodes.BGR2GRAY);
                else
                    maskMat.CopyTo(gray);

                Mat sized = gray;
                Mat resized = null;
                if (gray.Width != width || gray.Height != height)
                {
                    resized = new Mat();
                    Cv2.Resize(gray, resized, new OpenCvSharp.Size(width, height), 0, 0, InterpolationFlags.Nearest);
                    sized = resized;
                }

                var binary = new Mat();
                Cv2.Threshold(sized, binary, 1, 255, ThresholdTypes.Binary);
                resized?.Dispose();
                if (Cv2.CountNonZero(binary) == 0)
                {
                    binary.Dispose();
                    return null;
                }

                return binary;
            }
        }

        private static FastTemplateContour TransformContour(List<PointF> contour, float centerX, float centerY, float angle)
        {
            var result = new FastTemplateContour();
            double rad = angle * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            foreach (var point in contour)
            {
                float x = (float)(point.X * cos - point.Y * sin + centerX);
                float y = (float)(point.X * sin + point.Y * cos + centerY);
                result.Points.Add(new PointF(x, y));
            }

            return result;
        }

        /// <summary>
        /// native 模板匹配会话，缓存 MatchTool 引擎与模板学习结果，避免每次执行都重新 LearnPattern。
        /// </summary>
        internal sealed class MatchSession : IDisposable
        {
            /// <summary>
            /// native 引擎访问锁，保证同一模板会话不会被并发调用破坏状态。
            /// </summary>
            private readonly object syncRoot = new object();

            /// <summary>
            /// MatchTool.dll 创建的 native 引擎句柄。
            /// </summary>
            private FastMatchEngineHandle engine;

            /// <summary>
            /// 当前模板是否已经完成 native 学习。
            /// </summary>
            private bool templateLearned;

            /// <summary>
            /// 模板内容版本号，模板或涂抹蒙版变更时递增。
            /// </summary>
            private int templateVersion;

            /// <summary>
            /// native 引擎已学习的模板版本号。
            /// </summary>
            private int learnedTemplateVersion = -1;

            /// <summary>
            /// native 引擎已学习模板的宽度，用于防止未显式失效时尺寸变化。
            /// </summary>
            private int learnedTemplateWidth;

            /// <summary>
            /// native 引擎已学习模板的高度，用于防止未显式失效时尺寸变化。
            /// </summary>
            private int learnedTemplateHeight;

            /// <summary>
            /// 当前模板缓存的轮廓点。
            /// </summary>
            private List<PointF> cachedContour;

            /// <summary>
            /// 当前轮廓缓存对应的模板版本号。
            /// </summary>
            private int cachedContourVersion = -1;

            /// <summary>
            /// 当前轮廓缓存是否使用了涂抹蒙版。
            /// </summary>
            private bool cachedContourUsesMask;

            /// <summary>
            /// 是否强制走demo一致的native算法路径，NCC节点使用该开关避免进入原模板匹配CPU后备算法。
            /// </summary>
            private readonly bool forceNativeDemoAlgorithm;

            /// <summary>
            /// native 内部错误码，通常表示原生库内部异常已被 DLL 保护层拦截。
            /// </summary>
            private const int NativeInternalErrorStatus = -10;

            /// <summary>
            /// 会话是否已经释放。
            /// </summary>
            private bool disposed;

            /// <summary>
            /// 创建模板匹配会话。
            /// </summary>
            public MatchSession(bool forceNativeDemoAlgorithm = false)
            {
                this.forceNativeDemoAlgorithm = forceNativeDemoAlgorithm;
            }

            /// <summary>
            /// 使已学习模板和轮廓缓存失效，下一次匹配会重新学习模板。
            /// </summary>
            public void InvalidateTemplate()
            {
                lock (syncRoot)
                {
                    templateVersion++;
                    templateLearned = false;
                    cachedContour = null;
                    cachedContourVersion = -1;
                }
            }

            /// <summary>
            /// 执行模板匹配并返回匹配中心、角度、得分和轮廓。
            /// </summary>
            public FastTemplateMatchResult Match(NodeParamMatchTemplate param, Bitmap sourceImage, Bitmap templateImage)
            {
                WriteDiagnosticLog("MatchSession.Match入口：使用参数内蒙版，" + MatchTemplateDiagnosticLog.DescribeBitmap("source", sourceImage) + "，" + MatchTemplateDiagnosticLog.DescribeBitmap("template", templateImage));
                Bitmap contourMask = null;
                try
                {
                    if (param != null && param.TemplateEraseMaskBytes != null && param.TemplateEraseMaskBytes.Length > 0)
                    {
                        WriteDiagnosticLog("MatchSession.Match：开始从参数还原模板涂抹蒙版，字节数=" + param.TemplateEraseMaskBytes.Length);
                        contourMask = PngBytesToBitmap(param.TemplateEraseMaskBytes);
                        WriteDiagnosticLog("MatchSession.Match：参数蒙版还原完成，" + MatchTemplateDiagnosticLog.DescribeBitmap("mask", contourMask));
                    }
                    return Match(param, sourceImage, templateImage, contourMask);
                }
                finally
                {
                    contourMask?.Dispose();
                    WriteDiagnosticLog("MatchSession.Match出口：参数内蒙版已释放。");
                }
            }

            /// <summary>
            /// 执行模板匹配并使用调用方提供的涂抹蒙版计算显示轮廓。
            /// </summary>
            public FastTemplateMatchResult Match(NodeParamMatchTemplate param, Bitmap sourceImage, Bitmap templateImage, Bitmap contourMask)
            {
                WriteDiagnosticLog("MatchSession.Match入口：外部蒙版，" + MatchTemplateDiagnosticLog.DescribeParam(param) + "，" + MatchTemplateDiagnosticLog.DescribeBitmap("source", sourceImage) + "，" + MatchTemplateDiagnosticLog.DescribeBitmap("template", templateImage) + "，" + MatchTemplateDiagnosticLog.DescribeBitmap("mask", contourMask));
                if (param == null)
                    throw new ArgumentNullException(nameof(param));
                if (sourceImage == null)
                    throw new ArgumentNullException(nameof(sourceImage));
                if (templateImage == null)
                    throw new ArgumentNullException(nameof(templateImage));

                lock (syncRoot)
                {
                    WriteDiagnosticLog("MatchSession.Match：已进入会话锁。");
                    ThrowIfDisposed();

                    var result = new FastTemplateMatchResult();
                    var sw = Stopwatch.StartNew();

                    using (var source = To24Bpp(sourceImage))
                    using (var template = To24Bpp(templateImage))
                    {
                        WriteDiagnosticLog("MatchSession.Match：图像已转24位，" + MatchTemplateDiagnosticLog.DescribeBitmap("source24", source) + "，" + MatchTemplateDiagnosticLog.DescribeBitmap("template24", template));
                        if (!forceNativeDemoAlgorithm && ShouldUseManagedCpuFallback())
                            return MatchWithManagedCpuFallback(param, source, template, contourMask, sw);

                        try
                        {
                            WriteDiagnosticLog("MatchSession.Match：开始确保模板已学习，templateVersion=" + templateVersion + "，learnedVersion=" + learnedTemplateVersion);
                            EnsureTemplateLearned(template);
                            WriteDiagnosticLog("MatchSession.Match：模板学习状态确认完成。");

                            int maxResults = Math.Max(1, Math.Min(1000, param.ResultNum));
                            double[] xs = new double[maxResults];
                            double[] ys = new double[maxResults];
                            double[] angles = new double[maxResults];
                            double[] scores = new double[maxResults];

                            double expectedScore = NormalizeScore(param.MinScore);
                            double maxOverlap = NormalizeRatio(param.MaxOverlap);
                            double toleranceAngle = Math.Abs(param.ToleranceAngle);

                            WriteDiagnosticLog(string.Format("MatchSession.Match：准备调用native匹配，maxResults={0}，expectedScore={1:F4}，toleranceAngle={2:F4}，maxOverlap={3:F4}，angleStep={4:F4}，coarse={5}。", maxResults, expectedScore, toleranceAngle, maxOverlap, Math.Max(0, param.AngleStep), param.CoarseMatch));
                            int count = engine.Match(
                                source,
                                expectedScore,
                                toleranceAngle,
                                maxOverlap,
                                Math.Max(0, param.AngleStep),
                                param.CoarseMatch,
                                xs,
                                ys,
                                angles,
                                scores,
                                maxResults);
                            WriteDiagnosticLog("MatchSession.Match：native匹配返回，count=" + count);

                            List<PointF> contour = null;
                            if (param.ShowOutlineStatus)
                            {
                                WriteDiagnosticLog("MatchSession.Match：开始获取模板轮廓。");
                                contour = GetTemplateContour(template, contourMask);
                                WriteDiagnosticLog("MatchSession.Match：模板轮廓完成，点数=" + (contour == null ? 0 : contour.Count));
                            }

                            for (int i = 0; i < count; i++)
                            {
                                var match = new FastTemplateMatchInfo
                                {
                                    Box = new FastTemplateMatchBox
                                    {
                                        CenterX = (float)xs[i],
                                        CenterY = (float)ys[i],
                                        Width = template.Width,
                                        Height = template.Height,
                                        Angle = (float)-angles[i]
                                    },
                                    Score = scores[i]
                                };
                                result.Matches.Add(match);

                                if (contour != null && contour.Count > 0)
                                    result.Outlines.Add(TransformContour(contour, match.Box.CenterX, match.Box.CenterY, match.Box.Angle));
                            }

                            result.ExpectedCount = maxResults;
                            result.ExpectedScore = expectedScore;
                            result.IsOk = result.Matches.Count > 0 && result.Matches.All(m => m.Score >= expectedScore);
                        }
                        catch (MatchToolNativeException ex) when (ShouldFallbackAfterNativeFailure(ex))
                        {
                            ResetNativeEngineAfterFailure(ex);
                            return MatchWithManagedCpuFallback(param, source, template, contourMask, sw);
                        }
                    }

                    sw.Stop();
                    result.AlgorithmMs = sw.Elapsed.TotalMilliseconds;
                    WriteDiagnosticLog(string.Format("MatchSession.Match出口：成功，matches={0}，elapsed={1:F2}ms。", result.Matches.Count, result.AlgorithmMs));
                    return result;
                }
            }

            /// <summary>
            /// 使用订阅输出的 Mat 直接执行 native NCC 匹配，避免节点运行时整图 Bitmap 转换。
            /// </summary>
            public FastTemplateMatchResult Match(NodeParamMatchTemplate param, Mat sourceImage, Bitmap templateImage, Bitmap contourMask)
            {
                WriteDiagnosticLog("MatchSession.MatchMat入口：" + MatchTemplateDiagnosticLog.DescribeParam(param) + "，source=" + DescribeMat(sourceImage) + "，" + MatchTemplateDiagnosticLog.DescribeBitmap("template", templateImage) + "，" + MatchTemplateDiagnosticLog.DescribeBitmap("mask", contourMask));
                if (param == null)
                    throw new ArgumentNullException(nameof(param));
                if (sourceImage == null || sourceImage.Empty())
                    throw new ArgumentNullException(nameof(sourceImage));
                if (templateImage == null)
                    throw new ArgumentNullException(nameof(templateImage));

                lock (syncRoot)
                {
                    WriteDiagnosticLog("MatchSession.MatchMat：已进入会话锁。");
                    ThrowIfDisposed();

                    var result = new FastTemplateMatchResult();
                    var sw = Stopwatch.StartNew();
                    bool disposeTemplate = false;
                    Bitmap template = null;
                    try
                    {
                        template = PrepareNativeTemplate(templateImage, out disposeTemplate);
                        WriteDiagnosticLog("MatchSession.MatchMat：图像已准备，sourceNative=" + DescribeMat(sourceImage) + "，" + MatchTemplateDiagnosticLog.DescribeBitmap("templateNative", template));
                        try
                        {
                            WriteDiagnosticLog("MatchSession.MatchMat：开始确保模板已学习，templateVersion=" + templateVersion + "，learnedVersion=" + learnedTemplateVersion);
                            EnsureTemplateLearned(template);
                            WriteDiagnosticLog("MatchSession.MatchMat：模板学习状态确认完成。");

                            int maxResults = Math.Max(1, Math.Min(1000, param.ResultNum));
                            double[] xs = new double[maxResults];
                            double[] ys = new double[maxResults];
                            double[] angles = new double[maxResults];
                            double[] scores = new double[maxResults];

                            double expectedScore = NormalizeScore(param.MinScore);
                            double maxOverlap = NormalizeRatio(param.MaxOverlap);
                            double toleranceAngle = Math.Abs(param.ToleranceAngle);

                            WriteDiagnosticLog(string.Format("MatchSession.MatchMat：准备调用native匹配，maxResults={0}，expectedScore={1:F4}，toleranceAngle={2:F4}，maxOverlap={3:F4}，angleStep={4:F4}，coarse={5}。", maxResults, expectedScore, toleranceAngle, maxOverlap, Math.Max(0, param.AngleStep), param.CoarseMatch));
                            int count = engine.Match(
                                sourceImage,
                                expectedScore,
                                toleranceAngle,
                                maxOverlap,
                                Math.Max(0, param.AngleStep),
                                param.CoarseMatch,
                                xs,
                                ys,
                                angles,
                                scores,
                                maxResults);
                            WriteDiagnosticLog("MatchSession.MatchMat：native匹配返回，count=" + count);

                            List<PointF> contour = null;
                            if (param.ShowOutlineStatus)
                            {
                                WriteDiagnosticLog("MatchSession.MatchMat：开始获取模板轮廓。");
                                contour = GetTemplateContour(template, contourMask);
                                WriteDiagnosticLog("MatchSession.MatchMat：模板轮廓完成，点数=" + (contour == null ? 0 : contour.Count));
                            }

                            for (int i = 0; i < count; i++)
                            {
                                var match = new FastTemplateMatchInfo
                                {
                                    Box = new FastTemplateMatchBox
                                    {
                                        CenterX = (float)xs[i],
                                        CenterY = (float)ys[i],
                                        Width = template.Width,
                                        Height = template.Height,
                                        Angle = (float)-angles[i]
                                    },
                                    Score = scores[i]
                                };
                                result.Matches.Add(match);

                                if (contour != null && contour.Count > 0)
                                    result.Outlines.Add(TransformContour(contour, match.Box.CenterX, match.Box.CenterY, match.Box.Angle));
                            }

                            result.ExpectedCount = maxResults;
                            result.ExpectedScore = expectedScore;
                            result.IsOk = result.Matches.Count > 0 && result.Matches.All(m => m.Score >= expectedScore);
                        }
                        catch (MatchToolNativeException ex) when (ShouldFallbackAfterNativeFailure(ex))
                        {
                            ResetNativeEngineAfterFailure(ex);
                            return MatchWithManagedCpuFallback(param, sourceImage, template, contourMask, sw);
                        }
                    }
                    finally
                    {
                        if (disposeTemplate)
                            template?.Dispose();
                    }

                    sw.Stop();
                    result.AlgorithmMs = sw.Elapsed.TotalMilliseconds;
                    WriteDiagnosticLog(string.Format("MatchSession.MatchMat出口：成功，matches={0}，elapsed={1:F2}ms。", result.Matches.Count, result.AlgorithmMs));
                    return result;
                }
            }

            /// <summary>
            /// 获取 native 学习使用的模板；模板已是24位时直接复用，避免每次运行复制模板。
            /// </summary>
            private static Bitmap PrepareNativeTemplate(Bitmap template, out bool disposeAfterUse)
            {
                disposeAfterUse = false;
                if (template.PixelFormat == PixelFormat.Format24bppRgb)
                    return template;

                disposeAfterUse = true;
                return To24Bpp(template);
            }

            /// <summary>
            /// 生成 Mat 的简短诊断描述，避免为日志把图像转成 Bitmap。
            /// </summary>
            private static string DescribeMat(Mat image)
            {
                if (image == null)
                    return "null";
                if (image.Empty())
                    return "empty";
                return string.Format("{0}x{1}, channels={2}, step={3}, data={4}", image.Width, image.Height, image.Channels(), image.Step(), image.Data);
            }

            /// <summary>
            /// 判断 native 失败是否允许切换到托管 CPU 后备算法。
            /// </summary>
            private bool ShouldFallbackAfterNativeFailure(MatchToolNativeException exception)
            {
                bool useFallback = !forceNativeDemoAlgorithm && exception != null && exception.Status == NativeInternalErrorStatus;
                WriteDiagnosticLog("MatchSession.Match：native失败后备判断，Status=" + (exception == null ? 0 : exception.Status) + "，ForceNativeDemoAlgorithm=" + forceNativeDemoAlgorithm + "，UseFallback=" + useFallback + "。");
                return useFallback;
            }

            /// <summary>
            /// native 内部异常后释放当前引擎，避免复用可能已经不可靠的学习状态。
            /// </summary>
            private void ResetNativeEngineAfterFailure(MatchToolNativeException exception)
            {
                WriteDiagnosticLog("MatchSession.Match：native返回内部错误，自动释放引擎并切换CPU后备，原因=" + (exception == null ? string.Empty : exception.Message));
                engine?.Dispose();
                engine = null;
                templateLearned = false;
                learnedTemplateVersion = -1;
                learnedTemplateWidth = 0;
                learnedTemplateHeight = 0;
            }

            /// <summary>
            /// 当前发布的 native DLL 需要 AVX2 且本机不支持时，自动切换到 OpenCvSharp CPU 后备算法。
            /// </summary>
            private static bool ShouldUseManagedCpuFallback()
            {
                string nativeDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Native");
                bool forceManagedFallback = MatchToolRuntimeProfile.ForceManagedCpuFallback(nativeDirectory, "MatchTool.runtime.txt");
                bool requiresAvx2 = MatchToolRuntimeProfile.RequiresAvx2(nativeDirectory, "MatchTool.runtime.txt");
                bool supportsAvx2 = CpuInstructionSet.SupportsAvx2;
                bool useFallback = forceManagedFallback || (requiresAvx2 && !supportsAvx2);
                WriteDiagnosticLog("MatchSession.Match：CPU后备判断，ForceManagedFallback=" + forceManagedFallback + "，RequiresAvx2=" + requiresAvx2 + "，CpuSupportsAvx2=" + supportsAvx2 + "，UseFallback=" + useFallback + "。");
                return useFallback;
            }

            /// <summary>
            /// OpenCvSharp CPU 后备模板匹配，避免低配电脑进入 AVX2 native DLL 后进程级闪退。
            /// </summary>
            private FastTemplateMatchResult MatchWithManagedCpuFallback(
                NodeParamMatchTemplate param,
                Bitmap source,
                Bitmap template,
                Bitmap contourMask,
                Stopwatch stopwatch)
            {
                WriteDiagnosticLog("ManagedCpuFallback入口：" + MatchTemplateDiagnosticLog.DescribeParam(param));
                var result = new FastTemplateMatchResult();
                int maxResults = Math.Max(1, Math.Min(1000, param.ResultNum));
                double expectedScore = NormalizeScore(param.MinScore);
                double maxOverlap = NormalizeRatio(param.MaxOverlap);
                List<FastTemplateMatchInfo> candidates = FindManagedCpuCandidates(param, source, template, expectedScore, maxResults);

                foreach (var candidate in candidates.OrderByDescending(item => item.Score))
                {
                    if (result.Matches.Count >= maxResults)
                        break;
                    if (result.Matches.Any(item => CalculateOverlapRatio(item.Box, candidate.Box) > maxOverlap))
                        continue;

                    result.Matches.Add(candidate);
                }

                if (param.ShowOutlineStatus && result.Matches.Count > 0)
                {
                    List<PointF> contour = GetTemplateContour(template, contourMask);
                    foreach (var match in result.Matches)
                        result.Outlines.Add(TransformContour(contour, match.Box.CenterX, match.Box.CenterY, match.Box.Angle));
                }

                stopwatch.Stop();
                result.ExpectedCount = maxResults;
                result.ExpectedScore = expectedScore;
                result.IsOk = result.Matches.Count > 0 && result.Matches.All(m => m.Score >= expectedScore);
                result.AlgorithmMs = stopwatch.Elapsed.TotalMilliseconds;
                WriteDiagnosticLog(string.Format("ManagedCpuFallback出口：matches={0}，elapsed={1:F2}ms。", result.Matches.Count, result.AlgorithmMs));
                return result;
            }

            /// <summary>
            /// 使用 Mat 输入执行 OpenCvSharp CPU 后备模板匹配，供 native 内部错误时继续完成本次执行。
            /// </summary>
            private FastTemplateMatchResult MatchWithManagedCpuFallback(
                NodeParamMatchTemplate param,
                Mat source,
                Bitmap template,
                Bitmap contourMask,
                Stopwatch stopwatch)
            {
                WriteDiagnosticLog("ManagedCpuFallbackMat入口：" + DescribeMat(source));
                using (var sourceBitmap = BitmapConverter.ToBitmap(source))
                using (var source24 = To24Bpp(sourceBitmap))
                {
                    return MatchWithManagedCpuFallback(param, source24, template, contourMask, stopwatch);
                }
            }

            /// <summary>
            /// 按角度扫描收集候选结果，后续再统一做重叠抑制。
            /// </summary>
            private static List<FastTemplateMatchInfo> FindManagedCpuCandidates(
                NodeParamMatchTemplate param,
                Bitmap source,
                Bitmap template,
                double expectedScore,
                int maxResults)
            {
                var candidates = new List<FastTemplateMatchInfo>();
                using (var sourceMat = BitmapConverter.ToMat(source))
                using (var sourceGray = ToGrayMat(sourceMat))
                using (var templateMat = BitmapConverter.ToMat(template))
                using (var templateGray = ToGrayMat(templateMat))
                {
                    foreach (double angle in BuildFallbackAngles(param))
                    {
                        using (var rotated = RotateMat(templateGray, angle, InterpolationFlags.Linear))
                        using (var maskSeed = new Mat(templateGray.Rows, templateGray.Cols, MatType.CV_8UC1, Scalar.All(255)))
                        using (var rotatedMask = RotateMat(maskSeed, angle, InterpolationFlags.Nearest))
                        {
                            if (rotated.Width <= 0 || rotated.Height <= 0 || rotated.Width > sourceGray.Width || rotated.Height > sourceGray.Height)
                                continue;

                            using (var response = new Mat())
                            {
                                Cv2.MatchTemplate(sourceGray, rotated, response, TemplateMatchModes.CCorrNormed, rotatedMask);
                                int perAngleLimit = Math.Max(3, Math.Min(30, maxResults * 4));
                                for (int index = 0; index < perAngleLimit; index++)
                                {
                                    double minValue;
                                    double maxValue;
                                    OpenCvSharp.Point minLocation;
                                    OpenCvSharp.Point maxLocation;
                                    Cv2.MinMaxLoc(response, out minValue, out maxValue, out minLocation, out maxLocation);
                                    if (double.IsNaN(maxValue) || maxValue < expectedScore)
                                        break;

                                    candidates.Add(new FastTemplateMatchInfo
                                    {
                                        Box = new FastTemplateMatchBox
                                        {
                                            CenterX = maxLocation.X + rotated.Width / 2f,
                                            CenterY = maxLocation.Y + rotated.Height / 2f,
                                            Width = template.Width,
                                            Height = template.Height,
                                            Angle = (float)angle
                                        },
                                        Score = maxValue
                                    });

                                    SuppressResponsePeak(response, maxLocation, rotated.Width, rotated.Height);
                                }
                            }
                        }
                    }
                }

                return candidates;
            }

            /// <summary>
            /// 生成后备算法角度列表，0度优先，随后按正负方向扩展。
            /// </summary>
            private static List<double> BuildFallbackAngles(NodeParamMatchTemplate param)
            {
                double tolerance = Math.Min(180.0, Math.Abs(param.ToleranceAngle));
                double step = param.AngleStep > 0 ? param.AngleStep : (param.CoarseMatch ? 8.0 : 5.0);
                step = Math.Max(1.0, Math.Min(30.0, step));
                var angles = new List<double> { 0.0 };
                for (double angle = step; angle <= tolerance + 0.001; angle += step)
                {
                    angles.Add(angle);
                    angles.Add(-angle);
                }

                WriteDiagnosticLog("ManagedCpuFallback：角度数量=" + angles.Count + "，step=" + step + "。");
                return angles;
            }

            /// <summary>
            /// 转灰度 Mat，降低后备匹配计算量。
            /// </summary>
            private static Mat ToGrayMat(Mat source)
            {
                var gray = new Mat();
                if (source.Channels() >= 3)
                    Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
                else
                    source.CopyTo(gray);
                return gray;
            }

            /// <summary>
            /// 旋转 Mat 并扩展画布，保证旋转后的模板不被裁剪。
            /// </summary>
            private static Mat RotateMat(Mat source, double angle, InterpolationFlags interpolation)
            {
                var center = new Point2f(source.Width / 2f, source.Height / 2f);
                using (var matrix = Cv2.GetRotationMatrix2D(center, angle, 1.0))
                {
                    double cos = Math.Abs(matrix.At<double>(0, 0));
                    double sin = Math.Abs(matrix.At<double>(0, 1));
                    int newWidth = Math.Max(1, (int)Math.Round(source.Height * sin + source.Width * cos));
                    int newHeight = Math.Max(1, (int)Math.Round(source.Height * cos + source.Width * sin));
                    matrix.Set(0, 2, matrix.At<double>(0, 2) + newWidth / 2.0 - center.X);
                    matrix.Set(1, 2, matrix.At<double>(1, 2) + newHeight / 2.0 - center.Y);

                    var rotated = new Mat();
                    Cv2.WarpAffine(source, rotated, matrix, new OpenCvSharp.Size(newWidth, newHeight), interpolation, BorderTypes.Constant, Scalar.All(0));
                    return rotated;
                }
            }

            /// <summary>
            /// 清除当前响应图峰值附近区域，避免同一个目标被重复取出。
            /// </summary>
            private static void SuppressResponsePeak(Mat response, OpenCvSharp.Point location, int templateWidth, int templateHeight)
            {
                int radiusX = Math.Max(4, templateWidth / 2);
                int radiusY = Math.Max(4, templateHeight / 2);
                int left = Math.Max(0, location.X - radiusX);
                int top = Math.Max(0, location.Y - radiusY);
                int right = Math.Min(response.Width, location.X + radiusX);
                int bottom = Math.Min(response.Height, location.Y + radiusY);
                if (right > left && bottom > top)
                    Cv2.Rectangle(response, new CvRect(left, top, right - left, bottom - top), Scalar.All(0), -1);
            }

            /// <summary>
            /// 计算两个匹配框的轴对齐包围盒重叠率，用于后备算法去重。
            /// </summary>
            private static double CalculateOverlapRatio(FastTemplateMatchBox first, FastTemplateMatchBox second)
            {
                CvRect a = first.ToBoundingRect();
                CvRect b = second.ToBoundingRect();
                int left = Math.Max(a.X, b.X);
                int top = Math.Max(a.Y, b.Y);
                int right = Math.Min(a.X + a.Width, b.X + b.Width);
                int bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
                int width = Math.Max(0, right - left);
                int height = Math.Max(0, bottom - top);
                double intersection = width * height;
                double minArea = Math.Max(1, Math.Min(a.Width * a.Height, b.Width * b.Height));
                return intersection / minArea;
            }

            /// <summary>
            /// 释放 native 引擎。
            /// </summary>
            public void Dispose()
            {
                lock (syncRoot)
                {
                    if (disposed)
                        return;

                    engine?.Dispose();
                    engine = null;
                    disposed = true;
                }
            }

            /// <summary>
            /// 确保 native 引擎已经学习当前模板。
            /// </summary>
            private void EnsureTemplateLearned(Bitmap template)
            {
                if (engine == null)
                {
                    WriteDiagnosticLog("EnsureTemplateLearned：native引擎为空，开始创建。");
                    engine = new FastMatchEngineHandle();
                    WriteDiagnosticLog("EnsureTemplateLearned：native引擎创建完成。");
                }

                bool sizeChanged = template.Width != learnedTemplateWidth || template.Height != learnedTemplateHeight;
                if (templateLearned && learnedTemplateVersion == templateVersion && !sizeChanged)
                {
                    WriteDiagnosticLog("EnsureTemplateLearned：复用已学习模板。");
                    return;
                }

                WriteDiagnosticLog(string.Format("EnsureTemplateLearned：开始学习模板，sizeChanged={0}，oldSize={1}x{2}，newSize={3}x{4}。", sizeChanged, learnedTemplateWidth, learnedTemplateHeight, template.Width, template.Height));
                engine.Learn(template);
                templateLearned = true;
                learnedTemplateVersion = templateVersion;
                learnedTemplateWidth = template.Width;
                learnedTemplateHeight = template.Height;
                WriteDiagnosticLog("EnsureTemplateLearned：模板学习完成。");
            }

            /// <summary>
            /// 获取当前模板轮廓，未改变模板时直接复用缓存。
            /// </summary>
            private List<PointF> GetTemplateContour(Bitmap template, Bitmap contourMask)
            {
                bool usesMask = contourMask != null;
                if (cachedContour != null && cachedContourVersion == templateVersion && cachedContourUsesMask == usesMask)
                    return cachedContour;

                cachedContour = BuildTemplateContour(template, contourMask);
                cachedContourVersion = templateVersion;
                cachedContourUsesMask = usesMask;
                return cachedContour;
            }

            /// <summary>
            /// 已释放会话不允许继续执行 native 匹配。
            /// </summary>
            private void ThrowIfDisposed()
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(MatchSession));
            }
        }

        private sealed class FastMatchEngineHandle : IDisposable
        {
            /// <summary>
            /// MatchTool.dll 持有的模板匹配引擎指针。
            /// </summary>
            private IntPtr _engine;

            public FastMatchEngineHandle()
            {
                MatchTemplateDiagnosticLog.Write("FastMatchEngineHandle：准备调用CreateMatchEngine。");
                _engine = FastMatchNative.CreateMatchEngine();
                MatchTemplateDiagnosticLog.Write("FastMatchEngineHandle：CreateMatchEngine返回，handle=" + _engine);
                if (_engine == IntPtr.Zero)
                    throw new InvalidOperationException("模板匹配引擎创建失败。");
            }

            /// <summary>
            /// 学习当前模板图像，并缓存 native 金字塔与归一化统计量。
            /// </summary>
            public void Learn(Bitmap template)
            {
                MatchTemplateDiagnosticLog.Write("FastMatchEngineHandle.Learn入口：" + MatchTemplateDiagnosticLog.DescribeBitmap("template", template));
                var data = template.LockBits(new Rectangle(0, 0, template.Width, template.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                try
                {
                    MatchTemplateDiagnosticLog.Write(string.Format("FastMatchEngineHandle.Learn：准备调用LearnPatternMem，width={0}，height={1}，stride={2}，scan0={3}。", template.Width, template.Height, data.Stride, data.Scan0));
                    int code = FastMatchNative.LearnPatternMem(_engine, data.Scan0, template.Width, template.Height, data.Stride, 3);
                    string nativeStage = FastMatchNative.GetLastDiagnosticStage();
                    MatchTemplateDiagnosticLog.Write("FastMatchEngineHandle.Learn：LearnPatternMem返回，code=" + code + "，nativeStage=" + nativeStage);
                    if (code != 0)
                        throw CreateNativeException("模板学习失败", code, nativeStage);
                }
                finally
                {
                    template.UnlockBits(data);
                    MatchTemplateDiagnosticLog.Write("FastMatchEngineHandle.Learn出口：模板位图已解锁。");
                }
            }

            /// <summary>
            /// 执行模板匹配，native 返回错误码时抛出明确异常，避免被误判为零结果。
            /// </summary>
            public int Match(
                Bitmap source,
                double expectedScore,
                double toleranceAngle,
                double maxOverlap,
                double angleStep,
                bool coarseMatch,
                double[] xs,
                double[] ys,
                double[] angles,
                double[] scores,
                int maxResults)
            {
                MatchTemplateDiagnosticLog.Write(string.Format("FastMatchEngineHandle.Match入口：{0}，score={1:F4}，angle={2:F4}，overlap={3:F4}，step={4:F4}，coarse={5}，max={6}。", MatchTemplateDiagnosticLog.DescribeBitmap("source", source), expectedScore, toleranceAngle, maxOverlap, angleStep, coarseMatch, maxResults));
                var data = source.LockBits(new Rectangle(0, 0, source.Width, source.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                try
                {
                    MatchTemplateDiagnosticLog.Write(string.Format("FastMatchEngineHandle.Match：准备调用MatchMem，width={0}，height={1}，stride={2}，scan0={3}。", source.Width, source.Height, data.Stride, data.Scan0));
                    int count = FastMatchNative.MatchMem(
                        _engine,
                        data.Scan0,
                        source.Width,
                        source.Height,
                        data.Stride,
                        3,
                        expectedScore,
                        toleranceAngle,
                        maxOverlap,
                        angleStep,
                        xs,
                        ys,
                        angles,
                        scores,
                        maxResults,
                        coarseMatch ? 1 : 0);
                    MatchTemplateDiagnosticLog.Write("FastMatchEngineHandle.Match：MatchMem返回，count=" + count);
                    if (count < 0)
                        throw CreateNativeException("模板匹配执行失败", count);

                    return Math.Min(count, maxResults);
                }
                finally
                {
                    source.UnlockBits(data);
                    MatchTemplateDiagnosticLog.Write("FastMatchEngineHandle.Match出口：源图位图已解锁。");
                }
            }

            /// <summary>
            /// 使用 Mat 内存直接执行模板匹配，通道转换由 demo DLL 按原逻辑处理。
            /// </summary>
            public int Match(
                Mat source,
                double expectedScore,
                double toleranceAngle,
                double maxOverlap,
                double angleStep,
                bool coarseMatch,
                double[] xs,
                double[] ys,
                double[] angles,
                double[] scores,
                int maxResults)
            {
                if (source == null || source.Empty())
                    throw new ArgumentNullException(nameof(source));
                int channels = source.Channels();
                if (source.Depth() != MatType.CV_8U || (channels != 1 && channels != 3 && channels != 4))
                    throw new InvalidOperationException("NCC模板匹配native输入只支持8位1通道、3通道或4通道图像。");

                int stride = checked((int)source.Step());
                MatchTemplateDiagnosticLog.Write(string.Format("FastMatchEngineHandle.MatchMat入口：width={0}，height={1}，stride={2}，data={3}，score={4:F4}，angle={5:F4}，overlap={6:F4}，step={7:F4}，coarse={8}，max={9}。", source.Width, source.Height, stride, source.Data, expectedScore, toleranceAngle, maxOverlap, angleStep, coarseMatch, maxResults));
                int count = FastMatchNative.MatchMem(
                    _engine,
                    source.Data,
                    source.Width,
                    source.Height,
                    stride,
                    channels,
                    expectedScore,
                    toleranceAngle,
                    maxOverlap,
                    angleStep,
                    xs,
                    ys,
                    angles,
                    scores,
                    maxResults,
                    coarseMatch ? 1 : 0);
                MatchTemplateDiagnosticLog.Write("FastMatchEngineHandle.MatchMat：MatchMem返回，count=" + count);
                if (count < 0)
                    throw CreateNativeException("模板匹配执行失败", count);

                return Math.Min(count, maxResults);
            }

            /// <summary>
            /// 把 native 已学习的模板状态保存到文件，供后续冷启动快速加载。
            /// </summary>
            public void SavePattern(string filePath)
            {
                int code = FastMatchNative.SavePatternFile(_engine, filePath);
                if (code != 0)
                    throw CreateNativeException("模板学习结果保存失败", code);
            }

            /// <summary>
            /// 从文件加载 native 已学习模板状态，避免重新学习模板。
            /// </summary>
            public void LoadPattern(string filePath)
            {
                int code = FastMatchNative.LoadPatternFile(_engine, filePath);
                if (code != 0)
                    throw CreateNativeException("模板学习结果加载失败", code);
            }

            /// <summary>
            /// 创建带 native 错误文本的异常，方便低配电脑现场定位卡死或失败原因。
            /// </summary>
            private static MatchToolNativeException CreateNativeException(string actionName, int status, string nativeStage = null)
            {
                string message = FastMatchNative.GetStatusMessage(status);
                string stageText = string.IsNullOrWhiteSpace(nativeStage) ? string.Empty : " native阶段：" + nativeStage;
                return new MatchToolNativeException(status, string.Format("{0}：{1}（错误码 {2}）。{3}", actionName, message, status, stageText));
            }

            /// <summary>
            /// 释放 native 引擎。
            /// </summary>
            public void Dispose()
            {
                if (_engine == IntPtr.Zero)
                    return;

                FastMatchNative.ReleaseMatchEngine(_engine);
                _engine = IntPtr.Zero;
            }
        }

        /// <summary>
        /// native 模板匹配错误异常，保留稳定错误码以便上层决定是否自动降级。
        /// </summary>
        private sealed class MatchToolNativeException : InvalidOperationException
        {
            /// <summary>
            /// MatchTool.dll 返回的稳定错误码。
            /// </summary>
            public int Status { get; }

            /// <summary>
            /// 创建带错误码的 native 异常。
            /// </summary>
            public MatchToolNativeException(int status, string message)
                : base(message)
            {
                Status = status;
            }
        }

        private static class FastMatchNative
        {
            /// <summary>
            /// 随 native DLL 一起发布的运行配置文件，用于说明当前 DLL 是否依赖特殊 CPU 指令集。
            /// </summary>
            private const string RuntimeProfileFileName = "MatchTool.runtime.txt";

            /// <summary>
            /// 初始化 native DLL 搜索目录，并验证 CPU 指令集和模板匹配 native API 版本。
            /// </summary>
            static FastMatchNative()
            {
                string nativeDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Native");
                MatchTemplateDiagnosticLog.Write("FastMatchNative初始化：BaseDirectory=" + AppDomain.CurrentDomain.BaseDirectory + "，NativeDirectory=" + nativeDirectory + "，Exists=" + Directory.Exists(nativeDirectory));
                if (Directory.Exists(nativeDirectory))
                    SetDllDirectory(nativeDirectory);

                EnsureCpuInstructionSetSupported(nativeDirectory);

                MatchTemplateDiagnosticLog.Write("FastMatchNative初始化：准备调用GetMatchApiVersion。");
                uint apiVersion = GetMatchApiVersion();
                MatchTemplateDiagnosticLog.Write(string.Format("FastMatchNative初始化：GetMatchApiVersion返回0x{0:X8}。", apiVersion));
                if (apiVersion < RequiredNativeApiVersion)
                    throw new InvalidOperationException(string.Format("模板匹配 DLL 版本过旧，当前版本 0x{0:X8}，要求版本 0x{1:X8}。", apiVersion, RequiredNativeApiVersion));
            }

            /// <summary>
            /// 当前发布的 MatchTool.dll 为 AVX2 编译版本时，提前拦截不支持 AVX2 的电脑，避免进入 native 后触发进程级闪退。
            /// </summary>
            private static void EnsureCpuInstructionSetSupported(string nativeDirectory)
            {
                bool requiresAvx2 = MatchToolRuntimeProfile.RequiresAvx2(nativeDirectory, RuntimeProfileFileName);
                bool supportsAvx2 = CpuInstructionSet.SupportsAvx2;
                MatchTemplateDiagnosticLog.Write("FastMatchNative初始化：RequiresAvx2=" + requiresAvx2 + "，CpuSupportsAvx2=" + supportsAvx2 + "。");
                if (!requiresAvx2 || supportsAvx2)
                    return;

                throw new InvalidOperationException("当前电脑CPU不支持AVX2指令，随软件发布的模板匹配库为AVX2版本，已停止执行以避免软件闪退。请更新为通用CPU版MatchTool.dll后再运行模板匹配。");
            }

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            private static extern bool SetDllDirectory(string lpPathName);

            [DllImport("MatchTool.dll", CallingConvention = CallingConvention.Cdecl)]
            public static extern uint GetMatchApiVersion();

            [DllImport("MatchTool.dll", CallingConvention = CallingConvention.Cdecl)]
            private static extern IntPtr GetMatchStatusMessage(int status);

            [DllImport("MatchTool.dll", CallingConvention = CallingConvention.Cdecl)]
            private static extern IntPtr GetLastMatchDiagnosticStage();

            [DllImport("MatchTool.dll", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr CreateMatchEngine();

            [DllImport("MatchTool.dll", CallingConvention = CallingConvention.Cdecl)]
            public static extern void ReleaseMatchEngine(IntPtr engine);

            [DllImport("MatchTool.dll", CallingConvention = CallingConvention.Cdecl)]
            public static extern int LearnPatternMem(IntPtr engine, IntPtr imgData, int width, int height, int stride, int channels);

            [DllImport("MatchTool.dll", CallingConvention = CallingConvention.Cdecl)]
            public static extern int MatchMem(
                IntPtr engine,
                IntPtr imgData,
                int width,
                int height,
                int stride,
                int channels,
                double expectedScore,
                double toleranceAngle,
                double maxOverlapRatio,
                double maxAngleStep,
                double[] outX,
                double[] outY,
                double[] outAngle,
                double[] outScore,
                int maxResults,
                int bCoarseMatch);

            [DllImport("MatchTool.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern int SavePatternFile(IntPtr engine, [MarshalAs(UnmanagedType.LPStr)] string filePath);

            [DllImport("MatchTool.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern int LoadPatternFile(IntPtr engine, [MarshalAs(UnmanagedType.LPStr)] string filePath);

            /// <summary>
            /// 获取 native 错误码对应的稳定英文诊断文本，获取失败时返回通用描述。
            /// </summary>
            public static string GetStatusMessage(int status)
            {
                IntPtr pointer = GetMatchStatusMessage(status);
                if (pointer == IntPtr.Zero)
                    return "未知 native 错误";

                string message = Marshal.PtrToStringAnsi(pointer);
                return string.IsNullOrWhiteSpace(message) ? "未知 native 错误" : message;
            }

            /// <summary>
            /// 获取 native 内部最后执行阶段，定位被保护层拦截的-10错误发生位置。
            /// </summary>
            public static string GetLastDiagnosticStage()
            {
                try
                {
                    IntPtr pointer = GetLastMatchDiagnosticStage();
                    if (pointer == IntPtr.Zero)
                        return "unknown";

                    string stage = Marshal.PtrToStringAnsi(pointer);
                    return string.IsNullOrWhiteSpace(stage) ? "unknown" : stage;
                }
                catch (EntryPointNotFoundException)
                {
                    return "diagnostic-export-missing";
                }
                catch
                {
                    return "diagnostic-read-failed";
                }
            }
        }
    }

    /// <summary>
    /// 模板匹配 native DLL 的运行环境说明读取器，缺少说明文件时按当前已发布的 AVX2 版本保守处理。
    /// </summary>
    internal static class MatchToolRuntimeProfile
    {
        /// <summary>
        /// 判断当前 native DLL 是否需要 AVX2 指令集。
        /// </summary>
        public static bool RequiresAvx2(string nativeDirectory, string profileFileName)
        {
            try
            {
                string profilePath = Path.Combine(nativeDirectory ?? string.Empty, profileFileName ?? string.Empty);
                if (!File.Exists(profilePath))
                    return true;

                string profile = File.ReadAllText(profilePath, Encoding.UTF8);
                return profile.IndexOf("instruction_set=x64-generic", StringComparison.OrdinalIgnoreCase) < 0
                    && profile.IndexOf("requires_avx2=false", StringComparison.OrdinalIgnoreCase) < 0;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// 判断是否由现场配置强制绕开 native 模板学习，改用托管 CPU 后备算法。
        /// </summary>
        public static bool ForceManagedCpuFallback(string nativeDirectory, string profileFileName)
        {
            try
            {
                string profilePath = Path.Combine(nativeDirectory ?? string.Empty, profileFileName ?? string.Empty);
                if (!File.Exists(profilePath))
                    return false;

                string profile = File.ReadAllText(profilePath, Encoding.UTF8);
                return profile.IndexOf("force_managed_cpu_fallback=true", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Windows CPU 指令集能力检测，避免 AVX2-only native DLL 在低配电脑上触发不可捕获的非法指令。
    /// </summary>
    internal static class CpuInstructionSet
    {
        /// <summary>
        /// Windows 公开的 AVX2 支持特征编号。
        /// </summary>
        private const int PfAvx2InstructionsAvailable = 40;

        /// <summary>
        /// 当前操作系统和 CPU 是否同时支持 AVX2 指令。
        /// </summary>
        public static bool SupportsAvx2
        {
            get
            {
                try
                {
                    return IsProcessorFeaturePresent(PfAvx2InstructionsAvailable);
                }
                catch
                {
                    return false;
                }
            }
        }

        [DllImport("kernel32.dll")]
        private static extern bool IsProcessorFeaturePresent(int processorFeature);
    }

    /// <summary>
    /// 模板匹配崩溃定位专用日志，直接写文件并吞掉自身异常，避免影响主流程。
    /// </summary>
    internal static class MatchTemplateDiagnosticLog
    {
        /// <summary>
        /// 多线程写诊断日志时的文件锁。
        /// </summary>
        private static readonly object SyncRoot = new object();

        /// <summary>
        /// 追加一行诊断日志。
        /// </summary>
        public static void Write(string message)
        {
            try
            {
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string logDirectory = Path.Combine(baseDirectory, "Logs");
                Directory.CreateDirectory(logDirectory);
                string filePath = Path.Combine(logDirectory, DateTime.Now.ToString("yyyyMMdd") + "_模板匹配诊断.log");
                string line = string.Format(
                    "{0:yyyy-MM-dd HH:mm:ss.fff} [PID:{1}] [TID:{2}] {3}{4}",
                    DateTime.Now,
                    System.Diagnostics.Process.GetCurrentProcess().Id,
                    Thread.CurrentThread.ManagedThreadId,
                    message ?? string.Empty,
                    Environment.NewLine);

                lock (SyncRoot)
                    File.AppendAllText(filePath, line, Encoding.UTF8);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 生成位图关键信息文本。
        /// </summary>
        public static string DescribeBitmap(string name, Bitmap bitmap)
        {
            if (bitmap == null)
                return name + "=null";

            return string.Format(
                "{0}={1}x{2},format={3},raw={4}",
                name,
                bitmap.Width,
                bitmap.Height,
                bitmap.PixelFormat,
                Image.GetPixelFormatSize(bitmap.PixelFormat));
        }

        /// <summary>
        /// 生成模板匹配运行参数文本。
        /// </summary>
        public static string DescribeParam(NodeParamMatchTemplate param)
        {
            if (param == null)
                return "param=null";

            return string.Format(
                "param=resultNum:{0},score:{1},angle:{2},step:{3},overlap:{4},coarse:{5},sort:{6},allSearch:{7},showBox:{8},showOutline:{9}",
                param.ResultNum,
                param.MinScore,
                param.ToleranceAngle,
                param.AngleStep,
                param.MaxOverlap,
                param.CoarseMatch,
                param.SortMode,
                param.AllSearch,
                param.ShowOutRegionStatus,
                param.ShowOutlineStatus);
        }
    }

    public sealed class FastTemplateMatchResult
    {
        public List<FastTemplateMatchInfo> Matches { get; } = new List<FastTemplateMatchInfo>();
        public List<FastTemplateContour> Outlines { get; } = new List<FastTemplateContour>();
        public Bitmap OutputBitmap { get; set; }
        public bool IsOk { get; set; }
        public int ExpectedCount { get; set; }
        public double ExpectedScore { get; set; }
        public double AlgorithmMs { get; set; }
    }

    public sealed class FastTemplateMatchInfo
    {
        public FastTemplateMatchBox Box { get; set; }
        public double Score { get; set; }
    }

    public sealed class FastTemplateMatchBox
    {
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float Angle { get; set; }

        public PointF[] GetCorners()
        {
            float halfW = Width / 2f;
            float halfH = Height / 2f;
            var points = new[]
            {
                new PointF(-halfW, -halfH),
                new PointF( halfW, -halfH),
                new PointF( halfW,  halfH),
                new PointF(-halfW,  halfH)
            };

            using (var matrix = new Matrix())
            {
                matrix.Translate(CenterX, CenterY);
                matrix.Rotate(Angle);
                matrix.TransformPoints(points);
            }

            return points;
        }

        public CvRect ToBoundingRect()
        {
            var corners = GetCorners();
            float minX = corners.Min(p => p.X);
            float minY = corners.Min(p => p.Y);
            float maxX = corners.Max(p => p.X);
            float maxY = corners.Max(p => p.Y);

            int x = (int)Math.Floor(minX);
            int y = (int)Math.Floor(minY);
            int width = Math.Max(1, (int)Math.Ceiling(maxX - minX));
            int height = Math.Max(1, (int)Math.Ceiling(maxY - minY));
            return new CvRect(x, y, width, height);
        }
    }

    public sealed class FastTemplateContour
    {
        public List<PointF> Points { get; } = new List<PointF>();
    }
}
