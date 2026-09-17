using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace TDJS_Vision.Node._3_Detection.ContourMatch
{

/// <summary>模型梯度极性的处理方式。</summary>
public enum ShapeMetric
{
    /// <summary>区分由暗到亮和由亮到暗的边缘。</summary>
    使用极性,

    /// <summary>将方向相反的边缘视作同一种形状。</summary>
    忽略极性
}

/// <summary>搜索速度与精度配置。</summary>
public enum ShapeMatchMode
{
    /// <summary>减少搜索预算；大组合模板按亚像素开关执行稀疏精修，小模板保留快速离散搜索。</summary>
    快速 = 0,

    /// <summary>执行轮廓亚像素精修，但不执行灰度 ECC。</summary>
    平衡 = 1,

    /// <summary>在满足条件时执行完整的轮廓与灰度精修。</summary>
    高精度 = 2
}

/// <summary>创建形状模型所需的参数。</summary>
public sealed class CreateModelOptions
{
    /// <summary>是否按 ROI 自动估算模型 Canny 阈值；关闭时采用 Contrast。</summary>
    public bool AutoContrast { get; set; }

    /// <summary>金字塔层数；0 表示由算法自动决定。</summary>
    public int PyramidLevels { get; set; }

    /// <summary>建模起始角度，单位为度。</summary>
    public double AngleStartDegrees { get; set; } = -30D;

    /// <summary>建模结束角度，单位为度。</summary>
    public double AngleEndDegrees { get; set; } = 30D;

    /// <summary>离散模板角度间隔，单位为度。</summary>
    public double AngleStepDegrees { get; set; } = 1D;

    /// <summary>Canny 模型边缘的高阈值；低阈值自动取一半，连接强边缘的弱边缘也会保留。</summary>
    public double Contrast { get; set; } = 20D;

    /// <summary>搜索图像的最小梯度强度阈值。</summary>
    public double MinimumContrast { get; set; } = 10D;

    /// <summary>原图层期望特征数；0 表示自动选择。</summary>
    public int FeatureCount { get; set; } = 300;

    /// <summary>梯度方向是否忽略极性。</summary>
    public ShapeMetric Metric { get; set; } = ShapeMetric.使用极性;
}

/// <summary>搜索形状模型所需的参数。</summary>
public sealed class FindOptions
{
    /// <summary>搜索起始角度，单位为度。</summary>
    public double AngleStartDegrees { get; set; } = -30D;

    /// <summary>搜索结束角度，单位为度。</summary>
    public double AngleEndDegrees { get; set; } = 30D;

    /// <summary>最终结果必须达到的归一化分数。</summary>
    public double MinimumScore { get; set; } = 0.65D;

    /// <summary>最多返回的目标数量。</summary>
    public int MaximumMatches { get; set; } = 5;

    /// <summary>允许两个结果之间存在的最大模板覆盖比例。</summary>
    public double MaximumOverlap { get; set; } = 0.30D;

    /// <summary>是否启用亚像素位置和角度精修。</summary>
    public bool SubPixel { get; set; } = true;

    /// <summary>搜索使用的金字塔层数；0 表示沿用模型层数。</summary>
    public int PyramidLevels { get; set; }

    /// <summary>速度与精度模式。</summary>
    public ShapeMatchMode Mode { get; set; } = ShapeMatchMode.平衡;
}

/// <summary>一个形状匹配结果。</summary>
public sealed class ShapeMatchResult
{
    /// <summary>产生本结果的模板标识。</summary>
    public string TemplateId { get; set; }
    /// <summary>产生本结果的模板名称。</summary>
    public string TemplateName { get; set; }
    /// <summary>该目标对应的局部模板轮廓，运行缓存所有，不跨方案序列化。</summary>
    internal IReadOnlyList<PointF[]> ModelContours { get; set; }

    /// <summary>中心横坐标。</summary>
    public double CenterX { get; set; }

    /// <summary>中心纵坐标。</summary>
    public double CenterY { get; set; }

    /// <summary>旋转角度，单位为度。</summary>
    public double AngleDegrees { get; set; }

    /// <summary>归一化匹配分数。</summary>
    public double Score { get; set; }

    /// <summary>模板宽度。</summary>
    public double Width { get; set; }

    /// <summary>模板高度。</summary>
    public double Height { get; set; }
}

}
