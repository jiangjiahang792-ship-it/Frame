using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using OpenCvSharp;

namespace TDJS_Vision.Forms.AiTrainForm
{
    /// <summary>
    /// 定义 DINOv2 检测器使用的完整 native 调用边界。
    /// </summary>
    internal interface ILargeModelDinov2NativeApi
    {
        /// <summary>
        /// 配置 native 运行目录。
        /// </summary>
        /// <param name="runtimeRoot">大模型运行环境目录。</param>
        void Configure(string runtimeRoot);

        /// <summary>
        /// 初始化 native 模型实例。
        /// </summary>
        /// <param name="modelPath">模型路径。</param>
        /// <param name="bankPath">memory bank 路径。</param>
        /// <param name="deviceMode">native 设备模式。</param>
        /// <returns>native 模型句柄。</returns>
        IntPtr Init(string modelPath, string bankPath, int deviceMode);

        /// <summary>
        /// 执行 native memory bank 训练。
        /// </summary>
        /// <param name="handler">native 模型句柄。</param>
        /// <param name="okImage">单张 OK 图像指针。</param>
        /// <param name="okPath">OK 图像目录。</param>
        /// <param name="ngImage">单张 NG 图像指针。</param>
        /// <param name="ngPath">NG 图像目录。</param>
        /// <param name="saveBankPath">memory bank 保存路径。</param>
        /// <param name="appendMode">是否追加训练。</param>
        /// <returns>native 训练返回值。</returns>
        int Train(
            IntPtr handler,
            IntPtr okImage,
            string okPath,
            IntPtr ngImage,
            string ngPath,
            string saveBankPath,
            int appendMode);

        /// <summary>
        /// 执行 native 单图推理。
        /// </summary>
        /// <param name="handler">native 模型句柄。</param>
        /// <param name="image">OpenCV 图像镜像。</param>
        /// <param name="imageThreshold">图像异常阈值。</param>
        /// <param name="areaThreshold">异常区域面积阈值。</param>
        /// <param name="heatmapSaveDir">热力图保存目录。</param>
        /// <param name="outHeatmap">热力图输出指针。</param>
        /// <param name="outScore">异常分数。</param>
        /// <param name="outBboxes">异常框数组。</param>
        /// <param name="maxBboxes">异常框容量。</param>
        /// <param name="outNumBboxes">实际异常框数量。</param>
        /// <returns>native 推理返回值。</returns>
        int Infer(
            IntPtr handler,
            ref LargeModelOpenCvImage image,
            float imageThreshold,
            int areaThreshold,
            string heatmapSaveDir,
            IntPtr outHeatmap,
            out float outScore,
            LargeModelRectBBox[] outBboxes,
            int maxBboxes,
            out int outNumBboxes);

        /// <summary>
        /// 释放 native 模型实例。
        /// </summary>
        /// <param name="handler">待释放句柄。</param>
        void Release(IntPtr handler);
    }

    /// <summary>
    /// 把生产 P/Invoke 调用适配为可替换的 native API。
    /// </summary>
    internal sealed class LargeModelDinov2NativeApi : ILargeModelDinov2NativeApi
    {
        /// <summary>
        /// 配置生产 native 运行目录。
        /// </summary>
        /// <param name="runtimeRoot">大模型运行环境目录。</param>
        public void Configure(string runtimeRoot)
        {
            LargeModelDinov2Native.Configure(runtimeRoot);
        }

        /// <summary>
        /// 初始化生产 native 模型实例。
        /// </summary>
        /// <param name="modelPath">模型路径。</param>
        /// <param name="bankPath">memory bank 路径。</param>
        /// <param name="deviceMode">native 设备模式。</param>
        /// <returns>native 模型句柄。</returns>
        public IntPtr Init(string modelPath, string bankPath, int deviceMode)
        {
            return LargeModelDinov2Native.Init(modelPath, bankPath, deviceMode);
        }

