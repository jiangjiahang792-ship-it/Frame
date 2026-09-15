using OpenCvSharp;
using System;
using System.Collections.Generic;

namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    /// <summary>
    /// Yolo8Det 类用于加载并调用 td_det.dll 中的函数（普通目标检测）
    /// </summary>
    public class Yolo8Det : IYolo8
    {
        /// <summary>
        /// 当前 DET CPU模型会话。
        /// </summary>
        private YoloOpenVinoCpuSession _cpuSession;

        /// <summary>
        /// 当前 DET GPU TensorRT模型会话。
        /// </summary>
        private YoloOpenVinoCpuSession _gpuSession;

        /// <summary>
        /// 当前 DET 隔离worker模型会话。
        /// </summary>
        private YoloIsolatedWorkerClient _isolatedSession;

        /// <summary>
        /// 模型类型：普通目标检测（DET）
        /// </summary>
        public ModelType ModelType { get; } = ModelType.DET;
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
        /// 构造函数：初始化检测模型
        /// </summary>
        public void Init(string model_path, DeviceType deviceType, string[] class_names, int input_size, float score_threshold, float nms_threshold, int key_point_num = 0)
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
            {
                throw new Exception("Init初始化CPU模型发生错误,请检查模型路径是否正确或者dll是否有异常!!!");
            }
            if (DeviceType == DeviceType.GPU && _gpuSession == null)
            {
                throw new Exception("Init初始化GPU模型发生错误,请检查模型路径是否正确或者dll是否有异常!!!");
            }
        }


        /// <summary>
        /// 执行检测
        /// </summary>
        public List<DetResult> Detect(Mat image, int deltaX = 0, int deltaY = 0)
        {
            return DetectCore(image, deltaX, deltaY);
        }

        /// <summary>
        /// 执行 DET 检测核心逻辑。
        /// </summary>
        /// <param name="image">待检测图像。</param>
        /// <param name="deltaX">检测结果X方向偏移。</param>
        /// <param name="deltaY">检测结果Y方向偏移。</param>
        /// <returns>检测结果集合。</returns>
        private List<DetResult> DetectCore(Mat image, int deltaX, int deltaY)
        {
            if (_isolatedSession != null)
                return _isolatedSession.DetectDet(image, ScoreThreshold, NMSThreshold, deltaX, deltaY);

            switch (DeviceType)
            {
                case DeviceType.CPU:
                    return _cpuSession.DetectDet(image, ScoreThreshold, NMSThreshold, deltaX, deltaY);
                case DeviceType.GPU:
                    return _gpuSession.DetectDet(image, ScoreThreshold, NMSThreshold, deltaX, deltaY);
                default:
                    throw new Exception("未找到对应运行设备的指定");
            }
        }

        /// <summary>
        /// 销毁检测器句柄
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

