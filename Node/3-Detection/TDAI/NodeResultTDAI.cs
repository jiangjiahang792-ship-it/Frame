using System.Collections.Generic;
using System.Drawing;
using System.ComponentModel;
using OpenCvSharp;
using RotatedRect = TDJS_Vision.Node._3_Detection.TDAI.Yolo8.RotatedRect;
using System.Linq;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

namespace TDJS_Vision.Node._3_Detection.TDAI
{
    public class NodeResultTDAI : INodeResult
    {
        public int RunTime { get; set; }

        [DisplayName("AI输出结果")]
        public AlgorithmResult AlgorithmResult { get; set; } = new AlgorithmResult();
    }

    /// <summary>
    /// AI检出结果
    /// </summary>
    public class AlgorithmResult
    {
        private bool? _isAllOkOverride;

        /// <summary>
        /// 所有检测项的OK/NG
        /// </summary>
        public Dictionary<string, List<SingleDetectResult>> DetectResults { get; set; } = new Dictionary<string, List<SingleDetectResult>>();
        /// <summary>
        /// DetectResults所有检测项都OK
        /// </summary>
        public bool IsAllOk
        {
            get { return _isAllOkOverride ?? DetectResults.Values.SelectMany(list => list).All(result => result.IsOk); }
            set { _isAllOkOverride = value; }
        }
        /// <summary>
        /// 带颜色的当前结果检测框
        /// </summary>
        public List<ColorRotatedRect> Rects { get; set; } = new List<ColorRotatedRect>();
        /// <summary>
        /// 带颜色的缓存上次NG结果的检测框
        /// </summary>
        public Dictionary<string, List<ColorRotatedRect>> RectsNgMap { get; set; } = new Dictionary<string, List<ColorRotatedRect>>();
        /// <summary>
        /// 带颜色的文本
        /// </summary>
        public List<ColorText> Texts { get; set; } = new List<ColorText>();
        /// <summary>
        /// 带颜色的线段
        /// </summary>
        public List<ColorLine> Lines { get; set; } = new List<ColorLine>();
        /// <summary>
        /// 带颜色的圆
        /// </summary>
        public List<ColorCircle> Circles { get; set; } = new List<ColorCircle>();
        /// <summary>
        /// 带颜色的圆弧，供角度、方向等局部标注复用。
        /// </summary>
        public List<ColorArc> Arcs { get; set; } = new List<ColorArc>();
        /// <summary>
        /// 带颜色的椭圆
        /// </summary>
        public List<ColorEllipse> Ellipses { get; set; } = new List<ColorEllipse>();
        /// <summary>
        /// 带颜色的轮廓点集
        /// </summary>
        public List<ColorContour> Contours { get; set; } = new List<ColorContour>();

        public void Clear()
        {
            DetectResults.Clear();
            Rects.Clear();
            RectsNgMap.Clear();
            Texts.Clear();
            Lines.Clear();
            Circles.Clear();
            Arcs.Clear();
            Ellipses.Clear();
            Contours.Clear();
            _isAllOkOverride = null;
        }
    }

    /// <summary>
    /// 单个检测项结果
    /// </summary>
    public class SingleDetectResult
    {
        /// <summary>
        /// 检测值
        /// </summary>
        public string Value { get; set; }
        /// <summary>
        /// 检测项是否OK
        /// </summary>
        public bool IsOk { get; set; }
        /// <summary>
        /// 检测项名称
        /// </summary>
        public string Name { get; set; }
        public SingleDetectResult() { }

        public SingleDetectResult(string name, string value, bool isOk)
        {
            Name = name;
            Value = value;
            IsOk = isOk;
        }
    }
    /// <summary>
    /// 带有颜色标记的旋转矩形框
    /// </summary>
    public class ColorRotatedRect
    {
        private Color? _colorOverride;

        public ColorRotatedRect(RotatedRect rotatedRect)
        {
            this.RotatedRect = rotatedRect;
        }
        public ColorRotatedRect(ObbResult rotatedRect)
        {
            this.RotatedRect = new RotatedRect(rotatedRect);
        }
        public ColorRotatedRect(Rect Rect)
        {
            this.RotatedRect = new RotatedRect(Rect);
        }
        public ColorRotatedRect(Rect Rect, Color color)
        {
            this.RotatedRect = new RotatedRect(Rect);
            this.Color = color;
        }
        public ColorRotatedRect(float centerX, float centerY, float width, float height, float angle, Color color)
        {
            RotatedRect rect = new RotatedRect();
            rect.center.x = centerX;
            rect.center.y = centerY;
            rect.size.width = width;
            rect.size.height = height;
            rect.angle = angle;
            this.RotatedRect = rect;
            this.Color = color;
        }
        public ColorRotatedRect(int x, int y, int width, int height)
        {
            RotatedRect rect = new RotatedRect();
            rect.center.x = x + width / 2.0f;
            rect.center.y = y + height / 2.0f;
            rect.size.width = width;
            rect.size.height = height;
            rect.angle = 0.0f;
            this.RotatedRect = rect;
        }

        private List<bool> _flags = new List<bool>();
        /// <summary>
        /// 旋转矩形
        /// </summary>
        public RotatedRect RotatedRect {  get; set; }
        /// <summary>
        /// 显示叠加层线宽。
        /// </summary>
        public float LineWidth { get; set; } = 2F;
        /// <summary>
        /// 绘制的颜色
        /// </summary>
        public Color Color
        {
            get
            {
                if (_colorOverride.HasValue)
                    return _colorOverride.Value;
                return _flags.TrueForAll(item => item == true) ? Color.Green : Color.Red;
            }
            set { _colorOverride = value; }
        }
        /// <summary>
        /// 添加的条件决定绘制的颜色
        /// </summary>
        /// <param name="flag"></param>
        public void AddFlag(bool flag) 
        {
            _flags.Add(flag);
        }
    }

