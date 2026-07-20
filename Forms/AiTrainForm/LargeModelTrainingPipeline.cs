using System;
using System.IO;
using System.Text;
using System.Threading;

namespace TDJS_Vision.Forms.AiTrainForm
{
    /// <summary>
    /// 定义一次大模型训练链中可替换的阶段操作。
    /// </summary>
    public interface ILargeModelTrainingStages
    {
        /// <summary>
        /// 准备本次训练专用的数据集和输出目录。
        /// </summary>
        /// <param name="token">取消令牌。</param>
        void PrepareDataset(CancellationToken token);

        /// <summary>
        /// 为本次训练重新生成并发布 ONNX。
        /// </summary>
        /// <param name="token">取消令牌。</param>
        /// <returns>本次新生成的 ONNX 路径。</returns>
        string RebuildOnnxModel(CancellationToken token);

        /// <summary>
        /// 使用本次 ONNX 重新生成并发布 TensorRT Engine。
        /// </summary>
        /// <param name="onnxPath">本次新生成的 ONNX 路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>本次新生成的 TensorRT Engine 路径。</returns>
        string RebuildTensorRtEngine(string onnxPath, CancellationToken token);

        /// <summary>
        /// 使用本次模型训练 memory bank。
        /// </summary>
        /// <param name="modelPath">本次训练实际使用的模型路径。</param>
        /// <param name="token">取消令牌。</param>
        void TrainMemoryBank(string modelPath, CancellationToken token);

        /// <summary>
        /// 打包本次模型、memory bank 和参数清单。
        /// </summary>
        /// <param name="modelPath">本次训练实际使用的模型路径。</param>
        /// <param name="token">取消令牌。</param>
        void PackageTemplate(string modelPath, CancellationToken token);
    }

    /// <summary>
    /// 定义可替换的模型产物生成器。
    /// </summary>
    public interface ILargeModelArtifactGenerator
    {
        /// <summary>
        /// 将本次模型产物写入指定临时路径。
        /// </summary>
        /// <param name="temporaryPath">本次唯一临时文件路径。</param>
        /// <param name="token">取消令牌。</param>
        void Generate(string temporaryPath, CancellationToken token);
    }

    /// <summary>
    /// 定义可替换的模型产物可加载性验证器。
    /// </summary>
    public interface ILargeModelArtifactValidator
    {
        /// <summary>
        /// 验证指定临时模型能否被实际训练路径加载。
        /// </summary>
        /// <param name="artifactPath">待验证的临时模型路径。</param>
        void Validate(string artifactPath);
    }

    /// <summary>
    /// 负责使用唯一临时路径生成模型，并在成功后安全替换正式模型。
    /// </summary>
    public sealed class LargeModelArtifactPublisher
    {
        /// <summary>
        /// 生成、验证并发布一次模型产物。
        /// </summary>
        /// <param name="targetPath">正式模型路径。</param>
        /// <param name="stageName">用于错误信息的生成阶段名称。</param>
        /// <param name="minimumLength">有效产物允许的最小字节数。</param>
        /// <param name="generator">模型产物生成器。</param>
        /// <param name="validator">模型产物可加载性验证器。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>发布后的正式模型路径。</returns>
        public string Publish(
            string targetPath,
            string stageName,
            long minimumLength,
            ILargeModelArtifactGenerator generator,
            ILargeModelArtifactValidator validator,
            IProgress<string> log,
            CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
                throw new ArgumentException("正式模型路径不能为空。", "targetPath");
            if (string.IsNullOrWhiteSpace(stageName))
                throw new ArgumentException("模型生成阶段名称不能为空。", "stageName");
            if (minimumLength <= 0)
                throw new ArgumentOutOfRangeException("minimumLength", "模型最小有效长度必须大于零。");
            if (generator == null)
                throw new ArgumentNullException("generator");
            if (validator == null)
                throw new ArgumentNullException("validator");

            token.ThrowIfCancellationRequested();
            string temporaryPath = BuildTemporaryPath(targetPath);
            try
            {
                token.ThrowIfCancellationRequested();
                generator.Generate(temporaryPath, token);
                token.ThrowIfCancellationRequested();
                if (!File.Exists(temporaryPath))
                    throw new FileNotFoundException(stageName + " 生成完成后未找到临时产物。", temporaryPath);
                long actualLength = new FileInfo(temporaryPath).Length;
                if (actualLength < minimumLength)
                {
                    throw new InvalidDataException(
                        stageName + " 生成物无效，文件长度为 " + actualLength
                        + " 字节，最小有效长度为 " + minimumLength + " 字节。");
                }

                try
                {
                    validator.Validate(temporaryPath);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException(stageName + " 生成物无法由实际训练路径加载。", ex);
                }

                token.ThrowIfCancellationRequested();
                ReplaceGeneratedFile(temporaryPath, targetPath);
                return targetPath;
            }
            catch
            {
                TryDeleteGeneratedFile(temporaryPath, log);
                throw;
            }
        }

