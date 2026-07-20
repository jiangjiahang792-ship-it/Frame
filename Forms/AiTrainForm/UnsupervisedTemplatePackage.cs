using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;

namespace TDJS_Vision.Forms.AiTrainForm
{
    /// <summary>
    /// 无监督模板单文件打包器。
    /// </summary>
    public static class UnsupervisedTemplatePackage
    {
        /// <summary>
        /// 创建包含模型和参数清单的模板文件。
        /// </summary>
        /// <param name="templatePath">模板输出路径。</param>
        /// <param name="manifest">模板清单。</param>
        /// <param name="onnxPath">训练得到的 ONNX 文件。</param>
        public static void Create(string templatePath, UnsupervisedTemplateManifest manifest, string onnxPath)
        {
            if (string.IsNullOrWhiteSpace(templatePath))
                throw new ArgumentException("模板输出路径不能为空。", nameof(templatePath));
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));
            if (string.IsNullOrWhiteSpace(onnxPath) || !File.Exists(onnxPath))
                throw new FileNotFoundException("未找到训练生成的 ONNX 模型。", onnxPath);

            string directory = Path.GetDirectoryName(templatePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (FileStream stream = new FileStream(templatePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteManifest(archive, manifest);
                WriteModel(archive, onnxPath);
            }
        }

        /// <summary>
        /// 解出训练窗口生成的模板包，供识别节点加载模型与 ROI 参数。
        /// </summary>
        /// <param name="templatePath">模板包路径。</param>
        /// <param name="extractRoot">解包缓存根目录。</param>
        /// <returns>模板解包结果。</returns>
        public static UnsupervisedTemplateExtractResult Extract(string templatePath, string extractRoot)
        {
            if (string.IsNullOrWhiteSpace(templatePath))
                throw new ArgumentException("模板路径不能为空。", nameof(templatePath));
            if (!File.Exists(templatePath))
                throw new FileNotFoundException("未找到无监督模板文件。", templatePath);
            if (string.IsNullOrWhiteSpace(extractRoot))
                throw new ArgumentException("模板解包目录不能为空。", nameof(extractRoot));

            string fullTemplatePath = Path.GetFullPath(templatePath);
            string cacheName = SafeFileName(Path.GetFileNameWithoutExtension(fullTemplatePath)) + "_" + File.GetLastWriteTimeUtc(fullTemplatePath).Ticks.ToString(CultureInfo.InvariantCulture);
            string targetRoot = Path.Combine(Path.GetFullPath(extractRoot), cacheName);
            Directory.CreateDirectory(targetRoot);

            string manifestPath = Path.Combine(targetRoot, "manifest.json");
            string modelPath = Path.Combine(targetRoot, "model.onnx");
            if (File.Exists(manifestPath) && File.Exists(modelPath))
            {
                return new UnsupervisedTemplateExtractResult(targetRoot, modelPath, ReadManifestFromFile(manifestPath));
            }

            using (FileStream stream = new FileStream(fullTemplatePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                ZipArchiveEntry manifestEntry = archive.GetEntry("manifest.json");
                ZipArchiveEntry modelEntry = archive.GetEntry("model.onnx");
                if (manifestEntry == null)
                    throw new InvalidDataException("无监督模板缺少 manifest.json。");
                if (modelEntry == null)
                    throw new InvalidDataException("无监督模板缺少 model.onnx。");

                ExtractEntry(manifestEntry, manifestPath);
                ExtractEntry(modelEntry, modelPath);
            }

            return new UnsupervisedTemplateExtractResult(targetRoot, modelPath, ReadManifestFromFile(manifestPath));
        }

        /// <summary>
        /// 直接从模板包读取清单，不解包 ONNX 模型文件。
        /// </summary>
        /// <param name="templatePath">模板包路径。</param>
        /// <returns>模板清单。</returns>
        public static UnsupervisedTemplateManifest ReadManifest(string templatePath)
        {
            if (string.IsNullOrWhiteSpace(templatePath))
                throw new ArgumentException("模板路径不能为空。", nameof(templatePath));
            if (!File.Exists(templatePath))
                throw new FileNotFoundException("未找到无监督模板文件。", templatePath);

            string fullTemplatePath = Path.GetFullPath(templatePath);
            using (FileStream stream = new FileStream(fullTemplatePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                ZipArchiveEntry manifestEntry = archive.GetEntry("manifest.json");
                if (manifestEntry == null)
                    throw new InvalidDataException("无监督模板缺少 manifest.json。");

                using (Stream entryStream = manifestEntry.Open())
                using (var reader = new StreamReader(entryStream, Encoding.UTF8, true))
                {
                    string json = reader.ReadToEnd();
                    UnsupervisedTemplateManifest manifest = JsonConvert.DeserializeObject<UnsupervisedTemplateManifest>(json);
                    if (manifest == null)
                        throw new InvalidDataException("无监督模板清单解析失败。");

                    return manifest;
                }
            }
        }

        /// <summary>
        /// 写入模板清单。
        /// </summary>
        /// <param name="archive">模板压缩包。</param>
        /// <param name="manifest">模板清单。</param>
        private static void WriteManifest(ZipArchive archive, UnsupervisedTemplateManifest manifest)
        {
            ZipArchiveEntry entry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
            using (Stream entryStream = entry.Open())
            using (var writer = new StreamWriter(entryStream, new UTF8Encoding(false)))
            {
                string json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
                writer.Write(json);
            }
        }

        /// <summary>
        /// 写入模型文件。
        /// </summary>
        /// <param name="archive">模板压缩包。</param>
        /// <param name="onnxPath">ONNX 模型路径。</param>
        private static void WriteModel(ZipArchive archive, string onnxPath)
        {
            ZipArchiveEntry entry = archive.CreateEntry("model.onnx", CompressionLevel.Optimal);
            using (Stream entryStream = entry.Open())
            using (FileStream modelStream = new FileStream(onnxPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                modelStream.CopyTo(entryStream);
            }
        }

        /// <summary>
        /// 从模板包缓存文件中读取清单。
        /// </summary>
        /// <param name="manifestPath">清单路径。</param>
        /// <returns>模板清单。</returns>
        private static UnsupervisedTemplateManifest ReadManifestFromFile(string manifestPath)
        {
            string json = File.ReadAllText(manifestPath, Encoding.UTF8);
            UnsupervisedTemplateManifest manifest = JsonConvert.DeserializeObject<UnsupervisedTemplateManifest>(json);
            if (manifest == null)
                throw new InvalidDataException("无监督模板清单解析失败。");
            return manifest;
        }

        /// <summary>
        /// 将指定模板包条目写入目标文件。
        /// </summary>
        /// <param name="entry">压缩包条目。</param>
        /// <param name="targetPath">目标文件路径。</param>
        private static void ExtractEntry(ZipArchiveEntry entry, string targetPath)
        {
            using (Stream source = entry.Open())
            using (FileStream target = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                source.CopyTo(target);
            }
        }

        /// <summary>
        /// 将文件名转为可用于缓存目录的安全文本。
        /// </summary>
        /// <param name="name">原始文件名。</param>
        /// <returns>安全文件名。</returns>
        private static string SafeFileName(string name)
        {
            string value = string.IsNullOrWhiteSpace(name) ? "template" : name.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(c, '_');
            }
            return value;
        }
    }

    /// <summary>
    /// 无监督模板解包结果。
    /// </summary>
    public sealed class UnsupervisedTemplateExtractResult
    {
        /// <summary>
        /// 初始化模板解包结果。
        /// </summary>
        /// <param name="extractRoot">解包目录。</param>
        /// <param name="modelPath">解出的 ONNX 模型路径。</param>
        /// <param name="manifest">模板清单。</param>
        public UnsupervisedTemplateExtractResult(string extractRoot, string modelPath, UnsupervisedTemplateManifest manifest)
        {
            ExtractRoot = extractRoot;
            ModelPath = modelPath;
            Manifest = manifest;
        }

        /// <summary>
        /// 模板缓存解包目录。
        /// </summary>
        public string ExtractRoot { get; }

        /// <summary>
        /// 解出的 ONNX 模型路径。
        /// </summary>
        public string ModelPath { get; }

        /// <summary>
        /// 模板清单。
        /// </summary>
        public UnsupervisedTemplateManifest Manifest { get; }
    }
}
