using System.ComponentModel;
using TDJS_Vision.Device._3D;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._1_Acquisition.ImageSource3D
{
    /// <summary>
    /// 3D 图像源节点运行结果，输出完整 3D 帧和深度预览图。
    /// </summary>
    public class NodeResultImageSource3D : INodeResult
    {
        /// <summary>
        /// 节点运行耗时，单位毫秒。
        /// </summary>
        public int RunTime { get; set; }

        /// <summary>
        /// 3D 相机完整帧数据。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("3D数据源")]
        public Camera3DFrameData FrameData { get; set; } = new Camera3DFrameData();

        /// <summary>
        /// 深度图预览图，用于兼容现有图像显示节点。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("2D数据源")]
        public OutputImage DepthImage { get; set; } = new OutputImage();

        /// <summary>
        /// 当前帧号。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("帧号")]
        public uint FrameNumber { get; set; }

        /// <summary>
        /// 原始点云总点数。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("点云总点数")]
        public int TotalPointCount { get; set; }
    }
}