        /// <summary>
        /// 执行生产 native memory bank 训练。
        /// </summary>
        /// <param name="handler">native 模型句柄。</param>
        /// <param name="okImage">单张 OK 图像指针。</param>
        /// <param name="okPath">OK 图像目录。</param>
        /// <param name="ngImage">单张 NG 图像指针。</param>
        /// <param name="ngPath">NG 图像目录。</param>
        /// <param name="saveBankPath">memory bank 保存路径。</param>
        /// <param name="appendMode">是否追加训练。</param>
        /// <returns>native 训练返回值。</returns>
        public int Train(
            IntPtr handler,
            IntPtr okImage,
            string okPath,
            IntPtr ngImage,
            string ngPath,
            string saveBankPath,
            int appendMode)
        {
            return LargeModelDinov2Native.Train(
                handler,
                okImage,
                okPath,
                ngImage,
                ngPath,
                saveBankPath,
                appendMode);
        }

        /// <summary>
        /// 执行生产 native 单图推理。
        /// </summary>
        /// <param name="handler">native 模型句柄。</param>
        /// <param name="image">OpenCV 图像镜像。</param>
        /// <param name="imageThreshold">图像异常阈值。</param>
        /// <param name="areaThreshold">异常区域面积阈值。</param>
        /// <param name="heatmapSaveDir">热力图保存目录。</param>
        /// <param name="outHeatmap">热力图输出指针。</param>
        /// <param name="outScore">异常分数。</param>
        /// <param name="outBboxes">异常框数组。</param>
        /// <param name="maxBboxes">异常框容量。</param>
        /// <param name="outNumBboxes">实际异常框数量。</param>
        /// <returns>native 推理返回值。</returns>
        public int Infer(
            IntPtr handler,
            ref LargeModelOpenCvImage image,
            float imageThreshold,
            int areaThreshold,
            string heatmapSaveDir,
            IntPtr outHeatmap,
            out float outScore,
            LargeModelRectBBox[] outBboxes,
            int maxBboxes,
            out int outNumBboxes)
        {
            return LargeModelDinov2Native.Infer(
                handler,
                ref image,
                imageThreshold,
                areaThreshold,
                heatmapSaveDir,
                outHeatmap,
                out outScore,
                outBboxes,
                maxBboxes,
                out outNumBboxes);
        }

        /// <summary>
        /// 释放生产 native 模型实例。
        /// </summary>
        /// <param name="handler">待释放句柄。</param>
        public void Release(IntPtr handler)
        {
            LargeModelDinov2Native.Release(handler);
        }
    }

    /// <summary>
    /// DINOv2 大模型 native 训练与推理封装。
    /// </summary>
    public sealed class LargeModelDinov2Detector : IDisposable
    {
        /// <summary>
        /// 保护 disposed 状态和 native 句柄发布、摘除操作的生命周期锁。
        /// </summary>
        private readonly object _lifecycleLock = new object();

        /// <summary>
        /// 当前检测器使用的可替换 native API。
        /// </summary>
        private readonly ILargeModelDinov2NativeApi _nativeApi;

        /// <summary>
        /// native 模型句柄。
        /// </summary>
        private IntPtr _handle = IntPtr.Zero;

        /// <summary>
        /// 当前检测器是否已释放；一旦置位不允许重新发布句柄。
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 初始化 DINOv2 封装并配置隔离运行环境。
        /// </summary>
        /// <param name="runtimeRoot">大模型运行环境目录。</param>
        public LargeModelDinov2Detector(string runtimeRoot)
            : this(runtimeRoot, new LargeModelDinov2NativeApi())
        {
        }

        /// <summary>
        /// 使用可替换 native API 初始化 DINOv2 检测器。
        /// </summary>
        /// <param name="runtimeRoot">大模型运行环境目录。</param>
        /// <param name="nativeApi">native 调用边界。</param>
        internal LargeModelDinov2Detector(string runtimeRoot, ILargeModelDinov2NativeApi nativeApi)
        {
            _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
            _nativeApi.Configure(runtimeRoot);
        }

