using System;
using System.IO;
using System.Runtime.InteropServices;
using Logger;

namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    #region 使用示例
    
    //// 初始化模型
    //Yolo8Pose.Initialize("best-pose.onnx", new[] { "person" }, 1, 640, 0.5f, 0.4f, 17);

    //// 图像数据准备（假设已经获取）
    //byte[] imageData = File.ReadAllBytes("test.jpg");
    //int width = 640;
    //int height = 480;
    //int channels = 3;

    //// 执行推理
    //IntPtr boxesData, keypointsData;
    //int boxesCount, keypointsCount, keypointsPerObject;

    //Yolo8Pose.Inference(imageData, width, height, channels,
    //                    out boxesData, out boxesCount,
    //                    out keypointsData, out keypointsCount,
    //                    out keypointsPerObject);

    //// TODO: 解析 boxesData 和 keypointsData 指针中的内容

    //// 清理资源
    //Yolo8Pose.Cleanup();
   
    #endregion

    /// <summary>
    /// Yolo8Pose 类用于加载和调用 yolo_pose.dll 中的函数
    /// </summary>
    internal class Yolo8Pose
    {
        // 委托定义
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void InitializeDelegate(string modelPath, string[] classLabels, int classCount, int modelSize, float scoreThreshold, float nmsThreshold, int keyPointNum);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void InferenceDelegate(byte[] imageData, int width, int height, int channels,
                                              out IntPtr boxesData, out int boxesCount,
                                              out IntPtr keypointsData, out int keypointsCount,
                                              out int keypointsPerObject);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CleanupDelegate();

        // DLL 句柄和函数委托
        private static IntPtr _hModule = IntPtr.Zero;

        private static InitializeDelegate _initialize;
        private static InferenceDelegate _inference;
        private static CleanupDelegate _cleanup;

        // 静态构造函数加载 DLL 并绑定函数
        static Yolo8Pose()
        {
            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yolo_pose.dll");

            if (!File.Exists(dllPath))
                LogHelper.AddLog(MsgLevel.Exception, $"找不到 yolo_pose.dll，请确保它位于应用程序目录: {dllPath}", true);

            _hModule = LoadLibrary(dllPath);
            if (_hModule == IntPtr.Zero)
                LogHelper.AddLog(MsgLevel.Exception, "加载 yolo_pose.dll 失败!", true);

            try
            {
                _initialize = Marshal.GetDelegateForFunctionPointer<InitializeDelegate>(GetProcAddress(_hModule, "Initialize"));
                _inference = Marshal.GetDelegateForFunctionPointer<InferenceDelegate>(GetProcAddress(_hModule, "Inference"));
                _cleanup = Marshal.GetDelegateForFunctionPointer<CleanupDelegate>(GetProcAddress(_hModule, "Cleanup"));
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"绑定 yolo_pose.dll 函数失败: {ex.Message}", true);
            }
        }

        // 公共接口方法

        /// <summary>
        /// 初始化模型
        /// </summary>
        public static void Initialize(string modelPath, string[] classLabels, int classCount, int modelSize, float scoreThreshold, float nmsThreshold, int keyPointNum)
        {
            _initialize(modelPath, classLabels, classCount, modelSize, scoreThreshold, nmsThreshold, keyPointNum);
        }

        /// <summary>
        /// 执行姿态检测推理
        /// </summary>
        public static void Inference(byte[] imageData, int width, int height, int channels,
                                     out IntPtr boxesData, out int boxesCount,
                                     out IntPtr keypointsData, out int keypointsCount,
                                     out int keypointsPerObject)
        {
            _inference(imageData, width, height, channels,
                       out boxesData, out boxesCount,
                       out keypointsData, out keypointsCount,
                       out keypointsPerObject);
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public static void Cleanup()
        {
            _cleanup();
        }

        // Win32 API 声明
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    }
}