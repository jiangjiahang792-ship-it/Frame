using Newtonsoft.Json;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
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

            DestroyCurrentModel();
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

            using (Mat image = CreateMat(request))
            {
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
        /// 从请求消息还原OpenCV图像。
        /// </summary>
        /// <param name="request">推理请求。</param>
        /// <returns>OpenCV Mat图像。</returns>
        private static Mat CreateMat(YoloIsolatedRequest request)
        {
            byte[] bytes = Convert.FromBase64String(request.ImageBase64 ?? string.Empty);
            Mat image = new Mat(request.Rows, request.Cols, request.MatType);
            int expectedBytes = (int)(image.Total() * image.ElemSize());
            if (bytes.Length != expectedBytes)
            {
                image.Dispose();
                throw new InvalidOperationException($"YOLO worker图像字节数不匹配，期望={expectedBytes}，实际={bytes.Length}。");
            }

            Marshal.Copy(bytes, 0, image.Data, bytes.Length);
            return image;
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
