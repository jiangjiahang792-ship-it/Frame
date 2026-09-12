using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TDJS_Vision.Node._7_ResultProcessing.ResultSend
{
    /// <summary>发送前执行取值、汇总、严格类型转换及固定容量编码。</summary>
    public static class ResultSendValueConverter
    {
        /// <summary>验证不依赖本轮结果的配置。</summary>
        public static void ValidateConfiguration(ResultSendRow row)
        {
            if (row?.Source == null || row.Source.NodeId <= 0 || string.IsNullOrWhiteSpace(row.Source.Path))
                throw new InvalidOperationException("请选择订阅结果。");
            if (string.IsNullOrWhiteSpace(row.Address)) throw new InvalidOperationException("目标地址不能为空。");
            if (!Enum.IsDefined(typeof(ResultSendMode), row.Mode) || !Enum.IsDefined(typeof(ResultSendType), row.DataType) ||
                !Enum.IsDefined(typeof(ResultSendAggregate), row.Aggregate) ||
                !Enum.IsDefined(typeof(ResultSendSourceKind), row.Source.Kind)) throw new InvalidOperationException("配置包含未知类型。");
            if (row.Source.Kind >= ResultSendSourceKind.AiValues && string.IsNullOrWhiteSpace(row.Source.ItemName))
                throw new InvalidOperationException("请填写AI检测项名称。");
            if (row.Index < 1) throw new InvalidOperationException("明细序号必须从1开始。");
            if (row.Mode == ResultSendMode.All && (row.Capacity < 1 || row.Capacity > 1024 || string.IsNullOrWhiteSpace(row.CountAddress)))
                throw new InvalidOperationException("连续发送容量必须为1至1024，且必须填写数量地址（ushort）。");
            if (row.Mode == ResultSendMode.All && string.Equals(row.Address.Trim(), row.CountAddress.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("数据地址与数量地址不能相同。");
            if (row.DataType == ResultSendType.String)
            {
                if (row.StringBytes < 2 || row.StringBytes > 4096 || row.StringBytes % 2 != 0)
                    throw new InvalidOperationException("字符串字节长度必须为2至4096之间的偶数。");
                GetEncoding(row.EncodingName);
            }
        }

        /// <summary>冻结一个发送行，类型错误和超容量在调用设备前发生。</summary>
        public static ResultSendPreparedRow Prepare(ResultSendRow configuration, List<object> raw, int rowNumber)
        {
            var row = configuration.Copy();
            ValidateConfiguration(row);
            var selected = raw == null ? new List<object>() : new List<object>(raw);
            switch (row.Mode)
            {
                case ResultSendMode.Single:
                    if (selected.Count > 1) throw new InvalidOperationException("直接取值要求恰好一个值，请选择指定项、全部发送或汇总。");
                    if (selected.Count == 0) selected.Add(null);
                    break;
                case ResultSendMode.Index:
                    selected = new List<object> { row.Index > selected.Count ? null : selected[row.Index - 1] };
                    break;
                case ResultSendMode.All:
                    if (selected.Count > row.Capacity) throw new InvalidOperationException("本轮数量超过预留容量，已拒绝截断发送。");
                    break;
                case ResultSendMode.Aggregate:
                    var detected = selected.Where(value => value != null).ToList();
                    selected = new List<object> { detected.Count == 0 ? null : Aggregate(detected, row.Aggregate) };
                    break;
            }
            int count = selected.Count;
            // 零目标也至少写入一个默认值，数量仍为0，避免接收端沿用上轮结果。
            int length = row.Mode == ResultSendMode.All && row.ClearRemaining ? row.Capacity : Math.Max(1, count);
            object missing = GetMissingValue(row.DataType);
            Array values;
            if (row.DataType == ResultSendType.String)
            {
                var bytes = new byte[checked(length * row.StringBytes)];
                var encoding = GetEncoding(row.EncodingName);
                for (int index = 0; index < length; index++)
                {
                    object value = index < count ? selected[index] ?? missing : missing;
                    byte[] item = encoding.GetBytes(Convert.ToString(value, CultureInfo.InvariantCulture));
                    if (item.Length > row.StringBytes) throw new InvalidOperationException("字符串超过固定字节长度，已拒绝截断。");
                    Buffer.BlockCopy(item, 0, bytes, index * row.StringBytes, item.Length);
                }
                values = bytes;
            }
            else
            {
                values = Array.CreateInstance(GetValueType(row.DataType), length);
                for (int index = 0; index < length; index++)
                    values.SetValue(ConvertValue(index < count ? selected[index] ?? missing : missing, row.DataType), index);
            }
            return new ResultSendPreparedRow { RowNumber = rowNumber, Configuration = row, Values = values, Count = checked((ushort)count) };
        }

        /// <summary>未检出时按目标类型使用固定默认值，不增加用户配置项。</summary>
        private static object GetMissingValue(ResultSendType type)
        {
            if (type == ResultSendType.Boolean) return true;
            if (type == ResultSendType.String) return "无效";
            return 0;
        }

        /// <summary>获取严格编码，无法编码的字符不会替换成问号。</summary>
        private static Encoding GetEncoding(string name)
        {
            if (name != "UTF-8" && name != "ASCII" && name != "GB18030") throw new InvalidOperationException("不支持的字符串编码。");
            return Encoding.GetEncoding(name, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        }

        /// <summary>按所选方式合并多个值。</summary>
        private static object Aggregate(List<object> values, ResultSendAggregate mode)
        {
            if (mode == ResultSendAggregate.AllTrue || mode == ResultSendAggregate.AnyTrue)
            {
                var flags = values.Select(Boolean).ToArray();
                return mode == ResultSendAggregate.AllTrue ? flags.All(value => value) : flags.Any(value => value);
            }
            double[] numbers = values.Select(Number).ToArray();
            switch (mode)
            {
                case ResultSendAggregate.Maximum: return numbers.Max();
                case ResultSendAggregate.Minimum: return numbers.Min();
                case ResultSendAggregate.Average: return Finite(numbers.Average());
                default: return Finite(numbers.Sum());
            }
        }

        /// <summary>严格转换成指定基础类型，拒绝小数截断、溢出和非有限数。</summary>
        private static object ConvertValue(object value, ResultSendType type)
        {
            if (type == ResultSendType.Boolean) return Boolean(value);
            if (type == ResultSendType.Double) return Number(value);
            if (type == ResultSendType.Single)
            {
                float number = (float)Number(value);
                if (float.IsInfinity(number) || float.IsNaN(number)) throw new OverflowException("数值超出float范围。");
                return number;
            }
            decimal integer = value is string text ? decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture) : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            if (decimal.Truncate(integer) != integer) throw new InvalidOperationException("小数不能直接写入整数，请先在四则运算中取整。");
            switch (type)
            {
                case ResultSendType.Int16: return checked((short)integer);
                case ResultSendType.UInt16: return checked((ushort)integer);
                case ResultSendType.Int32: return checked((int)integer);
                case ResultSendType.UInt32: return checked((uint)integer);
                default: throw new NotSupportedException("未知写入类型。");
            }
        }

        /// <summary>只将明确的真、假、0、1转换为布尔值。</summary>
        private static bool Boolean(object value)
        {
            if (value is bool flag) return flag;
            string text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
            if (text == "1" || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) || text == "真") return true;
            if (text == "0" || string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) || text == "假") return false;
            throw new FormatException("布尔值只接受true、false、真、假、0、1。");
        }

        /// <summary>转换有限数值，字符串按不带单位的小数处理。</summary>
        private static double Number(object value) => Finite(value is string text ? double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture) : Convert.ToDouble(value, CultureInfo.InvariantCulture));

        /// <summary>拒绝NaN和无穷大。</summary>
        private static double Finite(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new FormatException("发送值必须是有限数字。");
            return value;
        }

        /// <summary>取得目标数组的CLR元素类型。</summary>
        public static Type GetValueType(ResultSendType type)
        {
            switch (type)
            {
                case ResultSendType.Boolean: return typeof(bool);
                case ResultSendType.Int16: return typeof(short);
                case ResultSendType.UInt16: return typeof(ushort);
                case ResultSendType.Int32: return typeof(int);
                case ResultSendType.UInt32: return typeof(uint);
                case ResultSendType.Single: return typeof(float);
                case ResultSendType.Double: return typeof(double);
                case ResultSendType.String: return typeof(byte);
                default: throw new NotSupportedException("未知写入类型。");
            }
        }

        /// <summary>显示用户确认的基础类型名称。</summary>
        public static string GetTypeName(ResultSendType type)
        {
            switch (type)
            {
                case ResultSendType.Boolean: return "bool";
                case ResultSendType.Int16: return "short";
                case ResultSendType.UInt16: return "ushort";
                case ResultSendType.Int32: return "int";
                case ResultSendType.UInt32: return "uint";
                case ResultSendType.Single: return "float";
                case ResultSendType.Double: return "double";
                default: return "string";
            }
        }
    }
}
