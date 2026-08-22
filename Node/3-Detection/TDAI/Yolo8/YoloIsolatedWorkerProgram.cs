using Newtonsoft.Json;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;
using Point = OpenCvSharp.Point;

namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    /// <summary>
    /// YOLO隔离worker入口，在独立进程中加载YOLO native运行环境并执行推理。
    /// </summary>
    internal static class YoloIsolatedWorkerProgram
    {
        /// <summary>
        /// 当前worker中持有的YOLO模型。
        /// </summary>
        private static IYolo8 currentModel;

        /// <summary>
        /// 当前父进程共享图像映射。
        /// </summary>
        private static MemoryMappedFile currentImageMap;

        /// <summary>
        /// worker只读共享图像视图。
        /// </summary>
        private static MemoryMappedViewAccessor currentImageView;

        /// <summary>
        /// 当前已打开的共享图像映射名称。
        /// </summary>
        private static string currentImageMapName;

        /// <summary>
        /// 当前已打开的共享图像映射容量。
        /// </summary>
        private static long currentImageMapCapacity;

        /// <summary>
        /// 如果当前启动参数为YOLO worker，则运行worker并阻止主程序继续启动。
        /// </summary>
        /// <param name="args">启动参数。</param>
        /// <returns>已作为worker处理返回true。</returns>
        public static bool TryRun(string[] args)
        {
            if (!YoloIsolatedRuntimeContext.HasWorkerArgument(args))
                return false;

            YoloIsolatedRuntimeContext.MarkAsWorkerProcess();
            // WinForms 的 WinExe 子进程没有控制台，直接设置 Console 编码会抛 IOException。
            // 此处只包装父进程传入的重定向句柄，协议仍固定使用无 BOM 的 UTF-8。
            using (StreamReader input = new StreamReader(
                Console.OpenStandardInput(),
                new UTF8Encoding(false),
                false,
                4096))
            using (StreamWriter output = new StreamWriter(
                Console.OpenStandardOutput(),
                new UTF8Encoding(false),
                4096))
            {
                output.AutoFlush = true;
                RunLoop(input, output);
            }

            return true;
        }

        /// <summary>
        /// 运行标准输入输出JSON消息循环。
        /// </summary>
        /// <param name="input">父进程请求输入流。</param>
        /// <param name="output">worker响应输出流。</param>
        private static void RunLoop(TextReader input, TextWriter output)
        {
            try
            {
                string line;
                while ((line = input.ReadLine()) != null)
                {
                    YoloIsolatedResponse response;
                    try
                    {
                        YoloIsolatedRequest request = JsonConvert.DeserializeObject<YoloIsolatedRequest>(line);
                        response = HandleRequest(request);
                    }
                    catch (Exception ex)
                    {
                        response = new YoloIsolatedResponse
                        {
                            Success = false,
                            Error = ex.Message
                        };
                    }

                    output.WriteLine(JsonConvert.SerializeObject(response, Formatting.None));
                    output.Flush();
                }
            }
            finally
            {
                try
                {
                    DestroyCurrentModel();
                }
                finally
                {
                    ReleaseSharedImageMap();
                }
            }
        }

        /// <summary>
        /// 处理一条worker请求。
        /// </summary>
        /// <param name="request">请求消息。</param>
        /// <returns>响应消息。</returns>
        private static YoloIsolatedResponse HandleRequest(YoloIsolatedRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            switch (request.Command)
            {
                case "ping":
                    return Success();
                case "validate-image":
                    return ValidateSharedImage(request);
                case "init":
                    InitializeModel(request);
                    return Success();
                case "detect":
                    return Detect(request);
                case "destroy":
                    DestroyCurrentModel();
                    return Success();
                default:
                    throw new InvalidOperationException("未知YOLO worker命令：" + request.Command);
            }
        }

        /// <summary>
        /// 初始化worker中的YOLO模型。
        /// </summary>
        /// <param name="request">初始化请求。</param>
        private static void InitializeModel(YoloIsolatedRequest request)
        {
            DestroyCurrentModel();

            IYolo8 model = CreateModel(request.ModelType);
            model.Init(
                request.ModelPath,
                request.DeviceType,
                request.ClassNames ?? new string[0],
                request.InputSize,
                request.ScoreThreshold,
                request.NmsThreshold,
                request.KeyPointNum);

            currentModel = model;
        }

        /// <summary>
        /// 执行worker推理。
        /// </summary>
        /// <param name="request">推理请求。</param>
        /// <returns>推理响应。</returns>
        private static YoloIsolatedResponse Detect(YoloIsolatedRequest request)
        {
            if (currentModel == null)
                throw new InvalidOperationException("YOLO worker模型尚未初始化。");

            using (SharedImageMatLease imageLease = CreateMat(request))
            {
                Mat image = imageLease.Image;
                switch (currentModel.ModelType)
                {
                    case ModelType.DET:
                        return SuccessWithDet(((Yolo8Det)currentModel).Detect(image, request.DeltaX, request.DeltaY));
                    case ModelType.OBB:
                        return SuccessWithObb(((Yolo8Obb)currentModel).Detect(image, request.DeltaX, request.DeltaY));
                    case ModelType.SEG:
                        return SuccessWithSeg(((Yolo8Seg)currentModel).Detect(image, request.DeltaX, request.DeltaY, request.NeedMaskBox));
                    case ModelType.POSE:
                        return SuccessWithPose(((Yolo8Pose)currentModel).Detect(image, request.DeltaX, request.DeltaY));
                    default:
                        throw new InvalidOperationException("不支持的YOLO模型类型：" + currentModel.ModelType);
                }
            }
        }

        /// <summary>
        /// 按模型类型创建worker内部本地YOLO模型。
        /// </summary>
        /// <param name="modelType">模型类型。</param>
        /// <returns>本地YOLO模型。</returns>
        private static IYolo8 CreateModel(ModelType modelType)
        {
            switch (modelType)
            {
                case ModelType.DET:
                    return new Yolo8Det();
                case ModelType.OBB:
                    return new Yolo8Obb();
                case ModelType.SEG:
                    return new Yolo8Seg();
                case ModelType.POSE:
                    return new Yolo8Pose();
                default:
                    throw new InvalidOperationException("不支持的YOLO模型类型：" + modelType);
            }
        }

        /// <summary>
        /// 从父进程共享内存建立零拷贝OpenCV Mat视图。
        /// </summary>
        /// <param name="request">推理请求。</param>
        /// <returns>同时管理Mat头和共享内存指针的租约。</returns>
        private static SharedImageMatLease CreateMat(YoloIsolatedRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ImageMapName))
                throw new InvalidOperationException("YOLO worker共享内存名称为空。");
            if (request.Rows <= 0 || request.Cols <= 0 || request.ImageStride <= 0L)
                throw new InvalidOperationException("YOLO worker共享图像尺寸或步长无效。");
            if (request.ImageByteCount <= 0L || request.ImageByteCount > YoloSharedImageTransport.MaximumImageBytes)
            {
                throw new InvalidOperationException(
                    "YOLO worker共享图像字节数无效，实际=" + request.ImageByteCount +
                    "，上限=" + YoloSharedImageTransport.MaximumImageBytes + "。");
            }
            if (request.ImageMapCapacity < request.ImageByteCount ||
                request.ImageMapCapacity > YoloSharedImageTransport.MaximumImageBytes)
                throw new InvalidOperationException("YOLO worker共享内存容量与图像字节数不匹配。");

            EnsureSharedImageMap(request.ImageMapName, request.ImageMapCapacity);
            Thread.MemoryBarrier();
            return new SharedImageMatLease(
                currentImageView,
                request.Rows,
                request.Cols,
                request.MatType,
                request.ImageStride,
                request.ImageByteCount);
        }

        /// <summary>
        /// 不加载模型地校验共享内存图像内容，供部署诊断和自动测试使用。
        /// </summary>
        /// <param name="request">共享图像请求。</param>
        /// <returns>包含全部通道像素和的响应。</returns>
        private static YoloIsolatedResponse ValidateSharedImage(YoloIsolatedRequest request)
        {
            using (SharedImageMatLease imageLease = CreateMat(request))
            {
                Scalar sum = Cv2.Sum(imageLease.Image);
                return new YoloIsolatedResponse
                {
                    Success = true,
                    SharedImageValueSum = sum.Val0 + sum.Val1 + sum.Val2 + sum.Val3
                };
            }
        }

        /// <summary>
        /// 打开父进程当前共享图像映射，名称未变化时复用现有视图。
        /// </summary>
        /// <param name="mapName">共享内存名称。</param>
        /// <param name="capacity">共享内存容量。</param>
        private static void EnsureSharedImageMap(string mapName, long capacity)
        {
            if (currentImageMap != null &&
                string.Equals(currentImageMapName, mapName, StringComparison.Ordinal) &&
                currentImageMapCapacity == capacity)
                return;

            ReleaseSharedImageMap();
            currentImageMap = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.Read);
            currentImageView = currentImageMap.CreateViewAccessor(0L, capacity, MemoryMappedFileAccess.Read);
            currentImageMapName = mapName;
            currentImageMapCapacity = capacity;
        }

        /// <summary>
        /// 释放worker侧共享图像映射和视图。
        /// </summary>
        private static void ReleaseSharedImageMap()
        {
            MemoryMappedViewAccessor view = currentImageView;
            MemoryMappedFile map = currentImageMap;
            currentImageView = null;
            currentImageMap = null;
            currentImageMapName = null;
            currentImageMapCapacity = 0L;

            try
            {
                if (view != null)
                    view.Dispose();
            }
            finally
            {
                if (map != null)
                    map.Dispose();
            }
        }

        /// <summary>
        /// 管理一次共享图像推理期间的Mat头和映射指针生命周期。
        /// </summary>
        private sealed unsafe class SharedImageMatLease : IDisposable
        {
            /// <summary>
            /// 当前共享内存视图。
            /// </summary>
            private readonly MemoryMappedViewAccessor view;

            /// <summary>
            /// 当前取得的视图起始指针。
            /// </summary>
            private byte* viewPointer;

            /// <summary>
            /// 标记指针是否已经成功取得。
            /// </summary>
            private bool pointerAcquired;

            /// <summary>
            /// 使用共享内存建立Mat头，并验证有效图像范围没有越界。
            /// </summary>
            /// <param name="view">共享内存视图。</param>
            /// <param name="rows">图像行数。</param>
            /// <param name="cols">图像列数。</param>
            /// <param name="matType">Mat类型。</param>
            /// <param name="stride">图像行步长。</param>
            /// <param name="imageByteCount">有效图像字节数。</param>
            public SharedImageMatLease(
                MemoryMappedViewAccessor view,
                int rows,
                int cols,
                int matType,
                long stride,
                long imageByteCount)
            {
                this.view = view ?? throw new ArgumentNullException(nameof(view));
                view.SafeMemoryMappedViewHandle.AcquirePointer(ref viewPointer);
                pointerAcquired = true;
                try
                {
                    IntPtr imagePointer = new IntPtr(viewPointer + view.PointerOffset);
                    Image = Mat.FromPixelData(rows, cols, matType, imagePointer, stride);
                    long minimumBytes = checked(((long)rows - 1L) * stride + ((long)cols * Image.ElemSize()));
                    if (minimumBytes > imageByteCount)
                        throw new InvalidOperationException("YOLO worker共享图像有效字节数不足。");
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            /// <summary>
            /// 获取本次推理使用的零拷贝Mat头。
            /// </summary>
            public Mat Image { get; private set; }

            /// <summary>
            /// 先释放Mat头，再归还共享内存视图指针。
            /// </summary>
            public void Dispose()
            {
                try
                {
                    if (Image != null)
                    {
                        Image.Dispose();
                        Image = null;
                    }
                }
                finally
                {
                    if (pointerAcquired)
                    {
                        view.SafeMemoryMappedViewHandle.ReleasePointer();
                        pointerAcquired = false;
                        viewPointer = null;
                    }
                }
            }
        }

        /// <summary>
        /// 销毁worker当前模型。
        /// </summary>
        private static void DestroyCurrentModel()
        {
            if (currentModel == null)
                return;

            currentModel.Destroy();
            currentModel = null;
        }

        /// <summary>
        /// 创建成功响应。
        /// </summary>
        /// <returns>成功响应。</returns>
        private static YoloIsolatedResponse Success()
        {
            return new YoloIsolatedResponse { Success = true };
        }

        /// <summary>
        /// 创建DET成功响应。
        /// </summary>
        /// <param name="results">DET结果。</param>
        /// <returns>成功响应。</returns>
        private static YoloIsolatedResponse SuccessWithDet(List<DetResult> results)
        {
            List<YoloIsolatedDetResult> dtoResults = new List<YoloIsolatedDetResult>();
            foreach (DetResult result in results)
            {
                dtoResults.Add(new YoloIsolatedDetResult
                {
                    ClassId = result.ClassId,
                    Score = result.Score,
                    X = result.Box.X,
                    Y = result.Box.Y,
                    Width = result.Box.Width,
                    Height = result.Box.Height
                });
            }

            return new YoloIsolatedResponse { Success = true, DetResults = dtoResults };
        }

        /// <summary>
        /// 创建OBB成功响应。
        /// </summary>
        /// <param name="results">OBB结果。</param>
        /// <returns>成功响应。</returns>
        private static YoloIsolatedResponse SuccessWithObb(List<ObbResult> results)
        {
            List<YoloIsolatedObbResult> dtoResults = new List<YoloIsolatedObbResult>();
            foreach (ObbResult result in results)
            {
                dtoResults.Add(new YoloIsolatedObbResult
                {
                    CenterX = result.center_x,
                    CenterY = result.center_y,
                    Width = result.width,
                    Height = result.height,
                    Angle = result.angle,
                    ClassId = result.class_id,
                    Confidence = result.confidence
                });
            }

            return new YoloIsolatedResponse { Success = true, ObbResults = dtoResults };
        }

        /// <summary>
        /// 创建SEG成功响应。
        /// </summary>
        /// <param name="results">SEG结果。</param>
        /// <returns>成功响应。</returns>
        private static YoloIsolatedResponse SuccessWithSeg(List<SegResult> results)
        {
            List<YoloIsolatedSegResult> dtoResults = new List<YoloIsolatedSegResult>();
            foreach (SegResult result in results)
            {
                dtoResults.Add(new YoloIsolatedSegResult
                {
                    ClassId = result.ClassId,
                    Score = result.Score,
                    X = result.Rect.X,
                    Y = result.Rect.Y,
                    Width = result.Rect.Width,
                    Height = result.Rect.Height,
                    TopLeftX = result.Box.TopLeft.X,
                    TopLeftY = result.Box.TopLeft.Y,
                    TopRightX = result.Box.TopRight.X,
                    TopRightY = result.Box.TopRight.Y,
                    BottomLeftX = result.Box.BottomLeft.X,
                    BottomLeftY = result.Box.BottomLeft.Y,
                    BottomRightX = result.Box.BottomRight.X,
                    BottomRightY = result.Box.BottomRight.Y
                });
            }

            return new YoloIsolatedResponse { Success = true, SegResults = dtoResults };
        }

        /// <summary>
        /// 创建POSE成功响应。
        /// </summary>
        /// <param name="result">POSE结果。</param>
        /// <returns>成功响应。</returns>
        private static YoloIsolatedResponse SuccessWithPose(PoseResult result)
        {
            YoloIsolatedPoseResult dtoResult = new YoloIsolatedPoseResult
            {
                Boxes = new List<YoloIsolatedDetResult>(),
                KeyPoints = new List<YoloIsolatedPoint>()
            };

            if (result.Boxs != null)
            {
                foreach (Rect box in result.Boxs)
                {
                    dtoResult.Boxes.Add(new YoloIsolatedDetResult
                    {
                        X = box.X,
                        Y = box.Y,
                        Width = box.Width,
                        Height = box.Height
                    });
                }
            }

            if (result.KeyPoints != null)
            {
                foreach (Point point in result.KeyPoints)
                {
                    dtoResult.KeyPoints.Add(new YoloIsolatedPoint { X = point.X, Y = point.Y });
                }
            }

            return new YoloIsolatedResponse { Success = true, PoseResult = dtoResult };
        }
    }
}
