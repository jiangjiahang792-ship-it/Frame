using System;
using System.Globalization;
using System.IO;

namespace TDJS_Vision.Forms.AiTrainForm
{
    /// <summary>
    /// 大模型运行环境路径管理、可用性检查与模型文件解析。
    /// </summary>
    public static class LargeModelRuntimeBootstrapper
    {
        /// <summary>
        /// DINOv2 native 主库文件名。
        /// </summary>
        private const string Dinov2DllName = "Dinov2AD.dll";

        /// <summary>
        /// DINOv2 OpenCV 桥接库文件名。
        /// </summary>
        private const string OpenCvBridgeDllName = "OpenCvBridge.dll";

        /// <summary>
        /// 设备授权文件名。
        /// </summary>
        private const string LicenseFileName = "device.license";

        /// <summary>
        /// 获取大模型运行环境目录。
        /// </summary>
        /// <returns>运行目录下的 LargeModelDll 路径。</returns>
        public static string GetRuntimeRoot()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LargeModelDll");
        }

        /// <summary>
        /// 获取大模型模板输出目录。
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
        /// 检查大模型运行环境是否可用于训练或推理。
        /// </summary>
        /// <returns>运行环境状态。</returns>
        public static LargeModelRuntimeStatus ValidateRuntime()
        {
            string root = GetRuntimeRoot();
            if (!Directory.Exists(root))
            {
                return new LargeModelRuntimeStatus
                {
                    IsReady = false,
                    RootPath = root,
                    Message = "未检测到大模型环境目录：" + root
                };
            }

            string dllPath = Path.Combine(root, "Dinov2AD.dll");
            if (!File.Exists(dllPath))
            {
                return new LargeModelRuntimeStatus
                {
                    IsReady = false,
                    RootPath = root,
                    Message = "大模型环境缺少 Dinov2AD.dll"
                };
            }

            string bridgePath = Path.Combine(root, "OpenCvBridge.dll");
            if (!File.Exists(bridgePath))
            {
                return new LargeModelRuntimeStatus
                {
                    IsReady = false,
                    RootPath = root,
                    Message = "大模型环境缺少 OpenCvBridge.dll"
                };
            }

            string licensePath = Path.Combine(root, "device.license");
            if (!File.Exists(licensePath))
            {
                return new LargeModelRuntimeStatus
                {
                    IsReady = false,
                    RootPath = root,
                    Message = "大模型环境缺少 device.license"
                };
            }

            return new LargeModelRuntimeStatus
            {
                IsReady = true,
                RootPath = root,
                Message = "大模型环境已就绪：" + root
            };
        }

        /// <summary>
        /// 根据训练请求解析 native 模型文件。
        /// </summary>
        /// <param name="runtimeRoot">大模型运行环境目录。</param>
        /// <param name="request">训练请求。</param>
        /// <returns>模型文件完整路径。</returns>
        public static string ResolveModelPath(string runtimeRoot, LargeModelTrainingRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            return ResolveModelPath(runtimeRoot, request.Device, request.Precision, request.InputWidth, request.InputHeight);
        }

        /// <summary>
        /// 根据模板清单解析 native 模型文件。
        /// </summary>
        /// <param name="runtimeRoot">大模型运行环境目录。</param>
        /// <param name="manifest">模板清单。</param>
        /// <returns>模型文件完整路径。</returns>
        public static string ResolveModelPath(string runtimeRoot, LargeModelTemplateManifest manifest)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            string root = LargeModelDinov2Native.ResolveRuntimeRoot(runtimeRoot);
            if (!string.IsNullOrWhiteSpace(manifest.ModelFileName))
            {
                string modelPath = Path.Combine(root, manifest.ModelFileName);
                if (File.Exists(modelPath))
                    return modelPath;
            }

