using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace TDJS_Vision.Node._3_Detection.ContourMatch
{
    /// <summary>编辑画布拥有的图像快照；生产运行直接借用上游Mat，不创建此对象。</summary>
    public sealed class ImageFrame : IDisposable
    {
        /// <summary>用于原生建模和预览匹配的独立图像。</summary>
        internal Mat Mat { get; private set; }
        /// <summary>用于画布绘制的独立位图。</summary>
        public Bitmap DisplayBitmap { get; private set; }
        /// <summary>图像宽度。</summary>
        public int Width { get { return Mat.Width; } }
        /// <summary>图像高度。</summary>
        public int Height { get { return Mat.Height; } }

        /// <summary>接管已经成功解码的图像，并建立画布位图。</summary>
        private ImageFrame(Mat image)
        {
            Mat = image;
            try { DisplayBitmap = image.ToBitmap(); }
            catch { image.Dispose(); throw; }
        }

        /// <summary>加载文件并立即解除文件占用，支持中文路径。</summary>
        public static ImageFrame Load(string path) { return Decode(File.ReadAllBytes(path)); }
        /// <summary>从方案内的无损PNG数据恢复图像。</summary>
        public static ImageFrame Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) throw new ArgumentException("模板来源图像为空。");
            Mat image = Cv2.ImDecode(bytes, ImreadModes.Color);
            if (image.Empty()) { image.Dispose(); throw new ArgumentException("无法解码模板来源图像。"); }
            return new ImageFrame(image);
        }

        /// <summary>复制上游图像为界面独占快照，不占用生产帧。</summary>
        public static ImageFrame CopyFrom(Mat source)
        {
            if (source == null || source.Empty()) throw new ArgumentException("输入图像为空。");
            return new ImageFrame(source.Clone());
        }

        /// <summary>将建模来源图以PNG保存到方案，避免外部文件丢失。</summary>
        public byte[] Encode() { return Mat.ToBytes(".png"); }

        /// <summary>释放本对象独占的图像。</summary>
        public void Dispose()
        {
            DisplayBitmap?.Dispose(); DisplayBitmap = null;
            Mat?.Dispose(); Mat = null;
        }
    }

    /// <summary>.NET Framework下复用Demo交互所需的数值辅助方法。</summary>
    internal static class ContourCompatibility
    {
        /// <summary>将整数限制在指定闭区间。</summary>
        public static int Clamp(int value, int min, int max) { return Math.Max(min, Math.Min(max, value)); }
        /// <summary>将控件数值限制在指定闭区间。</summary>
        public static decimal Clamp(decimal value, decimal min, decimal max) { return Math.Max(min, Math.Min(max, value)); }
        /// <summary>判断浮点数是否为有限数值。</summary>
        public static bool IsFinite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }
}
