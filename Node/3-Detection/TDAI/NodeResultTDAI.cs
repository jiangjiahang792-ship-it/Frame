using System.Collections.Generic;
using System.Drawing;
using System.ComponentModel;
using OpenCvSharp;
using RotatedRect = TDJS_Vision.Node._3_Detection.TDAI.Yolo8.RotatedRect;
using System.Linq;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

namespace TDJS_Vision.Node._3_Detection.TDAI
{
    /// <summary>
    /// AI检测节点运行结果，负责同时发布完整算法结果和多条件可直接判断的基础值。
    /// </summary>
    public class NodeResultTDAI : INodeResult, IJudgmentResult
    {
        /// <summary>
        /// 多条件节点回写的最终判定状态；为空时跟随算法原始 OK/NG。
        /// </summary>
        private bool? _judgeOkOverride;

        /// <summary>
        /// 获取或设置节点运行耗时，单位毫秒。
        /// </summary>
        public int RunTime { get; set; }

        /// <summary>
        /// 获取或设置完整 AI 算法结果，供结果绘制、汇总、通信发送等节点订阅。
        /// </summary>
        [SubscriptionOutput(SubscriptionDataCategory.AlgorithmResult)]
        [DisplayName("AI输出结果")]
        public AlgorithmResult AlgorithmResult { get; set; } = new AlgorithmResult();

        /// <summary>
        /// 获取 AI 原始检测项是否全部 OK，隐藏保留给旧方案兼容。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("是否OK")]
        public bool IsOk
        {
            get { return AlgorithmResult == null || AlgorithmResult.IsAllOk; }
        }

        /// <summary>
        /// 获取或设置多条件回写后的最终判定状态，供多条件、绘制和存图节点统一读取。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("判定OK")]
        public bool JudgeOk
        {
            get { return _judgeOkOverride ?? IsOk; }
            set { _judgeOkOverride = value; }
        }

        /// <summary>
        /// 获取当前 AI 结果中检测项名称的数量。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("检测项数量")]
        public int DetectItemCount
        {
            get { return AlgorithmResult == null || AlgorithmResult.DetectResults == null ? 0 : AlgorithmResult.DetectResults.Count; }
        }

        /// <summary>
        /// 获取当前 AI 结果中所有检测明细的总数量。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("检测明细数量")]
        public int DetectResultCount
        {
            get
            {
                return AlgorithmResult == null || AlgorithmResult.DetectResults == null
                    ? 0
                    : AlgorithmResult.DetectResults.Values.Sum(items => items == null ? 0 : items.Count);
            }
        }

        /// <summary>
        /// 获取当前 AI 结果的紧凑文本摘要，便于多条件用文本包含方式判断。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("检测结果文本")]
        public string DetectResultText
        {
            get { return AlgorithmResult == null ? string.Empty : AlgorithmResult.BuildDetectResultText(); }
        }

        /// <summary>
        /// 重置多条件回写状态，确保每次新运行先跟随本次算法原始结果。
        /// </summary>
        public void ResetJudgeOk()
        {
            _judgeOkOverride = null;
        }
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
            get
            {
                return _isAllOkOverride ??
                    (DetectResults == null ||
                     DetectResults.Values
                         .Where(list => list != null)
                         .SelectMany(list => list)
                         .Where(result => result != null)
                         .All(result => result.IsOk));
            }
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

        /// <summary>
        /// 构建检测结果文本摘要，格式为“检测项:OK/NG=值”，用于文本订阅和诊断显示。
        /// </summary>
        /// <returns>检测结果文本摘要。</returns>
        public string BuildDetectResultText()
        {
            if (DetectResults == null || DetectResults.Count == 0)
                return string.Empty;

            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, List<SingleDetectResult>> pair in DetectResults)
            {
                string itemName = string.IsNullOrWhiteSpace(pair.Key) ? "未命名检测项" : pair.Key;
                List<SingleDetectResult> itemResults = pair.Value;
                if (itemResults == null || itemResults.Count == 0)
                {
                    parts.Add(itemName + ":空");
                    continue;
                }

                for (int index = 0; index < itemResults.Count; index++)
                {
                    SingleDetectResult result = itemResults[index];
                    if (result == null)
                    {
                        parts.Add(itemName + "[" + (index + 1) + "]:空");
                        continue;
                    }

                    string resultName = string.IsNullOrWhiteSpace(result.Name) ? itemName : result.Name;
                    string valueText = string.IsNullOrWhiteSpace(result.Value) ? string.Empty : "=" + result.Value;
                    parts.Add(resultName + "[" + (index + 1) + "]:" + (result.IsOk ? "OK" : "NG") + valueText);
                }
            }

            return string.Join("; ", parts);
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
        /// <summary>是否绘制中心十字，默认兼容既有节点；纯中线结果可关闭。</summary>
        public bool ShowCenterCross { get; set; } = true;
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
