using Newtonsoft.Json;
using System.Collections.Generic;
using TDJS_Vision.Device;
using TDJS_Vision.Device.Modbus;
using TDJS_Vision.Forms.ShapeDraw;

namespace TDJS_Vision.Node._3_Detection.BatteryEar
{
    public class NodeParamBatteryEar : INodeParam
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
        /// 治具物理宽度（毫米）
        /// </summary>
        public double FixtureWidthMM { get; set; }
        /// <summary>
        /// 治具像素宽度
        /// </summary>
        public double FixtureWidthPix { get; set; }
        /// <summary>
        /// 比例尺
        /// </summary>
        public double Scale { get; set; }
        /// <summary>
        /// 分数阈值
        /// </summary>
        public double Score { get; set; }
        /// <summary>
        /// D1偏移量
        /// </summary>
        public double DeltaMM1 { get; set; }
        /// <summary>
        /// D2偏移量
        /// </summary>
        public double DeltaMM2 { get; set; }
        /// <summary>
        /// D3偏移量
        /// </summary>
        public double DeltaMM3 { get; set; }
        /// <summary>
        /// D4偏移量
        /// </summary>
        public double DeltaMM4 { get; set; }
        /// <summary>
        /// ROIs（反序列化用）
        /// </summary>
        [JsonConverter(typeof(ROIListConverter<ROI>))]
        public List<ROI> ROIs { get; set; }
        /// <summary>
        /// 通信设备
        /// </summary>
        public string DeviceName { get; set; }
        /// <summary>
        /// 图像保存路径
        /// </summary>
        public string ImageSavePath { get; set; }
    }
}
