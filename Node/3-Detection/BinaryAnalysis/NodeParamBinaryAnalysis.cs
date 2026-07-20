using Newtonsoft.Json;
using OpenCvSharp;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Documents;
using TDJS_Vision.Device;

namespace TDJS_Vision.Node._3_Detection.ColorDiscern
{
    public class NodeParamBinaryAnalysis : INodeParam
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
        /// 运行参数
        /// </summary>
        public List<BinaryAnlysisParams> BinaryAnlysisParams = new List<BinaryAnlysisParams>();
    }


    // ------------   运行参数  ------------------
    public class BinaryAnlysisParams {

        /// <summary>
        /// 名称
        /// </summary>
        public string Name = "Auto_1";

        /// <summary>
        /// 搜索区域
        /// </summary>
        public BinaryRegion Region;

        /// <summary>
        /// 二值化阈值最小值
        /// </summary>
        public int ThreshMin { get; set; } = 0;

        /// <summary>
        /// 二值化阈值最大值
        /// </summary>
        public int ThreshMax { get; set; } = 0;

        /// <summary>
        /// 形态学模式
        /// </summary>
        public string MorphStr { get; set; } = "无";
        /// <summary>
        /// 结构核大小
        /// </summary>
        public int KernelSize { get; set; } = 0;

        // 判定参数  面积
        public bool EnableArea { get; set; } = false;
        public double MinArea { get; set; } = 0;
        public double MaxArea { get; set; } = 0;

        // 判定参数  高度
        public bool EnableHeight { get; set; } = false;
        public double MinHeight { get; set; } = 0;
        public double MaxHeight { get; set; } = 0;

        // 判定参数  宽度
        public bool EnableWidth { get; set; } = false;
        public double MinWidth { get; set; } = 0;
        public double MaxWidth { get; set; } = 0;
    }

    public struct BinaryRegion {
        public double Row, Column, Phi, Length1, Length2;
    }
}
