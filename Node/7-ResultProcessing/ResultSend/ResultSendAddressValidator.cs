using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TDJS_Vision.Node._7_ResultProcessing.ResultSend
{
    /// <summary>预检已知寄存器地址范围重叠，避免数据区与数量区相互覆盖。</summary>
    public static class ResultSendAddressValidator
    {
        /// <summary>常用十进制PLC寄存器地址；X/Y/B/W等进制取决于协议，不猜测。</summary>
        private static readonly Regex WordAddress = new Regex(@"^(DM|DT|D|R|M)(\d+)(?:\.(\d+))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        /// <summary>在写入前检查所有行的已知地址区间。</summary>
        public static void Validate(List<ResultSendPreparedRow> rows, IResultSendWriter writer)
        {
            var ranges = new List<AddressRange>();
            foreach (var item in rows)
            {
                int reserved = item.Configuration.Mode == ResultSendMode.All ? item.Configuration.Capacity : item.Count;
                int length = item.Configuration.DataType == ResultSendType.String ? checked(reserved * item.Configuration.StringBytes) : reserved;
                AddRange(ranges, item.Configuration.Address, item.Configuration.DataType, length, item.RowNumber, writer is ModbusResultSendWriter);
                if (item.Configuration.Mode == ResultSendMode.All)
                    AddRange(ranges, item.Configuration.CountAddress, ResultSendType.UInt16, 1, item.RowNumber, writer is ModbusResultSendWriter);
            }
        }
        /// <summary>按位为单位比较范围，常见PLC字内位与整字可以正确识别重叠。</summary>
        private static void AddRange(List<AddressRange> ranges, string address, ResultSendType type, int length, int rowNumber, bool modbus)
        {
            string area;
            long start;
            long size;
            if (modbus)
            {
                area = type == ResultSendType.Boolean ? "线圈" : "寄存器";
                start = ushort.Parse(address, CultureInfo.InvariantCulture);
                size = ModbusResultSendWriter.AddressSpan(type, length);
                if (start + size > 65536) throw new ResultSendRowException(rowNumber, new InvalidOperationException("预留容量超过Modbus地址范围。"));
            }
            else
            {
                Match match = WordAddress.Match(address);
                if (!match.Success) return;
                area = match.Groups[1].Value.ToUpperInvariant();
                start = long.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                bool bit = type == ResultSendType.Boolean;
                if (area == "M" || area == "R")
                {
                    if (!bit) return;
                    size = length;
                }
                else
                {
                    start *= 16;
                    if (match.Groups[3].Success)
                    {
                        int index = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                        if (!bit || index > 15) throw new ResultSendRowException(rowNumber, new InvalidOperationException("字内位地址只支持bool且位号为0至15。"));
                        start += index;
                    }
                    size = bit ? length : (long)ModbusResultSendWriter.AddressSpan(type, length) * 16;
                }
            }
            if (size == 0) return;
            foreach (var range in ranges)
                if (range.Area == area && start < range.End && start + size > range.Start)
                    throw new ResultSendRowException(rowNumber, new InvalidOperationException("地址" + address + "与第" + range.Row + "行的数据或数量区域重叠。"));
            ranges.Add(new AddressRange { Area = area, Start = start, End = start + size, Row = rowNumber });
        }
        /// <summary>规范化的地址区间。</summary>
        private sealed class AddressRange
        {
            /// <summary>寄存器区域。</summary>
            public string Area;
            /// <summary>包含的起点。</summary>
            public long Start;
            /// <summary>不包含的终点。</summary>
            public long End;
            /// <summary>所属表格行。</summary>
            public int Row;
        }
    }
}
