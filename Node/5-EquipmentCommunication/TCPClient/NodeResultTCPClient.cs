using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TDJS_Vision.Node._5_EquipmentCommunication.TcpClient
{
    internal class NodeResultTCPClient : INodeResult
    {
        public int RunTime { get; set; }
        [DisplayName("请求结果")]
        public object ResponseData { get; set; }
    }
}
