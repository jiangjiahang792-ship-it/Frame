using System.Collections.Generic;

namespace TDJS_Vision.Node._3_Detection.TDAI.Yolo8
{
    /// <summary>
    /// YOLO隔离worker共享图像传输的容量规则。
    /// </summary>
    internal static class YoloSharedImageTransport
    {
        /// <summary>
        /// 共享图像缓冲按4MB粒度增长，减少相近分辨率切换时重复创建映射。
        /// </summary>
        public const long CapacityGrowthBytes = 4L * 1024L * 1024L;

        /// <summary>
        /// 单个YOLO模型worker允许的最大共享图像字节数。
        /// </summary>
        public const long MaximumImageBytes = 512L * 1024L * 1024L;
    }

    /// <summary>
    /// YOLO隔离worker请求消息。
    /// </summary>
    internal sealed class YoloIsolatedRequest
    {
        /// <summary>
        /// 获取或设置命令名称。
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// 获取或设置模型文件路径。
        /// </summary>
        public string ModelPath { get; set; }

        /// <summary>
        /// 获取或设置模型类型。
        /// </summary>
        public ModelType ModelType { get; set; }

        /// <summary>
        /// 获取或设置推理设备类型。
        /// </summary>
        public DeviceType DeviceType { get; set; }

        /// <summary>
        /// 获取或设置类别名称。
        /// </summary>
        public string[] ClassNames { get; set; }

        /// <summary>
        /// 获取或设置输入尺寸。
        /// </summary>
        public int InputSize { get; set; }

        /// <summary>
        /// 获取或设置置信度阈值。
        /// </summary>
        public float ScoreThreshold { get; set; }

        /// <summary>
        /// 获取或设置NMS阈值。
        /// </summary>
        public float NmsThreshold { get; set; }

        /// <summary>
        /// 获取或设置关键点数量。
        /// </summary>
        public int KeyPointNum { get; set; }

        /// <summary>
        /// 获取或设置图像行数。
        /// </summary>
        public int Rows { get; set; }

        /// <summary>
        /// 获取或设置图像列数。
        /// </summary>
        public int Cols { get; set; }

        /// <summary>
        /// 获取或设置OpenCV Mat类型整数值。
        /// </summary>
        public int MatType { get; set; }

        /// <summary>
        /// 获取或设置父进程创建的命名共享内存名称。
        /// </summary>
        public string ImageMapName { get; set; }

        /// <summary>
        /// 获取或设置共享内存映射容量。
        /// </summary>
        public long ImageMapCapacity { get; set; }

        /// <summary>
        /// 获取或设置本帧有效图像字节数。
        /// </summary>
        public long ImageByteCount { get; set; }

        /// <summary>
        /// 获取或设置共享图像的紧凑行步长。
        /// </summary>
        public long ImageStride { get; set; }

        /// <summary>
        /// 获取或设置结果X方向偏移。
        /// </summary>
        public int DeltaX { get; set; }

        /// <summary>
        /// 获取或设置结果Y方向偏移。
        /// </summary>
        public int DeltaY { get; set; }

        /// <summary>
        /// 获取或设置SEG是否需要计算掩膜不规则框。
        /// </summary>
        public bool NeedMaskBox { get; set; }
    }

    /// <summary>
    /// YOLO隔离worker响应消息。
    /// </summary>
    internal sealed class YoloIsolatedResponse
    {
        /// <summary>
        /// 获取或设置命令是否成功。
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 获取或设置失败原因。
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// 获取或设置DET检测结果。
        /// </summary>
        public List<YoloIsolatedDetResult> DetResults { get; set; }

        /// <summary>
        /// 获取或设置OBB检测结果。
        /// </summary>
        public List<YoloIsolatedObbResult> ObbResults { get; set; }

        /// <summary>
        /// 获取或设置SEG检测结果。
        /// </summary>
        public List<YoloIsolatedSegResult> SegResults { get; set; }

        /// <summary>
        /// 获取或设置POSE检测结果。
        /// </summary>
        public YoloIsolatedPoseResult PoseResult { get; set; }

        /// <summary>
        /// 获取或设置共享图像传输校验时的全部通道像素和。
        /// </summary>
        public double SharedImageValueSum { get; set; }
    }

    /// <summary>
    /// 隔离worker DET结果DTO。
    /// </summary>
    internal sealed class YoloIsolatedDetResult
    {
        /// <summary>类别ID。</summary>
        public int ClassId { get; set; }

        /// <summary>置信度。</summary>
        public float Score { get; set; }

        /// <summary>矩形X坐标。</summary>
        public int X { get; set; }

        /// <summary>矩形Y坐标。</summary>
        public int Y { get; set; }

        /// <summary>矩形宽度。</summary>
        public int Width { get; set; }

        /// <summary>矩形高度。</summary>
        public int Height { get; set; }
    }

    /// <summary>
    /// 隔离worker OBB结果DTO。
    /// </summary>
    internal sealed class YoloIsolatedObbResult
    {
        /// <summary>中心X坐标。</summary>
        public float CenterX { get; set; }

        /// <summary>中心Y坐标。</summary>
        public float CenterY { get; set; }

        /// <summary>宽度。</summary>
        public float Width { get; set; }

        /// <summary>高度。</summary>
        public float Height { get; set; }

        /// <summary>角度。</summary>
        public float Angle { get; set; }

        /// <summary>类别ID。</summary>
        public int ClassId { get; set; }

        /// <summary>置信度。</summary>
        public float Confidence { get; set; }
    }

    /// <summary>
    /// 隔离worker SEG结果DTO。
    /// </summary>
    internal sealed class YoloIsolatedSegResult
    {
        /// <summary>类别ID。</summary>
        public int ClassId { get; set; }

        /// <summary>置信度。</summary>
        public float Score { get; set; }

        /// <summary>矩形X坐标。</summary>
        public int X { get; set; }

        /// <summary>矩形Y坐标。</summary>
        public int Y { get; set; }

        /// <summary>矩形宽度。</summary>
        public int Width { get; set; }

        /// <summary>矩形高度。</summary>
        public int Height { get; set; }

        /// <summary>左上角X。</summary>
        public float TopLeftX { get; set; }

        /// <summary>左上角Y。</summary>
        public float TopLeftY { get; set; }

        /// <summary>右上角X。</summary>
        public float TopRightX { get; set; }

        /// <summary>右上角Y。</summary>
        public float TopRightY { get; set; }

        /// <summary>左下角X。</summary>
        public float BottomLeftX { get; set; }

        /// <summary>左下角Y。</summary>
        public float BottomLeftY { get; set; }

        /// <summary>右下角X。</summary>
        public float BottomRightX { get; set; }

        /// <summary>右下角Y。</summary>
        public float BottomRightY { get; set; }
    }

    /// <summary>
    /// 隔离worker POSE结果DTO。
    /// </summary>
    internal sealed class YoloIsolatedPoseResult
    {
        /// <summary>姿态框集合。</summary>
        public List<YoloIsolatedDetResult> Boxes { get; set; }

        /// <summary>关键点集合。</summary>
        public List<YoloIsolatedPoint> KeyPoints { get; set; }
    }

    /// <summary>
    /// 隔离worker点坐标DTO。
    /// </summary>
    internal sealed class YoloIsolatedPoint
    {
        /// <summary>X坐标。</summary>
        public int X { get; set; }

        /// <summary>Y坐标。</summary>
        public int Y { get; set; }
    }
}
