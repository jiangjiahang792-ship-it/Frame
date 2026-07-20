using System;
using System.Globalization;

namespace TDJS_Vision.Device.Modbus
{
    /// <summary>
    /// Modbus寄存器编解码工具，负责地址解析、站号解析和寄存器数组与业务类型之间的转换。
    /// </summary>
    internal static class ModbusRegisterCodec
    {
        /// <summary>
        /// 默认Modbus站号，保持与旧HSL实现中默认Station=1一致。
        /// </summary>
        private const byte DefaultSlaveAddress = 1;

        /// <summary>
        /// 将界面传入的地址字符串转换为NModbus4使用的零基寄存器地址。
        /// </summary>
        /// <param name="address">地址字符串，支持十进制、0x十六进制，以及类似s=1;100的站号前缀。</param>
        /// <returns>NModbus4零基地址。</returns>
        public static ushort ParseAddress(string address)
        {
            string normalizedAddress = NormalizeAddress(address);

            if (normalizedAddress.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return Convert.ToUInt16(normalizedAddress.Substring(2), 16);

            return ushort.Parse(normalizedAddress, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 获取本次Modbus访问要使用的站号。
        /// </summary>
        /// <param name="param">Modbus设备参数。</param>
        /// <param name="address">地址字符串，可携带s=站号;地址格式的临时站号。</param>
        /// <returns>有效站号。</returns>
        public static byte GetSlaveAddress(IModbusParam param, string address)
        {
            byte? stationFromAddress = TryParseStation(address);
            if (stationFromAddress.HasValue)
                return stationFromAddress.Value;

            ModbusTcpParam tcpParam = param as ModbusTcpParam;
            if (tcpParam != null && tcpParam.ID != 0)
                return tcpParam.ID;

            return DefaultSlaveAddress;
        }

        /// <summary>
        /// 计算指定业务类型对应的寄存器数量。
        /// </summary>
        /// <param name="valueCount">业务值数量。</param>
        /// <param name="registersPerValue">单个业务值占用的寄存器数量。</param>
        /// <returns>总寄存器数量。</returns>
        public static ushort GetRegisterCount(ushort valueCount, int registersPerValue)
        {
            checked
            {
                return (ushort)(valueCount * registersPerValue);
            }
        }

        /// <summary>
        /// 将ushort寄存器数组转换为short数组。
        /// </summary>
        /// <param name="registers">原始保持寄存器数组。</param>
        /// <returns>short数组。</returns>
        public static short[] ToInt16Array(ushort[] registers)
        {
            short[] values = new short[registers.Length];
            for (int i = 0; i < registers.Length; i++)
                values[i] = unchecked((short)registers[i]);
            return values;
        }

        /// <summary>
        /// 将ushort寄存器数组按低字在前的CDAB字序转换为int数组。
        /// </summary>
        /// <param name="registers">原始保持寄存器数组。</param>
        /// <param name="valueCount">要转换的业务值数量。</param>
        /// <returns>int数组。</returns>
        public static int[] ToInt32Array(ushort[] registers, ushort valueCount)
        {
            int[] values = new int[valueCount];
            for (int i = 0; i < valueCount; i++)
                values[i] = unchecked((int)CombineUInt32(registers[i * 2], registers[i * 2 + 1]));
            return values;
        }

        /// <summary>
        /// 将ushort寄存器数组按低字在前的CDAB字序转换为uint数组。
        /// </summary>
        /// <param name="registers">原始保持寄存器数组。</param>
        /// <param name="valueCount">要转换的业务值数量。</param>
        /// <returns>uint数组。</returns>
        public static uint[] ToUInt32Array(ushort[] registers, ushort valueCount)
        {
            uint[] values = new uint[valueCount];
            for (int i = 0; i < valueCount; i++)
                values[i] = CombineUInt32(registers[i * 2], registers[i * 2 + 1]);
            return values;
        }

        /// <summary>
        /// 将ushort寄存器数组按低字在前的CDAB字序转换为float数组。
        /// </summary>
        /// <param name="registers">原始保持寄存器数组。</param>
        /// <param name="valueCount">要转换的业务值数量。</param>
        /// <returns>float数组。</returns>
        public static float[] ToFloatArray(ushort[] registers, ushort valueCount)
        {
            float[] values = new float[valueCount];
            for (int i = 0; i < valueCount; i++)
            {
                uint rawValue = CombineUInt32(registers[i * 2], registers[i * 2 + 1]);
                values[i] = BitConverter.ToSingle(BitConverter.GetBytes(rawValue), 0);
            }
            return values;
        }

        /// <summary>
        /// 将ushort寄存器数组按低字在前的字序转换为long数组。
        /// </summary>
        /// <param name="registers">原始保持寄存器数组。</param>
        /// <param name="valueCount">要转换的业务值数量。</param>
        /// <returns>long数组。</returns>
        public static long[] ToInt64Array(ushort[] registers, ushort valueCount)
        {
            long[] values = new long[valueCount];
            for (int i = 0; i < valueCount; i++)
                values[i] = unchecked((long)CombineUInt64(registers, i * 4));
            return values;
        }

        /// <summary>
        /// 将ushort寄存器数组按低字在前的字序转换为ulong数组。
        /// </summary>
        /// <param name="registers">原始保持寄存器数组。</param>
        /// <param name="valueCount">要转换的业务值数量。</param>
        /// <returns>ulong数组。</returns>
        public static ulong[] ToUInt64Array(ushort[] registers, ushort valueCount)
        {
            ulong[] values = new ulong[valueCount];
            for (int i = 0; i < valueCount; i++)
                values[i] = CombineUInt64(registers, i * 4);
            return values;
        }

        /// <summary>
        /// 将ushort寄存器数组按低字在前的字序转换为double数组。
        /// </summary>
        /// <param name="registers">原始保持寄存器数组。</param>
        /// <param name="valueCount">要转换的业务值数量。</param>
        /// <returns>double数组。</returns>
        public static double[] ToDoubleArray(ushort[] registers, ushort valueCount)
        {
            double[] values = new double[valueCount];
            for (int i = 0; i < valueCount; i++)
            {
                ulong rawValue = CombineUInt64(registers, i * 4);
                values[i] = BitConverter.ToDouble(BitConverter.GetBytes(rawValue), 0);
            }
            return values;
        }

        /// <summary>
        /// 将short数组转换为ushort寄存器数组。
        /// </summary>
        /// <param name="values">short数组。</param>
        /// <returns>ushort寄存器数组。</returns>
        public static ushort[] FromInt16Array(short[] values)
        {
            ushort[] registers = new ushort[values.Length];
            for (int i = 0; i < values.Length; i++)
                registers[i] = unchecked((ushort)values[i]);
            return registers;
        }

        /// <summary>
        /// 将int数组按低字在前的CDAB字序转换为ushort寄存器数组。
        /// </summary>
        /// <param name="values">int数组。</param>
        /// <returns>ushort寄存器数组。</returns>
        public static ushort[] FromInt32Array(int[] values)
        {
            ushort[] registers = new ushort[values.Length * 2];
            for (int i = 0; i < values.Length; i++)
                SplitUInt32(unchecked((uint)values[i]), registers, i * 2);
            return registers;
        }

        /// <summary>
        /// 将uint数组按低字在前的CDAB字序转换为ushort寄存器数组。
        /// </summary>
        /// <param name="values">uint数组。</param>
        /// <returns>ushort寄存器数组。</returns>
        public static ushort[] FromUInt32Array(uint[] values)
        {
            ushort[] registers = new ushort[values.Length * 2];
            for (int i = 0; i < values.Length; i++)
                SplitUInt32(values[i], registers, i * 2);
            return registers;
        }

        /// <summary>
        /// 将float数组按低字在前的CDAB字序转换为ushort寄存器数组。
        /// </summary>
        /// <param name="values">float数组。</param>
        /// <returns>ushort寄存器数组。</returns>
        public static ushort[] FromFloatArray(float[] values)
        {
            ushort[] registers = new ushort[values.Length * 2];
            for (int i = 0; i < values.Length; i++)
            {
                uint rawValue = BitConverter.ToUInt32(BitConverter.GetBytes(values[i]), 0);
                SplitUInt32(rawValue, registers, i * 2);
            }
            return registers;
        }

        /// <summary>
        /// 将long数组按低字在前的字序转换为ushort寄存器数组。
        /// </summary>
        /// <param name="values">long数组。</param>
        /// <returns>ushort寄存器数组。</returns>
        public static ushort[] FromInt64Array(long[] values)
        {
            ushort[] registers = new ushort[values.Length * 4];
            for (int i = 0; i < values.Length; i++)
                SplitUInt64(unchecked((ulong)values[i]), registers, i * 4);
            return registers;
        }

        /// <summary>
        /// 将ulong数组按低字在前的字序转换为ushort寄存器数组。
        /// </summary>
        /// <param name="values">ulong数组。</param>
        /// <returns>ushort寄存器数组。</returns>
        public static ushort[] FromUInt64Array(ulong[] values)
        {
            ushort[] registers = new ushort[values.Length * 4];
            for (int i = 0; i < values.Length; i++)
                SplitUInt64(values[i], registers, i * 4);
            return registers;
        }

        /// <summary>
        /// 将double数组按低字在前的字序转换为ushort寄存器数组。
        /// </summary>
        /// <param name="values">double数组。</param>
        /// <returns>ushort寄存器数组。</returns>
        public static ushort[] FromDoubleArray(double[] values)
        {
            ushort[] registers = new ushort[values.Length * 4];
            for (int i = 0; i < values.Length; i++)
            {
                ulong rawValue = BitConverter.ToUInt64(BitConverter.GetBytes(values[i]), 0);
                SplitUInt64(rawValue, registers, i * 4);
            }
            return registers;
        }

        /// <summary>
        /// 清理地址中的HSL风格前缀，只保留最终地址部分。
        /// </summary>
        /// <param name="address">原始地址。</param>
        /// <returns>纯地址。</returns>
        private static string NormalizeAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("Modbus地址不能为空。", nameof(address));

            string normalizedAddress = address.Trim();
            int separatorIndex = normalizedAddress.LastIndexOf(';');
            if (separatorIndex >= 0)
                normalizedAddress = normalizedAddress.Substring(separatorIndex + 1).Trim();

            if (string.IsNullOrWhiteSpace(normalizedAddress))
                throw new ArgumentException("Modbus地址不能为空。", nameof(address));

            return normalizedAddress;
        }

        /// <summary>
        /// 尝试从地址字符串中解析临时站号。
        /// </summary>
        /// <param name="address">原始地址。</param>
        /// <returns>解析到的站号，未解析到则返回空。</returns>
        private static byte? TryParseStation(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return null;

            string[] segments = address.Split(';');
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i].Trim();
                if (!segment.StartsWith("s=", StringComparison.OrdinalIgnoreCase))
                    continue;

                string stationText = segment.Substring(2).Trim();
                if (byte.TryParse(stationText, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte station) && station != 0)
                    return station;
            }

            return null;
        }

