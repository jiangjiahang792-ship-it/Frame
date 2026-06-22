using Logger;
using OpenCvSharp;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Device._3D;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._1_Acquisition.ImageSource3D
{
    /// <summary>
    /// 3D 图像源节点，负责从方案中的 3D 相机主动获取一帧深度和点云数据。
    /// </summary>
    public class NodeImageSource3D : NodeBase
    {
        /// <summary>
        /// 创建 3D 图像源节点。
        /// </summary>
        /// <param name="nodeId">节点编号。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="process">所属流程。</param>
        /// <param name="nodeType">节点类型。</param>
        public NodeImageSource3D(int nodeId, string nodeName, Process process, NodeType nodeType)
            : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new ParamFormImageSource3D();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultImageSource3D();
        }

        /// <summary>
        /// 运行 3D 图像源节点。
        /// </summary>
        /// <param name="token">流程取消令牌。</param>
        /// <param name="showLog">是否输出日志。</param>
        /// <returns>节点运行控制结果。</returns>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;
            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return new NodeReturn(NodeRunFlag.StopRun);
            }

            if (!(ParamForm.Params is NodeParamImageSource3D param))
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行参数未设置或保存！");
            }

            try
            {
                SetStatus(NodeStatus.Unexecuted, "*");
                CheckTokenCancel(token);

                I3DCamera camera = ResolveCamera(param);
                ApplyCameraConfig(camera, param);
                EnsureCameraReady(camera, param);

                Camera3DFrameData frameData = await Task.Run(() => camera.GetOneFrameData(), token);
                CheckTokenCancel(token);

                NodeResultImageSource3D result = Result as NodeResultImageSource3D;
                if (result == null)
                {
                    result = new NodeResultImageSource3D();
                    Result = result;
                }

                result.FrameData = frameData ?? new Camera3DFrameData();
                result.DepthImage = BuildDepthPreviewImage(result.FrameData);
                result.FrameNumber = result.FrameData.FrameNumber;
                result.TotalPointCount = result.FrameData.TotalPointCount;

                int time = SetRunResult(startTime, NodeStatus.Successful);
                result.RunTime = time;
                Result = result;

                if (showLog)
                {
                    LogHelper.AddLog(
                        MsgLevel.Info,
                        $"节点({ID}.{NodeName})运行成功！3D帧={result.FrameNumber}，尺寸={result.FrameData.Width}x{result.FrameData.Height}，点数={result.TotalPointCount}，耗时={time} ms",
                        true);
                }

                return HasValidFrame(result.FrameData) ? new NodeReturn(NodeRunFlag.ContinueRun) : new NodeReturn(NodeRunFlag.StopRun);
            }
            catch (OperationCanceledException)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                SetRunResult(startTime, NodeStatus.Unexecuted);
                throw;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因：{ex.Message}", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
            }
        }

        /// <summary>
        /// 根据参数解析当前运行使用的 3D 相机。
        /// </summary>
        /// <param name="param">3D 图像源参数。</param>
        /// <returns>3D 相机对象。</returns>
        private static I3DCamera ResolveCamera(NodeParamImageSource3D param)
        {
            if (param == null)
                throw new ArgumentNullException(nameof(param));

            if (param.Camera != null)
                return param.Camera;

            foreach (I3DCamera camera in Solution.Instance.Camera3DDevices)
            {
                if (camera.UserDefinedName == param.CameraName)
                {
                    param.Camera = camera;
                    return camera;
                }
            }

            throw new Exception($"未找到3D相机“{param.CameraName}”！");
        }

        /// <summary>
        /// 将节点参数应用到 3D 相机配置。
        /// </summary>
        /// <param name="camera">3D 相机对象。</param>
        /// <param name="param">3D 图像源参数。</param>
        private static void ApplyCameraConfig(I3DCamera camera, NodeParamImageSource3D param)
        {
            if (camera == null)
                throw new ArgumentNullException(nameof(camera));

            if (param == null)
                throw new ArgumentNullException(nameof(param));

            if (camera.ConnectionConfig == null)
                camera.ConnectionConfig = new Camera3DConnectionConfig();

            if (camera.GrabConfig == null)
                camera.GrabConfig = new Camera3DGrabConfig();

            uint timeout = param.TimeOut == 0 ? 2000 : param.TimeOut;
            camera.GetImageTimeOut = timeout;
            camera.ConnectionConfig.GetImageTimeoutMs = timeout;
            camera.GrabConfig.ImageMode = param.ImageMode;
            camera.GrabConfig.EnableNativePointCloudCache = param.EnableNativePointCloudCache;
            camera.GrabConfig.BuildManagedPointCloudFallback = param.BuildManagedPointCloudFallback;
            camera.GrabConfig.MaxDisplayPointCount = Math.Max(1000, param.MaxDisplayPointCount);
        }

        /// <summary>
        /// 确保相机处于可以取帧的状态。
        /// </summary>
        /// <param name="camera">3D 相机对象。</param>
        /// <param name="param">3D 图像源参数。</param>
        private static void EnsureCameraReady(I3DCamera camera, NodeParamImageSource3D param)
        {
            if (!camera.IsOpen)
            {
                if (!param.AutoOpenCamera)
                    throw new Exception("3D相机尚未连接！");

                camera.Open();
            }

            if (param.AutoStartGrabbing && !camera.IsGrabbing)
                camera.StartGrabbing();
        }

        /// <summary>
        /// 判断 3D 帧是否包含可继续流转的数据。
        /// </summary>
        /// <param name="frameData">3D 帧数据。</param>
        /// <returns>包含深度图或点云时返回 true。</returns>
        private static bool HasValidFrame(Camera3DFrameData frameData)
        {
            if (frameData == null)
                return false;

            bool hasDepth = frameData.DepthValues != null && frameData.DepthValues.Length > 0;
            bool hasPointCloud = frameData.TotalPointCount > 0 ||
                (frameData.PointCloudPoints != null && frameData.PointCloudPoints.Length > 0) ||
                (frameData.NativePointCloudFrame != null && frameData.NativePointCloudFrame.DataLength > 0);

            return hasDepth || hasPointCloud;
        }

        /// <summary>
        /// 根据深度值构建伪彩色预览图，并保留 8 位灰度缓存供测量和 2D 算子复用。
        /// </summary>
        /// <param name="frameData">3D 帧数据。</param>
        /// <returns>深度预览输出图像。</returns>
        public static OutputImage BuildDepthPreviewImage(Camera3DFrameData frameData)
        {
            if (frameData == null ||
                frameData.Width <= 0 ||
                frameData.Height <= 0 ||
                frameData.DepthValues == null ||
                frameData.DepthValues.Length == 0)
            {
                return new OutputImage();
            }

            int totalPixelCount = frameData.Width * frameData.Height;
            int availablePixelCount = Math.Min(totalPixelCount, frameData.DepthValues.Length);
            if (availablePixelCount <= 0)
                return new OutputImage();

            int minValue;
            int maxValue;
            if (!TryGetDepthRange(frameData.DepthValues, availablePixelCount, frameData.DepthInvalidValue, out minValue, out maxValue))
                return new OutputImage();

            byte[] pixels = BuildDepthPreviewPixels(
                frameData.DepthValues,
                totalPixelCount,
                availablePixelCount,
                frameData.DepthInvalidValue,
                minValue,
                maxValue,
                out byte[] invalidMaskPixels,
                out bool hasInvalidPixel);

            Mat grayPreview = new Mat(frameData.Height, frameData.Width, MatType.CV_8UC1);
            Marshal.Copy(pixels, 0, grayPreview.Data, pixels.Length);

            Mat colorPreview = new Mat();
            Cv2.ApplyColorMap(grayPreview, colorPreview, ColormapTypes.Jet);

            if (hasInvalidPixel)
            {
                using (Mat invalidMask = new Mat(frameData.Height, frameData.Width, MatType.CV_8UC1))
                {
                    Marshal.Copy(invalidMaskPixels, 0, invalidMask.Data, invalidMaskPixels.Length);
                    colorPreview.SetTo(new Scalar(0, 0, 0), invalidMask);
                }
            }

            return OutputImage.FromSingleImage(colorPreview, grayPreview);
        }

        /// <summary>
        /// 计算深度值有效范围。
        /// </summary>
        /// <param name="depthValues">深度值数组。</param>
        /// <param name="pixelCount">参与计算的像素数量。</param>
        /// <param name="invalidValue">无效深度值。</param>
        /// <param name="minValue">输出最小深度。</param>
        /// <param name="maxValue">输出最大深度。</param>
        /// <returns>存在有效深度时返回 true。</returns>
        private static bool TryGetDepthRange(int[] depthValues, int pixelCount, int invalidValue, out int minValue, out int maxValue)
        {
            minValue = int.MaxValue;
            maxValue = int.MinValue;
            bool hasValidValue = false;

            for (int i = 0; i < pixelCount; i++)
            {
                int value = depthValues[i];
                if (value == invalidValue)
                    continue;

                if (value < minValue)
                    minValue = value;

                if (value > maxValue)
                    maxValue = value;

                hasValidValue = true;
            }

            return hasValidValue;
        }

        /// <summary>
        /// 将深度值按有效范围归一化为 8 位灰度像素，并同步生成无效深度遮罩。
        /// </summary>
        /// <param name="depthValues">深度值数组。</param>
        /// <param name="totalPixelCount">目标图像总像素数量。</param>
        /// <param name="availablePixelCount">深度数组中可用的像素数量。</param>
        /// <param name="invalidValue">无效深度值。</param>
        /// <param name="minValue">最小深度。</param>
        /// <param name="maxValue">最大深度。</param>
        /// <param name="invalidMaskPixels">无效深度遮罩像素，255 表示无效。</param>
        /// <param name="hasInvalidPixel">是否存在无效深度像素。</param>
        /// <returns>8 位灰度像素。</returns>
        private static byte[] BuildDepthPreviewPixels(
            int[] depthValues,
            int totalPixelCount,
            int availablePixelCount,
            int invalidValue,
            int minValue,
            int maxValue,
            out byte[] invalidMaskPixels,
            out bool hasInvalidPixel)
        {
            byte[] pixels = new byte[totalPixelCount];
            invalidMaskPixels = new byte[totalPixelCount];
            hasInvalidPixel = false;
            int range = maxValue - minValue;

            for (int i = 0; i < availablePixelCount; i++)
            {
                int value = depthValues[i];
                if (value == invalidValue)
                {
                    pixels[i] = 0;
                    invalidMaskPixels[i] = 255;
                    hasInvalidPixel = true;
                    continue;
                }

                pixels[i] = range <= 0 ? (byte)0 : (byte)Math.Max(0, Math.Min(255, (value - minValue) * 255 / range));
            }

            for (int i = availablePixelCount; i < totalPixelCount; i++)
            {
                invalidMaskPixels[i] = 255;
                hasInvalidPixel = true;
            }

            return pixels;
        }
    }
}
