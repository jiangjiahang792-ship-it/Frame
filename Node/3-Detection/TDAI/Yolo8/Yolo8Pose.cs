using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Logger;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Text;
using OpenCvSharp;

namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    #region 使用示例

    //static void Main()
    //{
    //    // 初始化模型
    //    string modelPath = "D:\\OpenVino_DLL\\model\\TrumpetPose_encrypted1.onnx";
    //    string[] classLabels = { "Trumpet" };
    //    int modelSize = 320;
    //    float scoreThreshold = 0.5f;
    //    float nmsThreshold = 0.45f;
    //    int keyPointNum = 12;
    //    IntPtr handle = Yolo8Pose.Initialize(modelPath, classLabels, classLabels.Length, modelSize, scoreThreshold, nmsThreshold, keyPointNum);

    //    // 读取图像
    //    Mat image = Cv2.ImRead("D:\\OpenVino_DLL\\model\\Trumpet.jpg", ImreadModes.Color);
    //    byte[] imageData = new byte[image.Total() * image.ElemSize()];
    //    Marshal.Copy(image.Data, imageData, 0, imageData.Length);

    //    // 进行推理
    //    IntPtr boxesDataPtr;
    //    int boxesCount;
    //    IntPtr keypointsDataPtr;
    //    int keypointsCount;
    //    int keypointsPerObject;
    //    Yolo8Pose.Inference(imageData, image.Width, image.Height, image.Channels(), out boxesDataPtr, out boxesCount, out keypointsDataPtr, out keypointsCount, out keypointsPerObject);

    //    // 将指针转换为数组
    //    float[] boxesData = new float[boxesCount];
    //    float[] keypointsData = new float[keypointsCount];
    //    Marshal.Copy(boxesDataPtr, boxesData, 0, boxesCount);
    //    Marshal.Copy(keypointsDataPtr, keypointsData, 0, keypointsCount);

    //    // 可视化结果
    //    for (int i = 0; i < boxesCount; i += 4)
    //    {
    //        int x = (int)boxesData[i];
    //        int y = (int)boxesData[i + 1];
    //        int width = (int)boxesData[i + 2];
    //        int height = (int)boxesData[i + 3];
    //        Rect rect = new Rect(x, y, width, height);
    //        Cv2.Rectangle(image, rect, Scalar.Red, 2);
    //    }

    //    for (int i = 0; i < keypointsCount; i += keypointsPerObject)
    //    {
    //        for (int j = 0; j < keypointsPerObject; j += 3)
    //        {
    //            int x = (int)keypointsData[i + j];
    //            int y = (int)keypointsData[i + j + 1];
    //            Point point = new Point(x, y);
    //            Cv2.Circle(image, point, 5, Scalar.Green, -1);
    //        }
    //    }

    //    // 显示图像
    //    Cv2.ImShow("Detection Result", image);
    //    Cv2.WaitKey(0);

    //    // 释放内存
    //    Marshal.FreeHGlobal(boxesDataPtr);
    //    Marshal.FreeHGlobal(keypointsDataPtr);

    //    // 清理资源
    //    Yolo8Pose.Cleanup(handle);
    //}

    #endregion

    /// <summary>
    /// Yolo8Pose 类用于加载和调用 yolo_pose.dll 中的函数
    /// </summary>
    internal class Yolo8Pose
    {
        // 委托定义
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr InitializeDelegate(string modelPath, string[] classLabels, int classCount, int modelSize, 
                                                float scoreThreshold, float nmsThreshold, int keyPointNum);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int InferenceDelegate(IntPtr handle, byte[] imageData, int width, int height, int channels,
                                                out IntPtr boxesData, out int boxesCount,
                                                out IntPtr keypointsData, out int keypointsCount,
                                                out int keypointsPerObject);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CleanupDelegate(IntPtr inference);

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
        public static IntPtr Initialize(string modelPath, string[] classLabels, int classCount, int modelSize, float scoreThreshold, float nmsThreshold, int keyPointNum)
        {
            return _initialize(modelPath, classLabels, classCount, modelSize, scoreThreshold, nmsThreshold, keyPointNum);
        }

        /// <summary>
        /// 执行姿态检测推理
        /// </summary>
        public static (int, int) Infer(IntPtr handle, byte[] image_data, int width, int height, int channels, [Out] PoseResult detectResult)
        {

            if (handle == IntPtr.Zero)
                throw new Exception("模型句柄为空！");
            // 进行推理
            IntPtr boxesDataPtr;
            int boxesCount;
            IntPtr keypointsDataPtr;
            int keypointsCount;
            int keypointsPerObject;
            _inference(handle, image_data, width, height, channels, out boxesDataPtr, out boxesCount, out keypointsDataPtr, out keypointsCount, out keypointsPerObject);

            // 将指针转换为数组
            float[] boxesData = new float[boxesCount];
            float[] keypointsData = new float[keypointsCount];
            Marshal.Copy(boxesDataPtr, boxesData, 0, boxesCount);
            Marshal.Copy(keypointsDataPtr, keypointsData, 0, keypointsCount);

            detectResult.Boxs = new List<Rect>();
            detectResult.KeyPoints = new List<Point>();
            // 可视化结果
            for (int i = 0; i < boxesCount; i += 4)
            {
                int x = (int)boxesData[i];
                int y = (int)boxesData[i + 1];
                int rect_width = (int)boxesData[i + 2];
                int rect_height = (int)boxesData[i + 3];
                Rect rect = new Rect(x, y, rect_width, rect_height);
                detectResult.Boxs.Add(rect);
            }

            for (int i = 0; i < keypointsCount; i += keypointsPerObject)
            {
                for (int j = 0; j < keypointsPerObject; j += 3)
                {
                    int x = (int)keypointsData[i + j];
                    int y = (int)keypointsData[i + j + 1];
                    Point point = new Point(x, y);
                    detectResult.KeyPoints.Add(point);
                }
            }
            return (boxesCount/4, keypointsCount/ keypointsPerObject);
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public static void Cleanup(IntPtr inference)
        {
            if (inference != IntPtr.Zero)
                _cleanup(inference);
        }

        // Win32 API 声明
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    }
}