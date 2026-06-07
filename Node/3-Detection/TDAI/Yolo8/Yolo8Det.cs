using OpenCvSharp;
using Sunny.UI.Win32;
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

        // 检测结果结构体（对应 DLL 中的 DetectionResult）
        [StructLayout(LayoutKind.Sequential)]
        public struct DetectionResult
        {
            public int class_id;
            public float score;
            public int left;
            public int top;
            public int width;
            public int height;
        }

        // 导入DLL函数
        [DllImport("td_det.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CreateDetector(
            string model_path,
            string[] class_names,
            int class_count,
            int input_size,
            float score_threshold,
            float nms_threshold);

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


        [DllImport("td_det.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DestroyDetector(IntPtr detector);

        [DllImport("td_det.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Detect(
            IntPtr detector,
            byte[] img_data,
            int width,
            int height,
            int channels,
            [Out] DetectionResult[] results);

        [DllImport("td_gpu_detect.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void YoloV8_FreeResult(IntPtr results);

        [DllImport("td_gpu_detect.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void YoloV8_Release(IntPtr handle);

        /// <summary>
        /// DET GPU 原生推理全局锁，避免多个流程同时进入 GPU 推理导致偶发识别数量为 0。
        /// </summary>
        private static readonly object GpuDetInferenceLock = new object();

        /// <summary>
        /// 当前 DET 模型的原生句柄。
        /// </summary>
        private IntPtr _handle;

        /// <summary>
        /// 模型类型：普通目标检测（DET）
        /// </summary>
        public ModelType ModelType { get; } = ModelType.DET;
        public DeviceType DeviceType { get; set; }
        public float ScoreThreshold { get; set; }
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
                    _handle = CreateDetector(model_path, class_names, class_names.Length, input_size, score_threshold, nms_threshold);
                    break;
                case DeviceType.GPU:
                    _handle = YoloV8_Init("det", model_path, input_size, input_size, class_names, class_names.Length, score_threshold, nms_threshold, 0);
                    break;
                default:
                    throw new Exception("未找到对应运行设备的指定");
            }
            
            if (_handle == IntPtr.Zero)
            {
                throw new Exception("Failed to initialize YOLO detection model.");
            }
        }


        /// <summary>
        /// 执行检测
        /// </summary>
        public List<DetResult> Detect(Mat image)
        {
            return DetectCore(image);
        }

        /// <summary>
        /// 执行 DET 检测核心逻辑，只有 GPU 推理分支会进入全局锁。
        /// </summary>
        /// <param name="image">待检测图像。</param>
        /// <returns>检测结果集合。</returns>
        private List<DetResult> DetectCore(Mat image)
        {
            //初始化返回对象
            List<DetResult> results = new List<DetResult>();

            switch (DeviceType)
            {
                case DeviceType.CPU:
                    byte[] imageData = new byte[image.Total() * image.ElemSize()];
                    Marshal.Copy(image.Data, imageData, 0, imageData.Length);

                    DetectionResult[] nativeResults = new DetectionResult[1024];

                    int count = 0;
                    count = Detect(_handle, imageData, image.Width, image.Height, image.Channels(), nativeResults);

                   
                    for (int i = 0; i < count; i++)
                    {
                        var dr = nativeResults[i];
                        results.Add(new DetResult
                        {
                            ClassId = dr.class_id,
                            Score = dr.score,
                            Box = new Rect(dr.left, dr.top, dr.width, dr.height)
                        });
                    }
                    break;
                case DeviceType.GPU:
                    lock (GpuDetInferenceLock)
                    {
                        // 空值校验
                        if (image == null || image.Empty() || image.Data == IntPtr.Zero || _handle == IntPtr.Zero)
                        {
                            return new List<DetResult>();
                        }

                        // 调用底层检测接口
                        IntPtr resPtr = YoloV8_Infer(_handle, image.Data, image.Width, image.Height, image.Channels(), ScoreThreshold, NMSThreshold);
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
            

            return results;
        }

        /// <summary>
        /// 销毁检测器句柄
        /// </summary>
        public void Destroy()
        {
            if (_handle != IntPtr.Zero)
            {
                switch (DeviceType)
                {
                    case DeviceType.CPU:
                        DestroyDetector(_handle);
                        _handle = IntPtr.Zero;
                        break;
                    case DeviceType.GPU:
                        lock (GpuDetInferenceLock)
                        {
                            if (_handle == IntPtr.Zero)
                                break;
                            YoloV8_Release(_handle);
                            _handle = IntPtr.Zero;
                        }
                        break;
                    default:
                        throw new Exception("未找到对应运行设备的指定");
                }
            }
        }
    }
}