        /// <summary>
        /// 加载 DINOv2 模型和 memory bank。
        /// </summary>
        /// <param name="modelPath">TensorRT engine 或 ONNX 模型路径。</param>
        /// <param name="bankPath">memory bank 路径。</param>
        /// <param name="deviceMode">0 自动、1 GPU TensorRT、2 CPU ONNX。</param>
        /// <returns>0 表示加载成功，-1 表示 native 初始化失败。</returns>
        public int LoadModel(string modelPath, string bankPath, int deviceMode)
        {
            return LoadModel(modelPath, bankPath, deviceMode, CancellationToken.None);
        }

        /// <summary>
        /// 以可取消方式加载 DINOv2 模型和 memory bank。
        /// </summary>
        /// <param name="modelPath">TensorRT engine 或 ONNX 模型路径。</param>
        /// <param name="bankPath">memory bank 路径。</param>
        /// <param name="deviceMode">0 自动、1 GPU TensorRT、2 CPU ONNX。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>0 表示加载成功，-1 表示 native 初始化失败。</returns>
        public int LoadModel(string modelPath, string bankPath, int deviceMode, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
                throw new ArgumentException("大模型模型路径不能为空。", nameof(modelPath));
            if (!File.Exists(modelPath))
                throw new FileNotFoundException("未找到大模型模型文件。", modelPath);

            token.ThrowIfCancellationRequested();
            IntPtr previousHandle;
            lock (_lifecycleLock)
            {
                ThrowIfDisposed();
                previousHandle = _handle;
                _handle = IntPtr.Zero;
            }

            ReleaseNativeHandle(previousHandle);
            token.ThrowIfCancellationRequested();
            IntPtr initializedHandle = _nativeApi.Init(modelPath, bankPath ?? string.Empty, deviceMode);
            if (initializedHandle == IntPtr.Zero)
            {
                token.ThrowIfCancellationRequested();
                lock (_lifecycleLock)
                {
                    ThrowIfDisposed();
                }

                return -1;
            }

            bool published = false;
            lock (_lifecycleLock)
            {
                if (!_disposed && !token.IsCancellationRequested)
                {
                    _handle = initializedHandle;
                    published = true;
                }
            }

            if (!published)
            {
                // Init 在取消或 Dispose 后返回时，句柄仍是局部所有权，必须立即释放且绝不写回字段。
                ReleaseNativeHandle(initializedHandle);
                token.ThrowIfCancellationRequested();
                throw new ObjectDisposedException(nameof(LargeModelDinov2Detector), "大模型检测器已释放，不能发布新句柄。");
            }

            return 0;
        }

        /// <summary>
        /// 使用 OK 图像生成 memory bank，并可选使用 NG 图像校准阈值。
        /// </summary>
        /// <param name="modelPath">TensorRT engine 或 ONNX 模型路径。</param>
        /// <param name="bankPath">memory bank 输出路径。</param>
        /// <param name="deviceMode">0 自动、1 GPU TensorRT、2 CPU ONNX。</param>
        /// <param name="okPath">OK 训练图片目录。</param>
        /// <param name="ngPath">NG 校准图片目录，可为空。</param>
        /// <returns>native 实际处理的图片数量。</returns>
        public int TrainMemoryBank(string modelPath, string bankPath, int deviceMode, string okPath, string ngPath)
        {
            return TrainMemoryBank(modelPath, bankPath, deviceMode, okPath, ngPath, CancellationToken.None);
        }

        /// <summary>
        /// 以可取消方式使用 OK 图像生成 memory bank，并可选使用 NG 图像校准阈值。
        /// </summary>
        /// <param name="modelPath">TensorRT engine 或 ONNX 模型路径。</param>
        /// <param name="bankPath">memory bank 输出路径。</param>
        /// <param name="deviceMode">0 自动、1 GPU TensorRT、2 CPU ONNX。</param>
        /// <param name="okPath">OK 训练图片目录。</param>
        /// <param name="ngPath">NG 校准图片目录，可为空。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>native 实际处理的图片数量。</returns>
        public int TrainMemoryBank(
            string modelPath,
            string bankPath,
            int deviceMode,
            string okPath,
            string ngPath,
            CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(okPath))
                throw new ArgumentException("OK 训练路径不能为空。", nameof(okPath));
            if (string.IsNullOrWhiteSpace(bankPath))
                throw new ArgumentException("memory bank 输出路径不能为空。", nameof(bankPath));

