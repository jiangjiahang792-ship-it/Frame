using System.Text;
using System.Threading.Tasks;
using HslCommunication;

namespace TDJS_Vision.Device.PLC
{
    /// <summary>字符串在每个16位寄存器内的字节顺序，默认值兼容已有方案。</summary>
    public enum PlcStringByteOrder
    {
        /// <summary>沿用设备原有字符串读取方式和长度单位。</summary>
        DeviceDefault = 0,
        /// <summary>每个寄存器先读取高8位，再读取低8位。</summary>
        HighByteFirst = 1,
        /// <summary>每个寄存器先读取低8位，再读取高8位。</summary>
        LowByteFirst = 2
    }

    /// <summary>基于统一PLC接口按节点指定顺序读取字符串，不修改共享连接的转换配置。</summary>
    public static class PlcStringReader
    {
        /// <summary>默认沿用设备读取；显式高低位以字为长度，一次读取后按寄存器高低8位解码ASCII。</summary>
        public static async Task<OperateResult<string>> ReadStringAsync(this IPlc plc, string address,
            ushort length, PlcStringByteOrder byteOrder)
        {
            if (byteOrder == PlcStringByteOrder.DeviceDefault)
                return await plc.ReadStringAsync(address, length).ConfigureAwait(false);
            if (byteOrder != PlcStringByteOrder.HighByteFirst && byteOrder != PlcStringByteOrder.LowByteFirst)
                return new OperateResult<string>("不支持的字符串字节顺序！");
            if (length == 0)
                return new OperateResult<string>("字符串读取长度必须大于0！");

            // 通过数值字接口获取高低位，屏蔽不同通信协议的报文字节顺序，且无需变更设备全局状态。
            var result = await plc.ReadInt16Async(address, length).ConfigureAwait(false);
            if (!result.IsSuccess) return OperateResult.CreateFailedResult<string>(result);
            if (result.Content == null || result.Content.Length != length)
                return new OperateResult<string>("字符串读取返回的寄存器数量与请求不符！");

            var bytes = new byte[length * 2];
            bool highFirst = byteOrder == PlcStringByteOrder.HighByteFirst;
            for (int i = 0; i < result.Content.Length; i++)
            {
                ushort word = unchecked((ushort)result.Content[i]);
                bytes[i * 2] = (byte)(highFirst ? word >> 8 : word & 0xff);
                bytes[i * 2 + 1] = (byte)(highFirst ? word & 0xff : word >> 8);
            }
            // 奇数字符和定长缓冲区常用零字节补齐，仅去除末尾填充，正文内的零字节及后续字符仍保留。
            int byteCount = bytes.Length;
            while (byteCount > 0 && bytes[byteCount - 1] == 0) byteCount--;
            return OperateResult.CreateSuccessResult(Encoding.ASCII.GetString(bytes, 0, byteCount));
        }
    }
}
