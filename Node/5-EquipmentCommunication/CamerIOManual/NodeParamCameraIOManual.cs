using TDJS_Vision.Device.Camera;
using Newtonsoft.Json;

namespace TDJS_Vision.Node._5_EquipmentCommunication.CamerIOImprovement
{
    public class NodeParamCameraIOManual : INodeParam
    {
        /// <summary>
        /// 相机
        /// </summary>
        [JsonIgnore]
        public ICamera Camera { get; set; }

        /// <summary>
        /// 相机名称
        /// </summary>
        public string CameraName { get; set; }

        /// <summary>
        /// 线路
        /// </summary>
        public string LineSelector { get; set; }

        /// <summary>
        /// 信号量  
        /// </summary>
        public bool Semaphore { get; set; } = true;
         
        /// <summary>
        /// 线路模式
        /// </summary>
        public string LineMode { get; set; }
        
    }
}
