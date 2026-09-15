using System;
using System.Linq;

namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    /// <summary>
    /// 保存当前进程是否为YOLO隔离推理worker的运行上下文。
    /// </summary>
    internal static class YoloIsolatedRuntimeContext
    {
        /// <summary>
        /// worker进程命令行参数。
        /// </summary>
        public const string WorkerArgument = "--tdjs-yolo-worker";

        /// <summary>
        /// worker进程环境变量名称。
        /// </summary>
        public const string WorkerEnvironmentName = "TDJS_YOLO_WORKER";

        /// <summary>
        /// 获取当前进程是否正在作为YOLO隔离worker运行。
        /// </summary>
        public static bool IsWorkerProcess
        {
            get
            {
                string value = Environment.GetEnvironmentVariable(WorkerEnvironmentName);
                return string.Equals(value, "1", StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// 判断启动参数是否请求进入YOLO隔离worker模式。
        /// </summary>
        /// <param name="args">启动参数。</param>
        /// <returns>包含worker参数返回true。</returns>
        public static bool HasWorkerArgument(string[] args)
        {
            return args != null && args.Any(arg => string.Equals(arg, WorkerArgument, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 标记当前进程为YOLO隔离worker进程。
        /// </summary>
        public static void MarkAsWorkerProcess()
        {
            Environment.SetEnvironmentVariable(WorkerEnvironmentName, "1");
        }
    }
}
