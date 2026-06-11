using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    /// <summary>
    /// Yolo8Det 类用于加载并调用 td_det.dll 中的函数（普通目标检测）
    /// </summary>
    public class Yolo8Det : IYolo8
    {
        // C-API structs mapped to C#
        [StructLayout(LayoutKind.Explicit, Size = 248)]
        public struct YoloResult
        {
            [FieldOffset(0)] public int class_id;
            [FieldOffset(4)] public float conf;
            [FieldOffset(8)] public float bbox_0; // cx or l
            [FieldOffset(12)] public float bbox_1; // cy or t
            [FieldOffset(16)] public float bbox_2; // w or r
            [FieldOffset(20)] public float bbox_3; // h or b
            [FieldOffset(24)] public float angle;

            // Keypoints follow (51 * 4 = 204 bytes) at offset 28
            [FieldOffset(28)] public float kpts_0; // First keypoint x

            [FieldOffset(232)] public IntPtr mask;
            [FieldOffset(240)] public int mask_width;
            [FieldOffset(244)] public int mask_height;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct YoloResultArray
        {
            public IntPtr results; // YoloResult*
            public int count;
        }

        //GPU初始化函数
        [DllImport("td_gpu_detect.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr YoloV8_Init(string model_type,
                                              string model_path,
                                              int intput_size_width, int intput_size_height,
                                              [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] class_names, int class_count,
                                              float score_threshold, float nms_threshold,
                                              int gpu_id);

        [DllImport("td_gpu_detect.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr YoloV8_Infer(IntPtr handle, IntPtr img_data, int width, int height, int channels,
                                                 float score_threshold, float nms_threshold);


        [DllImport("td_gpu_detect.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void YoloV8_FreeResult(IntPtr results);

        [DllImport("td_gpu_detect.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void YoloV8_Release(IntPtr handle);

        /// <summary>
        /// DET GPU 原生推理全局锁，避免多个流程同时进入 GPU 推理导致偶发识别数量为 0。
        /// </summary>
        private static readonly object GpuDetInferenceLock = new object();

        /// <summary>
        /// 当前 DET CPU模型会话。
        /// </summary>
        private YoloOpenVinoCpuSession _cpuSession;

        /// <summary>
        /// 当前 DET GPU模型的原生句柄。
        /// </summary>
        private IntPtr _gpuHandle;

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

            switch (DeviceType)
            {
                case DeviceType.CPU:
                    _cpuSession = YoloOpenVinoCpuSession.Open(model_path);
                    break;
                case DeviceType.GPU:
                    _gpuHandle = YoloV8_Init("det", model_path, input_size, input_size, class_names, class_names.Length, score_threshold, nms_threshold, 0);
                    break;
                default:
                    throw new Exception("未找到对应运行设备的指定");
            }
            
            if (DeviceType == DeviceType.CPU && _cpuSession == null)
            {
                throw new Exception("Init初始化CPU模型发生错误,请检查模型路径是否正确或者dll是否有异常!!!");
            }
            if (DeviceType == DeviceType.GPU && _gpuHandle == IntPtr.Zero)
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
        /// 执行 DET 检测核心逻辑，只有 GPU 推理分支会进入全局锁。
        /// </summary>
        /// <param name="image">待检测图像。</param>
        /// <param name="deltaX">检测结果X方向偏移。</param>
        /// <param name="deltaY">检测结果Y方向偏移。</param>
        /// <returns>检测结果集合。</returns>
        private List<DetResult> DetectCore(Mat image, int deltaX, int deltaY)
        {
            //初始化返回对象
            List<DetResult> results = new List<DetResult>();

            switch (DeviceType)
            {
                case DeviceType.CPU:
                    return _cpuSession.DetectDet(image, ScoreThreshold, NMSThreshold, deltaX, deltaY);
                case DeviceType.GPU:
                    lock (GpuDetInferenceLock)
                    {
                        // 空值校验
                        if (image == null || image.Empty() || image.Data == IntPtr.Zero || _gpuHandle == IntPtr.Zero)
                        {
                            return new List<DetResult>();
                        }

                        // 调用底层检测接口
                        IntPtr resPtr = YoloV8_Infer(_gpuHandle, image.Data, image.Width, image.Height, image.Channels(), ScoreThreshold, NMSThreshold);
                        if (resPtr == IntPtr.Zero)
                            return new List<DetResult>();

                        try
                        {
                            // 解析结果结构体
                            YoloResultArray resArray = Marshal.PtrToStructure<YoloResultArray>(resPtr);
                            if (resArray.count <= 0)
                                return new List<DetResult>();

                            int structSize = Marshal.SizeOf(typeof(YoloResult));

                            // 遍历所有检测结果
                            for (int i = 0; i < resArray.count; i++)
                            {
                                // 计算当前结果指针
                                IntPtr currentObjPtr = new IntPtr(resArray.results.ToInt64() + i * structSize);
                                YoloResult res = Marshal.PtrToStructure<YoloResult>(currentObjPtr);

                                // 封装为DetResult，应用偏移量
                                DetResult detResult = new DetResult
                                {
                                    ClassId = res.class_id,
                                    Score = res.conf,
                                    Box = new Rect(
                                        (int)res.bbox_0,
                                        (int)res.bbox_1,
                                        (int)res.bbox_2,
                                        (int)res.bbox_3
                                    )
                                };

                                // 添加到集合
                                results.Add(detResult);
                            }

                            // 返回最终集合
                            return results;
                        }
                        finally
                        {
                            YoloV8_FreeResult(resPtr);
                        }
                    }
                default:
                    throw new Exception("未找到对应运行设备的指定");
            }
        }

        /// <summary>
        /// 销毁检测器句柄
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
                    lock (GpuDetInferenceLock)
                    {
                        if (_gpuHandle != IntPtr.Zero)
                        {
                            YoloV8_Release(_gpuHandle);
                            _gpuHandle = IntPtr.Zero;
                        }
                    }
                    break;
                default:
                    throw new Exception("未找到对应运行设备的指定");
            }
        }
    }
}

