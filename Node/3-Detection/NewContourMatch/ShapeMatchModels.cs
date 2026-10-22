using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace TDJS_Vision.Node._3_Detection.NewContourMatch
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
    /// <summary>Demo 算法版本：1 为第一版，2 为 OpenCV2。</summary>
    public int AlgorithmVersion { get; set; } = 2;
    /// <summary>X 方向允许的相对缩放偏差。</summary>
    public double ScaleToleranceX { get; set; } = 0.1;
    /// <summary>Y 方向允许的相对缩放偏差。</summary>
    public double ScaleToleranceY { get; set; } = 0.1;
    /// <summary>轮廓提取低阈值。</summary>
    public double LowThreshold { get; set; } = 20;
    /// <summary>忽略长度小于该值的模板轮廓。</summary>
    public double MinimumContourLength { get; set; }
    /// <summary>单次搜索协作式超时，零为不限时。</summary>
    public int TimeoutMilliseconds { get; set; }

    /// <summary>界面兼容字段，新版固定关闭并使用显式高低阈值。</summary>
    public bool AutoContrast { get; set; }

    /// <summary>金字塔层数；0 表示由算法自动决定。</summary>
    public int PyramidLevels { get; set; }

    /// <summary>建模起始角度，单位为度。</summary>
    public double AngleStartDegrees { get; set; } = -30D;

    /// <summary>建模结束角度，单位为度。</summary>
    public double AngleEndDegrees { get; set; } = 30D;

    /// <summary>离散模板角度间隔，单位为度。</summary>
    public double AngleStepDegrees { get; set; } = 1D;

    /// <summary>模型轮廓高阈值；必须大于独立配置的 LowThreshold。</summary>
    public double Contrast { get; set; } = 40D;

    /// <summary>搜索图像的最小梯度强度阈值。</summary>
    public double MinimumContrast { get; set; } = 10D;

    /// <summary>界面兼容字段；新版特征采样由各 Demo 算法自动决定。</summary>
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

    /// <summary>界面兼容字段；新版始终使用 Demo 原生精修。</summary>
    public bool SubPixel { get; set; } = true;

    /// <summary>搜索使用的金字塔层数；0 表示沿用模型层数。</summary>
    public int PyramidLevels { get; set; }

    /// <summary>界面兼容字段；新版不采用旧算法的模式预设。</summary>
    public ShapeMatchMode Mode { get; set; } = ShapeMatchMode.平衡;
}

/// <summary>一个形状匹配结果。</summary>
public sealed class ShapeMatchResult
{
    /// <summary>相对于原模板的 X 缩放比例。</summary>
    public double ScaleX { get; set; } = 1;
    /// <summary>相对于原模板的 Y 缩放比例。</summary>
    public double ScaleY { get; set; } = 1;

    /// <summary>产生本结果的模板标识。</summary>
    public string TemplateId { get; set; }
    /// <summary>产生本结果的模板名称。</summary>
    public string TemplateName { get; set; }
    /// <summary>当前模板建模内容的稳定摘要。</summary>
    internal string TemplateRevision { get; set; }
    /// <summary>模板尺寸是否可唯一识别旧方案中的基准。</summary>
    internal bool UniqueTemplateSize { get; set; }
    /// <summary>同一目标的候选模板，用于在创建基准时记录不同中心间的关系。</summary>
    internal List<ShapeMatchResult> ReferenceMatches { get; set; }
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
