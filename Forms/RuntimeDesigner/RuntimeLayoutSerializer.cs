using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>界面模板的校验与存储；不使用多态类型反序列化。</summary>
    public static class RuntimeLayoutSerializer
    {
        /// <summary>限制模板和撤销快照大小，防止界面内存失控。</summary>
        public const int MaximumDocumentBytes = 16 * 1024 * 1024;
        /// <summary>单张嵌入 PNG 的最大字节数。</summary>
        private const int MaximumImageBytes = 3 * 1024 * 1024;

        /// <summary>序列化前检查所有属性和画布边界。</summary>
        public static string Serialize(RuntimeLayoutDocument document)
        {
            Validate(document);
            string json = JsonConvert.SerializeObject(document, Formatting.Indented);
            if (Encoding.UTF8.GetByteCount(json) > MaximumDocumentBytes)
                throw new InvalidDataException("界面模板不能超过 16 MB，请减少图片大小。 ");
            return json;
        }

        /// <summary>读取模板，拒绝未知版本、非法属性和损坏图片。</summary>
        public static RuntimeLayoutDocument Deserialize(string json)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > MaximumDocumentBytes)
                throw new InvalidDataException("界面模板为空或超过 16 MB。 ");
            try
            {
                var document = JsonConvert.DeserializeObject<RuntimeLayoutDocument>(json,
                    new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 24 });
                Validate(document);
                var checkedImages = new HashSet<string>();
                foreach (var element in document.Elements)
                    if (!string.IsNullOrEmpty(element.ImageData) && checkedImages.Add(element.ImageData))
                        using (DecodeImage(element.ImageData)) { }
                return document;
            }
            catch (JsonException ex) { throw new InvalidDataException("界面模板格式不正确。", ex); }
        }

        /// <summary>创建隔离副本，取消编辑不会修改生产方案。</summary>
        public static RuntimeLayoutDocument Clone(RuntimeLayoutDocument document)
        {
            return Deserialize(Serialize(document));
        }

        /// <summary>在创建画布和组件前检查所有持久化属性。</summary>
        public static void Validate(RuntimeLayoutDocument document)
        {
            if (document == null || document.Version != 1)
                throw new InvalidDataException("不支持此界面模板版本。 ");
            if (document.Width < 320 || document.Width > 7680 || document.Height < 240 || document.Height > 4320)
                throw new InvalidDataException("页面尺寸范围为 320×240 至 7680×4320。 ");
            CheckText(document.Name, 120, "页面名称");
            ParseColor(document.Background);
            if (document.Elements == null || document.Elements.Count > 200)
                throw new InvalidDataException("组件列表无效，最多允许 200 个组件。 ");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var element in document.Elements)
            {
                if (element == null || string.IsNullOrWhiteSpace(element.Id) || element.Id.Length > 64 || !ids.Add(element.Id))
                    throw new InvalidDataException("组件标识不能为空或重复。 ");
                if (!Enum.IsDefined(typeof(RuntimeWidgetKind), element.Kind))
                    throw new InvalidDataException("模板含有不支持的组件类型。 ");
                if (!Enum.IsDefined(typeof(RuntimeTextAlignment), element.TextAlignment))
                    throw new InvalidDataException("请选择有效的文本对齐方式。 ");
                if (element.Width < 48 || element.Height < 32 || element.Width > document.Width || element.Height > document.Height ||
                    element.X < 0 || element.Y < 0 || element.X > document.Width - element.Width || element.Y > document.Height - element.Height)
                    throw new InvalidDataException("组件尺寸至少为 48×32，且必须位于页面内。 ");
                if (float.IsNaN(element.FontSize) || float.IsInfinity(element.FontSize) || element.FontSize < 8 || element.FontSize > 96)
                    throw new InvalidDataException("字号范围为 8 至 96。 ");
                if (double.IsNaN(element.ElapsedMilliseconds) || double.IsInfinity(element.ElapsedMilliseconds) || element.ElapsedMilliseconds < 0 || element.ElapsedMilliseconds > 86400000)
                    throw new InvalidDataException("预览耗时必须为 0 至 86400000 毫秒。 ");
                CheckText(element.Name, 120, "组件名称");
                CheckText(element.Text, 4000, "组件文本");
                ValidateBinding(element, element.Binding, false);
                ValidateBinding(element, element.RoiBinding, true);
                if (element.RunProcessId.HasValue && (element.RunProcessId.Value < 0 || element.Kind != RuntimeWidgetKind.RunControl))
                    throw new InvalidDataException("运行流程编号无效或控件类型不匹配。 ");
                if (element.ActionProcessId.HasValue && (element.ActionProcessId.Value < 0 ||
                    (element.Kind != RuntimeWidgetKind.ManualParameters && element.Kind != RuntimeWidgetKind.OneClickLearning)))
                    throw new InvalidDataException("调参流程编号无效或控件类型不匹配。 ");
                if (element.LogProcessId.HasValue && (element.LogProcessId.Value < 0 || element.Kind != RuntimeWidgetKind.Log))
                    throw new InvalidDataException("日志流程编号无效或控件类型不匹配。 ");
                if (element.LogCapacity < 100 || element.LogCapacity > 10000 || !Enum.IsDefined(typeof(RuntimeLogLevel), element.LogMinimumLevel))
                    throw new InvalidDataException("日志保留条数范围为 100 至 10000，且必须选择有效的日志等级。 ");
                ParseColor(element.Foreground);
                ParseColor(element.Background);
                ParseColor(element.OnColor);
                ParseColor(element.OffColor);
                if (element.ImageData != null && element.ImageData.Length > MaximumImageBytes * 4 / 3 + 4)
                    throw new InvalidDataException("单张模板图片不能超过 3 MB。 ");
            }
        }

        /// <summary>拒绝不完整或类型不兼容的绑定，旧模板的空绑定继续有效。</summary>
        private static void ValidateBinding(RuntimeLayoutElement element, RuntimeDataBinding binding, bool roi)
        {
            if (binding == null) return;
            if (!Enum.IsDefined(typeof(RuntimeBindingKind), binding.Kind) || binding.ProcessId < 0 ||
                binding.NodePath == null || binding.NodePath.Length > 16 ||
                Array.Exists(binding.NodePath, id => id < 0))
                throw new InvalidDataException("数据源作用域路径无效。 ");
            bool process = binding.Kind == RuntimeBindingKind.ProcessElapsed;
            bool output = binding.Kind == RuntimeBindingKind.Image || binding.Kind == RuntimeBindingKind.Roi;
            bool compatible = roi
                ? binding.Kind == RuntimeBindingKind.Roi && element.Kind == RuntimeWidgetKind.Image
                : binding.Kind == RuntimeBindingKind.Image ? element.Kind == RuntimeWidgetKind.Image
                    : (binding.Kind == RuntimeBindingKind.ProcessElapsed || binding.Kind == RuntimeBindingKind.NodeElapsed) && element.Kind == RuntimeWidgetKind.Elapsed;
            if ((process && binding.NodePath.Length != 0) || (!process && binding.NodePath.Length == 0) ||
                !compatible)
                throw new InvalidDataException("数据源类型与控件不兼容。 ");
            CheckText(binding.PropertyPath, 256, "输出路径");
            CheckText(binding.DisplayPath, 4000, "数据源名称");
            if (output && string.IsNullOrWhiteSpace(binding.PropertyPath))
                throw new InvalidDataException("图像和ROI数据源必须选择输出项。 ");
        }

        /// <summary>限制文本长度，保留合法的简体中文和换行。</summary>
        private static void CheckText(string value, int maximum, string name)
        {
            if (value == null || value.Length > maximum)
                throw new InvalidDataException(name + "不能为空值或超过 " + maximum + " 个字符。 ");
        }

        /// <summary>只接受明确的 RGB 色值，避免导入任意颜色表达式。</summary>
        public static Color ParseColor(string value)
        {
            int rgb;
            if (value == null || value.Length != 7 || value[0] != '#' ||
                !int.TryParse(value.Substring(1), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out rgb))
                throw new InvalidDataException("颜色必须为 #RRGGBB 格式。 ");
            return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
        }

        /// <summary>将颜色保存为平台无关的 RGB 表达。</summary>
        public static string FormatColor(Color color)
        {
            return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
        }

        /// <summary>从文件加载，提前拒绝超大文件。</summary>
        public static RuntimeLayoutDocument Load(string path)
        {
            if (new FileInfo(path).Length > MaximumDocumentBytes)
                throw new InvalidDataException("界面模板不能超过 16 MB。 ");
            return Deserialize(File.ReadAllText(path, Encoding.UTF8));
        }

        /// <summary>先写临时文件再替换；失败时保留原模板。</summary>
        public static void Save(string path, RuntimeLayoutDocument document)
        {
            string json = Serialize(document);
            // 校验图片也在写文件前完成，失败时不触及旧模板。
            Deserialize(json);
            string target = Path.GetFullPath(path);
            string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(target)) File.Replace(temporary, target, null);
                else File.Move(temporary, target);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        /// <summary>读取本地图片并压缩到最长边 1600 像素，嵌入后不占用原文件。</summary>
        public static string ReadImage(string path)
        {
            if (new FileInfo(path).Length > 32 * 1024 * 1024)
                throw new InvalidDataException("请选择小于 32 MB 的图片。 ");
            using (var source = Image.FromFile(path))
            {
                CheckImageSize(source);
                double ratio = Math.Min(1, 1600.0 / Math.Max(source.Width, source.Height));
                using (var bitmap = new Bitmap(Math.Max(1, (int)(source.Width * ratio)), Math.Max(1, (int)(source.Height * ratio))))
                {
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.DrawImage(source, new Rectangle(Point.Empty, bitmap.Size));
                    }
                    using (var stream = new MemoryStream())
                    {
                        bitmap.Save(stream, ImageFormat.Png);
                        if (stream.Length > MaximumImageBytes) throw new InvalidDataException("图片压缩后仍超过 3 MB，请使用更小的图片。 ");
                        return Convert.ToBase64String(stream.ToArray());
                    }
                }
            }
        }

        /// <summary>还原独立位图，调用方负责释放，流关闭后仍可绘制。</summary>
        public static Bitmap DecodeImage(string data)
        {
            try
            {
                if (data == null || data.Length > MaximumImageBytes * 4 / 3 + 4)
                    throw new InvalidDataException("嵌入图片大小无效。 ");
                byte[] bytes = Convert.FromBase64String(data);
                using (var stream = new MemoryStream(bytes, false))
                using (var source = Image.FromStream(stream, true, true))
                {
                    CheckImageSize(source);
                    return new Bitmap(source);
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is System.Runtime.InteropServices.ExternalException)
            { throw new InvalidDataException("模板中的图片已损坏。", ex); }
        }

        /// <summary>限制解码尺寸，避免超大图像占用设计器内存。</summary>
        private static void CheckImageSize(Image image)
        {
            if (image.Width > 8192 || image.Height > 8192 || (long)image.Width * image.Height > 24000000)
                throw new InvalidDataException("图片分辨率过大，最长边不超过 8192，像素总数不超过 2400 万。 ");
        }
    }
}