            return ResolveModelPath(root, manifest.Device, manifest.Precision, manifest.InputWidth, manifest.InputHeight);
        }

        /// <summary>
        /// 根据设备、精度和输入尺寸解析 native 模型文件。
        /// </summary>
        /// <param name="runtimeRoot">大模型运行环境目录。</param>
        /// <param name="device">设备文本。</param>
        /// <param name="precision">GPU 模型精度。</param>
        /// <param name="width">输入宽度。</param>
        /// <param name="height">输入高度。</param>
        /// <returns>模型文件完整路径。</returns>
        public static string ResolveModelPath(string runtimeRoot, string device, string precision, int width, int height)
        {
            string root = LargeModelDinov2Native.ResolveRuntimeRoot(runtimeRoot);
            string normalizedDevice = NormalizeDevice(device);
            string normalizedPrecision = NormalizePrecision(precision);
            string sizeText = BuildSizeText(width, height);
            string fileName = string.Equals(normalizedDevice, "CPU", StringComparison.OrdinalIgnoreCase)
                ? "dinov2_" + sizeText + ".onnx"
                : "dinov2_" + sizeText + "_" + normalizedPrecision + ".engine";
            string modelPath = Path.Combine(root, fileName);
            if (File.Exists(modelPath))
                return modelPath;

            string legacyPath = ResolveLegacyModelPath(root, normalizedDevice, normalizedPrecision);
            if (!string.IsNullOrWhiteSpace(legacyPath))
                return legacyPath;

            throw new FileNotFoundException("大模型环境缺少模型文件：" + modelPath, modelPath);
        }

        /// <summary>
        /// 将设备文本转换为 native 设备模式。
        /// </summary>
        /// <param name="device">设备文本。</param>
        /// <returns>0 自动、1 GPU、2 CPU。</returns>
        public static int ResolveDeviceMode(string device)
        {
            string normalizedDevice = NormalizeDevice(device);
            if (string.Equals(normalizedDevice, "CPU", StringComparison.OrdinalIgnoreCase))
                return 2;
            if (string.Equals(normalizedDevice, "GPU", StringComparison.OrdinalIgnoreCase))
                return 1;

            return 0;
        }

        /// <summary>
        /// 规范化设备文本。
        /// </summary>
        /// <param name="device">原始设备文本。</param>
        /// <returns>GPU、CPU 或 AUTO。</returns>
        public static string NormalizeDevice(string device)
        {
            if (string.IsNullOrWhiteSpace(device))
                return "GPU";

            string value = device.Trim().ToUpperInvariant();
            if (value.Contains("CPU"))
                return "CPU";
            if (value.Contains("AUTO"))
                return "AUTO";

            return "GPU";
        }

        /// <summary>
        /// 规范化 GPU 模型精度。
        /// </summary>
        /// <param name="precision">原始精度文本。</param>
        /// <returns>fp16 或 fp32。</returns>
        public static string NormalizePrecision(string precision)
        {
            if (string.Equals(precision, "fp32", StringComparison.OrdinalIgnoreCase))
                return "fp32";

            return "fp16";
        }

        /// <summary>
        /// 构建 DINOv2 模型尺寸文本。
        /// </summary>
        /// <param name="width">输入宽度。</param>
        /// <param name="height">输入高度。</param>
        /// <returns>例如 448x224。</returns>
        private static string BuildSizeText(int width, int height)
        {
            int safeWidth = width > 0 ? width : 448;
            int safeHeight = height > 0 ? height : 224;
            return safeWidth.ToString(CultureInfo.InvariantCulture) + "x" + safeHeight.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 兼容早期导出的 DINOv2 文件名。
        /// </summary>
        /// <param name="root">运行环境根目录。</param>
        /// <param name="device">规范化设备。</param>
        /// <param name="precision">规范化精度。</param>
        /// <returns>存在的旧模型路径；不存在时返回空。</returns>
        private static string ResolveLegacyModelPath(string root, string device, string precision)
        {
            string[] candidates = string.Equals(device, "CPU", StringComparison.OrdinalIgnoreCase)
                ? new[] { "dinov2_op14.onnx", "dinov2_224x224.onnx" }
                : new[] { "dinov2_" + precision + ".engine" };

            foreach (string candidate in candidates)
            {
                string path = Path.Combine(root, candidate);
                if (File.Exists(path))
                    return path;
            }

            return null;
        }
    }
}
