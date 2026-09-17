using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using OpenCvSharp;

namespace TDJS_Vision.Node._3_Detection.ContourMatch
{
    /// <summary>可替换的轮廓匹配算法接口；每个实例由会话串行调用。</summary>
    public interface IShapeMatcher : IDisposable
    {
        /// <summary>是否已经成功创建模型。</summary>
        bool HasModel { get; }
        /// <summary>当前模型实际使用的原图层Canny高阈值。</summary>
        double ModelContrast { get; }
        /// <summary>从Mat及矩形区域建立轮廓模型，保留Demo原始参数语义。</summary>
        void CreateModel(Mat image, Rectangle roi, CreateModelOptions options);
        /// <summary>以ROI局部遮罩删除已有模型特征。</summary>
        void EraseModelFeatures(byte[] mask, int width, int height);
        /// <summary>读取参与匹配的ROI局部特征点。</summary>
        IReadOnlyList<PointF> GetModelFeatures();
        /// <summary>读取ROI局部完整有序轮廓。</summary>
        IReadOnlyList<PointF[]> GetModelContours();
        /// <summary>直接从Mat的原始指针及真实步长搜索，不复制整图。</summary>
        IReadOnlyList<ShapeMatchResult> Find(Mat image, FindOptions options);
    }

    /// <summary>Demo原生C ABI适配器，句柄延迟创建以支持WinForms设计器及无模型方案。</summary>
    public sealed class NativeShapeMatcher : IMaskedShapeMatcher
    {
        /// <summary>由SafeHandle独占的原生模型。</summary>
        private ShapeMatcherSafeHandle _handle;
        /// <summary>防止释放后重新创建句柄。</summary>
        private bool _disposed;
        /// <summary>获取已建模状态，不触发DLL加载。</summary>
        public bool HasModel { get { return !_disposed && _handle != null && !_handle.IsClosed && NativeMethods.HasModel(_handle) != 0; } }
        /// <summary>读取实际阈值。</summary>
        public double ModelContrast
        {
            get
            {
                if (!HasModel) return 0;
                double contrast;
                if (NativeMethods.GetModelContrast(_handle, out contrast) == 0) throw NativeError("读取模型阈值失败");
                return contrast;
            }
        }

        /// <summary>创建模型所需的参数校验；与Demo控件可输入范围保持一致。</summary>
        public static void ValidateCreate(CreateModelOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            ValidateAngles(options.AngleStartDegrees, options.AngleEndDegrees);
            if (!ContourCompatibility.IsFinite(options.AngleStepDegrees) || options.AngleStepDegrees < 0.1 || options.AngleStepDegrees > 180 ||
                !ContourCompatibility.IsFinite(options.Contrast) || options.Contrast < 1 || options.Contrast > 1024 ||
                !ContourCompatibility.IsFinite(options.MinimumContrast) || options.MinimumContrast < 0 || options.MinimumContrast > 1024 ||
                options.PyramidLevels < 0 || options.PyramidLevels > 6 || options.FeatureCount < 0 || options.FeatureCount > 10000 ||
                !Enum.IsDefined(typeof(ShapeMetric), options.Metric))
                throw new ArgumentException("模板创建参数无效，请检查角度步长、阈值、层数和特征数。");
        }

        /// <summary>验证搜索参数，分数和重叠率保持Demo的0～1范围。</summary>
        public static void ValidateFind(FindOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            ValidateAngles(options.AngleStartDegrees, options.AngleEndDegrees);
            if (!ContourCompatibility.IsFinite(options.MinimumScore) || options.MinimumScore < 0 || options.MinimumScore > 1 ||
                !ContourCompatibility.IsFinite(options.MaximumOverlap) || options.MaximumOverlap < 0 || options.MaximumOverlap > 1 ||
                options.MaximumMatches < 1 || options.MaximumMatches > 100 || options.PyramidLevels < 0 || options.PyramidLevels > 6 ||
                !Enum.IsDefined(typeof(ShapeMatchMode), options.Mode))
                throw new ArgumentException("搜索参数无效，请检查分数、数量、重叠率、层数和精度模式。");
        }

