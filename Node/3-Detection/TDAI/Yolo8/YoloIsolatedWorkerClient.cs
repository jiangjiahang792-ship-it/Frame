using Newtonsoft.Json;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Point = OpenCvSharp.Point;
using DiagnosticsProcess = System.Diagnostics.Process;
using DiagnosticsProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    /// <summary>
    /// 主程序侧YOLO隔离worker客户端，负责启动子进程并转发模型初始化和推理请求。
    /// </summary>
    internal sealed class YoloIsolatedWorkerClient : IDisposable
    {
        /// <summary>
        /// worker请求串行锁，保证同一模型句柄的请求顺序执行。
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// worker子进程。
        /// </summary>
        private DiagnosticsProcess process;

        /// <summary>
        /// worker标准输入。
        /// </summary>
        private StreamWriter input;

        /// <summary>
        /// worker标准输出。
        /// </summary>
        private StreamReader output;

        /// <summary>
        /// worker标准错误输出，用于在子进程异常退出时返回真实原因。
        /// </summary>
        private StreamReader errorOutput;

        /// <summary>
        /// 当前模型worker独享的命名共享内存。
        /// </summary>
        private MemoryMappedFile sharedImageMap;

        /// <summary>
        /// 主进程用于写入图像的共享内存视图。
        /// </summary>
        private MemoryMappedViewAccessor sharedImageView;

        /// <summary>
        /// 当前共享内存名称，扩容时生成新名称。
        /// </summary>
        private string sharedImageMapName;

        /// <summary>
        /// 当前共享图像映射容量。
        /// </summary>
        private long sharedImageMapCapacity;

        /// <summary>
        /// 当前模型路径。
        /// </summary>
        private string modelPath;

        /// <summary>
        /// 当前模型类型。
        /// </summary>
        private ModelType modelType;

        /// <summary>
        /// 当前推理设备。
        /// </summary>
        private DeviceType deviceType;

        /// <summary>
        /// 标记当前客户端是否已经释放。
        /// </summary>
        private bool disposed;

        /// <summary>
        /// 打开一个隔离worker模型会话。
        /// </summary>
        /// <param name="modelPath">模型路径。</param>
        /// <param name="modelType">模型类型。</param>
        /// <param name="deviceType">推理设备。</param>
        /// <param name="classNames">类别名称。</param>
        /// <param name="inputSize">输入尺寸。</param>
        /// <param name="scoreThreshold">置信度阈值。</param>
        /// <param name="nmsThreshold">NMS阈值。</param>
        /// <param name="keyPointNum">关键点数量。</param>
        /// <returns>已初始化的worker客户端。</returns>
        public static YoloIsolatedWorkerClient Open(
            string modelPath,
            ModelType modelType,
            DeviceType deviceType,
            string[] classNames,
            int inputSize,
            float scoreThreshold,
            float nmsThreshold,
            int keyPointNum)
        {
            YoloIsolatedWorkerClient client = new YoloIsolatedWorkerClient();
            try
            {
                client.StartWorker();
                client.InitializeModel(modelPath, modelType, deviceType, classNames, inputSize, scoreThreshold, nmsThreshold, keyPointNum);
                return client;
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 获取当前模型路径。
        /// </summary>
        public string ModelPath
        {
            get { return modelPath; }
        }

        /// <summary>
        /// 获取当前模型类型。
        /// </summary>
        public ModelType ModelType
        {
            get { return modelType; }
        }

        /// <summary>
        /// 获取当前推理设备。
        /// </summary>
        public DeviceType DeviceType
        {
            get { return deviceType; }
        }

        /// <summary>
        /// 执行DET检测。
        /// </summary>
        /// <param name="image">输入图像。</param>
        /// <param name="scoreThreshold">置信度阈值。</param>
        /// <param name="nmsThreshold">NMS阈值。</param>
        /// <param name="deltaX">X方向偏移。</param>
        /// <param name="deltaY">Y方向偏移。</param>
        /// <returns>DET结果。</returns>
        public List<DetResult> DetectDet(Mat image, float scoreThreshold, float nmsThreshold, int deltaX, int deltaY)
        {
            YoloIsolatedResponse response = SendDetectRequest(image, scoreThreshold, nmsThreshold, deltaX, deltaY, true);
            List<DetResult> results = new List<DetResult>();
            if (response.DetResults == null)
                return results;

            foreach (YoloIsolatedDetResult item in response.DetResults)
            {
                results.Add(new DetResult
                {
                    ClassId = item.ClassId,
                    Score = item.Score,
                    Box = new Rect(item.X, item.Y, item.Width, item.Height)
                });
            }

            return results;
        }

        /// <summary>
        /// 执行OBB检测。
        /// </summary>
        /// <param name="image">输入图像。</param>
        /// <param name="scoreThreshold">置信度阈值。</param>
        /// <param name="nmsThreshold">NMS阈值。</param>
        /// <param name="deltaX">X方向偏移。</param>
        /// <param name="deltaY">Y方向偏移。</param>
        /// <returns>OBB结果。</returns>
        public List<ObbResult> DetectObb(Mat image, float scoreThreshold, float nmsThreshold, int deltaX, int deltaY)
        {
            YoloIsolatedResponse response = SendDetectRequest(image, scoreThreshold, nmsThreshold, deltaX, deltaY, true);
            List<ObbResult> results = new List<ObbResult>();
            if (response.ObbResults == null)
                return results;

            foreach (YoloIsolatedObbResult item in response.ObbResults)
            {
                results.Add(new ObbResult
                {
                    center_x = item.CenterX,
                    center_y = item.CenterY,
                    width = item.Width,
                    height = item.Height,
                    angle = item.Angle,
                    class_id = item.ClassId,
                    confidence = item.Confidence
                });
            }

            return results;
        }

        /// <summary>
        /// 执行SEG检测。
        /// </summary>
        /// <param name="image">输入图像。</param>
        /// <param name="scoreThreshold">置信度阈值。</param>
        /// <param name="nmsThreshold">NMS阈值。</param>
        /// <param name="deltaX">X方向偏移。</param>
        /// <param name="deltaY">Y方向偏移。</param>
        /// <param name="needMaskBox">是否需要掩膜不规则框。</param>
        /// <returns>SEG结果。</returns>
        public List<SegResult> DetectSeg(Mat image, float scoreThreshold, float nmsThreshold, int deltaX, int deltaY, bool needMaskBox)
        {
            YoloIsolatedResponse response = SendDetectRequest(image, scoreThreshold, nmsThreshold, deltaX, deltaY, needMaskBox);
            List<SegResult> results = new List<SegResult>();
            if (response.SegResults == null)
                return results;

            foreach (YoloIsolatedSegResult item in response.SegResults)
            {
                results.Add(new SegResult
                {
                    ClassId = item.ClassId,
                    Score = item.Score,
                    Rect = new Rect(item.X, item.Y, item.Width, item.Height),
                    Box = new Box(
                        new PointF(item.TopLeftX, item.TopLeftY),
                        new PointF(item.TopRightX, item.TopRightY),
                        new PointF(item.BottomLeftX, item.BottomLeftY),
                        new PointF(item.BottomRightX, item.BottomRightY))
                });
            }

            return results;
        }

        /// <summary>
        /// 执行POSE检测。
        /// </summary>
        /// <param name="image">输入图像。</param>
        /// <param name="scoreThreshold">置信度阈值。</param>
        /// <param name="nmsThreshold">NMS阈值。</param>
        /// <param name="deltaX">X方向偏移。</param>
        /// <param name="deltaY">Y方向偏移。</param>
        /// <returns>POSE结果。</returns>
        public PoseResult DetectPose(Mat image, float scoreThreshold, float nmsThreshold, int deltaX, int deltaY)
        {
            YoloIsolatedResponse response = SendDetectRequest(image, scoreThreshold, nmsThreshold, deltaX, deltaY, true);
            PoseResult result = new PoseResult
            {
                Boxs = new List<Rect>(),
                KeyPoints = new List<Point>()
            };

            if (response.PoseResult == null)
                return result;

            if (response.PoseResult.Boxes != null)
            {
                foreach (YoloIsolatedDetResult box in response.PoseResult.Boxes)
                    result.Boxs.Add(new Rect(box.X, box.Y, box.Width, box.Height));
            }

            if (response.PoseResult.KeyPoints != null)
            {
                foreach (YoloIsolatedPoint point in response.PoseResult.KeyPoints)
                    result.KeyPoints.Add(new Point(point.X, point.Y));
            }

            return result;
        }

        /// <summary>
        /// 释放worker进程。
        /// </summary>
        public void Dispose()
        {
            lock (syncRoot)
            {
                if (disposed)
                    return;

                disposed = true;
                try
                {
                    if (process != null && !process.HasExited)
                        SendRequestCore(new YoloIsolatedRequest { Command = "destroy" });
                }
                catch
                {
                    // 释放阶段忽略worker通信异常，后续会直接结束进程。
                }

                DisposeSilently(input);
                input = null;
                DisposeSilently(output);
                output = null;
                DisposeSilently(errorOutput);
                errorOutput = null;
                try
                {
                    if (process != null && !process.HasExited)
                        process.Kill();
                }
                catch
                {
                    // worker可能已在状态检查后退出，关闭阶段无需上抛。
                }
                DisposeSilently(process);
                process = null;
                try
                {
                    ReleaseSharedImageMap();
                }
                catch
                {
                    // 关闭阶段逐资源尽力释放，避免影响主流程退出。
                }
            }
        }

        /// <summary>
        /// 在关闭路径中释放单个托管资源并隔离其异常。
        /// </summary>
        /// <param name="resource">待释放资源。</param>
        private static void DisposeSilently(IDisposable resource)
        {
            if (resource == null)
                return;

            try
            {
                resource.Dispose();
            }
            catch
            {
                // 关闭路径继续释放后续资源。
            }
        }

        /// <summary>
        /// 启动worker子进程。
        /// </summary>
        private void StartWorker()
        {
            string executablePath = Application.ExecutablePath;
            DiagnosticsProcessStartInfo startInfo = new DiagnosticsProcessStartInfo
            {
                FileName = executablePath,
                Arguments = YoloIsolatedRuntimeContext.WorkerArgument,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            startInfo.EnvironmentVariables[YoloIsolatedRuntimeContext.WorkerEnvironmentName] = "1";

            process = DiagnosticsProcess.Start(startInfo);
            if (process == null)
                throw new InvalidOperationException("启动YOLO隔离worker失败。");

            input = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false)) { AutoFlush = true };
            output = process.StandardOutput;
            errorOutput = process.StandardError;
        }

        /// <summary>
        /// 初始化worker模型。
        /// </summary>
        /// <param name="modelPath">模型路径。</param>
        /// <param name="modelType">模型类型。</param>
        /// <param name="deviceType">推理设备。</param>
        /// <param name="classNames">类别名称。</param>
        /// <param name="inputSize">输入尺寸。</param>
        /// <param name="scoreThreshold">置信度阈值。</param>
        /// <param name="nmsThreshold">NMS阈值。</param>
        /// <param name="keyPointNum">关键点数量。</param>
        private void InitializeModel(
            string modelPath,
            ModelType modelType,
            DeviceType deviceType,
            string[] classNames,
            int inputSize,
            float scoreThreshold,
            float nmsThreshold,
            int keyPointNum)
        {
            this.modelPath = modelPath;
            this.modelType = modelType;
            this.deviceType = deviceType;

            SendRequest(new YoloIsolatedRequest
            {
                Command = "init",
                ModelPath = modelPath,
                ModelType = modelType,
                DeviceType = deviceType,
                ClassNames = classNames ?? new string[0],
                InputSize = inputSize,
                ScoreThreshold = scoreThreshold,
                NmsThreshold = nmsThreshold,
                KeyPointNum = keyPointNum
            });
        }

        /// <summary>
        /// 将图像写入当前模型独享的共享内存并执行推理请求。
        /// </summary>
        /// <param name="image">输入图像。</param>
        /// <param name="scoreThreshold">置信度阈值。</param>
        /// <param name="nmsThreshold">NMS阈值。</param>
        /// <param name="deltaX">X方向偏移。</param>
        /// <param name="deltaY">Y方向偏移。</param>
        /// <param name="needMaskBox">是否需要掩膜不规则框。</param>
        /// <returns>推理响应。</returns>
        private YoloIsolatedResponse SendDetectRequest(Mat image, float scoreThreshold, float nmsThreshold, int deltaX, int deltaY, bool needMaskBox)
        {
            if (image == null || image.Empty())
                throw new InvalidOperationException("YOLO隔离worker输入图像为空。");

            lock (syncRoot)
            {
                ThrowIfDisposed();
                EnsureWorkerAlive();
                long rowByteCount = checked((long)image.Cols * image.ElemSize());
                long imageByteCount = checked((long)image.Rows * rowByteCount);
                EnsureSharedImageCapacity(imageByteCount);
                CopyImageToSharedMemory(image, rowByteCount, imageByteCount);
                Thread.MemoryBarrier();

                YoloIsolatedRequest request = new YoloIsolatedRequest
                {
                    Command = "detect",
                    ModelType = modelType,
                    DeviceType = deviceType,
                    ScoreThreshold = scoreThreshold,
                    NmsThreshold = nmsThreshold,
                    Rows = image.Rows,
                    Cols = image.Cols,
                    MatType = (int)image.Type(),
                    ImageMapName = sharedImageMapName,
                    ImageMapCapacity = sharedImageMapCapacity,
                    ImageByteCount = imageByteCount,
                    ImageStride = rowByteCount,
                    DeltaX = deltaX,
                    DeltaY = deltaY,
                    NeedMaskBox = needMaskBox
                };

                return SendRequestCore(request);
            }
        }

        /// <summary>
        /// 确保共享图像映射可容纳本帧，只有容量不足时才重建。
        /// </summary>
        /// <param name="requiredBytes">本帧所需字节数。</param>
        private void EnsureSharedImageCapacity(long requiredBytes)
        {
            if (requiredBytes <= 0L || requiredBytes > YoloSharedImageTransport.MaximumImageBytes)
            {
                throw new InvalidOperationException(
                    "YOLO共享图像字节数无效，实际=" + requiredBytes +
                    "，上限=" + YoloSharedImageTransport.MaximumImageBytes + "。");
            }
            if (sharedImageMap != null && sharedImageMapCapacity >= requiredBytes)
                return;

            ReleaseSharedImageMap();
            long growth = YoloSharedImageTransport.CapacityGrowthBytes;
            long capacity = checked(((requiredBytes + growth - 1L) / growth) * growth);
            sharedImageMapName = "TDJS_VISION_YOLO_" +
                DiagnosticsProcess.GetCurrentProcess().Id + "_" + Guid.NewGuid().ToString("N");
            sharedImageMap = MemoryMappedFile.CreateNew(
                sharedImageMapName,
                capacity,
                MemoryMappedFileAccess.ReadWrite);
            sharedImageView = sharedImageMap.CreateViewAccessor(
                0L,
                capacity,
                MemoryMappedFileAccess.ReadWrite);
            sharedImageMapCapacity = capacity;
        }

        /// <summary>
        /// 按源Mat真实步长逐行复制到紧凑共享内存，兼容非连续ROI。
        /// </summary>
        /// <param name="image">输入图像。</param>
        /// <param name="rowByteCount">每行有效字节数。</param>
        /// <param name="imageByteCount">整帧有效字节数。</param>
        private unsafe void CopyImageToSharedMemory(Mat image, long rowByteCount, long imageByteCount)
        {
            byte* viewPointer = null;
            sharedImageView.SafeMemoryMappedViewHandle.AcquirePointer(ref viewPointer);
            try
            {
                byte* destination = viewPointer + sharedImageView.PointerOffset;
                byte* source = (byte*)image.Data.ToPointer();
                long sourceStride = checked((long)image.Step());
                for (int row = 0; row < image.Rows; row++)
                {
                    Buffer.MemoryCopy(
                        source + (row * sourceStride),
                        destination + (row * rowByteCount),
                        imageByteCount - (row * rowByteCount),
                        rowByteCount);
                }
            }
            finally
            {
                if (viewPointer != null)
                    sharedImageView.SafeMemoryMappedViewHandle.ReleasePointer();
            }
        }

        /// <summary>
        /// 释放当前共享图像映射及其视图。
        /// </summary>
        private void ReleaseSharedImageMap()
        {
            MemoryMappedViewAccessor view = sharedImageView;
            MemoryMappedFile map = sharedImageMap;
            sharedImageView = null;
            sharedImageMap = null;
            sharedImageMapName = null;
            sharedImageMapCapacity = 0L;

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
        /// 向worker发送请求并读取响应。
        /// </summary>
        /// <param name="request">请求。</param>
        /// <returns>响应。</returns>
        private YoloIsolatedResponse SendRequest(YoloIsolatedRequest request)
        {
            lock (syncRoot)
            {
                ThrowIfDisposed();
                return SendRequestCore(request);
            }
        }

        /// <summary>
        /// 已关闭的客户端禁止继续发送模型或推理请求。
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(YoloIsolatedWorkerClient));
        }

        /// <summary>
        /// 在调用方已持有请求锁时发送一条小型JSON控制消息并读取响应。
        /// </summary>
        /// <param name="request">请求。</param>
        /// <returns>响应。</returns>
        private YoloIsolatedResponse SendRequestCore(YoloIsolatedRequest request)
        {
            EnsureWorkerAlive();
            string requestJson = JsonConvert.SerializeObject(request, Formatting.None);
            input.WriteLine(requestJson);

            string responseJson = output.ReadLine();
            if (responseJson == null)
                throw new InvalidOperationException(BuildWorkerExitMessage("YOLO隔离worker已退出，无法读取响应。"));

            YoloIsolatedResponse response = JsonConvert.DeserializeObject<YoloIsolatedResponse>(responseJson);
            if (response == null)
                throw new InvalidOperationException("YOLO隔离worker返回空响应。");
            if (!response.Success)
                throw new InvalidOperationException(response.Error ?? "YOLO隔离worker执行失败。");

            return response;
        }

        /// <summary>
        /// 确认worker进程仍在运行。
        /// </summary>
        private void EnsureWorkerAlive()
        {
            if (process == null)
                throw new ObjectDisposedException(nameof(YoloIsolatedWorkerClient));
            if (process.HasExited)
                throw new InvalidOperationException(BuildWorkerExitMessage("YOLO隔离worker已退出。"));
        }

        /// <summary>
        /// 组合worker退出码和标准错误，避免子进程故障被折叠成无原因的空响应。
        /// </summary>
        /// <param name="message">调用方错误摘要。</param>
        /// <returns>包含worker诊断信息的错误文本。</returns>
        private string BuildWorkerExitMessage(string message)
        {
            if (process == null)
                return message;

            try
            {
                if (!process.HasExited)
                    process.WaitForExit(1000);

                if (!process.HasExited)
                    return message + " worker进程尚未结束。";

                string standardError = errorOutput == null ? string.Empty : errorOutput.ReadToEnd();
                if (string.IsNullOrWhiteSpace(standardError))
                    return message + " 退出码：" + process.ExitCode + "。";

                return message + " 退出码：" + process.ExitCode + "；错误输出：" + standardError.Trim();
            }
            catch (Exception ex)
            {
                return message + " 读取worker诊断失败：" + ex.Message;
            }
        }
    }
}
