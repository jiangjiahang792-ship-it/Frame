using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.PointLineDistance
{
    /// <summary>
    /// 点到线距离测量节点参数。
    /// </summary>
    public class NodeParamPointLineDistance : INodeParam
    {
        /// <summary>
        /// 输入图像订阅节点文本。
        /// </summary>
        public string ImageText1 { get; set; }

        /// <summary>
        /// 输入图像订阅结果文本。
        /// </summary>
        public string ImageText2 { get; set; }

        /// <summary>
        /// 数据来源模式，订阅或本地绘制。
        /// </summary>
        public MeasurementDataSourceMode SourceMode { get; set; } = MeasurementDataSourceMode.Draw;

        /// <summary>
        /// 订阅点结果节点文本。
        /// </summary>
        public string PointText1 { get; set; }

        /// <summary>
        /// 订阅点结果属性文本。
        /// </summary>
        public string PointText2 { get; set; }

        /// <summary>
        /// 订阅点的读取角色。
        /// </summary>
        public MeasurementPointRole PointRole { get; set; } = MeasurementPointRole.Auto;

        /// <summary>
        /// 订阅直线结果节点文本。
        /// </summary>
        public string LineText1 { get; set; }

        /// <summary>
        /// 订阅直线结果属性文本。
        /// </summary>
        public string LineText2 { get; set; }

        /// <summary>
        /// 是否启用位置修正跟随。
        /// </summary>
        public bool UsePositionCorrection { get; set; }

        /// <summary>
        /// 位置修正节点订阅文本。
        /// </summary>
        public string CorrectionText1 { get; set; }

        /// <summary>
        /// 位置修正结果订阅文本。
        /// </summary>
        public string CorrectionText2 { get; set; }

        /// <summary>
        /// 几何计算模式，亚像素或像素。
        /// </summary>
        public GeometryMeasureMode MeasureMode { get; set; } = GeometryMeasureMode.SubPixel;

        /// <summary>
        /// 本地绘制点的基准圆心 X。
        /// </summary>
        public float PointCenterX { get; set; } = 240;

        /// <summary>
        /// 本地绘制点的基准圆心 Y。
        /// </summary>
        public float PointCenterY { get; set; } = 220;

        /// <summary>
        /// 本地绘制点的找圆半径。
        /// </summary>
        public float PointRadius { get; set; } = 50;

        /// <summary>
        /// 本地绘制直线基准起点 X。
        /// </summary>
        public float LineStartX { get; set; } = 120;

        /// <summary>
        /// 本地绘制直线基准起点 Y。
        /// </summary>
        public float LineStartY { get; set; } = 360;

        /// <summary>
        /// 本地绘制直线基准终点 X。
        /// </summary>
        public float LineEndX { get; set; } = 460;

        /// <summary>
        /// 本地绘制直线基准终点 Y。
        /// </summary>
        public float LineEndY { get; set; } = 360;

        /// <summary>
        /// 默认卡尺宽度。
        /// </summary>
        public float CaliperWidth { get; set; } = 20;

        /// <summary>
        /// 默认卡尺高度。
        /// </summary>
        public float CaliperHeight { get; set; } = 120;

        /// <summary>
        /// 默认卡尺数量。
        /// </summary>
        public int Count { get; set; } = 20;

        /// <summary>
        /// 默认边缘强度阈值。
        /// </summary>
        public int EdgeStrength { get; set; } = 20;

        /// <summary>
        /// 默认边缘极性。
        /// </summary>
        public CaliperEdgePolarity Polarity { get; set; } = CaliperEdgePolarity.Both;

        /// <summary>
        /// 默认边缘查找模式。
        /// </summary>
        public CaliperEdgeFindMode FindMode { get; set; } = CaliperEdgeFindMode.Best;

        /// <summary>
        /// 默认查找方向。
        /// </summary>
        public int Direction { get; set; }

        /// <summary>
        /// 默认平滑核大小。
        /// </summary>
        public int BlurSize { get; set; } = 3;

        /// <summary>
        /// 点圆卡尺宽度。
        /// </summary>
        public float? PointCaliperWidth { get; set; }

        /// <summary>
        /// 点圆卡尺高度。
        /// </summary>
        public float? PointCaliperHeight { get; set; }

        /// <summary>
        /// 点圆卡尺数量。
        /// </summary>
        public int? PointCount { get; set; }

        /// <summary>
        /// 点圆边缘强度。
        /// </summary>
        public int? PointEdgeStrength { get; set; }

        /// <summary>
        /// 点圆边缘极性。
        /// </summary>
        public CaliperEdgePolarity? PointPolarity { get; set; }

        /// <summary>
        /// 点圆查找模式。
        /// </summary>
        public CaliperEdgeFindMode? PointFindMode { get; set; }

        /// <summary>
        /// 点圆查找方向。
        /// </summary>
        public int? PointDirection { get; set; }

        /// <summary>
        /// 点圆平滑核。
        /// </summary>
        public int? PointBlurSize { get; set; }

        /// <summary>
        /// 线卡尺宽度。
        /// </summary>
        public float? LineCaliperWidth { get; set; }

        /// <summary>
        /// 线卡尺高度。
        /// </summary>
        public float? LineCaliperHeight { get; set; }

        /// <summary>
        /// 线卡尺数量。
        /// </summary>
        public int? LineCount { get; set; }

        /// <summary>
        /// 线边缘强度。
        /// </summary>
        public int? LineEdgeStrength { get; set; }

        /// <summary>
        /// 线边缘极性。
        /// </summary>
        public CaliperEdgePolarity? LinePolarity { get; set; }

        /// <summary>
        /// 线查找模式。
        /// </summary>
        public CaliperEdgeFindMode? LineFindMode { get; set; }

        /// <summary>
        /// 线查找方向。
        /// </summary>
        public int? LineDirection { get; set; }

        /// <summary>
        /// 线平滑核。
        /// </summary>
        public int? LineBlurSize { get; set; }

        /// <summary>
        /// 获取指定对象的卡尺宽度。
        /// </summary>
        public float GetCaliperWidth(bool point) => (point ? PointCaliperWidth : LineCaliperWidth) ?? CaliperWidth;

        /// <summary>
        /// 获取指定对象的卡尺高度。
        /// </summary>
        public float GetCaliperHeight(bool point) => (point ? PointCaliperHeight : LineCaliperHeight) ?? CaliperHeight;

        /// <summary>
        /// 获取指定对象的卡尺数量。
        /// </summary>
        public int GetCount(bool point) => (point ? PointCount : LineCount) ?? Count;

        /// <summary>
        /// 获取指定对象的边缘强度。
        /// </summary>
        public int GetEdgeStrength(bool point) => (point ? PointEdgeStrength : LineEdgeStrength) ?? EdgeStrength;

        /// <summary>
        /// 获取指定对象的边缘极性。
        /// </summary>
        public CaliperEdgePolarity GetPolarity(bool point) => (point ? PointPolarity : LinePolarity) ?? Polarity;

        /// <summary>
        /// 获取指定对象的查找模式。
        /// </summary>
        public CaliperEdgeFindMode GetFindMode(bool point) => (point ? PointFindMode : LineFindMode) ?? FindMode;

        /// <summary>
        /// 获取指定对象的查找方向。
        /// </summary>
        public int GetDirection(bool point) => (point ? PointDirection : LineDirection) ?? Direction;

        /// <summary>
        /// 获取指定对象的平滑核大小。
        /// </summary>
        public int GetBlurSize(bool point) => (point ? PointBlurSize : LineBlurSize) ?? BlurSize;
    }
}