        /// <summary>
        /// 将两个16位寄存器按低字在前组合为32位无符号整数。
        /// </summary>
        /// <param name="lowWord">低16位寄存器。</param>
        /// <param name="highWord">高16位寄存器。</param>
        /// <returns>32位无符号整数。</returns>
        private static uint CombineUInt32(ushort lowWord, ushort highWord)
        {
            return ((uint)highWord << 16) | lowWord;
        }

        /// <summary>
        /// 将四个16位寄存器按低字在前组合为64位无符号整数。
        /// </summary>
        /// <param name="registers">寄存器数组。</param>
        /// <param name="offset">起始偏移。</param>
        /// <returns>64位无符号整数。</returns>
        private static ulong CombineUInt64(ushort[] registers, int offset)
        {
            return ((ulong)registers[offset + 3] << 48)
                | ((ulong)registers[offset + 2] << 32)
                | ((ulong)registers[offset + 1] << 16)
                | registers[offset];
        }

        /// <summary>
        /// 将32位无符号整数拆成低字在前的两个16位寄存器。
        /// </summary>
        /// <param name="value">32位无符号整数。</param>
        /// <param name="registers">输出寄存器数组。</param>
        /// <param name="offset">写入偏移。</param>
        private static void SplitUInt32(uint value, ushort[] registers, int offset)
        {
            registers[offset] = (ushort)(value & 0xFFFF);
            registers[offset + 1] = (ushort)((value >> 16) & 0xFFFF);
        }

        /// <summary>
        /// 将64位无符号整数拆成低字在前的四个16位寄存器。
        /// </summary>
        /// <param name="value">64位无符号整数。</param>
        /// <param name="registers">输出寄存器数组。</param>
        /// <param name="offset">写入偏移。</param>
        private static void SplitUInt64(ulong value, ushort[] registers, int offset)
        {
            registers[offset] = (ushort)(value & 0xFFFF);
            registers[offset + 1] = (ushort)((value >> 16) & 0xFFFF);
            registers[offset + 2] = (ushort)((value >> 32) & 0xFFFF);
            registers[offset + 3] = (ushort)((value >> 48) & 0xFFFF);
        }
    }
}
