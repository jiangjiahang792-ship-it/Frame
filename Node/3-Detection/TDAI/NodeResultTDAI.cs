using System.Collections.Generic;
using System.Drawing;
using TDJS_Vision.Forms.ResultView;
using System.ComponentModel;
using OpenCvSharp;
using System.Linq;

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
        public List<DetectItemResult> ResultList { get; set; } = new List<DetectItemResult>();

        [DisplayName("是否全部OK")]
        public bool IsAllOk { get { return ResultList.TrueForAll(res => res.IsOk); } }
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
    /// 检测项结果类
    /// </summary>
    internal class DetectItemResult 
    {
        /// <summary>
        /// 检测项名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 检测项的结果列表
        /// </summary>
        public List<SingleDetectItemResult> Results { get; set; } = new List<SingleDetectItemResult>();
        /// <summary>
        /// 返回 Results 中所有 Value 值的拼接字符串，用英文逗号分隔(这个属性只是为了方便在数据结果窗口直接显示)
        /// </summary>
        public string ResultsStr
        {
            get
            {
                if (Results == null || Results.Count == 0)
                    return string.Empty;

                // 使用 LINQ Select 提取每个 Result 的 Value，并用 string.Join 拼接
                return string.Join(",", Results.Select(r => r.Value));
            }
        }
        /// <summary>
        /// 检测项结果是否OK
        /// </summary>
        public bool IsOk { get { return Results.TrueForAll(item => item.IsOk); } }
    }
    /// <summary>
    /// 单个检测项结果类
    /// </summary>
    internal class SingleDetectItemResult
    {
        /// <summary>
        /// 单个检测项结果是否OK(决定结果矩形框的颜色)
        /// </summary>
        public bool IsOk { get; set; }
        /// <summary>
        /// 单个检测项的结果值
        /// </summary>
        public string Value { get; set; }
        /// <summary>
        /// 单个检测项涉及的矩形框列表（用于绘制）
        /// </summary>
        public List<Rect> Rects { get; set; } = new List<Rect> { };
    }
}
