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
        public delegate IntPtr InitDetectorDelegate(string model_path, string[] class_names, int num_classes, int input_size, float conf, float nms, float score);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DetectDelegate(IntPtr detector_ptr, byte[] image_data, int width, int height, int channels, [Out] DetectionResult[] results, out int result_count);

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
            _initDetector = Marshal.GetDelegateForFunctionPointer<InitDetectorDelegate>(GetProcAddress(_hModule, "InitDetector"));
            _detect = Marshal.GetDelegateForFunctionPointer<DetectDelegate>(GetProcAddress(_hModule, "Detect"));
            _destroyDetector = Marshal.GetDelegateForFunctionPointer<DestroyDetectorDelegate>(GetProcAddress(_hModule, "DestroyDetector"));
        }

        // 导出的函数接口

        /// <summary>
        /// 创建检测器句柄
        /// </summary>
        public static IntPtr CreateDetector(string model_path, string[] class_names, int num_classes, int input_size, float conf, float nms, float score)
        {
            return _initDetector(model_path, class_names, num_classes, input_size, conf, nms, score);
        }

        /// <summary>
        /// 执行检测
        /// </summary>
        public static int Detect(IntPtr handle, byte[] image_data, int width, int height, int channels, [Out] List<ObbResult> detectResults)
        {
            int count = 0;
            DetectionResult[] results = new DetectionResult[1000];
            _detect(handle, image_data, width, height, channels, results, out count);

            if (results != null && results.Length >= count)
            {
                for (int i = 0; i < count; i++)
                {
                    var res = results[i];
                    detectResults.Add(new ObbResult()
                    {
                        RotateBox = res.box,
                        ClassId = res.class_id,
                        Score = res.confidence
                    });
                }
            }
            return count;
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