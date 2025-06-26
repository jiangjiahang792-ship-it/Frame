using System;
using System.Runtime.InteropServices;
using System.IO;
using Logger;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    /// <summary>
    /// YoloOBB 类用于加载并调用 yolo_obb.dll 中的函数（支持旋转框检测）
    /// </summary>
    internal class Yolo8Obb
    {
        // 检测结果结构体
        [StructLayout(LayoutKind.Sequential)]
        public struct DetectionResult
        {
            public RotatedRect box;
            public int class_id;
            public float confidence;
        }

        // 定义委托类型，对应 DLL 中的函数指针
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr InitDetectorDelegate(string model_path, string[] class_names, int num_classes, int input_size, float conf, float nms);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DetectDelegate(IntPtr detector_ptr, byte[] image_data, int width, int height, int channels, out IntPtr results, out int result_count);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DestroyDetectorDelegate(IntPtr detector_ptr);

        // 运行时加载的 DLL 句柄
        private static IntPtr _hModule = IntPtr.Zero;

        // 委托实例
        private static InitDetectorDelegate _initDetector;
        private static DetectDelegate _detect;
        private static DestroyDetectorDelegate _destroyDetector;

        // 加载 DLL 并绑定函数
        static Yolo8Obb()
        {
            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yolo_obb.dll");

            if (!File.Exists(dllPath))
                LogHelper.AddLog(MsgLevel.Exception, $"找不到 yolo_obb.dll，请确保它位于应用程序目录: {dllPath}", true);

            _hModule = LoadLibrary(dllPath);
            if (_hModule == IntPtr.Zero)
                LogHelper.AddLog(MsgLevel.Exception, "加载 yolo_obb.dll 失败!", true);

            // 获取函数地址并绑定到委托
            _initDetector = Marshal.GetDelegateForFunctionPointer<InitDetectorDelegate>(GetProcAddress(_hModule, "YOLO_Initialize"));
            _detect = Marshal.GetDelegateForFunctionPointer<DetectDelegate>(GetProcAddress(_hModule, "YOLO_Inference"));
            _destroyDetector = Marshal.GetDelegateForFunctionPointer<DestroyDetectorDelegate>(GetProcAddress(_hModule, "YOLO_Release"));
        }

        // 导出的函数接口

        /// <summary>
        /// 创建检测器句柄
        /// </summary>
        public static IntPtr CreateDetector(string model_path, string[] class_names, int num_classes, int input_size, float conf, float nms)
        {
            return _initDetector(model_path, class_names, num_classes, input_size, conf, nms);
        }

        /// <summary>
        /// 执行检测
        /// </summary>
        public static int Detect(IntPtr handle, byte[] image_data, int width, int height, int channels, [Out] List<ObbResult> detectResults)
        {
            // 先清空输出列表
            detectResults.Clear();

            IntPtr resultsPtr;
            int resultCount;

            // 调用原生推理接口
            _detect(handle, image_data, width, height, channels, out resultsPtr, out resultCount);

            if (resultCount <= 0)
                return 0;

            int structSize = Marshal.SizeOf(typeof(RotatedRectResult));
            for (int i = 0; i < resultCount; i++)
            {
                IntPtr currentPtr = new IntPtr(resultsPtr.ToInt64() + i * structSize);
                RotatedRectResult raw = (RotatedRectResult)Marshal.PtrToStructure(currentPtr, typeof(RotatedRectResult));

                // 构建 ObbResult
                ObbResult obb = new ObbResult
                {
                    ClassId = raw.class_id,
                    Score = raw.confidence,

                    RotateBox = new RotatedRect
                    {
                        center = new Point2f { x = raw.center_x, y = raw.center_y },
                        size = new Size2f { width = raw.width, height = raw.height },
                        angle = raw.angle  // 单位：角度
                    }
                };

                detectResults.Add(obb);
            }

            // 释放 native 分配的内存
            Marshal.FreeHGlobal(resultsPtr);

            return detectResults.Count;
        }
        /// <summary>
        /// 销毁检测器句柄
        /// </summary>
        public static void DestroyDetector(IntPtr detector)
        {
            _destroyDetector(detector);
        }

        /// <summary>
        /// 解密文件
        /// </summary>
        public static void DecryptFile(string encryptedPath, string outputPath, byte[] key, byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                using (FileStream input = File.OpenRead(encryptedPath))
                using (FileStream output = File.Create(outputPath))
                using (CryptoStream crypto = new CryptoStream(input, aes.CreateDecryptor(), CryptoStreamMode.Read))
                {
                    crypto.CopyTo(output);
                }
            }
        }

        // Win32 API 函数声明
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    }
}