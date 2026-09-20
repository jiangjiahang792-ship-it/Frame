using System;
using System.ComponentModel;
using OpenCvSharp;

namespace TDJS_Vision.Node._4_Measurement.TerminalAngle
{
    /// <summary>绘制帧中的水平搜索框及当时的定位快照；与自动拟合矩形独立。</summary>
    [Newtonsoft.Json.JsonObject, TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class TerminalAngleRoi
    {
        /// <summary>左边界列坐标，包含该像素。</summary>
        [Category("区域"), DisplayName("左边界")] public double Left { get; set; }
        /// <summary>上边界行坐标，包含该像素。</summary>
        [Category("区域"), DisplayName("上边界")] public double Top { get; set; }
        /// <summary>右边界列坐标，包含该像素。</summary>
        [Category("区域"), DisplayName("右边界")] public double Right { get; set; }
        /// <summary>下边界行坐标，包含该像素。</summary>
        [Category("区域"), DisplayName("下边界")] public double Bottom { get; set; }
        /// <summary>绘制该区域时的定位变换；旧方案为空时仍按原基准坐标解释。</summary>
        public TerminalAnglePoseSnapshot DrawingPose { get; set; }
        /// <summary>返回独立参数副本，防止界面编辑污染运行参数。</summary>
        public TerminalAngleRoi Copy()
        {
            var copy = (TerminalAngleRoi)MemberwiseClone();
            copy.DrawingPose = DrawingPose == null ? null : DrawingPose.Copy();
            return copy;
        }
        /// <summary>验证并转换到包含两端像素的 OpenCV 区域；越界直接拒绝，不静默裁切。</summary>
        public Rect ToRect(int width, int height)
        {
            if (!Finite(Left) || !Finite(Top) || !Finite(Right) || !Finite(Bottom) ||
                Left < 0 || Top < 0 || Right >= width || Bottom >= height || Right - Left < 5 || Bottom - Top < 5)
                throw new ArgumentException("ROI 越界或尺寸不足，请在原图内重新绘制。");
            int x = (int)Math.Floor(Left), y = (int)Math.Floor(Top);
            int r = (int)Math.Ceiling(Right), b = (int)Math.Ceiling(Bottom);
            if (r >= width || b >= height) throw new ArgumentException("ROI 取整后超出图像，请向内调整边界。");
            return new Rect(x, y, r - x + 1, b - y + 1);
        }
        /// <summary>检查有限实数，兼容 .NET Framework。</summary>
        internal static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        /// <summary>属性面板中的区域说明。</summary>
        public override string ToString() { return string.Format("({0:F1},{1:F1})～({2:F1},{3:F1})", Left, Top, Right, Bottom); }
    }

    /// <summary>端子角度节点持久化参数，仅保存订阅文本、ROI 与算法配置。</summary>
    public sealed class NodeParamTerminalAngle : INodeParam
    {
        /// <summary>上游图像节点显示文本。</summary>
        [Browsable(false)] public string Text1 { get; set; }
        /// <summary>上游图像结果显示名称。</summary>
        [Browsable(false)] public string Text2 { get; set; }
        /// <summary>是否根据上游定位结果修正两个基准区域；旧方案默认关闭。</summary>
        public bool UsePositionCorrection { get; set; }
        /// <summary>位置修正信息列表的上游节点名称。</summary>
        public string CorrectionText1 { get; set; }
        /// <summary>位置修正信息列表的输出端口名称。</summary>
        public string CorrectionText2 { get; set; }
        /// <summary>端子主体搜索区域；未绘制时为空。</summary>
        [Category("区域"), DisplayName("端子区域")] public TerminalAngleRoi TerminalRoi { get; set; }
        /// <summary>基座参考区域，只确定固定水平垂直基准的显示位置，不拟合基座倾角。</summary>
        [Category("区域"), DisplayName("基座区域")] public TerminalAngleRoi BaseRoi { get; set; }
        /// <summary>彩色图优先使用蓝色通道以增强金色端子的外轮廓对比。</summary>
        [Category("分割"), DisplayName("使用蓝色通道")] public bool UseBlueChannel { get; set; } = true;
        /// <summary>低于或等于此值的像素作为端子前景。</summary>
        [Category("分割"), DisplayName("分割阈值")] public double Threshold { get; set; } = 220;
        /// <summary>高斯核大小，使用与原算法对应的显式 sigma。</summary>
        [Category("分割"), DisplayName("高斯尺寸")] public int GaussianSize { get; set; } = 3;
        /// <summary>旋转矩形短边最小宽度，单位为当前输入图像素。</summary>
        [Category("质量检查"), DisplayName("最小端子宽度")] public double MinimumWidth { get; set; } = 25;
        /// <summary>旋转矩形短边最大宽度，单位为当前输入图像素。</summary>
        [Category("质量检查"), DisplayName("最大端子宽度")] public double MaximumWidth { get; set; } = 105;
        /// <summary>主体在搜索框内应覆盖的最低高度比例。</summary>
        [Category("质量检查"), DisplayName("最低高度覆盖率")] public double MinimumHeightCoverage { get; set; } = 0.9;
        /// <summary>复制运行快照，保持原图坐标不随窗口缩放改变。</summary>
        public NodeParamTerminalAngle Copy()
        {
            var copy = (NodeParamTerminalAngle)MemberwiseClone();
            copy.TerminalRoi = TerminalRoi == null ? null : TerminalRoi.Copy();
            copy.BaseRoi = BaseRoi == null ? null : BaseRoi.Copy();
            return copy;
        }
        /// <summary>参数窗体和实际运行共用校验，防止手工方案绕过约束。</summary>
        public void Validate(int width, int height)
        {
            if (TerminalRoi == null || BaseRoi == null) throw new ArgumentException("请绘制端子 ROI 和基座 ROI 两个区域。");
            TerminalRoi.ToRect(width, height); BaseRoi.ToRect(width, height);
            if (!TerminalAngleRoi.Finite(Threshold) || Threshold < 0 || Threshold > 255 ||
                GaussianSize < 3 || GaussianSize > 11 || GaussianSize % 2 == 0 ||
                !TerminalAngleRoi.Finite(MinimumWidth) || !TerminalAngleRoi.Finite(MaximumWidth) || MinimumWidth < 1 || MaximumWidth <= MinimumWidth ||
                !TerminalAngleRoi.Finite(MinimumHeightCoverage) || MinimumHeightCoverage < 0.5 || MinimumHeightCoverage > 1)
                throw new ArgumentException("阈值、高斯尺寸、矩形宽度或高度覆盖率无效。");
        }
    }
}
