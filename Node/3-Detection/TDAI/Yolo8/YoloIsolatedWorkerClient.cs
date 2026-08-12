using Newtonsoft.Json;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
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
            YoloIsolatedRequest request = CreateDetectRequest(image, scoreThreshold, nmsThreshold, deltaX, deltaY, true);
            YoloIsolatedResponse response = SendRequest(request);
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
            YoloIsolatedRequest request = CreateDetectRequest(image, scoreThreshold, nmsThreshold, deltaX, deltaY, true);
            YoloIsolatedResponse response = SendRequest(request);
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
            YoloIsolatedRequest request = CreateDetectRequest(image, scoreThreshold, nmsThreshold, deltaX, deltaY, needMaskBox);
            YoloIsolatedResponse response = SendRequest(request);
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
            YoloIsolatedRequest request = CreateDetectRequest(image, scoreThreshold, nmsThreshold, deltaX, deltaY, true);
            YoloIsolatedResponse response = SendRequest(request);
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
            if (disposed)
                return;

            disposed = true;
            try
            {
                if (process != null && !process.HasExited)
                    SendRequest(new YoloIsolatedRequest { Command = "destroy" });
            }
            catch
            {
                // 释放阶段忽略worker通信异常，后续会直接结束进程。
            }

            try
            {
                if (input != null)
                    input.Dispose();
                if (output != null)
                    output.Dispose();
                if (process != null && !process.HasExited)
                    process.Kill();
                if (process != null)
                    process.Dispose();
            }
            catch
            {
                // 释放阶段不再向调用方抛异常，避免关闭模型时影响主流程。
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
        /// 创建推理请求。
        /// </summary>
        /// <param name="image">输入图像。</param>
        /// <param name="scoreThreshold">置信度阈值。</param>
        /// <param name="nmsThreshold">NMS阈值。</param>
        /// <param name="deltaX">X方向偏移。</param>
        /// <param name="deltaY">Y方向偏移。</param>
        /// <param name="needMaskBox">是否需要掩膜不规则框。</param>
        /// <returns>推理请求。</returns>
        private YoloIsolatedRequest CreateDetectRequest(Mat image, float scoreThreshold, float nmsThreshold, int deltaX, int deltaY, bool needMaskBox)
        {
            if (image == null || image.Empty())
                throw new InvalidOperationException("YOLO隔离worker输入图像为空。");

            using (Mat continuousImage = image.IsContinuous() ? image.Clone() : image.Clone())
            {
                int byteCount = (int)(continuousImage.Total() * continuousImage.ElemSize());
                byte[] bytes = new byte[byteCount];
                Marshal.Copy(continuousImage.Data, bytes, 0, byteCount);

                return new YoloIsolatedRequest
                {
                    Command = "detect",
                    ModelType = modelType,
                    DeviceType = deviceType,
                    ScoreThreshold = scoreThreshold,
                    NmsThreshold = nmsThreshold,
                    Rows = continuousImage.Rows,
                    Cols = continuousImage.Cols,
                    MatType = (int)continuousImage.Type(),
                    ImageBase64 = Convert.ToBase64String(bytes),
                    DeltaX = deltaX,
                    DeltaY = deltaY,
                    NeedMaskBox = needMaskBox
                };
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
                EnsureWorkerAlive();
                string requestJson = JsonConvert.SerializeObject(request, Formatting.None);
                input.WriteLine(requestJson);

                string responseJson = output.ReadLine();
                if (responseJson == null)
                    throw new InvalidOperationException("YOLO隔离worker已退出，无法读取响应。");

                YoloIsolatedResponse response = JsonConvert.DeserializeObject<YoloIsolatedResponse>(responseJson);
                if (response == null)
                    throw new InvalidOperationException("YOLO隔离worker返回空响应。");
                if (!response.Success)
                    throw new InvalidOperationException(response.Error ?? "YOLO隔离worker执行失败。");

                return response;
            }
        }

        /// <summary>
        /// 确认worker进程仍在运行。
        /// </summary>
        private void EnsureWorkerAlive()
        {
            if (process == null)
                throw new ObjectDisposedException(nameof(YoloIsolatedWorkerClient));
            if (process.HasExited)
                throw new InvalidOperationException("YOLO隔离worker已退出，退出码：" + process.ExitCode);
        }
    }
}
