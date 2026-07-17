using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace TDJS_Vision.Forms.AiTrainForm
{
    /// <summary>
    /// 定义模板打包读取模型和 Bank 源文件的可替换流工厂。
    /// </summary>
    internal interface ILargeModelTemplateSourceStreamFactory
    {
        /// <summary>
        /// 以只读方式打开指定模板源文件。
        /// </summary>
        /// <param name="path">模型或 memory bank 路径。</param>
        /// <returns>可读取的源流。</returns>
        Stream OpenRead(string path);
    }

    /// <summary>
    /// 使用标准文件流读取模板源文件。
    /// </summary>
    internal sealed class FileLargeModelTemplateSourceStreamFactory : ILargeModelTemplateSourceStreamFactory
    {
        /// <summary>
        /// 以允许其它读取者共享的方式打开模板源文件。
        /// </summary>
        /// <param name="path">模型或 memory bank 路径。</param>
        /// <returns>只读文件流。</returns>
        public Stream OpenRead(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    }

    /// <summary>
    /// 定义生产训练阶段使用的可替换模板发布边界。
    /// </summary>
    internal interface ILargeModelTemplatePublisher
    {
        /// <summary>
        /// 发布本次模型、memory bank 和清单组成的模板。
        /// </summary>
        /// <param name="templatePath">正式模板路径。</param>
        /// <param name="manifest">模板清单。</param>
        /// <param name="modelPath">本次模型路径。</param>
        /// <param name="bankPath">本次 memory bank 路径。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        void Publish(
            string templatePath,
            LargeModelTemplateManifest manifest,
            string modelPath,
            string bankPath,
            IProgress<string> log,
            System.Threading.CancellationToken token);
    }

    /// <summary>
    /// 使用标准大模型模板包格式发布正式模板。
    /// </summary>
    internal sealed class LargeModelTemplatePublisher : ILargeModelTemplatePublisher
    {
        /// <summary>
        /// 模板源文件流工厂。
        /// </summary>
        private readonly ILargeModelTemplateSourceStreamFactory _sourceStreamFactory;

        /// <summary>
        /// 使用标准文件流初始化模板发布器。
        /// </summary>
        public LargeModelTemplatePublisher()
            : this(new FileLargeModelTemplateSourceStreamFactory())
        {
        }

        /// <summary>
        /// 使用可替换模板源流初始化发布器。
        /// </summary>
        /// <param name="sourceStreamFactory">模板源文件流工厂。</param>
        internal LargeModelTemplatePublisher(ILargeModelTemplateSourceStreamFactory sourceStreamFactory)
        {
            _sourceStreamFactory = sourceStreamFactory ?? throw new ArgumentNullException(nameof(sourceStreamFactory));
        }

        /// <summary>
        /// 发布标准大模型模板包。
        /// </summary>
        /// <param name="templatePath">正式模板路径。</param>
        /// <param name="manifest">模板清单。</param>
        /// <param name="modelPath">本次模型路径。</param>
        /// <param name="bankPath">本次 memory bank 路径。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        public void Publish(
            string templatePath,
            LargeModelTemplateManifest manifest,
            string modelPath,
            string bankPath,
            IProgress<string> log,
            System.Threading.CancellationToken token)
        {
            LargeModelTemplatePackage.Create(
                templatePath,
                manifest,
                modelPath,
                bankPath,
                log,
                token,
                _sourceStreamFactory);
        }
    }

    /// <summary>
    /// 大模型模板单文件打包器。
    /// </summary>
    public static class LargeModelTemplatePackage
    {
        /// <summary>
        /// 模板包内清单文件名。
        /// </summary>
        private const string ManifestEntryName = "manifest.json";

        /// <summary>
        /// 模板包内 memory bank 文件名。
        /// </summary>
        private const string BankEntryName = "bank.bin";

        /// <summary>
        /// 创建包含 native 模型、memory bank 和参数清单的模板文件。
        /// </summary>
        /// <param name="templatePath">模板输出路径。</param>
        /// <param name="manifest">模板清单。</param>
        /// <param name="modelPath">训练使用的 engine 或 ONNX 模型文件。</param>
        /// <param name="bankPath">训练得到的 memory bank 文件。</param>
        public static void Create(string templatePath, LargeModelTemplateManifest manifest, string modelPath, string bankPath)
        {
            Create(
                templatePath,
                manifest,
                modelPath,
                bankPath,
                null,
                CancellationToken.None,
                new FileLargeModelTemplateSourceStreamFactory());
        }

        /// <summary>
        /// 使用同目录唯一临时 ZIP 创建、验证并原子发布模板文件。
        /// </summary>
        /// <param name="templatePath">正式模板输出路径。</param>
        /// <param name="manifest">模板清单。</param>
        /// <param name="modelPath">训练使用的 engine 或 ONNX 模型文件。</param>
        /// <param name="bankPath">训练得到的 memory bank 文件。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        /// <param name="sourceStreamFactory">模型和 Bank 源文件流工厂。</param>
        internal static void Create(
            string templatePath,
            LargeModelTemplateManifest manifest,
            string modelPath,
            string bankPath,
            IProgress<string> log,
            CancellationToken token,
            ILargeModelTemplateSourceStreamFactory sourceStreamFactory)
        {
            if (string.IsNullOrWhiteSpace(templatePath))
                throw new ArgumentException("模板输出路径不能为空。", nameof(templatePath));
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));
            if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
                throw new FileNotFoundException("未找到训练使用的 native 模型文件。", modelPath);
            if (string.IsNullOrWhiteSpace(bankPath) || !File.Exists(bankPath))
                throw new FileNotFoundException("未找到训练生成的 memory bank。", bankPath);
            if (sourceStreamFactory == null)
                throw new ArgumentNullException(nameof(sourceStreamFactory));

            token.ThrowIfCancellationRequested();
            string fullTemplatePath = Path.GetFullPath(templatePath);
            string directory = Path.GetDirectoryName(fullTemplatePath);
            Directory.CreateDirectory(directory);

            manifest.BankFileName = BankEntryName;
            manifest.ModelFileName = Path.GetFileName(modelPath);
            string temporaryPath = BuildTemporaryTemplatePath(fullTemplatePath);
            try
            {
                token.ThrowIfCancellationRequested();
                WriteTemporaryPackage(
                    temporaryPath,
                    manifest,
                    modelPath,
                    bankPath,
                    sourceStreamFactory,
                    token);
                token.ThrowIfCancellationRequested();
                ValidateTemporaryPackage(temporaryPath, manifest.ModelFileName, token);
                token.ThrowIfCancellationRequested();
                ReplaceTemplateFile(temporaryPath, fullTemplatePath);
                // 原子提交后不再检查令牌，避免模板已经成功更新却向调用方报告取消。
            }
            catch
            {
                TryDeleteTemporaryTemplate(temporaryPath, log);
                throw;
            }
        }

        /// <summary>
        /// 构建与正式模板同目录的 GUID 唯一临时文件路径。
        /// </summary>
        /// <param name="templatePath">正式模板完整路径。</param>
        /// <returns>唯一临时模板路径。</returns>
        private static string BuildTemporaryTemplatePath(string templatePath)
        {
            string directory = Path.GetDirectoryName(templatePath);
            string extension = Path.GetExtension(templatePath);
            string fileName = Path.GetFileNameWithoutExtension(templatePath)
                + "_" + Guid.NewGuid().ToString("N") + ".building" + extension;
            return Path.Combine(directory, fileName);
        }

        /// <summary>
        /// 把清单、模型和 Bank 写入尚未发布的临时 ZIP。
        /// </summary>
        /// <param name="temporaryPath">临时模板路径。</param>
        /// <param name="manifest">模板清单。</param>
        /// <param name="modelPath">本次模型路径。</param>
        /// <param name="bankPath">本次 Bank 路径。</param>
        /// <param name="sourceStreamFactory">模板源文件流工厂。</param>
        /// <param name="token">取消令牌。</param>
        private static void WriteTemporaryPackage(
            string temporaryPath,
            LargeModelTemplateManifest manifest,
            string modelPath,
            string bankPath,
            ILargeModelTemplateSourceStreamFactory sourceStreamFactory,
            CancellationToken token)
        {
            using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteManifest(archive, manifest, token);
                WriteModel(archive, modelPath, manifest.ModelFileName, sourceStreamFactory, token);
                WriteBank(archive, bankPath, sourceStreamFactory, token);
            }
        }

        /// <summary>
        /// 解出训练窗口生成的模板包，供流程节点加载 bank 与 ROI 参数。
        /// </summary>
        /// <param name="templatePath">模板包路径。</param>
        /// <param name="extractRoot">解包缓存根目录。</param>
        /// <returns>模板解包结果。</returns>
        public static LargeModelTemplateExtractResult Extract(string templatePath, string extractRoot)
        {
            if (string.IsNullOrWhiteSpace(templatePath))
                throw new ArgumentException("模板路径不能为空。", nameof(templatePath));
            if (!File.Exists(templatePath))
                throw new FileNotFoundException("未找到大模型模板文件。", templatePath);
            if (string.IsNullOrWhiteSpace(extractRoot))
                throw new ArgumentException("模板解包目录不能为空。", nameof(extractRoot));

            string fullTemplatePath = Path.GetFullPath(templatePath);
            string cacheName = SafeFileName(Path.GetFileNameWithoutExtension(fullTemplatePath)) + "_" + File.GetLastWriteTimeUtc(fullTemplatePath).Ticks.ToString(CultureInfo.InvariantCulture);
            string targetRoot = Path.Combine(Path.GetFullPath(extractRoot), cacheName);
            Directory.CreateDirectory(targetRoot);

            string manifestPath = Path.Combine(targetRoot, ManifestEntryName);
            string bankPath = Path.Combine(targetRoot, BankEntryName);
            if (File.Exists(manifestPath) && File.Exists(bankPath))
            {
                LargeModelTemplateManifest cachedManifest = ReadManifestFromFile(manifestPath);
                string cachedModelPath = ResolveExtractedModelPath(targetRoot, cachedManifest);
                if (string.IsNullOrWhiteSpace(cachedManifest.ModelFileName) || File.Exists(cachedModelPath))
                    return new LargeModelTemplateExtractResult(targetRoot, bankPath, cachedModelPath, cachedManifest);
            }

            LargeModelTemplateManifest manifest;
            string modelPath;
            using (FileStream stream = new FileStream(fullTemplatePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                ZipArchiveEntry manifestEntry = archive.GetEntry(ManifestEntryName);
                ZipArchiveEntry bankEntry = archive.GetEntry(BankEntryName);
                if (manifestEntry == null)
                    throw new InvalidDataException("大模型模板缺少 manifest.json。");
                if (bankEntry == null)
                    throw new InvalidDataException("大模型模板缺少 bank.bin。");

                ExtractEntry(manifestEntry, manifestPath);
                manifest = ReadManifestFromFile(manifestPath);
                modelPath = ExtractModelIfPresent(archive, targetRoot, manifest);
                ExtractEntry(bankEntry, bankPath);
            }

            return new LargeModelTemplateExtractResult(targetRoot, bankPath, modelPath, manifest);
        }

        /// <summary>
        /// 直接从模板包读取清单，不解包模型与 memory bank 大文件。
        /// </summary>
        /// <param name="templatePath">模板包路径。</param>
        /// <returns>模板清单。</returns>
        public static LargeModelTemplateManifest ReadManifest(string templatePath)
        {
            if (string.IsNullOrWhiteSpace(templatePath))
                throw new ArgumentException("模板路径不能为空。", nameof(templatePath));
            if (!File.Exists(templatePath))
                throw new FileNotFoundException("未找到大模型模板文件。", templatePath);

            string fullTemplatePath = Path.GetFullPath(templatePath);
            using (FileStream stream = new FileStream(fullTemplatePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                ZipArchiveEntry manifestEntry = archive.GetEntry(ManifestEntryName);
                if (manifestEntry == null)
                    throw new InvalidDataException("大模型模板缺少 manifest.json。");

                using (Stream entryStream = manifestEntry.Open())
                using (var reader = new StreamReader(entryStream, Encoding.UTF8, true))
                {
                    string json = reader.ReadToEnd();
                    LargeModelTemplateManifest manifest = JsonConvert.DeserializeObject<LargeModelTemplateManifest>(json);
                    if (manifest == null)
                        throw new InvalidDataException("大模型模板清单解析失败。");

                    return manifest;
                }
            }
        }

        /// <summary>
        /// 写入模板清单。
        /// </summary>
        /// <param name="archive">模板压缩包。</param>
        /// <param name="manifest">模板清单。</param>
        /// <param name="token">取消令牌。</param>
        private static void WriteManifest(
            ZipArchive archive,
            LargeModelTemplateManifest manifest,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ZipArchiveEntry entry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
            using (Stream entryStream = entry.Open())
            using (var writer = new StreamWriter(entryStream, new UTF8Encoding(false)))
            {
                string json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
                writer.Write(json);
            }
            token.ThrowIfCancellationRequested();
        }

        /// <summary>
        /// 写入 native 模型文件。
        /// </summary>
        /// <param name="archive">模板压缩包。</param>
        /// <param name="modelPath">模型文件路径。</param>
        /// <param name="entryName">模板包内模型文件名。</param>
        /// <param name="sourceStreamFactory">模板源文件流工厂。</param>
        /// <param name="token">取消令牌。</param>
        private static void WriteModel(
            ZipArchive archive,
            string modelPath,
            string entryName,
            ILargeModelTemplateSourceStreamFactory sourceStreamFactory,
            CancellationToken token)
        {
            string safeEntryName = Path.GetFileName(entryName);
            ZipArchiveEntry entry = archive.CreateEntry(safeEntryName, CompressionLevel.NoCompression);
            using (Stream entryStream = entry.Open())
            using (Stream modelStream = sourceStreamFactory.OpenRead(modelPath))
            {
                CopyToWithCancellation(modelStream, entryStream, token);
            }
        }

        /// <summary>
        /// 写入 memory bank 文件。
        /// </summary>
        /// <param name="archive">模板压缩包。</param>
        /// <param name="bankPath">memory bank 路径。</param>
        /// <param name="sourceStreamFactory">模板源文件流工厂。</param>
        /// <param name="token">取消令牌。</param>
        private static void WriteBank(
            ZipArchive archive,
            string bankPath,
            ILargeModelTemplateSourceStreamFactory sourceStreamFactory,
            CancellationToken token)
        {
            ZipArchiveEntry entry = archive.CreateEntry(BankEntryName, CompressionLevel.Optimal);
            using (Stream entryStream = entry.Open())
            using (Stream bankStream = sourceStreamFactory.OpenRead(bankPath))
            {
                CopyToWithCancellation(bankStream, entryStream, token);
            }
        }

        /// <summary>
        /// 逐块复制模板源文件，并在阻塞读取返回后和每次写入后观察取消。
        /// </summary>
        /// <param name="source">模型或 Bank 源流。</param>
        /// <param name="target">ZIP 条目目标流。</param>
        /// <param name="token">取消令牌。</param>
        private static void CopyToWithCancellation(Stream source, Stream target, CancellationToken token)
        {
            byte[] buffer = new byte[81920];
            while (true)
            {
                token.ThrowIfCancellationRequested();
                int read = source.Read(buffer, 0, buffer.Length);
                token.ThrowIfCancellationRequested();
                if (read <= 0)
                    break;

                target.Write(buffer, 0, read);
                token.ThrowIfCancellationRequested();
            }
        }

        /// <summary>
        /// 验证临时 ZIP 可完整读取且包含清单、本次模型和 Bank 必要条目。
        /// </summary>
        /// <param name="temporaryPath">临时模板路径。</param>
        /// <param name="modelEntryName">本次模型条目名。</param>
        /// <param name="token">取消令牌。</param>
        private static void ValidateTemporaryPackage(
            string temporaryPath,
            string modelEntryName,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (FileStream stream = new FileStream(temporaryPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                ZipArchiveEntry manifestEntry = archive.GetEntry(ManifestEntryName);
                ZipArchiveEntry modelEntry = archive.GetEntry(Path.GetFileName(modelEntryName));
                ZipArchiveEntry bankEntry = archive.GetEntry(BankEntryName);
                if (manifestEntry == null)
                    throw new InvalidDataException("临时大模型模板缺少 manifest.json。");
                if (modelEntry == null)
                    throw new InvalidDataException("临时大模型模板缺少本次模型文件。");
                if (bankEntry == null)
                    throw new InvalidDataException("临时大模型模板缺少 bank.bin。");
                if (manifestEntry.Length <= 0 || modelEntry.Length <= 0 || bankEntry.Length <= 0)
                    throw new InvalidDataException("临时大模型模板包含空的必要条目。");

                LargeModelTemplateManifest validatedManifest;
                using (Stream manifestStream = manifestEntry.Open())
                using (var reader = new StreamReader(manifestStream, Encoding.UTF8, true, 1024, false))
                {
                    string json = reader.ReadToEnd();
                    validatedManifest = JsonConvert.DeserializeObject<LargeModelTemplateManifest>(json);
                }

                if (validatedManifest == null ||
                    !string.Equals(
                        Path.GetFileName(validatedManifest.ModelFileName),
                        Path.GetFileName(modelEntryName),
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(validatedManifest.BankFileName, BankEntryName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("临时大模型模板清单与必要条目不一致。");
                }

                ValidateReadableEntry(modelEntry, token);
                ValidateReadableEntry(bankEntry, token);
            }
        }

        /// <summary>
        /// 读完整个 ZIP 条目以触发压缩流完整性校验。
        /// </summary>
        /// <param name="entry">待验证条目。</param>
        /// <param name="token">取消令牌。</param>
        private static void ValidateReadableEntry(ZipArchiveEntry entry, CancellationToken token)
        {
            using (Stream source = entry.Open())
            {
                byte[] buffer = new byte[81920];
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    int read = source.Read(buffer, 0, buffer.Length);
                    token.ThrowIfCancellationRequested();
                    if (read <= 0)
                        break;
                }
            }
        }

        /// <summary>
        /// 使用同目录临时文件原子替换正式模板。
        /// </summary>
        /// <param name="temporaryPath">已完成并验证的临时模板。</param>
        /// <param name="templatePath">正式模板路径。</param>
        private static void ReplaceTemplateFile(string temporaryPath, string templatePath)
        {
            if (File.Exists(templatePath))
            {
                File.Replace(temporaryPath, templatePath, null, true);
                return;
            }

            File.Move(temporaryPath, templatePath);
        }

        /// <summary>
        /// 尽力清理失败或取消后遗留的临时模板，不覆盖原始异常。
        /// </summary>
        /// <param name="temporaryPath">临时模板路径。</param>
        /// <param name="log">训练日志回调。</param>
        private static void TryDeleteTemporaryTemplate(string temporaryPath, IProgress<string> log)
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (Exception ex)
            {
                TryReportLog(log, "临时大模型模板清理失败：" + temporaryPath + "。原因：" + ex.Message);
            }
        }

        /// <summary>
        /// 尽力写入模板清理日志，日志回调异常不得覆盖原始异常。
        /// </summary>
        /// <param name="log">训练日志回调。</param>
        /// <param name="message">简体中文日志内容。</param>
        private static void TryReportLog(IProgress<string> log, string message)
        {
            try
            {
                if (log != null)
                    log.Report(message);
            }
            catch
            {
                // 日志失败时保留调用方正在处理的原始打包或取消异常。
            }
        }

        /// <summary>
        /// 从模板包缓存文件中读取清单。
        /// </summary>
        /// <param name="manifestPath">清单路径。</param>
        /// <returns>模板清单。</returns>
        private static LargeModelTemplateManifest ReadManifestFromFile(string manifestPath)
        {
            string json = File.ReadAllText(manifestPath, Encoding.UTF8);
            LargeModelTemplateManifest manifest = JsonConvert.DeserializeObject<LargeModelTemplateManifest>(json);
            if (manifest == null)
                throw new InvalidDataException("大模型模板清单解析失败。");
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
        /// 按清单从模板包中解出模型文件；旧模板缺少模型时允许流程回退到 LargeModelDll。
        /// </summary>
        /// <param name="archive">模板压缩包。</param>
        /// <param name="targetRoot">解包目录。</param>
        /// <param name="manifest">模板清单。</param>
        /// <returns>解出的模型文件路径；旧模板缺少模型时为空。</returns>
        private static string ExtractModelIfPresent(ZipArchive archive, string targetRoot, LargeModelTemplateManifest manifest)
        {
            string modelPath = ResolveExtractedModelPath(targetRoot, manifest);
            if (string.IsNullOrWhiteSpace(modelPath))
                return string.Empty;

            ZipArchiveEntry modelEntry = archive.GetEntry(Path.GetFileName(manifest.ModelFileName));
            if (modelEntry == null)
                return string.Empty;

            ExtractEntry(modelEntry, modelPath);
            return modelPath;
        }

        /// <summary>
        /// 解析模型文件解包目标路径。
        /// </summary>
        /// <param name="targetRoot">解包目录。</param>
        /// <param name="manifest">模板清单。</param>
        /// <returns>模型文件路径；清单未记录时为空。</returns>
        private static string ResolveExtractedModelPath(string targetRoot, LargeModelTemplateManifest manifest)
        {
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.ModelFileName))
                return string.Empty;

            return Path.Combine(targetRoot, Path.GetFileName(manifest.ModelFileName));
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
    /// 大模型模板解包结果。
    /// </summary>
    public sealed class LargeModelTemplateExtractResult
    {
        /// <summary>
        /// 初始化模板解包结果。
        /// </summary>
        /// <param name="extractRoot">解包目录。</param>
        /// <param name="bankPath">解出的 memory bank 路径。</param>
        /// <param name="modelPath">解出的 native 模型路径。</param>
        /// <param name="manifest">模板清单。</param>
        public LargeModelTemplateExtractResult(string extractRoot, string bankPath, string modelPath, LargeModelTemplateManifest manifest)
        {
            ExtractRoot = extractRoot;
            BankPath = bankPath;
            ModelPath = modelPath;
            Manifest = manifest;
        }

        /// <summary>
        /// 模板缓存解包目录。
        /// </summary>
        public string ExtractRoot { get; }

        /// <summary>
        /// 解出的 memory bank 路径。
        /// </summary>
        public string BankPath { get; }

        /// <summary>
        /// 解出的 native 模型路径；旧模板未内置模型时可能为空。
        /// </summary>
        public string ModelPath { get; }

        /// <summary>
        /// 模板清单。
        /// </summary>
        public LargeModelTemplateManifest Manifest { get; }
    }
}
