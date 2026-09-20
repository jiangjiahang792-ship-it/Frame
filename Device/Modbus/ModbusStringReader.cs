using System;
using System.Text;

namespace TDJS_Vision.Device.Modbus
{
    /// <summary>基于已有寄存器读取接口提供字符串读取，兼容不同 Modbus 主站实现。</summary>
    public static class ModbusStringReader
    {
        /// <summary>读取连续保持寄存器并按约定编码解码成一个完整字符串。</summary>
        /// <param name="device">提供寄存器读取能力的设备。</param>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="registerCount">寄存器个数，每个寄存器包含两个字节，范围为 1 至 125。</param>
        /// <param name="encodingName">字符编码名称。</param>
        /// <param name="lowByteFirst">是否将每个寄存器的低字节放在前面。</param>
        /// <returns>去除零终止符及后续填充的字符串，保留有效空格。</returns>
        public static string ReadString(this IModbus device, string address, ushort registerCount,
            string encodingName, bool lowByteFirst)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));
            if (registerCount < 1 || registerCount > 125)
                throw new ArgumentOutOfRangeException(nameof(registerCount), "字符串读取寄存器个数必须为 1 至 125。");

            Encoding encoding = GetEncoding(encodingName);
            ushort[] registers = device.ReadUInt16(address, registerCount);
            if (registers == null || registers.Length != registerCount)
                throw new InvalidOperationException("字符串读取返回的寄存器数量不完整。");

            return Decode(registers, encoding, lowByteFirst);
        }

        /// <summary>使用严格解码，避免编码不匹配时悄悄产生错误的图像文件名。</summary>
        /// <param name="encodingName">支持 ASCII、UTF-8、GB18030 和 UTF-16 小端编码。</param>
        /// <returns>遇到无效字节时抛出异常的编码器。</returns>
        internal static Encoding GetEncoding(string encodingName)
        {
            switch (encodingName)
            {
                case "us-ascii":
                case "utf-8":
                case "gb18030":
                case "utf-16":
                    return Encoding.GetEncoding(encodingName, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                default:
                    throw new ArgumentException("不支持的字符串编码。", nameof(encodingName));
            }
        }

        /// <summary>按寄存器内字节顺序展开数据，并在字符边界识别零终止符。</summary>
        /// <param name="registers">按地址递增排列的寄存器。</param>
        /// <param name="encoding">字符编码。</param>
        /// <param name="lowByteFirst">是否低字节在前。</param>
        /// <returns>解码后的完整字符串。</returns>
        internal static string Decode(ushort[] registers, Encoding encoding, bool lowByteFirst)
        {
            byte[] bytes = new byte[registers.Length * 2];
            for (int index = 0; index < registers.Length; index++)
            {
                ushort register = registers[index];
                bytes[index * 2] = (byte)(lowByteFirst ? register : register >> 8);
                bytes[index * 2 + 1] = (byte)(lowByteFirst ? register >> 8 : register);
            }

            // UTF-16 必须按双字节查找终止符，不能把英文字母中的零字节误判为结束。
            int characterWidth = encoding.CodePage == 1200 ? 2 : 1;
            int length = bytes.Length;
            for (int index = 0; index < bytes.Length; index += characterWidth)
            {
                if (bytes[index] == 0 && (characterWidth == 1 || bytes[index + 1] == 0))
                {
                    length = index;
                    break;
                }
            }

            return encoding.GetString(bytes, 0, length);
        }
    }
}