            int initRet = LoadModel(modelPath, bankPath, deviceMode, token);
            if (initRet != 0)
                return -1;

            token.ThrowIfCancellationRequested();
            string bankDirectory = Path.GetDirectoryName(bankPath);
            if (!string.IsNullOrWhiteSpace(bankDirectory))
            {
                Directory.CreateDirectory(bankDirectory);
            }

            IntPtr activeHandle = GetActiveHandle(token);
            // 句柄取得后紧邻 native Train 再检查一次，阻止 Init 返回边界的取消继续进入训练。
            token.ThrowIfCancellationRequested();
            return _nativeApi.Train(
                activeHandle,
                IntPtr.Zero,
                okPath,
                IntPtr.Zero,
                ngPath ?? string.Empty,
                bankPath,
                0);
        }

        /// <summary>
        /// 对 BGR 图像执行大模型异常推理。
        /// </summary>
        /// <param name="image">BGR、灰度或 BGRA OpenCV 图像。</param>
        /// <param name="imageThreshold">图像级异常阈值，0 表示使用 bank 自动阈值。</param>
        /// <param name="areaThreshold">异常框最小面积阈值。</param>
        /// <param name="maxBoxes">最多返回的异常框数量。</param>
        /// <returns>大模型推理结果。</returns>
        public LargeModelDinov2Result InferBgr(Mat image, float imageThreshold, int areaThreshold, int maxBoxes)
        {
            long inputPreparationMilliseconds;
            long nativeInferenceMilliseconds;
            return InferBgr(
                image,
                imageThreshold,
                areaThreshold,
                maxBoxes,
                out inputPreparationMilliseconds,
                out nativeInferenceMilliseconds);
        }

        /// <summary>
        /// 对 BGR 图像执行大模型异常推理，并返回托管图像准备与 native 调用耗时。
        /// </summary>
        /// <param name="image">BGR、灰度或 BGRA OpenCV 图像。</param>
        /// <param name="imageThreshold">图像级异常阈值。</param>
        /// <param name="areaThreshold">异常框最小面积阈值。</param>
        /// <param name="maxBoxes">最多返回的异常框数量。</param>
        /// <param name="inputPreparationMilliseconds">BGR 转换或复制耗时。</param>
        /// <param name="nativeInferenceMilliseconds">native Infer 调用耗时。</param>
        /// <returns>大模型推理结果。</returns>
        public LargeModelDinov2Result InferBgr(
            Mat image,
            float imageThreshold,
            int areaThreshold,
            int maxBoxes,
            out long inputPreparationMilliseconds,
            out long nativeInferenceMilliseconds)
        {
            if (image == null || image.Empty())
                throw new ArgumentException("大模型推理输入图像不能为空。", nameof(image));

            inputPreparationMilliseconds = 0L;
            nativeInferenceMilliseconds = 0L;
            IntPtr activeHandle = GetActiveHandle(CancellationToken.None);
            int boxCapacity = Math.Max(1, maxBoxes);
            LargeModelRectBBox[] boxes = new LargeModelRectBBox[boxCapacity];
            Stopwatch inputTimer = Stopwatch.StartNew();
            Mat input = EnsureBgr(image);
            inputTimer.Stop();
            inputPreparationMilliseconds = inputTimer.ElapsedMilliseconds;
            using (input)
            {
                LargeModelOpenCvImage nativeImage = new LargeModelOpenCvImage { Mat = input.CvPtr };
                float score;
                int count;
                Stopwatch nativeTimer = Stopwatch.StartNew();
                int status;
                try
                {
                    status = _nativeApi.Infer(
                        activeHandle,
                        ref nativeImage,
                        imageThreshold,
                        Math.Max(0, areaThreshold),
                        string.Empty,
                        IntPtr.Zero,
                        out score,
                        boxes,
                        boxCapacity,
                        out count);
                }
                finally
                {
                    nativeTimer.Stop();
                    nativeInferenceMilliseconds = nativeTimer.ElapsedMilliseconds;
                }

                return new LargeModelDinov2Result(status, score, ToRectangles(boxes, count, boxCapacity));
            }
        }

