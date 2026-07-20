using System;
using System.IO;

namespace TDJS_Vision.Forms.AiTrainForm
{
    /// <summary>
    /// 无监督运行环境路径管理与可用性检查。
    /// </summary>
    public static class UnsupervisedRuntimeBootstrapper
    {
        /// <summary>
        /// 获取无监督运行环境目录。
        /// </summary>
        /// <returns>运行目录下的 UnsupervisedDll 路径。</returns>
        public static string GetRuntimeRoot()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UnsupervisedDll");
        }

        /// <summary>
        /// 获取无监督模板输出目录。
        /// </summary>
        /// <returns>运行目录下的 Model 路径。</returns>
        public static string GetModelRoot()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Model");
        }

        /// <summary>
        /// 确保模板输出目录存在。
        /// </summary>
        /// <returns>模板输出目录。</returns>
        public static string EnsureModelRoot()
        {
            string root = GetModelRoot();
            Directory.CreateDirectory(root);
            return root;
        }

        /// <summary>
        /// 检查无监督运行环境是否可用于训练。
        /// </summary>
        /// <returns>运行环境状态。</returns>
        public static UnsupervisedRuntimeStatus ValidateRuntime()
        {
            string root = GetRuntimeRoot();
            if (!Directory.Exists(root))
            {
                return new UnsupervisedRuntimeStatus
                {
                    IsReady = false,
                    RootPath = root,
                    Message = "未检测到无监督环境目录：" + root
                };
            }

            string dllPath = Path.Combine(root, "AnomalibLib.dll");
            if (!File.Exists(dllPath))
            {
                return new UnsupervisedRuntimeStatus
                {
                    IsReady = false,
                    RootPath = root,
                    Message = "无监督环境缺少 AnomalibLib.dll"
                };
            }

            string licensePath = Path.Combine(root, "device.license");
            if (!File.Exists(licensePath))
            {
                return new UnsupervisedRuntimeStatus
                {
                    IsReady = false,
                    RootPath = root,
                    Message = "无监督环境缺少 device.license"
                };
            }

            return new UnsupervisedRuntimeStatus
            {
                IsReady = true,
                RootPath = root,
                Message = "无监督环境已就绪：" + root
            };
        }
    }
}
