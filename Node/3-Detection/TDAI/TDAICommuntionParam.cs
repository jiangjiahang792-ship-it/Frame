using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TDJS_Vision.Node._3_Detection.TDAI
{
    /// <summary>
    /// AI通信配置参数
    /// </summary>
    public class TDAICommuntionParam
    {
        /// <summary>
        /// 触发值
        /// </summary>
        private string _triggerVal;

        /// <summary>
        /// 检测项名称
        /// </summary>
        private string _detectionName;

        public string TriggerVal { get => _triggerVal; set => _triggerVal = value; }
        public string DetectionName { get => _detectionName; set => _detectionName = value; }
    }
}
