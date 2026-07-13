using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;

namespace TDJS_Vision.Forms.AiTrainForm
{
    /// <summary>
    /// 大模型训练图片加载器，负责递归扫描图片、分类并生成缩略图。
    /// </summary>
    public static class LargeModelImageLoader
    {
        /// <summary>
        /// 支持加载的图片后缀。
        /// </summary>
        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff"
        };

        /// <summary>
        /// 递归加载目录中的训练图片。
        /// </summary>
        /// <param name="folder">图片根目录。</param>
        /// <param name="thumbnailSize">缩略图尺寸。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>图片信息列表。</returns>
        public static List<LargeModelImageItem> LoadImages(string folder, Size thumbnailSize, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return new List<LargeModelImageItem>();

            var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                .Where(IsImageFile)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var result = new List<LargeModelImageItem>(files.Count);
            foreach (string file in files)
            {
                token.ThrowIfCancellationRequested();
                Bitmap thumbnail = null;
                try
                {
                    thumbnail = CreateThumbnail(file, thumbnailSize);
                    result.Add(new LargeModelImageItem
                    {
                        FilePath = file,
                        DisplayName = Path.GetFileName(file),
                        Category = DetectCategory(file, folder),
                        Thumbnail = thumbnail
                    });
                }
                catch
                {
                    if (thumbnail != null) thumbnail.Dispose();
                }
            }

            return result;
        }

        /// <summary>
        /// 判断文件是否是支持的图片。
        /// </summary>
        /// <param name="file">文件路径。</param>
        /// <returns>是图片返回 true。</returns>
        private static bool IsImageFile(string file)
        {
            return ImageExtensions.Contains(Path.GetExtension(file));
        }

        /// <summary>
        /// 按路径或文件名标记推断 OK/NG 分类。
        /// </summary>
        /// <param name="file">图片路径。</param>
        /// <param name="root">图片根目录。</param>
        /// <returns>图片分类。</returns>
        private static LargeModelImageCategory DetectCategory(string file, string root)
        {
            string relative = file;
            if (!string.IsNullOrWhiteSpace(root) && file.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                relative = file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            string directory = Path.GetDirectoryName(relative) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(file) ?? string.Empty;
            string[] tokens = (directory + " " + name)
                .Split(new[] { '\\', '/', '_', '-', '.', ' ', '(', ')', '[', ']', '（', '）' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string token in tokens)
            {
                string value = token.Trim();
                if (value.Equals("NG", StringComparison.OrdinalIgnoreCase) ||
                    value.StartsWith("NG", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("异常") ||
                    value.Contains("不良"))
                {
                    return LargeModelImageCategory.NG;
                }
            }

            return LargeModelImageCategory.OK;
        }

        /// <summary>
        /// 创建不锁源文件的缩略图。
        /// </summary>
        /// <param name="file">图片路径。</param>
        /// <param name="size">缩略图尺寸。</param>
        /// <returns>缩略图位图。</returns>
        private static Bitmap CreateThumbnail(string file, Size size)
        {
            using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (Image image = Image.FromStream(stream, false, false))
            using (Bitmap source = new Bitmap(image))
            {
                var thumbnail = new Bitmap(size.Width, size.Height);
                using (Graphics graphics = Graphics.FromImage(thumbnail))
                using (Brush brush = new SolidBrush(Color.FromArgb(36, 38, 42)))
                {
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.FillRectangle(brush, 0, 0, size.Width, size.Height);

                    Rectangle target = CalculateFitRect(source.Size, size);
                    graphics.DrawImage(source, target);
                }

                return thumbnail;
            }
        }

        /// <summary>
        /// 计算等比缩放后的绘制区域。
        /// </summary>
        /// <param name="sourceSize">源图尺寸。</param>
        /// <param name="targetSize">目标尺寸。</param>
        /// <returns>绘制区域。</returns>
        private static Rectangle CalculateFitRect(Size sourceSize, Size targetSize)
        {
            if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
                return new Rectangle(0, 0, targetSize.Width, targetSize.Height);

            float scale = Math.Min((float)targetSize.Width / sourceSize.Width, (float)targetSize.Height / sourceSize.Height);
            int width = Math.Max(1, (int)(sourceSize.Width * scale));
            int height = Math.Max(1, (int)(sourceSize.Height * scale));
            int x = (targetSize.Width - width) / 2;
            int y = (targetSize.Height - height) / 2;
            return new Rectangle(x, y, width, height);
        }
    }
}