    /// <summary>
    /// 带颜色的文本
    /// </summary>
    public enum DisplayTextPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    /// <summary>
    /// 四角文本定位使用的坐标系。
    /// </summary>
    public enum DisplayTextCoordinateMode
    {
        /// <summary>
        /// 按当前图像显示区域的四角定位，缩放或平移图像时跟随图像移动。
        /// </summary>
        Image,

        /// <summary>
        /// 按显示控件客户区的四角定位，缩放或平移图像时保持在控件固定位置。
        /// </summary>
        Control
    }

    /// <summary>
    /// 带颜色的文本显示项。
    /// </summary>
    public class ColorText
    {
        /// <summary>
        /// 显示文本。
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 文本颜色。
        /// </summary>
        public Color Color { get; set; }

        /// <summary>
        /// 文本字号。
        /// </summary>
        public int FontSize { get; set; } = 11;

        /// <summary>
        /// 四角定位位置，UseImagePosition 为 false 时生效。
        /// </summary>
        public DisplayTextPosition Position { get; set; } = DisplayTextPosition.TopLeft;

        /// <summary>
        /// 四角定位边距。
        /// </summary>
        public int Margin { get; set; } = 10;

        /// <summary>
        /// 文本块标题。
        /// </summary>
        public string Title { get; set; } = "Result";

        /// <summary>
        /// 四角定位使用的坐标系；绝对图像坐标模式仍由 UseImagePosition 控制。
        /// </summary>
        public DisplayTextCoordinateMode CoordinateMode { get; set; } = DisplayTextCoordinateMode.Image;

        /// <summary>
        /// 是否按图像坐标显示文本；为 false 时按四角信息栏显示。
        /// </summary>
        public bool UseImagePosition { get; set; }

        /// <summary>
        /// 文本左上角图像坐标，UseImagePosition 为 true 时生效。
        /// </summary>
        public PointF ImagePosition { get; set; }

        public ColorText(string text, Color color)
        {
            Text = text;
            Color = color;
        }
    }

    /// <summary>
    /// 带颜色的线段
    /// </summary>
    public class ColorLine
    {
        public PointF P1 { get; set; }
        public PointF P2 { get; set; }
        public Color Color { get; set; }
        public float LineWidth { get; set; } = 2F;

        public ColorLine(PointF p1, PointF p2, Color color)
        {
            P1 = p1;
            P2 = p2;
            Color = color;
        }

        public ColorLine(LineSegmentPoint line, Color color)
        {
            P1 = new PointF(line.P1.X, line.P1.Y);
            P2 = new PointF(line.P2.X, line.P2.Y);
            Color = color;
        }
    }

    /// <summary>
    /// 带颜色的圆
    /// </summary>
    public class ColorCircle
    {
        public PointF Center { get; set; }
        public float Radius { get; set; }
        public Color Color { get; set; }
        public float LineWidth { get; set; } = 2F;

        public ColorCircle(PointF center, int radius, Color color)
        {
            Center = center;
            Radius = radius;
            Color = color;
        }
        public ColorCircle(CircleSegment circle, Color color)
        {
            Center = new PointF(circle.Center.X, circle.Center.Y);
            Radius = circle.Radius;
            Color = color;
        }
    }

    /// <summary>
    /// 带颜色的圆弧。
    /// </summary>
    public class ColorArc
    {
        /// <summary>
        /// 圆弧圆心。
        /// </summary>
        public PointF Center { get; set; }
        /// <summary>
        /// 圆弧半径。
        /// </summary>
        public float Radius { get; set; }
        /// <summary>
        /// 起始角度，单位为度，遵循图像坐标系。
        /// </summary>
        public float StartAngle { get; set; }
        /// <summary>
        /// 扫描角度，单位为度，正值沿图像坐标顺时针方向。
        /// </summary>
        public float SweepAngle { get; set; }
        /// <summary>
        /// 绘制颜色。
        /// </summary>
        public Color Color { get; set; }
        /// <summary>
        /// 显示叠加层线宽。
        /// </summary>
        public float LineWidth { get; set; } = 2F;

        /// <summary>
        /// 创建带颜色的圆弧结果。
        /// </summary>
        public ColorArc(PointF center, float radius, float startAngle, float sweepAngle, Color color)
        {
            Center = center;
            Radius = radius;
            StartAngle = startAngle;
            SweepAngle = sweepAngle;
            Color = color;
        }
    }

    /// <summary>
    /// 带颜色的椭圆
    /// </summary>
    public class ColorEllipse
    {
        public PointF Center { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float Angle { get; set; }
        public Color Color { get; set; }
        public float LineWidth { get; set; } = 2F;

        public ColorEllipse(PointF center, float width, float height, float angle, Color color)
        {
            Center = center;
            Width = width;
            Height = height;
            Angle = angle;
            Color = color;
        }
    }

    /// <summary>
    /// 带颜色的轮廓点集
    /// </summary>
    public class ColorContour
    {
        public List<PointF> Points { get; set; }
        public Color Color { get; set; }
        public float LineWidth { get; set; } = 2F;

        public ColorContour(List<PointF> points, Color color)
        {
            Points = points ?? new List<PointF>();
            Color = color;
        }
    }
}
