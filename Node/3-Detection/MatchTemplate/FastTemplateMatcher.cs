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
using CvRect = OpenCvSharp.Rect;

namespace TDJS_Vision.Node._3_Detection.MatchTemplate
{
    /// <summary>
    /// 模板匹配静态工具，负责图像格式转换、模板学习、native 匹配调用和结果绘制。
    /// </summary>
    internal static class FastTemplateMatcher
    {
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
            /// 会话是否已经释放。
            /// </summary>
            private bool disposed;

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
                Bitmap contourMask = null;
                try
                {
                    if (param != null && param.TemplateEraseMaskBytes != null && param.TemplateEraseMaskBytes.Length > 0)
                        contourMask = PngBytesToBitmap(param.TemplateEraseMaskBytes);
                    return Match(param, sourceImage, templateImage, contourMask);
                }
                finally
                {
                    contourMask?.Dispose();
                }
            }

            /// <summary>
            /// 执行模板匹配并使用调用方提供的涂抹蒙版计算显示轮廓。
            /// </summary>
            public FastTemplateMatchResult Match(NodeParamMatchTemplate param, Bitmap sourceImage, Bitmap templateImage, Bitmap contourMask)
            {
                if (param == null)
                    throw new ArgumentNullException(nameof(param));
                if (sourceImage == null)
                    throw new ArgumentNullException(nameof(sourceImage));
                if (templateImage == null)
                    throw new ArgumentNullException(nameof(templateImage));

                lock (syncRoot)
                {
                    ThrowIfDisposed();

                    var result = new FastTemplateMatchResult();
                    var sw = Stopwatch.StartNew();

                    using (var source = To24Bpp(sourceImage))
                    using (var template = To24Bpp(templateImage))
                    {
                        EnsureTemplateLearned(template);

                        int maxResults = Math.Max(1, Math.Min(1000, param.ResultNum));
                        double[] xs = new double[maxResults];
                        double[] ys = new double[maxResults];
                        double[] angles = new double[maxResults];
                        double[] scores = new double[maxResults];

                        double expectedScore = NormalizeScore(param.MinScore);
                        double maxOverlap = NormalizeRatio(param.MaxOverlap);
                        double toleranceAngle = Math.Abs(param.ToleranceAngle);

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

                        List<PointF> contour = null;
                        if (param.ShowOutlineStatus)
                            contour = GetTemplateContour(template, contourMask);

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

                    sw.Stop();
                    result.AlgorithmMs = sw.Elapsed.TotalMilliseconds;
                    return result;
                }
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
                    engine = new FastMatchEngineHandle();

                bool sizeChanged = template.Width != learnedTemplateWidth || template.Height != learnedTemplateHeight;
                if (templateLearned && learnedTemplateVersion == templateVersion && !sizeChanged)
                    return;

                engine.Learn(template);
                templateLearned = true;
                learnedTemplateVersion = templateVersion;
                learnedTemplateWidth = template.Width;
                learnedTemplateHeight = template.Height;
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
            private IntPtr _engine;

            public FastMatchEngineHandle()
            {
                _engine = FastMatchNative.CreateMatchEngine();
                if (_engine == IntPtr.Zero)
                    throw new InvalidOperationException("CreateMatchEngine failed.");
            }

            public void Learn(Bitmap template)
            {
                var data = template.LockBits(new Rectangle(0, 0, template.Width, template.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                try
                {
                    int code = FastMatchNative.LearnPatternMem(_engine, data.Scan0, template.Width, template.Height, data.Stride, 3);
                    if (code != 0)
                        throw new InvalidOperationException("LearnPatternMem failed: " + code);
                }
                finally
                {
                    template.UnlockBits(data);
                }
            }

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
                var data = source.LockBits(new Rectangle(0, 0, source.Width, source.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                try
                {
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
                    return Math.Max(0, Math.Min(count, maxResults));
                }
                finally
                {
                    source.UnlockBits(data);
                }
            }

            public void Dispose()
            {
                if (_engine == IntPtr.Zero)
                    return;

                FastMatchNative.ReleaseMatchEngine(_engine);
                _engine = IntPtr.Zero;
            }
        }

        private static class FastMatchNative
        {
            static FastMatchNative()
            {
                string nativeDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Native");
                if (Directory.Exists(nativeDirectory))
                    SetDllDirectory(nativeDirectory);
            }

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            private static extern bool SetDllDirectory(string lpPathName);

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
