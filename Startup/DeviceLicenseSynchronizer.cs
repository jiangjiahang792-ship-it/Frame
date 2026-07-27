using System;
using System.Collections.Generic;
using System.IO;

namespace TDJS_Vision.Startup
{
    /// <summary>
    /// 从主程序根目录向已部署 AI 运行目录同步设备许可证。
    /// </summary>
    internal sealed class DeviceLicenseSynchronizer : IDeviceLicenseSynchronizer
    {
        /// <summary>
        /// 设备许可证文件名。
        /// </summary>
        private const string LicenseFileName = "device.license";

        /// <summary>
        /// 文件内容比较缓冲区大小。
        /// </summary>
        private const int ComparisonBufferSize = 4096;

        /// <summary>
        /// 需要接收许可证副本的 AI 运行环境相对目录。
        /// </summary>
        private static readonly string[] TargetRelativeDirectories =
        {
            "LargeModelDll",
            "UnsupervisedDll",
            "YoloGPUDll",
            Path.Combine("YoloGPUDll", "1050tidll"),
            Path.Combine("YoloGPUDll", "750dll")
        };

        /// <summary>
        /// 把程序根目录中的设备许可证同步到已经部署的 AI 运行目录。
        /// </summary>
        /// <param name="applicationDirectory">主程序运行目录。</param>
        /// <returns>本次同步产生的逐目标结果。</returns>
        public IReadOnlyList<DeviceLicenseSynchronizationResult> Synchronize(string applicationDirectory)
        {
            List<DeviceLicenseSynchronizationResult> results = new List<DeviceLicenseSynchronizationResult>();
            string rootDirectory;

            try
            {
                if (string.IsNullOrWhiteSpace(applicationDirectory))
                {
                    results.Add(CreateFailureResult(string.Empty, "设备许可证同步失败：程序运行目录不能为空。"));
                    return results;
                }

                rootDirectory = Path.GetFullPath(applicationDirectory);
            }
            catch (Exception ex)
            {
                results.Add(CreateFailureResult(applicationDirectory, "设备许可证同步失败：程序运行目录无效；原因：" + ex.Message));
                return results;
            }

            string sourcePath = Path.Combine(rootDirectory, LicenseFileName);
            if (!File.Exists(sourcePath))
            {
                results.Add(CreateFailureResult(sourcePath, "设备许可证同步已跳过，程序根目录缺少 device.license：" + sourcePath));
                return results;
            }

            foreach (string relativeDirectory in TargetRelativeDirectories)
            {
                results.Add(SynchronizeTarget(sourcePath, rootDirectory, relativeDirectory));
            }

            return results;
        }

        /// <summary>
        /// 同步单个目标目录，并把该目标的异常限制在本次处理内部。
        /// </summary>
        /// <param name="sourcePath">根目录许可证完整路径。</param>
        /// <param name="rootDirectory">主程序运行目录。</param>
        /// <param name="relativeDirectory">目标运行环境相对目录。</param>
        /// <returns>单个目标的同步结果。</returns>
        private static DeviceLicenseSynchronizationResult SynchronizeTarget(
            string sourcePath,
            string rootDirectory,
            string relativeDirectory)
        {
            string targetDirectory = Path.Combine(rootDirectory, relativeDirectory);
            string targetPath = Path.Combine(targetDirectory, LicenseFileName);

            if (!Directory.Exists(targetDirectory))
            {
                return new DeviceLicenseSynchronizationResult(
                    DeviceLicenseSynchronizationStatus.Skipped,
                    targetPath,
                    "设备许可证同步已跳过，运行目录不存在：" + targetDirectory);
            }

            try
            {
                if (File.Exists(targetPath) && FilesHaveSameContent(sourcePath, targetPath))
                {
                    return new DeviceLicenseSynchronizationResult(
                        DeviceLicenseSynchronizationStatus.Unchanged,
                        targetPath,
                        "设备许可证无需更新：" + targetPath);
                }

                CopyAtomically(sourcePath, targetPath);
                return new DeviceLicenseSynchronizationResult(
                    DeviceLicenseSynchronizationStatus.Copied,
                    targetPath,
                    "设备许可证已同步：" + targetPath);
            }
            catch (Exception ex)
            {
                // 单个运行环境没有写权限时继续处理其它目标，不能影响主程序启动。
                return CreateFailureResult(targetPath, "设备许可证同步失败：" + targetPath + "；原因：" + ex.Message);
            }
        }

        /// <summary>
        /// 先在目标目录生成完整临时副本，再以原子方式切换许可证文件。
        /// </summary>
        /// <param name="sourcePath">源许可证路径。</param>
        /// <param name="targetPath">目标许可证路径。</param>
        private static void CopyAtomically(string sourcePath, string targetPath)
        {
            string temporaryPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(sourcePath, temporaryPath, false);
                if (File.Exists(targetPath))
                {
                    File.Replace(temporaryPath, targetPath, null);
                }
                else
                {
                    File.Move(temporaryPath, targetPath);
                }
            }
            finally
            {
                TryDeleteTemporaryFile(temporaryPath);
            }
        }

        /// <summary>
        /// 尝试清理同步临时文件，清理失败不能覆盖原始同步异常。
        /// </summary>
        /// <param name="temporaryPath">临时许可证路径。</param>
        private static void TryDeleteTemporaryFile(string temporaryPath)
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch
            {
                // 临时文件清理属于尽力而为，主同步结果仍应保留真正的复制或替换异常。
            }
        }

        /// <summary>
        /// 比较两个许可证文件的二进制内容，内容一致时避免重复覆盖。
        /// </summary>
        /// <param name="sourcePath">源许可证路径。</param>
        /// <param name="targetPath">目标许可证路径。</param>
        /// <returns>文件长度和全部字节均相同返回 true。</returns>
        private static bool FilesHaveSameContent(string sourcePath, string targetPath)
        {
            FileInfo sourceInfo = new FileInfo(sourcePath);
            FileInfo targetInfo = new FileInfo(targetPath);
            if (sourceInfo.Length != targetInfo.Length)
                return false;

            byte[] sourceBuffer = new byte[ComparisonBufferSize];
            byte[] targetBuffer = new byte[ComparisonBufferSize];

            using (FileStream sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, ComparisonBufferSize, FileOptions.SequentialScan))
            using (FileStream targetStream = new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read, ComparisonBufferSize, FileOptions.SequentialScan))
            {
                while (true)
                {
                    int sourceRead = sourceStream.Read(sourceBuffer, 0, sourceBuffer.Length);
                    int targetRead = targetStream.Read(targetBuffer, 0, targetBuffer.Length);
                    if (sourceRead != targetRead)
                        return false;

                    if (sourceRead == 0)
                        return true;

                    for (int index = 0; index < sourceRead; index++)
                    {
                        if (sourceBuffer[index] != targetBuffer[index])
                            return false;
                    }
                }
            }
        }

        /// <summary>
        /// 创建失败结果，统一失败状态的表达方式。
        /// </summary>
        /// <param name="targetPath">源文件或目标文件路径。</param>
        /// <param name="message">简体中文失败消息。</param>
        /// <returns>失败同步结果。</returns>
        private static DeviceLicenseSynchronizationResult CreateFailureResult(string targetPath, string message)
        {
            return new DeviceLicenseSynchronizationResult(
                DeviceLicenseSynchronizationStatus.Failed,
                targetPath,
                message);
        }
    }
}