        /// <summary>
        /// 释放 native 句柄。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 终结器兜底释放 native 句柄。
        /// </summary>
        ~LargeModelDinov2Detector()
        {
            Dispose(false);
        }

        /// <summary>
        /// 以线程安全且幂等的方式终止生命周期并释放当前句柄。
        /// </summary>
        /// <param name="disposing">是否由显式 Dispose 调用。</param>
        private void Dispose(bool disposing)
        {
            IntPtr handleToRelease;
            lock (_lifecycleLock)
            {
                if (_disposed)
                    return;

                _disposed = true;
                handleToRelease = _handle;
                _handle = IntPtr.Zero;
            }

            ReleaseNativeHandle(handleToRelease);
        }

        /// <summary>
        /// 获取仍属于当前未释放检测器的有效句柄。
        /// </summary>
        /// <param name="token">取消令牌。</param>
        /// <returns>可用于紧随其后的 native 调用的句柄。</returns>
        private IntPtr GetActiveHandle(CancellationToken token)
        {
            lock (_lifecycleLock)
            {
                token.ThrowIfCancellationRequested();
                ThrowIfDisposed();
                if (_handle == IntPtr.Zero)
                    throw new InvalidOperationException("大模型模型尚未加载。");

                return _handle;
            }
        }

        /// <summary>
        /// 检查当前检测器是否已经结束生命周期。
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LargeModelDinov2Detector));
        }

        /// <summary>
        /// 在生命周期锁外释放指定 native 句柄，避免阻塞或回调导致死锁。
        /// </summary>
        /// <param name="handle">待释放句柄。</param>
        private void ReleaseNativeHandle(IntPtr handle)
        {
            if (handle != IntPtr.Zero)
                _nativeApi.Release(handle);
        }

        /// <summary>
        /// 确保输入图像为 DINOv2 native 支持的 BGR 三通道。
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
                throw new InvalidOperationException("大模型推理只支持 1、3、4 通道图像。");

            return bgr;
        }

        /// <summary>
        /// 将 native 返回的矩形数组转换为绘图矩形。
        /// </summary>
        /// <param name="boxes">native 矩形数组。</param>
        /// <param name="count">native 实际返回数量。</param>
        /// <param name="capacity">托管数组容量。</param>
        /// <returns>异常矩形集合。</returns>
        private static Rectangle[] ToRectangles(LargeModelRectBBox[] boxes, int count, int capacity)
        {
            int safeCount = Math.Max(0, Math.Min(count, capacity));
            Rectangle[] rects = new Rectangle[safeCount];
            for (int i = 0; i < safeCount; i++)
            {
                rects[i] = new Rectangle(boxes[i].X, boxes[i].Y, boxes[i].Width, boxes[i].Height);
            }

            return rects;
        }
    }

    /// <summary>
    /// 大模型 native 推理结果。
    /// </summary>
    public sealed class LargeModelDinov2Result
    {
        /// <summary>
        /// 初始化大模型推理结果。
        /// </summary>
        /// <param name="returnCode">native 返回码，1 表示 NG、0 表示 OK、-1 表示失败。</param>
        /// <param name="score">异常分数。</param>
        /// <param name="boxes">异常区域矩形。</param>
        public LargeModelDinov2Result(int returnCode, float score, Rectangle[] boxes)
        {
            ReturnCode = returnCode;
            Score = score;
            Boxes = boxes ?? new Rectangle[0];
        }

        /// <summary>
        /// native 返回码，1 表示 NG、0 表示 OK、-1 表示失败。
        /// </summary>
        public int ReturnCode { get; }

        /// <summary>
        /// 当前结果是否 OK。
        /// </summary>
        public bool IsOk => ReturnCode == 0;

        /// <summary>
        /// 当前结果是否 NG。
        /// </summary>
        public bool IsNg => ReturnCode == 1;

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
    /// DINOv2 native 的 RectBBox 结构体镜像。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct LargeModelRectBBox
    {
        /// <summary>
        /// 矩形左上角 X 坐标。
        /// </summary>
        public int X;

        /// <summary>
        /// 矩形左上角 Y 坐标。
        /// </summary>
        public int Y;

        /// <summary>
        /// 矩形宽度。
        /// </summary>
        public int Width;

        /// <summary>
        /// 矩形高度。
        /// </summary>
        public int Height;
    }

    /// <summary>
    /// DINOv2 native 的 OpenCvImage 结构体镜像。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct LargeModelOpenCvImage
    {
        /// <summary>
        /// cv::Mat 指针，来自 OpenCvSharp Mat.CvPtr。
        /// </summary>
        public IntPtr Mat;
    }

    /// <summary>
    /// DINOv2 native P/Invoke 定义与 DLL 预加载。
    /// </summary>
    internal static class LargeModelDinov2Native
    {
        /// <summary>
        /// native DLL 名称。
        /// </summary>
        private const string DllName = "Dinov2AD.dll";

        /// <summary>
        /// OpenCV 桥接 DLL 名称。
        /// </summary>
        private const string OpenCvBridgeDllName = "OpenCvBridge.dll";

        /// <summary>
        /// 使用 DLL 所在目录参与依赖搜索。
        /// </summary>
        private const uint LoadWithAlteredSearchPath = 0x00000008;

        /// <summary>
        /// 已加载的 DINOv2 DLL 句柄。
        /// </summary>
        private static IntPtr _libraryHandle = IntPtr.Zero;

        /// <summary>
        /// 已加载的 OpenCV 桥接 DLL 句柄。
        /// </summary>
        private static IntPtr _openCvBridgeHandle = IntPtr.Zero;

        /// <summary>
        /// 环境配置锁。
        /// </summary>
        private static readonly object ConfigureLock = new object();

        /// <summary>
        /// 配置 native DLL 搜索路径。
        /// </summary>
        /// <param name="runtimeRoot">大模型运行环境目录。</param>
        public static void Configure(string runtimeRoot)
        {
            lock (ConfigureLock)
            {
                string root = ResolveRuntimeRoot(runtimeRoot);
                SetDllDirectory(root);
                PrependPath(root);
                LoadLibraryByName(root, OpenCvBridgeDllName, ref _openCvBridgeHandle);
                LoadLibraryByName(root, DllName, ref _libraryHandle);
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
        /// 初始化 DINOv2 模型实例。
        /// </summary>
        [DllImport(DllName, EntryPoint = "init", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern IntPtr Init(string modelPath, string bankPath, int deviceMode);

        /// <summary>
        /// 执行 memory bank 训练或阈值校准。
        /// </summary>
        [DllImport(DllName, EntryPoint = "train", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern int Train(
            IntPtr handler,
            IntPtr okImage,
            string okPath,
            IntPtr ngImage,
            string ngPath,
            string saveBankPath,
            int appendMode);

        /// <summary>
        /// 执行单图推理。
        /// </summary>
        [DllImport(DllName, EntryPoint = "infer", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern int Infer(
            IntPtr handler,
            ref LargeModelOpenCvImage image,
            float imageThreshold,
            int areaThreshold,
            string heatmapSaveDir,
            IntPtr outHeatmap,
            out float outScore,
            [In, Out] LargeModelRectBBox[] outBboxes,
            int maxBboxes,
            out int outNumBboxes);

        /// <summary>
        /// 释放 DINOv2 模型实例。
        /// </summary>
        [DllImport(DllName, EntryPoint = "release", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Release(IntPtr handler);

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
        /// 按完整路径加载指定 DLL。
        /// </summary>
        /// <param name="root">运行环境根目录。</param>
        /// <param name="fileName">DLL 文件名。</param>
        /// <param name="handle">缓存的 DLL 句柄。</param>
        private static void LoadLibraryByName(string root, string fileName, ref IntPtr handle)
        {
            if (handle != IntPtr.Zero) return;

            string dllPath = Path.Combine(root, fileName);
            handle = LoadLibraryEx(dllPath, IntPtr.Zero, LoadWithAlteredSearchPath);
            if (handle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                throw new DllNotFoundException("加载 " + dllPath + " 失败，Win32Error=" + error);
            }
        }

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
