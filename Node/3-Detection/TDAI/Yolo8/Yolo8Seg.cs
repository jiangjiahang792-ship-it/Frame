using System;
using System.Collections.Generic;
using OpenCvSharp;


namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    /// <summary>
    /// Yolo8 Seg 类
    /// </summary>
    public class Yolo8Seg : IYolo8
    {
        /// <summary>
        /// 当前 SEG CPU模型会话。
        /// </summary>
        private YoloOpenVinoCpuSession _cpuSession;

        /// <summary>
        /// 当前 SEG GPU TensorRT模型会话。
        /// </summary>
        private YoloOpenVinoCpuSession _gpuSession;

        /// <summary>
        /// 模型类型
        /// </summary>
        public ModelType ModelType { get; } = ModelType.SEG;
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
        /// 初始化模型
        /// </summary>
        public void Init(string model_path, DeviceType deviceType,string[] class_names, int input_size, float score_threshold, float nms_threshold, int key_point_num = 0)
        {
            DeviceType = deviceType;
            ScoreThreshold = score_threshold;
            NMSThreshold = nms_threshold;

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
                throw new Exception("Failed to initialize YOLO segmentation model.");
            }
            if (DeviceType == DeviceType.GPU && _gpuSession == null)
            {
                throw new Exception("Failed to initialize YOLO segmentation model.");
            }
        }


        /// <summary>
        /// 执行推理并返回分割结果列表。
        /// </summary>
        /// <param name="image">输入图像。</param>
        /// <param name="deltaX">结果X方向偏移。</param>
        /// <param name="deltaY">结果Y方向偏移。</param>
        /// <param name="needMaskBox">是否需要从掩膜提取不规则框。</param>
        /// <returns>分割结果列表。</returns>
        public List<SegResult> Detect(Mat image, int deltaX = 0, int deltaY = 0, bool needMaskBox = true)
        {
            switch (DeviceType)
            {
                case DeviceType.CPU:
                    return _cpuSession.DetectSeg(image, ScoreThreshold, NMSThreshold, deltaX, deltaY, needMaskBox);

                case DeviceType.GPU:
                    return _gpuSession.DetectSeg(image, ScoreThreshold, NMSThreshold, deltaX, deltaY, needMaskBox);
                default:
                    throw new Exception("未找到对应运行设备的指定");
            }
        }

        /// <summary>
        /// 销毁模型资源
        /// </summary>
        public void Destroy()
        {
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

