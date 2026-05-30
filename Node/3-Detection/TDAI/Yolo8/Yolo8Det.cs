using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OpenCvSharp;
using Sunny.UI.Win32;

namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    /// <summary>
    /// Yolo8Det 类用于加载并调用 td_det.dll 中的函数（普通目标检测）
    /// </summary>
    public class Yolo8Det : IYolo8
    {
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


        //GPU初始化函数
        [DllImport("td_gpu_detect.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr YoloV8_Init(string model_type,
                                              string model_path,
                                              int intput_size_width, int intput_size_height,
                                              [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] class_names, int class_count,
                                              float score_threshold, float nms_threshold,
                                              int gpu_id);

        [DllImport("td_gpu_detect.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void YoloV8_Release(IntPtr handle);


        [DllImport("td_gpu_detect.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr YoloV8_Infer(IntPtr handle, IntPtr img_data, int width, int height, int channels,
                                                 float score_threshold, float nms_threshold);

        [DllImport("td_gpu_detect.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void YoloV8_FreeResult(IntPtr results);

        private IntPtr _handle;

        /// <summary>
        /// 模型类型：普通目标检测（DET）
        /// </summary>
        public ModelType ModelType { get; } = ModelType.DET;

        /// <summary>
        /// 构造函数：初始化检测模型
        /// </summary>
        public void Init(string model_path, string[] class_names, int input_size, float score_threshold, float nms_threshold, int key_point_num = 0,DeviceType deviceType=DeviceType.CPU)
        {
            switch (deviceType)
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
        public List<DetResult> Detect(Mat image, int deltaX = 0, int deltaY = 0)
        {
            byte[] imageData = new byte[image.Total() * image.ElemSize()];
            Marshal.Copy(image.Data, imageData, 0, imageData.Length);

            DetectionResult[] nativeResults = new DetectionResult[1024];

            int count = 0;
            count = Detect(_handle, imageData, image.Width, image.Height, image.Channels(), nativeResults);

            List<DetResult> results = new List<DetResult>();
            for (int i = 0; i < count; i++)
            {
                var dr = nativeResults[i];
                results.Add(new DetResult
                {
                    ClassId = dr.class_id,
                    Score = dr.score,
                    Box = new Rect(dr.left + deltaX, dr.top + deltaY, dr.width, dr.height)
                });
            }

            return results;
        }

        /// <summary>
        /// 执行检测(刺破机颜色线序检测专用，带屏蔽区)
        /// </summary>
        public List<DetResult> Detect(Mat image, List<Rect> invalidRects)
        {
            byte[] imageData = new byte[image.Total() * image.ElemSize()];
            Marshal.Copy(image.Data, imageData, 0, imageData.Length);

            DetectionResult[] nativeResults = new DetectionResult[1024];

            int count = 0;
            count = Detect(_handle, imageData, image.Width, image.Height, image.Channels(), nativeResults);

            List<DetResult> results = new List<DetResult>();
            for (int i = 0; i < count; i++)
            {
                var dr = nativeResults[i];
                // 计算检测框的中点
                int centerX = dr.left + dr.width / 2;
                int centerY = dr.top + dr.height / 2;
                // 判断结果框是否处于其中一个无效区域内，若是，跳过
                if (invalidRects.Exists(r => r.Contains(centerX, centerY)))
                    continue;
                results.Add(new DetResult
                {
                    ClassId = dr.class_id,
                    Score = dr.score,
                    Box = new Rect(dr.left, dr.top, dr.width, dr.height)
                });
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
                DestroyDetector(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }
}

