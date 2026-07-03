using System;
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
    }
}
