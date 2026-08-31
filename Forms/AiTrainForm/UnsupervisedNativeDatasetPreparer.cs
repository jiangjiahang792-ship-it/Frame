using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace TDJS_Vision.Forms.AiTrainForm
{
    /// <summary>
    /// 为无监督 native 训练准备兼容的数据集文件，隔离第三方数据拆分规则。
    /// </summary>
    public static class UnsupervisedNativeDatasetPreparer
    {
        /// <summary>
        /// native 数据模块拆分验证集和测试集所需的最少 NG 文件数。
        /// </summary>
        private const int MinimumNativeNgFileCount = 2;

        /// <summary>
        /// native 内部拆分文件的固定前缀。
        /// </summary>
        private const string NativeSplitFilePrefix = "__native_split_";

        /// <summary>
        /// native 训练支持的图像扩展名。
        /// </summary>
        private static readonly string[] SupportedImageExtensions =
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff"
        };

        /// <summary>
        /// 当客户只提供一张 NG 图时生成仅供 native 验证集/测试集拆分的副本。
        /// </summary>
        /// <param name="ngPath">NG 数据集目录。</param>
        /// <param name="logicalNgCount">客户实际提供并参与训练统计的 NG 图片数量。</param>
        /// <returns>新增的 native 兼容文件数量。</returns>
        public static int EnsureMinimumNgSplitFiles(string ngPath, int logicalNgCount)
        {
            if (logicalNgCount != 1)
                return 0;
            if (string.IsNullOrWhiteSpace(ngPath))
                throw new ArgumentException("NG 数据集目录不能为空。", nameof(ngPath));
            if (!Directory.Exists(ngPath))
                throw new DirectoryNotFoundException("未找到 NG 数据集目录：" + ngPath);

            string[] allImages = Directory.GetFiles(ngPath, "*", SearchOption.TopDirectoryOnly)
                .Where(IsSupportedImageFile)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (allImages.Length >= MinimumNativeNgFileCount)
                return 0;

            string[] sourceImages = allImages
                .Where(path => !Path.GetFileName(path).StartsWith(NativeSplitFilePrefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (sourceImages.Length != 1)
            {
                throw new InvalidOperationException(
                    "无监督训练统计为 1 张 NG 图片，但数据集目录实际可读取图片数为 "
                    + sourceImages.Length.ToString(CultureInfo.InvariantCulture)
                    + "。请重新加载训练图片。");
            }

            int addedCount = 0;
            string sourcePath = sourceImages[0];
            string extension = Path.GetExtension(sourcePath);
            for (int index = allImages.Length; index < MinimumNativeNgFileCount; index++)
            {
                string targetPath = Path.Combine(
                    ngPath,
                    NativeSplitFilePrefix + index.ToString("D2", CultureInfo.InvariantCulture) + extension);
                File.Copy(sourcePath, targetPath, true);
                addedCount++;
            }

            return addedCount;
        }

        /// <summary>
        /// 判断文件是否为 native 训练支持的图像格式。
        /// </summary>
        /// <param name="path">文件路径。</param>
        /// <returns>扩展名受支持时返回 true。</returns>
        private static bool IsSupportedImageFile(string path)
        {
            string extension = Path.GetExtension(path);
            return SupportedImageExtensions.Any(item => string.Equals(item, extension, StringComparison.OrdinalIgnoreCase));
        }
    }
}
