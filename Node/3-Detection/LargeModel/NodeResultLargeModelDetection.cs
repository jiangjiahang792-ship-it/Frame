using System.ComponentModel;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._3_Detection.LargeModel
{
    /// <summary>
    /// 大模型调用节点运行结果。
    /// </summary>
    public class NodeResultLargeModelDetection : INodeResult
    {
        /// <summary>
        /// 节点运行耗时，单位毫秒。
        /// </summary>
        public int RunTime { get; set; }

        /// <summary>
        /// 可供 ROI 结果绘制、条件判断、汇总节点订阅的算法结果。
        /// </summary>
        [DisplayName("大模型输出结果")]
        public AlgorithmResult AlgorithmResult { get; set; } = new AlgorithmResult();

        /// <summary>
        /// 当前推理是否判定 OK。
        /// </summary>
        [DisplayName("判定OK")]
        public bool IsOk { get; set; } = true;

        /// <summary>
        /// native 返回的异常分数。
        /// </summary>
        [DisplayName("异常分数")]
        public float Score { get; set; }

        /// <summary>
        /// 当前异常区域数量。
        /// </summary>
        [DisplayName("异常框数量")]
        public int BoxCount { get; set; }
    }
}