        /// <summary>
        /// 构建与正式模型同目录、扩展名保持不变的唯一临时路径。
        /// </summary>
        /// <param name="targetPath">正式模型路径。</param>
        /// <returns>本次唯一临时模型路径。</returns>
        private static string BuildTemporaryPath(string targetPath)
        {
            string directory = Path.GetDirectoryName(targetPath);
            string extension = Path.GetExtension(targetPath);
            string fileName = Path.GetFileNameWithoutExtension(targetPath)
                + "_" + Guid.NewGuid().ToString("N") + ".building" + extension;
            return Path.Combine(directory, fileName);
        }

        /// <summary>
        /// 使用同目录临时文件安全替换正式模型。
        /// </summary>
        /// <param name="temporaryPath">已经生成并验证的临时模型路径。</param>
        /// <param name="targetPath">正式模型路径。</param>
        private static void ReplaceGeneratedFile(string temporaryPath, string targetPath)
        {
            if (File.Exists(targetPath))
            {
                File.Replace(temporaryPath, targetPath, null, true);
                return;
            }

            File.Move(temporaryPath, targetPath);
        }

        /// <summary>
        /// 尽力清理失败路径遗留的临时模型。
        /// </summary>
        /// <param name="path">待清理的临时模型路径。</param>
        /// <param name="log">训练日志回调。</param>
        private static void TryDeleteGeneratedFile(string path, IProgress<string> log)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                TryReportLog(log, "临时模型文件清理失败：" + path + "。原因：" + ex.Message);
            }
        }

        /// <summary>
        /// 尽力写入清理失败日志，日志回调异常不得覆盖原始训练异常。
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
                // 日志回调失败时保留调用方正在处理的原始生成或取消异常。
            }
        }
    }

    /// <summary>
    /// 线程安全收集外部进程的标准输出和标准错误文本。
    /// </summary>
    public sealed class LargeModelProcessOutputBuffer
    {
        /// <summary>
        /// 保护输出缓存读写的同步对象。
        /// </summary>
        private readonly object _syncRoot = new object();

        /// <summary>
        /// 按接收顺序保存外部进程输出行的缓存。
        /// </summary>
        private readonly StringBuilder _builder = new StringBuilder();

        /// <summary>
        /// 线程安全追加一行非空进程输出。
        /// </summary>
        /// <param name="line">标准输出或标准错误中的一行。</param>
        public void AppendLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            lock (_syncRoot)
            {
                _builder.AppendLine(line);
            }
        }

        /// <summary>
        /// 获取当前进程输出的一致快照。
        /// </summary>
        /// <returns>已经收集的完整输出文本。</returns>
        public string GetText()
        {
            lock (_syncRoot)
            {
                return _builder.ToString();
            }
        }
    }

    /// <summary>
    /// 按固定顺序协调大模型数据集、模型、memory bank 和模板打包阶段。
    /// </summary>
    public sealed class LargeModelTrainingCoordinator
    {
        /// <summary>
        /// 执行一次完整训练链。
        /// </summary>
        /// <param name="runtimeRoot">大模型运行目录。</param>
        /// <param name="useGpu">是否执行 GPU TensorRT Engine 构建阶段。</param>
        /// <param name="stages">可替换的训练阶段实现。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>本次训练实际消费的新 ONNX 或 TensorRT Engine 路径。</returns>
        public string Execute(
            string runtimeRoot,
            bool useGpu,
            ILargeModelTrainingStages stages,
            IProgress<string> log,
            CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(runtimeRoot))
                throw new ArgumentException("大模型运行目录不能为空。", "runtimeRoot");
            if (stages == null)
                throw new ArgumentNullException("stages");

            using (LargeModelTrainingRunLock.Acquire(runtimeRoot, log, token))
            {
                token.ThrowIfCancellationRequested();
                stages.PrepareDataset(token);
                token.ThrowIfCancellationRequested();
                string onnxPath = stages.RebuildOnnxModel(token);
                token.ThrowIfCancellationRequested();
                string modelPath = useGpu
                    ? stages.RebuildTensorRtEngine(onnxPath, token)
                    : onnxPath;
                // 模型重建返回后立即确认取消状态，禁止取消请求继续进入 native Bank。
                token.ThrowIfCancellationRequested();
                stages.TrainMemoryBank(modelPath, token);
                token.ThrowIfCancellationRequested();
                stages.PackageTemplate(modelPath, token);
                // 模板发布阶段在原子提交前执行最后取消检查；提交后不能再把成功结果改报为取消。
                return modelPath;
            }
        }
    }

    /// <summary>
    /// 使用独占锁文件保护同一大模型运行目录中的完整训练链。
    /// </summary>
    internal sealed class LargeModelTrainingRunLock : IDisposable
    {
        /// <summary>
        /// 跨进程锁文件名。
        /// </summary>
        private const string LockFileName = ".large-model-training.lock";

        /// <summary>
        /// 等待锁时的取消检查间隔，单位为毫秒。
        /// </summary>
        private const int RetryIntervalMilliseconds = 100;

        /// <summary>
        /// 当前进程持有独占共享模式的锁文件流。
        /// </summary>
        private FileStream _stream;

        /// <summary>
        /// 使用已经独占打开的文件流初始化训练链锁。
        /// </summary>
        /// <param name="stream">持有 <see cref="FileShare.None"/> 的文件流。</param>
        private LargeModelTrainingRunLock(FileStream stream)
        {
            _stream = stream;
        }

        /// <summary>
        /// 以可取消轮询方式获取同一运行目录的跨线程、跨进程独占锁。
        /// </summary>
        /// <param name="runtimeRoot">大模型运行目录。</param>
        /// <param name="log">训练日志回调。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>成功持有的训练链锁。</returns>
        public static LargeModelTrainingRunLock Acquire(string runtimeRoot, IProgress<string> log, CancellationToken token)
        {
            string fullRoot = Path.GetFullPath(runtimeRoot);
            Directory.CreateDirectory(fullRoot);
            string lockPath = Path.Combine(fullRoot, LockFileName);
            bool waitMessageReported = false;

            while (true)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    FileStream stream = new FileStream(
                        lockPath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None);
                    return new LargeModelTrainingRunLock(stream);
                }
                catch (IOException ex)
                {
                    if (!IsLockContention(ex))
                        throw;

                    if (!waitMessageReported && log != null)
                    {
                        log.Report("已有大模型训练正在运行，正在等待独占训练锁。");
                        waitMessageReported = true;
                    }

                    if (token.WaitHandle.WaitOne(RetryIntervalMilliseconds))
                        token.ThrowIfCancellationRequested();
                }
            }
        }

        /// <summary>
        /// 判断文件打开失败是否确实由其它线程或进程持有独占锁引起。
        /// </summary>
        /// <param name="exception">打开锁文件时发生的输入输出异常。</param>
        /// <returns>共享冲突或锁冲突返回 true，其它磁盘错误返回 false。</returns>
        private static bool IsLockContention(IOException exception)
        {
            int errorCode = exception.HResult & 0xFFFF;
            return errorCode == 32 || errorCode == 33;
        }

        /// <summary>
        /// 释放独占文件流，使下一次训练可以进入受保护链路。
        /// </summary>
        public void Dispose()
        {
            FileStream stream = Interlocked.Exchange(ref _stream, null);
            if (stream != null)
                stream.Dispose();
        }
    }
}
