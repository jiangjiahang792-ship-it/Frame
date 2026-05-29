using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OpenCvSharp;
using Logger;
using System.Security.Cryptography;
using System.IO;

namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    /// <summary>
    /// YoloOBB 类用于加载并调用 td_obb.dll 中的函数（支持旋转框检测）
    /// </summary>
    public class Yolo8Obb : IYolo8
    {
        // 导入DLL函数
        [DllImport("td_obb.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr YOLO_Initialize(
            string encrypted_model_path,
            string[] class_names,
            int num_classes,
            int input_size,
            float score_threshold,
            float nms_threshold);

        [DllImport("td_obb.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void YOLO_Inference(
            IntPtr handle,
            byte[] image_data,
            int width,
            int height,
            int channels,
            out IntPtr out_results,
            out int out_result_count);

        [DllImport("td_obb.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void YOLO_Release(IntPtr handle);

        private IntPtr handle;

        /// <summary>
        /// 模型类型
        /// </summary>
        public ModelType ModelType { get; } = ModelType.OBB;
        /// <summary>
        /// 初始化 YOLO OBB模型
        /// </summary>
        /// <param name="encrypted_model_path"></param>
        /// <param name="class_names"></param>
        /// <param name="input_size"></param>
        /// <param name="score_threshold"></param>
        /// <param name="nms_threshold"></param>
        /// <exception cref="Exception"></exception>
        public void Init(string model_path, string[] class_names, int input_size, float score_threshold, float nms_threshold, int key_point_num = 0)
        {
            handle = YOLO_Initialize(model_path, class_names, class_names.Length, input_size, score_threshold, nms_threshold);
            if (handle == IntPtr.Zero)
            {
                throw new Exception("Failed to initialize YOLO model.");
            }
        }
        /// <summary>
        /// 执行检测
        /// </summary>
        /// <param name="image"></param>
        /// <param name="deltaX"></param>
        /// <param name="deltaY"></param>
        /// <returns></returns>
        public List<ObbResult> Detect(Mat image, int deltaX = 0, int deltaY = 0)
        {
            byte[] imageData = new byte[image.Total() * image.ElemSize()];
            Marshal.Copy(image.Data, imageData, 0, imageData.Length);

            IntPtr resultsPtr;
            int resultCount;
            YOLO_Inference(handle, imageData, image.Width, image.Height, image.Channels(), out resultsPtr, out resultCount);

            List<ObbResult> results = new List<ObbResult>();
            if (resultCount > 0)
            {
                int size = Marshal.SizeOf(typeof(ObbResult));
                for (int i = 0; i < resultCount; i++)
                {
                    IntPtr currentPtr = new IntPtr(resultsPtr.ToInt64() + i * size);
                    results.Add((ObbResult)Marshal.PtrToStructure(currentPtr, typeof(ObbResult)));
                }
                Marshal.FreeHGlobal(resultsPtr);
            }
            OffsetAllCenters(results, deltaX, deltaY);
            return results;
        }
        public static void OffsetAllCenters(List<ObbResult> obbResults, int deltaX, int deltaY)
        {
            if (obbResults == null) return;

            for (int i = 0; i < obbResults.Count; i++)
            {
                var item = obbResults[i]; // 获取副本
                item.center_x += deltaX;
                item.center_y += deltaY;
                obbResults[i] = item; // 重新赋值回列表（关键！）
            }
        }
        /// <summary>
        /// 销毁检测器句柄
        /// </summary>
        /// <param name="handle"></param>
        public void Destroy()
        {
            if (handle != IntPtr.Zero)
            {
                YOLO_Release(handle);
                handle = IntPtr.Zero;
            }
        }
    }
}
