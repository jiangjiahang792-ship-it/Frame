using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using System.Collections.Generic;
using TDJS_Vision.Device.Camera;

namespace TDJS_Vision.Node._1_Acquisition.ImageSource
{
    public class NodeParamImageSoucre : INodeParam
    {
        /// <summary>
        /// 图像源
        /// </summary>
        public string ImageSource { get; set; }

        #region 本地图像参数
        /// <summary>
        /// 文件或文件夹路径
        /// </summary>
        public string PathText { get; set; }
        /// <summary>
        /// 图片路径
        /// </summary>
        public string ImagePath { get; set; }
        /// <summary>
        /// 触发遍历下一张图片
        /// </summary>
        public bool IsAutoLoop { get; set; }
        /// <summary>
        /// 图片路径列表
        /// </summary>
        [JsonIgnore]
        public List<string> ImagePaths { get; set; } = new List<string>();
        #endregion

        #region 相机参数

        /// <summary>
        /// 使用的相机
        /// </summary>
        [JsonIgnore]
        public ICamera Camera { get; set; }
        /// <summary>
        /// 使用的相机名称
        /// </summary>
        public string CameraName { get; set; }
        /// <summary>
        /// 触发模式；新节点默认开启，旧方案显式保存的关闭模式仍保留。
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public TriggerModel TriggerModel { get; set; } = TriggerModel.On;
        /// <summary>
        /// 触发方式
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public TriggerSource TriggerSource { get; set; }
        /// <summary>
        /// 触发沿
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public TriggerEdge TriggerEdge { get; set; }
        /// <summary>
        /// 触发延迟
        /// </summary>
        public int TriggerDelay { get; set; }
        /// <summary>
        /// 曝光时间
        /// </summary>
        public double ExposureTime { get; set; }
        /// <summary>
        /// 增益
        /// </summary>
        public double Gain { get; set; }
        /// <summary>
        /// 采图超时时间
        /// </summary>
        public uint TimeOut { get; set; }
        /// <summary>
        /// 是否每次设置参数
        /// </summary>
        public bool IsEveryTime { get; set; }
        

        #endregion

        #region 共享变量参数

        /// <summary>
        /// 读取图像时使用的共享变量名称。
        /// </summary>
        public string SharedVariableName { get; set; }

        #endregion
    }
}
