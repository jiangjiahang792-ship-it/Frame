using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace TDJS_Vision.Forms.AiTrainForm
{
    /// <summary>
    /// 无监督 Anomalib native 训练封装。
    /// </summary>
    public sealed class UnsupervisedAnomalibDetector : IDisposable
    {
        /// <summary>
        /// native 模型句柄。
        /// </summary>
        private IntPtr _handle = IntPtr.Zero;

        /// <summary>
        /// 训练进度回调，必须持有引用避免被 GC 回收。
        /// </summary>
        private UnsupervisedAnomalibNative.ProgressCallback _progressCallback;

        /// <summary>
        /// 初始化无监督封装并配置运行环境。
        /// </summary>
        /// <param name="runtimeRoot">无监督运行环境目录。</param>
        public UnsupervisedAnomalibDetector(string runtimeRoot)
        {
            UnsupervisedAnomalibNative.Configure(runtimeRoot);
        }

        /// <summary>
        /// 初始化训练句柄。
        /// </summary>
        /// <param name="modelType">算法类型。</param>
        /// <param name="threshold">异常阈值。</param>
        /// <param name="width">训练输入宽度。</param>
        /// <param name="height">训练输入高度。</param>
        /// <param name="miniArea">最小异常面积。</param>
        /// <param name="maxEpochs">最大训练轮数。</param>
        /// <param name="batchSize">批次大小。</param>
        /// <param name="device">训练设备。</param>
        /// <param name="pythonPath">Python 路径。</param>
        /// <param name="showConsole">是否显示控制台。</param>
        /// <returns>native 返回码。</returns>
        public int InitForTraining(string modelType, float threshold, int width, int height, float miniArea, int maxEpochs, int batchSize, string device, string pythonPath, bool showConsole)
        {
            ReleaseHandle();
            return UnsupervisedAnomalibNative.Init(modelType, string.Empty, threshold, width, height, miniArea, maxEpochs, batchSize, device, pythonPath, showConsole, out _handle);
        }

        /// <summary>
        /// 执行模型训练。
        /// </summary>
        /// <param name="okPath">OK 数据集路径。</param>
        /// <param name="ngPath">NG 数据集路径，可为空。</param>
        /// <param name="savePath">训练输出目录。</param>
        /// <param name="maxEpochs">最大训练轮数。</param>
        /// <param name="progress">训练进度回调。</param>
        /// <returns>native 返回码。</returns>
        public int TrainModel(string okPath, string ngPath, string savePath, int maxEpochs, Action<int> progress)
        {
            _progressCallback = progress == null ? null : new UnsupervisedAnomalibNative.ProgressCallback(progress);
            int ret = UnsupervisedAnomalibNative.TrainModel(_handle, okPath, ngPath ?? string.Empty, savePath, maxEpochs, _progressCallback);
            GC.KeepAlive(_progressCallback);
            return ret;
        }

        /// <summary>
        /// 请求取消训练。
        /// </summary>
        /// <returns>native 返回码。</returns>
        public int CancelTrain()
        {
            if (_handle == IntPtr.Zero) return 0;
            return UnsupervisedAnomalibNative.CancelTrain(_handle);
        }

        /// <summary>
        /// 释放 native 句柄。
        /// </summary>
        public void Dispose()
        {
            ReleaseHandle();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 终结器兜底释放 native 句柄。
        /// </summary>
        ~UnsupervisedAnomalibDetector()
        {
            ReleaseHandle();
        }

        /// <summary>
        /// 在运行环境中查找 Python。
        /// </summary>
        /// <param name="runtimeRoot">无监督运行环境目录。</param>
        /// <returns>Python 路径。</returns>
        public static string FindPython(string runtimeRoot)
        {
            string root = UnsupervisedAnomalibNative.ResolveRuntimeRoot(runtimeRoot);
            string gpuPython = Path.Combine(root, "python_env_gpu", "Scripts", "python.exe");
            if (File.Exists(gpuPython)) return gpuPython;
            return Path.Combine(root, "python_env", "python.exe");
        }

        /// <summary>
        /// 查找训练目录中最新的 ONNX 文件。
        /// </summary>
        /// <param name="modelDir">模型输出目录。</param>
        /// <returns>最新 ONNX 路径。</returns>
        public static string FindNewestOnnx(string modelDir)
        {
            return Directory.GetFiles(modelDir, "*.onnx", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .First();
        }

        /// <summary>
        /// 释放 native 句柄。
        /// </summary>
        private void ReleaseHandle()
        {
            if (_handle != IntPtr.Zero)
            {
                UnsupervisedAnomalibNative.Release(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }

    /// <summary>
    /// 无监督 Anomalib native P/Invoke 定义。
    /// </summary>
    internal static class UnsupervisedAnomalibNative
    {
        /// <summary>
        /// native DLL 名称。
        /// </summary>
        private const string DllName = "AnomalibLib.dll";

        /// <summary>
        /// 使用 DLL 所在目录参与依赖搜索。
        /// </summary>
        private const uint LoadWithAlteredSearchPath = 0x00000008;

        /// <summary>
        /// 已加载的 native DLL 句柄。
        /// </summary>
        private static IntPtr _libraryHandle = IntPtr.Zero;

        /// <summary>
        /// 环境配置锁。
        /// </summary>
        private static readonly object ConfigureLock = new object();

        /// <summary>
        /// 配置 native DLL 搜索路径和 Python 环境。
        /// </summary>
        /// <param name="runtimeRoot">无监督运行环境目录。</param>
        public static void Configure(string runtimeRoot)
        {
            lock (ConfigureLock)
            {
                string root = ResolveRuntimeRoot(runtimeRoot);
                SetDllDirectory(root);
                PrependPath(root);
                PrependPath(Path.Combine(root, "onnxruntime-win-x64-gpu-cuda12-1.17.3", "onnxruntime-win-x64-gpu-1.17.3", "lib"));
                PrependPath(Path.Combine(root, "TensorRT-8.6.1.6", "lib"));
                PrependPath(Path.Combine(root, "python_env"));
                PrependPath(Path.Combine(root, "python_env", "DLLs"));
                PrependPath(Path.Combine(root, "python_env_gpu", "Scripts"));
                PrependPath(Path.Combine(root, "python_env_gpu", "Lib", "site-packages", "torch", "lib"));
                PrependPath(Path.Combine(root, "python_env_gpu", "Lib", "site-packages", "torchvision"));
                PatchPythonVenvConfig(root);
                SetPythonHome(UnsupervisedAnomalibDetector.FindPython(root));
                LoadAnomalibLibrary(root);
            }
        }

        /// <summary>
        /// 解析真实运行环境目录。
        /// </summary>
        /// <param name="runtimeRoot">原始运行环境目录。</param>
        /// <returns>真实运行环境目录。</returns>
        internal static string ResolveRuntimeRoot(string runtimeRoot)
        {
            string root = Path.GetFullPath(runtimeRoot);
            if (File.Exists(Path.Combine(root, DllName)) && File.Exists(Path.Combine(root, "device.license")))
            {
                return root;
            }

            string releaseRoot = Path.Combine(root, "bin", "Release", "net8.0-windows");
            if (File.Exists(Path.Combine(releaseRoot, DllName)) && File.Exists(Path.Combine(releaseRoot, "device.license")))
            {
                return releaseRoot;
            }

            return root;
        }

        /// <summary>
        /// 把目录加入 PATH 前部。
        /// </summary>
        /// <param name="path">目录路径。</param>
        private static void PrependPath(string path)
        {
            if (!Directory.Exists(path)) return;

            string current = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            if (current.Split(';').Any(p => string.Equals(p.Trim(), path, StringComparison.OrdinalIgnoreCase))) return;

            Environment.SetEnvironmentVariable("PATH", path + ";" + current);
        }

        /// <summary>
        /// 修正 GPU 虚拟环境配置，避免迁移目录后 Python 解析到旧路径。
        /// </summary>
        /// <param name="root">运行环境根目录。</param>
        private static void PatchPythonVenvConfig(string root)
        {
            try
            {
                string cfgPath = Path.Combine(root, "python_env_gpu", "pyvenv.cfg");
                string basePython = Path.Combine(root, "python_env", "python.exe");
                if (!File.Exists(cfgPath) || !File.Exists(basePython)) return;

                string baseDir = Path.GetDirectoryName(basePython);
                string content = string.Join(Environment.NewLine,
                    "home = " + baseDir,
                    "include-system-site-packages = false",
                    "version = 3.12.13",
                    "executable = " + basePython,
                    "command = " + basePython + " -m venv " + Path.Combine(root, "python_env_gpu")) + Environment.NewLine;
                if (File.ReadAllText(cfgPath) == content) return;
                File.WriteAllText(cfgPath, content);
            }
            catch
            {
                // 环境目录只读时不阻塞主流程，真正训练初始化会返回错误。
            }
        }

        /// <summary>
        /// 设置 PythonHome 环境变量。
        /// </summary>
        /// <param name="pythonPath">Python 路径。</param>
        private static void SetPythonHome(string pythonPath)
        {
            string pythonDir = Path.GetDirectoryName(Path.GetFullPath(pythonPath));
            if (string.Equals(Path.GetFileName(pythonDir), "Scripts", StringComparison.OrdinalIgnoreCase))
            {
                string venvRoot = Directory.GetParent(pythonDir).FullName;
                string packageRoot = Directory.GetParent(venvRoot).FullName;
                string basePython = Path.Combine(packageRoot, "python_env");
                Environment.SetEnvironmentVariable("PYTHONHOME", Directory.Exists(basePython) ? basePython : venvRoot);
                return;
            }

            Environment.SetEnvironmentVariable("PYTHONHOME", pythonDir);
        }

        /// <summary>
        /// 按完整路径预加载 AnomalibLib.dll。
        /// </summary>
        /// <param name="root">运行环境根目录。</param>
        private static void LoadAnomalibLibrary(string root)
        {
            if (_libraryHandle != IntPtr.Zero) return;

            string dllPath = Path.Combine(root, DllName);
            _libraryHandle = LoadLibraryEx(dllPath, IntPtr.Zero, LoadWithAlteredSearchPath);
            if (_libraryHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                throw new DllNotFoundException("加载 " + dllPath + " 失败，Win32Error=" + error);
            }
        }

        /// <summary>
        /// native 训练进度回调。
        /// </summary>
        /// <param name="progress">训练进度。</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ProgressCallback(int progress);

        /// <summary>
        /// 初始化 native 模型句柄。
        /// </summary>
        [DllImport(DllName, EntryPoint = "Init", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Init(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string modelType,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string modelPath,
            float threshold,
            int width,
            int height,
            float miniArea,
            int maxEpochs,
            int batchSize,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string device,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pythonPath,
            [MarshalAs(UnmanagedType.I1)] bool showConsole,
            out IntPtr handle);

        /// <summary>
        /// 执行 native 训练。
        /// </summary>
        [DllImport(DllName, EntryPoint = "TrainModel", CallingConvention = CallingConvention.Cdecl)]
        public static extern int TrainModel(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string okPath,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string ngPath,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string savePath,
            int maxEpochs,
            ProgressCallback callback);

        /// <summary>
        /// 取消 native 训练。
        /// </summary>
        [DllImport(DllName, EntryPoint = "CancelTrain", CallingConvention = CallingConvention.Cdecl)]
        public static extern int CancelTrain(IntPtr handle);

        /// <summary>
        /// 释放 native 模型句柄。
        /// </summary>
        [DllImport(DllName, EntryPoint = "Release", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Release(IntPtr handle);

        /// <summary>
        /// 设置 DLL 搜索目录。
        /// </summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SetDllDirectory(string lpPathName);

        /// <summary>
        /// 按完整路径加载 DLL。
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);
    }
}
