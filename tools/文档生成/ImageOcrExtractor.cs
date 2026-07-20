using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TDJSVision.Tools.DocumentGeneration
{
    /// <summary>
    /// 使用 Windows 内置简体中文识别引擎提取截图中的字幕和界面文字。
    /// </summary>
    internal static class ImageOcrExtractor
    {
        /// <summary>
        /// 程序入口，接收一个图片文件或图片目录。
        /// </summary>
        /// <param name="args">命令行参数。</param>
        /// <returns>成功返回 0，参数或识别引擎不可用时返回非 0。</returns>
        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            return RunAsync(args).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步读取图片并逐张输出文字识别结果。
        /// </summary>
        /// <param name="args">命令行参数。</param>
        /// <returns>执行结果代码。</returns>
        private static async Task<int> RunAsync(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("用法：ImageOcrExtractor <图片文件或图片目录>");
                return 2;
            }

            IReadOnlyList<string> imagePaths = ResolveImagePaths(args[0]);
            if (imagePaths.Count == 0)
            {
                Console.Error.WriteLine("没有找到可识别的 JPG 或 PNG 图片。");
                return 3;
            }

            OcrEngine engine = OcrEngine.TryCreateFromLanguage(new Language("zh-Hans"));
            if (engine == null)
            {
                Console.Error.WriteLine("当前系统没有可用的简体中文 OCR 引擎。");
                return 4;
            }

            foreach (string imagePath in imagePaths)
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(imagePath);
                using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                    using (SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync())
                    {
                        OcrResult result = await engine.RecognizeAsync(bitmap);
                        string normalizedText = result.Text.Replace("\r", " ").Replace("\n", " ").Trim();
                        Console.WriteLine("图片|文件={0}|文字={1}", Path.GetFileName(imagePath), normalizedText);
                    }
                }
            }

            return 0;
        }

        /// <summary>
        /// 将输入路径转换为按名称排序的图片文件列表。
        /// </summary>
        /// <param name="inputPath">图片文件或图片目录路径。</param>
        /// <returns>规范化后的图片文件列表。</returns>
        private static IReadOnlyList<string> ResolveImagePaths(string inputPath)
        {
            string fullPath = Path.GetFullPath(inputPath);
            if (File.Exists(fullPath))
            {
                return new[] { fullPath };
            }

            if (!Directory.Exists(fullPath))
            {
                return Array.Empty<string>();
            }

            return Directory
                .EnumerateFiles(fullPath)
                .Where(path =>
                {
                    string extension = Path.GetExtension(path);
                    return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                        || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                        || extension.Equals(".png", StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
