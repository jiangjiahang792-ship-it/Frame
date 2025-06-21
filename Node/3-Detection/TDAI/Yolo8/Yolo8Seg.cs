using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Logger;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;
using OpenCvSharp;

namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    #region 使用示例

    //string modelPath = "best.onnx";
    //string[] classNames = { "person", "car", "bicycle" };
    //int classCount = classNames.Length;
    //int modelSize = 640;
    //float scoreThreshold = 0.5f;
    //float nmsThreshold = 0.4f;

    //// 1. 初始化模型
    //IntPtr context = YoloSeg.Initialize(modelPath, classNames, classCount, modelSize, scoreThreshold, nmsThreshold);

    //// 2. 图像数据准备（假设你已经有了 imageBytes、width、height、channels）
    //byte[] imageBytes = ...;
    //int width = 640;
    //int height = 480;
    //int channels = 3;

    //// 3. 推理
    //int boxCount;
    //IntPtr boxData, scoreData, classIdData;
    //int maskCount;
    //IntPtr maskWidth, maskHeight, maskData;

    //YoloSeg.Infer(context, imageBytes, width, height, channels,
    //          out boxCount, out boxData, out scoreData, out classIdData,
    //          out maskCount, out maskWidth, out maskHeight, out maskData);

    //// TODO: 解析 boxData, scoreData 等内存指针的数据

    //// 4. 释放资源
    //YoloSeg.Release(context);

    #endregion

    /// <summary>
    /// Yolo8Seg 类用于加载和调用 yolo_seg.dll 中的函数
    /// </summary>
    internal class Yolo8Seg
    {
        // 初始化结果结构体（可选）
        [StructLayout(LayoutKind.Sequential)]
        public struct SegmentationResult
        {
            public int class_id;
            public float score;
            public int left;
            public int top;
            public int width;
            public int height;
            // 可扩展 mask 数据指针等字段
        }

        // 定义委托类型，对应 yolo_seg.dll 中的函数
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr InitializeDelegate(string modelPath, string[] classNames, int classCount, int modelSize, float scoreThreshold, float nmsThreshold);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void InferDelegate(IntPtr context, byte[] imageData, int width, int height, int channels,
                                          out int boxCount, out IntPtr boxData, out IntPtr scoreData, out IntPtr classIdData,
                                          out int maskCount, out IntPtr maskWidth, out IntPtr maskHeight, out IntPtr maskData);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ReleaseDelegate(IntPtr context);

        // 运行时加载的 DLL 句柄
        private static IntPtr _hModule = IntPtr.Zero;

        // 委托实例
        private static InitializeDelegate _initialize;
        private static InferDelegate _infer;
        private static ReleaseDelegate _release;

        // 静态构造函数中加载 DLL 并绑定函数
        static Yolo8Seg()
        {
            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yolo_seg.dll");

            if (!File.Exists(dllPath))
                LogHelper.AddLog(MsgLevel.Exception, $"找不到 yolo_seg.dll，请确保它位于应用程序目录: {dllPath}", true);

            _hModule = LoadLibrary(dllPath);
            if (_hModule == IntPtr.Zero)
                LogHelper.AddLog(MsgLevel.Exception, "加载 yolo_seg.dll 失败!", true);

            try
            {
                _initialize = Marshal.GetDelegateForFunctionPointer<InitializeDelegate>(GetProcAddress(_hModule, "Initialize"));
                _infer = Marshal.GetDelegateForFunctionPointer<InferDelegate>(GetProcAddress(_hModule, "Infer"));
                _release = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(GetProcAddress(_hModule, "Release"));
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"绑定 yolo_seg.dll 函数失败: {ex.Message}", true);
            }
        }

        // 导出的函数接口

        /// <summary>
        /// 初始化模型上下文
        /// </summary>
        public static IntPtr Initialize(string modelPath, string[] classNames, int classCount, int modelSize, float scoreThreshold, float nmsThreshold)
        {
            return _initialize(modelPath, classNames, classCount, modelSize, scoreThreshold, nmsThreshold);
        }

        /// <summary>
        /// 执行推理
        /// </summary>
        /// <summary>
        /// 执行推理并返回检测结果数组
        /// </summary>
        public static int Infer(IntPtr handle, byte[] image_data, int width, int height, int channels, [Out] List<SegResult> detectResults)
        {
            int boxCount;
            IntPtr boxData, scoreData, classIdData;
            int maskCount;
            IntPtr maskWidth, maskHeight, maskData;

            _infer(handle, image_data, width, height, channels,
                   out boxCount, out boxData, out scoreData, out classIdData,
                   out maskCount, out maskWidth, out maskHeight, out maskData);

            if (boxData != IntPtr.Zero)
            {
                for (int i = 0; i < boxCount; i++)
                {
                    int left = Marshal.ReadInt32(boxData, i * 4 * sizeof(int));
                    int top = Marshal.ReadInt32(boxData, (i * 4 + 1) * sizeof(int));
                    int rect_width = Marshal.ReadInt32(boxData, (i * 4 + 2) * sizeof(int));
                    int rect_height = Marshal.ReadInt32(boxData, (i * 4 + 3) * sizeof(int));

                    float score = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(scoreData, i * sizeof(float))), 0);
                    int class_id = Marshal.ReadInt32(classIdData, i * sizeof(int));

                    detectResults.Add(new SegResult()
                    {
                        ClassId = class_id,
                        Score = score,
                        Box = new Rect(left, top, rect_width, rect_height)
                    });
                }
            }
            return boxCount;
        }

        /// <summary>
        /// 释放模型上下文资源
        /// </summary>
        public static void Release(IntPtr context)
        {
            _release(context);
        }

        // Win32 API 函数声明
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    }
}
