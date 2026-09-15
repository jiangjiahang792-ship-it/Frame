using Newtonsoft.Json;
using OpenCvSharp;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Documents;
using TDJS_Vision.Device;

namespace TDJS_Vision.Node._3_Detection.ColorDiscern
{
    public class NodeParamColorDiscern : INodeParam
    {
        /// <summary>
        /// 订阅节点的名称
        /// </summary>
        public string Text1 { get; set; }
        /// <summary>
        /// 订阅节点的属性
        /// </summary>
        public string Text2 { get; set; }

        /// <summary>
        /// 模版图片文件名称
        /// </summary>
        public string TemplateFileName { get; set; }

        /// <summary>
        /// 是否开启通信获取检测组
        /// </summary>
        public bool IsOpenGeiGround { get; set; }

        /// <summary>
        /// 设备名称
        /// </summary>
        public string DeviceName { get; set; }

        /// <summary>
        /// 通过通信获取检测项的设备
        /// </summary>
        [JsonIgnore]
        public IDevice Device { get; set; }

        /// <summary>
        /// 通信监听的地址
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// 是否启用位置修正跟随。
        /// </summary>
        public bool UsePositionCorrection { get; set; }

        /// <summary>
        /// 位置修正订阅节点的名称。
        /// </summary>
        public string CorrectionText1 { get; set; }

        /// <summary>
        /// 位置修正订阅节点的属性。
        /// </summary>
        public string CorrectionText2 { get; set; }


        // <summary>
        /// 是否开启结果排序
        /// </summary>
        public bool IsOpenSort { get; set; } = true;


        /// <summary>
        /// 排序方式 0为X排序,1为Y排序
        /// </summary>
        public int SrotModel { get; set; } = 0;
    }



    /// <summary>
    /// 颜色检测配置参数类
    /// </summary>
    public class ColorProfile
    {
        public string Name { get; set; } = "Target";

        /// <summary>
        /// 目标颜色的 Lab 值 (L:亮度 0-255, A:红绿, B:黄蓝)。
        /// Lab 空间比 RGB 更接近人眼感知，适合光照变化的场景。
        /// </summary>
        public Scalar TargetLab { get; set; }

        /// <summary>
        /// 亮度容差 (L通道 +/- 范围)。
        /// 值越大，允许的明暗变化越大。如果环境光不稳定，请调大此值。
        /// </summary>
        public double ToleranceL { get; set; } = 30;

        /// <summary>
        /// A通道容差 (红绿轴)。
        /// 独立控制 A 通道偏差，有助于区分绿色（A值低）和黑色（A值中等）。
        /// </summary>
        public double ToleranceA { get; set; } = 20;

        /// <summary>
        /// B通道容差 (蓝黄轴)。
        /// </summary>
        public double ToleranceB { get; set; } = 20;

        /// <summary>
        /// 兼容旧属性：色差容限。
        /// 设置此值将同步设置 ToleranceA 和 ToleranceB。
        /// 读取时返回 ToleranceA。
        /// </summary>
        public double ToleranceC
        {
            get { return ToleranceA; }
            set { ToleranceA = value; ToleranceB = value; }
        }

        /// <summary>
        /// Gamma 校正系数 (预处理)。
        /// 1.0 为原图。
        /// 小于 1.0 (如 0.5) 会提亮暗部，增加阴影细节。
        /// 大于 1.0 (如 2.0) 会压暗暗部，增加对比度。
        /// </summary>
        public double Gamma { get; set; } = 1.0;

        /// <summary>
        /// 是否启用背景移除 (结构提取)。
        /// 针对极低对比度或复杂纹理背景。
        /// </summary>
        public bool UseStructure { get; set; } = false;

        /// <summary>
        /// 结构提取模式:
        /// TopHat (顶帽运算): 提取比背景亮的细节 (白底找白物/暗底找亮物)。
        /// BlackHat (黑帽运算): 提取比背景暗的细节 (亮底找暗物)。
        /// </summary>
        public MorphTypes StructureOp { get; set; } = MorphTypes.TopHat;

        /// <summary>
        /// 结构提取的核大小。
        /// 决定了能提取多大的特征。物体越大，此值应越大，通常为物体宽度的 1/3 到 1/2。
        /// </summary>
        public int StructureKernelSize { get; set; } = 15;

        /// <summary>
        /// 结构提取后的二值化阈值。
        /// 提取出的特征强度大于此值才会被保留。
        /// </summary>
        public double StructureThreshold { get; set; } = 20;

        /// <summary>
        /// 是否启用形态学运算 (结果优化)。
        /// 用于填补二值图中的空洞或去除噪点。
        /// </summary>
        public bool UseMorphology { get; set; } = true;

        /// <summary>
        /// 形态学操作类型:
        /// Close (闭运算): 先膨胀后腐蚀，用于填补物体内部空洞，连接断开的区域。
        /// Open (开运算): 先腐蚀后膨胀，用于去除背景噪点，分离粘连物体。
        /// Dilate (膨胀): 扩大物体区域。
        /// Erode (腐蚀): 收缩物体区域。
        /// </summary>
        public MorphTypes MorphOp { get; set; } = MorphTypes.Close;

        /// <summary>
        /// 形态学核大小 (像素)。值越大，填补/去噪能力越强，但可能改变物体形状。
        /// </summary>
        public int MorphKernelSize { get; set; } = 5;

        /// <summary>
        /// 形态学迭代次数。
        /// </summary>
        public int MorphIterations { get; set; } = 1;

        /// <summary>
        /// 是否启用二次均值校验。
        /// 在初步提取轮廓后，计算轮廓内部的平均颜色再次与 TargetLab 对比。
        /// 能有效剔除边缘杂色或颜色不纯的噪点。
        /// </summary>
        public bool EnableMeanCheck { get; set; } = true;


        /// <summary>
        /// 筛选模式  All:得到所有识别到的颜色,MinArea:只要最小面积,MaxArea:只要最大面积
        /// </summary>
        public string ScreeningModel { get; set; } = "MaxArea";


        /// <summary>
        /// 最小面积过滤 (像素数)。小于此面积的斑点将被忽略。
        /// </summary>
        public int MinArea { get; set; } = 100;

        // --- 尺寸公差 (QC 判定) ---

        /// <summary>
        /// 是否打开宽度校验
        /// </summary>
        public bool IsOpenWidthCheck = true;

        /// <summary>
        /// 判定合格的最小宽度 (像素)。
        /// </summary>
        public int MinWidth { get; set; } = 0;

        /// <summary>
        /// 判定合格的最大宽度 (像素)。
        /// </summary>
        public int MaxWidth { get; set; } = 1000;

        /// <summary>
        /// 是否打开宽度百分比校验
        /// </summary>
        public bool IsOpenWidthBCheck = false;

        /// <summary>
        /// 判定合格的最小宽度 (像素) 这里用百分率计算。
        /// </summary>
        public double MinWidthB { get; set; } = 0;

        /// <summary>
        /// 判定合格的最大宽度 (像素) 这里用百分率计算。
        /// </summary>
        public double MaxWidthB { get; set; } = 40;

        /// <summary>
        /// 通信触发值
        /// </summary>

        public string TriggerVal { get; set; } = "1";


    }

    public class ColorParam
    {

        /// <summary>
        /// 搜索区域
        /// </summary>
        public RotatedRect DetectedRoi;

        // 颜色模板
        public List<ColorProfile> Profiles = new List<ColorProfile>
        {

        };
    }
}
