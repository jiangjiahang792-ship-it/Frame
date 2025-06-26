using System.Collections.Generic;
using System.Drawing;
using TDJS_Vision.Forms.ResultView;
using System.ComponentModel;
using System.Linq;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

namespace TDJS_Vision.Node._3_Detection.TDAI
{
    internal class NodeResultTDAI : INodeResult
    {
        [DisplayName("运行状态")]
        public NodeStatus Status { get; set; }

        [DisplayName("节点耗时")]
        public long RunTime { get; set; }

        [DisplayName("运行状态码")]
        public NodeRunStatusCode RunStatusCode { get; set; }

        [DisplayName("AI检测项配置")]
        public AIInputInfo AIInputInfo { get; set; }

        [DisplayName("AI输出结果")]
        public AiResult AiResult { get; set; } = new AiResult();
    }

    /// <summary>
    /// AI/传统算法检出结果
    /// </summary>
    internal class ResultViewData
    {
        public bool IsAllOk { get { return SingleRowDataList.TrueForAll(sr => sr.IsOk); } }
        public List<SingleResultViewData> SingleRowDataList = new List<SingleResultViewData>();
    }
    /// <summary>
    /// AI检出结果
    /// </summary>
    internal class AiResult
    {
        /// <summary>
        /// 所有检测项的OK/NG
        /// </summary>
        public Dictionary<string, List<SingleDetectResult>> DetectResults { get; set; } = new Dictionary<string, List<SingleDetectResult>>();
        /// <summary>
        /// 带颜色的检测框
        /// </summary>
        public List<ColorRotatedRect> Rects { get; set; } = new List<ColorRotatedRect>();
        /// <summary>
        /// 带颜色的文本
        /// </summary>
        public List<ColorText> Texts { get; set; } = new List<ColorText>();
        /// <summary>
        /// 带颜色的线段
        /// </summary>
        public List<ColorLine> Lines { get; set; } = new List<ColorLine>();

        /// <summary>
        /// 清空结果
        /// </summary>
        public void Clear ()
        {
            DetectResults.Clear();
            Rects.Clear(); 
            Texts.Clear(); 
            Lines.Clear();
        }
    }

    /// <summary>
    /// 单个检测项结果
    /// </summary>
    internal class SingleDetectResult
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
    }
    /// <summary>
    /// 带有颜色标记的旋转矩形框
    /// </summary>
    internal class ColorRotatedRect
    {
        public ColorRotatedRect(RotatedRect rotatedRect)
        {
            this.RotatedRect = rotatedRect;
        }

        private List<bool> _flags = new List<bool>();
        /// <summary>
        /// 旋转矩形
        /// </summary>
        public RotatedRect RotatedRect {  get; set; }
        /// <summary>
        /// 绘制的颜色
        /// </summary>
        public Color Color
        {
            get
            {
                return _flags.TrueForAll(item => item == true) ? Color.Green : Color.Red;
            }
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
    internal class ColorText
    {
        public string Text { get; set; }
        public Color Color { get; set; }
        public ColorText(string text, Color color)
        {
            Text = text;
            Color = color;
        }
    }

    /// <summary>
    /// 带颜色的线段
    /// </summary>
    internal class ColorLine
    {
        public PointF P1 { get; set; }
        public PointF P2 { get; set; }
        public Color Color { get; set; }
    }

}
