using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using OpenCvSharp;

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
        /// 加载训练窗口生成模板包中的 ONNX 模型。
        /// </summary>
        /// <param name="modelType">算法类型。</param>
        /// <param name="modelPath">模板包解出的 ONNX 模型路径。</param>
        /// <param name="threshold">异常阈值。</param>
        /// <param name="width">模型输入宽度。</param>
        /// <param name="height">模型输入高度。</param>
        /// <param name="miniArea">最小异常面积。</param>
        /// <param name="batchSize">批次大小。</param>
        /// <param name="device">推理设备。</param>
        /// <param name="pythonPath">Python 路径。</param>
        /// <returns>native 返回码。</returns>
        public int LoadModel(string modelType, string modelPath, float threshold, int width, int height, float miniArea, int batchSize, string device, string pythonPath)
        {
            ReleaseHandle();
            return UnsupervisedAnomalibNative.Init(modelType, modelPath, threshold, width, height, miniArea, 1, batchSize, device, pythonPath, false, out _handle);
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
        /// 对 BGR 图像执行无监督异常推理。
        /// </summary>
        /// <param name="image">BGR 或可转换为 BGR 的 OpenCV 图像。</param>
        /// <param name="threshold">异常阈值。</param>
        /// <param name="miniArea">最小异常面积。</param>
        /// <param name="maxBoxes">最多返回的异常框数量。</param>
        /// <returns>无监督推理结果。</returns>
        public UnsupervisedAnomalibResult InferBgr(Mat image, float threshold, float miniArea, int maxBoxes)
        {
            long inputPreparationMilliseconds;
            long nativeInferenceMilliseconds;
            return InferBgr(
                image,
                threshold,
                miniArea,
                maxBoxes,
                out inputPreparationMilliseconds,
                out nativeInferenceMilliseconds);
        }

        /// <summary>
        /// 对 BGR 图像执行无监督异常推理，并返回托管图像准备与 native 调用耗时。
        /// </summary>
        /// <param name="image">BGR 或可转换为 BGR 的 OpenCV 图像。</param>
        /// <param name="threshold">异常阈值。</param>
        /// <param name="miniArea">最小异常面积。</param>
        /// <param name="maxBoxes">最多返回的异常框数量。</param>
        /// <param name="inputPreparationMilliseconds">BGR 转换或复制耗时。</param>
        /// <param name="nativeInferenceMilliseconds">native Infer 调用耗时。</param>
        /// <returns>无监督推理结果。</returns>
        public UnsupervisedAnomalibResult InferBgr(
            Mat image,
            float threshold,
            float miniArea,
            int maxBoxes,
            out long inputPreparationMilliseconds,
            out long nativeInferenceMilliseconds)
        {
            if (_handle == IntPtr.Zero)
                throw new InvalidOperationException("无监督模型尚未加载。");
            if (image == null || image.Empty())
                throw new ArgumentException("无监督推理输入图像不能为空。", nameof(image));

            inputPreparationMilliseconds = 0L;
            nativeInferenceMilliseconds = 0L;
            int boxCapacity = Math.Max(1, maxBoxes);
            int[] boxes = new int[boxCapacity * 4];
            Stopwatch inputTimer = Stopwatch.StartNew();
            Mat input = EnsureBgr(image);
            inputTimer.Stop();
            inputPreparationMilliseconds = inputTimer.ElapsedMilliseconds;
            using (input)
            {
                float score;
                int count;
                Stopwatch nativeTimer = Stopwatch.StartNew();
                int ret;
                try
                {
                    ret = UnsupervisedAnomalibNative.Infer(_handle, input.Data, input.Width, input.Height, threshold, miniArea, string.Empty, out score, null, boxes, boxCapacity, out count);
                }
                finally
                {
                    nativeTimer.Stop();
                    nativeInferenceMilliseconds = nativeTimer.ElapsedMilliseconds;
                }

                return new UnsupervisedAnomalibResult(ret, score, ToRectangles(boxes, count, boxCapacity));
            }
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

        /// <summary>
        /// 确保输入图像为 BGR 三通道，匹配 native Infer 接口预期。
        /// </summary>
        /// <param name="image">原始图像。</param>
        /// <returns>BGR 图像副本。</returns>
        private static Mat EnsureBgr(Mat image)
        {
            if (image.Channels() == 3)
                return image.Clone();

            Mat bgr = new Mat();
            if (image.Channels() == 1)
                Cv2.CvtColor(image, bgr, ColorConversionCodes.GRAY2BGR);
            else if (image.Channels() == 4)
                Cv2.CvtColor(image, bgr, ColorConversionCodes.BGRA2BGR);
            else
                throw new InvalidOperationException("无监督推理只支持 1、3、4 通道图像。");

            return bgr;
        }

        /// <summary>
        /// 将 native 返回的 x、y、width、height 数组转换为矩形集合。
        /// </summary>
        /// <param name="boxes">native 返回的扁平矩形数组。</param>
        /// <param name="count">native 返回的实际数量。</param>
        /// <param name="capacity">托管数组可容纳的最大数量。</param>
        /// <returns>异常矩形集合。</returns>
        private static Rectangle[] ToRectangles(int[] boxes, int count, int capacity)
        {
            int safeCount = Math.Max(0, Math.Min(count, capacity));
            Rectangle[] rects = new Rectangle[safeCount];
            for (int i = 0; i < safeCount; i++)
            {
                int offset = i * 4;
                rects[i] = new Rectangle(boxes[offset], boxes[offset + 1], boxes[offset + 2], boxes[offset + 3]);
            }
            return rects;
        }
    }

    /// <summary>
    /// 无监督 native 推理结果。
    /// </summary>
    public sealed class UnsupervisedAnomalibResult
    {
        /// <summary>
        /// 初始化无监督推理结果。
        /// </summary>
        /// <param name="returnCode">native 返回码。</param>
        /// <param name="score">异常分数。</param>
        /// <param name="boxes">异常区域矩形。</param>
        public UnsupervisedAnomalibResult(int returnCode, float score, Rectangle[] boxes)
        {
            ReturnCode = returnCode;
            Score = score;
            Boxes = boxes ?? new Rectangle[0];
        }

        /// <summary>
        /// native 返回码，0 表示推理调用成功。
        /// </summary>
        public int ReturnCode { get; }

        /// <summary>
        /// native 返回的异常分数。
        /// </summary>
        public float Score { get; }

        /// <summary>
        /// native 返回的异常区域矩形。
        /// </summary>
        public Rectangle[] Boxes { get; }
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
        /// 执行 native 推理。
        /// </summary>
        [DllImport(DllName, EntryPoint = "Infer", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Infer(
            IntPtr handle,
            IntPtr inputData,
            int width,
            int height,
            float threshold,
            float miniArea,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string saveMapPath,
            out float anomalyScore,
            [Out] float[] anomalyMap,
            [Out] int[] boxes,
            int maxBoxes,
            out int actualCount);

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
