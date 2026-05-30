using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using OpenCvSharp;
using Point = OpenCvSharp.Point;


namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    /// <summary>
    /// Yolo8 Seg 类
    /// </summary>
    public class Yolo8Seg : IYolo8
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

        // 导入DLL函数
        [DllImport("td_seg.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr Initialize(
            string modelPath,
            string[] classNames,
            int classCount,
            int modelSize,
            float scoreThreshold,
            float nmsThreshold);

        [DllImport("td_seg.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Infer(
            IntPtr context,
            byte[] imageData,
            int width,
            int height,
            int channels,
            out int boxCount,
            out IntPtr boxData,
            out IntPtr scoreData,
            out IntPtr classIdData,
            out int maskCount,
            out IntPtr maskWidth,
            out IntPtr maskHeight,
            out IntPtr maskData);

        [DllImport("td_seg.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Release(IntPtr context);


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
        /// 模型类型
        /// </summary>
        public ModelType ModelType { get; } = ModelType.SEG;
        public DeviceType DeviceType { get; set; }
        public float ScoreThreshold { get; set; }
        public float NMSThreshold { get; set; }

        /// <summary>
        /// 初始化模型
        /// </summary>
        public void Init(string model_path, DeviceType deviceType,string[] class_names, int input_size, float score_threshold, float nms_threshold, int key_point_num = 0)
        {
            DeviceType = deviceType;
            ScoreThreshold = score_threshold;
            NMSThreshold = nms_threshold;

            switch (DeviceType)
            {
                case DeviceType.CPU:
                    _handle = Initialize(model_path, class_names, class_names.Length, input_size, score_threshold, nms_threshold);
                    break;
                case DeviceType.GPU:
                    _handle = YoloV8_Init("seg", model_path, input_size, input_size, class_names, class_names.Length, score_threshold, nms_threshold, 0);
                    break;
                default:
                    throw new Exception("未找到对应运行设备的指定");
            }

            if (_handle == IntPtr.Zero)
            {
                throw new Exception("Failed to initialize YOLO segmentation model.");
            }
        }


        /// <summary>
        /// 执行推理并返回检测结果列表
        /// </summary>
        public List<SegResult> Detect(Mat image, int deltaX = 0, int deltaY = 0)
        {
            var results = new List<SegResult>();

            switch (DeviceType)
            {
                case DeviceType.CPU:
                    byte[] imageData = new byte[image.Total() * image.ElemSize()];
                    Marshal.Copy(image.Data, imageData, 0, imageData.Length);
                    // Perform inference
                    int boxCount;
                    IntPtr boxData;
                    IntPtr scoreData;
                    IntPtr classIdData;
                    int maskCount;
                    IntPtr maskWidth;
                    IntPtr maskHeight;
                    IntPtr maskData;

                    Infer(_handle, imageData, image.Width, image.Height, image.Channels(),
                          out boxCount, out boxData, out scoreData, out classIdData,
                          out maskCount, out maskWidth, out maskHeight, out maskData);

                    // Visualize the results
                    for (int i = 0; i < boxCount; i++)
                    {
                        // Get box data
                        int x = Marshal.ReadInt32(boxData, i * 4 * sizeof(int)) + deltaX;
                        int y = Marshal.ReadInt32(boxData, (i * 4 + 1) * sizeof(int)) + deltaY;
                        int w = Marshal.ReadInt32(boxData, (i * 4 + 2) * sizeof(int));
                        int h = Marshal.ReadInt32(boxData, (i * 4 + 3) * sizeof(int));

                        // Get score data
                        int intValue = Marshal.ReadInt32(scoreData, i * sizeof(float));

                        // 将整数按原字节位解释为浮点数（不改变字节内容）
                        float score = BitConverter.ToSingle(BitConverter.GetBytes(intValue), 0);

                        // Get class ID data
                        int classId = Marshal.ReadInt32(classIdData, i * sizeof(int));

                        // Draw bounding box
                        Cv2.Rectangle(image, new Rect(x, y, w, h), Scalar.Green, 2);

                        // Get mask data
                        int maskW = Marshal.ReadInt32(maskWidth, i * sizeof(int));
                        int maskH = Marshal.ReadInt32(maskHeight, i * sizeof(int));
                        IntPtr maskPtr = Marshal.ReadIntPtr(maskData, i * IntPtr.Size);

                        byte[] maskBytes = new byte[maskW * maskH];
                        Marshal.Copy(maskPtr, maskBytes, 0, maskBytes.Length);

                        // Create a Mat object for the mask
                        Mat mask = new Mat(maskH, maskW, MatType.CV_8UC1);
                        // Copy the mask data to the Mat object
                        for (int yMask = 0; yMask < maskH; yMask++)
                        {
                            for (int xMask = 0; xMask < maskW; xMask++)
                            {
                                mask.Set(yMask, xMask, maskBytes[yMask * maskW + xMask]);
                            }

                        }

                        // 查找轮廓
                        Point[][] contours;
                        HierarchyIndex[] hierarchy;
                        Cv2.FindContours(mask, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                        // 计算该轮廓的四个极点：左上、右上、右下、左下
                        Point topLeft = new Point(), topRight = new Point(), bottomRight = new Point(), bottomLeft = new Point();

                        // 在原图上绘制轮廓
                        foreach (var contour in contours)
                        {
                            // 将轮廓点坐标转换到原图坐标系
                            var scaledContour = contour.Select(p => new Point(p.X + x, p.Y + y)).ToArray();
                            //Cv2.DrawContours(image, new Point[][] { scaledContour }, -1, Scalar.Red, 2);

                            GetExtremeCorners(scaledContour, out topLeft, out topRight, out bottomRight, out bottomLeft);

                        }
                        results.Add(new SegResult
                        {
                            ClassId = classId,
                            Score = score,
                            Rect = new Rect(x, y, w, h),
                            Box = new Box(
                                new PointF(topLeft.X, topLeft.Y),
                                new PointF(topRight.X, topRight.Y),
                                new PointF(bottomLeft.X, bottomLeft.Y),
                                new PointF(bottomRight.X, bottomRight.Y))
                        });

                        mask.Dispose();
                    }
                    break;

                case DeviceType.GPU:
                    // 空值校验
                    if (image == null || image.Empty() || image.Data == IntPtr.Zero || _handle == IntPtr.Zero)
                    {
                        return new List<SegResult>();
                    }

                    // 调用底层检测接口
                    IntPtr resPtr = YoloV8_Infer(_handle, image.Data, image.Width, image.Height, image.Channels(), ScoreThreshold, NMSThreshold);
                    if (resPtr == IntPtr.Zero)
                        return new List<SegResult>();

                    try
                    {
                        // 解析结果结构体
                        YoloResultArray resArray = Marshal.PtrToStructure<YoloResultArray>(resPtr);
                        if (resArray.count <= 0)
                            return new List<SegResult>();

                        int structSize = Marshal.SizeOf(typeof(YoloResult));

                        // 遍历所有检测结果
                        for (int i = 0; i < resArray.count; i++)
                        {
                            // 计算当前结果指针
                            IntPtr currentObjPtr = new IntPtr(resArray.results.ToInt64() + i * structSize);
                            YoloResult res = Marshal.PtrToStructure<YoloResult>(currentObjPtr);

                            int x = (int)res.bbox_0 + deltaX;
                            int y = (int)res.bbox_1 + deltaY;
                            int w = (int)res.bbox_2;
                            int h = (int)res.bbox_3;

                            // Draw bounding box
                            Cv2.Rectangle(image, new Rect(x, y, w, h), Scalar.Green, 2);

                            int maskW = res.mask_width;
                            int maskH = res.mask_height;
                            IntPtr maskPtr = res.mask;

                            Point topLeft = new Point(), topRight = new Point(), bottomRight = new Point(), bottomLeft = new Point();

                            if (maskPtr != IntPtr.Zero && maskW > 0 && maskH > 0)
                            {
                                byte[] maskBytes = new byte[maskW * maskH];
                                Marshal.Copy(maskPtr, maskBytes, 0, maskBytes.Length);

                                // Create a Mat object for the mask
                                Mat mask = new Mat(maskH, maskW, MatType.CV_8UC1);
                                // Copy the mask data to the Mat object
                                for (int yMask = 0; yMask < maskH; yMask++)
                                {
                                    for (int xMask = 0; xMask < maskW; xMask++)
                                    {
                                        mask.Set(yMask, xMask, maskBytes[yMask * maskW + xMask]);
                                    }
                                }

                                // 查找轮廓
                                Point[][] contours;
                                HierarchyIndex[] hierarchy;
                                Cv2.FindContours(mask, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                                // 在原图上绘制轮廓
                                foreach (var contour in contours)
                                {
                                    // 将轮廓点坐标转换到原图坐标系
                                    var scaledContour = contour.Select(p => new Point(p.X + x, p.Y + y)).ToArray();
                                    GetExtremeCorners(scaledContour, out topLeft, out topRight, out bottomRight, out bottomLeft);
                                }
                                mask.Dispose(); // Memory management
                            }

                            // 封装为SegResult
                            results.Add(new SegResult
                            {
                                ClassId = res.class_id,
                                Score = res.conf,
                                Rect = new Rect(x, y, w, h),
                                Box = new Box(
                                    new PointF(topLeft.X, topLeft.Y),
                                    new PointF(topRight.X, topRight.Y),
                                    new PointF(bottomLeft.X, bottomLeft.Y),
                                    new PointF(bottomRight.X, bottomRight.Y))
                            });
                        }
                    }
                    finally
                    {
                        YoloV8_FreeResult(resPtr);
                    }
                    break;
                default:
                    throw new Exception("未找到对应运行设备的指定");
            }
            return results;
        }

        private static void GetExtremeCorners(Point[] contour,
            out Point topLeft,
            out Point topRight,
            out Point bottomRight,
            out Point bottomLeft)
        {
            if (contour == null || contour.Length == 0)
            {
                topLeft = new Point(0, 0);
                topRight = new Point(0, 0);
                bottomRight = new Point(0, 0);
                bottomLeft = new Point(0, 0);
                return;
            }

            var scored = contour.Select(p => new { P = p, Sum = p.X + p.Y, Diff = p.X - p.Y });
            topLeft = scored.OrderBy(t => t.Sum).First().P;         // 最小 x+y
            bottomRight = scored.OrderByDescending(t => t.Sum).First().P; // 最大 x+y
            topRight = scored.OrderByDescending(t => t.Diff).First().P;     // 最大 x-y
            bottomLeft = scored.OrderBy(t => t.Diff).First().P;            // 最小 x-y
        }

        /// <summary>
        /// 销毁模型资源
        /// </summary>
        public void Destroy()
        {
            if (_handle != IntPtr.Zero)
            {
                switch (DeviceType)
                {
                    case DeviceType.CPU:
                        Release(_handle);
                        break;
                    case DeviceType.GPU:
                        YoloV8_Release(_handle);
                        break;
                    default:
                        throw new Exception("未找到对应运行设备的指定");
                }
                _handle = IntPtr.Zero;
            }
        }
    }
}

