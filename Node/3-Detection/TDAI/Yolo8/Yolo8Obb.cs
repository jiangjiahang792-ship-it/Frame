using OpenCvSharp;
using System;
using System.Collections.Generic;

namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    /// <summary>
    /// Yolo8Obb类用于加载并执行旋转框检测模型。
    /// </summary>
    public class Yolo8Obb : IYolo8
    {
        /// <summary>
        /// 当前OBB CPU模型会话。
        /// </summary>
        private YoloOpenVinoCpuSession _cpuSession;

        /// <summary>
        /// 当前OBB GPU TensorRT模型会话。
        /// </summary>
        private YoloOpenVinoCpuSession _gpuSession;

        /// <summary>
        /// 当前OBB隔离worker模型会话。
        /// </summary>
        private YoloIsolatedWorkerClient _isolatedSession;

        /// <summary>
        /// 模型类型。
        /// </summary>
        public ModelType ModelType { get; } = ModelType.OBB;

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
        /// 初始化YOLO OBB模型。
        /// </summary>
        /// <param name="model_path">模型路径。</param>
        /// <param name="deviceType">推理设备类型。</param>
        /// <param name="class_names">类别名称。</param>
        /// <param name="input_size">模型输入尺寸。</param>
        /// <param name="score_threshold">置信度阈值。</param>
        /// <param name="nms_threshold">NMS阈值。</param>
        /// <param name="key_point_num">关键点数量，OBB模型不使用。</param>
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
                throw new Exception("初始化OBB CPU模型发生错误,请检查模型路径是否正确或者dll是否有异常!!!");

            if (DeviceType == DeviceType.GPU && _gpuSession == null)
                throw new Exception("初始化OBB GPU模型发生错误,请检查模型路径是否正确或者dll是否有异常!!!");
        }

        /// <summary>
        /// 执行旋转框检测。
        /// </summary>
        /// <param name="image">待检测图像。</param>
        /// <param name="deltaX">结果X方向偏移。</param>
        /// <param name="deltaY">结果Y方向偏移。</param>
        /// <returns>旋转框结果集合。</returns>
        public List<ObbResult> Detect(Mat image, int deltaX = 0, int deltaY = 0)
        {
            if (_isolatedSession != null)
                return _isolatedSession.DetectObb(image, ScoreThreshold, NMSThreshold, deltaX, deltaY);

            switch (DeviceType)
            {
                case DeviceType.CPU:
                    return _cpuSession.DetectObb(image, ScoreThreshold, NMSThreshold, deltaX, deltaY);
                case DeviceType.GPU:
                    return _gpuSession.DetectObb(image, ScoreThreshold, NMSThreshold, deltaX, deltaY);
                default:
                    throw new Exception("未找到对应运行设备的指定");
            }
        }

        /// <summary>
        /// 对旋转框中心点统一应用偏移量。
        /// </summary>
        /// <param name="obbResults">旋转框集合。</param>
        /// <param name="deltaX">X方向偏移。</param>
        /// <param name="deltaY">Y方向偏移。</param>
        public static void OffsetAllCenters(List<ObbResult> obbResults, int deltaX, int deltaY)
        {
            if (obbResults == null)
                return;

            for (int i = 0; i < obbResults.Count; i++)
            {
                ObbResult item = obbResults[i];
                item.center_x += deltaX;
                item.center_y += deltaY;
                obbResults[i] = item;
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
