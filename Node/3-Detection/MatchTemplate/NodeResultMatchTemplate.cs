using System.ComponentModel;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._3_Detection.MatchTemplate
{
    public class NodeResultMatchTemplate : INodeResult
    {
        public int RunTime { get; set; }

        [DisplayName("输出图像")]
        public OutputImage OutputImage { get; set; } = new OutputImage();

        [DisplayName("算法结果")]
        public AlgorithmResult Result { get; set; } = new AlgorithmResult();

        [DisplayName("是否OK")]
        public bool IsOk { get; set; }

        [DisplayName("匹配数量")]
        public int MatchCount { get; set; }

        [DisplayName("匹配点X")]
        public double? MatchX { get; set; }

        [DisplayName("匹配点Y")]
        public double? MatchY { get; set; }

        [DisplayName("角度")]
        public double? Angle { get; set; }

        [DisplayName("得分")]
        public double? Score { get; set; }
    }
}
