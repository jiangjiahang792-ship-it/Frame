using Newtonsoft.Json;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using Point = OpenCvSharp.Point;

namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    /// <summary>
    /// YOLO OpenVINO CPU会话，封装新版td_det.dll的yolo_init、yolo_infer、yolo_release三函数接口。
    /// </summary>
    internal sealed class YoloOpenVinoCpuSession : IDisposable
    {
        /// <summary>
        /// 新版CPU推理DLL名称。
        /// </summary>
        private const string CpuDllName = "td_det.dll";

        /// <summary>
        /// 新版TensorRT GPU推理DLL名称。
        /// </summary>
        private const string GpuDllName = "yolo_openvino_tensorrt.dll";

        /// <summary>
        /// 原生错误信息缓冲区大小。
        /// </summary>
        private const int ErrorBufferSize = 4096;

        /// <summary>
        /// 当前模型的原生句柄。
        /// </summary>
        private IntPtr _handle;

        /// <summary>
        /// 当前会话使用的原生调用适配器，用于隔离CPU DLL与GPU DLL。
        /// </summary>
        private readonly IYoloOpenVinoNativeMethods _nativeMethods;

        /// <summary>
        /// 当前会话名称，用于异常信息定位。
        /// </summary>
        private readonly string _sessionName;

        /// <summary>
        /// 当前会话是否已经释放。
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 模型标签列表，优先来自模型metadata。
        /// </summary>
        public List<string> Labels { get; private set; }

        /// <summary>
        /// 模型初始化信息。
        /// </summary>
        public YoloOpenVinoModelInfo ModelInfo { get; private set; }

        /// <summary>
        /// 创建CPU会话。
        /// </summary>
        /// <param name="handle">原生模型句柄。</param>
        /// <param name="labels">模型标签。</param>
        /// <param name="modelInfo">模型初始化信息。</param>
        private YoloOpenVinoCpuSession(IntPtr handle, List<string> labels, YoloOpenVinoModelInfo modelInfo, IYoloOpenVinoNativeMethods nativeMethods)
        {
            _handle = handle;
            Labels = labels ?? new List<string>();
            ModelInfo = modelInfo;
            _nativeMethods = nativeMethods;
            _sessionName = nativeMethods == null ? "YoloOpenVinoCpuSession" : nativeMethods.SessionName;
        }

        /// <summary>
        /// 打开CPU模型会话。
        /// </summary>
        /// <param name="modelPath">模型文件路径。</param>
        /// <returns>已初始化的CPU会话。</returns>
        public static YoloOpenVinoCpuSession Open(string modelPath)
        {
            return OpenCore(modelPath, CpuNativeMethods.Instance);
        }

        /// <summary>
        /// 打开TensorRT GPU模型会话。
        /// </summary>
        /// <param name="modelPath">模型文件路径。</param>
        /// <returns>已初始化的GPU会话。</returns>
        public static YoloOpenVinoCpuSession OpenGpu(string modelPath)
        {
            return OpenCore(modelPath, GpuTensorRtNativeMethods.Instance);
        }

        /// <summary>
        /// 使用指定native适配器打开模型会话。
        /// </summary>
        /// <param name="modelPath">模型文件路径。</param>
        /// <param name="nativeMethods">native调用适配器。</param>
        /// <returns>已初始化的模型会话。</returns>
        private static YoloOpenVinoCpuSession OpenCore(string modelPath, IYoloOpenVinoNativeMethods nativeMethods)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
                throw new Exception("AI模型路径为空，无法初始化" + nativeMethods.DeviceText + "推理会话！");

            using (Utf8NativeString path = new Utf8NativeString(modelPath))
            {
                byte[] error = new byte[ErrorBufferSize];
                IntPtr handle = IntPtr.Zero;
                IntPtr initInfoPtr = IntPtr.Zero;

                try
                {
                    int ret = nativeMethods.YoloInit(path.Pointer, (int)nativeMethods.DeviceMode, out handle, out initInfoPtr, error, error.Length);
                    if (ret != 1 || handle == IntPtr.Zero)
                        throw new Exception(DecodeError(error));

                    YoloInitInfo initInfo = Marshal.PtrToStructure<YoloInitInfo>(initInfoPtr);
                    string labelsJson = PtrToUtf8(initInfo.LabelsJson);
                    List<string> labels = ParseLabels(labelsJson);
                    YoloOpenVinoModelInfo modelInfo = new YoloOpenVinoModelInfo
                    {
                        YoloVersion = initInfo.YoloVersion,
                        Task = initInfo.Task,
                        ModelScale = initInfo.ModelScale,
                        InputChannels = initInfo.InputChannels,
                        InputWidth = initInfo.InputWidth,
                        InputHeight = initInfo.InputHeight,
                        NumClasses = initInfo.NumClasses,
                        LabelCount = initInfo.LabelCount,
                        LabelsJson = labelsJson,
                        ModelTimestampMs = initInfo.ModelTimestampMs,
                        ModelDatetimeIso = PtrToUtf8(initInfo.ModelDatetimeIso),
                        InfoJson = PtrToUtf8(initInfo.InfoJson)
                    };

                    return new YoloOpenVinoCpuSession(handle, labels, modelInfo, nativeMethods);
                }
                catch(Exception ex)
                {
                    if (handle != IntPtr.Zero)
                        nativeMethods.YoloRelease(handle);
                    if (ex.Message.Contains("-2"))
                    {
                        throw new Exception("初始化" + nativeMethods.DeviceText + "模型发生错误,请检查License文件是否过期,过期请联系厂家重新授权!!!");
                    }
                    throw;
                }
                finally
                {
                    if (initInfoPtr != IntPtr.Zero)
                        nativeMethods.YoloRelease(initInfoPtr);
                }
            }
        }

        /// <summary>
        /// 执行目标检测推理。
        /// </summary>
        /// <param name="image">输入图像。</param>
        /// <param name="scoreThreshold">置信度阈值。</param>
        /// <param name="nmsThreshold">NMS阈值。</param>
        /// <param name="deltaX">结果X方向偏移。</param>
        /// <param name="deltaY">结果Y方向偏移。</param>
        /// <returns>目标检测结果集合。</returns>
        public List<DetResult> DetectDet(Mat image, float scoreThreshold, float nmsThreshold, int deltaX = 0, int deltaY = 0)
        {
            YoloOpenVinoInferenceResult inferenceResult = Infer(image, scoreThreshold, nmsThreshold);
            List<DetResult> results = new List<DetResult>(inferenceResult.Objects.Count);

            foreach (YoloObject obj in inferenceResult.Objects)
            {
                Rect rect = ToRect(obj.X1, obj.Y1, obj.X2, obj.Y2, deltaX, deltaY);
                if (rect.Width <= 0 || rect.Height <= 0)
                    continue;

                results.Add(new DetResult
                {
                    ClassId = obj.ClassId,
                    Score = obj.Score,
                    Box = rect
                });
            }

            return results;
        }

        /// <summary>
        /// 执行旋转框推理。
        /// </summary>
        /// <param name="image">输入图像。</param>
        /// <param name="scoreThreshold">置信度阈值。</param>
        /// <param name="nmsThreshold">NMS阈值。</param>
        /// <param name="deltaX">结果X方向偏移。</param>
        /// <param name="deltaY">结果Y方向偏移。</param>
        /// <returns>旋转框结果集合。</returns>
        public List<ObbResult> DetectObb(Mat image, float scoreThreshold, float nmsThreshold, int deltaX = 0, int deltaY = 0)
        {
            YoloOpenVinoInferenceResult inferenceResult = Infer(image, scoreThreshold, nmsThreshold);
            List<ObbResult> results = new List<ObbResult>(inferenceResult.Objects.Count);

            foreach (YoloObject obj in inferenceResult.Objects)
            {
                results.Add(new ObbResult
                {
                    center_x = obj.Cx + deltaX,
                    center_y = obj.Cy + deltaY,
                    width = obj.W,
                    height = obj.H,
                    angle = RadianToDegree(obj.AngleRad),
                    class_id = obj.ClassId,
                    confidence = obj.Score
                });
            }

            return results;
        }

        /// <summary>
        /// 执行分割推理。
        /// </summary>
        /// <param name="image">输入图像。</param>
        /// <param name="scoreThreshold">置信度阈值。</param>
        /// <param name="nmsThreshold">NMS阈值。</param>
        /// <param name="deltaX">结果X方向偏移。</param>
        /// <param name="deltaY">结果Y方向偏移。</param>
        /// <param name="needMaskBox">是否需要从掩膜提取不规则框。</param>
        /// <returns>分割结果集合。</returns>
        public List<SegResult> DetectSeg(Mat image, float scoreThreshold, float nmsThreshold, int deltaX = 0, int deltaY = 0, bool needMaskBox = true)
        {
            YoloOpenVinoInferenceResult inferenceResult = Infer(image, scoreThreshold, nmsThreshold, needMaskBox);
            List<SegResult> results = new List<SegResult>(inferenceResult.Objects.Count);

            foreach (YoloObject obj in inferenceResult.Objects)
            {
                Rect rect = ToRect(obj.X1, obj.Y1, obj.X2, obj.Y2, deltaX, deltaY);
                Box box = needMaskBox ? ExtractMaskBox(inferenceResult, obj, rect, deltaX, deltaY) : CreateBoxFromRect(rect);

                results.Add(new SegResult
                {
                    ClassId = obj.ClassId,
                    Score = obj.Score,
                    Rect = rect,
                    Box = box
                });
            }

            return results;
        }

        /// <summary>
        /// 执行姿态推理。
        /// </summary>
        /// <param name="image">输入图像。</param>
        /// <param name="scoreThreshold">置信度阈值。</param>
        /// <param name="nmsThreshold">NMS阈值。</param>
        /// <param name="deltaX">结果X方向偏移。</param>
        /// <param name="deltaY">结果Y方向偏移。</param>
        /// <returns>姿态结果。</returns>
        public PoseResult DetectPose(Mat image, float scoreThreshold, float nmsThreshold, int deltaX = 0, int deltaY = 0)
        {
            YoloOpenVinoInferenceResult inferenceResult = Infer(image, scoreThreshold, nmsThreshold);
            PoseResult result = new PoseResult
            {
                Boxs = new List<Rect>(),
                KeyPoints = new List<Point>()
            };

            foreach (YoloObject obj in inferenceResult.Objects)
            {
                Rect rect = ToRect(obj.X1, obj.Y1, obj.X2, obj.Y2, deltaX, deltaY);
                if (rect.Width > 0 && rect.Height > 0)
                    result.Boxs.Add(rect);

                int start = Math.Max(0, obj.KeypointOffset);
                int end = Math.Min(inferenceResult.Keypoints.Count, start + Math.Max(0, obj.KeypointCount));
                for (int i = start; i < end; i++)
                {
                    YoloKeypoint keypoint = inferenceResult.Keypoints[i];
                    result.KeyPoints.Add(new Point((int)Math.Round(keypoint.X) + deltaX, (int)Math.Round(keypoint.Y) + deltaY));
                }
            }

            return result;
        }

        /// <summary>
        /// 释放CPU模型会话。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_handle != IntPtr.Zero)
            {
                _nativeMethods.YoloRelease(_handle);
                _handle = IntPtr.Zero;
            }
        }

        /// <summary>
        /// 执行原生推理并按需复制托管结果。
        /// </summary>
        /// <param name="image">输入图像。</param>
        /// <param name="scoreThreshold">置信度阈值。</param>
        /// <param name="nmsThreshold">NMS阈值。</param>
        /// <param name="copyMasks">是否复制分割掩膜。</param>
        /// <returns>托管推理结果。</returns>
        private YoloOpenVinoInferenceResult Infer(Mat image, float scoreThreshold, float nmsThreshold, bool copyMasks = true)
        {
            ThrowIfDisposed();
            EnsureImageReady(image);

            YoloImage nativeImage = new YoloImage
            {
                Data = image.Data,
                Width = image.Width,
                Height = image.Height,
                StrideBytes = checked((int)image.Step()),
                PixelFormat = GetPixelFormat(image.Channels())
            };

            byte[] error = new byte[ErrorBufferSize];
            IntPtr resultPtr = IntPtr.Zero;

            try
            {
                int ret = _nativeMethods.YoloInfer(_handle, ref nativeImage, scoreThreshold, nmsThreshold, out resultPtr, error, error.Length);
                if (ret != 1 || resultPtr == IntPtr.Zero)
                    throw new Exception("yolo_infer失败：" + DecodeError(error));

                YoloResult nativeResult = Marshal.PtrToStructure<YoloResult>(resultPtr);
                return CopyInferenceResult(nativeResult, copyMasks);
            }
            finally
            {
                if (resultPtr != IntPtr.Zero)
                    _nativeMethods.YoloRelease(resultPtr);
            }
        }

        /// <summary>
        /// 校验会话是否仍然可用。
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed || _handle == IntPtr.Zero)
                throw new ObjectDisposedException(_sessionName);
        }

        /// <summary>
        /// 校验输入图像是否可推理。
        /// </summary>
        /// <param name="image">输入图像。</param>
        private static void EnsureImageReady(Mat image)
        {
            if (image == null || image.Empty() || image.Data == IntPtr.Zero)
                throw new Exception("AI输入图像为空，无法执行CPU推理！");
        }

        /// <summary>
        /// 将原生结果复制成托管结果，避免释放原生结果后访问悬空指针。
        /// </summary>
        /// <param name="nativeResult">原生推理结果。</param>
        /// <param name="copyMasks">是否复制分割掩膜。</param>
        /// <returns>托管推理结果。</returns>
        private static YoloOpenVinoInferenceResult CopyInferenceResult(YoloResult nativeResult, bool copyMasks = true)
        {
            YoloOpenVinoInferenceResult result = new YoloOpenVinoInferenceResult
            {
                Task = nativeResult.Task,
                ImageWidth = nativeResult.ImageWidth,
                ImageHeight = nativeResult.ImageHeight,
                InputWidth = nativeResult.InputWidth,
                InputHeight = nativeResult.InputHeight,
                MaskWidth = nativeResult.MaskWidth,
                MaskHeight = nativeResult.MaskHeight,
                MaskStride = nativeResult.MaskStride
            };

            CopyObjects(nativeResult, result);
            CopyKeypoints(nativeResult, result);
            if (copyMasks)
                CopyMasks(nativeResult, result);
            return result;
        }

        /// <summary>
        /// 复制目标集合。
        /// </summary>
        /// <param name="nativeResult">原生推理结果。</param>
        /// <param name="result">托管推理结果。</param>
        private static void CopyObjects(YoloResult nativeResult, YoloOpenVinoInferenceResult result)
        {
            if (nativeResult.Objects == IntPtr.Zero || nativeResult.ObjectCount <= 0)
                return;

            int objectSize = Marshal.SizeOf(typeof(YoloObject));
            for (int i = 0; i < nativeResult.ObjectCount; i++)
            {
                IntPtr currentPtr = new IntPtr(nativeResult.Objects.ToInt64() + i * objectSize);
                result.Objects.Add(Marshal.PtrToStructure<YoloObject>(currentPtr));
            }
        }

        /// <summary>
        /// 复制关键点集合。
        /// </summary>
        /// <param name="nativeResult">原生推理结果。</param>
        /// <param name="result">托管推理结果。</param>
        private static void CopyKeypoints(YoloResult nativeResult, YoloOpenVinoInferenceResult result)
        {
            if (nativeResult.Keypoints == IntPtr.Zero || nativeResult.KeypointCount <= 0)
                return;

            int keypointSize = Marshal.SizeOf(typeof(YoloKeypoint));
            for (int i = 0; i < nativeResult.KeypointCount; i++)
            {
                IntPtr currentPtr = new IntPtr(nativeResult.Keypoints.ToInt64() + i * keypointSize);
                result.Keypoints.Add(Marshal.PtrToStructure<YoloKeypoint>(currentPtr));
            }
        }

        /// <summary>
        /// 复制分割掩膜集合。
        /// </summary>
        /// <param name="nativeResult">原生推理结果。</param>
        /// <param name="result">托管推理结果。</param>
        private static void CopyMasks(YoloResult nativeResult, YoloOpenVinoInferenceResult result)
        {
            if (nativeResult.Masks == IntPtr.Zero || nativeResult.MaskCount <= 0 || nativeResult.MaskWidth <= 0 || nativeResult.MaskHeight <= 0)
                return;

            int stride = nativeResult.MaskStride > 0 ? nativeResult.MaskStride : nativeResult.MaskWidth;
            int singleMaskBytes = checked(nativeResult.MaskHeight * stride);
            for (int i = 0; i < nativeResult.MaskCount; i++)
            {
                byte[] maskBytes = new byte[singleMaskBytes];
                IntPtr maskPtr = new IntPtr(nativeResult.Masks.ToInt64() + i * singleMaskBytes);
                Marshal.Copy(maskPtr, maskBytes, 0, maskBytes.Length);
                result.Masks.Add(maskBytes);
            }
        }

        /// <summary>
        /// 从分割掩膜提取四角框。
        /// </summary>
        /// <param name="inferenceResult">托管推理结果。</param>
        /// <param name="obj">当前目标。</param>
        /// <param name="rect">当前目标矩形。</param>
        /// <param name="deltaX">结果X方向偏移。</param>
        /// <param name="deltaY">结果Y方向偏移。</param>
        /// <returns>不规则框四角。</returns>
        private static Box ExtractMaskBox(YoloOpenVinoInferenceResult inferenceResult, YoloObject obj, Rect rect, int deltaX, int deltaY)
        {
            if (obj.MaskIndex < 0 || obj.MaskIndex >= inferenceResult.Masks.Count || inferenceResult.MaskWidth <= 0 || inferenceResult.MaskHeight <= 0)
                return CreateBoxFromRect(rect);

            byte[] compactMask = CompactMask(inferenceResult.Masks[obj.MaskIndex], inferenceResult.MaskWidth, inferenceResult.MaskHeight, inferenceResult.MaskStride);
            using (Mat mask = new Mat(inferenceResult.MaskHeight, inferenceResult.MaskWidth, MatType.CV_8UC1))
            {
                Marshal.Copy(compactMask, 0, mask.Data, compactMask.Length);

                Point[][] contours;
                HierarchyIndex[] hierarchy;
                Cv2.FindContours(mask, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                if (contours == null || contours.Length == 0)
                    return CreateBoxFromRect(rect);

                Point[] contour = FindLargestContour(contours);
                int offsetX = deltaX;
                int offsetY = deltaY;
                if (inferenceResult.MaskWidth != inferenceResult.ImageWidth || inferenceResult.MaskHeight != inferenceResult.ImageHeight)
                {
                    offsetX = rect.X;
                    offsetY = rect.Y;
                }

                Point[] shiftedContour = OffsetContour(contour, offsetX, offsetY);
                Point topLeft;
                Point topRight;
                Point bottomRight;
                Point bottomLeft;
                GetExtremeCorners(shiftedContour, out topLeft, out topRight, out bottomRight, out bottomLeft);

                return new Box(
                    new PointF(topLeft.X, topLeft.Y),
                    new PointF(topRight.X, topRight.Y),
                    new PointF(bottomLeft.X, bottomLeft.Y),
                    new PointF(bottomRight.X, bottomRight.Y));
            }
        }

        /// <summary>
        /// 将带步长的掩膜转换为紧凑掩膜。
        /// </summary>
        /// <param name="source">原始掩膜字节。</param>
        /// <param name="width">掩膜宽度。</param>
        /// <param name="height">掩膜高度。</param>
        /// <param name="stride">掩膜步长。</param>
        /// <returns>紧凑掩膜字节。</returns>
        private static byte[] CompactMask(byte[] source, int width, int height, int stride)
        {
            int actualStride = stride > 0 ? stride : width;
            byte[] compact = new byte[checked(width * height)];
            for (int row = 0; row < height; row++)
            {
                Buffer.BlockCopy(source, row * actualStride, compact, row * width, width);
            }
            return compact;
        }

        /// <summary>
        /// 查找面积最大的轮廓。
        /// </summary>
        /// <param name="contours">轮廓集合。</param>
        /// <returns>面积最大的轮廓。</returns>
        private static Point[] FindLargestContour(Point[][] contours)
        {
            Point[] largest = contours[0];
            double largestArea = Cv2.ContourArea(largest);
            for (int i = 1; i < contours.Length; i++)
            {
                double area = Cv2.ContourArea(contours[i]);
                if (area > largestArea)
                {
                    largest = contours[i];
                    largestArea = area;
                }
            }
            return largest;
        }

        /// <summary>
        /// 对轮廓点统一偏移。
        /// </summary>
        /// <param name="contour">原始轮廓。</param>
        /// <param name="offsetX">X偏移。</param>
        /// <param name="offsetY">Y偏移。</param>
        /// <returns>偏移后的轮廓。</returns>
        private static Point[] OffsetContour(Point[] contour, int offsetX, int offsetY)
        {
            Point[] shifted = new Point[contour.Length];
            for (int i = 0; i < contour.Length; i++)
                shifted[i] = new Point(contour[i].X + offsetX, contour[i].Y + offsetY);
            return shifted;
        }

        /// <summary>
        /// 从矩形创建不规则框。
        /// </summary>
        /// <param name="rect">矩形。</param>
        /// <returns>矩形四角框。</returns>
        private static Box CreateBoxFromRect(Rect rect)
        {
            return new Box(
                new PointF(rect.Left, rect.Top),
                new PointF(rect.Right, rect.Top),
                new PointF(rect.Left, rect.Bottom),
                new PointF(rect.Right, rect.Bottom));
        }

        /// <summary>
        /// 计算轮廓四个极点。
        /// </summary>
        /// <param name="contour">轮廓点。</param>
        /// <param name="topLeft">左上点。</param>
        /// <param name="topRight">右上点。</param>
        /// <param name="bottomRight">右下点。</param>
        /// <param name="bottomLeft">左下点。</param>
        private static void GetExtremeCorners(Point[] contour, out Point topLeft, out Point topRight, out Point bottomRight, out Point bottomLeft)
        {
            if (contour == null || contour.Length == 0)
            {
                topLeft = new Point(0, 0);
                topRight = new Point(0, 0);
                bottomRight = new Point(0, 0);
                bottomLeft = new Point(0, 0);
                return;
            }

            topLeft = contour[0];
            topRight = contour[0];
            bottomRight = contour[0];
            bottomLeft = contour[0];

            foreach (Point point in contour)
            {
                if (point.X + point.Y < topLeft.X + topLeft.Y)
                    topLeft = point;
                if (point.X - point.Y > topRight.X - topRight.Y)
                    topRight = point;
                if (point.X + point.Y > bottomRight.X + bottomRight.Y)
                    bottomRight = point;
                if (point.X - point.Y < bottomLeft.X - bottomLeft.Y)
                    bottomLeft = point;
            }
        }

        /// <summary>
        /// 将坐标转换为OpenCvSharp矩形。
        /// </summary>
        /// <param name="x1">左上X。</param>
        /// <param name="y1">左上Y。</param>
        /// <param name="x2">右下X。</param>
        /// <param name="y2">右下Y。</param>
        /// <param name="deltaX">结果X方向偏移。</param>
        /// <param name="deltaY">结果Y方向偏移。</param>
        /// <returns>矩形。</returns>
        private static Rect ToRect(float x1, float y1, float x2, float y2, int deltaX, int deltaY)
        {
            int left = (int)Math.Round(Math.Min(x1, x2)) + deltaX;
            int top = (int)Math.Round(Math.Min(y1, y2)) + deltaY;
            int right = (int)Math.Round(Math.Max(x1, x2)) + deltaX;
            int bottom = (int)Math.Round(Math.Max(y1, y2)) + deltaY;
            return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
        }

        /// <summary>
        /// 按通道数获取新版DLL像素格式。
        /// </summary>
        /// <param name="channels">图像通道数。</param>
        /// <returns>像素格式枚举值。</returns>
        private static int GetPixelFormat(int channels)
        {
            switch (channels)
            {
                case 1:
                    return (int)YoloPixelFormat.Gray;
                case 2:
                    return (int)YoloPixelFormat.TwoChannel;
                case 3:
                    return (int)YoloPixelFormat.Bgr;
                case 4:
                    return (int)YoloPixelFormat.Bgra;
                default:
                    throw new Exception("暂不支持的AI输入图像通道数：" + channels);
            }
        }

        /// <summary>
        /// 弧度转角度。
        /// </summary>
        /// <param name="radian">弧度。</param>
        /// <returns>角度。</returns>
        private static float RadianToDegree(float radian)
        {
            return (float)(radian * 180.0 / Math.PI);
        }

        /// <summary>
        /// 读取UTF-8原生字符串。
        /// </summary>
        /// <param name="ptr">字符串指针。</param>
        /// <returns>托管字符串。</returns>
        private static string PtrToUtf8(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return string.Empty;

            int length = 0;
            while (Marshal.ReadByte(ptr, length) != 0)
                length++;

            byte[] bytes = new byte[length];
            Marshal.Copy(ptr, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// 解码原生错误缓冲区。
        /// </summary>
        /// <param name="buffer">错误缓冲区。</param>
        /// <returns>错误文本。</returns>
        private static string DecodeError(byte[] buffer)
        {
            int length = Array.IndexOf(buffer, (byte)0);
            if (length < 0)
                length = buffer.Length;

            string text = Encoding.UTF8.GetString(buffer, 0, length);
            return string.IsNullOrWhiteSpace(text) ? "未知错误" : text;
        }

        /// <summary>
        /// 解析模型标签JSON。
        /// </summary>
        /// <param name="labelsJson">标签JSON。</param>
        /// <returns>标签集合。</returns>
        private static List<string> ParseLabels(string labelsJson)
        {
            if (string.IsNullOrWhiteSpace(labelsJson))
                return new List<string>();

            try
            {
                return JsonConvert.DeserializeObject<List<string>>(labelsJson) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// YOLO native调用适配器，用于在同一套托管结果解析逻辑下切换不同DLL。
        /// </summary>
        private interface IYoloOpenVinoNativeMethods
        {
            /// <summary>
            /// 会话名称。
            /// </summary>
            string SessionName { get; }

            /// <summary>
            /// 设备显示名称。
            /// </summary>
            string DeviceText { get; }

            /// <summary>
            /// native设备模式。
            /// </summary>
            YoloOpenVinoDeviceMode DeviceMode { get; }

            /// <summary>
            /// 初始化新版YOLO模型。
            /// </summary>
            /// <param name="onnxPath">UTF-8模型路径。</param>
            /// <param name="mode">设备模式。</param>
            /// <param name="handle">模型句柄。</param>
            /// <param name="initInfo">初始化信息。</param>
            /// <param name="errorMsg">错误信息缓冲区。</param>
            /// <param name="errorCapacity">错误信息缓冲区大小。</param>
            /// <returns>1表示成功，0表示失败。</returns>
            int YoloInit(IntPtr onnxPath, int mode, out IntPtr handle, out IntPtr initInfo, byte[] errorMsg, int errorCapacity);

            /// <summary>
            /// 执行新版YOLO推理。
            /// </summary>
            /// <param name="handle">模型句柄。</param>
            /// <param name="image">图像结构。</param>
            /// <param name="confThreshold">置信度阈值。</param>
            /// <param name="nmsThreshold">NMS阈值。</param>
            /// <param name="result">推理结果。</param>
            /// <param name="errorMsg">错误信息缓冲区。</param>
            /// <param name="errorCapacity">错误信息缓冲区大小。</param>
            /// <returns>1表示成功，0表示失败。</returns>
            int YoloInfer(IntPtr handle, ref YoloImage image, float confThreshold, float nmsThreshold, out IntPtr result, byte[] errorMsg, int errorCapacity);

            /// <summary>
            /// 释放新版YOLO资源。
            /// </summary>
            /// <param name="resource">资源指针。</param>
            /// <returns>1表示成功，0表示失败。</returns>
            int YoloRelease(IntPtr resource);
        }

        /// <summary>
        /// td_det.dll CPU调用适配器。
        /// </summary>
        private sealed class CpuNativeMethods : IYoloOpenVinoNativeMethods
        {
            /// <summary>
            /// CPU调用适配器单例。
            /// </summary>
            public static readonly CpuNativeMethods Instance = new CpuNativeMethods();

            /// <summary>
            /// 会话名称。
            /// </summary>
            public string SessionName { get { return "YoloOpenVinoCpuSession"; } }

            /// <summary>
            /// 设备显示名称。
            /// </summary>
            public string DeviceText { get { return "CPU"; } }

            /// <summary>
            /// native设备模式。
            /// </summary>
            public YoloOpenVinoDeviceMode DeviceMode { get { return YoloOpenVinoDeviceMode.Cpu; } }

            /// <summary>
            /// 私有构造函数，确保调用适配器复用同一个实例。
            /// </summary>
            private CpuNativeMethods()
            {
            }

            /// <summary>
            /// 调用CPU DLL初始化模型。
            /// </summary>
            public int YoloInit(IntPtr onnxPath, int mode, out IntPtr handle, out IntPtr initInfo, byte[] errorMsg, int errorCapacity)
            {
                return NativeYoloInit(onnxPath, mode, out handle, out initInfo, errorMsg, errorCapacity);
            }

            /// <summary>
            /// 调用CPU DLL执行推理。
            /// </summary>
            public int YoloInfer(IntPtr handle, ref YoloImage image, float confThreshold, float nmsThreshold, out IntPtr result, byte[] errorMsg, int errorCapacity)
            {
                return NativeYoloInfer(handle, ref image, confThreshold, nmsThreshold, out result, errorMsg, errorCapacity);
            }

            /// <summary>
            /// 调用CPU DLL释放资源。
            /// </summary>
            public int YoloRelease(IntPtr resource)
            {
                return NativeYoloRelease(resource);
            }

            /// <summary>
            /// 初始化CPU模型。
            /// </summary>
            [DllImport(CpuDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "yolo_init")]
            private static extern int NativeYoloInit(IntPtr onnxPath, int mode, out IntPtr handle, out IntPtr initInfo, [Out] byte[] errorMsg, int errorCapacity);

            /// <summary>
            /// 执行CPU推理。
            /// </summary>
            [DllImport(CpuDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "yolo_infer")]
            private static extern int NativeYoloInfer(IntPtr handle, ref YoloImage image, float confThreshold, float nmsThreshold, out IntPtr result, [Out] byte[] errorMsg, int errorCapacity);

            /// <summary>
            /// 释放CPU资源。
            /// </summary>
            [DllImport(CpuDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "yolo_release")]
            private static extern int NativeYoloRelease(IntPtr resource);
        }

        /// <summary>
        /// yolo_openvino_tensorrt.dll GPU调用适配器。
        /// </summary>
        private sealed class GpuTensorRtNativeMethods : IYoloOpenVinoNativeMethods
        {
            /// <summary>
            /// GPU调用适配器单例。
            /// </summary>
            public static readonly GpuTensorRtNativeMethods Instance = new GpuTensorRtNativeMethods();

            /// <summary>
            /// 会话名称。
            /// </summary>
            public string SessionName { get { return "YoloOpenVinoTensorRtGpuSession"; } }

            /// <summary>
            /// 设备显示名称。
            /// </summary>
            public string DeviceText { get { return "GPU"; } }

            /// <summary>
            /// native设备模式。
            /// </summary>
            public YoloOpenVinoDeviceMode DeviceMode { get { return YoloOpenVinoDeviceMode.Gpu; } }

            /// <summary>
            /// 私有构造函数，确保调用适配器复用同一个实例。
            /// </summary>
            private GpuTensorRtNativeMethods()
            {
            }

            /// <summary>
            /// 调用TensorRT GPU DLL初始化模型。
            /// </summary>
            public int YoloInit(IntPtr onnxPath, int mode, out IntPtr handle, out IntPtr initInfo, byte[] errorMsg, int errorCapacity)
            {
                return NativeYoloInit(onnxPath, mode, out handle, out initInfo, errorMsg, errorCapacity);
            }

            /// <summary>
            /// 调用TensorRT GPU DLL执行推理。
            /// </summary>
            public int YoloInfer(IntPtr handle, ref YoloImage image, float confThreshold, float nmsThreshold, out IntPtr result, byte[] errorMsg, int errorCapacity)
            {
                return NativeYoloInfer(handle, ref image, confThreshold, nmsThreshold, out result, errorMsg, errorCapacity);
            }

            /// <summary>
            /// 调用TensorRT GPU DLL释放资源。
            /// </summary>
            public int YoloRelease(IntPtr resource)
            {
                return NativeYoloRelease(resource);
            }

            /// <summary>
            /// 初始化TensorRT GPU模型。
            /// </summary>
            [DllImport(GpuDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "yolo_init")]
            private static extern int NativeYoloInit(IntPtr onnxPath, int mode, out IntPtr handle, out IntPtr initInfo, [Out] byte[] errorMsg, int errorCapacity);

            /// <summary>
            /// 执行TensorRT GPU推理。
            /// </summary>
            [DllImport(GpuDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "yolo_infer")]
            private static extern int NativeYoloInfer(IntPtr handle, ref YoloImage image, float confThreshold, float nmsThreshold, out IntPtr result, [Out] byte[] errorMsg, int errorCapacity);

            /// <summary>
            /// 释放TensorRT GPU资源。
            /// </summary>
            [DllImport(GpuDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "yolo_release")]
            private static extern int NativeYoloRelease(IntPtr resource);
        }

        /// <summary>
        /// 新版YOLO设备模式。
        /// </summary>
        private enum YoloOpenVinoDeviceMode
        {
            /// <summary>
            /// CPU模式。
            /// </summary>
            Cpu = 0,

            /// <summary>
            /// GPU模式。
            /// </summary>
            Gpu = 1
        }

        /// <summary>
        /// 新版YOLO图像像素格式。
        /// </summary>
        private enum YoloPixelFormat
        {
            /// <summary>
            /// BGR三通道。
            /// </summary>
            Bgr = 0,

            /// <summary>
            /// BGRA四通道。
            /// </summary>
            Bgra = 2,

            /// <summary>
            /// 灰度单通道。
            /// </summary>
            Gray = 4,

            /// <summary>
            /// 双通道。
            /// </summary>
            TwoChannel = 5
        }

        /// <summary>
        /// UTF-8原生字符串。
        /// </summary>
        private sealed class Utf8NativeString : IDisposable
        {
            /// <summary>
            /// 原生字符串指针。
            /// </summary>
            public IntPtr Pointer { get; private set; }

            /// <summary>
            /// 创建UTF-8原生字符串。
            /// </summary>
            /// <param name="text">托管字符串。</param>
            public Utf8NativeString(string text)
            {
                byte[] bytes = Encoding.UTF8.GetBytes((text ?? string.Empty) + "\0");
                Pointer = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, Pointer, bytes.Length);
            }

            /// <summary>
            /// 释放UTF-8原生字符串。
            /// </summary>
            public void Dispose()
            {
                if (Pointer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(Pointer);
                    Pointer = IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// 新版YOLO初始化信息结构。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct YoloInitInfo
        {
            /// <summary>
            /// YOLO版本号。
            /// </summary>
            public int YoloVersion;

            /// <summary>
            /// 模型任务类型。
            /// </summary>
            public int Task;

            /// <summary>
            /// 模型规模。
            /// </summary>
            public int ModelScale;

            /// <summary>
            /// 输入通道数。
            /// </summary>
            public int InputChannels;

            /// <summary>
            /// 输入宽度。
            /// </summary>
            public int InputWidth;

            /// <summary>
            /// 输入高度。
            /// </summary>
            public int InputHeight;

            /// <summary>
            /// 类别数量。
            /// </summary>
            public int NumClasses;

            /// <summary>
            /// 标签数量。
            /// </summary>
            public int LabelCount;

            /// <summary>
            /// 标签JSON指针。
            /// </summary>
            public IntPtr LabelsJson;

            /// <summary>
            /// 模型时间戳。
            /// </summary>
            public long ModelTimestampMs;

            /// <summary>
            /// 模型时间文本指针。
            /// </summary>
            public IntPtr ModelDatetimeIso;

            /// <summary>
            /// 完整模型信息JSON指针。
            /// </summary>
            public IntPtr InfoJson;
        }

        /// <summary>
        /// 新版YOLO输入图像结构。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct YoloImage
        {
            /// <summary>
            /// 图像数据指针。
            /// </summary>
            public IntPtr Data;

            /// <summary>
            /// 图像宽度。
            /// </summary>
            public int Width;

            /// <summary>
            /// 图像高度。
            /// </summary>
            public int Height;

            /// <summary>
            /// 每行字节数。
            /// </summary>
            public int StrideBytes;

            /// <summary>
            /// 像素格式。
            /// </summary>
            public int PixelFormat;
        }

        /// <summary>
        /// 新版YOLO目标结构。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct YoloObject
        {
            /// <summary>
            /// 类别编号。
            /// </summary>
            public int ClassId;

            /// <summary>
            /// 置信度。
            /// </summary>
            public float Score;

            /// <summary>
            /// 左上X。
            /// </summary>
            public float X1;

            /// <summary>
            /// 左上Y。
            /// </summary>
            public float Y1;

            /// <summary>
            /// 右下X。
            /// </summary>
            public float X2;

            /// <summary>
            /// 右下Y。
            /// </summary>
            public float Y2;

            /// <summary>
            /// 中心X。
            /// </summary>
            public float Cx;

            /// <summary>
            /// 中心Y。
            /// </summary>
            public float Cy;

            /// <summary>
            /// 宽度。
            /// </summary>
            public float W;

            /// <summary>
            /// 高度。
            /// </summary>
            public float H;

            /// <summary>
            /// 旋转角弧度。
            /// </summary>
            public float AngleRad;

            /// <summary>
            /// 旋转框四角坐标。
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public float[] Corners;

            /// <summary>
            /// 分割掩膜索引。
            /// </summary>
            public int MaskIndex;

            /// <summary>
            /// 关键点起始索引。
            /// </summary>
            public int KeypointOffset;

            /// <summary>
            /// 关键点数量。
            /// </summary>
            public int KeypointCount;
        }

        /// <summary>
        /// 新版YOLO关键点结构。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct YoloKeypoint
        {
            /// <summary>
            /// 关键点X。
            /// </summary>
            public float X;

            /// <summary>
            /// 关键点Y。
            /// </summary>
            public float Y;

            /// <summary>
            /// 关键点置信度。
            /// </summary>
            public float Score;
        }

        /// <summary>
        /// 新版YOLO推理结果结构。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct YoloResult
        {
            /// <summary>
            /// 模型任务类型。
            /// </summary>
            public int Task;

            /// <summary>
            /// 原图宽度。
            /// </summary>
            public int ImageWidth;

            /// <summary>
            /// 原图高度。
            /// </summary>
            public int ImageHeight;

            /// <summary>
            /// 模型输入宽度。
            /// </summary>
            public int InputWidth;

            /// <summary>
            /// 模型输入高度。
            /// </summary>
            public int InputHeight;

            /// <summary>
            /// 目标数量。
            /// </summary>
            public int ObjectCount;

            /// <summary>
            /// 目标数组指针。
            /// </summary>
            public IntPtr Objects;

            /// <summary>
            /// 分类结果数量。
            /// </summary>
            public int ClassCount;

            /// <summary>
            /// 分类结果数组指针。
            /// </summary>
            public IntPtr Classes;

            /// <summary>
            /// 关键点数量。
            /// </summary>
            public int KeypointCount;

            /// <summary>
            /// 关键点数组指针。
            /// </summary>
            public IntPtr Keypoints;

            /// <summary>
            /// 掩膜数量。
            /// </summary>
            public int MaskCount;

            /// <summary>
            /// 掩膜宽度。
            /// </summary>
            public int MaskWidth;

            /// <summary>
            /// 掩膜高度。
            /// </summary>
            public int MaskHeight;

            /// <summary>
            /// 掩膜步长。
            /// </summary>
            public int MaskStride;

            /// <summary>
            /// 掩膜数组指针。
            /// </summary>
            public IntPtr Masks;

            /// <summary>
            /// 缩放比例。
            /// </summary>
            public float Scale;

            /// <summary>
            /// X方向填充。
            /// </summary>
            public float PadX;

            /// <summary>
            /// Y方向填充。
            /// </summary>
            public float PadY;

            /// <summary>
            /// 预处理耗时。
            /// </summary>
            public double PreprocessMs;

            /// <summary>
            /// 推理耗时。
            /// </summary>
            public double InferMs;

            /// <summary>
            /// 后处理耗时。
            /// </summary>
            public double PostprocessMs;

            /// <summary>
            /// 总耗时。
            /// </summary>
            public double TotalMs;
        }
    }

    /// <summary>
    /// YOLO OpenVINO模型初始化信息。
    /// </summary>
    internal sealed class YoloOpenVinoModelInfo
    {
        /// <summary>
        /// YOLO版本号。
        /// </summary>
        public int YoloVersion { get; set; }

        /// <summary>
        /// 模型任务类型。
        /// </summary>
        public int Task { get; set; }

        /// <summary>
        /// 模型规模。
        /// </summary>
        public int ModelScale { get; set; }

        /// <summary>
        /// 输入通道数。
        /// </summary>
        public int InputChannels { get; set; }

        /// <summary>
        /// 输入宽度。
        /// </summary>
        public int InputWidth { get; set; }

        /// <summary>
        /// 输入高度。
        /// </summary>
        public int InputHeight { get; set; }

        /// <summary>
        /// 类别数量。
        /// </summary>
        public int NumClasses { get; set; }

        /// <summary>
        /// 标签数量。
        /// </summary>
        public int LabelCount { get; set; }

        /// <summary>
        /// 标签JSON。
        /// </summary>
        public string LabelsJson { get; set; }

        /// <summary>
        /// 模型时间戳。
        /// </summary>
        public long ModelTimestampMs { get; set; }

        /// <summary>
        /// 模型时间文本。
        /// </summary>
        public string ModelDatetimeIso { get; set; }

        /// <summary>
        /// 完整模型信息JSON。
        /// </summary>
        public string InfoJson { get; set; }
    }

    /// <summary>
    /// YOLO OpenVINO托管推理结果。
    /// </summary>
    internal sealed class YoloOpenVinoInferenceResult
    {
        /// <summary>
        /// 模型任务类型。
        /// </summary>
        public int Task { get; set; }

        /// <summary>
        /// 原图宽度。
        /// </summary>
        public int ImageWidth { get; set; }

        /// <summary>
        /// 原图高度。
        /// </summary>
        public int ImageHeight { get; set; }

        /// <summary>
        /// 模型输入宽度。
        /// </summary>
        public int InputWidth { get; set; }

        /// <summary>
        /// 模型输入高度。
        /// </summary>
        public int InputHeight { get; set; }

        /// <summary>
        /// 掩膜宽度。
        /// </summary>
        public int MaskWidth { get; set; }

        /// <summary>
        /// 掩膜高度。
        /// </summary>
        public int MaskHeight { get; set; }

        /// <summary>
        /// 掩膜步长。
        /// </summary>
        public int MaskStride { get; set; }

        /// <summary>
        /// 目标集合。
        /// </summary>
        public List<YoloOpenVinoCpuSession.YoloObject> Objects { get; private set; }

        /// <summary>
        /// 关键点集合。
        /// </summary>
        public List<YoloOpenVinoCpuSession.YoloKeypoint> Keypoints { get; private set; }

        /// <summary>
        /// 掩膜字节集合。
        /// </summary>
        public List<byte[]> Masks { get; private set; }

        /// <summary>
        /// 创建托管推理结果。
        /// </summary>
        public YoloOpenVinoInferenceResult()
        {
            Objects = new List<YoloOpenVinoCpuSession.YoloObject>();
            Keypoints = new List<YoloOpenVinoCpuSession.YoloKeypoint>();
            Masks = new List<byte[]>();
        }
    }
}
