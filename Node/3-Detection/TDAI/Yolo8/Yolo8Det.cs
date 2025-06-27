using System;
using System.Runtime.InteropServices;
using System.IO;
using Logger;
using System.Security.Cryptography;
using OpenCvSharp;
using System.Collections.Generic;


namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    #region 使用示例

    //IntPtr detector = Yolo8.CreateDetector("yolov8s.onnx", new[] { "person", "car", "dog" }, 3, 640, 0.25f, 0.45f);
    //DetectionResult[] results = new DetectionResult[100];
    //int count = Yolo8.Detect(detector, imageData, width, height, 3, results);
    //// ...
    //Yolo8.DestroyDetector(detector);

    #endregion

    /// <summary>
    /// YoloV8Det 类用于加载和调用 yolo_det.dll 中的函数
    /// </summary>
    internal class Yolo8Det
    {
        // 定义检测结果结构体
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
        // 定义委托类型，对应 DLL 中的函数指针
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr CreateDetectorDelegate(string model_path, string[] class_names, int class_count, int model_size, float score_threshold, float nms_threshold);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DestroyDetectorDelegate(IntPtr detector);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int DetectDelegate(IntPtr detector, byte[] img_data, int width, int height, int channels, [Out] DetectionResult[] results);

        // 运行时加载的 DLL 句柄
        private static IntPtr _hModule = IntPtr.Zero;

        // 委托实例
        private static CreateDetectorDelegate _createDetector;
        private static DestroyDetectorDelegate _destroyDetector;
        private static DetectDelegate _detect;

        // 加载 DLL 并绑定函数
        static Yolo8Det()
        {
            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yolo_det.dll");

            if (!File.Exists(dllPath))
                LogHelper.AddLog(MsgLevel.Exception, $"找不到 yolo_det.dll，请确保它位于应用程序目录: {dllPath}", true);

            _hModule = LoadLibrary(dllPath);
            if (_hModule == IntPtr.Zero)
                LogHelper.AddLog(MsgLevel.Exception, "加载 yolo_det.dll 失败!", true);

            // 获取函数地址并绑定到委托
            _createDetector = Marshal.GetDelegateForFunctionPointer<CreateDetectorDelegate>(GetProcAddress(_hModule, "CreateDetector"));
            _destroyDetector = Marshal.GetDelegateForFunctionPointer<DestroyDetectorDelegate>(GetProcAddress(_hModule, "DestroyDetector"));
            _detect = Marshal.GetDelegateForFunctionPointer<DetectDelegate>(GetProcAddress(_hModule, "Detect"));
        }

        // 导出的函数接口

        /// <summary>
        /// 创建检测器句柄
        /// </summary>
        /// <param name="model_path"></param>
        /// <param name="class_names"></param>
        /// <param name="class_count"></param>
        /// <param name="model_size"></param>
        /// <param name="score_threshold"></param>
        /// <param name="nms_threshold"></param>
        /// <returns></returns>
        public static IntPtr CreateDetector(string model_path, string[] class_names, int class_count, int model_size, float score_threshold, float nms_threshold)
        {
            return _createDetector(model_path, class_names, class_count, model_size, score_threshold, nms_threshold);
        }
        /// <summary>
        /// 销毁检测器句柄
        /// </summary>
        /// <param name="detector"></param>
        public static void DestroyDetector(IntPtr detector)
        {
            if (detector != IntPtr.Zero)
                _destroyDetector(detector);
        }
        /// <summary>
        /// 执行AI检测
        /// </summary>
        /// <param name="detector"></param>
        /// <param name="img_data"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="channels"></param>
        /// <param name="results"></param>
        /// <returns></returns>
        public static int Detect(IntPtr detector, byte[] img_data, int width, int height, int channels, [Out] List<DetResult> detectResults)
        {
            if (detector == IntPtr.Zero)
                throw new Exception("模型句柄为空！");
            DetectionResult[] results = new DetectionResult[1000];
            int count = _detect(detector, img_data, width, height, channels, results);

            if (results != null && results.Length >= count)
            {
                for (int i = 0; i < count; i++)
                {
                    var dr = results[i];
                    detectResults.Add(new DetResult()
                    {
                        ClassId = dr.class_id,
                        Score = dr.score,
                        Box = new Rect(dr.left, dr.top, dr.width, dr.height)
                    });
                }
            }

            return count;
        }
        /// <summary>
        /// 解密文件
        /// </summary>
        /// <param name="encryptedPath"></param>
        /// <param name="outputPath"></param>
        /// <param name="key"></param>
        /// <param name="iv"></param>
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
