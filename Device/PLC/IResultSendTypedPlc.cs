using System;
using System.Threading.Tasks;
using HslCommunication;

namespace TDJS_Vision.Device.PLC
{
    /// <summary>可选的PLC基础数值写入能力；扩展IPlc而不破坏已有设备插件。</summary>
    public interface IResultSendTypedPlc
    {
        /// <summary>按设备原有字节转换规则写入short、ushort、uint或double数组。</summary>
        Task<OperateResult> WriteTypedValuesAsync(string address, Array values);
    }
}
