using OpenCvSharp;
using System;

namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    /// <summary>
    /// Yolo8Pose类用于加载并执行姿态检测模型。
    /// </summary>
    public class Yolo8Pose : IYolo8
    {
        /// <summary>
        /// 当前POSE CPU模型会话。
        /// </summary>
        private YoloOpenVinoCpuSession _cpuSession;

        /// <summary>
        /// 当前POSE GPU TensorRT模型会话。
        /// </summary>
        private YoloOpenVinoCpuSession _gpuSession;

        /// <summary>
        /// 当前POSE隔离worker模型会话。
        /// </summary>
        private YoloIsolatedWorkerClient _isolatedSession;

        /// <summary>
        /// 模型类型。
        /// </summary>
        public ModelType ModelType { get; } = ModelType.POSE;

        /// <summary>
        /// 推理设备类型。
        /// </summary>
        public DeviceType DeviceType { get; set; }

        /// <summary>
        /// 置信度阈值。
        /// </summary>
        public float ScoreThreshold { get; set; }

        /// <summary>
        /// NMS阈值。
        /// </summary>
        public float NMSThreshold { get; set; }

        /// <summary>
        /// 初始化YOLO姿态模型。
        /// </summary>
        /// <param name="model_path">模型路径。</param>
        /// <param name="deviceType">推理设备类型。</param>
        /// <param name="class_names">类别名称。</param>
        /// <param name="input_size">模型输入尺寸。</param>
        /// <param name="score_threshold">置信度阈值。</param>
        /// <param name="nms_threshold">NMS阈值。</param>
        /// <param name="key_point_num">关键点数量。</param>
        public void Init(string model_path, DeviceType deviceType, string[] class_names, int input_size, float score_threshold, float nms_threshold, int key_point_num)
        {
            DeviceType = deviceType;
            ScoreThreshold = score_threshold;
            NMSThreshold = nms_threshold;

            if (!YoloIsolatedRuntimeContext.IsWorkerProcess)
            {
                _isolatedSession = YoloIsolatedWorkerClient.Open(
                    model_path,
                    ModelType,
                    deviceType,
                    class_names,
                    input_size,
                    score_threshold,
                    nms_threshold,
                    key_point_num);
                return;
            }

            switch (DeviceType)
            {
                case DeviceType.CPU:
                    _cpuSession = YoloOpenVinoCpuSession.Open(model_path);
                    break;
                case DeviceType.GPU:
                    _gpuSession = YoloOpenVinoCpuSession.OpenGpu(model_path);
                    break;
                default:
                    throw new Exception("未找到对应运行设备的指定");
            }

            if (DeviceType == DeviceType.CPU && _cpuSession == null)
                throw new Exception("初始化POSE CPU模型发生错误,请检查模型路径是否正确或者dll是否有异常!!!");

            if (DeviceType == DeviceType.GPU && _gpuSession == null)
                throw new Exception("初始化POSE GPU模型发生错误,请检查模型路径是否正确或者dll是否有异常!!!");
        }

        /// <summary>
        /// 执行姿态检测。
        /// </summary>
        /// <param name="image">待检测图像。</param>
        /// <param name="deltaX">结果X方向偏移。</param>
        /// <param name="deltaY">结果Y方向偏移。</param>
        /// <returns>姿态检测结果。</returns>
        public PoseResult Detect(Mat image, int deltaX = 0, int deltaY = 0)
        {
            if (_isolatedSession != null)
                return _isolatedSession.DetectPose(image, ScoreThreshold, NMSThreshold, deltaX, deltaY);

            switch (DeviceType)
            {
                case DeviceType.CPU:
                    return _cpuSession.DetectPose(image, ScoreThreshold, NMSThreshold, deltaX, deltaY);
                case DeviceType.GPU:
                    return _gpuSession.DetectPose(image, ScoreThreshold, NMSThreshold, deltaX, deltaY);
                default:
                    throw new Exception("未找到对应运行设备的指定");
            }
        }

        /// <summary>
        /// 销毁检测器句柄。
        /// </summary>
        public void Destroy()
        {
            if (_isolatedSession != null)
            {
                _isolatedSession.Dispose();
                _isolatedSession = null;
                return;
            }

            switch (DeviceType)
            {
                case DeviceType.CPU:
                    if (_cpuSession != null)
                    {
                        _cpuSession.Dispose();
                        _cpuSession = null;
                    }
                    break;
                case DeviceType.GPU:
                    if (_gpuSession != null)
                    {
                        _gpuSession.Dispose();
                        _gpuSession = null;
                    }
                    break;
                default:
                    throw new Exception("未找到对应运行设备的指定");
            }
        }
    }
}