        /// <summary>验证角度范围，拒绝非有限坐标。</summary>
        private static void ValidateAngles(double start, double end)
        {
            if (!ContourCompatibility.IsFinite(start) || !ContourCompatibility.IsFinite(end) || start < -180 || end > 180 || start > end)
                throw new ArgumentException("起始角不能大于结束角，角度范围为-180～180度。");
        }

        /// <summary>检查图像深度和通道；允许非连续ROI，原生函数按真实行步长访问。</summary>
        private static void ValidateImage(Mat image)
        {
            if (image == null || image.IsDisposed || image.Empty() ||
                (image.Type() != MatType.CV_8UC1 && image.Type() != MatType.CV_8UC3 && image.Type() != MatType.CV_8UC4))
                throw new ArgumentException("轮廓匹配需要非空的8位灰度、BGR或BGRA图像。");
        }

        /// <summary>首次调用时加载原生实例，并检查ABI版本。</summary>
        private void EnsureHandle()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NativeShapeMatcher));
            if (_handle != null) return;
            if (!Environment.Is64BitProcess) throw new InvalidOperationException("轮廓模板匹配需要64位进程。");
            if (NativeMethods.GetAbiVersion() != 1) throw new InvalidOperationException("轮廓模板匹配DLL版本不兼容。");
            IntPtr pointer = NativeMethods.Create();
            if (pointer == IntPtr.Zero) throw new InvalidOperationException("无法创建轮廓模板匹配实例。");
            _handle = new ShapeMatcherSafeHandle(pointer);
        }

        /// <summary>按Demo参数直接创建模型；生产会话使用新实例成功后再替换旧实例。</summary>
        public void CreateModel(Mat image, Rectangle roi, CreateModelOptions options)
        {
            CreateMaskedModel(image, roi, options, null);
        }

        /// <summary>复用原生已有整图掩膜入口，固定内存仅覆盖同步建模调用。</summary>
        public void CreateMaskedModel(Mat image, Rectangle roi, CreateModelOptions options, byte[] mask)
        {
            ValidateImage(image); ValidateCreate(options);
            if (mask != null && mask.Length != checked(image.Width * image.Height))
                throw new ArgumentException("模板掩膜必须与来源图像尺寸一致。");
            if (roi.Width < 8 || roi.Height < 8 || roi.X < 0 || roi.Y < 0 ||
                (long)roi.X + roi.Width > image.Width || (long)roi.Y + roi.Height > image.Height)
                throw new ArgumentException("模板ROI必须位于图像内，且至少为8×8像素。");
            EnsureHandle();
            GCHandle pinned = default(GCHandle);
            try
            {
            if (mask != null) pinned = GCHandle.Alloc(mask, GCHandleType.Pinned);
            if (NativeMethods.CreateModel(_handle, image.Data, image.Width, image.Height, checked((int)image.Step()), image.Channels(),
                roi.X, roi.Y, roi.Width, roi.Height, options.PyramidLevels, options.AngleStartDegrees, options.AngleEndDegrees,
                options.AngleStepDegrees, options.Metric == ShapeMetric.忽略极性 ? "ignore_polarity" : "use_polarity",
                options.AutoContrast ? 0 : options.Contrast, options.MinimumContrast, options.FeatureCount,
                mask == null ? IntPtr.Zero : pinned.AddrOfPinnedObject(), mask == null ? 0 : image.Width) == 0)
                throw NativeError("创建轮廓模板失败");
            GC.KeepAlive(image);
            }
            finally { if (pinned.IsAllocated) pinned.Free(); }
        }

        /// <summary>删除ROI局部特征；原生接口确保失败时旧模型保持有效。</summary>
        public void EraseModelFeatures(byte[] mask, int width, int height)
        {
            if (!HasModel) throw new InvalidOperationException("请先创建模板。");
            if (width <= 0 || height <= 0 || mask == null || mask.Length != checked(width * height))
                throw new ArgumentException("删除遮罩尺寸与模板不一致。");
            if (NativeMethods.EraseFeatures(_handle, mask, width, height, width) == 0)
                throw NativeError("删除特征失败，至少需要保留8个特征");
        }

        /// <summary>获取ROI局部特征点。</summary>
        public IReadOnlyList<PointF> GetModelFeatures()
        {
            if (!HasModel) return Array.Empty<PointF>();
            int count;
            if (NativeMethods.GetFeatures(_handle, null, 0, out count) == 0) throw NativeError("读取模板特征失败");
            float[] xy = new float[checked(count * 2)];
            int written;
            if (NativeMethods.GetFeatures(_handle, xy, count, out written) == 0) throw NativeError("复制模板特征失败");
            PointF[] result = new PointF[written];
            for (int i = 0; i < written; i++) result[i] = new PointF(xy[i * 2], xy[i * 2 + 1]);
            return result;
        }

        /// <summary>获取分段有序轮廓，避免下游错误连接不同轮廓。</summary>
        public IReadOnlyList<PointF[]> GetModelContours()
        {
            if (!HasModel) return Array.Empty<PointF[]>();
            int count;
            if (NativeMethods.GetContours(_handle, null, null, 0, out count) == 0) throw NativeError("读取模板轮廓失败");
            float[] xy = new float[checked(count * 2)]; int[] ids = new int[count]; int written;
            if (NativeMethods.GetContours(_handle, xy, ids, count, out written) == 0) throw NativeError("复制模板轮廓失败");
            var paths = new List<PointF[]>();
            for (int first = 0; first < written;)
            {
                int end = first + 1;
                while (end < written && ids[end] == ids[first]) end++;
                var path = new PointF[end - first + 1];
                for (int i = first; i < end; i++) path[i - first] = new PointF(xy[i * 2], xy[i * 2 + 1]);
                path[path.Length - 1] = path[0]; paths.Add(path); first = end;
            }
            return paths;
        }

        /// <summary>保持Demo搜索参数和分数语义，直接传递Mat，不执行Bitmap往返。</summary>
        public IReadOnlyList<ShapeMatchResult> Find(Mat image, FindOptions options)
        {
            ValidateImage(image); ValidateFind(options);
            if (!HasModel) throw new InvalidOperationException("请先创建轮廓模板。");
            var buffer = new NativeMatch[options.MaximumMatches];
            GCHandle pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                int count;
                if (NativeMethods.Find(_handle, image.Data, image.Width, image.Height, checked((int)image.Step()), image.Channels(),
                    options.AngleStartDegrees, options.AngleEndDegrees, options.MinimumScore, options.MaximumMatches,
                    options.MaximumOverlap, options.SubPixel ? 1 : 0, options.PyramidLevels, (int)options.Mode,
                    pin.AddrOfPinnedObject(), buffer.Length, out count) == 0) throw NativeError("轮廓搜索失败");
                if (count < 0 || count > buffer.Length) throw new InvalidOperationException("原生匹配结果数量异常。");
                var result = new List<ShapeMatchResult>(count);
                for (int i = 0; i < count; i++)
                {
                    NativeMatch match = buffer[i];
                    result.Add(new ShapeMatchResult { CenterX = match.CenterX, CenterY = match.CenterY,
                        AngleDegrees = match.AngleDegrees, Score = match.Score, Width = match.Width, Height = match.Height });
                }
                return result;
            }
            finally { pin.Free(); GC.KeepAlive(image); }
        }

        /// <summary>解码原生UTF-8错误消息，不依赖.NET 8的封送API。</summary>
        private Exception NativeError(string operation)
        {
            IntPtr pointer = NativeMethods.GetLastMessage(_handle);
            string detail = "未知原生错误";
            if (pointer != IntPtr.Zero)
            {
                int length = 0;
                while (length < 16384 && Marshal.ReadByte(pointer, length) != 0) length++;
                var bytes = new byte[length]; Marshal.Copy(pointer, bytes, 0, length);
                detail = Encoding.UTF8.GetString(bytes);
            }
            return new InvalidOperationException(operation + "：" + detail);
        }

        /// <summary>释放原生模型及金字塔缓存。</summary>
        public void Dispose() { if (_disposed) return; _disposed = true; _handle?.Dispose(); _handle = null; }
    }
}
